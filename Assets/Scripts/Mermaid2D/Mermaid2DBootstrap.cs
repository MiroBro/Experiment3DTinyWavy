using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 2D side-view port of <see cref="MermaidBootstrap"/>: builds the entire mermaid + world
/// procedurally at runtime in the XY plane. Same architecture as the 3D version — a
/// swimmer-driven head, lagged bone chains for spine/arms/tail/flukes/hair, ribbon meshes
/// lofted through the live bones, a foraging loop, treadmill seaweed and an underwater
/// atmosphere — flattened to 2D and layered with sorting orders. She faces screen-right.
///
/// Defaults are the hand-tuned values from the 3D scene (long 24-segment tail, big flowing
/// flukes, thick ribbon hair locks, globalSmooth 0.37), converted from Z-forward to
/// X-forward. No twirl in 2D.
/// </summary>
[DefaultExecutionOrder(-100)]
public class Mermaid2DBootstrap : MonoBehaviour
{
    [Header("Colors (reference look)")]
    public Color skinColor = new Color(0.46f, 0.25f, 0.14f);
    public Color hipColor = new Color(0.42f, 0.22f, 0.12f);
    public Color goldColor = new Color(1f, 0.75f, 0.28f);
    public Color goldDeepColor = new Color(0.62f, 0.40f, 0.10f);
    public Color hairColor = new Color(0.80f, 0.14f, 0.09f);
    public Color hairShadowColor = new Color(0.30f, 0.08f, 0.05f);

    [Header("Spawn")]
    public Vector2 spawnPosition = Vector2.zero;

    [Header("Swim Motion (live-editable)")]
    public float porpoiseAmplitude = 0.29f;
    public float porpoiseFrequency = 0.83f;
    public float cruiseSpeed = 3f;

    [Header("Bone Lag (live-editable)")]
    [Tooltip("Multiplies every per-bone smoothTime.")]
    public float globalSmoothMultiplier = 0.37f;
    public float neckSmoothTime = 0.15f;
    public float torsoSmoothTime = 0.30f;
    public float hipSmoothTime = 0.50f;
    public float shoulderSmoothTime = 0.2f;
    public float elbowSmoothTime = 0.4f;
    public float handSmoothTime = 0.7f;
    [Tooltip("Extra multiplier on ELBOW and HAND smoothTimes only. >1 = floppier arms.")]
    [Range(0.05f, 5f)]
    public float armFlowMultiplier = 1f;

    [Header("Joint Constraints (live-editable)")]
    [Range(0f, 180f)] public float handMaxBendAngleDeg = 31.9f;
    [Range(0f, 180f)] public float elbowMaxBendAngleDeg = 97.2f;

    [Header("Tail (live-editable; rebuilds on segments/length change)")]
    [Range(3, 32)] public int tailSegments = 24;
    public float tailLength = 2.98f;
    [Tooltip("Tail half-width along its length. X = 0 at the hip, 1 at the tip.")]
    public AnimationCurve tailWidthCurve = new AnimationCurve(
        new Keyframe(0f, 0.266f, 0.44f, 0.44f),
        new Keyframe(0.279f, 0.345f, -0.146f, -0.146f),
        new Keyframe(0.719f, 0.106f, -0.541f, -0.541f),
        new Keyframe(0.894f, 0.059f, -0.229f, -0.229f),
        new Keyframe(1f, 0.025f, -0.83f, -0.83f));
    public float tailBaseSmoothTime = 0.18f;
    public float tailTipSmoothTime = 0.55f;
    [Tooltip("Extra multiplier on the TAIL bones' smoothTimes only. <1 = stiffer.")]
    [Range(0.05f, 3f)]
    public float tailFlowMultiplier = 0.3f;

    [Header("Fluke (live-editable; rebuilds on bones/span/sweep change)")]
    [Range(2, 48)] public int flukeBonesPerLobe = 21;
    [Tooltip("How far each fluke lobe extends vertically (up / down) from the tail tip.")]
    public float flukeSpan = 1.38f;
    [Tooltip("How far back each fluke lobe sweeps from the tail tip.")]
    public float flukeSweep = 1.74f;
    [Tooltip("Fluke half-width along the lobe. X = 0 at tail tip, 1 at fluke tip. Leaf-shaped.")]
    public AnimationCurve flukeWidthCurve = new AnimationCurve(
        new Keyframe(0f, 0.05f, 1.29f, 1.29f),
        new Keyframe(0.632f, 0.180f, -0.246f, -0.246f),
        new Keyframe(0.939f, 0.033f, -0.139f, -0.139f),
        new Keyframe(1f, 0.004f, -1f, -1f));
    public float flukeBaseSmoothTime = 0.1f;
    public float flukeTipSmoothTime = 0.3f;
    [Tooltip("Extra multiplier on FLUKE bones' smoothTimes. >1 = floppier, fabric-like.")]
    [Range(0.05f, 5f)]
    public float flukeFlowMultiplier = 1.52f;

    [Header("Hair (live-editable; some fields rebuild the hair on change)")]
    [Range(1, 80)] public int hairStrandCount = 26;
    [Range(3, 48)] public int hairBonesPerStrand = 34;
    public float hairStrandLength = 2.55f;
    [Tooltip("Per-strand length variance. 0.22 = lengths range ~78%-122% of hairStrandLength.")]
    [Range(0f, 1f)]
    public float hairLengthVariance = 0.221f;
    [Tooltip("Position of the hair root in the head bone's local frame (top-front of the head). Live-editable.")]
    public Vector2 hairRootOffset = new Vector2(0.16f, 0.28f);
    [Tooltip("Strands' scalp positions are spread within this radius around the hair root.")]
    [Range(0f, 0.30f)]
    public float hairScalpRadius = 0f;
    [Tooltip("Base direction strands extend from the scalp. (-1, 0.22) = back and slightly up.")]
    public Vector2 hairBaseDirection = new Vector2(-1f, 0.22f);
    [Tooltip("Hair strand half-width along its length. X = 0 at scalp, 1 at tip. Thick lock mid-strand.")]
    public AnimationCurve hairWidthCurve = new AnimationCurve(
        new Keyframe(0f, 0.01f, 1.145f, 1.145f),
        new Keyframe(0.437f, 0.338f, -0.081f, -0.081f),
        new Keyframe(1f, 0.008f, 0f, 0f));
    [Tooltip("2D-only multiplier on hair widths — the 3D strands spread in depth, but in 2D they all share one plane, so slightly thinner locks read better.")]
    [Range(0.05f, 2f)]
    public float hairWidthScale = 0.55f;
    public float hairBaseSmoothTime = 0.1f;
    public float hairTipSmoothTime = 0.5f;
    [Tooltip("Extra multiplier on hair bones' smoothTimes only. >1 = floppier hair.")]
    [Range(0.05f, 5f)]
    public float hairFlowMultiplier = 0.36f;
    [Tooltip("How wide the strands fan around the back of the head, in degrees.")]
    [Range(0f, 90f)]
    public float hairSpreadAngle = 13.1f;
    public int hairSeed = 168;
    [Tooltip("Multiplier for the body circle radii that hair gets pushed out of.")]
    [Range(0.5f, 2.5f)]
    public float hairColliderRadiusMultiplier = 1.05f;
    [Tooltip("Include the head as an avoidance circle. OFF lets strands emerge from inside the skull.")]
    public bool hairAvoidsHead = false;

    [Header("Body Shape")]
    [Tooltip("Torso half-width: thin neck → shoulders → bust → pinched waist → hips.")]
    public AnimationCurve torsoWidthCurve = new AnimationCurve(
        new Keyframe(0.00f, 0.12f), new Keyframe(0.18f, 0.26f), new Keyframe(0.32f, 0.30f),
        new Keyframe(0.52f, 0.24f), new Keyframe(0.66f, 0.20f), new Keyframe(0.82f, 0.30f),
        new Keyframe(1.00f, 0.30f));
    public AnimationCurve armWidthCurve = new AnimationCurve(
        new Keyframe(0f, 0.10f), new Keyframe(0.5f, 0.07f), new Keyframe(1f, 0.05f));

    [Header("Extras")]
    [Tooltip("Gold crown, necklace and armbands.")]
    public bool goldJewelry = true;
    [Tooltip("Spawn the two-layer flowing seaweed bed that parts around her body and hands.")]
    public bool spawnSeaweed = true;
    [Tooltip("Make her periodically pause and rummage in the grass for gems/rocks.")]
    public bool enableForaging = true;
    [Tooltip("Spawn the 2D underwater atmosphere (gradient water, god rays, motes, seabed).")]
    public bool spawnAtmosphere = true;
    [Tooltip("Golden sparkles drifting from her trailing hand.")]
    public bool spawnSparkles = true;

    [Header("Anchors (populated at runtime)")]
    public Transform root;
    public Transform driver;
    public Transform headScalp;
    public Transform hipPoint;

    // ---------------------------------------------------------------- runtime state

    class BoneEntry { public Mermaid2DBone bone; public float baseSmoothTime; }
    readonly List<BoneEntry> boneEntries = new List<BoneEntry>();

    Mermaid2DSwimmer swimmer;
    Mermaid2DBoneChain chain;
    Mermaid2DBone elbowNear, elbowFar, handNear, handFar;

    // Tail/fluke runtime state — tracked separately so we can rebuild on the fly.
    readonly List<GameObject> tailGameObjects = new List<GameObject>();
    readonly List<Mermaid2DBone> tailFlukeBones = new List<Mermaid2DBone>();
    readonly List<Mermaid2DBone> tailBonesOrdered = new List<Mermaid2DBone>();
    readonly HashSet<Mermaid2DBone> tailBoneSet = new HashSet<Mermaid2DBone>();
    readonly HashSet<Mermaid2DBone> flukeBoneSet = new HashSet<Mermaid2DBone>();
    readonly HashSet<Mermaid2DBone> armBoneSet = new HashSet<Mermaid2DBone>();

    // Hair runtime state.
    readonly List<GameObject> hairGameObjects = new List<GameObject>();
    readonly List<Mermaid2DBone> hairBones = new List<Mermaid2DBone>();
    readonly HashSet<Mermaid2DBone> hairBoneSet = new HashSet<Mermaid2DBone>();
    readonly List<Ribbon2D> hairRibbons = new List<Ribbon2D>();

    // Hair-body collision state (shared arrays, radii updated in-place each frame).
    Transform[] _hairColliderTransforms;
    float[] _hairColliderRadii;        // shared with hair bones; updated in-place
    float[] _hairColliderBaseRadii;    // unmultiplied base radii
    int _hairColliderTailStartIdx = -1;

    Mesh _discMesh;
    Mesh _quadMesh;

    int _lastTailSegments = -1;
    float _lastTailLength = float.NaN;
    int _lastFlukeBonesPerLobe = -1;
    float _lastFlukeSpan = float.NaN;
    float _lastFlukeSweep = float.NaN;
    int _lastHairStrandCount = -1;
    int _lastHairBonesPerStrand = -1;
    float _lastHairStrandLength = float.NaN;
    float _lastHairSpreadAngle = float.NaN;
    int _lastHairSeed = int.MinValue;
    float _lastHairLengthVariance = float.NaN;
    Vector2 _lastHairBaseDirection = new Vector2(float.NaN, 0f);
    float _lastHairScalpRadius = float.NaN;
    bool _lastHairAvoidsHead;

    // ---------------------------------------------------------------- sorting layers
    // backdrop -100 | rays -90 | back motes -85 | seabed -60 | back seaweed -50 |
    // far arm -15/-14 | hair locks -12..-7 | flukes 0 | tail 1 | hair blob 2 | torso 3 |
    // head 4 | front locks 5 | face 6 | near arm 8/9 | items 12 | sparkles 13 |
    // front seaweed 20 | front motes 30
    const int OrderFarArm = -15, OrderFarHand = -14, OrderFluke = 0, OrderTail = 1;
    const int OrderHairBlob = 2, OrderTorso = 3, OrderHead = 4, OrderFrontLock = 5;
    const int OrderFace = 6, OrderNearArm = 8, OrderNearHand = 9;

    void Awake()
    {
        BuildMermaid();
        WireCamera();
        EnsureForagingAndSeaweed();
        EnsureAtmosphere();
        EnsureSparkles();
        SnapshotShapeValues();
        _lastHairAvoidsHead = hairAvoidsHead;
    }

    // ---------------------------------------------------------------- build: mermaid

    void BuildMermaid()
    {
        var groupGO = new GameObject("Mermaid2D");
        groupGO.transform.position = (Vector3)spawnPosition;
        root = groupGO.transform;
        chain = groupGO.AddComponent<Mermaid2DBoneChain>();

        // Head = the driver. She faces +X.
        var headGO = new GameObject("Head");
        headGO.transform.SetParent(root, false);
        headGO.transform.localPosition = new Vector3(0.85f, 0f, 0f);
        swimmer = headGO.AddComponent<Mermaid2DSwimmer>();
        swimmer.porpoiseAmplitude = porpoiseAmplitude;
        swimmer.porpoiseFrequency = porpoiseFrequency;
        swimmer.cruiseSpeed = cruiseSpeed;
        driver = headGO.transform;
        chain.driver = driver;

        var neckBone = MakeBone("Neck", new Vector2(0.50f, 0f), driver, neckSmoothTime);
        var torsoBone = MakeBone("Torso", new Vector2(0.05f, 0f), neckBone, torsoSmoothTime);
        var hipBone = MakeBone("Hip", new Vector2(-0.55f, 0f), torsoBone, hipSmoothTime);
        hipPoint = hipBone;

        // Arms — near (screen-front) and far (behind the body, darker). Anchored to the neck.
        for (int side = 0; side < 2; side++)
        {
            bool near = side == 0;
            string sfx = near ? "Near" : "Far";
            Vector2 off = near ? Vector2.zero : new Vector2(0.04f, -0.04f);

            var shoulder = MakeBone("Shoulder" + sfx, new Vector2(0.40f, 0f) + off, neckBone, shoulderSmoothTime);
            var elbow = MakeBone("Elbow" + sfx, new Vector2(-0.18f, -0.08f) + off, shoulder, elbowSmoothTime);
            var hand = MakeBone("Hand" + sfx, new Vector2(-0.62f, -0.32f) + off, elbow, handSmoothTime);

            var elbowMB = elbow.GetComponent<Mermaid2DBone>();
            var handMB = hand.GetComponent<Mermaid2DBone>();
            elbowMB.maxBendAngleDeg = elbowMaxBendAngleDeg;
            handMB.maxBendAngleDeg = handMaxBendAngleDeg;
            armBoneSet.Add(elbowMB);
            armBoneSet.Add(handMB);
            if (near) { elbowNear = elbowMB; handNear = handMB; }
            else { elbowFar = elbowMB; handFar = handMB; }

            Color armCol = near ? skinColor : skinColor * 0.68f;
            armCol.a = 1f;
            var armRibbon = MakeRibbon("ArmRibbon" + sfx, new[] { shoulder, elbow, hand },
                armWidthCurve, 1f, 14, armCol, armCol, near ? OrderNearArm : OrderFarArm);
            armRibbon.roundCaps = true;

            MakeDisc("HandDisc" + sfx, hand, Vector2.zero, 0.085f, armCol, near ? OrderNearHand : OrderFarHand);

            if (goldJewelry && near)
                MakeQuad("Armband", elbow, new Vector2(0.10f, 0.02f), new Vector2(0.15f, 0.05f), 25f, goldColor, OrderNearArm + 1);
        }

        // Spine + arm bones capture their rest poses from the built layout.
        chain.Initialize();

        // Torso ribbon: head → neck → torso → hip, feminine profile, skin blending to gold
        // at the hip where the tail begins.
        Color torsoEnd = Color.Lerp(hipColor, goldColor, 0.45f); torsoEnd.a = 1f;
        MakeRibbon("TorsoRibbon", new[] { driver, neckBone, torsoBone, hipBone },
            torsoWidthCurve, 1f, 26, skinColor, torsoEnd, OrderTorso);

        BuildTail(hipBone);
        Transform tailTip = (tailBonesOrdered.Count > 0)
            ? tailBonesOrdered[tailBonesOrdered.Count - 1].transform : hipBone;
        BuildFlukes(tailTip);

        BuildHead(headGO.transform);
        BuildHair();
        RebuildHairColliders();
    }

    void BuildTail(Transform hipBone)
    {
        float segLen = tailLength / Mathf.Max(1, tailSegments);
        float startX = hipBone.localPosition.x;

        var tubePoints = new Transform[tailSegments + 1];
        tubePoints[0] = hipBone;
        Transform prev = hipBone;

        for (int i = 0; i < tailSegments; i++)
        {
            float tBone = (i + 1) / (float)tailSegments;
            float smoothTime = Mathf.Lerp(tailBaseSmoothTime, tailTipSmoothTime, tBone);

            float x = startX - segLen * (i + 1);
            Vector3 worldRestPos = root.position + new Vector3(x, 0f, 0f);
            var seg = MakeBoneAtRest($"Tail{i:D2}", worldRestPos, prev, new Vector3(-segLen, 0f, 0f), smoothTime);

            tubePoints[i + 1] = seg;
            var mb = seg.GetComponent<Mermaid2DBone>();
            tailFlukeBones.Add(mb);
            tailBoneSet.Add(mb);
            tailBonesOrdered.Add(mb);
            tailGameObjects.Add(seg.gameObject);
            prev = seg;
        }

        var ribbon = MakeRibbon("TailRibbon", tubePoints, tailWidthCurve, 1f, 52,
            goldColor, goldDeepColor, OrderTail);
        tailGameObjects.Add(ribbon.gameObject);
    }

    void BuildFlukes(Transform tailTip)
    {
        float spanDelta = flukeSpan / Mathf.Max(1, flukeBonesPerLobe);
        float sweepDelta = flukeSweep / Mathf.Max(1, flukeBonesPerLobe);

        for (int s = 0; s < 2; s++)
        {
            int side = (s == 0) ? 1 : -1;           // up lobe, then down lobe
            string suffix = side > 0 ? "Up" : "Down";

            var tubePoints = new Transform[flukeBonesPerLobe + 1];
            tubePoints[0] = tailTip;
            Transform prev = tailTip;
            Vector3 prevWorldPos = tailTip.position;

            for (int i = 0; i < flukeBonesPerLobe; i++)
            {
                float tBone = (i + 1) / (float)flukeBonesPerLobe;
                float smoothTime = Mathf.Lerp(flukeBaseSmoothTime, flukeTipSmoothTime, tBone);

                Vector3 delta = new Vector3(-sweepDelta, side * spanDelta, 0f);
                Vector3 boneRestPos = prevWorldPos + delta;
                var seg = MakeBoneAtRest($"Fluke{suffix}{i:D2}", boneRestPos, prev, delta, smoothTime);

                tubePoints[i + 1] = seg;
                var mb = seg.GetComponent<Mermaid2DBone>();
                tailFlukeBones.Add(mb);
                flukeBoneSet.Add(mb);
                tailGameObjects.Add(seg.gameObject);
                prev = seg;
                prevWorldPos = boneRestPos;
            }

            Color flukeTipCol = Color.Lerp(goldDeepColor, hairShadowColor, 0.3f); flukeTipCol.a = 1f;
            var ribbon = MakeRibbon($"FlukeRibbon{suffix}", tubePoints, flukeWidthCurve, 1f, 34,
                goldDeepColor, flukeTipCol, OrderFluke);
            tailGameObjects.Add(ribbon.gameObject);
        }
    }

    void BuildHead(Transform headBone)
    {
        MakeDisc("HeadDisc", headBone, Vector2.zero, 0.30f, skinColor, OrderHead);

        // Soft hair mass at the back/top of the head that the long strands flow out of.
        var blob = MakeDisc("HairVolume", headBone, new Vector2(-0.16f, 0.13f), 0.34f, hairColor, OrderHairBlob);
        blob.transform.localScale = new Vector3(0.40f, 0.32f, 1f);   // squashed bean
        blob.AddComponent<GentleBillow2D>();

        // Scalp anchor the hair bones hang from (live-editable via hairRootOffset).
        var scalpGO = new GameObject("ScalpAnchor");
        scalpGO.transform.SetParent(headBone, false);
        scalpGO.transform.localPosition = (Vector3)hairRootOffset;
        headScalp = scalpGO.transform;

        // Face: tilted almond eye + brow + a hint of lips (side view shows one of each).
        var eyeCol = new Color(0.05f, 0.025f, 0.02f);
        var eye = MakeDisc("Eye", headBone, new Vector2(0.16f, 0.04f), 1f, eyeCol, OrderFace);
        eye.transform.localScale = new Vector3(0.058f, 0.028f, 1f);
        eye.transform.localRotation = Quaternion.Euler(0f, 0f, 8f);

        MakeQuad("Brow", headBone, new Vector2(0.165f, 0.135f), new Vector2(0.13f, 0.024f), 10f,
            new Color(0.12f, 0.05f, 0.04f), OrderFace);

        var lips = MakeDisc("Lips", headBone, new Vector2(0.275f, -0.075f), 1f,
            new Color(0.62f, 0.22f, 0.16f), OrderFace);
        lips.transform.localScale = new Vector3(0.035f, 0.02f, 1f);

        if (goldJewelry)
        {
            BuildCrown(headBone);
            BuildNecklace();
        }
    }

    void BuildCrown(Transform headBone)
    {
        var go = new GameObject("Crown");
        go.transform.SetParent(headBone, false);
        go.transform.localPosition = new Vector3(0.03f, 0.27f, 0f);
        go.transform.localRotation = Quaternion.Euler(0f, 0f, -6f);

        Color baseCol = goldColor * 0.85f; baseCol.a = 1f;
        Color tipCol = Color.Lerp(goldColor, Color.white, 0.35f); tipCol.a = 1f;

        var verts = new List<Vector3>();
        var cols = new List<Color>();
        var tris = new List<int>();

        // Band.
        verts.Add(new Vector3(-0.16f, 0f, 0f)); cols.Add(baseCol);
        verts.Add(new Vector3(0.16f, 0f, 0f)); cols.Add(baseCol);
        verts.Add(new Vector3(-0.16f, 0.045f, 0f)); cols.Add(goldColor);
        verts.Add(new Vector3(0.16f, 0.045f, 0f)); cols.Add(goldColor);
        tris.AddRange(new[] { 0, 2, 1, 1, 2, 3 });

        // Three spikes.
        float[] sx = { -0.10f, 0f, 0.10f };
        float[] sh = { 0.10f, 0.16f, 0.10f };
        for (int i = 0; i < 3; i++)
        {
            int b = verts.Count;
            verts.Add(new Vector3(sx[i] - 0.045f, 0.045f, 0f)); cols.Add(goldColor);
            verts.Add(new Vector3(sx[i] + 0.045f, 0.045f, 0f)); cols.Add(goldColor);
            verts.Add(new Vector3(sx[i], 0.045f + sh[i], 0f)); cols.Add(tipCol);
            tris.AddRange(new[] { b, b + 2, b + 1 });
        }

        var mesh = new Mesh { name = "CrownMesh2D" };
        mesh.SetVertices(verts);
        mesh.SetColors(cols);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();

        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = SpriteMat(Color.white);
        mr.sortingOrder = OrderFace;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
    }

    void BuildNecklace()
    {
        var neck = root.Find("Neck");
        if (neck == null) return;
        for (int i = 0; i < 5; i++)
        {
            float ang = Mathf.Lerp(215f, 325f, i / 4f) * Mathf.Deg2Rad;
            Vector2 p = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * 0.17f;
            Color c = (i == 2) ? Color.Lerp(goldColor, Color.white, 0.3f) : goldColor;
            MakeDisc($"NecklaceBead{i}", neck, p, (i == 2) ? 0.034f : 0.024f, c, OrderFrontLock);
        }
    }

    // ---------------------------------------------------------------- build: hair

    void BuildHair()
    {
        if (headScalp == null) return;

        var prevState = Random.state;
        Random.InitState(hairSeed);

        Vector2 baseDir = hairBaseDirection.sqrMagnitude > 0.0001f
            ? hairBaseDirection.normalized
            : Vector2.left;

        for (int s = 0; s < hairStrandCount; s++)
        {
            // Per-strand scalp anchor (spread within hairScalpRadius so wide scalps have no
            // bald central point).
            Vector2 disc = Random.insideUnitCircle * hairScalpRadius;
            var strandScalpGO = new GameObject($"ScalpAnchor{s:D2}");
            strandScalpGO.transform.SetParent(headScalp, false);
            strandScalpGO.transform.localPosition = new Vector3(disc.x, disc.y, 0f);
            Transform strandScalp = strandScalpGO.transform;
            hairGameObjects.Add(strandScalpGO);

            // Per-strand variance — fan angle and length.
            float t = (hairStrandCount > 1) ? (float)s / (hairStrandCount - 1) : 0.5f;
            float angDeg = Mathf.Lerp(-hairSpreadAngle, hairSpreadAngle, t) + Random.Range(-5f, 5f);
            float rad = angDeg * Mathf.Deg2Rad;
            float ca = Mathf.Cos(rad), sa = Mathf.Sin(rad);
            Vector2 strandDir = new Vector2(baseDir.x * ca - baseDir.y * sa, baseDir.x * sa + baseDir.y * ca);

            float lenMul = 1f + Random.Range(-hairLengthVariance, hairLengthVariance);
            float strandLen = hairStrandLength * Mathf.Max(0.05f, lenMul);
            float segLen = strandLen / Mathf.Max(1, hairBonesPerStrand);

            int N = hairBonesPerStrand + 1;
            var tubePoints = new Transform[N];
            tubePoints[0] = strandScalp;

            Transform prev = strandScalp;
            Vector3 prevWorldPos = strandScalp.position;

            for (int i = 0; i < hairBonesPerStrand; i++)
            {
                float tBone = (i + 1) / (float)hairBonesPerStrand;
                float smoothTime = Mathf.Lerp(hairBaseSmoothTime, hairTipSmoothTime, tBone);

                Vector3 step = (Vector3)(strandDir * segLen);
                Vector3 boneRestPos = prevWorldPos + step;
                var seg = MakeBoneAtRest($"Hair{s:D2}_{i:D2}", boneRestPos, prev, step, smoothTime);

                tubePoints[i + 1] = seg;
                var mb = seg.GetComponent<Mermaid2DBone>();
                hairBones.Add(mb);
                hairBoneSet.Add(mb);
                hairGameObjects.Add(seg.gameObject);
                prev = seg;
                prevWorldPos = boneRestPos;
            }

            // Every fifth lock flows in FRONT of the torso (over the shoulder); the rest
            // stream behind the body at staggered depths.
            bool frontLock = (s % 5 == 2);
            int order = frontLock ? OrderFrontLock : (-12 + (s % 6));

            Color start = Color.Lerp(hairColor, Color.Lerp(hairColor, goldColor, 0.25f), Random.value * 0.5f);
            start.a = 1f;
            Color end = Color.Lerp(hairColor, hairShadowColor, 0.55f); end.a = 1f;

            var ribbon = MakeRibbon($"HairRibbon{s:D2}", tubePoints, hairWidthCurve, hairWidthScale,
                44, start, end, order);
            hairRibbons.Add(ribbon);
            hairGameObjects.Add(ribbon.gameObject);
        }

        Random.state = prevState;
    }

    void RebuildHair()
    {
        if (root == null || chain == null) return;

        for (int i = 0; i < hairBones.Count; i++)
        {
            var bone = hairBones[i];
            if (bone != null)
            {
                chain.bones.Remove(bone);
                boneEntries.RemoveAll(e => e.bone == bone);
            }
        }
        hairBones.Clear();
        hairBoneSet.Clear();
        hairRibbons.Clear();

        for (int i = 0; i < hairGameObjects.Count; i++)
            if (hairGameObjects[i] != null) Destroy(hairGameObjects[i]);
        hairGameObjects.Clear();

        BuildHair();
        RebuildHairColliders();
    }

    void RebuildHairColliders()
    {
        var transforms = new List<Transform>();
        var radii = new List<float>();

        if (hairAvoidsHead && driver != null) { transforms.Add(driver); radii.Add(0.32f); }
        var neck = root != null ? root.Find("Neck") : null;
        if (neck != null) { transforms.Add(neck); radii.Add(0.18f); }
        var torso = root != null ? root.Find("Torso") : null;
        if (torso != null) { transforms.Add(torso); radii.Add(0.30f); }
        if (hipPoint != null) { transforms.Add(hipPoint); radii.Add(0.30f); }

        // Tail bones — radii refreshed live from tailWidthCurve in Update.
        _hairColliderTailStartIdx = transforms.Count;
        for (int i = 0; i < tailBonesOrdered.Count; i++)
        {
            var b = tailBonesOrdered[i];
            if (b == null) continue;
            transforms.Add(b.transform);
            radii.Add(0.2f);
        }

        _hairColliderTransforms = transforms.ToArray();
        _hairColliderBaseRadii = radii.ToArray();
        _hairColliderRadii = new float[_hairColliderBaseRadii.Length];
        UpdateHairColliderRadii();

        for (int i = 0; i < hairBones.Count; i++)
        {
            if (hairBones[i] == null) continue;
            hairBones[i].avoidanceCircles = _hairColliderTransforms;
            hairBones[i].avoidanceRadii = _hairColliderRadii;
        }
    }

    void UpdateHairColliderRadii()
    {
        if (_hairColliderRadii == null || _hairColliderBaseRadii == null) return;
        float buf = Mathf.Max(0.1f, hairColliderRadiusMultiplier);
        int tailCount = tailBonesOrdered.Count;
        int idx0 = _hairColliderTailStartIdx;
        for (int i = 0; i < _hairColliderRadii.Length; i++)
        {
            float baseR;
            if (idx0 >= 0 && i >= idx0 && tailCount > 0 && (i - idx0) < tailCount)
            {
                // Tail entries sample the live tail width curve.
                float t = (i - idx0 + 1) / (float)tailCount;
                baseR = tailWidthCurve.Evaluate(t);
            }
            else
            {
                baseR = _hairColliderBaseRadii[i];
            }
            _hairColliderRadii[i] = baseR * buf;
        }
    }

    void RebuildTailAndFluke()
    {
        if (root == null || chain == null) return;

        for (int i = 0; i < tailFlukeBones.Count; i++)
        {
            var bone = tailFlukeBones[i];
            if (bone != null)
            {
                chain.bones.Remove(bone);
                boneEntries.RemoveAll(e => e.bone == bone);
            }
        }
        tailFlukeBones.Clear();
        tailBoneSet.Clear();
        flukeBoneSet.Clear();
        tailBonesOrdered.Clear();

        for (int i = 0; i < tailGameObjects.Count; i++)
            if (tailGameObjects[i] != null) Destroy(tailGameObjects[i]);
        tailGameObjects.Clear();

        var hipBone = hipPoint != null ? hipPoint : root.Find("Hip");
        if (hipBone != null)
        {
            BuildTail(hipBone);
            Transform tailTip = (tailBonesOrdered.Count > 0)
                ? tailBonesOrdered[tailBonesOrdered.Count - 1].transform : hipBone;
            BuildFlukes(tailTip);
        }

        RebuildHairColliders();
    }

    // ---------------------------------------------------------------- build: world

    void WireCamera()
    {
        Camera cam = Camera.main;
        if (cam == null) { Debug.LogWarning("Mermaid2DBootstrap: no Camera tagged MainCamera found."); return; }
        var follow = cam.GetComponent<Camera2DFollow>();
        if (follow == null) follow = cam.gameObject.AddComponent<Camera2DFollow>();
        follow.target = driver;
    }

    void EnsureForagingAndSeaweed()
    {
        GemInventory inventory = null;
        if (enableForaging)
        {
            inventory = FindAnyObjectByType<GemInventory>();
            if (inventory == null)
                inventory = new GameObject("GemInventory").AddComponent<GemInventory>();
        }

        Transform[] bodyCircles = null;
        float[] bodyRadii = null;
        if (spawnSeaweed)
        {
            var cols = new List<Transform>();
            var rad = new List<float>();
            if (driver != null) { cols.Add(driver); rad.Add(0.40f); }
            var neck = root.Find("Neck");
            if (neck != null) { cols.Add(neck); rad.Add(0.22f); }
            var torso = root.Find("Torso");
            if (torso != null) { cols.Add(torso); rad.Add(0.36f); }
            if (hipPoint != null) { cols.Add(hipPoint); rad.Add(0.36f); }
            if (handNear != null) { cols.Add(handNear.transform); rad.Add(0.17f); }
            if (handFar != null) { cols.Add(handFar.transform); rad.Add(0.17f); }
            bodyCircles = cols.ToArray();
            bodyRadii = rad.ToArray();

            // Two depth layers: a darker bed behind her, a brighter sparser fringe in front
            // that her hands visibly dig into.
            MakeSeaweedLayer("SeaweedBack", 170, -1.02f, 0.72f, -50, 1234, bodyCircles, bodyRadii);
            MakeSeaweedLayer("SeaweedFront", 110, -1.18f, 1.08f, 20, 4321, bodyCircles, bodyRadii);
        }

        if (enableForaging && FindAnyObjectByType<Mermaid2DForager>() == null)
        {
            var forageGO = new GameObject("Mermaid2DForager");
            var forager = forageGO.AddComponent<Mermaid2DForager>();
            forager.swimmer = swimmer;
            forager.handNear = handNear;
            forager.handFar = handFar;
            forager.elbowNear = elbowNear;
            forager.elbowFar = elbowFar;
            forager.inventory = inventory;
        }
    }

    void MakeSeaweedLayer(string name, int count, float rootY, float brightness, int order,
        int seed, Transform[] circles, float[] radii)
    {
        var go = new GameObject(name);
        go.transform.position = new Vector3(spawnPosition.x, 0f, 0f);
        var field = go.AddComponent<Seaweed2D>();
        field.patchCenterX = spawnPosition.x;
        field.rootY = rootY;
        field.bladeCount = count;
        field.brightness = brightness;
        field.seed = seed;
        if (order > 0) field.heightRange = new Vector2(0.62f, 1.05f);
        field.bodyCircles = circles;
        field.bodyRadii = radii;
        field.swimmer = swimmer;

        var mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterial = SpriteMat(Color.white);
        mr.sortingOrder = order;
    }

    void EnsureAtmosphere()
    {
        if (!spawnAtmosphere) return;
        if (FindAnyObjectByType<Underwater2DAtmosphere>() != null) return;
        new GameObject("Underwater2DAtmosphere").AddComponent<Underwater2DAtmosphere>();
    }

    void EnsureSparkles()
    {
        if (!spawnSparkles) return;
        if (FindAnyObjectByType<Sparkle2DSpawner>() != null) return;
        var go = new GameObject("Sparkles2D");
        var spawner = go.AddComponent<Sparkle2DSpawner>();
        spawner.handTransform = (handNear != null) ? handNear.transform : null;
        spawner.swimmer = swimmer;
    }

    // ---------------------------------------------------------------- live editing

    void SnapshotShapeValues()
    {
        _lastTailSegments = tailSegments;
        _lastTailLength = tailLength;
        _lastFlukeBonesPerLobe = flukeBonesPerLobe;
        _lastFlukeSpan = flukeSpan;
        _lastFlukeSweep = flukeSweep;
        _lastHairStrandCount = hairStrandCount;
        _lastHairBonesPerStrand = hairBonesPerStrand;
        _lastHairStrandLength = hairStrandLength;
        _lastHairSpreadAngle = hairSpreadAngle;
        _lastHairSeed = hairSeed;
        _lastHairLengthVariance = hairLengthVariance;
        _lastHairBaseDirection = hairBaseDirection;
        _lastHairScalpRadius = hairScalpRadius;
    }

    bool TailFlukeShapeChanged()
    {
        return tailSegments != _lastTailSegments
            || !Mathf.Approximately(tailLength, _lastTailLength)
            || flukeBonesPerLobe != _lastFlukeBonesPerLobe
            || !Mathf.Approximately(flukeSpan, _lastFlukeSpan)
            || !Mathf.Approximately(flukeSweep, _lastFlukeSweep);
    }

    bool HairShapeChanged()
    {
        return hairStrandCount != _lastHairStrandCount
            || hairBonesPerStrand != _lastHairBonesPerStrand
            || !Mathf.Approximately(hairStrandLength, _lastHairStrandLength)
            || !Mathf.Approximately(hairSpreadAngle, _lastHairSpreadAngle)
            || hairSeed != _lastHairSeed
            || !Mathf.Approximately(hairLengthVariance, _lastHairLengthVariance)
            || (hairBaseDirection - _lastHairBaseDirection).sqrMagnitude > 0.0001f
            || !Mathf.Approximately(hairScalpRadius, _lastHairScalpRadius);
    }

    void Update()
    {
        // 1. Live smoothTime updates with per-group flow multipliers.
        float gm = Mathf.Max(0f, globalSmoothMultiplier);
        float tm = Mathf.Max(0.01f, tailFlowMultiplier);
        float fm = Mathf.Max(0.01f, flukeFlowMultiplier);
        float hm = Mathf.Max(0.01f, hairFlowMultiplier);
        float am = Mathf.Max(0.01f, armFlowMultiplier);
        for (int i = 0; i < boneEntries.Count; i++)
        {
            var e = boneEntries[i];
            if (e.bone == null) continue;
            float mult = gm;
            if (tailBoneSet.Contains(e.bone)) mult *= tm;
            else if (flukeBoneSet.Contains(e.bone)) mult *= fm;
            else if (hairBoneSet.Contains(e.bone)) mult *= hm;
            else if (armBoneSet.Contains(e.bone)) mult *= am;
            e.bone.smoothTime = e.baseSmoothTime * mult;
        }

        // 2. Live swimmer params.
        if (swimmer != null)
        {
            swimmer.porpoiseAmplitude = porpoiseAmplitude;
            swimmer.porpoiseFrequency = porpoiseFrequency;
            swimmer.cruiseSpeed = cruiseSpeed;
        }

        // 3. Live joint constraints.
        if (handNear != null) handNear.maxBendAngleDeg = handMaxBendAngleDeg;
        if (handFar != null) handFar.maxBendAngleDeg = handMaxBendAngleDeg;
        if (elbowNear != null) elbowNear.maxBendAngleDeg = elbowMaxBendAngleDeg;
        if (elbowFar != null) elbowFar.maxBendAngleDeg = elbowMaxBendAngleDeg;

        // 4. Live-editable hair root position + hair widths.
        if (headScalp != null) headScalp.localPosition = (Vector3)hairRootOffset;
        for (int i = 0; i < hairRibbons.Count; i++)
            if (hairRibbons[i] != null) hairRibbons[i].widthScale = hairWidthScale;

        // 5. Shape changes — rebuild affected groups.
        if (TailFlukeShapeChanged()) RebuildTailAndFluke();
        if (HairShapeChanged()) RebuildHair();
        SnapshotShapeValues();

        // 6. Hair-body collider refresh.
        if (hairAvoidsHead != _lastHairAvoidsHead)
        {
            _lastHairAvoidsHead = hairAvoidsHead;
            RebuildHairColliders();
        }
        UpdateHairColliderRadii();
    }

    // ---------------------------------------------------------------- factory helpers

    Transform MakeBone(string name, Vector2 localPos, Transform anchor, float baseSmoothTime)
    {
        var go = new GameObject(name);
        go.transform.SetParent(root, false);
        go.transform.localPosition = (Vector3)localPos;
        var bone = go.AddComponent<Mermaid2DBone>();
        bone.anchor = anchor;
        bone.smoothTime = baseSmoothTime * Mathf.Max(0f, globalSmoothMultiplier);
        chain.bones.Add(bone);
        boneEntries.Add(new BoneEntry { bone = bone, baseSmoothTime = baseSmoothTime });
        return go.transform;
    }

    Transform MakeBoneAtRest(string name, Vector3 worldRestPos, Transform anchor,
        Vector3 localOffsetFromAnchor, float baseSmoothTime)
    {
        var go = new GameObject(name);
        go.transform.SetParent(root, false);
        worldRestPos.z = 0f;
        go.transform.position = worldRestPos;
        var bone = go.AddComponent<Mermaid2DBone>();
        bone.anchor = anchor;
        bone.smoothTime = baseSmoothTime * Mathf.Max(0f, globalSmoothMultiplier);
        bone.InitializeWithExplicitOffset(localOffsetFromAnchor, Quaternion.identity);
        chain.bones.Add(bone);
        boneEntries.Add(new BoneEntry { bone = bone, baseSmoothTime = baseSmoothTime });
        return go.transform;
    }

    Ribbon2D MakeRibbon(string name, Transform[] points, AnimationCurve widthCurve, float widthScale,
        int samples, Color colorStart, Color colorEnd, int sortingOrder)
    {
        var go = new GameObject(name);
        go.transform.SetParent(root, false);
        var ribbon = go.AddComponent<Ribbon2D>();
        ribbon.points = points;
        ribbon.widthCurve = widthCurve;
        ribbon.widthScale = widthScale;
        ribbon.samples = samples;
        ribbon.colorStart = colorStart;
        ribbon.colorEnd = colorEnd;
        var mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterial = SpriteMat(Color.white);
        mr.sortingOrder = sortingOrder;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        return ribbon;
    }

    GameObject MakeDisc(string name, Transform parent, Vector2 localPos, float radius, Color color, int sortingOrder)
    {
        if (_discMesh == null) _discMesh = BuildDiscMesh(28);
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = (Vector3)localPos;
        go.transform.localScale = new Vector3(radius, radius, 1f);
        go.AddComponent<MeshFilter>().sharedMesh = _discMesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = SpriteMat(color);
        mr.sortingOrder = sortingOrder;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        return go;
    }

    GameObject MakeQuad(string name, Transform parent, Vector2 localPos, Vector2 size, float rotDeg,
        Color color, int sortingOrder)
    {
        if (_quadMesh == null) _quadMesh = BuildQuadMesh();
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = (Vector3)localPos;
        go.transform.localRotation = Quaternion.Euler(0f, 0f, rotDeg);
        go.transform.localScale = new Vector3(size.x, size.y, 1f);
        go.AddComponent<MeshFilter>().sharedMesh = _quadMesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = SpriteMat(color);
        mr.sortingOrder = sortingOrder;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        return go;
    }

    static Mesh BuildDiscMesh(int segments)
    {
        var verts = new Vector3[segments + 1];
        var cols = new Color[segments + 1];
        var tris = new int[segments * 3];
        verts[0] = Vector3.zero;
        cols[0] = Color.white;
        for (int i = 0; i < segments; i++)
        {
            float ang = i * 2f * Mathf.PI / segments;
            verts[i + 1] = new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0f);
            cols[i + 1] = Color.white;
        }
        int t = 0;
        for (int i = 0; i < segments; i++)
        {
            tris[t++] = 0;
            tris[t++] = 1 + i;
            tris[t++] = 1 + (i + 1) % segments;
        }
        var m = new Mesh { name = "DiscMesh2D" };
        m.vertices = verts; m.colors = cols; m.triangles = tris;
        m.RecalculateBounds();
        return m;
    }

    static Mesh BuildQuadMesh()
    {
        var m = new Mesh { name = "QuadMesh2D" };
        m.vertices = new[]
        {
            new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
            new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f),
        };
        m.colors = new[] { Color.white, Color.white, Color.white, Color.white };
        m.triangles = new[] { 0, 2, 1, 1, 2, 3 };
        m.RecalculateBounds();
        return m;
    }

    static Material SpriteMat(Color c)
    {
        return new Material(Shader.Find("Sprites/Default")) { color = c };
    }
}
