using UnityEngine;
using System.Collections.Generic;

[DefaultExecutionOrder(-100)]
public class MermaidBootstrap : MonoBehaviour
{
    [Header("Colors (mermaid art palette by default)")]
    public Color skinColor  = new Color(0.93f, 0.78f, 0.62f);
    public Color torsoColor = new Color(0.78f, 0.55f, 0.20f);
    public Color hipColor   = new Color(0.50f, 0.32f, 0.10f);
    public Color jointColor = new Color(0.93f, 0.78f, 0.62f);
    public Color handColor  = new Color(0.93f, 0.78f, 0.62f);
    public Color tailColor  = new Color(0.45f, 0.28f, 0.10f);
    public Color flukeColor = new Color(0.30f, 0.18f, 0.07f);
    public Color hairColor  = new Color(0.55f, 0.18f, 0.08f);

    [Header("Spawn")]
    public Vector3 spawnPosition = Vector3.zero;

    [Header("Swim Motion (live-editable)")]
    public float porpoiseAmplitude = 0.35f;
    public float porpoiseFrequency = 0.9f;
    public float cruiseSpeed = 3f;

    [Header("Bone Lag (live-editable)")]
    [Tooltip("Multiplies every per-bone smoothTime. 1.0 = tuned defaults.")]
    public float globalSmoothMultiplier = 1f;

    public float neckSmoothTime = 0.15f;
    public float torsoSmoothTime = 0.30f;
    public float hipSmoothTime = 0.50f;
    public float shoulderSmoothTime = 0f;
    public float elbowSmoothTime = 0.40f;
    public float handSmoothTime = 0.70f;
    [Tooltip("Extra multiplier on ELBOW and HAND smoothTimes only. Use this to add visible flow/delay to the arms without unstickying the shoulders. >1 = floppier arms; 1.5–2.5 makes the upper arm clearly lag the spine.")]
    [Range(0.05f, 5f)]
    public float armFlowMultiplier = 1f;

    [Header("Joint Constraints (live-editable)")]
    [Range(0f, 180f)]
    public float handMaxBendAngleDeg = 30f;
    [Range(0f, 180f)]
    public float elbowMaxBendAngleDeg = 180f;

    [Header("Tail (live-editable; rebuilds on segments/length change)")]
    [Range(3, 24)]
    public int tailSegments = 8;
    public float tailLength = 1.6f;
    [Tooltip("Tail radius along its length. X-axis = 0 at the hip, 1 at the tip. Edit this curve to make a thin → thick → thin shape, or any silhouette you like.")]
    public AnimationCurve tailRadiusCurve = new AnimationCurve(
        new Keyframe(0f, 0.42f),
        new Keyframe(1f, 0.10f));
    [Range(6, 32)]
    public int tailTubeSides = 16;
    public float tailBaseSmoothTime = 0.18f;
    public float tailTipSmoothTime = 0.55f;
    [Tooltip("Extra multiplier on the TAIL bones' smoothTimes only. <1 = stiffer (wave settles faster, less floppy). >1 = floppier. Independent of globalSmoothMultiplier.")]
    [Range(0.05f, 3f)]
    public float tailFlowMultiplier = 1f;

    [Header("Fluke (live-editable; rebuilds on bones/span/sweep change)")]
    [Range(2, 48)]
    public int flukeBonesPerLobe = 4;
    [Tooltip("How far each fluke lobe extends sideways from the tail tip.")]
    public float flukeSpan = 0.55f;
    [Tooltip("How far back (along -Z) each fluke lobe sweeps from the tail tip.")]
    public float flukeSweepZ = -0.30f;
    [Tooltip("Fluke radius along the lobe length. X = 0 at tail tip, 1 at fluke tip. Default is leaf-shaped: small at base (so it merges with the tail's narrow tip), wide in the middle, narrow at the end.")]
    public AnimationCurve flukeRadiusCurve = new AnimationCurve(
        new Keyframe(0f, 0.04f),
        new Keyframe(0.35f, 0.22f),
        new Keyframe(1f, 0.04f));
    [Tooltip("0.05 = very flat horizontal fluke; 1 = round.")]
    [Range(0.05f, 1f)]
    public float flukeAspectRatio = 0.20f;
    [Range(6, 24)]
    public int flukeTubeSides = 12;
    public float flukeBaseSmoothTime = 0.10f;
    public float flukeTipSmoothTime = 0.30f;
    [Tooltip("Extra multiplier on the FLUKE bones' smoothTimes only. <1 = stiffer fluke (rigid). >1 = floppier, fabric-like. Try 2–3 for a flowy cloth feel.")]
    [Range(0.05f, 5f)]
    public float flukeFlowMultiplier = 1f;

    [Header("Hair (live-editable; some fields rebuild the hair on change)")]
    [Range(1, 120)]
    public int hairStrandCount = 14;
    [Range(3, 48)]
    public int hairBonesPerStrand = 8;
    public float hairStrandLength = 1.6f;
    [Tooltip("Per-strand length variance. 0 = all strands same length; 0.4 = lengths randomly range from 60%–140% of hairStrandLength.")]
    [Range(0f, 1f)]
    public float hairLengthVariance = 0.20f;
    [Tooltip("Position of the hair root in the head's local frame. Live: drag in Inspector to move where the hair attaches to the head, no rebuild needed.")]
    public Vector3 hairRootOffset = new Vector3(0f, 0.20f, -0.25f);
    [Tooltip("Strands' scalp positions are spread within this radius (head local units) around the hair root, on the X-Y plane. Higher = wider scalp, fuller look. Set 0 to put all strands at one point.")]
    [Range(0f, 0.30f)]
    public float hairScalpRadius = 0.10f;
    [Tooltip("Base direction strands extend from the scalp, in the head's local frame. (0,0,-1) = straight back. (0, 0.3, -1) = arc up-then-back so strands stay above the body. Per-strand pitch/yaw randomness is applied on top.")]
    public Vector3 hairBaseDirection = new Vector3(0f, 0f, -1f);
    [Tooltip("Hair strand radius along its length. X = 0 at scalp, 1 at tip.")]
    public AnimationCurve hairRadiusCurve = new AnimationCurve(
        new Keyframe(0f, 0.020f),
        new Keyframe(1f, 0.008f));
    [Tooltip("Hair strand FLATNESS along its length. 1 = round, <1 = flat (ribbon-like). Edit to start round at the scalp and flatten toward the tip, or any custom shape.")]
    public AnimationCurve hairAspectCurve = new AnimationCurve(
        new Keyframe(0f, 1f),
        new Keyframe(1f, 1f));
    [Range(3, 12)]
    public int hairTubeSides = 6;
    public float hairBaseSmoothTime = 0.10f;
    public float hairTipSmoothTime = 0.50f;
    [Tooltip("Extra multiplier on hair bones' smoothTimes only. >1 = floppier hair.")]
    [Range(0.05f, 5f)]
    public float hairFlowMultiplier = 1f;
    [Tooltip("How wide the strands fan around the back of the head, in degrees.")]
    [Range(0f, 90f)]
    public float hairSpreadAngle = 35f;
    [Tooltip("Random seed for strand directions. Same seed = same look.")]
    public int hairSeed = 42;
    [Tooltip("Multiplier for the body sphere-collider radii that hair gets pushed out of. 1.0 = match the body's actual size; >1 = hair stays a bit further from the body.")]
    [Range(0.5f, 2.5f)]
    public float hairColliderRadiusMultiplier = 1.05f;
    [Tooltip("Include the head as an avoidance sphere. OFF lets you anchor the hair inside the head — strands emerge from inside the skull rather than being pushed to the surface.")]
    public bool hairAvoidsHead = false;

    [Header("Textured Hair (live-editable)")]
    [Tooltip("ON = use the Hair/Curly shader for a coily/textured natural hair look. OFF = default smooth look. Geometry is unchanged either way.")]
    public bool texturedHair = false;
    [Tooltip("Curl frequency along the strand (cycles per strand length). Higher = tighter curls. Try 30–60 for typical coily textures.")]
    [Range(1f, 200f)]
    public float hairCurlFrequency = 35f;
    [Tooltip("Curl band light/dark depth. Higher = more pronounced bands.")]
    [Range(0f, 1f)]
    public float hairCurlDepth = 0.45f;
    [Tooltip("Curl band sharpness. 0 = soft sine; 1 = hard square edges.")]
    [Range(0f, 1f)]
    public float hairCurlSharpness = 0.4f;
    [Tooltip("How much each strand's curl phase is offset — keeps strands from looking lockstep.")]
    [Range(0f, 1f)]
    public float hairCurlPhaseScramble = 0.6f;
    [Tooltip("Texture noise scale.")]
    [Range(0.1f, 200f)]
    public float hairNoiseScale = 60f;
    [Tooltip("Texture noise strength. Higher = fuzzier strand surface.")]
    [Range(0f, 0.5f)]
    public float hairNoiseStrength = 0.12f;
    [Tooltip("Color in the dark portions of the curls.")]
    public Color hairCurlShadowColor = new Color(0.18f, 0.07f, 0.04f);
    [Tooltip("Rim/sheen highlight color on strand edges.")]
    public Color hairCurlRimColor = new Color(1.0f, 0.7f, 0.45f);

    [Header("Seaweed / Foraging (spawned at runtime)")]
    [Tooltip("Spawn a flowing seaweed bed beneath her that reacts to her body and hands.")]
    public bool spawnSeaweed = true;
    [Tooltip("Make her periodically pause and rummage in the grass for gems/rocks.")]
    public bool enableForaging = true;
    [Tooltip("Spawn the gorgeous underwater atmosphere (post FX, fog, gradient sky, god rays, caustic seabed, drifting motes).")]
    public bool spawnAtmosphere = true;
    // NOTE: seaweed + foraging tuning lives on the SeaweedField and MermaidForager components
    // (created at runtime, so select them in the Hierarchy while playing to tweak). It is
    // intentionally NOT mirrored here — Bootstrap fields already saved in the scene freeze at
    // their first value, so mirroring them would silently override the live defaults.

    [Header("Anchors (populated at runtime)")]
    public Transform root;
    public Transform driver;
    public Transform headPoint;
    public Transform hipPoint;

    class BoneEntry { public MermaidBone bone; public float baseSmoothTime; }
    readonly List<BoneEntry> boneEntries = new List<BoneEntry>();

    MermaidSwimmer swimmer;
    MermaidBone elbowL, elbowR, handL, handR;
    MermaidBoneChain chain;

    // Tail/fluke runtime state — tracked separately so we can rebuild on the fly.
    readonly List<GameObject> tailGameObjects = new List<GameObject>();
    readonly List<MermaidBone> tailFlukeBones = new List<MermaidBone>();
    readonly HashSet<MermaidBone> tailBoneSet = new HashSet<MermaidBone>();
    readonly HashSet<MermaidBone> flukeBoneSet = new HashSet<MermaidBone>();
    readonly HashSet<MermaidBone> armBoneSet = new HashSet<MermaidBone>();
    TubeRenderer tailTube;
    float[] tailTubeRadii;
    readonly TubeRenderer[] flukeTubes = new TubeRenderer[2];
    readonly float[][] flukeTubeRadii = new float[2][];

    // Hair runtime state.
    readonly List<GameObject> hairGameObjects = new List<GameObject>();
    readonly List<MermaidBone> hairBones = new List<MermaidBone>();
    readonly HashSet<MermaidBone> hairBoneSet = new HashSet<MermaidBone>();
    readonly List<TubeRenderer> hairTubes = new List<TubeRenderer>();
    readonly List<float[]> hairTubeRadiiList = new List<float[]>();
    readonly List<float[]> hairTubeAspectsList = new List<float[]>();

    // Hair-body collision state.
    readonly List<MermaidBone> tailBonesOrdered = new List<MermaidBone>();
    Transform[] _hairColliderTransforms;
    float[] _hairColliderRadii;       // shared with hair bones; updated in-place
    float[] _hairColliderBaseRadii;   // unmultiplied base radii
    int _hairColliderTailStartIdx = -1;

    int _lastTailSegments = -1;
    float _lastTailLength = float.NaN;
    int _lastFlukeBonesPerLobe = -1;
    float _lastFlukeSpan = float.NaN;
    float _lastFlukeSweepZ = float.NaN;
    Shader _curlyHairShader;
    bool _curlyHairShaderResolved;
    Material _defaultLitMatTemplate;

    int _lastHairStrandCount = -1;
    int _lastHairBonesPerStrand = -1;
    float _lastHairStrandLength = float.NaN;
    float _lastHairSpreadAngle = float.NaN;
    int _lastHairSeed = int.MinValue;
    float _lastHairLengthVariance = float.NaN;
    Vector3 _lastHairBaseDirection = new Vector3(float.NaN, 0f, 0f);
    bool _lastHairAvoidsHead;
    float _lastHairScalpRadius = float.NaN;

    void Awake()
    {
        BuildMermaid();
        WireCamera();
        EnsureSparkles();
        EnsureForagingAndSeaweed();
        EnsureAtmosphere();
        SnapshotShapeValues();
    }

    void EnsureSparkles()
    {
        if (FindAnyObjectByType<SparkleSpawner>() != null) return;
        var go = new GameObject("Sparkles");
        var spawner = go.AddComponent<SparkleSpawner>();
        spawner.handTransform = (handR != null) ? handR.transform : null;
        spawner.swimmer = swimmer;
        var ui = go.AddComponent<SparkleUI>();
        ui.spawner = spawner;
    }

    void EnsureForagingAndSeaweed()
    {
        // Shared inventory used by the forager and shown in the on-screen "Treasure" panel.
        GemInventory inventory = null;
        if (enableForaging)
        {
            inventory = FindAnyObjectByType<GemInventory>();
            if (inventory == null)
                inventory = new GameObject("GemInventory").AddComponent<GemInventory>();
        }

        // Body-avoidance spheres the seaweed is pushed out of. Reuses the same approach as
        // the hair: a transform + radius per body part. Hands are included so rummaging
        // visibly parts the grass. These transforms are stable for the whole run (the tail
        // is intentionally left out — it gets rebuilt on the fly and would leave stale refs).
        Transform[] bodyColliders = null;
        float[] bodyRadii = null;
        if (spawnSeaweed)
        {
            var cols = new List<Transform>();
            var rad = new List<float>();
            if (driver != null) { cols.Add(driver); rad.Add(0.40f); }
            if (root != null)
            {
                var neck = root.Find("Neck");
                if (neck != null) { cols.Add(neck); rad.Add(0.22f); }
                var torso = root.Find("Torso");
                if (torso != null) { cols.Add(torso); rad.Add(0.36f); }
            }
            if (hipPoint != null) { cols.Add(hipPoint); rad.Add(0.36f); }
            if (handL != null) { cols.Add(handL.transform); rad.Add(0.17f); }
            if (handR != null) { cols.Add(handR.transform); rad.Add(0.17f); }
            bodyColliders = cols.ToArray();
            bodyRadii = rad.ToArray();

            // Use a hand-placed bed if one exists (editor preview workflow), else spawn one.
            // Either way we only hand over the body colliders + (for a freshly spawned bed)
            // shift it under the spawn point. Tuning is left to the SeaweedField's OWN script
            // defaults — copying values from this Bootstrap would freeze them at whatever was
            // first serialized into the scene component.
            var field = FindAnyObjectByType<SeaweedField>();
            if (field == null)
            {
                var fieldGO = new GameObject("SeaweedField");
                field = fieldGO.AddComponent<SeaweedField>();
                field.patchCenter += spawnPosition;
            }
            field.bodyColliders = bodyColliders;
            field.bodyRadii = bodyRadii;
        }

        // Forager — drives the pause/rummage/collect loop on its own GameObject. As with the
        // seaweed, all tuning (reach, look, cadence) is left to MermaidForager's own current
        // defaults; we only wire the runtime references it can't know on its own.
        if (enableForaging && FindAnyObjectByType<MermaidForager>() == null)
        {
            var forageGO = new GameObject("MermaidForager");
            var forager = forageGO.AddComponent<MermaidForager>();
            forager.swimmer = swimmer;
            forager.handL = handL;
            forager.handR = handR;
            forager.elbowL = elbowL;
            forager.elbowR = elbowR;
            forager.inventory = inventory;
        }
    }

    void EnsureAtmosphere()
    {
        if (!spawnAtmosphere) return;
        if (FindAnyObjectByType<UnderwaterAtmosphere>() != null) return;
        new GameObject("UnderwaterAtmosphere").AddComponent<UnderwaterAtmosphere>();
    }

    void SnapshotShapeValues()
    {
        _lastTailSegments = tailSegments;
        _lastTailLength = tailLength;
        _lastFlukeBonesPerLobe = flukeBonesPerLobe;
        _lastFlukeSpan = flukeSpan;
        _lastFlukeSweepZ = flukeSweepZ;
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
            || !Mathf.Approximately(flukeSweepZ, _lastFlukeSweepZ);
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
        // 1. Live smoothTime updates. Tail / fluke / hair bones each get an extra
        //    multiplier so the user can tune those independently from the global slider.
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
        if (handL != null) handL.maxBendAngleDeg = handMaxBendAngleDeg;
        if (handR != null) handR.maxBendAngleDeg = handMaxBendAngleDeg;
        if (elbowL != null) elbowL.maxBendAngleDeg = elbowMaxBendAngleDeg;
        if (elbowR != null) elbowR.maxBendAngleDeg = elbowMaxBendAngleDeg;

        // Live-editable hair root position. Hair bones stay anchored to headPoint so
        // moving it drags the entire hair with the chain's lag.
        if (headPoint != null) headPoint.localPosition = hairRootOffset;

        // 4. Detect shape changes — rebuild affected groups.
        if (TailFlukeShapeChanged())
        {
            RebuildTailAndFluke();
        }
        if (HairShapeChanged())
        {
            RebuildHair();
        }
        SnapshotShapeValues();

        // 5. Live update tail tube (sides + radii from curve).
        if (tailTube != null && tailTubeRadii != null)
        {
            tailTube.sides = tailTubeSides;
            int n = tailTubeRadii.Length;
            for (int i = 0; i < n; i++)
            {
                float t = (n > 1) ? (float)i / (n - 1) : 0f;
                tailTubeRadii[i] = Mathf.Max(0.001f, tailRadiusCurve.Evaluate(t));
            }
        }

        // 6. Live update fluke tubes.
        for (int s = 0; s < 2; s++)
        {
            var tube = flukeTubes[s];
            var radii = flukeTubeRadii[s];
            if (tube != null && radii != null)
            {
                tube.sides = flukeTubeSides;
                tube.aspectRatio = flukeAspectRatio;
                int n = radii.Length;
                for (int i = 0; i < n; i++)
                {
                    float t = (n > 1) ? (float)i / (n - 1) : 0f;
                    radii[i] = Mathf.Max(0.001f, flukeRadiusCurve.Evaluate(t));
                }
            }
        }

        // 6b. Hair-body collider list: rebuild when the head toggle flips, then refresh radii.
        if (hairAvoidsHead != _lastHairAvoidsHead)
        {
            _lastHairAvoidsHead = hairAvoidsHead;
            RebuildHairColliders();
        }
        UpdateHairColliderRadii();

        // 6c. Hair material — swap to/from the Hair/Curly shader and push live curl params.
        UpdateHairMaterials();

        // 7. Live update hair tubes (radii + per-ring aspect from curves).
        for (int s = 0; s < hairTubes.Count; s++)
        {
            var tube = hairTubes[s];
            var radii = (s < hairTubeRadiiList.Count) ? hairTubeRadiiList[s] : null;
            var aspects = (s < hairTubeAspectsList.Count) ? hairTubeAspectsList[s] : null;
            if (tube != null && radii != null)
            {
                tube.sides = hairTubeSides;
                tube.aspectRatio = 1f;
                int n = radii.Length;
                for (int i = 0; i < n; i++)
                {
                    float t = (n > 1) ? (float)i / (n - 1) : 0f;
                    radii[i] = Mathf.Max(0.001f, hairRadiusCurve.Evaluate(t));
                    if (aspects != null && i < aspects.Length)
                        aspects[i] = Mathf.Max(0.01f, hairAspectCurve.Evaluate(t));
                }
            }
        }
    }

    void BuildMermaid()
    {
        var groupGO = new GameObject("Mermaid");
        groupGO.transform.position = spawnPosition;
        root = groupGO.transform;
        chain = groupGO.AddComponent<MermaidBoneChain>();

        var headGO = new GameObject("Head");
        headGO.transform.SetParent(root, false);
        headGO.transform.localPosition = new Vector3(0f, 0f, 0.85f);
        swimmer = headGO.AddComponent<MermaidSwimmer>();
        swimmer.porpoiseAmplitude = porpoiseAmplitude;
        swimmer.porpoiseFrequency = porpoiseFrequency;
        swimmer.cruiseSpeed = cruiseSpeed;
        var headBone = headGO.transform;
        driver = headBone;
        chain.driver = headBone;

        var neckBone  = MakeBone("Neck",  root, new Vector3(0f, 0f,  0.50f), headBone,  neckSmoothTime,  chain);
        var torsoBone = MakeBone("Torso", root, new Vector3(0f, 0f,  0.05f), neckBone,  torsoSmoothTime, chain);
        var hipBone   = MakeBone("Hip",   root, new Vector3(0f, 0f, -0.55f), torsoBone, hipSmoothTime,   chain);

        var head = MakePrim(PrimitiveType.Sphere, "HeadVisual", headBone,
            Vector3.zero, new Vector3(0.6f, 0.6f, 0.6f), Quaternion.identity, skinColor);

        headPoint = new GameObject("HeadAnchor").transform;
        headPoint.SetParent(head.transform, false);
        headPoint.localPosition = hairRootOffset;

        for (int side = -1; side <= 1; side += 2)
        {
            MakePrim(PrimitiveType.Sphere, side < 0 ? "EyeL" : "EyeR", head.transform,
                new Vector3(side * 0.25f, 0.05f, 0.42f),
                new Vector3(0.18f, 0.18f, 0.18f),
                Quaternion.identity, Color.black);
        }

        MakePrim(PrimitiveType.Sphere, "NeckVisual", neckBone,
            Vector3.zero, new Vector3(0.30f, 0.30f, 0.30f), Quaternion.identity, skinColor);
        MakePrim(PrimitiveType.Sphere, "HipVisual", hipBone,
            Vector3.zero, new Vector3(0.55f, 0.50f, 0.55f), Quaternion.identity, hipColor);
        hipPoint = hipBone;

        MakeLink("UpperSpineLink", root, neckBone, torsoBone, 0.27f, torsoColor);
        MakeLink("LowerSpineLink", root, torsoBone, hipBone, 0.28f, torsoColor);

        for (int side = -1; side <= 1; side += 2)
        {
            string suffix = side < 0 ? "L" : "R";
            float sx = side;

            var shoulder = MakeBone("Shoulder" + suffix, root,
                new Vector3(sx * 0.30f,  0f,     0.40f), neckBone,  shoulderSmoothTime, chain);
            var elbow    = MakeBone("Elbow"    + suffix, root,
                new Vector3(sx * 0.46f, -0.08f, -0.18f), shoulder,  elbowSmoothTime,    chain);
            var hand     = MakeBone("Hand"     + suffix, root,
                new Vector3(sx * 0.34f, -0.32f, -0.62f), elbow,     handSmoothTime,     chain);

            var elbowMB = elbow.GetComponent<MermaidBone>();
            var handMB = hand.GetComponent<MermaidBone>();
            elbowMB.maxBendAngleDeg = elbowMaxBendAngleDeg;
            handMB.bendReferenceAnchor = shoulder;
            handMB.maxBendAngleDeg = handMaxBendAngleDeg;
            if (side < 0) { elbowL = elbowMB; handL = handMB; }
            else          { elbowR = elbowMB; handR = handMB; }
            // Tag elbow + hand so armFlowMultiplier scales them. Shoulder is left out
            // intentionally — it's rigidly attached to the spine ("in its socket").
            armBoneSet.Add(elbowMB);
            armBoneSet.Add(handMB);

            MakePrim(PrimitiveType.Sphere, "ShoulderViz" + suffix, shoulder,
                Vector3.zero, new Vector3(0.18f, 0.18f, 0.18f), Quaternion.identity, jointColor);
            MakePrim(PrimitiveType.Sphere, "ElbowViz" + suffix, elbow,
                Vector3.zero, new Vector3(0.14f, 0.14f, 0.14f), Quaternion.identity, jointColor);
            MakePrim(PrimitiveType.Sphere, "HandViz" + suffix, hand,
                Vector3.zero, new Vector3(0.13f, 0.13f, 0.13f), Quaternion.identity, handColor);

            MakeLink("UpperArmLink" + suffix, root, shoulder, elbow, 0.08f, skinColor);
            MakeLink("LowerArmLink" + suffix, root, elbow, hand, 0.07f, skinColor);
        }

        // Initialize spine + arm bones with their captured rest poses.
        // (Tail and fluke bones use explicit init in MakeBoneAtRest, so they're
        //  added AFTER this point and are not re-initialized.)
        chain.Initialize();

        BuildTail(root, hipBone, chain);
        Transform tailTip = (tailFlukeBones.Count > 0) ? tailFlukeBones[tailFlukeBones.Count - 1].transform : hipBone;
        BuildFluke(root, tailTip, chain);

        BuildHair(root, headPoint, chain);
    }

    void BuildHair(Transform root, Transform scalpAnchor, MermaidBoneChain chain)
    {
        if (scalpAnchor == null) return;

        // Use a deterministic seed so the same hairSeed produces the same look.
        var prevState = UnityEngine.Random.state;
        UnityEngine.Random.InitState(hairSeed);

        Vector3 baseDir = hairBaseDirection.sqrMagnitude > 0.0001f
            ? hairBaseDirection.normalized
            : Vector3.back;

        for (int s = 0; s < hairStrandCount; s++)
        {
            // Per-strand scalp anchor: a small offset from the central scalp point so
            // strands fan out across an area on the back of the head — no more bald
            // patches around the central point.
            Vector2 disc = UnityEngine.Random.insideUnitCircle * hairScalpRadius;
            var strandScalpGO = new GameObject($"ScalpAnchor{s:D2}");
            strandScalpGO.transform.SetParent(scalpAnchor, false);
            strandScalpGO.transform.localPosition = new Vector3(disc.x, disc.y, 0f);
            Transform strandScalp = strandScalpGO.transform;
            hairGameObjects.Add(strandScalpGO);

            // Per-strand variance — direction (yaw spread + small pitch wobble) and length.
            float t = (hairStrandCount > 1) ? (float)s / (hairStrandCount - 1) : 0.5f;
            float yawDeg = Mathf.Lerp(-hairSpreadAngle, hairSpreadAngle, t)
                           + UnityEngine.Random.Range(-4f, 4f);
            float pitchDeg = UnityEngine.Random.Range(-15f, 15f);
            Quaternion strandRot = Quaternion.Euler(pitchDeg, yawDeg, 0f);
            Vector3 strandDir = strandRot * baseDir;

            float lenMul = 1f + UnityEngine.Random.Range(-hairLengthVariance, hairLengthVariance);
            float strandLen = hairStrandLength * Mathf.Max(0.05f, lenMul);
            float segLen = strandLen / Mathf.Max(1, hairBonesPerStrand);

            int N = hairBonesPerStrand + 1;
            Transform[] tubePoints = new Transform[N];
            tubePoints[0] = strandScalp;

            Transform prev = strandScalp;
            Vector3 prevWorldPos = strandScalp.position;

            for (int i = 0; i < hairBonesPerStrand; i++)
            {
                float tBone = (i + 1) / (float)hairBonesPerStrand;
                float smoothTime = Mathf.Lerp(hairBaseSmoothTime, hairTipSmoothTime, tBone);

                Vector3 boneRestPos = prevWorldPos + strandDir * segLen;
                Vector3 localOffsetFromPrev = strandDir * segLen;
                var seg = MakeBoneAtRest(
                    $"Hair{s:D2}_{i:D2}", root, boneRestPos, prev,
                    localOffsetFromPrev, smoothTime, chain);

                tubePoints[i + 1] = seg;
                var mb = seg.GetComponent<MermaidBone>();
                hairBones.Add(mb);
                hairBoneSet.Add(mb);
                hairGameObjects.Add(seg.gameObject);

                prev = seg;
                prevWorldPos = boneRestPos;
            }

            var tube = MakeTube($"HairTube{s:D2}", root, tubePoints, hairColor, hairTubeSides, 1f);
            tube.aspectRatios = new float[N]; // per-ring aspect, populated each frame from hairAspectCurve
            hairTubes.Add(tube);
            hairTubeRadiiList.Add(tube.radii);
            hairTubeAspectsList.Add(tube.aspectRatios);
            hairGameObjects.Add(tube.gameObject);
        }

        UnityEngine.Random.state = prevState;
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
        hairTubes.Clear();
        hairTubeRadiiList.Clear();
        hairTubeAspectsList.Clear();

        for (int i = 0; i < hairGameObjects.Count; i++)
        {
            if (hairGameObjects[i] != null) Destroy(hairGameObjects[i]);
        }
        hairGameObjects.Clear();

        if (headPoint != null)
        {
            BuildHair(root, headPoint, chain);
            RebuildHairColliders();
        }
    }

    void RebuildHairColliders()
    {
        var transforms = new List<Transform>();
        var baseRadii = new List<float>();

        // Upper-body sphere approximations (radii roughly match the visible meshes).
        // Head is opt-in — left out by default so the hair can be anchored inside the
        // head (strands appear to grow out from inside the skull).
        if (hairAvoidsHead && driver != null) { transforms.Add(driver); baseRadii.Add(0.32f); }
        if (root != null) {
            var neck = root.Find("Neck");
            if (neck != null)             { transforms.Add(neck);      baseRadii.Add(0.18f); }
            var torso = root.Find("Torso");
            if (torso != null)            { transforms.Add(torso);     baseRadii.Add(0.30f); }
        }
        if (hipPoint != null)             { transforms.Add(hipPoint);  baseRadii.Add(0.30f); }

        // Tail bones — radii will be sampled live from tailRadiusCurve in Update.
        _hairColliderTailStartIdx = transforms.Count;
        for (int i = 0; i < tailBonesOrdered.Count; i++)
        {
            var b = tailBonesOrdered[i];
            if (b == null) continue;
            transforms.Add(b.transform);
            baseRadii.Add(0.20f); // placeholder, replaced each frame
        }

        _hairColliderTransforms = transforms.ToArray();
        _hairColliderBaseRadii = baseRadii.ToArray();
        _hairColliderRadii = new float[_hairColliderBaseRadii.Length];
        UpdateHairColliderRadii();

        // Push the shared arrays to every hair bone.
        for (int i = 0; i < hairBones.Count; i++)
        {
            if (hairBones[i] == null) continue;
            hairBones[i].avoidanceColliders = _hairColliderTransforms;
            hairBones[i].avoidanceRadii = _hairColliderRadii;
        }
    }

    Shader GetCurlyHairShader()
    {
        if (!_curlyHairShaderResolved)
        {
            _curlyHairShader = Shader.Find("Hair/Curly");
            _curlyHairShaderResolved = true;
        }
        return _curlyHairShader;
    }

    Material GetDefaultLitTemplate()
    {
        if (_defaultLitMatTemplate == null)
        {
            var temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _defaultLitMatTemplate = temp.GetComponent<Renderer>().sharedMaterial;
            Destroy(temp);
        }
        return _defaultLitMatTemplate;
    }

    void UpdateHairMaterials()
    {
        if (hairTubes.Count == 0) return;

        Shader curly = texturedHair ? GetCurlyHairShader() : null;

        for (int i = 0; i < hairTubes.Count; i++)
        {
            var tube = hairTubes[i];
            if (tube == null) continue;
            var mr = tube.GetComponent<MeshRenderer>();
            if (mr == null) continue;
            var mat = mr.sharedMaterial;

            if (texturedHair && curly != null)
            {
                if (mat == null || mat.shader != curly)
                {
                    mat = new Material(curly);
                    mr.sharedMaterial = mat;
                }
                mat.SetColor("_BaseColor", hairColor);
                mat.SetFloat("_CurlFrequency", hairCurlFrequency);
                mat.SetFloat("_CurlDepth", hairCurlDepth);
                mat.SetFloat("_CurlSharpness", hairCurlSharpness);
                mat.SetFloat("_CurlPhaseScramble", hairCurlPhaseScramble);
                mat.SetFloat("_NoiseScale", hairNoiseScale);
                mat.SetFloat("_NoiseStrength", hairNoiseStrength);
                mat.SetColor("_ShadowColor", hairCurlShadowColor);
                mat.SetColor("_RimColor", hairCurlRimColor);
            }
            else
            {
                // texturedHair OFF (or shader missing): make sure we're back on the default URP/Lit.
                bool needsSwap = (mat == null) || (curly != null && mat.shader == curly);
                if (needsSwap)
                {
                    var src = GetDefaultLitTemplate();
                    if (src != null)
                    {
                        mat = new Material(src);
                        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", hairColor);
                        if (mat.HasProperty("_Color")) mat.color = hairColor;
                        mr.sharedMaterial = mat;
                    }
                }
                else if (mat != null)
                {
                    if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", hairColor);
                    if (mat.HasProperty("_Color")) mat.color = hairColor;
                }
            }
        }
    }

    void UpdateHairColliderRadii()
    {
        if (_hairColliderRadii == null || _hairColliderBaseRadii == null) return;
        float buf = Mathf.Max(0.1f, hairColliderRadiusMultiplier);
        int tailCount = tailBonesOrdered.Count;
        for (int i = 0; i < _hairColliderRadii.Length; i++)
        {
            float baseR;
            if (i < _hairColliderTailStartIdx || _hairColliderTailStartIdx < 0)
            {
                baseR = _hairColliderBaseRadii[i];
            }
            else
            {
                int tailIdx = i - _hairColliderTailStartIdx;
                if (tailCount > 0 && tailIdx < tailCount)
                {
                    float t = (tailIdx + 1) / (float)tailCount;
                    baseR = tailRadiusCurve.Evaluate(t);
                }
                else
                {
                    baseR = _hairColliderBaseRadii[i];
                }
            }
            _hairColliderRadii[i] = baseR * buf;
        }
    }

    void BuildTail(Transform root, Transform hipBone, MermaidBoneChain chain)
    {
        Transform prev = hipBone;
        float segLen = tailLength / Mathf.Max(1, tailSegments);
        float startZ = hipBone.localPosition.z;

        int N = tailSegments + 1;
        Transform[] tubePoints = new Transform[N];
        tubePoints[0] = hipBone;

        for (int i = 0; i < tailSegments; i++)
        {
            float tBone = (i + 1) / (float)tailSegments;
            float smoothTime = Mathf.Lerp(tailBaseSmoothTime, tailTipSmoothTime, tBone);

            float z = startZ - segLen * (i + 1);
            Vector3 worldRestPos = root.position + new Vector3(0f, 0f, z);
            Vector3 localOffsetFromPrev = new Vector3(0f, 0f, -segLen);
            var seg = MakeBoneAtRest($"Tail{i:D2}", root, worldRestPos, prev, localOffsetFromPrev, smoothTime, chain);

            tubePoints[i + 1] = seg;
            var mb = seg.GetComponent<MermaidBone>();
            tailFlukeBones.Add(mb);
            tailBoneSet.Add(mb);
            tailBonesOrdered.Add(mb);
            tailGameObjects.Add(seg.gameObject);

            prev = seg;
        }

        tailTube = MakeTube("TailTube", root, tubePoints, tailColor, tailTubeSides, 1f);
        tailTubeRadii = tailTube.radii;
        tailGameObjects.Add(tailTube.gameObject);
    }

    void BuildFluke(Transform root, Transform tailTip, MermaidBoneChain chain)
    {
        float spanDelta = flukeSpan / Mathf.Max(1, flukeBonesPerLobe);
        float sweepDelta = flukeSweepZ / Mathf.Max(1, flukeBonesPerLobe);

        for (int s = 0; s < 2; s++)
        {
            int side = (s == 0) ? -1 : 1;
            string suffix = side < 0 ? "L" : "R";

            int N = flukeBonesPerLobe + 1;
            Transform[] tubePoints = new Transform[N];
            tubePoints[0] = tailTip;
            Transform prev = tailTip;
            Vector3 prevWorldPos = tailTip.position;

            for (int i = 0; i < flukeBonesPerLobe; i++)
            {
                float tBone = (i + 1) / (float)flukeBonesPerLobe;
                float smoothTime = Mathf.Lerp(flukeBaseSmoothTime, flukeTipSmoothTime, tBone);

                Vector3 boneRestPos = prevWorldPos + new Vector3(side * spanDelta, 0f, sweepDelta);
                Vector3 localOffsetFromPrev = new Vector3(side * spanDelta, 0f, sweepDelta);
                var seg = MakeBoneAtRest($"Fluke{suffix}{i:D2}", root, boneRestPos, prev, localOffsetFromPrev, smoothTime, chain);

                tubePoints[i + 1] = seg;
                var mb = seg.GetComponent<MermaidBone>();
                tailFlukeBones.Add(mb);
                flukeBoneSet.Add(mb);
                tailGameObjects.Add(seg.gameObject);

                prev = seg;
                prevWorldPos = boneRestPos;
            }

            var tube = MakeTube($"FlukeTube{suffix}", root, tubePoints, flukeColor, flukeTubeSides, flukeAspectRatio);
            flukeTubes[s] = tube;
            flukeTubeRadii[s] = tube.radii;
            tailGameObjects.Add(tube.gameObject);
        }
    }

    void RebuildTailAndFluke()
    {
        if (root == null || chain == null) return;

        // Detach old tail/fluke bones from the chain registry.
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

        // Destroy old GameObjects (tail bones, tail tube, fluke bones, fluke tubes).
        for (int i = 0; i < tailGameObjects.Count; i++)
        {
            if (tailGameObjects[i] != null) Destroy(tailGameObjects[i]);
        }
        tailGameObjects.Clear();

        tailTube = null;
        tailTubeRadii = null;
        flukeTubes[0] = flukeTubes[1] = null;
        flukeTubeRadii[0] = flukeTubeRadii[1] = null;

        // Rebuild from current values.
        var hipBone = (hipPoint != null) ? hipPoint : root.Find("Hip");
        if (hipBone != null)
        {
            BuildTail(root, hipBone, chain);
            Transform tailTip = (tailFlukeBones.Count > 0) ? tailFlukeBones[tailFlukeBones.Count - 1].transform : hipBone;
            BuildFluke(root, tailTip, chain);
        }

        // Refresh hair colliders since the tail bones are new references now.
        RebuildHairColliders();
    }

    Transform MakeBone(string name, Transform parent, Vector3 localPos, Transform anchor, float baseSmoothTime, MermaidBoneChain chain)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        var bone = go.AddComponent<MermaidBone>();
        bone.anchor = anchor;
        bone.smoothTime = baseSmoothTime * Mathf.Max(0f, globalSmoothMultiplier);
        chain.bones.Add(bone);
        boneEntries.Add(new BoneEntry { bone = bone, baseSmoothTime = baseSmoothTime });
        return go.transform;
    }

    Transform MakeBoneAtRest(string name, Transform parent, Vector3 worldRestPos, Transform anchor, Vector3 localOffsetFromAnchor, float baseSmoothTime, MermaidBoneChain chain)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = worldRestPos;
        var bone = go.AddComponent<MermaidBone>();
        bone.anchor = anchor;
        bone.smoothTime = baseSmoothTime * Mathf.Max(0f, globalSmoothMultiplier);
        bone.InitializeWithExplicitOffset(localOffsetFromAnchor, Quaternion.identity);
        chain.bones.Add(bone);
        boneEntries.Add(new BoneEntry { bone = bone, baseSmoothTime = baseSmoothTime });
        return go.transform;
    }

    void MakeLink(string name, Transform parent, Transform a, Transform b, float radius, Color tint)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name;
        go.transform.SetParent(parent, false);
        var col = go.GetComponent<Collider>();
        if (col != null) Destroy(col);
        var link = go.AddComponent<LinkVisual>();
        link.a = a;
        link.b = b;
        link.radius = radius;
        Tint(go, tint);
    }

    TubeRenderer MakeTube(string name, Transform parent, Transform[] tubePoints, Color tint, int sides, float aspectRatio)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>();
        var mr = go.AddComponent<MeshRenderer>();

        // Snag the URP/Lit (or built-in) default material from a temp primitive.
        var temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
        var srcMat = temp.GetComponent<Renderer>().sharedMaterial;
        var mat = new Material(srcMat);
        Destroy(temp);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", tint);
        if (mat.HasProperty("_Color")) mat.color = tint;
        mr.sharedMaterial = mat;

        var tube = go.AddComponent<TubeRenderer>();
        tube.points = tubePoints;
        tube.radii = new float[tubePoints.Length];
        tube.sides = sides;
        tube.aspectRatio = aspectRatio;
        // Round tubes (tail / hair): parallel transport — no twist between rings, no pinches.
        // Flat tubes (fluke / wings): world-up alignment — keeps the flat axis horizontal so
        // it doesn't twist around its own axis as the lobe waves.
        tube.frameMode = Mathf.Approximately(aspectRatio, 1f)
            ? TubeRenderer.FrameMode.ParallelTransport
            : TubeRenderer.FrameMode.WorldUpAligned;
        tube.capEnds = true;
        return tube;
    }

    void WireCamera()
    {
        Camera cam = Camera.main;
        if (cam == null) { Debug.LogWarning("MermaidBootstrap: no Camera tagged MainCamera found."); return; }
        var follow = cam.GetComponent<CameraFollow>();
        if (follow == null) follow = cam.gameObject.AddComponent<CameraFollow>();
        follow.target = driver;
    }

    [ContextMenu("Reset Tail and Fluke Curves to Defaults")]
    void ResetCurvesToDefaults()
    {
        tailRadiusCurve = new AnimationCurve(
            new Keyframe(0f, 0.42f),
            new Keyframe(1f, 0.10f));
        flukeRadiusCurve = new AnimationCurve(
            new Keyframe(0f, 0.04f),
            new Keyframe(0.35f, 0.22f),
            new Keyframe(1f, 0.04f));
    }

    [ContextMenu("Apply Mermaid Art Colors")]
    void ApplyMermaidArtColors()
    {
        skinColor  = new Color(0.93f, 0.78f, 0.62f);
        torsoColor = new Color(0.78f, 0.55f, 0.20f);
        hipColor   = new Color(0.50f, 0.32f, 0.10f);
        jointColor = new Color(0.93f, 0.78f, 0.62f);
        handColor  = new Color(0.93f, 0.78f, 0.62f);
        tailColor  = new Color(0.45f, 0.28f, 0.10f);
        flukeColor = new Color(0.30f, 0.18f, 0.07f);
    }

    static GameObject MakePrim(PrimitiveType t, string name, Transform parent,
                               Vector3 localPos, Vector3 localScale, Quaternion localRot, Color tint)
    {
        var go = GameObject.CreatePrimitive(t);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = localRot;
        go.transform.localScale = localScale;
        var col = go.GetComponent<Collider>();
        if (col != null) Destroy(col);
        Tint(go, tint);
        return go;
    }

    static void Tint(GameObject go, Color c)
    {
        var rend = go.GetComponent<Renderer>();
        if (rend == null) return;
        var mat = new Material(rend.sharedMaterial);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
        if (mat.HasProperty("_Color")) mat.color = c;
        rend.material = mat;
    }
}
