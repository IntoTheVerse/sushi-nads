using UnityEngine;
using Unity.Cinemachine;

[RequireComponent(typeof(CinemachineCamera))]
public class CMAutoPeekOnOcclusion : MonoBehaviour
{
    [Header("Targets & Layers")]
    [SerializeField] Transform followTarget;            // Player
    [SerializeField] LayerMask occluders;               // Cookware | Walls (exclude Player)

    [Header("Match your Third Person Follow")]
    [SerializeField] float baseDistance = -2.5f;        // front cam = negative
    [SerializeField] float baseHeight = 1.7f;         // Vertical Arm Length
    [SerializeField] float baseSide = 0f;           // centered

    [Header("When occluded")]
    [SerializeField] float farDistance = -6f;          // push back a bit
    [SerializeField] float highHeight = 6f;           // you liked 6
    [SerializeField] float sidePeek = 0.25f;        // small lateral

    [Header("Occlusion test (tame jitter)")]
    [SerializeField] float castYOffset = 1.7f;        // head height
    [SerializeField] float testRadius = 0.4f;        // Decollider Camera Radius
    [SerializeField] float clearMargin = 0.25f;       // require some clearance before declaring "clear"
    [SerializeField] float enterDelay = 0.05f;       // how long to be blocked before peeking
    [SerializeField] float exitDelay = 0.20f;       // how long to stay peeking after clear

    [Header("Smoothing")]
    [SerializeField] float smoothTime = 0.20f;       // SmoothDamp time

    CinemachineCamera vcam;
    CinemachineThirdPersonFollow tpf;
    bool occluded;
    float exitHoldUntil;

    float distVel, heightVel, sideVel;

    void Awake()
    {
        vcam = GetComponent<CinemachineCamera>();
        tpf = GetComponent<CinemachineThirdPersonFollow>();
        if (tpf)
        {
            tpf.CameraDistance = baseDistance;
            tpf.VerticalArmLength = baseHeight;
            tpf.CameraSide = baseSide;
        }
    }

    void LateUpdate()
    {
        if (!followTarget || !tpf) return;

        // Spherecast from player head to current camera to check LOS,
        // with a small "clearMargin" so we don't flap on contact.
        Vector3 head = followTarget.position + Vector3.up * castYOffset;
        Vector3 camPos = vcam.transform.position;
        Vector3 dir = (camPos - head);
        float dist = dir.magnitude;
        if (dist < 0.001f) return;
        dir /= dist;

        bool hitSomething = Physics.SphereCast(head, testRadius, dir, out var hit, dist - clearMargin,
                                               occluders, QueryTriggerInteraction.Ignore);

        // Hysteresis: require a little time before toggling states
        if (hitSomething)
        {
            if (!occluded)
            {                 // entering blocked
                // small debounce on enter
                Invoke(nameof(SetOccludedTrue), enterDelay);
            }
            exitHoldUntil = Time.time + exitDelay;
        }
        else if (occluded && Time.time >= exitHoldUntil)
        {
            occluded = false;
            CancelInvoke(nameof(SetOccludedTrue));
        }

        float targetDist = occluded ? farDistance : baseDistance;
        float targetH = occluded ? highHeight : baseHeight;
        float targetSide = occluded ? sidePeek : baseSide;

        tpf.CameraDistance = Mathf.SmoothDamp(tpf.CameraDistance, targetDist, ref distVel, smoothTime);
        tpf.VerticalArmLength = Mathf.SmoothDamp(tpf.VerticalArmLength, targetH, ref heightVel, smoothTime);
        tpf.CameraSide = Mathf.SmoothDamp(tpf.CameraSide, targetSide, ref sideVel, smoothTime);
    }

    void SetOccludedTrue() { occluded = true; }
}
