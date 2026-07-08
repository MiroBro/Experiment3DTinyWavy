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
    [Tooltip("Where the boat tries to sit relative to her while she SWIMS, in X. NEGATIVE = trailing behind her (screen-left). It follows her at all times.")]
    public float boatOffsetX = -1.4f;
    [Tooltip("Where the boat parks relative to her while she's AT the surface (and heading up). POSITIVE = in front of her face; the crow hop-turns to face her.")]
    public float boatDockOffsetX = 1.5f;
    [Tooltip("The crow's top rowing speed, world units/sec. Below her swim speed = the gap grows while she cruises and closes when she slows or surfaces.")]
    public float boatRowSpeed = 2.5f;
    [Tooltip("How strongly the treadmill current drags the boat backward while she swims (kept in sync with the seaweed scroll so boat and ground drift together).")]
    public float waterCurrentScale = 0.5f;
    [Tooltip("SmoothDamp time for the swim up / down. Bigger = a longer, lazier ascent.")]
    public float ascendSmoothTime = 0.9f;
    [Tooltip("Stay at the surface until the player presses Dive (surfaceStayTime is ignored).")]
    public bool stayUntilDive = true;
    [Tooltip("Seconds she lingers at the surface when stayUntilDive is off (extends until all rocks have flown).")]
    public float surfaceStayTime = 3.4f;
    [Tooltip("Seconds after popping up before the rocks fly to the crow.")]
    public float sellDelay = 0.9f;
    [Tooltip("How high above the waterline her head-bob centre sits — just her face out, body idling below.")]
    public float surfaceHeadLift = 0.12f;
    [Tooltip("How much swim undulation her (submerged) body keeps while she holds position at the surface.")]
    [Range(0f, 1f)]
    public float surfaceMotionScale = 0.4f;
    [Tooltip("Nose-up body tilt at the surface, degrees: torso and tail hang down underwater while only her face is out. Her face stays level — the tilt is gaze-compensated.")]
    [Range(0f, 85f)]
    public float surfaceBodyTiltDeg = 58f;
    [Tooltip("SmoothDamp time for the boat's rowing. Small = it responds briskly.")]
    public float boatCatchUpTime = 0.7f;

    [Header("Dive (the dolphin arc back under)")]
    [Tooltip("How far FORWARD she travels over the crest of the dive arc.")]
    public float diveForwardDistance = 1.8f;
    [Tooltip("How high the arc crests above her surface position before she plunges.")]
    public float diveArcHeight = 0.5f;
    [Tooltip("How deep below the start the arc ends (she glides the rest of the way home underwater).")]
    public float diveDepth = 1.6f;
    [Tooltip("Seconds the dive arc takes, crest to splash.")]
    public float diveArcTime = 2f;
    [Tooltip("After the splash: how long the dive's forward momentum carries before she settles back over her spot. Bigger = a longer, faster-feeling glide out of the dive.")]
    public float diveGlideTime = 1.8f;

    enum Phase { Idle, Ascend, Surface, Dive, Recover }
    Phase phase = Phase.Idle;
    float phaseT;
    bool sold;
    Vector2 offset, offsetVel;
    float lookVel;
    float tiltVel;
    float motionVel;
    float faceLookVel;
    Vector2 diveStartOffset;

    // Rummage reach captured at Begin and faded out — zeroing it instantly teleported the
    // hand targets, which read as a hitch when the trip started mid-dig.
    Vector2 fadeHandNear, fadeHandFar, fadeElbowNear, fadeElbowFar;
    float reachFade;

    GameObject surfaceSet;
    Transform sky, waterline;
    Transform boat;
    Vector3 boatBasePos;
    float boatTargetX, boatXVel;
    Transform crow;
    Vector3 crowBasePos;
    float crowFlap;
    int crowFacing = 1;   // +1 = faces +X (toward a mermaid ahead of the boat), -1 = faces -X

    class FlyingRock { public Transform t; public Vector3 from, to; public float u; }
    readonly List<FlyingRock> flyingRocks = new List<FlyingRock>();
    const float RockFlightTime = 0.7f;

    static Mesh _disc, _quad;

    public bool IsActive => phase != Phase.Idle;

    /// <summary>True while she's up (or on the way up) — the UI shows "Dive" instead of "Surface".</summary>
    public bool CanDive => phase == Phase.Ascend || phase == Phase.Surface;

    /// <summary>Send her back down early (the "Dive" button). Any flying rocks finish on their own.</summary>
    public void RequestDive()
    {
        if (!CanDive) return;
        EnterDive();
    }

    void EnterDive()
    {
        phase = Phase.Dive;
        phaseT = 0f;
        diveStartOffset = offset;
    }

    public void Begin()
    {
        if (phase != Phase.Idle || swimmer == null) return;
        if (forager != null)
        {
            forager.suspended = true;
            // Capture any rummage reach and FADE it out over the ascent start — the hand
            // targets glide back to the swim pose instead of teleporting.
            fadeHandNear = forager.handNear != null ? forager.handNear.reachOffsetWorld : Vector2.zero;
            fadeHandFar = forager.handFar != null ? forager.handFar.reachOffsetWorld : Vector2.zero;
            fadeElbowNear = forager.elbowNear != null ? forager.elbowNear.reachOffsetWorld : Vector2.zero;
            fadeElbowFar = forager.elbowFar != null ? forager.elbowFar.reachOffsetWorld : Vector2.zero;
            reachFade = 1f;
        }
        offset = swimmer.forageBodyOffsetWorld;
        offsetVel = Vector2.zero;
        sold = false;
        phase = Phase.Ascend;
        phaseT = 0f;
        EnsureSurfaceSet();
        // No boat repositioning here — it lives on the surface full-time, trailing her,
        // and simply closes the last gap while she ascends.
    }

    void Update()
    {
        float dt = Time.deltaTime;
        // The boat lives on the surface full-time, trailing her — build the set up front.
        if (surfaceSet == null && swimmer != null) EnsureSurfaceSet();
        if (phase != Phase.Idle && swimmer != null)
        {
            phaseT += dt;

            // Aim the head-bob centre just above the waterline: her face breaks the surface
            // and dips with each gentle bob, while everything below the chin stays under,
            // softly undulating in place.
            float surfaceOffsetY = (surfaceY + surfaceHeadLift) - swimmer.BasePosition.y;

            switch (phase)
            {
                case Phase.Ascend:
                    // Nose up toward the light; the body starts pitching toward vertical on the way.
                    Drive(new Vector2(0f, surfaceOffsetY), -20f, surfaceBodyTiltDeg * 0.5f, 1f, dt);
                    if (Mathf.Abs(offset.y - surfaceOffsetY) < 0.12f) { phase = Phase.Surface; phaseT = 0f; }
                    break;

                case Phase.Surface:
                    // Treads water: body hangs down at the tilt, face level, eyeing the boat.
                    Drive(new Vector2(0f, surfaceOffsetY), 8f, surfaceBodyTiltDeg, surfaceMotionScale, dt);
                    if (!sold && phaseT >= sellDelay) SellToCrow();
                    if (!stayUntilDive && phaseT >= surfaceStayTime && flyingRocks.Count == 0)
                        EnterDive();
                    break;

                case Phase.Dive:
                {
                    // Dolphin arc: forward over the crest — briefly OUT of the water — then
                    // plunging in and down. The offset follows the curve directly; her nose
                    // (bone AND face, via the lookDown channel) follows the path tangent.
                    float dur = Mathf.Max(0.5f, diveArcTime);
                    float u = Mathf.Clamp01(phaseT / dur);
                    // Ease-IN only (u^1.5): gathers gently out of the hover, then the arc
                    // ends AT SPEED — smoothstep's zero end-slope braked her to a dead stop
                    // at the splash, which was the post-dive hitch. The crest bump is tied
                    // to travelled distance so the arch keeps its shape.
                    float su = Mathf.Pow(u, 1.5f);
                    Vector2 target = diveStartOffset
                        + new Vector2(diveForwardDistance * su, -diveDepth * su);
                    target.y += diveArcHeight * Mathf.Sin(Mathf.PI * Mathf.Min(1f, su / 0.55f));

                    Vector2 prev = offset;
                    offset = target;
                    offsetVel = (offset - prev) / Mathf.Max(0.0001f, dt);
                    swimmer.forageBodyOffsetWorld = offset;
                    swimmer.motionScale = Mathf.SmoothDamp(swimmer.motionScale, 1f, ref motionVel, 0.45f, Mathf.Infinity, dt);
                    // Straighten her nose-up surface tilt gently: a fast straighten made the
                    // neck/torso whip around faster than the lagged spine could follow, folding
                    // her upper body.
                    swimmer.bodyTiltDeg = Mathf.SmoothDamp(swimmer.bodyTiltDeg, 0f, ref tiltVel, 0.55f, Mathf.Infinity, dt);
                    swimmer.faceLookDownDeg = Mathf.SmoothDamp(swimmer.faceLookDownDeg, 0f, ref faceLookVel, 0.4f, Mathf.Infinity, dt);

                    // Nose along the arc: rising = up, plunging = down. She looks where she dives.
                    if (offsetVel.magnitude > 0.05f)
                    {
                        float pathAngle = Mathf.Atan2(offsetVel.y, Mathf.Max(0.6f, offsetVel.x)) * Mathf.Rad2Deg;
                        pathAngle = Mathf.Clamp(pathAngle, -65f, 40f);
                        swimmer.lookDownDeg = Mathf.SmoothDamp(swimmer.lookDownDeg, -pathAngle, ref lookVel, 0.18f, Mathf.Infinity, dt);
                    }

                    if (u >= 1f) { phase = Phase.Recover; phaseT = 0f; }
                    break;
                }

                case Phase.Recover:
                {
                    // She keeps the dive's FORWARD momentum and simply sheds it — speeding out
                    // of the splash then easing to cruise — and is only ever nudged home at a
                    // gentle capped speed. She's never pulled BACKWARD hard (facing +X while
                    // being yanked -X is what folded her spine / "broke her back"); the world
                    // scroll re-centres her the rest of the way.
                    float cruiseLift = forager != null ? forager.cruiseLift : 1.2f;
                    float tau = Mathf.Max(0.15f, diveGlideTime * 0.5f);
                    float forwardV = Mathf.Max(0f, offsetVel.x) * Mathf.Exp(-dt / tau);  // coast, never reverse
                    float homePull = Mathf.Clamp(-offset.x / 0.6f, -0.5f, 0.5f);          // gentle, capped
                    offsetVel.x = forwardV;
                    offset.x += (forwardV + homePull) * dt;
                    // Rise back to the cruise line gently so the vertical reversal at the
                    // bottom of the plunge doesn't fold her either.
                    offset.y = Mathf.SmoothDamp(offset.y, cruiseLift, ref offsetVel.y, 1.4f, Mathf.Infinity, dt);
                    swimmer.forageBodyOffsetWorld = offset;
                    DrivePose(0f, 0f, 1f, dt);
                    // Hand back to the forager once she's ROUGHLY home — its resume blend
                    // absorbs the remaining offset smoothly, so we don't have to keep her
                    // suspended for the whole long glide tail.
                    if (Mathf.Abs(offset.y - cruiseLift) < 0.15f && Mathf.Abs(offset.x) < 0.35f
                        && Mathf.Abs(swimmer.bodyTiltDeg) < 4f)
                    {
                        phase = Phase.Idle;
                        if (forager != null) forager.suspended = false;
                    }
                    break;
                }
            }
        }
        else if (swimmer != null && Mathf.Abs(swimmer.bodyTiltDeg) > 0.01f)
        {
            // Idle: bleed any leftover body tilt away (the forager never touches it).
            swimmer.bodyTiltDeg = Mathf.SmoothDamp(swimmer.bodyTiltDeg, 0f, ref tiltVel, 0.5f, Mathf.Infinity, dt);
        }

        UpdateFlyingRocks(dt);
        AnimateSet(dt);
    }

    void Drive(Vector2 targetOffset, float lookDownTarget, float tiltTarget, float motion, float dt)
    {
        offset = Vector2.SmoothDamp(offset, targetOffset, ref offsetVel, ascendSmoothTime, Mathf.Infinity, dt);
        swimmer.forageBodyOffsetWorld = offset;
        DrivePose(lookDownTarget, tiltTarget, motion, dt);
    }

    // Angles + motion + reach fade, shared by every phase regardless of how the offset moves.
    void DrivePose(float lookDownTarget, float tiltTarget, float motion, float dt)
    {
        // Everything eases — instant motionScale jumps popped the bob amplitude mid-wave.
        swimmer.motionScale = Mathf.SmoothDamp(swimmer.motionScale, motion, ref motionVel, 0.4f, Mathf.Infinity, dt);
        swimmer.lookDownDeg = Mathf.SmoothDamp(swimmer.lookDownDeg, lookDownTarget, ref lookVel, 0.5f, Mathf.Infinity, dt);
        swimmer.bodyTiltDeg = Mathf.SmoothDamp(swimmer.bodyTiltDeg, tiltTarget, ref tiltVel, 0.8f, Mathf.Infinity, dt);
        swimmer.faceLookDownDeg = Mathf.SmoothDamp(swimmer.faceLookDownDeg, 0f, ref faceLookVel, 0.5f, Mathf.Infinity, dt);

        // Fade any captured rummage reach out of the arm bones.
        if (reachFade > 0f && forager != null)
        {
            reachFade = Mathf.Max(0f, reachFade - dt / 0.55f);
            float f = reachFade * reachFade * (3f - 2f * reachFade);   // smoothstep
            if (forager.handNear != null) forager.handNear.reachOffsetWorld = fadeHandNear * f;
            if (forager.handFar != null) forager.handFar.reachOffsetWorld = fadeHandFar * f;
            if (forager.elbowNear != null) forager.elbowNear.reachOffsetWorld = fadeElbowNear * f;
            if (forager.elbowFar != null) forager.elbowFar.reachOffsetWorld = fadeElbowFar * f;
        }
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
        // NOTE: never reposition an existing boat here — it follows/trails continuously and
        // snapping it to the target on Begin() read as a teleport.
        if (surfaceSet != null) return;
        float boatX = (swimmer != null ? swimmer.transform.position.x : 0f) + boatOffsetX;

        surfaceSet = new GameObject("SurfaceSet");

        // Sky: soft vertical gradient filling the view above the waterline.
        sky = MakeQuadObj(surfaceSet.transform, "Sky", new Vector3(boatX, surfaceY + 4f, 0f), new Vector2(44f, 8f),
            new Color(0.55f, 0.78f, 0.88f), new Color(0.74f, 0.90f, 0.98f), -80).transform;

        // The waterline itself: a bright band where sea meets sky.
        waterline = MakeQuadObj(surfaceSet.transform, "Waterline", new Vector3(boatX, surfaceY, 0f), new Vector2(44f, 0.07f),
            new Color(1f, 1f, 1f, 0.55f), new Color(1f, 1f, 1f, 0.55f), -79).transform;

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
        MakeQuadObj(boat, "Mast", new Vector3(-0.30f, 0.38f, 0f), new Vector2(0.05f, 0.66f), wood, wood, -46);
        MakeTri(boat, "Pennant", new Vector3(-0.325f, 0.66f, 0f),
            new Vector2(0f, 0.05f), new Vector2(0f, -0.05f), new Vector2(-0.26f, 0f),
            new Color(0.85f, 0.25f, 0.20f), -46);

        // The crow, perched on the bow — the boat trails BEHIND her now, so the bow is the
        // +X end and the crow faces +X, toward the mermaid ahead.
        var crowGO = new GameObject("Crow");
        crowGO.transform.SetParent(boat, false);
        crowBasePos = new Vector3(0.52f, 0.16f, 0f);
        crowGO.transform.localPosition = crowBasePos;
        crow = crowGO.transform;

        Color feather = new Color(0.09f, 0.09f, 0.12f);
        var body = MakeDiscObj(crow, "Body", feather, -43, 1f);
        body.transform.localScale = new Vector3(0.16f, 0.115f, 1f);
        var head = MakeDiscObj(crow, "Head", feather, -43, 0.075f);
        head.transform.localPosition = new Vector3(0.11f, 0.10f, 0f);
        MakeTri(crow, "Beak", new Vector3(0.17f, 0.095f, 0f),
            new Vector2(0f, 0.028f), new Vector2(0f, -0.028f), new Vector2(0.09f, 0.006f),
            new Color(0.95f, 0.62f, 0.18f), -42);
        var eye = MakeDiscObj(crow, "Eye", new Color(0.9f, 0.85f, 0.7f), -42, 0.016f);
        eye.transform.localPosition = new Vector3(0.125f, 0.115f, 0f);
        MakeTri(crow, "Tail", new Vector3(-0.13f, 0.03f, 0f),
            new Vector2(0f, 0.035f), new Vector2(0f, -0.02f), new Vector2(-0.13f, 0.05f),
            feather, -43);
    }

    void AnimateSet(float dt)
    {
        if (boat == null) return;
        float t = Time.time;

        // Everything re-derives from the LIVE settings each frame, so tuning surfaceY /
        // boat offset on Bootstrap2D during play moves the whole set immediately. While she
        // heads up / sits at the surface the boat docks IN FRONT of her; otherwise it trails.
        bool docked = phase == Phase.Ascend || phase == Phase.Surface;
        if (swimmer != null)
            boatTargetX = swimmer.transform.position.x + (docked ? boatDockOffsetX : boatOffsetX);
        boatBasePos.y = surfaceY;
        if (sky != null) sky.position = new Vector3(sky.position.x, surfaceY + 4f, 0f);
        if (waterline != null) waterline.position = new Vector3(waterline.position.x, surfaceY, 0f);

        // Boat "physics": the treadmill current drags it backward exactly like the ground
        // scroll (same motionScale remap the seaweed uses), while the crow rows it toward
        // its spot near her at a capped speed. Fast mermaid → the gap grows; she slows or
        // surfaces → the current dies and the boat catches up.
        if (swimmer != null)
        {
            float t01 = Mathf.Clamp01((swimmer.motionScale - 0.6f) / 0.37f);
            float ease = t01 * t01 * (3f - 2f * t01);
            boatBasePos.x -= swimmer.cruiseSpeed * waterCurrentScale * ease * dt;
        }
        // Rowing only ever CLOSES the gap toward her; it never rows backward to re-open the
        // trailing distance. So if the boat catches all the way up to her while she's stopped
        // (rummaging), it just sits there — it only falls behind again when she swims off
        // (the current) or docks in front (surfacing). Clamp the trail target so it can't sit
        // ahead of where the boat already is.
        float rowTarget = docked ? boatTargetX : Mathf.Max(boatTargetX, boatBasePos.x);
        boatBasePos.x = Mathf.SmoothDamp(boatBasePos.x, rowTarget, ref boatXVel,
            Mathf.Max(0.05f, boatCatchUpTime), Mathf.Max(0.1f, boatRowSpeed), dt);
        boat.position = boatBasePos + Vector3.up * (0.045f * Mathf.Sin(t * 1.2f));
        boat.rotation = Quaternion.Euler(0f, 0f, 2.6f * Mathf.Sin(t * 0.8f + 1f));

        if (crow != null)
        {
            // The crow keeps its perch on the boat and flips instantly in place to face the
            // mermaid when the boat crosses her side — no hopping to the other end of the boat.
            if (swimmer != null)
            {
                int desiredFacing = boat.position.x > swimmer.transform.position.x ? -1 : 1;
                if (desiredFacing != crowFacing)
                {
                    crowFacing = desiredFacing;
                    crowFlap = 0.35f;   // a little flap on the flip
                }
            }

            crowFlap = Mathf.Max(0f, crowFlap - dt);
            float hop = crowFlap > 0f ? Mathf.Abs(Mathf.Sin(crowFlap * 22f)) * 0.06f : 0f;
            float idle = 0.008f * Mathf.Sin(t * 3.1f);
            crow.localPosition = new Vector3(crowBasePos.x, crowBasePos.y + hop + idle, 0f);
            crow.localScale = new Vector3(crowFacing,
                1f + (crowFlap > 0f ? 0.14f * Mathf.Abs(Mathf.Sin(crowFlap * 22f)) : 0f), 1f);
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
