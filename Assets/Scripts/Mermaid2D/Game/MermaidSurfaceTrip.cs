using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// The "Surface" trip: she stops foraging, swims up to the waterline, pops her head out
/// next to a little rowboat where a crow waits, sells the crow her ROCKS (they arc into
/// the boat one by one), then dives back down and resumes foraging.
///
/// The surface set (sky, waterline, boat, crow) is built procedurally on the first trip
/// and stays up there — it's far above the play area so it's simply out of frame while
/// she's underwater. Created and wired at runtime by <see cref="GemGameManager"/>.
/// </summary>
public class MermaidSurfaceTrip : MonoBehaviour
{
    [System.NonSerialized] public GemGameManager manager;
    [System.NonSerialized] public Mermaid2DForager forager;
    [System.NonSerialized] public Mermaid2DSwimmer swimmer;

    [Header("Surface")]
    [Tooltip("World Y of the waterline she surfaces to.")]
    public float surfaceY = 5f;
    [Tooltip("The boat floats this far ahead (+X) of where she pops up.")]
    public float boatOffsetX = 1.7f;
    [Tooltip("SmoothDamp time for the swim up / down. Bigger = a longer, lazier ascent.")]
    public float ascendSmoothTime = 0.9f;
    [Tooltip("Seconds she lingers at the surface (extends until all rocks have flown).")]
    public float surfaceStayTime = 3.4f;
    [Tooltip("Seconds after popping up before the rocks fly to the crow.")]
    public float sellDelay = 0.9f;

    enum Phase { Idle, Ascend, Surface, Descend }
    Phase phase = Phase.Idle;
    float phaseT;
    bool sold;
    Vector2 offset, offsetVel;
    float lookVel;

    GameObject surfaceSet;
    Transform boat;
    Vector3 boatBasePos;
    Transform crow;
    Vector3 crowBasePos;
    float crowFlap;

    class FlyingRock { public Transform t; public Vector3 from, to; public float u; }
    readonly List<FlyingRock> flyingRocks = new List<FlyingRock>();
    const float RockFlightTime = 0.7f;

    static Mesh _disc, _quad;

    public bool IsActive => phase != Phase.Idle;

    public void Begin()
    {
        if (phase != Phase.Idle || swimmer == null) return;
        if (forager != null)
        {
            forager.suspended = true;
            // Release any rummage reach — the bones smooth-damp back to the swim pose.
            if (forager.handNear != null) forager.handNear.reachOffsetWorld = Vector2.zero;
            if (forager.handFar != null) forager.handFar.reachOffsetWorld = Vector2.zero;
            if (forager.elbowNear != null) forager.elbowNear.reachOffsetWorld = Vector2.zero;
            if (forager.elbowFar != null) forager.elbowFar.reachOffsetWorld = Vector2.zero;
        }
        offset = swimmer.forageBodyOffsetWorld;
        offsetVel = Vector2.zero;
        sold = false;
        phase = Phase.Ascend;
        phaseT = 0f;
        EnsureSurfaceSet();
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (phase != Phase.Idle && swimmer != null)
        {
            phaseT += dt;

            // Aim the bob centre a whisker above the waterline so her face pops out and
            // dips with every gentle bob while she chats with the crow.
            float surfaceOffsetY = (surfaceY + 0.05f) - swimmer.BasePosition.y;

            switch (phase)
            {
                case Phase.Ascend:
                    Drive(new Vector2(0f, surfaceOffsetY), -24f, 1f, dt);   // nose up toward the light
                    if (Mathf.Abs(offset.y - surfaceOffsetY) < 0.12f) { phase = Phase.Surface; phaseT = 0f; }
                    break;

                case Phase.Surface:
                    Drive(new Vector2(0f, surfaceOffsetY), 10f, 0.55f, dt); // idles, eyeing the boat
                    if (!sold && phaseT >= sellDelay) SellToCrow();
                    if (phaseT >= surfaceStayTime && flyingRocks.Count == 0) { phase = Phase.Descend; phaseT = 0f; }
                    break;

                case Phase.Descend:
                    float cruiseLift = forager != null ? forager.cruiseLift : 1.2f;
                    Drive(Vector2.up * cruiseLift, 0f, 1f, dt);
                    if (Mathf.Abs(offset.y - cruiseLift) < 0.15f)
                    {
                        phase = Phase.Idle;
                        if (forager != null) forager.suspended = false;
                    }
                    break;
            }
        }

        UpdateFlyingRocks(dt);
        AnimateSet(dt);
    }

    void Drive(Vector2 targetOffset, float lookDownTarget, float motion, float dt)
    {
        offset = Vector2.SmoothDamp(offset, targetOffset, ref offsetVel, ascendSmoothTime, Mathf.Infinity, dt);
        swimmer.forageBodyOffsetWorld = offset;
        swimmer.motionScale = motion;
        swimmer.lookDownDeg = Mathf.SmoothDamp(swimmer.lookDownDeg, lookDownTarget, ref lookVel, 0.5f, Mathf.Infinity, dt);
    }

    // ---------------------------------------------------------------- selling

    void SellToCrow()
    {
        sold = true;
        int rocksSold = 0;
        if (manager != null) manager.SellRocksToCrow(out rocksSold);

        if (rocksSold <= 0) { crowFlap = 0.5f; return; }   // an expectant caw-flap anyway

        int visuals = Mathf.Min(rocksSold, 8);
        Vector3 from = swimmer != null ? swimmer.transform.position : transform.position;
        Vector3 to = boat != null ? boatBasePos + new Vector3(0.05f, 0.10f, 0f) : from + Vector3.right;
        for (int i = 0; i < visuals; i++)
        {
            var go = MakeDiscObj(surfaceSet.transform, "SoldRock",
                new Color(0.45f, 0.43f, 0.40f), 12, 0.065f + 0.02f * (i % 3));
            go.transform.position = from;
            flyingRocks.Add(new FlyingRock { t = go.transform, from = from, to = to, u = -i * 0.16f });
        }
    }

    void UpdateFlyingRocks(float dt)
    {
        for (int i = flyingRocks.Count - 1; i >= 0; i--)
        {
            var r = flyingRocks[i];
            if (r.t == null) { flyingRocks.RemoveAt(i); continue; }
            r.u += dt / RockFlightTime;          // u < 0 = staggered launch delay
            float u = Mathf.Clamp01(r.u);
            Vector3 p = Vector3.Lerp(r.from, r.to, u) + Vector3.up * (1.1f * 4f * u * (1f - u));
            r.t.position = p;
            if (r.u >= 1f)
            {
                Destroy(r.t.gameObject);
                flyingRocks.RemoveAt(i);
                crowFlap = 0.45f;                // the crow hops for every rock it catches
            }
        }
    }

    // ---------------------------------------------------------------- surface set

    void EnsureSurfaceSet()
    {
        float boatX = (swimmer != null ? swimmer.transform.position.x : 0f) + boatOffsetX;
        if (surfaceSet != null)
        {
            boatBasePos = new Vector3(boatX, surfaceY, 0f);
            return;
        }

        surfaceSet = new GameObject("SurfaceSet");

        // Sky: soft vertical gradient filling the view above the waterline.
        MakeQuadObj(surfaceSet.transform, "Sky", new Vector3(boatX, surfaceY + 4f, 0f), new Vector2(44f, 8f),
            new Color(0.55f, 0.78f, 0.88f), new Color(0.74f, 0.90f, 0.98f), -80);

        // The waterline itself: a bright band where sea meets sky.
        MakeQuadObj(surfaceSet.transform, "Waterline", new Vector3(boatX, surfaceY, 0f), new Vector2(44f, 0.07f),
            new Color(1f, 1f, 1f, 0.55f), new Color(1f, 1f, 1f, 0.55f), -79);

        // The little rowboat.
        var boatGO = new GameObject("Boat");
        boatGO.transform.SetParent(surfaceSet.transform, false);
        boatBasePos = new Vector3(boatX, surfaceY, 0f);
        boat = boatGO.transform;
        boat.position = boatBasePos;

        Color wood = new Color(0.38f, 0.24f, 0.13f);
        Color woodLight = new Color(0.52f, 0.35f, 0.20f);
        // Hull: deck-wide, keel-narrow trapezoid hanging just below the waterline.
        MakeTrapezoid(boat, "Hull", 0.78f, 0.46f, 0.30f, wood, -45);
        MakeQuadObj(boat, "Gunwale", new Vector3(0f, 0.03f, 0f), new Vector2(1.60f, 0.07f), woodLight, woodLight, -44);
        MakeQuadObj(boat, "Mast", new Vector3(0.30f, 0.38f, 0f), new Vector2(0.05f, 0.66f), wood, wood, -46);
        MakeTri(boat, "Pennant", new Vector3(0.325f, 0.66f, 0f),
            new Vector2(0f, 0.05f), new Vector2(0f, -0.05f), new Vector2(-0.26f, 0f),
            new Color(0.85f, 0.25f, 0.20f), -46);

        // The crow, perched on the bow (facing the mermaid, -X).
        var crowGO = new GameObject("Crow");
        crowGO.transform.SetParent(boat, false);
        crowBasePos = new Vector3(-0.52f, 0.16f, 0f);
        crowGO.transform.localPosition = crowBasePos;
        crow = crowGO.transform;

        Color feather = new Color(0.09f, 0.09f, 0.12f);
        var body = MakeDiscObj(crow, "Body", feather, -43, 1f);
        body.transform.localScale = new Vector3(0.16f, 0.115f, 1f);
        var head = MakeDiscObj(crow, "Head", feather, -43, 0.075f);
        head.transform.localPosition = new Vector3(-0.11f, 0.10f, 0f);
        MakeTri(crow, "Beak", new Vector3(-0.17f, 0.095f, 0f),
            new Vector2(0f, 0.028f), new Vector2(0f, -0.028f), new Vector2(-0.09f, 0.006f),
            new Color(0.95f, 0.62f, 0.18f), -42);
        var eye = MakeDiscObj(crow, "Eye", new Color(0.9f, 0.85f, 0.7f), -42, 0.016f);
        eye.transform.localPosition = new Vector3(-0.125f, 0.115f, 0f);
        MakeTri(crow, "Tail", new Vector3(0.13f, 0.03f, 0f),
            new Vector2(0f, 0.035f), new Vector2(0f, -0.02f), new Vector2(0.13f, 0.05f),
            feather, -43);
    }

    void AnimateSet(float dt)
    {
        if (boat == null) return;
        float t = Time.time;
        boat.position = boatBasePos + Vector3.up * (0.045f * Mathf.Sin(t * 1.2f));
        boat.rotation = Quaternion.Euler(0f, 0f, 2.6f * Mathf.Sin(t * 0.8f + 1f));

        if (crow != null)
        {
            crowFlap = Mathf.Max(0f, crowFlap - dt);
            float hop = crowFlap > 0f ? Mathf.Abs(Mathf.Sin(crowFlap * 22f)) * 0.06f : 0f;
            float idle = 0.008f * Mathf.Sin(t * 3.1f);
            crow.localPosition = crowBasePos + new Vector3(0f, hop + idle, 0f);
            crow.localScale = new Vector3(1f, 1f + (crowFlap > 0f ? 0.14f * Mathf.Abs(Mathf.Sin(crowFlap * 22f)) : 0f), 1f);
        }
    }

    // ---------------------------------------------------------------- mesh helpers

    static Material SpriteMat() => new Material(Shader.Find("Sprites/Default"));

    static MeshRenderer AttachMesh(Transform parent, string name, Mesh mesh, int order, out GameObject go)
    {
        go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = SpriteMat();
        mr.sortingOrder = order;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        return mr;
    }

    static GameObject MakeQuadObj(Transform parent, string name, Vector3 localPos, Vector2 size,
        Color bottom, Color top, int order)
    {
        if (_quad == null)
        {
            _quad = new Mesh { name = "SurfaceQuad" };
            _quad.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f), new Vector3(0.5f, -0.5f),
                new Vector3(-0.5f, 0.5f), new Vector3(0.5f, 0.5f),
            };
            _quad.triangles = new[] { 0, 2, 1, 1, 2, 3 };
        }
        // Per-object mesh copy so each quad can hold its own gradient colors.
        var m = Object.Instantiate(_quad);
        m.colors = new[] { bottom, bottom, top, top };
        m.RecalculateBounds();
        AttachMesh(parent, name, m, order, out var go);
        go.transform.localPosition = localPos;
        go.transform.localScale = new Vector3(size.x, size.y, 1f);
        return go;
    }

    static GameObject MakeDiscObj(Transform parent, string name, Color color, int order, float radius)
    {
        if (_disc == null)
        {
            const int SEG = 24;
            var verts = new Vector3[SEG + 1];
            var tris = new int[SEG * 3];
            verts[0] = Vector3.zero;
            for (int i = 0; i < SEG; i++)
            {
                float a = i * 2f * Mathf.PI / SEG;
                verts[i + 1] = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
            }
            for (int i = 0; i < SEG; i++)
            {
                tris[i * 3] = 0; tris[i * 3 + 1] = 1 + i; tris[i * 3 + 2] = 1 + (i + 1) % SEG;
            }
            _disc = new Mesh { name = "SurfaceDisc", vertices = verts, triangles = tris };
            _disc.RecalculateBounds();
        }
        var mr = AttachMesh(parent, name, _disc, order, out var go);
        mr.sharedMaterial.color = color;
        go.transform.localScale = new Vector3(radius, radius, 1f);
        return go;
    }

    static GameObject MakeTri(Transform parent, string name, Vector3 localPos,
        Vector2 a, Vector2 b, Vector2 c, Color color, int order)
    {
        var m = new Mesh { name = "SurfaceTri" };
        m.vertices = new[] { (Vector3)a, (Vector3)b, (Vector3)c };
        m.triangles = new[] { 0, 2, 1 };
        m.colors = new[] { color, color, color };
        m.RecalculateBounds();
        AttachMesh(parent, name, m, order, out var go);
        go.transform.localPosition = localPos;
        return go;
    }

    static GameObject MakeTrapezoid(Transform parent, string name, float deckHalf, float keelHalf,
        float depth, Color color, int order)
    {
        var m = new Mesh { name = "SurfaceHull" };
        m.vertices = new[]
        {
            new Vector3(-deckHalf, 0f), new Vector3(deckHalf, 0f),
            new Vector3(-keelHalf, -depth), new Vector3(keelHalf, -depth),
        };
        m.triangles = new[] { 0, 1, 2, 2, 1, 3 };
        Color keel = color * 0.8f; keel.a = 1f;
        m.colors = new[] { color, color, keel, keel };
        m.RecalculateBounds();
        AttachMesh(parent, name, m, order, out var go);
        return go;
    }
}
