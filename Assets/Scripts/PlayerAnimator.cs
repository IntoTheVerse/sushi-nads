using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerAnimator : MonoBehaviour
{private Vector2 moveDir;

    private const string IS_WALKING = "IsWalking";
    [SerializeField] private Player player;
    private Animator animator;

    private void Awake() {
        animator = GetComponent<Animator>();
    }

    private void Update() {
         bool walking = player.IsWalking();
         Debug.Log(walking);
         animator.SetBool(IS_WALKING, walking);
    }
public bool IsWalking() {
    return moveDir.sqrMagnitude > 0.01f; // or velocity.sqrMagnitude > 0.01f
}
}
