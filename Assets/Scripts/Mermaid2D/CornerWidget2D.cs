using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Turns the game into a round "corner widget": the main camera renders into a small square
/// RenderTexture, which is shown inside an anti-aliased circular mask anchored to a chosen
/// screen corner, framed by a thin decorative ring. Everything outside the circle is filled
/// with <see cref="surroundColor"/> by a dedicated clear camera.
///
/// For a desktop-overlay build (transparent window floating over other apps), set
/// surroundColor's alpha to 0 and combine with a borderless window + per-pixel transparency
/// (DWM layered window on Windows) in a small native plugin/launcher — this component
/// produces exactly the alpha channel that setup needs. In the editor the surround simply
/// shows as the color's RGB.
///
/// Bonus: rendering into a fixed small RT makes GPU cost independent of monitor resolution —
/// ideal for an always-on corner pet.
/// </summary>
public class CornerWidget2D : MonoBehaviour
{
    public enum Corner { BottomLeft, BottomRight, TopLeft, TopRight }

    [Tooltip("The camera that renders the game. Defaults to Camera.main.")]
    public Camera gameCamera;
    public Corner corner = Corner.BottomRight;
    [Tooltip("Circle diameter as a fraction of the smaller screen dimension.")]
    [Range(0.15f, 1f)]
    public float screenFraction = 0.55f;
    [Tooltip("Gap between the circle and the screen edges, in pixels.")]
    public float marginPixels = 16f;
    [Tooltip("Square render texture resolution. Smaller = cheaper GPU; 512–768 is crisp at typical widget sizes.")]
    public int renderTextureSize = 768;
    [Tooltip("Fills the screen outside the circle. Alpha 0 = ready for transparent-overlay builds.")]
    public Color surroundColor = new Color(0f, 0f, 0f, 0f);

    [Header("Ring")]
    public Color ringColor = new Color(1f, 0.75f, 0.28f, 1f);
    [Tooltip("Ring thickness as a fraction of the circle diameter. 0 = no ring.")]
    [Range(0f, 0.08f)]
    public float ringThicknessFrac = 0.018f;

    [Header("Framing")]
    [Tooltip("Adopt a wider zoom + centered offset on the follow camera so her whole body fits the square/circular view (the default framing was tuned for 16:9).")]
    public bool autoFrame = true;

    RenderTexture rt;
    Camera clearCam;
    RectTransform maskRect, ringRect;
    Image ringImage;
    int lastW, lastH;

    void Start()
    {
        if (gameCamera == null) gameCamera = Camera.main;
        if (gameCamera == null)
        {
            Debug.LogWarning("CornerWidget2D: no camera found.");
            enabled = false;
            return;
        }

        rt = new RenderTexture(Mathf.Clamp(renderTextureSize, 128, 2048),
                               Mathf.Clamp(renderTextureSize, 128, 2048), 16)
        { name = "CornerWidgetRT" };
        gameCamera.targetTexture = rt;

        if (autoFrame)
        {
            var follow = gameCamera.GetComponent<Camera2DFollow>();
            if (follow != null)
            {
                follow.defaultOrthoSize = 4.2f;
                follow.followOffset = new Vector2(-2.7f, -0.35f);
            }
            gameCamera.orthographicSize = 4.2f;
        }

        // A camera must still render to the screen, and it paints the surround color.
        var cgo = new GameObject("WidgetClearCamera");
        cgo.transform.SetParent(transform, false);
        clearCam = cgo.AddComponent<Camera>();
        clearCam.clearFlags = CameraClearFlags.SolidColor;
        clearCam.backgroundColor = surroundColor;
        clearCam.cullingMask = 0;
        clearCam.orthographic = true;
        clearCam.depth = -100;
        clearCam.useOcclusionCulling = false;
        clearCam.allowHDR = false;
        clearCam.allowMSAA = false;

        BuildUI();
        Layout();
    }

    void BuildUI()
    {
        var canvasGO = new GameObject("WidgetCanvas");
        canvasGO.transform.SetParent(transform, false);
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        // Circular stencil mask holding the game image.
        var maskGO = new GameObject("CircleMask");
        maskGO.transform.SetParent(canvasGO.transform, false);
        var maskImg = maskGO.AddComponent<Image>();
        maskImg.sprite = MakeCircleSprite(256, 2f);
        maskImg.raycastTarget = false;
        var mask = maskGO.AddComponent<Mask>();
        mask.showMaskGraphic = false;
        maskRect = maskGO.GetComponent<RectTransform>();

        var imgGO = new GameObject("GameView");
        imgGO.transform.SetParent(maskGO.transform, false);
        var raw = imgGO.AddComponent<RawImage>();
        raw.texture = rt;
        raw.raycastTarget = false;
        var rawRect = imgGO.GetComponent<RectTransform>();
        rawRect.anchorMin = Vector2.zero;
        rawRect.anchorMax = Vector2.one;
        rawRect.offsetMin = Vector2.zero;
        rawRect.offsetMax = Vector2.zero;

        // Decorative ring drawn over the mask edge (also hides the stencil's hard edge).
        var ringGO = new GameObject("Ring");
        ringGO.transform.SetParent(canvasGO.transform, false);
        ringImage = ringGO.AddComponent<Image>();
        ringImage.sprite = MakeRingSprite(256, ringThicknessFrac);
        ringImage.color = ringColor;
        ringImage.raycastTarget = false;
        ringRect = ringGO.GetComponent<RectTransform>();
        if (ringThicknessFrac <= 0.0001f) ringGO.SetActive(false);
    }

    void Layout()
    {
        lastW = Screen.width;
        lastH = Screen.height;
        float d = screenFraction * Mathf.Min(lastW, lastH);

        Vector2 anchor, offset;
        switch (corner)
        {
            case Corner.BottomLeft:
                anchor = new Vector2(0f, 0f); offset = new Vector2(marginPixels, marginPixels); break;
            case Corner.TopLeft:
                anchor = new Vector2(0f, 1f); offset = new Vector2(marginPixels, -marginPixels); break;
            case Corner.TopRight:
                anchor = new Vector2(1f, 1f); offset = new Vector2(-marginPixels, -marginPixels); break;
            default:
                anchor = new Vector2(1f, 0f); offset = new Vector2(-marginPixels, marginPixels); break;
        }

        Place(maskRect, anchor, offset, d);
        Place(ringRect, anchor, offset, d);
    }

    static void Place(RectTransform r, Vector2 anchor, Vector2 offset, float size)
    {
        if (r == null) return;
        r.anchorMin = anchor;
        r.anchorMax = anchor;
        r.pivot = anchor;
        r.anchoredPosition = offset;
        r.sizeDelta = new Vector2(size, size);
    }

    void Update()
    {
        if (Screen.width != lastW || Screen.height != lastH) Layout();
        if (clearCam != null && clearCam.backgroundColor != surroundColor)
            clearCam.backgroundColor = surroundColor;
        if (ringImage != null && ringImage.color != ringColor)
            ringImage.color = ringColor;
    }

    /// <summary>
    /// Map a screen point (pixels) through the circular widget into game-world coordinates.
    /// Returns false when the point is outside the widget circle.
    /// </summary>
    public bool ScreenToGameWorld(Vector2 screenPos, Camera gameCam, out Vector2 world)
    {
        world = default;
        if (maskRect == null || gameCam == null) return false;
        // Overlay canvas: rect coordinates ARE screen pixels.
        Vector2 local;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(maskRect, screenPos, null, out local))
            return false;
        Rect r = maskRect.rect;
        Vector2 uv = new Vector2((local.x - r.xMin) / r.width, (local.y - r.yMin) / r.height);
        if (uv.x < 0f || uv.x > 1f || uv.y < 0f || uv.y > 1f) return false;
        if ((uv - new Vector2(0.5f, 0.5f)).sqrMagnitude > 0.25f) return false;   // outside circle
        Vector3 w = gameCam.ViewportToWorldPoint(new Vector3(uv.x, uv.y, 0f));
        world = new Vector2(w.x, w.y);
        return true;
    }

    void OnDestroy()
    {
        if (gameCamera != null && gameCamera.targetTexture == rt)
            gameCamera.targetTexture = null;
        if (rt != null)
        {
            rt.Release();
            Destroy(rt);
        }
    }

    // Filled circle with a feathered (anti-aliased) edge.
    static Sprite MakeCircleSprite(int size, float featherPx)
    {
        var tex = NewUITexture(size);
        var px = new Color32[size * size];
        float c = (size - 1) * 0.5f;
        float rMax = c - 1f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - c, dy = y - c;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01((rMax - dist) / Mathf.Max(0.5f, featherPx));
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    // Annulus (ring) with feathered edges. Thickness is a fraction of the diameter.
    static Sprite MakeRingSprite(int size, float thicknessFrac)
    {
        var tex = NewUITexture(size);
        var px = new Color32[size * size];
        float c = (size - 1) * 0.5f;
        float rOut = c - 1f;
        float rIn = rOut - Mathf.Max(1.5f, thicknessFrac * size);
        const float feather = 1.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - c, dy = y - c;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float aOut = Mathf.Clamp01((rOut - dist) / feather);
                float aIn = Mathf.Clamp01((dist - rIn) / feather);
                float a = Mathf.Min(aOut, aIn);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    static Texture2D NewUITexture(int size)
    {
        return new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
        };
    }
}
