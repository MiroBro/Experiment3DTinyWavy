using UnityEngine;

/// <summary>
/// 2D port of <see cref="SeaweedField"/>: a whole bed of seaweed in ONE combined mesh. Each
/// blade is a vertical ribbon of quads; the sway, the body-circle parting and the treadmill
/// scroll are applied on the CPU each frame (at 2D blade counts this is a few thousand
/// verts — trivial). Blades wrap within the patch as the bed scrolls past her to fake
/// forward swimming, and the scroll eases to a stop while she rummages.
///
/// Spawn two layers (one sorted behind the mermaid, one in front) for depth.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class Seaweed2D : MonoBehaviour
{
    [Header("Patch")]
    [Tooltip("World-space X centre of the bed.")]
    public float patchCenterX = 0f;
    [Tooltip("World-space Y the blades root to (the seabed).")]
    public float rootY = -1.05f;
    [Tooltip("Width of the bed. Blades wrap within this as the bed scrolls.")]
    public float patchWidth = 18f;
    [Range(1, 2000)]
    public int bladeCount = 160;
    public int seed = 1234;

    [Header("Blade Shape")]
    public Vector2 heightRange = new Vector2(0.55f, 0.95f);
    [Tooltip("Segments up each blade.")]
    [Range(2, 8)]
    public int segments = 5;
    [Tooltip("Half-width of the blade at the root / tip.")]
    public float baseHalfWidth = 0.05f;
    public float tipHalfWidth = 0.014f;

    [Header("Underwater Flow (live)")]
    public float swayAmplitude = 0.35f;
    public float swayFrequency = 0.8f;
    public float waveCount = 1.1f;
    public float flutterAmplitude = 0.10f;
    [Range(0f, 1f)]
    public float rootDarken = 0.45f;

    [Header("Colour")]
    public Color colorA = new Color(0.10f, 0.42f, 0.20f);
    public Color colorB = new Color(0.22f, 0.62f, 0.34f);
    [Tooltip("Layer tint: <1 = darker (back layer), >1 = brighter (front layer).")]
    public float brightness = 1f;
    [Tooltip("ON = shade blades with the green colorA/colorB gradient (procedural look). OFF = grayscale shading only, so a custom textured material shows its own colors (root darkening and layer brightness still apply).")]
    public bool useVertexTint = true;

    [Header("Treadmill Scroll")]
    [Tooltip("How fast the grass scrolls past her (to fake swimming forward) relative to her swim speed. 0 = static bed.")]
    public float scrollScale = 0.5f;
    [Tooltip("Seconds for the scroll to ease in / out as she starts and stops swimming.")]
    public float scrollEaseTime = 1.0f;

    // Assigned by the bootstrap — body circles the grass parts around, and the swimmer that
    // drives scroll speed (and stops it while she rummages).
    [System.NonSerialized] public Transform[] bodyCircles;
    [System.NonSerialized] public float[] bodyRadii;
    [System.NonSerialized] public Mermaid2DSwimmer swimmer;

    float scroll;
    float scrollEase = 1f, scrollEaseVel;

    Mesh mesh;
    Vector3[] verts;
    // Per-blade static data.
    float[] bladeX, bladeYJit, bladeHeight, bladePhase, bladeAmp, bladeWidthMul;

    void Start()
    {
        Build();
    }

    [ContextMenu("Rebuild Seaweed")]
    public void Build()
    {
        var prev = Random.state;
        Random.InitState(seed);

        int N = Mathf.Max(2, segments);
        int rings = N + 1;
        int count = Mathf.Max(1, bladeCount);
        int vpb = rings * 2;

        bladeX = new float[count];
        bladeYJit = new float[count];
        bladeHeight = new float[count];
        bladePhase = new float[count];
        bladeAmp = new float[count];
        bladeWidthMul = new float[count];

        verts = new Vector3[count * vpb];
        var cols = new Color[count * vpb];
        var uvs = new Vector2[count * vpb];
        var tris = new int[count * N * 6];

        int t = 0;
        for (int b = 0; b < count; b++)
        {
            bladeX[b] = Random.Range(-0.5f, 0.5f) * patchWidth;
            bladeYJit[b] = Random.Range(-0.06f, 0.02f);
            bladeHeight[b] = Random.Range(heightRange.x, heightRange.y);
            bladePhase[b] = Random.Range(0f, Mathf.PI * 2f);
            bladeAmp[b] = Random.Range(0.8f, 1.2f);
            bladeWidthMul[b] = Random.Range(0.8f, 1.2f);

            Color tint = useVertexTint ? Color.Lerp(colorA, colorB, Random.value) : Color.white;
            Color shade = tint * brightness;
            shade.a = 1f;

            int baseIdx = b * vpb;
            for (int i = 0; i < rings; i++)
            {
                float h = (float)i / N;
                Color c = shade * Mathf.Lerp(1f - rootDarken, 1f, h);
                c.a = 1f;
                cols[baseIdx + i * 2] = c;
                cols[baseIdx + i * 2 + 1] = c;
                // UVs: U across the blade, V up the blade — a blade texture maps naturally.
                uvs[baseIdx + i * 2] = new Vector2(0f, h);
                uvs[baseIdx + i * 2 + 1] = new Vector2(1f, h);
            }

            for (int i = 0; i < N; i++)
            {
                int a = baseIdx + i * 2;
                int bb = a + 1;
                int c2 = baseIdx + (i + 1) * 2;
                int d = c2 + 1;
                tris[t++] = a; tris[t++] = c2; tris[t++] = bb;
                tris[t++] = bb; tris[t++] = c2; tris[t++] = d;
            }
        }

        Random.state = prev;

        if (mesh == null)
        {
            mesh = new Mesh { name = "SeaweedBed2D" };
            mesh.MarkDynamic();
        }
        mesh.Clear();
        mesh.indexFormat = (verts.Length > 65000)
            ? UnityEngine.Rendering.IndexFormat.UInt32
            : UnityEngine.Rendering.IndexFormat.UInt16;
        UpdateVertices();
        mesh.vertices = verts;
        mesh.colors = cols;
        mesh.uv = uvs;
        mesh.triangles = tris;
        // Static generous bounds so scroll/sway never frustum-culls the bed.
        mesh.bounds = new Bounds(
            new Vector3(patchCenterX, rootY + heightRange.y * 0.5f, 0f),
            new Vector3(patchWidth + 4f, heightRange.y * 2f + 4f, 1f));

        GetComponent<MeshFilter>().sharedMesh = mesh;
        var mr = GetComponent<MeshRenderer>();
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
    }

    void Update()
    {
        // Recover after editor domain reloads (non-serialized buffers go null).
        if (mesh == null || verts == null) Build();
        if (mesh == null || verts == null) return;

        // Treadmill scroll: full speed while cruising, eases to 0 while she rummages.
        // Remap motionScale to 0..1 across [0.6, 0.97]; rummage motionScale (~0.55) -> 0.
        if (swimmer != null)
        {
            float t = Mathf.Clamp01((swimmer.motionScale - 0.6f) / 0.37f);
            float target = t * t * (3f - 2f * t);
            scrollEase = Mathf.SmoothDamp(scrollEase, target, ref scrollEaseVel, Mathf.Max(0.01f, scrollEaseTime));

            float speed = swimmer.cruiseSpeed * scrollScale * scrollEase;
            scroll += speed * Time.deltaTime;
            scroll = Mathf.Repeat(scroll, Mathf.Max(0.01f, patchWidth));
        }

        UpdateVertices();
        mesh.vertices = verts;
    }

    void UpdateVertices()
    {
        int N = Mathf.Max(2, segments);
        int rings = N + 1;
        float time = Application.isPlaying ? Time.time : 0f;
        float w = Mathf.Max(0.01f, patchWidth);
        float swayOmega = swayFrequency * 2f * Mathf.PI;

        int circles = 0;
        if (bodyCircles != null && bodyRadii != null)
            circles = Mathf.Min(bodyCircles.Length, bodyRadii.Length);

        int v = 0;
        for (int b = 0; b < bladeX.Length; b++)
        {
            // She swims toward +X, so the bed scrolls toward -X, wrapping within the patch.
            float rx = Mathf.Repeat(bladeX[b] - scroll + w * 0.5f, w) - w * 0.5f + patchCenterX;
            float ry = rootY + bladeYJit[b];
            float height = bladeHeight[b];
            float phase = bladePhase[b];
            float amp = bladeAmp[b];
            float widthMul = bladeWidthMul[b];

            for (int i = 0; i < rings; i++)
            {
                float h = (float)i / N;
                float y = ry + height * h;
                float xoff = Mathf.Sin(phase + time * swayOmega + h * waveCount * 2f * Mathf.PI)
                             * swayAmplitude * amp * Mathf.Pow(h, 1.3f)
                           + Mathf.Sin(phase * 2f + time * swayOmega * 2.7f + h * 7f)
                             * flutterAmplitude * h * h;
                float cx = rx + xoff;

                // Body parting: push the blade out of any body circle, weighted by height so
                // the roots stay planted.
                for (int c = 0; c < circles; c++)
                {
                    var col = bodyCircles[c];
                    if (col == null) continue;
                    float r = bodyRadii[c];
                    if (r <= 0f) continue;
                    Vector3 cp = col.position;
                    float dx = cx - cp.x, dy = y - cp.y;
                    float distSq = dx * dx + dy * dy;
                    if (distSq < r * r && distSq > 0.000001f)
                    {
                        float dist = Mathf.Sqrt(distSq);
                        float push = (r - dist) * h;
                        cx += (dx / dist) * push;
                        y += (dy / dist) * push * 0.5f;
                    }
                }

                float halfW = Mathf.Lerp(baseHalfWidth, tipHalfWidth, h) * widthMul;
                verts[v++] = new Vector3(cx - halfW, y, 0f);
                verts[v++] = new Vector3(cx + halfW, y, 0f);
            }
        }
    }
}
