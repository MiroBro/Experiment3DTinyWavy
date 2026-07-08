using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 2D port of <see cref="UnderwaterAtmosphere"/>: builds the whole "gorgeous underwater"
/// look at runtime with flat vertex-colored meshes — a deep→horizon gradient backdrop
/// (parented to the camera), slowly swaying god-ray shafts, drifting plankton motes (front
/// and back layers), and a dark seabed strip with a lit sand line. Everything is unlit
/// Sprites/Default, layered purely with sortingOrder.
/// </summary>
public class Underwater2DAtmosphere : MonoBehaviour
{
    [Header("Water Mood")]
    public Color deepColor = new Color(0.10f, 0.34f, 0.50f);
    public Color horizonColor = new Color(0.34f, 0.66f, 0.74f);

    [Header("God Rays")]
    public int godRayCount = 5;
    public Color godRayColor = new Color(0.45f, 0.75f, 0.85f);
    [Range(0f, 1f)] public float godRayAlpha = 0.22f;
    public float godRaySwaySpeed = 0.15f;

    [Header("Motes (floating plankton)")]
    public int moteCount = 90;
    public Color moteColor = new Color(0.7f, 0.9f, 1f, 0.5f);
    [Tooltip("Motes drift backward past her (treadmill flow) at this speed, plus wander.")]
    public float moteFlowSpeed = 0.45f;

    [Header("Seabed")]
    public float seabedY = -1.08f;
    public Color seabedColor = new Color(0.16f, 0.14f, 0.10f);
    public Color sandLineColor = new Color(0.45f, 0.38f, 0.24f);

    [Header("Custom Art (optional — set on Bootstrap2D, copied here at spawn)")]
    [Tooltip("Your own background image. Replaces the procedural deep→horizon gradient (drawn untinted, stretched to fill the view).")]
    public Sprite backdropSprite;
    [Tooltip("Your own ground/sand image. Replaces the procedural seabed strip + sand line.")]
    public Sprite seabedSprite;
    [Tooltip("Your own plankton/particle sprite. Replaces the procedural soft diamonds; moteColor still tints it.")]
    public Sprite moteSprite;
    [Tooltip("Your own god-ray material (e.g. an additive shader or a textured ray). The vertex-gradient quad geometry is kept.")]
    public Material godRayMaterial;

    class Mote
    {
        public Transform t;
        public float speed, wobblePhase, wobbleAmp, baseY;
    }

    class GodRay
    {
        public Transform t;
        public Material mat;
        public float phase;
        public float baseAngle;
    }

    readonly List<Mote> motes = new List<Mote>();
    readonly List<GodRay> rays = new List<GodRay>();
    readonly List<GameObject> spawned = new List<GameObject>();
    Camera cam;
    bool built;
    const float MoteHalfW = 8f;

    void Start()
    {
        if (!built) Build();
    }

    /// <summary>Build (or fully rebuild) every atmosphere piece from the current colors.
    /// Called again by the meta-game when she travels to a new location.</summary>
    public void Build()
    {
        for (int i = 0; i < spawned.Count; i++)
        {
            if (spawned[i] == null) continue;
            if (Application.isPlaying) Destroy(spawned[i]);
            else DestroyImmediate(spawned[i]);
        }
        spawned.Clear();
        motes.Clear();
        rays.Clear();
        built = true;

        cam = Camera.main;
        if (cam != null && Application.isPlaying)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = deepColor;
        }

        BuildBackdrop();
        BuildSeabed();
        BuildGodRays();
        BuildMotes();
    }

    /// <summary>Alias used by the meta-game when the location palette changes.</summary>
    public void Rebuild() => Build();

    static Material SpriteMat(Color tint)
    {
        return new Material(Shader.Find("Sprites/Default")) { color = tint };
    }

    static MeshRenderer MakeMeshGO(string name, Transform parent, Mesh mesh, int sortingOrder, Color tint)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = SpriteMat(tint);
        mr.sortingOrder = sortingOrder;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        return mr;
    }

    /// <summary>Quad with independent corner colors: BL, BR, TL, TR around (0,0), size w×h.</summary>
    public static Mesh GradientQuad(float w, float h, Color bl, Color br, Color tl, Color tr)
    {
        var m = new Mesh { name = "GradientQuad2D" };
        float hw = w * 0.5f, hh = h * 0.5f;
        m.vertices = new[]
        {
            new Vector3(-hw, -hh, 0f), new Vector3(hw, -hh, 0f),
            new Vector3(-hw,  hh, 0f), new Vector3(hw,  hh, 0f),
        };
        m.colors = new[] { bl, br, tl, tr };
        m.triangles = new[] { 0, 2, 1, 1, 2, 3 };
        m.RecalculateBounds();
        return m;
    }

    static SpriteRenderer MakeSpriteGO(string name, Transform parent, Sprite sprite,
        Vector2 fitSize, Color tint, int sortingOrder)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = tint;
        sr.sortingOrder = sortingOrder;
        Vector2 b = sprite.bounds.size;
        go.transform.localScale = new Vector3(
            fitSize.x / Mathf.Max(0.0001f, b.x),
            fitSize.y / Mathf.Max(0.0001f, b.y), 1f);
        return sr;
    }

    void BuildBackdrop()
    {
        // Parented to the camera so it never moves on screen (in edit-mode preview it is
        // parented here instead, placed at the camera's position). Big enough for any zoom.
        Transform parent = (Application.isPlaying && cam != null) ? cam.transform : transform;
        Vector3 pos = new Vector3(0f, 0f, 20f);
        if (!Application.isPlaying && cam != null) pos += cam.transform.position;

        if (backdropSprite != null)
        {
            var sr = MakeSpriteGO("Backdrop", parent, backdropSprite, new Vector2(60f, 30f), Color.white, -100);
            sr.transform.localPosition = pos;
            spawned.Add(sr.gameObject);
            return;
        }
        var mesh = GradientQuad(60f, 30f, deepColor, deepColor, horizonColor, horizonColor);
        var mr = MakeMeshGO("Backdrop", parent, mesh, -100, Color.white);
        mr.transform.localPosition = pos;
        spawned.Add(mr.gameObject);
    }

    void BuildSeabed()
    {
        if (seabedSprite != null)
        {
            // Custom ground art: top edge sits at seabedY, drawn instead of strip + line.
            var sr = MakeSpriteGO("Seabed", transform, seabedSprite, new Vector2(70f, 6f), Color.white, -60);
            sr.transform.position = new Vector3(0f, seabedY - 3f, 0f);
            spawned.Add(sr.gameObject);
            return;
        }

        Color deepBed = seabedColor * 0.55f; deepBed.a = 1f;
        var bed = GradientQuad(70f, 6f, deepBed, deepBed, seabedColor, seabedColor);
        var mrBed = MakeMeshGO("Seabed", transform, bed, -60, Color.white);
        mrBed.transform.position = new Vector3(0f, seabedY - 3f, 0f);
        spawned.Add(mrBed.gameObject);

        Color lineLo = sandLineColor * 0.7f; lineLo.a = 1f;
        var line = GradientQuad(70f, 0.09f, lineLo, lineLo, sandLineColor, sandLineColor);
        var mrLine = MakeMeshGO("SandLine", transform, line, -59, Color.white);
        mrLine.transform.position = new Vector3(0f, seabedY + 0.045f, 0f);
        spawned.Add(mrLine.gameObject);
    }

    void BuildGodRays()
    {
        var prev = Random.state;
        Random.InitState(777);
        for (int i = 0; i < godRayCount; i++)
        {
            float width = Random.Range(0.5f, 1.5f);
            Color top = godRayColor; top.a = 1f;
            Color bot = godRayColor; bot.a = 0f;
            var mesh = GradientQuad(width, 9f, bot, bot, top, top);
            var mr = MakeMeshGO($"GodRay{i:D2}", transform, mesh, -90, Color.white);
            spawned.Add(mr.gameObject);
            if (godRayMaterial != null)
                mr.sharedMaterial = new Material(godRayMaterial);   // per-ray copy: alpha animates independently
            float x = Mathf.Lerp(-6.5f, 6.5f, (godRayCount > 1) ? (float)i / (godRayCount - 1) : 0.5f)
                      + Random.Range(-0.8f, 0.8f);
            mr.transform.position = new Vector3(x, 2.6f, 0f);
            float angle = Random.Range(-18f, -8f);
            mr.transform.rotation = Quaternion.Euler(0f, 0f, angle);

            var ray = new GodRay
            {
                t = mr.transform,
                mat = mr.sharedMaterial,
                phase = Random.Range(0f, Mathf.PI * 2f),
                baseAngle = angle,
            };
            ray.mat.color = new Color(1f, 1f, 1f, godRayAlpha);
            rays.Add(ray);
        }
        Random.state = prev;
    }

    void BuildMotes()
    {
        var prev = Random.state;
        Random.InitState(31415);

        // One tiny soft-diamond mesh shared by every mote (alpha falls off to the corners).
        Color core = Color.white;
        Color edge = new Color(1f, 1f, 1f, 0f);
        var moteMesh = new Mesh { name = "Mote2D" };
        moteMesh.vertices = new[]
        {
            Vector3.zero,
            new Vector3(0f, 1f, 0f), new Vector3(1f, 0f, 0f),
            new Vector3(0f, -1f, 0f), new Vector3(-1f, 0f, 0f),
        };
        moteMesh.colors = new[] { core, edge, edge, edge, edge };
        moteMesh.triangles = new[] { 0, 1, 2, 0, 2, 3, 0, 3, 4, 0, 4, 1 };
        moteMesh.RecalculateBounds();

        var sharedMat = SpriteMat(moteColor);

        for (int i = 0; i < moteCount; i++)
        {
            bool front = (i % 3 == 0);   // a third of the motes float in front of her
            var go = new GameObject($"Mote{i:D3}");
            go.transform.SetParent(transform, false);
            spawned.Add(go);
            float size = Random.Range(0.015f, 0.05f) * (front ? 1.6f : 1f);

            if (moteSprite != null)
            {
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = moteSprite;
                sr.color = moteColor;
                sr.sortingOrder = front ? 30 : -85;
                float s = (size * 2f) / Mathf.Max(0.0001f, moteSprite.bounds.size.y);
                go.transform.localScale = new Vector3(s, s, 1f);
            }
            else
            {
                go.AddComponent<MeshFilter>().sharedMesh = moteMesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = sharedMat;
                mr.sortingOrder = front ? 30 : -85;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                go.transform.localScale = new Vector3(size, size, 1f);
            }
            float y = Random.Range(seabedY + 0.2f, 4.5f);
            go.transform.position = new Vector3(Random.Range(-MoteHalfW, MoteHalfW), y, 0f);

            motes.Add(new Mote
            {
                t = go.transform,
                speed = moteFlowSpeed * Random.Range(0.5f, 1.4f),
                wobblePhase = Random.Range(0f, Mathf.PI * 2f),
                wobbleAmp = Random.Range(0.05f, 0.25f),
                baseY = y,
            });
        }
        Random.state = prev;
    }

    void Update()
    {
        float time = Time.time;

        // God rays: slow alpha shimmer + a gentle pendulum sway.
        for (int i = 0; i < rays.Count; i++)
        {
            var r = rays[i];
            float s = Mathf.Sin(time * godRaySwaySpeed * 2f * Mathf.PI * 0.35f + r.phase);
            r.t.rotation = Quaternion.Euler(0f, 0f, r.baseAngle + s * 2.5f);
            float a = godRayAlpha * (0.7f + 0.3f * Mathf.Sin(time * 0.4f + r.phase * 1.7f));
            r.mat.color = new Color(1f, 1f, 1f, a);
        }

        // Motes: drift backward past her, wobble vertically, wrap around the view.
        float dt = Time.deltaTime;
        for (int i = 0; i < motes.Count; i++)
        {
            var mo = motes[i];
            Vector3 p = mo.t.position;
            p.x -= mo.speed * dt;
            if (p.x < -MoteHalfW) p.x += MoteHalfW * 2f;
            p.y = mo.baseY + Mathf.Sin(time * 0.5f + mo.wobblePhase) * mo.wobbleAmp;
            mo.t.position = p;
        }
    }
}
