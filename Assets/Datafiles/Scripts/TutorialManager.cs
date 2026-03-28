using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TutorialManager : MonoBehaviour
{
    private enum StepCompletionMode
    {
        None,
        Timed,
        HoldIngredient,
        HoldPlate,
        PlacePlateOnCounter,
        ProduceIngredient,
        PlaceIngredientOnPlate,
        AssembleRecipeOnPlate,
        DeliverRecipe
    }

    private enum StepTargetType
    {
        None,
        UI,
        World
    }

    private enum ProcessStepType
    {
        Cook,
        Cut
    }

    private sealed class TutorialStep
    {
        public string Prompt;
        public StepTargetType TargetType;
        public RectTransform UiTarget;
        public Transform WorldTarget;
        public BaseCounter TargetCounter;

        public StepCompletionMode CompletionMode;
        public float TimedDurationSeconds;
        public float ElapsedUnscaledSeconds;
        public KitchenObjectSO RequiredIngredient;
        public RecipeSO RequiredRecipe;
        public int RequiredSuccessfulRecipesMin;
        public int RequiredPlateIngredientCountMin;
        public bool RequireFriedStateOnStove;
    }

    private struct ProcessRecipeLink
    {
        public ProcessStepType StepType;
        public KitchenObjectSO Input;
        public KitchenObjectSO Output;
        public BaseCounter Counter;
    }

    private static readonly BindingFlags PrivateInstanceFlags = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly Vector3[] RectCorners = new Vector3[4];
    private static FieldInfo cuttingRecipesField;
    private static FieldInfo fryingRecipesField;
    private static FieldInfo containerIngredientField;
    private static FieldInfo counterHighlighterCounterField;
    private static FieldInfo platesCounterPlateKitchenObjectSOField;
    private static FieldInfo plateVariantRulesField;

    [Header("Start")]
    [SerializeField] private bool forceStartInEditor;
    [SerializeField] private bool pauseGameDuringTutorial;

    [Header("Overlay")]
    [SerializeField] private GameObject tutorialOverlayRoot;
    [SerializeField] private TextMeshProUGUI promptText;
    [SerializeField] private RectTransform handPointer;
    [SerializeField] private Button nextButton;

    [Header("Pointer")]
    [SerializeField] private Canvas pointerCanvas;
    [SerializeField] private Camera worldCamera;
    [SerializeField] private Vector2 handOffset = new Vector2(90f, -50f);
    [SerializeField] private float worldTargetYOffset = 1.1f;
    [SerializeField] private bool autoPlaceHandNearTarget = true;
    [SerializeField] private float handTargetPadding = 24f;
    [SerializeField] private float handScreenPadding = 12f;
    [SerializeField] private Vector2 worldTargetVirtualSize = new Vector2(120f, 120f);

    [Header("Pointer Pulse")]
    [SerializeField] private bool enableHandPulse = true;
    [SerializeField] private float handPulseCyclesPerSecond = 1.8f;
    [SerializeField] [Range(0f, 0.9f)] private float handPulseAmount = 0.12f;

    [Header("Step Targets")]
    [SerializeField] private RectTransform priorityOrderTargetUI;
    [SerializeField] private RectTransform timerTargetUI;
    [SerializeField] private float priorityOrderPromptDurationSeconds = 5f;
    [SerializeField] private float timerPromptDurationSeconds = 5f;
    [SerializeField] private float introStepDurationSeconds = 3f;

    private readonly List<TutorialStep> steps = new List<TutorialStep>();
    private readonly List<NextCounterHighlighterManager> disabledCounterHintManagers = new List<NextCounterHighlighterManager>();
    private readonly Dictionary<KitchenObjectSO, KitchenObjectSO> variantOutputToInputMap = new Dictionary<KitchenObjectSO, KitchenObjectSO>();
    private TutorialStep currentStep;
    private CounterHighlighter currentStepCounterHighlighter;
    private BaseCounter trackedPlateCounter;
    private bool isTutorialRunning;
    private bool pausedByTutorial;
    private float cachedTimeScale = 1f;
    private int currentStepIndex = -1;
    private Vector3 handBaseScale = Vector3.one;
    private bool hasHandBaseScale;
    private bool hasBuiltVariantOutputMap;

#if UNITY_WEBGL || UNITY_STANDALONE || UNITY_EDITOR
    private const string PCText_Interact = " (Press E)";
    private const string PCText_Cut = " (Press E to pick/place, then press F to cut)";
#else
    private const string PCText_Interact = "";
    private const string PCText_Cut = "";
#endif

    private void Awake()
    {
        GameSceneLaunchMode launchMode = GameSceneLaunchContext.ConsumeMode();
        bool shouldRunTutorial = forceStartInEditor || launchMode == GameSceneLaunchMode.Tutorial;

        if (!shouldRunTutorial)
        {
            if (tutorialOverlayRoot != null)
            {
                tutorialOverlayRoot.SetActive(false);
            }
            enabled = false;
            return;
        }

        if (worldCamera == null)
        {
            worldCamera = Camera.main;
        }

        if (pointerCanvas == null && handPointer != null)
        {
            pointerCanvas = handPointer.GetComponentInParent<Canvas>();
        }

        CacheHandBaseScale();

        if (priorityOrderTargetUI == null)
        {
            DeliveryManagerUI deliveryUI = FindFirstObjectByType<DeliveryManagerUI>();
            if (deliveryUI != null)
            {
                priorityOrderTargetUI = deliveryUI.GetComponent<RectTransform>();
            }
        }

        if (timerTargetUI == null)
        {
            GamePlayingClockUI timerUI = FindFirstObjectByType<GamePlayingClockUI>();
            if (timerUI != null)
            {
                timerTargetUI = timerUI.GetComponent<RectTransform>();
            }
        }

        if (tutorialOverlayRoot != null)
        {
            tutorialOverlayRoot.SetActive(false);
        }

        if (nextButton != null)
        {
            nextButton.gameObject.SetActive(false);
        }
    }

    private void Start()
    {
        StartCoroutine(BeginTutorialRoutine());
    }

    private void OnDisable()
    {
        HideCurrentCounterHighlight();
        RestoreDynamicCounterHighlighters();
        ResetHandScale();
        ResumeTimeIfNeeded();
    }

    private void Update()
    {
        if (!isTutorialRunning)
        {
            return;
        }

        if (ShouldShowHandPointer())
        {
            PositionHandForCurrentStep();
        }
        else if (handPointer != null && handPointer.gameObject.activeSelf)
        {
            handPointer.gameObject.SetActive(false);
        }

        AnimateHandPointer();

        if (IsCurrentStepComplete())
        {
            AdvanceStep();
        }
    }

    private IEnumerator BeginTutorialRoutine()
    {
        while (DeliveryManager.Instance == null)
        {
            yield return null;
        }

        // Let DeliveryManager run once so the first priority order can spawn before freezing time.
        yield return null;

        BuildTutorialSteps();
        if (steps.Count == 0)
        {
            enabled = false;
            yield break;
        }

        isTutorialRunning = true;
        currentStepIndex = -1;

        if (pauseGameDuringTutorial)
        {
            PauseTimeForTutorial();
        }

        DisableDynamicCounterHighlighters();

        if (tutorialOverlayRoot != null)
        {
            tutorialOverlayRoot.SetActive(true);
        }

        AdvanceStep();
    }

    private void BuildTutorialSteps()
    {
        steps.Clear();
        trackedPlateCounter = null;

#if UNITY_WEBGL || UNITY_STANDALONE || UNITY_EDITOR
        AddUIStep(
            "Use W, A, S, D to move your chef around the kitchen.",
            null,
            introStepDurationSeconds
        );
#endif

        AddUIStep(
            "Priority orders appear here. Complete the top order first, then the next order becomes priority.",
            priorityOrderTargetUI,
            priorityOrderPromptDurationSeconds
        );

        AddUIStep(
            "This is your game timer. Finish as many correct dishes as possible before it reaches zero.",
            timerTargetUI,
            timerPromptDurationSeconds
        );

        AddPriorityOrderCounterSteps();

        if (steps.Count == 0)
        {
            steps.Add(new TutorialStep
            {
                Prompt = "Tutorial setup is missing references. Assign targets in TutorialManager and try again.",
                TargetType = StepTargetType.None,
                CompletionMode = StepCompletionMode.Timed,
                TimedDurationSeconds = introStepDurationSeconds
            });
        }
    }

    private void AddPriorityOrderCounterSteps()
    {
        if (DeliveryManager.Instance == null)
        {
            return;
        }

        List<RecipeSO> waitingRecipes = DeliveryManager.Instance.GetWaitingRecipeSOList();
        if (waitingRecipes == null || waitingRecipes.Count == 0 || waitingRecipes[0] == null)
        {
            steps.Add(new TutorialStep
            {
                Prompt = "No priority order spawned yet. Wait for an order, then follow the required counters.",
                TargetType = StepTargetType.None,
                CompletionMode = StepCompletionMode.Timed,
                TimedDurationSeconds = introStepDurationSeconds
            });
            return;
        }

        RecipeSO priorityRecipe = waitingRecipes[0];
        List<ProcessRecipeLink> recipeLinks = BuildSceneRecipeLinks();
        AddPlateSetupSteps();

        for (int i = 0; i < priorityRecipe.kitchenObjectSOList.Count; i++)
        {
            KitchenObjectSO targetIngredient = priorityRecipe.kitchenObjectSOList[i];
            if (targetIngredient == null)
            {
                continue;
            }

            KitchenObjectSO preparationIngredient = ResolvePreparationIngredient(targetIngredient);
            AddFlowStepsForIngredient(preparationIngredient, i + 1, priorityRecipe.kitchenObjectSOList.Count, recipeLinks);
        }

        AddDeliveryStep(priorityRecipe);
    }

    private void AddPlateSetupSteps()
    {
        PlatesCounter plateCounter = FindPlateCounter();
        BaseCounter emptyCounter = FindPreferredEmptyCounterForPlate();

        if (plateCounter != null)
        {
            AddWorldStep(
                "Pick a plate from this plate counter." + PCText_Interact,
                plateCounter.transform,
                plateCounter,
                StepCompletionMode.HoldPlate,
                null
            );
        }
        else
        {
            steps.Add(new TutorialStep
            {
                Prompt = "Pick a plate from the plate counter." + PCText_Interact,
                TargetType = StepTargetType.None,
                CompletionMode = StepCompletionMode.HoldPlate
            });
        }

        if (emptyCounter != null)
        {
            AddWorldStep(
                "Place the plate on this empty counter." + PCText_Interact,
                emptyCounter.transform,
                emptyCounter,
                StepCompletionMode.PlacePlateOnCounter,
                null
            );
        }
        else
        {
            steps.Add(new TutorialStep
            {
                Prompt = "Place the plate on an empty counter." + PCText_Interact,
                TargetType = StepTargetType.None,
                CompletionMode = StepCompletionMode.PlacePlateOnCounter
            });
        }
    }

    private void AddAssemblyStep(RecipeSO priorityRecipe)
    {
        PlatesCounter platesCounter = FindPlateCounter();
        if (platesCounter == null)
        {
            steps.Add(new TutorialStep
            {
                Prompt = "Now assemble all prepared ingredients on a plate. Add the base first (Nori Roll or Cone)." + PCText_Interact,
                TargetType = StepTargetType.None,
                CompletionMode = StepCompletionMode.AssembleRecipeOnPlate,
                RequiredRecipe = priorityRecipe
            });
            AddDeliveryStep(priorityRecipe);
            return;
        }

        AddWorldStep(
            "Now assemble all prepared ingredients on a plate. Add the base first (Nori Roll or Cone)." + PCText_Interact,
            platesCounter.transform,
            platesCounter,
            StepCompletionMode.AssembleRecipeOnPlate,
            null,
            false,
            priorityRecipe
        );

        AddDeliveryStep(priorityRecipe);
    }

    private void AddDeliveryStep(RecipeSO priorityRecipe)
    {
        DeliveryCounter deliveryCounter = FindDeliveryCounter();
        if (deliveryCounter == null)
        {
            steps.Add(new TutorialStep
            {
                Prompt = "Great! Now deliver the completed plate at the delivery counter." + PCText_Interact,
                TargetType = StepTargetType.None,
                CompletionMode = StepCompletionMode.DeliverRecipe,
                RequiredRecipe = priorityRecipe
            });
            return;
        }

        AddWorldStep(
            "Great! Now deliver the completed plate at this delivery counter." + PCText_Interact,
            deliveryCounter.transform,
            deliveryCounter,
            StepCompletionMode.DeliverRecipe,
            null,
            false,
            priorityRecipe
        );
    }

    private void AddFlowStepsForIngredient(
        KitchenObjectSO targetIngredient,
        int ingredientIndex,
        int ingredientCount,
        List<ProcessRecipeLink> recipeLinks)
    {
        if (TryResolveIngredientFlow(targetIngredient, recipeLinks, new HashSet<KitchenObjectSO>(), out KitchenObjectSO rawIngredient, out List<ProcessRecipeLink> flow))
        {
            ContainerCounter rawCounter = FindContainerCounter(rawIngredient);
            if (rawCounter != null)
            {
                AddWorldStep(
                    $"Ingredient {ingredientIndex}/{ingredientCount}: Take {GetDisplayName(rawIngredient)} from this counter." + PCText_Interact,
                    rawCounter.transform,
                    rawCounter,
                    StepCompletionMode.HoldIngredient,
                    rawIngredient
                );
            }

            for (int i = 0; i < flow.Count; i++)
            {
                ProcessRecipeLink link = flow[i];
                if (link.Counter == null)
                {
                    continue;
                }

                if (link.StepType == ProcessStepType.Cook)
                {
                    AddWorldStep(
                        $"Cook {GetDisplayName(link.Input)} here to make {GetDisplayName(link.Output)}." + PCText_Interact,
                        link.Counter.transform,
                        link.Counter,
                        StepCompletionMode.ProduceIngredient,
                        link.Output,
                        true
                    );
                }
                else
                {
                    AddWorldStep(
                        $"Cut {GetDisplayName(link.Input)} here to make {GetDisplayName(link.Output)}." + PCText_Cut,
                        link.Counter.transform,
                        link.Counter,
                        StepCompletionMode.ProduceIngredient,
                        link.Output
                    );
                }
            }

            AddPlaceIngredientOnPlateStep(targetIngredient, ingredientIndex, ingredientCount);
            return;
        }

        if (TryFindDirectProducer(targetIngredient, out BaseCounter directCounter, out string actionText))
        {
            AddWorldStep(
                $"Ingredient {ingredientIndex}/{ingredientCount}: {actionText} for {GetDisplayName(targetIngredient)}.",
                directCounter.transform,
                directCounter,
                StepCompletionMode.ProduceIngredient,
                targetIngredient
            );
            AddPlaceIngredientOnPlateStep(targetIngredient, ingredientIndex, ingredientCount);
            return;
        }

        steps.Add(new TutorialStep
        {
            Prompt = $"Ingredient {ingredientIndex}/{ingredientCount}: Could not find counter flow for {GetDisplayName(targetIngredient)}. Check counter recipe setup.",
            TargetType = StepTargetType.None,
            CompletionMode = StepCompletionMode.Timed,
            TimedDurationSeconds = introStepDurationSeconds
        });
    }

    private void AddPlaceIngredientOnPlateStep(KitchenObjectSO ingredient, int ingredientIndex, int ingredientCount)
    {
        BaseCounter plateCounter = ResolveTrackedPlateCounter();
        string prompt = $"Ingredient {ingredientIndex}/{ingredientCount}: Place the ingredient on the plate.";

        if (plateCounter != null)
        {
            AddWorldStep(
                prompt,
                plateCounter.transform,
                plateCounter,
                StepCompletionMode.PlaceIngredientOnPlate,
                ingredient
            );
            return;
        }

        steps.Add(new TutorialStep
        {
            Prompt = prompt,
            TargetType = StepTargetType.None,
            CompletionMode = StepCompletionMode.PlaceIngredientOnPlate,
            RequiredIngredient = ingredient
        });
    }

    private bool TryResolveIngredientFlow(
        KitchenObjectSO target,
        List<ProcessRecipeLink> recipeLinks,
        HashSet<KitchenObjectSO> visited,
        out KitchenObjectSO rawIngredient,
        out List<ProcessRecipeLink> flow)
    {
        flow = new List<ProcessRecipeLink>();
        rawIngredient = null;

        if (target == null)
        {
            return false;
        }

        if (FindContainerCounter(target) != null)
        {
            rawIngredient = target;
            return true;
        }

        if (!visited.Add(target))
        {
            return false;
        }

        for (int i = 0; i < recipeLinks.Count; i++)
        {
            ProcessRecipeLink link = recipeLinks[i];
            if (link.Output != target)
            {
                continue;
            }

            if (TryResolveIngredientFlow(link.Input, recipeLinks, visited, out KitchenObjectSO resolvedRaw, out List<ProcessRecipeLink> subFlow))
            {
                subFlow.Add(link);
                flow = subFlow;
                rawIngredient = resolvedRaw;
                visited.Remove(target);
                return true;
            }
        }

        visited.Remove(target);
        return false;
    }

    private List<ProcessRecipeLink> BuildSceneRecipeLinks()
    {
        List<ProcessRecipeLink> links = new List<ProcessRecipeLink>();

        if (cuttingRecipesField == null)
        {
            cuttingRecipesField = typeof(CuttingCounter).GetField("cuttingRecipeSOArray", PrivateInstanceFlags);
        }

        if (fryingRecipesField == null)
        {
            fryingRecipesField = typeof(StoveCounter).GetField("fryingRecipeSOArray", PrivateInstanceFlags);
        }

        CuttingCounter[] cuttingCounters = FindObjectsByType<CuttingCounter>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < cuttingCounters.Length; i++)
        {
            CuttingCounter counter = cuttingCounters[i];
            if (counter == null || cuttingRecipesField == null)
            {
                continue;
            }

            CuttingRecipeSO[] recipes = cuttingRecipesField.GetValue(counter) as CuttingRecipeSO[];
            if (recipes == null)
            {
                continue;
            }

            for (int recipeIndex = 0; recipeIndex < recipes.Length; recipeIndex++)
            {
                CuttingRecipeSO recipe = recipes[recipeIndex];
                if (recipe == null || recipe.input == null || recipe.output == null)
                {
                    continue;
                }

                links.Add(new ProcessRecipeLink
                {
                    StepType = ProcessStepType.Cut,
                    Input = recipe.input,
                    Output = recipe.output,
                    Counter = counter
                });
            }
        }

        StoveCounter[] stoveCounters = FindObjectsByType<StoveCounter>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < stoveCounters.Length; i++)
        {
            StoveCounter counter = stoveCounters[i];
            if (counter == null || fryingRecipesField == null)
            {
                continue;
            }

            FryingRecipeSO[] recipes = fryingRecipesField.GetValue(counter) as FryingRecipeSO[];
            if (recipes == null)
            {
                continue;
            }

            for (int recipeIndex = 0; recipeIndex < recipes.Length; recipeIndex++)
            {
                FryingRecipeSO recipe = recipes[recipeIndex];
                if (recipe == null || recipe.input == null || recipe.output == null)
                {
                    continue;
                }

                links.Add(new ProcessRecipeLink
                {
                    StepType = ProcessStepType.Cook,
                    Input = recipe.input,
                    Output = recipe.output,
                    Counter = counter
                });
            }
        }

        return links;
    }

    private ContainerCounter FindContainerCounter(KitchenObjectSO ingredient)
    {
        if (ingredient == null)
        {
            return null;
        }

        if (containerIngredientField == null)
        {
            containerIngredientField = typeof(ContainerCounter).GetField("kitchenObjectSO", PrivateInstanceFlags);
        }

        ContainerCounter[] counters = FindObjectsByType<ContainerCounter>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < counters.Length; i++)
        {
            ContainerCounter counter = counters[i];
            if (counter == null || containerIngredientField == null)
            {
                continue;
            }

            KitchenObjectSO sourceIngredient = containerIngredientField.GetValue(counter) as KitchenObjectSO;
            if (sourceIngredient == ingredient)
            {
                return counter;
            }
        }

        return null;
    }

    private bool TryFindDirectProducer(KitchenObjectSO output, out BaseCounter counter, out string actionText)
    {
        counter = null;
        actionText = "Use this counter";

        if (output == null)
        {
            return false;
        }

        CuttingCounter[] cuttingCounters = FindObjectsByType<CuttingCounter>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < cuttingCounters.Length; i++)
        {
            CuttingCounter cuttingCounter = cuttingCounters[i];
            if (cuttingCounter != null && cuttingCounter.CanProduce(output))
            {
                counter = cuttingCounter;
                actionText = "Cut ingredient here";
                return true;
            }
        }

        StoveCounter[] stoveCounters = FindObjectsByType<StoveCounter>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < stoveCounters.Length; i++)
        {
            StoveCounter stoveCounter = stoveCounters[i];
            if (stoveCounter != null && stoveCounter.CanProduce(output))
            {
                counter = stoveCounter;
                actionText = "Cook ingredient here";
                return true;
            }
        }

        return false;
    }

    private PlatesCounter FindPlateCounter()
    {
        PlatesCounter[] plateCounters = FindObjectsByType<PlatesCounter>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (plateCounters == null || plateCounters.Length == 0)
        {
            return null;
        }

        return plateCounters[0];
    }

    private BaseCounter FindPreferredEmptyCounterForPlate()
    {
        ClearCounter[] clearCounters = FindObjectsByType<ClearCounter>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < clearCounters.Length; i++)
        {
            ClearCounter counter = clearCounters[i];
            if (counter == null || counter.HasKitchenObject())
            {
                continue;
            }

            return counter;
        }

        return null;
    }

    private DeliveryCounter FindDeliveryCounter()
    {
        DeliveryCounter[] deliveryCounters = FindObjectsByType<DeliveryCounter>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (deliveryCounters == null || deliveryCounters.Length == 0)
        {
            return null;
        }

        return deliveryCounters[0];
    }

    private KitchenObjectSO ResolvePreparationIngredient(KitchenObjectSO requestedIngredient)
    {
        if (requestedIngredient == null)
        {
            return null;
        }

        BuildVariantOutputInputMapIfNeeded();
        if (variantOutputToInputMap.TryGetValue(requestedIngredient, out KitchenObjectSO mappedIngredient) && mappedIngredient != null)
        {
            return mappedIngredient;
        }

        return requestedIngredient;
    }

    private void BuildVariantOutputInputMapIfNeeded()
    {
        if (hasBuiltVariantOutputMap)
        {
            return;
        }

        hasBuiltVariantOutputMap = true;
        variantOutputToInputMap.Clear();

        PlateKitchenObject platePrefab = GetPlateKitchenObjectPrefab();
        if (platePrefab == null)
        {
            return;
        }

        if (plateVariantRulesField == null)
        {
            plateVariantRulesField = typeof(PlateKitchenObject).GetField("variantRules", PrivateInstanceFlags);
        }

        IList rules = plateVariantRulesField != null ? plateVariantRulesField.GetValue(platePrefab) as IList : null;
        if (rules == null || rules.Count == 0)
        {
            return;
        }

        for (int i = 0; i < rules.Count; i++)
        {
            object boxedRule = rules[i];
            if (boxedRule == null)
            {
                continue;
            }

            System.Type ruleType = boxedRule.GetType();
            FieldInfo inputField = ruleType.GetField("inputSO");
            FieldInfo outputField = ruleType.GetField("outputSO");
            if (inputField == null || outputField == null)
            {
                continue;
            }

            KitchenObjectSO input = inputField.GetValue(boxedRule) as KitchenObjectSO;
            KitchenObjectSO output = outputField.GetValue(boxedRule) as KitchenObjectSO;
            if (input == null || output == null || output == input)
            {
                continue;
            }

            if (!variantOutputToInputMap.ContainsKey(output))
            {
                variantOutputToInputMap.Add(output, input);
            }
        }
    }

    private PlateKitchenObject GetPlateKitchenObjectPrefab()
    {
        PlatesCounter platesCounter = FindPlateCounter();
        if (platesCounter == null)
        {
            return null;
        }

        if (platesCounterPlateKitchenObjectSOField == null)
        {
            platesCounterPlateKitchenObjectSOField = typeof(PlatesCounter).GetField("plateKitchenObjectSO", PrivateInstanceFlags);
        }

        KitchenObjectSO plateSO = platesCounterPlateKitchenObjectSOField != null
            ? platesCounterPlateKitchenObjectSOField.GetValue(platesCounter) as KitchenObjectSO
            : null;
        if (plateSO == null || plateSO.prefab == null)
        {
            return null;
        }

        PlateKitchenObject plateOnRoot = plateSO.prefab.GetComponent<PlateKitchenObject>();
        if (plateOnRoot != null)
        {
            return plateOnRoot;
        }

        return plateSO.prefab.GetComponentInChildren<PlateKitchenObject>(true);
    }

    private void AdvanceStep()
    {
        if (!isTutorialRunning)
        {
            return;
        }

        currentStepIndex++;
        if (currentStepIndex >= steps.Count)
        {
            CompleteTutorial();
            return;
        }

        currentStep = steps[currentStepIndex];
        currentStep.ElapsedUnscaledSeconds = 0f;
        if (currentStep.CompletionMode == StepCompletionMode.DeliverRecipe)
        {
            currentStep.RequiredSuccessfulRecipesMin = DeliveryManager.Instance != null
                ? DeliveryManager.Instance.GetSuccessfulRecipesAmount() + 1
                : 1;
        }
        else if (currentStep.CompletionMode == StepCompletionMode.PlaceIngredientOnPlate)
        {
            BaseCounter plateCounter = ResolveTrackedPlateCounter();
            if (plateCounter != null)
            {
                currentStep.TargetType = StepTargetType.World;
                currentStep.TargetCounter = plateCounter;
                currentStep.WorldTarget = plateCounter.transform;
            }

            currentStep.RequiredPlateIngredientCountMin = GetIngredientCountOnCounterPlate(plateCounter) + 1;
        }

        if (promptText != null)
        {
            promptText.text = currentStep.Prompt;
        }

        ApplyStepVisuals();
        if (ShouldShowHandPointer())
        {
            PositionHandForCurrentStep();
        }
        else if (handPointer != null && handPointer.gameObject.activeSelf)
        {
            handPointer.gameObject.SetActive(false);
        }
    }

    private void CompleteTutorial()
    {
        isTutorialRunning = false;
        currentStep = null;
        HideCurrentCounterHighlight();
        RestoreDynamicCounterHighlighters();
        trackedPlateCounter = null;

        if (tutorialOverlayRoot != null)
        {
            tutorialOverlayRoot.SetActive(false);
        }

        ResetHandScale();

        ResumeTimeIfNeeded();
    }

    private void PauseTimeForTutorial()
    {
        if (pausedByTutorial)
        {
            return;
        }

        cachedTimeScale = Time.timeScale;
        if (cachedTimeScale > 0f)
        {
            Time.timeScale = 0f;
            pausedByTutorial = true;
        }
    }

    private void ResumeTimeIfNeeded()
    {
        if (!pausedByTutorial)
        {
            return;
        }

        Time.timeScale = cachedTimeScale <= 0f ? 1f : cachedTimeScale;
        pausedByTutorial = false;
    }

    private void PositionHandForCurrentStep()
    {
        if (handPointer == null || currentStep == null)
        {
            return;
        }

        Vector2 localPoint;
        Rect targetRect;
        bool validTarget = false;

        if (currentStep.TargetType == StepTargetType.UI && currentStep.UiTarget != null)
        {
            validTarget = TryGetUiTargetPoint(currentStep.UiTarget, out localPoint, out targetRect);
        }
        else if (currentStep.TargetType == StepTargetType.World && currentStep.WorldTarget != null)
        {
            validTarget = TryGetWorldTargetPoint(currentStep.WorldTarget, out localPoint, out targetRect);
        }
        else
        {
            localPoint = Vector2.zero;
            targetRect = new Rect(0f, 0f, 0f, 0f);
        }

        handPointer.gameObject.SetActive(validTarget);
        if (validTarget)
        {
            RectTransform handParent = handPointer.parent as RectTransform;
            Vector2 finalPosition = localPoint + handOffset;

            if (autoPlaceHandNearTarget && handParent != null)
            {
                finalPosition = GetBestHandPlacement(handParent, targetRect) + handOffset;
            }

            if (handParent != null)
            {
                finalPosition = ClampToParentRect(handParent, finalPosition);
            }

            handPointer.anchoredPosition = finalPosition;
        }
    }

    private bool TryGetUiTargetPoint(RectTransform target, out Vector2 localPoint, out Rect targetRect)
    {
        localPoint = Vector2.zero;
        targetRect = new Rect(0f, 0f, 0f, 0f);
        if (target == null || handPointer == null)
        {
            return false;
        }

        RectTransform handParent = handPointer.parent as RectTransform;
        if (handParent == null)
        {
            return false;
        }

        target.GetWorldCorners(RectCorners);
        Vector3 centerWorld = (RectCorners[0] + RectCorners[2]) * 0.5f;
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(GetUICamera(), centerWorld);

        bool gotCenter = RectTransformUtility.ScreenPointToLocalPointInRectangle(
            handParent,
            screenPoint,
            GetUICamera(),
            out localPoint
        );

        if (!gotCenter)
        {
            return false;
        }

        Vector2 localCornerMin;
        Vector2 localCornerMax;

        Vector2 cornerMinScreen = RectTransformUtility.WorldToScreenPoint(GetUICamera(), RectCorners[0]);
        Vector2 cornerMaxScreen = RectTransformUtility.WorldToScreenPoint(GetUICamera(), RectCorners[2]);

        bool gotMin = RectTransformUtility.ScreenPointToLocalPointInRectangle(handParent, cornerMinScreen, GetUICamera(), out localCornerMin);
        bool gotMax = RectTransformUtility.ScreenPointToLocalPointInRectangle(handParent, cornerMaxScreen, GetUICamera(), out localCornerMax);
        if (!gotMin || !gotMax)
        {
            return false;
        }

        float xMin = Mathf.Min(localCornerMin.x, localCornerMax.x);
        float xMax = Mathf.Max(localCornerMin.x, localCornerMax.x);
        float yMin = Mathf.Min(localCornerMin.y, localCornerMax.y);
        float yMax = Mathf.Max(localCornerMin.y, localCornerMax.y);
        targetRect = Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        return true;
    }

    private bool TryGetWorldTargetPoint(Transform target, out Vector2 localPoint, out Rect targetRect)
    {
        localPoint = Vector2.zero;
        targetRect = new Rect(0f, 0f, 0f, 0f);
        if (target == null || handPointer == null)
        {
            return false;
        }

        Camera cam = worldCamera != null ? worldCamera : Camera.main;
        if (cam == null)
        {
            return false;
        }

        RectTransform handParent = handPointer.parent as RectTransform;
        if (handParent == null)
        {
            return false;
        }

        Vector3 worldPosition = target.position + Vector3.up * worldTargetYOffset;
        Vector3 screenPoint = cam.WorldToScreenPoint(worldPosition);
        if (screenPoint.z <= 0f)
        {
            return false;
        }

        Vector2 clampedScreenPoint = new Vector2(
            Mathf.Clamp(screenPoint.x, 16f, Screen.width - 16f),
            Mathf.Clamp(screenPoint.y, 16f, Screen.height - 16f)
        );

        bool gotPoint = RectTransformUtility.ScreenPointToLocalPointInRectangle(
            handParent,
            clampedScreenPoint,
            GetUICamera(),
            out localPoint
        );

        if (!gotPoint)
        {
            return false;
        }

        targetRect = new Rect(
            localPoint.x - (worldTargetVirtualSize.x * 0.5f),
            localPoint.y - (worldTargetVirtualSize.y * 0.5f),
            worldTargetVirtualSize.x,
            worldTargetVirtualSize.y
        );
        return true;
    }

    private Vector2 GetBestHandPlacement(RectTransform handParent, Rect targetRect)
    {
        Vector2 handHalfSize = GetHandHalfSize();
        Vector2 center = targetRect.center;

        Vector2[] candidates =
        {
            new Vector2(targetRect.xMax + handHalfSize.x + handTargetPadding, center.y), // right
            new Vector2(targetRect.xMin - handHalfSize.x - handTargetPadding, center.y), // left
            new Vector2(center.x, targetRect.yMax + handHalfSize.y + handTargetPadding), // top
            new Vector2(center.x, targetRect.yMin - handHalfSize.y - handTargetPadding), // bottom
        };

        for (int i = 0; i < candidates.Length; i++)
        {
            if (IsInsideParent(handParent.rect, candidates[i], handHalfSize))
            {
                return candidates[i];
            }
        }

        return ClampToParentRect(handParent, candidates[0]);
    }

    private Vector2 ClampToParentRect(RectTransform handParent, Vector2 anchoredPosition)
    {
        Rect parentRect = handParent.rect;
        Vector2 handHalfSize = GetHandHalfSize();

        float minX = parentRect.xMin + handHalfSize.x + handScreenPadding;
        float maxX = parentRect.xMax - handHalfSize.x - handScreenPadding;
        float minY = parentRect.yMin + handHalfSize.y + handScreenPadding;
        float maxY = parentRect.yMax - handHalfSize.y - handScreenPadding;

        return new Vector2(
            Mathf.Clamp(anchoredPosition.x, minX, maxX),
            Mathf.Clamp(anchoredPosition.y, minY, maxY)
        );
    }

    private bool IsInsideParent(Rect parentRect, Vector2 anchoredPosition, Vector2 handHalfSize)
    {
        float minX = parentRect.xMin + handHalfSize.x + handScreenPadding;
        float maxX = parentRect.xMax - handHalfSize.x - handScreenPadding;
        float minY = parentRect.yMin + handHalfSize.y + handScreenPadding;
        float maxY = parentRect.yMax - handHalfSize.y - handScreenPadding;

        return anchoredPosition.x >= minX && anchoredPosition.x <= maxX && anchoredPosition.y >= minY && anchoredPosition.y <= maxY;
    }

    private Vector2 GetHandHalfSize()
    {
        if (handPointer == null)
        {
            return new Vector2(32f, 32f);
        }

        Vector3 scaleSource = hasHandBaseScale ? handBaseScale : handPointer.localScale;
        Vector2 scale = new Vector2(Mathf.Abs(scaleSource.x), Mathf.Abs(scaleSource.y));
        Vector2 size = Vector2.Scale(handPointer.rect.size, scale);
        if (size.x <= 0f || size.y <= 0f)
        {
            return new Vector2(32f, 32f);
        }

        return size * 0.5f;
    }

    private Camera GetUICamera()
    {
        if (pointerCanvas == null || pointerCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            return null;
        }

        return pointerCanvas.worldCamera;
    }

    private bool WasTapPressedThisFrame()
    {
        if (Input.GetMouseButtonDown(0))
        {
            return true;
        }

        if (Input.touchCount <= 0)
        {
            return false;
        }

        return Input.GetTouch(0).phase == TouchPhase.Began;
    }

    private void AddUIStep(string prompt, RectTransform target, float durationSeconds)
    {
        steps.Add(new TutorialStep
        {
            Prompt = prompt,
            TargetType = target == null ? StepTargetType.None : StepTargetType.UI,
            UiTarget = target,
            CompletionMode = StepCompletionMode.Timed,
            TimedDurationSeconds = Mathf.Max(0.1f, durationSeconds)
        });
    }

    private void AddWorldStep(
        string prompt,
        Transform target,
        BaseCounter targetCounter,
        StepCompletionMode completionMode,
        KitchenObjectSO requiredIngredient,
        bool requireFriedStateOnStove = false)
        => AddWorldStep(prompt, target, targetCounter, completionMode, requiredIngredient, requireFriedStateOnStove, null);

    private void AddWorldStep(
        string prompt,
        Transform target,
        BaseCounter targetCounter,
        StepCompletionMode completionMode,
        KitchenObjectSO requiredIngredient,
        bool requireFriedStateOnStove,
        RecipeSO requiredRecipe)
    {
        steps.Add(new TutorialStep
        {
            Prompt = prompt,
            TargetType = target == null ? StepTargetType.None : StepTargetType.World,
            WorldTarget = target,
            TargetCounter = targetCounter,
            CompletionMode = completionMode,
            RequiredIngredient = requiredIngredient,
            RequiredRecipe = requiredRecipe,
            RequireFriedStateOnStove = requireFriedStateOnStove
        });
    }

    private static string GetDisplayName(KitchenObjectSO kitchenObjectSO)
    {
        if (kitchenObjectSO == null)
        {
            return "ingredient";
        }

        if (!string.IsNullOrWhiteSpace(kitchenObjectSO.objectName))
        {
            return kitchenObjectSO.objectName;
        }

        return kitchenObjectSO.name;
    }

    private void AnimateHandPointer()
    {
        if (handPointer == null || !handPointer.gameObject.activeSelf)
        {
            return;
        }

        if (!enableHandPulse)
        {
            ResetHandScale();
            return;
        }

        CacheHandBaseScale();
        float wave = Mathf.Sin(Time.unscaledTime * handPulseCyclesPerSecond * Mathf.PI * 2f);
        float pulseScale = 1f + (wave * handPulseAmount);
        handPointer.localScale = handBaseScale * pulseScale;
    }

    private bool ShouldShowHandPointer()
    {
        if (currentStep == null)
        {
            return false;
        }

        return currentStep.TargetType == StepTargetType.UI && currentStep.UiTarget != null;
    }

    private bool IsCurrentStepComplete()
    {
        if (!isTutorialRunning || currentStep == null)
        {
            return false;
        }

        switch (currentStep.CompletionMode)
        {
            case StepCompletionMode.Timed:
                currentStep.ElapsedUnscaledSeconds += Time.unscaledDeltaTime;
                return currentStep.ElapsedUnscaledSeconds >= currentStep.TimedDurationSeconds;

            case StepCompletionMode.HoldIngredient:
                return IsIngredientHeldByPlayer(currentStep.RequiredIngredient);

            case StepCompletionMode.HoldPlate:
                return IsPlateHeldByPlayer();

            case StepCompletionMode.PlacePlateOnCounter:
                return HasPlacedPlateOnCounter(currentStep);

            case StepCompletionMode.ProduceIngredient:
                return HasProducedRequiredIngredient(currentStep);

            case StepCompletionMode.PlaceIngredientOnPlate:
                return HasPlacedIngredientOnPlate(currentStep);

            case StepCompletionMode.AssembleRecipeOnPlate:
                return HasAssembledRecipeOnPlate(currentStep.RequiredRecipe);

            case StepCompletionMode.DeliverRecipe:
                return HasDeliveredRecipe(currentStep);

            case StepCompletionMode.None:
            default:
                return false;
        }
    }

    private bool IsPlateHeldByPlayer()
    {
        if (Player.Instance == null || !Player.Instance.HasKitchenObject())
        {
            return false;
        }

        KitchenObject heldObject = Player.Instance.GetKitchenObject();
        return heldObject != null && heldObject.TryGetPlate(out _);
    }

    private bool IsIngredientHeldByPlayer(KitchenObjectSO requiredIngredient)
    {
        if (requiredIngredient == null || Player.Instance == null || !Player.Instance.HasKitchenObject())
        {
            return false;
        }

        KitchenObject heldObject = Player.Instance.GetKitchenObject();
        if (heldObject == null)
        {
            return false;
        }

        if (heldObject.GetKitchenObjectSO() == requiredIngredient)
        {
            return true;
        }

        if (heldObject.TryGetPlate(out PlateKitchenObject plate))
        {
            return plate.GetKitchenObjectSOList().Contains(requiredIngredient);
        }

        return false;
    }

    private bool HasProducedRequiredIngredient(TutorialStep step)
    {
        if (step == null || step.RequiredIngredient == null)
        {
            return false;
        }

        if (step.TargetCounter != null && step.TargetCounter.HasKitchenObject())
        {
            KitchenObject onCounter = step.TargetCounter.GetKitchenObject();
            if (onCounter != null && onCounter.GetKitchenObjectSO() == step.RequiredIngredient)
            {
                if (!step.RequireFriedStateOnStove)
                {
                    return true;
                }

                StoveCounter stoveCounter = step.TargetCounter as StoveCounter;
                if (stoveCounter == null || stoveCounter.GetState() == StoveCounter.State.Fried)
                {
                    return true;
                }
            }
        }

        return IsIngredientHeldByPlayer(step.RequiredIngredient);
    }

    private bool HasPlacedPlateOnCounter(TutorialStep step)
    {
        if (IsPlateHeldByPlayer())
        {
            return false;
        }

        if (step != null && step.TargetCounter != null && TryGetPlateOnCounter(step.TargetCounter, out _))
        {
            trackedPlateCounter = step.TargetCounter;
            return true;
        }

        if (TryFindAnyPlateCounterPlacement(out BaseCounter plateCounter))
        {
            trackedPlateCounter = plateCounter;
            return true;
        }

        return false;
    }

    private bool HasPlacedIngredientOnPlate(TutorialStep step)
    {
        if (step == null)
        {
            return false;
        }

        BaseCounter plateCounter = ResolveTrackedPlateCounter();
        if (!TryGetPlateOnCounter(plateCounter, out PlateKitchenObject plate))
        {
            return false;
        }

        List<KitchenObjectSO> ingredients = plate.GetKitchenObjectSOList();
        int ingredientCount = ingredients != null ? ingredients.Count : 0;
        if (ingredientCount < Mathf.Max(1, step.RequiredPlateIngredientCountMin))
        {
            return false;
        }

        if (step.RequiredIngredient == null)
        {
            return true;
        }

        return PlateContainsIngredientOrResolvedVariant(plate, step.RequiredIngredient);
    }

    private BaseCounter ResolveTrackedPlateCounter()
    {
        if (trackedPlateCounter != null && TryGetPlateOnCounter(trackedPlateCounter, out _))
        {
            return trackedPlateCounter;
        }

        if (TryFindAnyPlateCounterPlacement(out BaseCounter foundCounter))
        {
            trackedPlateCounter = foundCounter;
            return foundCounter;
        }

        trackedPlateCounter = null;
        return null;
    }

    private int GetIngredientCountOnCounterPlate(BaseCounter counter)
    {
        if (!TryGetPlateOnCounter(counter, out PlateKitchenObject plate))
        {
            return 0;
        }

        List<KitchenObjectSO> ingredients = plate.GetKitchenObjectSOList();
        return ingredients != null ? ingredients.Count : 0;
    }

    private bool TryGetPlateOnCounter(BaseCounter counter, out PlateKitchenObject plate)
    {
        plate = null;
        if (counter == null || !counter.HasKitchenObject())
        {
            return false;
        }

        KitchenObject objectOnCounter = counter.GetKitchenObject();
        if (objectOnCounter == null || !objectOnCounter.TryGetPlate(out PlateKitchenObject resolvedPlate))
        {
            return false;
        }

        plate = resolvedPlate;
        return true;
    }

    private bool TryFindAnyPlateCounterPlacement(out BaseCounter plateCounter)
    {
        plateCounter = null;
        ClearCounter[] counters = FindObjectsByType<ClearCounter>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < counters.Length; i++)
        {
            ClearCounter counter = counters[i];
            if (counter == null)
            {
                continue;
            }

            if (TryGetPlateOnCounter(counter, out _))
            {
                plateCounter = counter;
                return true;
            }
        }

        return false;
    }

    private bool PlateContainsIngredientOrResolvedVariant(PlateKitchenObject plate, KitchenObjectSO requiredIngredient)
    {
        if (plate == null || requiredIngredient == null)
        {
            return false;
        }

        BuildVariantOutputInputMapIfNeeded();
        List<KitchenObjectSO> plateIngredients = plate.GetKitchenObjectSOList();
        if (plateIngredients == null)
        {
            return false;
        }

        for (int i = 0; i < plateIngredients.Count; i++)
        {
            KitchenObjectSO ingredient = plateIngredients[i];
            if (ingredient == null)
            {
                continue;
            }

            if (ingredient == requiredIngredient)
            {
                return true;
            }

            if (variantOutputToInputMap.TryGetValue(ingredient, out KitchenObjectSO mappedInput) && mappedInput == requiredIngredient)
            {
                return true;
            }
        }

        return false;
    }

    private bool HasAssembledRecipeOnPlate(RecipeSO recipe)
    {
        if (recipe == null || recipe.kitchenObjectSOList == null || recipe.kitchenObjectSOList.Count == 0)
        {
            return false;
        }

        if (Player.Instance != null && Player.Instance.HasKitchenObject())
        {
            KitchenObject held = Player.Instance.GetKitchenObject();
            if (held != null && held.TryGetPlate(out PlateKitchenObject heldPlate) && DoesPlateMatchRecipe(heldPlate, recipe))
            {
                return true;
            }
        }

        PlateKitchenObject[] allPlates = FindObjectsByType<PlateKitchenObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < allPlates.Length; i++)
        {
            PlateKitchenObject plate = allPlates[i];
            if (plate == null)
            {
                continue;
            }

            if (DoesPlateMatchRecipe(plate, recipe))
            {
                return true;
            }
        }

        return false;
    }

    private bool DoesPlateMatchRecipe(PlateKitchenObject plate, RecipeSO recipe)
    {
        if (plate == null || recipe == null || recipe.kitchenObjectSOList == null)
        {
            return false;
        }

        List<KitchenObjectSO> plateIngredients = plate.GetKitchenObjectSOList();
        if (plateIngredients == null || plateIngredients.Count != recipe.kitchenObjectSOList.Count)
        {
            return false;
        }

        Dictionary<KitchenObjectSO, int> counts = new Dictionary<KitchenObjectSO, int>();
        for (int i = 0; i < plateIngredients.Count; i++)
        {
            KitchenObjectSO ingredient = plateIngredients[i];
            if (ingredient == null)
            {
                continue;
            }

            counts.TryGetValue(ingredient, out int currentCount);
            counts[ingredient] = currentCount + 1;
        }

        for (int i = 0; i < recipe.kitchenObjectSOList.Count; i++)
        {
            KitchenObjectSO needed = recipe.kitchenObjectSOList[i];
            if (needed == null)
            {
                continue;
            }

            if (!counts.TryGetValue(needed, out int count) || count <= 0)
            {
                return false;
            }

            counts[needed] = count - 1;
        }

        return true;
    }

    private bool HasDeliveredRecipe(TutorialStep step)
    {
        if (step == null || DeliveryManager.Instance == null)
        {
            return false;
        }

        return DeliveryManager.Instance.GetSuccessfulRecipesAmount() >= Mathf.Max(1, step.RequiredSuccessfulRecipesMin);
    }

    private void ApplyStepVisuals()
    {
        HideCurrentCounterHighlight();

        if (currentStep == null || currentStep.TargetType != StepTargetType.World)
        {
            return;
        }

        BaseCounter targetCounter = currentStep.TargetCounter;
        if (targetCounter == null && currentStep.WorldTarget != null)
        {
            targetCounter = currentStep.WorldTarget.GetComponent<BaseCounter>();
        }

        if (targetCounter == null)
        {
            return;
        }

        CounterHighlighter highlighter = GetOrCreateCounterHighlighter(targetCounter);
        if (highlighter == null)
        {
            return;
        }

        currentStepCounterHighlighter = highlighter;
        currentStepCounterHighlighter.ShowHighlight();
    }

    private void HideCurrentCounterHighlight()
    {
        if (currentStepCounterHighlighter == null)
        {
            return;
        }

        currentStepCounterHighlighter.HideHighlight();
        currentStepCounterHighlighter = null;
    }

    private CounterHighlighter GetOrCreateCounterHighlighter(BaseCounter counter)
    {
        if (counter == null)
        {
            return null;
        }

        CounterHighlighter highlighter = counter.GetComponent<CounterHighlighter>();
        if (highlighter != null)
        {
            return highlighter;
        }

        highlighter = counter.gameObject.AddComponent<CounterHighlighter>();
        if (counterHighlighterCounterField == null)
        {
            counterHighlighterCounterField = typeof(CounterHighlighter).GetField("baseCounter", PrivateInstanceFlags);
        }
        if (counterHighlighterCounterField != null)
        {
            counterHighlighterCounterField.SetValue(highlighter, counter);
        }

        return highlighter;
    }

    private void DisableDynamicCounterHighlighters()
    {
        disabledCounterHintManagers.Clear();
        NextCounterHighlighterManager[] managers = FindObjectsByType<NextCounterHighlighterManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < managers.Length; i++)
        {
            NextCounterHighlighterManager manager = managers[i];
            if (manager == null || !manager.enabled)
            {
                continue;
            }

            manager.enabled = false;
            disabledCounterHintManagers.Add(manager);
        }
    }

    private void RestoreDynamicCounterHighlighters()
    {
        for (int i = 0; i < disabledCounterHintManagers.Count; i++)
        {
            NextCounterHighlighterManager manager = disabledCounterHintManagers[i];
            if (manager == null)
            {
                continue;
            }

            manager.enabled = true;
        }

        disabledCounterHintManagers.Clear();
    }

    private void CacheHandBaseScale()
    {
        if (handPointer == null || hasHandBaseScale)
        {
            return;
        }

        handBaseScale = handPointer.localScale;
        hasHandBaseScale = true;
    }

    private void ResetHandScale()
    {
        if (handPointer == null)
        {
            return;
        }

        CacheHandBaseScale();
        handPointer.localScale = handBaseScale;
    }
}
