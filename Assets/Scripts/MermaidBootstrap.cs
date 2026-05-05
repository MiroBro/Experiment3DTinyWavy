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
    [Range(2, 12)]
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

    [Header("Hair (live-editable; rebuilds on strands/bones/length/spread/seed change)")]
    [Range(1, 40)]
    public int hairStrandCount = 14;
    [Range(3, 16)]
    public int hairBonesPerStrand = 8;
    public float hairStrandLength = 1.6f;
    [Tooltip("Hair strand radius along its length. X = 0 at scalp, 1 at tip.")]
    public AnimationCurve hairRadiusCurve = new AnimationCurve(
        new Keyframe(0f, 0.020f),
        new Keyframe(1f, 0.008f));
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

    int _lastTailSegments = -1;
    float _lastTailLength = float.NaN;
    int _lastFlukeBonesPerLobe = -1;
    float _lastFlukeSpan = float.NaN;
    float _lastFlukeSweepZ = float.NaN;
    int _lastHairStrandCount = -1;
    int _lastHairBonesPerStrand = -1;
    float _lastHairStrandLength = float.NaN;
    float _lastHairSpreadAngle = float.NaN;
    int _lastHairSeed = int.MinValue;

    void Awake()
    {
        BuildMermaid();
        WireCamera();
        SnapshotShapeValues();
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
            || hairSeed != _lastHairSeed;
    }

    void Update()
    {
        // 1. Live smoothTime updates. Tail / fluke / hair bones each get an extra
        //    multiplier so the user can tune those independently from the global slider.
        float gm = Mathf.Max(0f, globalSmoothMultiplier);
        float tm = Mathf.Max(0.01f, tailFlowMultiplier);
        float fm = Mathf.Max(0.01f, flukeFlowMultiplier);
        float hm = Mathf.Max(0.01f, hairFlowMultiplier);
        for (int i = 0; i < boneEntries.Count; i++)
        {
            var e = boneEntries[i];
            if (e.bone == null) continue;
            float mult = gm;
            if (tailBoneSet.Contains(e.bone)) mult *= tm;
            else if (flukeBoneSet.Contains(e.bone)) mult *= fm;
            else if (hairBoneSet.Contains(e.bone)) mult *= hm;
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

        // 7. Live update hair tubes.
        for (int s = 0; s < hairTubes.Count; s++)
        {
            var tube = hairTubes[s];
            var radii = (s < hairTubeRadiiList.Count) ? hairTubeRadiiList[s] : null;
            if (tube != null && radii != null)
            {
                tube.sides = hairTubeSides;
                tube.aspectRatio = 1f;
                int n = radii.Length;
                for (int i = 0; i < n; i++)
                {
                    float t = (n > 1) ? (float)i / (n - 1) : 0f;
                    radii[i] = Mathf.Max(0.001f, hairRadiusCurve.Evaluate(t));
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
        headPoint.localPosition = new Vector3(0f, 0.2f, -0.25f);

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

        float segLen = hairStrandLength / Mathf.Max(1, hairBonesPerStrand);

        for (int s = 0; s < hairStrandCount; s++)
        {
            // Pick a deterministic-pseudo-random direction in the back hemisphere:
            // yaw spread sideways across the back, with a downward pitch bias so hair
            // drapes over the shoulders rather than sticking out horizontally.
            float t = (hairStrandCount > 1) ? (float)s / (hairStrandCount - 1) : 0.5f;
            float yawDeg = Mathf.Lerp(-hairSpreadAngle, hairSpreadAngle, t)
                           + UnityEngine.Random.Range(-4f, 4f);
            float pitchDeg = UnityEngine.Random.Range(-25f, 5f); // mostly downward
            Quaternion strandRot = Quaternion.Euler(pitchDeg, yawDeg, 0f);
            Vector3 strandDir = strandRot * Vector3.back;        // base direction = -Z, then tilted

            // First control point of the tube is the scalp anchor itself.
            int N = hairBonesPerStrand + 1;
            Transform[] tubePoints = new Transform[N];
            tubePoints[0] = scalpAnchor;

            Transform prev = scalpAnchor;
            Vector3 prevWorldPos = scalpAnchor.position;

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
            hairTubes.Add(tube);
            hairTubeRadiiList.Add(tube.radii);
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

        for (int i = 0; i < hairGameObjects.Count; i++)
        {
            if (hairGameObjects[i] != null) Destroy(hairGameObjects[i]);
        }
        hairGameObjects.Clear();

        if (headPoint != null)
        {
            BuildHair(root, headPoint, chain);
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
