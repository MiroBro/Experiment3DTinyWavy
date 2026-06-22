using UnityEngine;
using UnityEngine.InputSystem;

public class CameraFollow : MonoBehaviour
{
    public Transform target;

    [Header("Defaults")]
    public float defaultDistance = 6f;
    public float defaultYaw = -90f;   // 0 = directly behind target, -90 = her right side
    public float defaultPitch = 5f;   // degrees above horizon
    public float minDistance = 2f;
    public float maxDistance = 30f;
    public float minPitch = -80f;
    public float maxPitch = 80f;

    [Header("Behaviour")]
    [Tooltip("If true, yaw is added on top of the target's heading so the camera trails as she turns. If false, yaw is world-aligned (free orbit — camera stays put when input stops).")]
    public bool yawRelativeToTarget = false;
    [Tooltip("Look at this offset above the target's pivot.")]
    public float lookAtHeightOffset = 0.4f;

    [Header("Smoothing")]
    public float positionSmoothTime = 0.2f;
    public float rotationSharpness = 8f;
    [Tooltip("How heavily to filter the target's vertical motion (e.g. porpoise bob) before the camera tracks it. Higher = camera ignores fast vertical bobs.")]
    public float verticalSmoothTime = 1.5f;

    [Header("Input")]
    [Tooltip("Degrees of orbit per pixel of mouse delta while right mouse is held.")]
    public float orbitSensitivity = 0.25f;
    [Tooltip("Distance change per scroll notch.")]
    public float zoomSensitivity = 1.5f;
    public Key resetKey = Key.R;

    float yaw;
    float pitch;
    float distance;
    Vector3 posVelocity;
    Vector3 stablePivot;
    float stablePivotYVel;
    bool pivotInitialized;

    void OnEnable()
    {
        ResetView();
        pivotInitialized = false;
    }

    void ResetView()
    {
        yaw = defaultYaw;
        pitch = defaultPitch;
        distance = defaultDistance;
    }

    void LateUpdate()
    {
        if (target == null) return;
        HandleInput();

        // Build a stable pivot: track the target horizontally exactly,
        // but heavily low-pass-filter Y so porpoise bob doesn't reach the camera.
        if (!pivotInitialized)
        {
            stablePivot = target.position;
            pivotInitialized = true;
        }
        stablePivot.x = target.position.x;
        stablePivot.z = target.position.z;
        stablePivot.y = Mathf.SmoothDamp(stablePivot.y, target.position.y, ref stablePivotYVel, verticalSmoothTime);

        float baseYawDeg = 0f;
        if (yawRelativeToTarget)
        {
            Vector3 fwd = target.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude > 0.0001f)
                baseYawDeg = Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg;
        }

        float worldYaw = baseYawDeg + yaw;
        Quaternion orbitRot = Quaternion.Euler(pitch, worldYaw, 0f);
        Vector3 offset = orbitRot * (Vector3.back * distance);

        Vector3 desiredPos = stablePivot + offset;
        transform.position = Vector3.SmoothDamp(transform.position, desiredPos, ref posVelocity, positionSmoothTime);

        Vector3 lookPoint = stablePivot + Vector3.up * lookAtHeightOffset;
        Vector3 lookDir = lookPoint - transform.position;
        if (lookDir.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(lookDir.normalized, Vector3.up);
            float lerp = 1f - Mathf.Exp(-rotationSharpness * Time.deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, lerp);
        }
    }

    void HandleInput()
    {
        var mouse = Mouse.current;
        if (mouse != null)
        {
            if (mouse.rightButton.isPressed)
            {
                Vector2 delta = mouse.delta.ReadValue();
                yaw -= delta.x * orbitSensitivity;
                pitch -= delta.y * orbitSensitivity;
                pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
            }

            Vector2 scroll = mouse.scroll.ReadValue();
            if (Mathf.Abs(scroll.y) > 0.01f)
            {
                // Scroll magnitude varies wildly by OS/mouse (1 vs 120 per notch), so zoom by
                // its SIGN times a fixed step — reliable everywhere. Scroll up = zoom in.
                distance -= Mathf.Sign(scroll.y) * zoomSensitivity;
                distance = Mathf.Clamp(distance, minDistance, maxDistance);
            }
        }

        var kb = Keyboard.current;
        if (kb != null && kb[resetKey].wasPressedThisFrame)
        {
            ResetView();
        }
    }
}
