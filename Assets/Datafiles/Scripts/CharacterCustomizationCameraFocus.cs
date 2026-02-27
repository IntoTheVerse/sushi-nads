using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

public class CharacterCustomizationCameraFocus : MonoBehaviour
{
    [Serializable]
    public class FocusView
    {
        [Tooltip("Trait slot name, e.g. Backpack, Glass, Hat, Eye, Mouth, Outfit")]
        public string slotName;
        [Tooltip("Follow target for this trait focus. Usually an anchor Transform on the character.")]
        public Transform followTarget;
        [Tooltip("Look-at target for this trait focus. If empty, follow target is used.")]
        public Transform lookAtTarget;
        [Tooltip("Third Person Follow camera distance for this view.")]
        public float cameraDistance = -2.5f;
        [Tooltip("Third Person Follow vertical arm length for this view.")]
        public float verticalArmLength = 1.7f;
        [Tooltip("Third Person Follow side offset for this view.")]
        public float cameraSide = 0f;
        [Tooltip("Blend duration to this view.")]
        public float blendDuration = 0.35f;
    }

    [Header("Camera")]
    [SerializeField] private CinemachineCamera cinematicCamera;
    [SerializeField] private bool autoFindCamera = true;
    [SerializeField] private bool debugLogs;

    [Header("Default View")]
    [SerializeField] private Transform defaultFollowTarget;
    [SerializeField] private Transform defaultLookAtTarget;
    [SerializeField] private float defaultCameraDistance = -2.5f;
    [SerializeField] private float defaultVerticalArmLength = 1.7f;
    [SerializeField] private float defaultCameraSide = 0f;
    [SerializeField] private float defaultBlendDuration = 0.3f;

    [Header("Trait Views")]
    [SerializeField] private List<FocusView> focusViews = new List<FocusView>();

    private readonly Dictionary<string, FocusView> viewBySlot = new Dictionary<string, FocusView>(StringComparer.OrdinalIgnoreCase);
    private CinemachineThirdPersonFollow thirdPersonFollow;
    private Coroutine blendRoutine;

    private void Awake()
    {
        ResolveCamera();
        BuildViewMap();
        CacheDefaultsFromCamera();
    }

    [ContextMenu("Focus Default")]
    public void FocusDefault()
    {
        if (!HasCamera()) return;
        ApplyView(
            defaultFollowTarget,
            defaultLookAtTarget,
            defaultCameraDistance,
            defaultVerticalArmLength,
            defaultCameraSide,
            defaultBlendDuration
        );
    }

    public void FocusSlot(string slotName)
    {
        if (!HasCamera()) return;

        string normalizedSlot = NormalizeSlotName(slotName);
        if (string.IsNullOrEmpty(normalizedSlot)) return;

        if (!viewBySlot.TryGetValue(normalizedSlot, out var view) || view == null)
        {
            if (debugLogs)
            {
                Debug.Log($"[CharacterCustomizationCameraFocus] No view configured for slot '{normalizedSlot}'.");
            }
            return;
        }

        ApplyView(
            view.followTarget,
            view.lookAtTarget,
            view.cameraDistance,
            view.verticalArmLength,
            view.cameraSide,
            view.blendDuration
        );
    }

    private bool HasCamera()
    {
        if (cinematicCamera != null) return true;
        ResolveCamera();
        return cinematicCamera != null;
    }

    private void ResolveCamera()
    {
        if (cinematicCamera == null)
        {
            cinematicCamera = GetComponent<CinemachineCamera>();
        }

        if (cinematicCamera == null && autoFindCamera)
        {
            cinematicCamera = FindFirstObjectByType<CinemachineCamera>();
        }

        if (cinematicCamera != null)
        {
            thirdPersonFollow = cinematicCamera.GetComponent<CinemachineThirdPersonFollow>();
        }

        if (debugLogs)
        {
            string cameraName = cinematicCamera != null ? cinematicCamera.name : "null";
            string followName = thirdPersonFollow != null ? thirdPersonFollow.name : "null";
            Debug.Log($"[CharacterCustomizationCameraFocus] ResolveCamera camera={cameraName}, tpf={followName}");
        }
    }

    private void CacheDefaultsFromCamera()
    {
        if (cinematicCamera == null) return;

        if (defaultFollowTarget == null)
        {
            defaultFollowTarget = cinematicCamera.Follow;
        }

        if (defaultLookAtTarget == null)
        {
            defaultLookAtTarget = cinematicCamera.LookAt;
        }

        if (thirdPersonFollow == null) return;

        defaultCameraDistance = thirdPersonFollow.CameraDistance;
        defaultVerticalArmLength = thirdPersonFollow.VerticalArmLength;
        defaultCameraSide = thirdPersonFollow.CameraSide;
    }

    private void BuildViewMap()
    {
        viewBySlot.Clear();

        for (int i = 0; i < focusViews.Count; i++)
        {
            FocusView view = focusViews[i];
            if (view == null || string.IsNullOrWhiteSpace(view.slotName)) continue;

            string normalizedSlot = NormalizeSlotName(view.slotName);
            if (string.IsNullOrEmpty(normalizedSlot)) continue;

            viewBySlot[normalizedSlot] = view;
        }
    }

    private void ApplyView(
        Transform followTarget,
        Transform lookAtTarget,
        float distance,
        float armLength,
        float side,
        float blendDuration
    )
    {
        if (cinematicCamera == null) return;

        if (followTarget != null)
        {
            cinematicCamera.Follow = followTarget;
        }

        if (lookAtTarget != null)
        {
            cinematicCamera.LookAt = lookAtTarget;
        }
        else if (followTarget != null)
        {
            cinematicCamera.LookAt = followTarget;
        }

        if (thirdPersonFollow == null) return;

        if (blendRoutine != null)
        {
            StopCoroutine(blendRoutine);
            blendRoutine = null;
        }

        if (blendDuration <= 0f)
        {
            thirdPersonFollow.CameraDistance = distance;
            thirdPersonFollow.VerticalArmLength = armLength;
            thirdPersonFollow.CameraSide = side;
            return;
        }

        blendRoutine = StartCoroutine(BlendThirdPersonFollow(distance, armLength, side, blendDuration));
    }

    private IEnumerator BlendThirdPersonFollow(float targetDistance, float targetArmLength, float targetSide, float duration)
    {
        float startDistance = thirdPersonFollow.CameraDistance;
        float startArmLength = thirdPersonFollow.VerticalArmLength;
        float startSide = thirdPersonFollow.CameraSide;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = Mathf.SmoothStep(0f, 1f, t);

            thirdPersonFollow.CameraDistance = Mathf.Lerp(startDistance, targetDistance, eased);
            thirdPersonFollow.VerticalArmLength = Mathf.Lerp(startArmLength, targetArmLength, eased);
            thirdPersonFollow.CameraSide = Mathf.Lerp(startSide, targetSide, eased);

            yield return null;
        }

        thirdPersonFollow.CameraDistance = targetDistance;
        thirdPersonFollow.VerticalArmLength = targetArmLength;
        thirdPersonFollow.CameraSide = targetSide;
        blendRoutine = null;
    }

    private string NormalizeSlotName(string slotName)
    {
        if (string.IsNullOrWhiteSpace(slotName)) return string.Empty;

        string key = slotName.Trim().Replace("_", string.Empty).Replace(" ", string.Empty).ToLowerInvariant();
        switch (key)
        {
            case "backpack":
            case "backpacks":
            case "bag":
            case "bags":
                return "Backpack";

            case "outfit":
            case "outfits":
            case "clothes":
            case "cloth":
                return "Outfit";

            case "glass":
            case "glasses":
            case "eyewear":
                return "Glass";

            case "eye":
                return "Eye";

            case "hat":
                return "Hat";

            case "mouth":
                return "Mouth";

            case "hair":
                return "Hair";

            case "body":
                return "Body";

            default:
                return slotName.Trim();
        }
    }
}
