using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using System;
using System.Collections;
using System.Collections.Generic;

public class GameOverUI : MonoBehaviour
{
    public static GameOverUI instance;
    [SerializeField] private TextMeshProUGUI recipesDeliveredText;
    [SerializeField] public TextMeshProUGUI scroreTxt;
    [SerializeField] private TextMeshProUGUI recipesDiscardedText;
    [SerializeField] private TextMeshProUGUI wellCookedText;
    [SerializeField] private TextMeshProUGUI overCookedText;
    [SerializeField] private TextMeshProUGUI movesText;
    [SerializeField] private TextMeshProUGUI efficiencyText;
    [SerializeField] private TextMeshProUGUI durationText;
    [SerializeField] private TextMeshProUGUI comboText;
    [SerializeField] private TextMeshProUGUI streakText;
    
    [Header("Loading State")]
    [SerializeField] private GameObject loadingPanel;
    [SerializeField] private TextMeshProUGUI loadingText;
    [SerializeField] private GameObject scorePanel;
    
    [Header("Animation Settings")]
    [SerializeField] private float scoreCountUpDuration = 1.5f;
    [SerializeField] private float statDisplayDelay = 0.3f;
    [SerializeField] private float statAnimationDuration = 0.5f;
    
    [Header("Navigation")]
    [SerializeField] private Button closeButton;

    [Header("Tutorial Timeout Flow")]
    [SerializeField] private float tutorialRedirectDelaySeconds = 5f;
    [SerializeField] private string tutorialTimeoutMessage = "Well done! Redirecting to main menu...";

    // Catchy loading phrases (Overcooked-style)
    private readonly string[] loadingPhrases = {
        "Calculating your culinary score...",
        "Tallying those perfect dishes...",
        "Counting your kitchen mastery...",
        "Computing your chef rating...",
        "Adding up your delicious points...",
        "Measuring your cooking skills...",
        "Finalizing your kitchen performance...",
        "Crunching the numbers on-chain..."
    };

    // Congratulatory messages shown once score is displayed
    private readonly string[] completionPhrases = {
        "Congratulations on the successful cook!",
        "Bravo! You did great!",
        "Bravo! You cooked!",
        "Well done, chef!",
        "Amazing kitchen mastery!",
        "You nailed it!"
    };
    
    private int currentScore = 0;
    private bool isAnimating = false;
    private Coroutine loadingCoroutine;
    private Coroutine scoreAnimationCoroutine;
    private Coroutine tutorialRedirectCoroutine;
    private SessionStats latestStats;
    
    private void Start() {
        if (KitchenGameManager.Instance != null) {
            KitchenGameManager.Instance.OnStateChanged += KitchenGameManager_OnStateChanged;
        } else {
            Debug.LogError("KitchenGameManager.Instance is null in GameStartCountdownUI.Start()");
        }
        
        // Setup close button
        if (closeButton != null) {
            closeButton.onClick.AddListener(OnCloseButtonClicked);
        } else {
            Debug.LogWarning("GameOverUI: Close button not assigned in Inspector. Add a Button and assign it to 'closeButton' field.");
        }
        
        Hide();
        SessionManager.Instance.AssignGameOver(this);
    }

    private void KitchenGameManager_OnStateChanged(object sender, System.EventArgs e) {
        if (KitchenGameManager.Instance.IsGameOver()) {
            Show();
            if (IsTutorialSession()) {
                ShowTutorialTimeoutState();
                return;
            }
            // Start with loading state
            ShowLoadingState();
            
            // Initialization: Set all texts to zero so they don't pop with previous values during the loading screen
            if (recipesDeliveredText != null) recipesDeliveredText.text = "0";
            if (recipesDiscardedText != null) recipesDiscardedText.text = "0";
            if (wellCookedText != null) wellCookedText.text = "0";
            if (overCookedText != null) overCookedText.text = "0";
            if (movesText != null) movesText.text = "0";
            if (comboText != null) comboText.text = "0";
            if (streakText != null) streakText.text = "0";
            if (efficiencyText != null) efficiencyText.text = "0%";
            if (durationText != null) durationText.text = "00:00";
            if (scroreTxt != null) scroreTxt.text = "0";
        } else {
            Hide();
        }
    }

    private void Show() {
        gameObject.SetActive(true);
    }

    private void Hide() {
        gameObject.SetActive(false);
        // Stop any running coroutines
        if (loadingCoroutine != null) {
            StopCoroutine(loadingCoroutine);
            loadingCoroutine = null;
        }
        if (scoreAnimationCoroutine != null) {
            StopCoroutine(scoreAnimationCoroutine);
            scoreAnimationCoroutine = null;
        }
        if (tutorialRedirectCoroutine != null) {
            StopCoroutine(tutorialRedirectCoroutine);
            tutorialRedirectCoroutine = null;
        }
        isAnimating = false;
    }
    
    /// <summary>
    /// Show loading state with rotating catchy phrases
    /// Called when game ends, before score is computed
    /// </summary>
    public void ShowLoadingState() {
        if (loadingPanel != null) {
            loadingPanel.SetActive(true);
        }
        if (scorePanel != null) {
            scorePanel.SetActive(false);
        }
        
        // Start rotating loading phrases only if this component is active/enabled
        if (!isActiveAndEnabled) {
            // Component is inactive (e.g., during application quit); skip coroutine to avoid warnings
            if (loadingText != null && loadingPhrases.Length > 0) {
                loadingText.text = loadingPhrases[0];
            }
            return;
        }

        if (loadingCoroutine != null) {
            StopCoroutine(loadingCoroutine);
        }
        loadingCoroutine = StartCoroutine(RotateLoadingPhrases());
    }
    
    /// <summary>
    /// Called by SessionManager when score is received from blockchain
    /// </summary>
    public void DisplayScore(int finalScore, SessionStats stats) {
        // Preserve local recipe count if blockchain stats are missing or invalid
        int localRecipesDelivered = DeliveryManager.Instance != null 
            ? DeliveryManager.Instance.GetSuccessfulRecipesAmount() 
            : 0;
        
        // Use blockchain stats if available, otherwise create fallback with local count
        if (stats == null) {
            latestStats = new SessionStats {
                recipesDelivered = localRecipesDelivered
            };
        } else {
            latestStats = stats;
            // If blockchain stats show 0 but we have local count, use local count
            // (this handles cases where blockchain hasn't updated yet)
            if (latestStats.recipesDelivered == 0 && localRecipesDelivered > 0) {
                Debug.LogWarning($"⚠️ Blockchain shows 0 recipes delivered, but local count is {localRecipesDelivered}. Using local count.");
                latestStats.recipesDelivered = localRecipesDelivered;
            }
        }
        
        // Stop loading
        if (loadingCoroutine != null) {
            StopCoroutine(loadingCoroutine);
            loadingCoroutine = null;
        }
        
        if (loadingPanel != null) {
            loadingPanel.SetActive(false);
        }
        if (scorePanel != null) {
            scorePanel.SetActive(true);
        }

        // Update bottom message to congratulatory phrase
        if (loadingText != null && completionPhrases.Length > 0) {
            int index = UnityEngine.Random.Range(0, completionPhrases.Length);
            loadingText.text = completionPhrases[index];
        }
        
        // Start score count-up animation
        if (scoreAnimationCoroutine != null) {
            StopCoroutine(scoreAnimationCoroutine);
        }
        scoreAnimationCoroutine = StartCoroutine(AnimateScoreDisplay(finalScore));
    }
    
    /// <summary>
    /// Rotate through catchy loading phrases
    /// </summary>
    private IEnumerator RotateLoadingPhrases() {
        int phraseIndex = 0;
        
        while (true) {
            if (loadingText != null && loadingPhrases.Length > 0) {
                loadingText.text = loadingPhrases[phraseIndex];
                phraseIndex = (phraseIndex + 1) % loadingPhrases.Length;
            }
            yield return new WaitForSeconds(1.5f); // Change phrase every 1.5 seconds
        }
    }
    
    /// <summary>
    /// Animate score counting up and display stats sequentially
    /// </summary>
    private IEnumerator AnimateScoreDisplay(int finalScore) {
        isAnimating = true;
        
        // Step 1: Count up the score
        yield return StartCoroutine(CountUpScore(finalScore));
        
        // Step 2: Display stats one by one with animation
        yield return new WaitForSeconds(statDisplayDelay);
        float betweenDelay = 0.2f;
        
        if (latestStats != null) {
            if (recipesDeliveredText != null && latestStats.recipesDelivered > 0) {
                yield return StartCoroutine(AnimateStat(recipesDeliveredText, latestStats.recipesDelivered));
                yield return new WaitForSeconds(betweenDelay);
            } else if (recipesDeliveredText != null) recipesDeliveredText.text = "0";

            if (recipesDiscardedText != null && latestStats.recipesDiscarded > 0) {
                yield return StartCoroutine(AnimateStat(recipesDiscardedText, latestStats.recipesDiscarded));
                yield return new WaitForSeconds(betweenDelay);
            } else if (recipesDiscardedText != null) recipesDiscardedText.text = "0";

            if (wellCookedText != null && latestStats.wellCooked > 0) {
                yield return StartCoroutine(AnimateStat(wellCookedText, latestStats.wellCooked));
                yield return new WaitForSeconds(betweenDelay);
            } else if (wellCookedText != null) wellCookedText.text = "0";

            if (overCookedText != null && latestStats.overCooked > 0) {
                yield return StartCoroutine(AnimateStat(overCookedText, latestStats.overCooked));
                yield return new WaitForSeconds(betweenDelay);
            } else if (overCookedText != null) overCookedText.text = "0";

            if (movesText != null && latestStats.moves > 0) {
                yield return StartCoroutine(AnimateStat(movesText, latestStats.moves));
                yield return new WaitForSeconds(betweenDelay);
            } else if (movesText != null) movesText.text = "0";

            if (comboText != null && latestStats.combo > 0) {
                yield return StartCoroutine(AnimateStat(comboText, latestStats.combo));
                yield return new WaitForSeconds(betweenDelay);
            } else if (comboText != null) comboText.text = "0";

            if (streakText != null && latestStats.currentStreak > 0) {
                yield return StartCoroutine(AnimateStat(streakText, latestStats.currentStreak));
                yield return new WaitForSeconds(betweenDelay);
            } else if (streakText != null) streakText.text = "0";

            if (efficiencyText != null && latestStats.efficiency > 0) {
                yield return StartCoroutine(AnimateStat(efficiencyText, latestStats.efficiency, "0", "%"));
                yield return new WaitForSeconds(betweenDelay);
            } else if (efficiencyText != null) efficiencyText.text = "0%";

            if (durationText != null && latestStats.duration > 0) {
                yield return StartCoroutine(AnimateDurationStat(durationText, latestStats.duration));
                yield return new WaitForSeconds(betweenDelay);
            } else if (durationText != null) durationText.text = "00:00";
        }
        
        // Play Yay Sound Effect from AudioClipRefsSO
        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlayCelebrateSound();
        }

        isAnimating = false;
    }
    
    /// <summary>
    /// Count up score from 0 to final value
    /// </summary>
    private IEnumerator CountUpScore(int targetScore) {
        if (scroreTxt == null) yield break;
        
        if (MusicManager.Instance != null && targetScore > 0) MusicManager.Instance.ResumeGameScoreMusic();

        float elapsed = 0f;
        int startScore = 0;
        currentScore = startScore;
        
        while (elapsed < scoreCountUpDuration) {
            elapsed += Time.deltaTime;
            float progress = elapsed / scoreCountUpDuration;
            
            // Ease out curve for smooth animation
            progress = 1f - Mathf.Pow(1f - progress, 3f);
            
            currentScore = Mathf.RoundToInt(Mathf.Lerp(startScore, targetScore, progress));
            scroreTxt.text = currentScore.ToString();
            
            yield return null;
        }
        
        if (MusicManager.Instance != null) MusicManager.Instance.PauseGameScoreMusic();

        // Ensure final value is set
        currentScore = targetScore;
        scroreTxt.text = currentScore.ToString();
    }
    
    /// <summary>
    /// Animate a stat text with scale/pop effect
    /// </summary>
    private IEnumerator AnimateStat(TextMeshProUGUI textComponent, float targetValue, string format = "0", string suffix = "") {
        if (textComponent == null) yield break;
        
        if (MusicManager.Instance != null) MusicManager.Instance.ResumeGameScoreMusic();
        
        RectTransform rectTransform = textComponent.rectTransform;
        Vector3 originalScale = rectTransform.localScale;
        
        float elapsed = 0f;
        while (elapsed < statAnimationDuration) {
            elapsed += Time.deltaTime;
            float progress = elapsed / statAnimationDuration;
            
            // Bounce effect
            float scale = 1f + Mathf.Sin(progress * Mathf.PI) * 0.3f;
            rectTransform.localScale = originalScale * scale;
            
            float currentVal = Mathf.Lerp(0, targetValue, progress);
            textComponent.text = currentVal.ToString(format) + suffix;
            
            yield return null;
        }
        
        if (MusicManager.Instance != null) MusicManager.Instance.PauseGameScoreMusic();
        
        // Reset scale
        rectTransform.localScale = originalScale;
        textComponent.text = targetValue.ToString(format) + suffix;
    }

    private IEnumerator AnimateDurationStat(TextMeshProUGUI textComponent, int targetSeconds) {
        if (textComponent == null) yield break;
        
        if (MusicManager.Instance != null) MusicManager.Instance.ResumeGameScoreMusic();
        
        RectTransform rectTransform = textComponent.rectTransform;
        Vector3 originalScale = rectTransform.localScale;
        
        float elapsed = 0f;
        while (elapsed < statAnimationDuration) {
            elapsed += Time.deltaTime;
            float progress = elapsed / statAnimationDuration;
            
            float scale = 1f + Mathf.Sin(progress * Mathf.PI) * 0.3f;
            rectTransform.localScale = originalScale * scale;
            
            int currentSeconds = Mathf.RoundToInt(Mathf.Lerp(0, targetSeconds, progress));
            textComponent.text = FormatDuration(currentSeconds);
            
            yield return null;
        }
        
        if (MusicManager.Instance != null) MusicManager.Instance.PauseGameScoreMusic();
        
        rectTransform.localScale = originalScale;
        textComponent.text = FormatDuration(targetSeconds);
    }

    private bool IsTutorialSession() {
        TutorialManager tutorialManager = FindFirstObjectByType<TutorialManager>();
        return tutorialManager != null && tutorialManager.isActiveAndEnabled;
    }

    private void ShowTutorialTimeoutState() {
        if (loadingCoroutine != null) {
            StopCoroutine(loadingCoroutine);
            loadingCoroutine = null;
        }

        if (scoreAnimationCoroutine != null) {
            StopCoroutine(scoreAnimationCoroutine);
            scoreAnimationCoroutine = null;
        }

        if (tutorialRedirectCoroutine != null) {
            StopCoroutine(tutorialRedirectCoroutine);
        }

        if (loadingPanel != null) {
            loadingPanel.SetActive(true);
        }

        if (scorePanel != null) {
            scorePanel.SetActive(false);
        }

        if (loadingText != null) {
            loadingText.text = tutorialTimeoutMessage;
        }

        if (closeButton != null) {
            closeButton.interactable = false;
        }

        tutorialRedirectCoroutine = StartCoroutine(RedirectToMainMenuAfterTutorialDelay());
    }

    private IEnumerator RedirectToMainMenuAfterTutorialDelay() {
        yield return new WaitForSecondsRealtime(Mathf.Max(4f, tutorialRedirectDelaySeconds));
        Loader.Load(Loader.Scene.MainMenuScene);
    }

    private string FormatDuration(int seconds) {
        if (seconds <= 0) return "00:00";
        var timeSpan = TimeSpan.FromSeconds(seconds);
        if (timeSpan.TotalHours >= 1f) {
            return $"{(int)timeSpan.TotalHours:D2}:{timeSpan.Minutes:D2}:{timeSpan.Seconds:D2}";
        }
        return $"{timeSpan.Minutes:D2}:{timeSpan.Seconds:D2}";
    }
    
    /// <summary>
    /// Legacy method for backward compatibility
    /// Called directly by SessionManager when score is received
    /// </summary>
    public void SetScore(int score) {
        if (isAnimating) {
            // If already animating, just update the target
            if (scoreAnimationCoroutine != null) {
                StopCoroutine(scoreAnimationCoroutine);
            }
            scoreAnimationCoroutine = StartCoroutine(CountUpScore(score));
        } else {
            // Set immediately if not animating
            currentScore = score;
            if (scroreTxt != null) {
                scroreTxt.text = score.ToString();
            }
        }
    }
    
    /// <summary>
    /// Handle close button click - return to main menu
    /// </summary>
    private void OnCloseButtonClicked() {
        Debug.Log("🏠 Returning to main menu...");
        
        // Stop any running animations
        if (loadingCoroutine != null) {
            StopCoroutine(loadingCoroutine);
            loadingCoroutine = null;
        }
        if (scoreAnimationCoroutine != null) {
            StopCoroutine(scoreAnimationCoroutine);
            scoreAnimationCoroutine = null;
        }
        if (tutorialRedirectCoroutine != null) {
            StopCoroutine(tutorialRedirectCoroutine);
            tutorialRedirectCoroutine = null;
        }
        
        // Return to main menu using Loader (consistent with GamePauseUI)
        Loader.Load(Loader.Scene.MainMenuScene);
    }
}