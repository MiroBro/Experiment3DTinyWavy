using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// "Brush the Sand" reveal — the minigame that replaces precision glint-clicking: a find
/// surfaces as a little sand mound at the dig spot with a colored glimmer peeking through.
/// HOLD the mouse button ANYWHERE and wiggle the cursor to scrub the sand away (motion
/// matters, position doesn't). An untouched mound crumbles open by itself after a few
/// seconds, so idle play still collects everything. On full reveal it pops, puffs dust,
/// and hands the find back to the manager via onRevealed.
/// </summary>
public class SandMound2D : MonoBehaviour
{
    [Tooltip("Seconds for an untouched mound to crumble open on its own.")]
    public float autoRevealTime = 6f;
    [Tooltip("Total cursor travel (screen pixels, while held) for a full manual reveal.")]
    public float scrubPixelsToReveal = 1800f;
    public System.Action onRevealed;

    float progress;      // 0..1
    bool revealed;
    bool hadMouse;
    Vector2 lastMouse;
    float dustBudget;    // scrub pixels until the next dust puff
    float scrubGlow;     // recent-scrub feedback, decays

    Transform[] lumps;
    float[] lumpBaseScale;
    Vector3[] lumpBasePos;
    Material sandMat, sandDarkMat, glintMat;
    Transform glint;

    public static SandMound2D Spawn(Vector3 pos, Color itemColor, int sortingOrder)
    {
        var go = new GameObject("SandMound2D");
        pos.z = 0f;
        go.transform.position = pos;
        var m = go.AddComponent<SandMound2D>();

        var sand = new Color(0.76f, 0.66f, 0.47f);
        var sandDark = new Color(0.62f, 0.52f, 0.36f);
        m.sandMat = new Material(Shader.Find("Sprites/Default")) { color = sand };
        m.sandDarkMat = new Material(Shader.Find("Sprites/Default")) { color = sandDark };

        // A squat pile of overlapping discs; the smaller back lumps are darker.
        var lumpDefs = new[]
        {
            (p: new Vector3(0f, 0.02f, 0f),     s: 0.30f, dark: false),
            (p: new Vector3(-0.16f, -0.03f, 0f), s: 0.22f, dark: true),
            (p: new Vector3(0.15f, -0.02f, 0f),  s: 0.24f, dark: true),
            (p: new Vector3(-0.07f, 0.08f, 0f),  s: 0.19f, dark: false),
            (p: new Vector3(0.08f, 0.09f, 0f),   s: 0.16f, dark: false),
        };
        m.lumps = new Transform[lumpDefs.Length];
        m.lumpBaseScale = new float[lumpDefs.Length];
        m.lumpBasePos = new Vector3[lumpDefs.Length];
        for (int i = 0; i < lumpDefs.Length; i++)
        {
            var lump = MakeDisc(go.transform, lumpDefs[i].p, lumpDefs[i].s,
                lumpDefs[i].dark ? m.sandDarkMat : m.sandMat,
                sortingOrder + (lumpDefs[i].dark ? 1 : 2));
            lump.localScale = new Vector3(1f, 0.62f, 1f) * lumpDefs[i].s;   // squashed pile
            m.lumps[i] = lump;
            m.lumpBaseScale[i] = lumpDefs[i].s;
            m.lumpBasePos[i] = lumpDefs[i].p;
        }

        // The buried item's glimmer, peeking through and brightening as sand clears.
        Color glintCol = Color.Lerp(itemColor, Color.white, 0.35f);
        m.glintMat = new Material(Shader.Find("Sprites/Default")) { color = glintCol };
        m.glint = MakeDisc(go.transform, new Vector3(0f, 0.03f, 0f), 0.06f, m.glintMat, sortingOrder + 3);
        return m;
    }

    static Transform MakeDisc(Transform parent, Vector3 localPos, float scale, Material mat, int order)
    {
        var go = new GameObject("MoundDisc");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = Vector3.one * scale;
        go.AddComponent<MeshFilter>().sharedMesh = SandFx.DiscMesh();
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.sortingOrder = order;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        return go.transform;
    }

    void Update()
    {
        if (revealed) return;
        float dt = Time.deltaTime;

        // Idle crumble — the game never blocks on the player.
        progress += dt / Mathf.Max(0.5f, autoRevealTime);

        // Scrub: cursor travel while the button is held, anywhere on screen.
        var mouse = Mouse.current;
        bool held = mouse != null && mouse.leftButton.isPressed && !SandFx.PointerOverUI();
        if (mouse != null)
        {
            Vector2 mp = mouse.position.ReadValue();
            if (held && hadMouse)
            {
                float px = Mathf.Min((mp - lastMouse).magnitude, 90f);   // ignore warp spikes
                if (px > 1.5f)
                {
                    progress += px / Mathf.Max(200f, scrubPixelsToReveal);
                    scrubGlow = 1f;
                    dustBudget -= px;
                    if (dustBudget <= 0f)
                    {
                        dustBudget = Random.Range(120f, 220f);
                        SandFx.DustPuff(transform.position + new Vector3(Random.Range(-0.18f, 0.18f), 0.06f, 0f),
                            sandMat.color, 21);
                    }
                }
            }
            lastMouse = mp;
            hadMouse = true;
        }
        scrubGlow = Mathf.Max(0f, scrubGlow - dt * 3f);

        // Visuals: lumps shrink away in sequence as progress rises; the glimmer swells.
        float p = Mathf.Clamp01(progress);
        for (int i = 0; i < lumps.Length; i++)
        {
            // Each lump fully gone by a staggered point; jiggle while being scrubbed.
            float gone = Mathf.Clamp01((p - i * 0.12f) / 0.55f);
            float s = lumpBaseScale[i] * (1f - gone);
            float jig = scrubGlow * 0.02f;
            lumps[i].localScale = new Vector3(1f, 0.62f, 1f) * Mathf.Max(0.0001f, s);
            lumps[i].localPosition = lumpBasePos[i] + new Vector3(
                Mathf.Sin(Time.time * 37f + i * 2f) * jig,
                Mathf.Abs(Mathf.Sin(Time.time * 31f + i)) * jig, 0f);
        }
        float glintPulse = 1f + Mathf.Sin(Time.time * 8f) * 0.2f;
        glint.localScale = Vector3.one * (0.05f + 0.09f * p) * glintPulse;

        if (p >= 1f) Reveal();
    }

    void Reveal()
    {
        if (revealed) return;
        revealed = true;
        for (int i = 0; i < 6; i++)
            SandFx.DustPuff(transform.position + new Vector3(Random.Range(-0.2f, 0.2f), Random.Range(0f, 0.12f), 0f),
                sandMat.color, 21);
        onRevealed?.Invoke();
        Destroy(gameObject);
    }

    void OnDestroy()
    {
        // Never lose a rolled find: if something destroys the mound early (travel, teardown
        // of a still-loaded scene), deliver instantly.
        if (!revealed && gameObject.scene.isLoaded) { revealed = true; onRevealed?.Invoke(); }
        if (sandMat != null) Destroy(sandMat);
        if (sandDarkMat != null) Destroy(sandDarkMat);
        if (glintMat != null) Destroy(glintMat);
    }
}

/// <summary>Tiny shared FX + helpers for the dig minigames (dust puffs, help sparkles).</summary>
public static class SandFx
{
    static Mesh _disc;
    public static Mesh DiscMesh()
    {
        if (_disc != null) return _disc;
        const int seg = 20;
        var verts = new Vector3[seg + 1];
        var cols = new Color[seg + 1];
        var tris = new int[seg * 3];
        verts[0] = Vector3.zero; cols[0] = Color.white;
        for (int i = 0; i < seg; i++)
        {
            float a = i * 2f * Mathf.PI / seg;
            verts[i + 1] = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
            cols[i + 1] = Color.white;
        }
        int t = 0;
        for (int i = 0; i < seg; i++)
        {
            tris[t++] = 0; tris[t++] = 1 + i; tris[t++] = 1 + (i + 1) % seg;
        }
        _disc = new Mesh { name = "SandFxDisc" };
        _disc.vertices = verts; _disc.colors = cols; _disc.triangles = tris;
        _disc.RecalculateBounds();
        return _disc;
    }

    public static bool PointerOverUI()
    {
        var es = UnityEngine.EventSystems.EventSystem.current;
        return es != null && es.IsPointerOverGameObject();
    }

    public static void DustPuff(Vector3 pos, Color color, int sortingOrder)
    {
        var go = new GameObject("SandDust");
        pos.z = 0f;
        go.transform.position = pos;
        go.AddComponent<MeshFilter>().sharedMesh = DiscMesh();
        var mr = go.AddComponent<MeshRenderer>();
        var mote = go.AddComponent<SandDustMote2D>();
        mote.mat = new Material(Shader.Find("Sprites/Default")) { color = color };
        mr.sharedMaterial = mote.mat;
        mr.sortingOrder = sortingOrder;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        mote.vel = new Vector3(Random.Range(-0.25f, 0.25f), Random.Range(0.25f, 0.6f), 0f);
    }

    /// <summary>Hold-to-help feedback: a sparkle at the cursor that drifts to her hand.</summary>
    public static void HelpSpark(Vector3 from, Transform toHand, int sortingOrder)
    {
        var go = new GameObject("HelpSpark");
        from.z = 0f;
        go.transform.position = from;
        go.AddComponent<MeshFilter>().sharedMesh = DiscMesh();
        var mr = go.AddComponent<MeshRenderer>();
        var s = go.AddComponent<HelpSpark2D>();
        s.mat = new Material(Shader.Find("Sprites/Default")) { color = new Color(1f, 0.93f, 0.6f, 0.9f) };
        s.target = toHand;
        mr.sharedMaterial = s.mat;
        mr.sortingOrder = sortingOrder;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
    }
}

/// <summary>A pinch of sand drifting up and fading (spawned by SandFx.DustPuff).</summary>
public class SandDustMote2D : MonoBehaviour
{
    public Material mat;
    public Vector3 vel;
    float age;
    readonly float life = 0.55f;
    readonly float size = 0.045f;

    void Update()
    {
        age += Time.deltaTime;
        if (age >= life) { Destroy(gameObject); return; }
        float t = age / life;
        transform.position += vel * Time.deltaTime;
        vel *= 1f - Time.deltaTime * 2f;
        transform.localScale = Vector3.one * size * (1f - t * 0.5f);
        if (mat != null) { var c = mat.color; c.a = 0.8f * (1f - t); mat.color = c; }
    }

    void OnDestroy() { if (mat != null) Destroy(mat); }
}

/// <summary>Hold-to-help sparkle: eases from the cursor toward her digging hand.</summary>
public class HelpSpark2D : MonoBehaviour
{
    public Material mat;
    public Transform target;
    float age;
    readonly float life = 0.5f;
    Vector3 startPos;

    void Start() { startPos = transform.position; }

    void Update()
    {
        age += Time.deltaTime;
        if (age >= life || target == null) { Destroy(gameObject); return; }
        float t = age / life;
        float ease = t * t * (3f - 2f * t);
        transform.position = Vector3.Lerp(startPos, target.position, ease);
        transform.localScale = Vector3.one * 0.05f * (1f - t * 0.6f);
        if (mat != null) { var c = mat.color; c.a = 0.9f * (1f - t * t); mat.color = c; }
    }

    void OnDestroy() { if (mat != null) Destroy(mat); }
}
