using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 2D port of <see cref="CameraFollow"/>: an orthographic side-view camera that tracks the
/// mermaid horizontally and heavily low-pass-filters her vertical motion so the porpoise
/// bob doesn't reach the camera. Scroll to zoom, R to reset.
/// </summary>
[RequireComponent(typeof(Camera))]
public class Camera2DFollow : MonoBehaviour
{
    public Transform target;

    [Header("Framing")]
    [Tooltip("Offset from the target (driver = her head). Negative X centers her long tail + trailing hair.")]
    public Vector2 followOffset = new Vector2(-1.8f, -0.55f);
    public float defaultOrthoSize = 3.6f;
    public float minOrthoSize = 1.5f;
    public float maxOrthoSize = 8f;

    [Header("Smoothing")]
    public float horizontalSmoothTime = 0.25f;
    [Tooltip("How heavily to filter the target's vertical motion (porpoise bob) before the camera tracks it.")]
    public float verticalSmoothTime = 1.5f;

    [Header("Input")]
    public float zoomSensitivity = 0.4f;
    public Key resetKey = Key.R;

    Camera cam;
    float xVel, yVel;
    bool pivotInitialized;
    Vector2 pivot;

    void OnEnable()
    {
        cam = GetComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = defaultOrthoSize;
        pivotInitialized = false;
    }

    void LateUpdate()
    {
        if (target == null) return;
        HandleInput();

        Vector2 desired = (Vector2)target.position + followOffset;
        if (!pivotInitialized)
        {
            pivot = desired;
            pivotInitialized = true;
        }
        pivot.x = Mathf.SmoothDamp(pivot.x, desired.x, ref xVel, horizontalSmoothTime);
        pivot.y = Mathf.SmoothDamp(pivot.y, desired.y, ref yVel, verticalSmoothTime);

        transform.position = new Vector3(pivot.x, pivot.y, transform.position.z);
    }

    void HandleInput()
    {
        var mouse = Mouse.current;
        if (mouse != null)
        {
            Vector2 scroll = mouse.scroll.ReadValue();
            if (Mathf.Abs(scroll.y) > 0.01f)
            {
                // Zoom by the scroll SIGN times a fixed step — scroll magnitude varies
                // wildly by OS/mouse. Scroll up = zoom in.
                cam.orthographicSize = Mathf.Clamp(
                    cam.orthographicSize - Mathf.Sign(scroll.y) * zoomSensitivity,
                    minOrthoSize, maxOrthoSize);
            }
        }

        var kb = Keyboard.current;
        if (kb != null && kb[resetKey].wasPressedThisFrame)
        {
            cam.orthographicSize = defaultOrthoSize;
            pivotInitialized = false;
        }
    }
}
