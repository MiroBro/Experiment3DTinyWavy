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
    [Tooltip("Maximum angle (degrees) the hand can deviate from its rest direction relative to the elbow. Lower = stiffer; prevents the lower arm from bending backward past natural. 60–80° looks natural; 180 = no limit.")]
    [Range(0f, 180f)]
    public float handMaxBendAngleDeg = 70f;
    [Tooltip("Same for the elbow relative to the shoulder. Set high (~120) to allow free upper-arm swing.")]
    [Range(0f, 180f)]
    public float elbowMaxBendAngleDeg = 120f;

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

        chain.Initialize();
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
