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

    [Header("Spawn")]
    public Vector3 spawnPosition = Vector3.zero;

    [Header("Swim Motion (live-editable)")]
    [Tooltip("Vertical bob amplitude in metres. Higher = bigger up/down motion.")]
    public float porpoiseAmplitude = 0.35f;
    [Tooltip("Cycles per second. Higher = faster bob.")]
    public float porpoiseFrequency = 0.9f;
    [Tooltip("Virtual forward swim speed. Used by the swimmer to compute the porpoise pitch — higher cruiseSpeed = flatter swim path; lower = more vertical pitch oscillation.")]
    public float cruiseSpeed = 3f;

    [Header("Bone Lag (higher = more dramatic ragdoll wave) — live-editable")]
    [Tooltip("Single overall floppy-ness knob. Multiplies every per-bone smoothTime below. 1.0 = the tuned defaults. Bump to 1.5–2 for very loose; drop to 0.5 for stiffer.")]
    public float globalSmoothMultiplier = 1f;

    [Tooltip("Lag of the neck behind the head, in seconds.")]
    public float neckSmoothTime = 0.15f;
    public float torsoSmoothTime = 0.30f;
    public float hipSmoothTime = 0.50f;
    [Tooltip("0 = shoulders are rigidly glued to the upper torso (recommended).")]
    public float shoulderSmoothTime = 0f;
    public float elbowSmoothTime = 0.40f;
    public float handSmoothTime = 0.70f;

    [Header("Joint Constraints (live-editable)")]
    [Tooltip("Asymmetric bend cone for the lower arm at the elbow. The hand is clamped to within this many degrees of its rest direction in the BACKWARD and SIDEWAYS directions. The NATURAL FOLD direction (forward) is always unrestricted. 30 = fairly stiff in the backward direction; 60 = looser; 180 = no constraint.")]
    [Range(0f, 180f)]
    public float handMaxBendAngleDeg = 30f;
    [Tooltip("Symmetric bend cone for the elbow relative to the shoulder, in degrees from rest direction. 180 = no constraint.")]
    [Range(0f, 180f)]
    public float elbowMaxBendAngleDeg = 180f;

    [Header("Tail")]
    [Range(3, 16)]
    public int tailSegments = 8;
    [Tooltip("Total length of the tail from hip to fluke base, in metres.")]
    public float tailLength = 1.6f;
    [Tooltip("Tube radius at the hip end of the tail.")]
    public float tailBaseRadius = 0.42f;
    [Tooltip("Tube radius at the fluke end of the tail. Linearly interpolated.")]
    public float tailTipRadius = 0.10f;
    [Tooltip("Number of sides around the tail's tube cross-section. Higher = smoother silhouette.")]
    [Range(6, 32)]
    public int tailTubeSides = 16;
    [Tooltip("smoothTime of the first tail segment (small lag).")]
    public float tailBaseSmoothTime = 0.18f;
    [Tooltip("smoothTime of the last tail segment (most lag — whip-like tip).")]
    public float tailTipSmoothTime = 0.55f;

    [Header("Fluke")]
    [Tooltip("Total left-right span of the fluke (the horizontal fluke at the tail tip).")]
    public float flukeSpan = 0.85f;
    [Tooltip("Front-to-back depth of each fluke lobe.")]
    public float flukeChord = 0.45f;
    [Tooltip("Vertical thickness of the fluke (it's a flat horizontal fin).")]
    public float flukeThickness = 0.06f;
    [Tooltip("Outward sweep angle of each fluke lobe in degrees (gives the V-shape).")]
    [Range(0f, 45f)]
    public float flukeSweepDeg = 12f;

    [Header("Anchors (populated at runtime)")]
    public Transform root;
    public Transform driver;
    public Transform headPoint;
    public Transform hipPoint;

    // Runtime registry of bones with their *base* smoothTime — used to re-multiply
    // by globalSmoothMultiplier each frame so the multiplier is live-editable.
    class BoneEntry { public MermaidBone bone; public float baseSmoothTime; }
    readonly List<BoneEntry> boneEntries = new List<BoneEntry>();

    MermaidSwimmer swimmer;
    MermaidBone elbowL, elbowR, handL, handR;

    void Awake()
    {
        BuildMermaid();
        WireCamera();
    }

    void Update()
    {
        // Push live-editable values onto the runtime components each frame.
        float m = Mathf.Max(0f, globalSmoothMultiplier);
        for (int i = 0; i < boneEntries.Count; i++)
        {
            var e = boneEntries[i];
            if (e.bone != null) e.bone.smoothTime = e.baseSmoothTime * m;
        }

        if (swimmer != null)
        {
            swimmer.porpoiseAmplitude = porpoiseAmplitude;
            swimmer.porpoiseFrequency = porpoiseFrequency;
            swimmer.cruiseSpeed = cruiseSpeed;
        }

        if (handL != null) handL.maxBendAngleDeg = handMaxBendAngleDeg;
        if (handR != null) handR.maxBendAngleDeg = handMaxBendAngleDeg;
        if (elbowL != null) elbowL.maxBendAngleDeg = elbowMaxBendAngleDeg;
        if (elbowR != null) elbowR.maxBendAngleDeg = elbowMaxBendAngleDeg;
    }

    void BuildMermaid()
    {
        var groupGO = new GameObject("Mermaid");
        groupGO.transform.position = spawnPosition;
        root = groupGO.transform;
        var chain = groupGO.AddComponent<MermaidBoneChain>();

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
                new Vector3(sx * 0.42f, -0.05f, -0.05f), shoulder,  elbowSmoothTime,    chain);
            var hand     = MakeBone("Hand"     + suffix, root,
                new Vector3(sx * 0.32f, -0.25f, -0.40f), elbow,     handSmoothTime,     chain);

            var elbowMB = elbow.GetComponent<MermaidBone>();
            var handMB = hand.GetComponent<MermaidBone>();
            elbowMB.maxBendAngleDeg = elbowMaxBendAngleDeg;
            // Asymmetric cone on the hand: tight in backward/sideways directions, but
            // freely opens up in the natural fold direction. Shoulder is the reference
            // so the cone knows which way "forward fold" points.
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

        BuildTail(root, hipBone, chain);

        chain.Initialize();
    }

    void BuildTail(Transform root, Transform hipBone, MermaidBoneChain chain)
    {
        Transform prev = hipBone;
        float segLen = tailLength / Mathf.Max(1, tailSegments);
        float startZ = hipBone.localPosition.z;

        // Single continuous tube mesh covering hip + every tail bone — no more
        // visible joins between cylinders, just one smooth tapered tail.
        int N = tailSegments + 1;
        Transform[] tubePoints = new Transform[N];
        float[] tubeRadii = new float[N];
        tubePoints[0] = hipBone;
        tubeRadii[0] = tailBaseRadius;

        for (int i = 0; i < tailSegments; i++)
        {
            float tBone = (i + 1) / (float)tailSegments;
            float smoothTime = Mathf.Lerp(tailBaseSmoothTime, tailTipSmoothTime, tBone);

            float z = startZ - segLen * (i + 1);
            var seg = MakeBone($"Tail{i:D2}", root, new Vector3(0f, 0f, z), prev, smoothTime, chain);

            tubePoints[i + 1] = seg;
            tubeRadii[i + 1] = Mathf.Lerp(tailBaseRadius, tailTipRadius, tBone);

            prev = seg;
        }

        MakeTube("TailTube", root, tubePoints, tubeRadii, tailColor, tailTubeSides);

        BuildFluke(prev);
    }

    void MakeTube(string name, Transform parent, Transform[] tubePoints, float[] tubeRadii, Color tint, int sides)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>();
        var mr = go.AddComponent<MeshRenderer>();

        // Snag the URP/Lit (or built-in) default material from a temp primitive so the
        // tube renders the same as the other body parts.
        var temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
        var srcMat = temp.GetComponent<Renderer>().sharedMaterial;
        var mat = new Material(srcMat);
        Destroy(temp);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", tint);
        if (mat.HasProperty("_Color")) mat.color = tint;
        mr.sharedMaterial = mat;

        var tube = go.AddComponent<TubeRenderer>();
        tube.points = tubePoints;
        tube.radii = tubeRadii;
        tube.sides = sides;
        tube.capEnds = true;
    }

    void BuildFluke(Transform tailTip)
    {
        // Two flat horizontal lobes attached to the tail's last bone.
        // They inherit the tip's rotation, so they slap up/down with the tail wave.
        float lobeWidth = flukeSpan * 0.45f;
        float lobeCenterX = flukeSpan * 0.30f;
        float lobeOffsetZ = -flukeChord * 0.5f;
        for (int side = -1; side <= 1; side += 2)
        {
            string suffix = side < 0 ? "L" : "R";
            Vector3 lobePos = new Vector3(side * lobeCenterX, 0f, lobeOffsetZ);
            Vector3 lobeScale = new Vector3(lobeWidth, flukeThickness, flukeChord);
            // Sweep each lobe outward-back so the fluke forms a shallow V.
            Quaternion lobeRot = Quaternion.Euler(0f, side * flukeSweepDeg, 0f);
            MakePrim(PrimitiveType.Cube, $"Fluke{suffix}", tailTip, lobePos, lobeScale, lobeRot, flukeColor);
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

    void WireCamera()
    {
        Camera cam = Camera.main;
        if (cam == null) { Debug.LogWarning("MermaidBootstrap: no Camera tagged MainCamera found."); return; }
        var follow = cam.GetComponent<CameraFollow>();
        if (follow == null) follow = cam.gameObject.AddComponent<CameraFollow>();
        follow.target = driver;
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
