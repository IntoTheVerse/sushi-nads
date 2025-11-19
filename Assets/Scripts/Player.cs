using PinePie.SimpleJoystick;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Player class that implements IKitchenObjectParent interface for handling kitchen objects
public class Player : MonoBehaviour, IKitchenObjectParent
{
    // Singleton pattern implementation to ensure only one player instance exists
    public static Player Instance { get; private set; }

    public event EventHandler OnPickedSomething;

    // Event that fires when the selected counter changes
    public event EventHandler<OnSelectedCounterChangedEventArgs> OnSelectedCounterChanged;
    public class OnSelectedCounterChangedEventArgs : EventArgs {
        public BaseCounter selectedCounter;
    }

    // Serialized fields for Unity Inspector configuration
    [SerializeField] private float moveSpeed = 7f;                    // Legacy speed (used as step speed if grid movement enabled)
    [SerializeField] private GameInput gameInput;                     // Reference to input system
    [SerializeField] private LayerMask countersLayerMask;            // Layer mask for counter detection
    [SerializeField] private Transform kitchenObjectHoldPoint;       // Point where kitchen objects are held

    [SerializeField] public JoystickController playerJoystick;
        
    [Header("Grid Movement Settings")]
    [SerializeField, Range(0f, 1f)] private float gridAxisThreshold = 0.60f;
    [SerializeField] private bool useGridMovement = true;             // Toggle between continuous and grid-based movement
    [SerializeField] private float tileSize = 1.5f;                   // Size of one grid tile (x and z)
    [SerializeField] private Vector3 gridOrigin = Vector3.zero;       // Origin used for snapping to grid centers
    [SerializeField] private float stepMoveSpeed = 10f;               // Units per second when stepping between cells
    [SerializeField] private float holdRepeatInitialDelay = 0.35f;    // Seconds before auto-repeat starts when key is held
    [SerializeField] private float holdRepeatInterval = 0.12f;        // Seconds between repeated steps while held

    // Private state variables
    private bool isWalking;                                          // Tracks if player is currently walking
    private Vector3 lastInteractDir;                                 // Last direction player tried to interact (XZ plane)
    private BaseCounter selectedCounter;                           // Currently selected counter
    private KitchenObject kitchenObject;                            // Currently held kitchen object

    // Grid movement state
    private bool isStepping;
    private Vector3 stepTargetPosition;
    public int StepCount { get; private set; }
    private bool isMoveHeld;
    private Vector2 heldCardinalDir;
    private float holdTimer;
    
    // Move recording throttling (prevent spam)
    private Vector2Int lastRecordedGridCell = new Vector2Int(int.MinValue, int.MinValue);
    private float lastMoveRecordTime = 0f;
    private const float MOVE_RECORD_COOLDOWN = 0.1f; // Minimum seconds between move recordings (very short - just prevents duplicate calls for same position)

    // Awake is called when the script instance is being loaded
    private void Awake()
    {
        // Singleton pattern implementation
        if (Instance != null)
        {
            Debug.LogError("There is more than one Player Instance");
        }
        Instance = this;
    }

    // Start is called before the first frame update
    private void Start() {

        // Subscribe to the interaction event from GameInput
        gameInput.OnInteractAction += GameInput_OnInteraction;
        gameInput.OnInteractAlternateAction += GameInput_OnInteractAlternateAction;
        gameInput.OnMoveStarted += GameInput_OnMoveStarted;
        gameInput.OnMoveCanceled += GameInput_OnMoveCanceled;
        // Initialize last interact direction to current forward so raycasts work from frame 0
        lastInteractDir = new Vector3(transform.forward.x, 0f, transform.forward.z).normalized;
        // Optional: snap player to nearest grid center at start when using grid movement
        if (useGridMovement) {
            transform.position = GetSnappedGridCenter(transform.position);
        }
    }

    private void OnDestroy() {
        if (gameInput != null) {
            gameInput.OnInteractAction -= GameInput_OnInteraction;
            gameInput.OnInteractAlternateAction -= GameInput_OnInteractAlternateAction;
            gameInput.OnMoveStarted -= GameInput_OnMoveStarted;
            gameInput.OnMoveCanceled -= GameInput_OnMoveCanceled;
        }
    }

    // Event handler for interaction input
    public void GameInput_OnInteraction(object sender = null, System.EventArgs e = null){
        if (!KitchenGameManager.Instance.IsGamePlaying()) return;
        if (selectedCounter != null) {
            selectedCounter.Interact(this);
        }
    }

    private void GameInput_OnInteractAlternateAction(object sender, System.EventArgs e){
        if (!KitchenGameManager.Instance.IsGamePlaying()) return;
        
        if (selectedCounter != null) {
            selectedCounter.InteractAlternate(this);
        }
    }

    // Update is called once per frame
    private void Update()
    {
        HandleMovement();        // Handle player movement
        HandleInteractions();    // Handle player interactions
        HandleRotation();        // Ensure eyes/forward face the target counter or interact direction
    }

    // Handles all player interactions with counters
    private void HandleInteractions()
    {
        // Cast ray to detect counters in the last interaction direction
        float interactDistance = 2f;
        RaycastHit raycastHit;
        Vector3 rayDirection = lastInteractDir == Vector3.zero ? new Vector3(transform.forward.x, 0f, transform.forward.z).normalized : lastInteractDir;
        if (Physics.Raycast(transform.position, rayDirection, out raycastHit, interactDistance, countersLayerMask)) {
            // Check if hit object has a BaseCounter component
            if (raycastHit.transform.TryGetComponent(out BaseCounter clearCounter)){
                // Update selected counter if it's different from current
                if (clearCounter != selectedCounter) {
                    SetSelectedCounter(clearCounter);
                }
            } else {
                SetSelectedCounter(null);
            }
        } else {
            SetSelectedCounter(null);  
        }
    }

    // Returns whether the player is currently walking
    public bool IsWalking() {
        return isWalking;
    }
    private Vector2 ReadMoveInput()
    {
        Vector2 v = Vector2.zero;

        if (playerJoystick != null)
            v = new Vector2(playerJoystick.InputDirection.x, playerJoystick.InputDirection.y);

        if (v.magnitude < playerJoystick.deadZone)                 // stick idle fall back
            v = gameInput.GetMovementVectorNormalized();    // keeps keyboard/gamepad alive

        return v.normalized;
    }


    // Handles player movement including collision detection
    private void HandleMovement() {
        if (!useGridMovement) {
            // Fallback: original continuous movement
            Vector2 joystickinputVector = ReadMoveInput();


            Vector2 inputVector = gameInput.GetMovementVectorNormalized();
            Vector3 moveDir = new Vector3(inputVector.x, 0f, inputVector.y);
            float moveDistance = moveSpeed * Time.deltaTime;

            float playerRadius = .7f;
            float playerHeight = 2f;
            bool canMove = !Physics.CapsuleCast(transform.position, transform.position + Vector3.up * playerHeight, playerRadius, moveDir, moveDistance);
            if (!canMove) {
                Vector3 moveDirX = new Vector3(moveDir.x, 0, 0).normalized;
                canMove = moveDir.x != 0 && !Physics.CapsuleCast(transform.position, transform.position + Vector3.up * playerHeight, playerRadius, moveDirX, moveDistance);
                if (canMove) {
                    moveDir = moveDirX;
                } else {
                    Vector3 moveDirZ = new Vector3(0, 0, moveDir.z).normalized;
                    canMove = moveDir.z != 0 && !Physics.CapsuleCast(transform.position, transform.position + Vector3.up * playerHeight, playerRadius, moveDirZ, moveDistance);
                    if (canMove) {
                        moveDir = moveDirZ;
                    }
                }
            }
            if (canMove) {
                transform.position += moveDir * moveSpeed * Time.deltaTime;
            }
            isWalking = moveDir != Vector3.zero;
            if (moveDir != Vector3.zero) {
                lastInteractDir = moveDir;
            }
            return;
        }

        // Read joystick/keyboard and convert to a cardinal when pushed enough (for joystick)
        Vector2 raw = ReadMoveInput();
        Vector2 desiredCardinal = Vector2.zero;

        if (Mathf.Abs(raw.x) > gridAxisThreshold || Mathf.Abs(raw.y) > gridAxisThreshold)
        {
            desiredCardinal = (Mathf.Abs(raw.x) > Mathf.Abs(raw.y))
                ? new Vector2(Mathf.Sign(raw.x), 0f)
                : new Vector2(0f, Mathf.Sign(raw.y));
        }

        // Mirror the keyboard event behavior when using the joystick
        if (desiredCardinal != Vector2.zero)
        {
            if (!isMoveHeld || desiredCardinal != heldCardinalDir)
            {
                heldCardinalDir = desiredCardinal;
                isMoveHeld = true;

                if (!isStepping)
                {
                    TryStartStep(heldCardinalDir);
                    holdTimer = holdRepeatInitialDelay; // use your existing field name
                }
            }
        }
        else
        {
            // only release if keyboard isn't holding either (keyboard events will set/clear isMoveHeld)
            if (!isStepping) isMoveHeld = false;
        }


        // Grid-based step movement
        if (isStepping) {
            Vector3 newPosition = Vector3.MoveTowards(transform.position, stepTargetPosition, stepMoveSpeed * Time.deltaTime);
            transform.position = newPosition;
            isWalking = true;
            if (Vector3.Distance(transform.position, stepTargetPosition) < 0.001f) {
                transform.position = stepTargetPosition; // snap exactly
                isStepping = false;
                isWalking = false;
                StepCount++;
                
                // Record move to blockchain for scoring (with throttling)
                // Only record if:
                // 1. Grid cell actually changed
                // 2. Cooldown has passed
                // 3. Session is active
                // 4. Game is playing
                Vector2Int currentGridCell = GetGridCell(transform.position);
                float currentTime = Time.time;
                bool gridCellChanged = currentGridCell != lastRecordedGridCell;
                bool cooldownPassed = (currentTime - lastMoveRecordTime) >= MOVE_RECORD_COOLDOWN;
                bool sessionActive = SessionManager.Instance != null && SessionManager.Instance.IsSessionActive();
                bool gamePlaying = KitchenGameManager.Instance != null && KitchenGameManager.Instance.IsGamePlaying();
                
                if (gridCellChanged && cooldownPassed && sessionActive && gamePlaying)
                {
                    if (GameplayEventTracker.Instance != null)
                    {
                        GameplayEventTracker.Instance.RecordMove();
                        lastRecordedGridCell = currentGridCell;
                        lastMoveRecordTime = currentTime;
                    }
                }
                
                // If we are in hold-to-repeat mode, trigger next step timing
                if (isMoveHeld) {
                    holdTimer = holdRepeatInterval; // subsequent repeats
                }
            }
            return;
        } else {
            isWalking = false;
            // When not stepping, if key is held, manage repeat timers
            if (isMoveHeld) {
                holdTimer -= Time.deltaTime;
                if (holdTimer <= 0f) {
                    TryStartStep(heldCardinalDir);
                    // If we started stepping, movement loop will handle advancing
                }
            }
        }
    }

    // Ensure player faces toward selected counter or last interact direction
    private void HandleRotation() {
        Vector3 desiredForward;
        if (selectedCounter != null) {
            Vector3 toCounter = selectedCounter.transform.position - transform.position;
            desiredForward = new Vector3(toCounter.x, 0f, toCounter.z).normalized;
        } else {
            desiredForward = lastInteractDir == Vector3.zero ? new Vector3(transform.forward.x, 0f, transform.forward.z).normalized : lastInteractDir;
        }
        if (desiredForward != Vector3.zero) {
            float rotateSpeed = 10f;
            transform.forward = Vector3.Slerp(transform.forward, desiredForward, Time.deltaTime * rotateSpeed);
        }
    }

    private void GameInput_OnMoveStarted(object sender, GameInput.OnMoveEventArgs e) {
        if (!useGridMovement) return;
        if (!KitchenGameManager.Instance.IsGamePlaying()) return;
        // Mark held and setup timers
        isMoveHeld = true;
        heldCardinalDir = e.direction;
        if (!isStepping) {
            // First step instantly on key press
            TryStartStep(heldCardinalDir);
            // Setup initial delay for subsequent repeats
            holdTimer = holdRepeatInitialDelay;
        }
    }

    private void GameInput_OnMoveCanceled(object sender, EventArgs e) {
        if (!useGridMovement) return;
        isMoveHeld = false;
    }

    private void TryStartStep(Vector2 dir) {
        if (isStepping) return;
        // Convert input to world step
        Vector3 stepDir = new Vector3(dir.x, 0f, dir.y);
        if (stepDir == Vector3.zero) return;

        // Update last interact direction immediately so raycast/facing follows input
        lastInteractDir = stepDir;

        Vector3 fromCenter = GetSnappedGridCenter(transform.position);
        Vector3 proposedTarget = fromCenter + new Vector3(stepDir.x * tileSize, 0f, stepDir.z * tileSize);

        // Collision check along the path to the next cell
        float playerRadius = .7f;
        float playerHeight = 2f;
        Vector3 castDir = (proposedTarget - transform.position).normalized;
        float castDistance = Vector3.Distance(transform.position, proposedTarget);
        bool blocked = Physics.CapsuleCast(transform.position, transform.position + Vector3.up * playerHeight, playerRadius, castDir, castDistance);
        if (blocked) {
            return; // Do not start a step into an obstacle
        }

        stepTargetPosition = proposedTarget;
        isStepping = true;
    }

    private Vector3 GetSnappedGridCenter(Vector3 worldPosition) {
        Vector3 relative = worldPosition - gridOrigin;
        float cellX = Mathf.Round(relative.x / tileSize) * tileSize;
        float cellZ = Mathf.Round(relative.z / tileSize) * tileSize;
        return new Vector3(cellX, worldPosition.y, cellZ) + gridOrigin;
    }
    
    /// <summary>
    /// Get the grid cell coordinates (x, z) for a world position
    /// </summary>
    private Vector2Int GetGridCell(Vector3 worldPosition) {
        Vector3 relative = worldPosition - gridOrigin;
        int cellX = Mathf.RoundToInt(relative.x / tileSize);
        int cellZ = Mathf.RoundToInt(relative.z / tileSize);
        return new Vector2Int(cellX, cellZ);
    }
    
    /// <summary>
    /// Reset move tracking (call when session starts/ends to prevent stale data)
    /// </summary>
    public void ResetMoveTracking() {
        lastRecordedGridCell = new Vector2Int(int.MinValue, int.MinValue);
        lastMoveRecordTime = 0f;
    }

    // Updates the selected counter and triggers the change event
    private void SetSelectedCounter(BaseCounter selectedCounter) {
        this.selectedCounter = selectedCounter;

        OnSelectedCounterChanged?.Invoke(this, new OnSelectedCounterChangedEventArgs {
            selectedCounter = selectedCounter
        });
    }

    // IKitchenObjectParent interface implementation
    public Transform GetKitchenObjectFollowTransform()
    {
        return kitchenObjectHoldPoint;
    }

    public void SetKitchenObject(KitchenObject kitchenObject)
    {
        this.kitchenObject = kitchenObject;
        if (kitchenObject != null) {
            OnPickedSomething?.Invoke(this, EventArgs.Empty);
        }
    }

    public KitchenObject GetKitchenObject()
    {
        return kitchenObject;
    }

    public void ClearKitchenObject()
    {
        kitchenObject = null;
    }

    public bool HasKitchenObject()
    {
        return kitchenObject != null;
    }
}
