using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// GameInput class handles all input processing for the game using the new Unity Input System
public class GameInput : MonoBehaviour
{
    public static GameInput Instance { get; private set; }
    // Event that other classes can subscribe to for interaction input
    public event EventHandler OnInteractAction; 
    public event EventHandler OnInteractAlternateAction;
    public event EventHandler OnPauseAction;
    // Fires once when movement input starts (useful for grid step movement)
    public event EventHandler<OnMoveEventArgs> OnMoveStarted;
    public event EventHandler OnMoveCanceled;

    public class OnMoveEventArgs : EventArgs {
        public Vector2 direction; // Cardinal direction: (-1,0), (1,0), (0,-1), (0,1)
    }
    // Reference to the PlayerInputActions asset (generated from Input Actions)
    private PlayerInputActions playerInputActions;

    // Called when the script instance is being loaded
    private void Awake() {
        Instance = this;
        // Initialize the PlayerInputActions
        playerInputActions = new PlayerInputActions();
        // Enable the Player action map
        playerInputActions.Player.Enable();
        // Subscribe to the Interact action's performed event
        playerInputActions.Player.Interact.performed += Interact_performed;
        playerInputActions.Player.InteractAlternate.performed += InteractAlternate_performed;
        playerInputActions.Player.Pause.performed += Pause_performed;
        // Subscribe to Move started for step-based movement
        playerInputActions.Player.Move.started += Move_started;
        playerInputActions.Player.Move.canceled += Move_canceled;
    }

    private void OnDestroy() {
        playerInputActions.Player.Interact.performed -= Interact_performed;
        playerInputActions.Player.InteractAlternate.performed -= InteractAlternate_performed;
        playerInputActions.Player.Pause.performed -= Pause_performed;
        playerInputActions.Player.Move.started -= Move_started;
        playerInputActions.Player.Move.canceled -= Move_canceled;
        playerInputActions.Dispose();
    }

    // Callback method that gets triggered when the Interact action is performed

    private void InteractAlternate_performed(UnityEngine.InputSystem.InputAction.CallbackContext obj) {
        // Using null-conditional operator to safely invoke the event if there are subscribers

        OnInteractAlternateAction?.Invoke(this, EventArgs.Empty);
    }

    // Callback method that gets triggered when the Interact action is performed
    private void Interact_performed(UnityEngine.InputSystem.InputAction.CallbackContext obj) {
        // Using null-conditional operator to safely invoke the event if there are subscribers
        OnInteractAction?.Invoke(this, EventArgs.Empty);
    }

    // Fires once when Move action begins (from zero to non-zero)
    private void Move_started(UnityEngine.InputSystem.InputAction.CallbackContext context) {
        Vector2 rawDir = context.ReadValue<Vector2>();
        if (rawDir == Vector2.zero) return;

        // Convert to cardinal direction
        Vector2 cardinal;
        if (Mathf.Abs(rawDir.x) > Mathf.Abs(rawDir.y)) {
            cardinal = new Vector2(Mathf.Sign(rawDir.x), 0f);
        } else {
            cardinal = new Vector2(0f, Mathf.Sign(rawDir.y));
        }

        OnMoveStarted?.Invoke(this, new OnMoveEventArgs { direction = cardinal });
    }

    private void Move_canceled(UnityEngine.InputSystem.InputAction.CallbackContext context) {
        OnMoveCanceled?.Invoke(this, EventArgs.Empty);
    }

    // Returns the normalized movement input vector (magnitude of 1)
    public Vector2 GetMovementVectorNormalized() {
        // Read the raw input value from the Move action
        Vector2 inputVector = playerInputActions.Player.Move.ReadValue<Vector2>();
        // Normalize the vector to ensure consistent movement speed in all directions
        inputVector = inputVector.normalized;
        return inputVector;
    }   

    private void Pause_performed(UnityEngine.InputSystem.InputAction.CallbackContext obj) {
        // Using null-conditional operator to safely invoke the event if there are subscribers
        OnPauseAction?.Invoke(this, EventArgs.Empty);
    }
}
