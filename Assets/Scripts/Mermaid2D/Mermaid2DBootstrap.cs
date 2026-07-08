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
///
/// EDIT-MODE PREVIEW: in the editor (before Play) this builds the whole world — mermaid at
/// rest pose, seaweed, atmosphere — under a temporary "__Mermaid2DPreview" root that is
/// never saved into the scene. Edit any field on this component and the preview rebuilds.
/// The preview is a mirror of the runtime build, so tune the look here (colors, curves,
/// custom art slots) and Play will match.
/// </summary>
[ExecuteAlways]
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
    [Tooltip("How much her face counter-rotates against the swim pitch so her eyes keep pointing where she's swimming. 0 = face rides the bob fully (old look), 1 = gaze locked level. The forage look-down still comes through — she always turns her face to her rummaging hands.")]
    [Range(0f, 1f)]
    public float gazeStabilization = 0.85f;

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

    [Header("Neck (elegant articulated neck; length/links rebuild the preview)")]
    [Tooltip("Distance from the chest to the head — her neck length. The edit-mode preview rebuilds on change; for play mode set it before pressing Play.")]
    public float neckLength = 0.55f;
    [Tooltip("How many lagged links articulate the neck. More = softer, more serpentine undulation; 0 = the old rigid connection.")]
    [Range(0, 6)]
    public int neckLinkCount = 3;
    [Tooltip("Bend limit per neck link, in degrees. Lower = a poised, swan-like neck that rights itself behind the head; higher = loose and noodly.")]
    [Range(0f, 180f)]
    public float neckMaxBendDeg = 28f;
    [Tooltip("Neck half-width along its length. X = 0 under the skull, 1 where it meets the chest. Edits apply live.")]
    public AnimationCurve neckWidthCurve = new AnimationCurve(
        new Keyframe(0f, 0.105f, 0f, -0.10f),
        new Keyframe(0.40f, 0.075f, 0f, 0f),
        new Keyframe(1f, 0.165f, 0.30f, 0.30f));

    [Header("Body Structure (live-editable)")]
    [Tooltip("Bend limit between spine links (head→neck→torso→hip), in degrees. This is her 'core strength': lower = the body rights itself quickly behind the head instead of trailing like a ribbon. 0 = no limit (pure lag).")]
    [Range(0f, 180f)]
    public float spineMaxBendDeg = 25f;
    [Tooltip("Bend limit per tail link, in degrees. Lower = a shallower, more controlled tail wave with fewer stacked S-curves. 0 = no limit.")]
    [Range(0f, 180f)]
    public float tailMaxBendDeg = 18f;
    [Tooltip("Bend limit per fluke link. Higher = loose fabric feel. 0 = no limit.")]
    [Range(0f, 180f)]
    public float flukeMaxBendDeg = 50f;

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

    // ---------------------------------------------------------------- custom art
    // Everything below is optional. Leave a slot empty to keep the procedural look.
    // RULE OF THUMB: parts that DEFORM (torso, tail, flukes, arms, hair, seaweed) take a
    // MATERIAL — their ribbon meshes have UVs (U across, V along root→tip), so a material
    // with your texture flows with the animation. Parts that are RIGID (head, hands, gems,
    // rocks, sparkles, motes, backdrop, ground) take a SPRITE, auto-fitted to the same size
    // as the procedural shape they replace. When a material override is set, the ribbon's
    // vertex tint is forced to white so your art shows untinted.
    // Ribbon UV mapping (all these parts): image X runs along the part with image LEFT =
    // far tip and image RIGHT = attachment (hip/shoulder/scalp/chest); image Y = width.
    [Header("Custom Art — deforming parts (materials)")]
    [Tooltip("Torso ribbon material. Usually you want the simpler torsoTexture slot below instead.")]
    public Material torsoMaterial;
    [Tooltip("Tail ribbon material (e.g. a custom scales shader). For a plain painted texture use tailTexture below.")]
    public Material tailMaterial;
    [Tooltip("Fluke lobe material. For a plain painted texture use flukeTexture below.")]
    public Material flukeMaterial;
    [Tooltip("Arm ribbon material. Used by both arms; the far arm is still darkened for depth.")]
    public Material armMaterial;
    [Tooltip("Hair lock material. Per-lock color variation is disabled when set.")]
    public Material hairMaterial;
    [Tooltip("Seaweed blade material for BOTH layers. Green vertex tint is disabled when set; root darkening is kept.")]
    public Material seaweedMaterial;

    [Header("Custom Art — rigid parts (sprites)")]
    [Tooltip("Head sprite (side view, facing right). Replaces the head disc AND the procedural face (eye/brow/lips) — draw those into your sprite.")]
    public Sprite headSprite;
    [Tooltip("Hand sprite. Used for both hands; the far hand is automatically darkened.")]
    public Sprite handSprite;
    [Tooltip("Gem sprite (untinted).")]
    public Sprite gemSprite;
    [Tooltip("Rock sprite (untinted).")]
    public Sprite rockSprite;
    [Tooltip("Sparkle sprite (tinted by the spawner's sparkleColor).")]
    public Sprite sparkleSprite;
    [Tooltip("Background image (replaces the deep→horizon gradient).")]
    public Sprite backdropSprite;
    [Tooltip("Ground/sand image (replaces the seabed strip + sand line; its top edge sits at the seabed height).")]
    public Sprite seabedSprite;
    [Tooltip("Drifting plankton mote sprite (tinted by moteColor).")]
    public Sprite moteSprite;
    [Tooltip("God-ray material override (e.g. additive). Needs a _Color tint property for the shimmer.")]
    public Material godRayMaterial;

    // Paintable slots: drag a plain PNG (default import settings) straight in — no material
    // or Sprite setup needed. PAINT THE PART AS IT APPEARS ON SCREEN (she faces right):
    // image LEFT = the far end (tail tip / fluke tip / hand / hair tip), image RIGHT = where
    // it attaches (hip / shoulder / scalp / chest), image height = the part's width. The
    // texture bends and flows with the ribbon animation. If both a material and a texture
    // are set for a part, the material wins.
    [Header("Custom Art — paintable textures (drag a PNG straight in)")]
    [Tooltip("Painted torso. Image right = chest/head end, image left = hip.")]
    public Texture2D torsoTexture;
    [Tooltip("Painted tail. Image right = hip, image left = tail tip.")]
    public Texture2D tailTexture;
    [Tooltip("Painted fluke lobe (used for both lobes). Image right = tail tip, image left = fin tip.")]
    public Texture2D flukeTexture;
    [Tooltip("Painted arm. Image right = shoulder, image left = wrist. Far arm is auto-darkened.")]
    public Texture2D armTexture;
    [Tooltip("Painted hair lock (used for every lock). Image right = scalp, image left = hair tip.")]
    public Texture2D hairTexture;
    [Tooltip("Painted seaweed blade (used for every blade). Image bottom = root, image top = blade tip.")]
    public Texture2D seaweedTexture;
    [Tooltip("Painted head with face, side view facing right. Same as headSprite but accepts a plain texture.")]
    public Texture2D headTexture;
    [Tooltip("Painted hand.")]
    public Texture2D handTexture;

    [Header("Seaweed Motion (live-editable)")]
    [Tooltip("How far each blade sways side to side.")]
    public float seaweedSwayAmplitude = 0.35f;
    [Tooltip("How fast the blades sway, in wave cycles per second.")]
    public float seaweedSwayFrequency = 0.8f;
    [Tooltip("How many wave humps travel up a blade at once. Higher = more serpentine.")]
    public float seaweedWaveCount = 1.1f;
    [Tooltip("Small, faster secondary ripple layered on top of the main sway.")]
    public float seaweedFlutterAmplitude = 0.10f;
    [Tooltip("Segments per blade — more = smoother, rounder bends (rebuilds the beds on change).")]
    [Range(2, 24)]
    public int seaweedSegments = 12;
    [Tooltip("How fast the grass scrolls LEFT past her while she swims, as a fraction of her cruise speed. 0 = static bed, 1 = matches her speed exactly.")]
    [Range(0f, 2f)]
    public float seaweedScrollScale = 0.5f;

    [Header("Editor Preview")]
    [Tooltip("Animate the edit-mode preview with the exact same swim + bone-lag simulation as play mode (she undulates in the Scene view without pressing Play). Costs some editor CPU while the scene is open.")]
    public bool animatePreview = true;

    [Header("Extras")]
    [Tooltip("Gold crown, necklace and armbands.")]
    public bool goldJewelry = true;
    [Tooltip("Soft billowing hair mass behind the head that the locks flow out of.")]
    public bool hairVolumeBlob = true;
    [Tooltip("Spawn the two-layer flowing seaweed bed that parts around her body and hands.")]
    public bool spawnSeaweed = true;
    [Tooltip("Make her periodically pause and rummage in the grass for gems/rocks.")]
    public bool enableForaging = true;
    [Tooltip("Spawn the 2D underwater atmosphere (gradient water, god rays, motes, seabed).")]
    public bool spawnAtmosphere = true;
    [Tooltip("Golden sparkles drifting from her trailing hand.")]
    public bool spawnSparkles = true;

    [Header("Corner Widget Mode")]
    [Tooltip("Render the whole game inside a round viewport anchored to a screen corner (for a corner-of-screen widget game). The area outside the circle is filled with widgetSurroundColor — set its alpha to 0 for transparent-desktop-overlay builds.")]
    public bool cornerWidget = false;
    public CornerWidget2D.Corner widgetCorner = CornerWidget2D.Corner.BottomRight;
    [Tooltip("Circle diameter as a fraction of the smaller screen dimension.")]
    [Range(0.15f, 1f)]
    public float widgetScreenFraction = 0.55f;
    [Tooltip("What fills the screen outside the circle. Alpha 0 = ready for a transparent overlay window build.")]
    public Color widgetSurroundColor = new Color(0f, 0f, 0f, 0f);
    [Tooltip("Resolution of the square texture the game is rendered into. Smaller = big GPU savings for a corner widget (512–768 looks crisp at typical widget sizes).")]
    public int widgetRenderSize = 768;

    [Header("Performance")]
    [Tooltip("0 = uncapped (vsync). For a corner-widget/desktop-pet build, 30 keeps CPU+GPU usage tiny while the motion still reads smoothly.")]
    public int capFrameRate = 0;

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
    readonly List<Mermaid2DBone> spineBones = new List<Mermaid2DBone>();
    readonly List<Mermaid2DBone> neckLinkBones = new List<Mermaid2DBone>();

    // Tail/fluke runtime state — tracked separately so we can rebuild on the fly.
    readonly List<GameObject> tailGameObjects = new List<GameObject>();
    readonly List<Mermaid2DBone> tailFlukeBones = new List<Mermaid2DBone>();
    readonly List<Mermaid2DBone> tailBonesOrdered = new List<Mermaid2DBone>();
    readonly HashSet<Mermaid2DBone> tailBoneSet = new HashSet<Mermaid2DBone>();
    readonly HashSet<Mermaid2DBone> flukeBoneSet = new HashSet<Mermaid2DBone>();
    readonly HashSet<Mermaid2DBone> armBoneSet = new HashSet<Mermaid2DBone>();

    // Seaweed runtime state (both depth layers) — kept so the motion fields tune live.
    readonly List<Seaweed2D> seaweedLayers = new List<Seaweed2D>();

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
        if (!Application.isPlaying)
        {
#if UNITY_EDITOR
            SchedulePreviewRebuild();
#endif
            return;
        }
        EnsureRuntimeBuild();
    }

    bool _runtimeBuilt;

    // Also called from Update: with Enter Play Mode Options (no scene reload) Awake is
    // never re-called on play, which would otherwise leave only the un-tuned edit preview
    // in the game.
    void EnsureRuntimeBuild()
    {
        if (_runtimeBuilt && root != null) return;
        _runtimeBuilt = true;
        DestroyPreview();      // a DontSave preview can survive into play mode
        ClearBuildState();
        ApplyFrameCap();
        BuildMermaid();
        WireCamera();
        EnsureForagingAndSeaweed();
        EnsureAtmosphere();
        EnsureSparkles();
        EnsureCornerWidget();
        SnapshotShapeValues();
        _lastHairAvoidsHead = hairAvoidsHead;
    }

    // ---------------------------------------------------------------- edit-mode preview

    const string PreviewRootName = "__Mermaid2DPreview (not saved)";

    void ClearBuildState()
    {
        spineBones.Clear();
        neckLinkBones.Clear();
        boneEntries.Clear();
        tailGameObjects.Clear();
        tailFlukeBones.Clear();
        tailBonesOrdered.Clear();
        tailBoneSet.Clear();
        flukeBoneSet.Clear();
        armBoneSet.Clear();
        seaweedLayers.Clear();
        hairGameObjects.Clear();
        hairBones.Clear();
        hairBoneSet.Clear();
        hairRibbons.Clear();
        _hairColliderTransforms = null;
        _hairColliderRadii = null;
        _hairColliderBaseRadii = null;
        _hairColliderTailStartIdx = -1;
        root = null; driver = null; headScalp = null; hipPoint = null;
        swimmer = null; chain = null;
        elbowNear = elbowFar = handNear = handFar = null;
    }

    void DestroyPreview()
    {
        // Marker-based sweep — catches the preview even if it was renamed or duplicated.
        var markers = FindObjectsByType<Mermaid2DPreviewMarker>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var m in markers)
        {
            if (m == null) continue;
            if (Application.isPlaying) { m.gameObject.SetActive(false); Destroy(m.gameObject); }
            else DestroyImmediate(m.gameObject);
        }
        // Name-based fallback.
        for (var existing = GameObject.Find(PreviewRootName); existing != null;
             existing = GameObject.Find(PreviewRootName))
        {
            if (Application.isPlaying) { existing.SetActive(false); Destroy(existing); break; }
            DestroyImmediate(existing);
        }

        // Stale SAVED copies: the whole mermaid is procedural, so any bone-chain hierarchy
        // found here (our own preview/runtime chains were already handled above) is junk an
        // older preview accidentally saved into the scene file — it would swim alongside
        // the real mermaid with stale tuning. Destroy it and dirty the scene so a save
        // persists the cleanup.
        var staleChains = FindObjectsByType<Mermaid2DBoneChain>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var c in staleChains)
        {
            if (c == null || (chain != null && c == chain)) continue;
            var go = c.gameObject;
            Debug.LogWarning($"Mermaid2DBootstrap: removed stale saved mermaid '{go.name}' — save the scene to make the cleanup permanent.", this);
            if (Application.isPlaying) { go.SetActive(false); Destroy(go); }
            else
            {
#if UNITY_EDITOR
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(go.scene);
#endif
                DestroyImmediate(go);
            }
        }
    }

#if UNITY_EDITOR
    bool _previewQueued;
    double _lastEditorTickTime;

    void OnEnable()
    {
        if (!Application.isPlaying)
        {
            SchedulePreviewRebuild();
            _lastEditorTickTime = UnityEditor.EditorApplication.timeSinceStartup;
            UnityEditor.EditorApplication.update -= EditorPreviewTick;
            UnityEditor.EditorApplication.update += EditorPreviewTick;
        }
    }

    void OnDisable()
    {
        UnityEditor.EditorApplication.update -= EditorPreviewTick;
    }

    // Edit-mode animation: run the EXACT same simulation as play mode — live tuning, the
    // swimmer's porpoise bob, then the lagged bone chain — driven off the editor clock.
    // The queued player-loop update then runs the [ExecuteAlways] visuals (Ribbon2D,
    // Seaweed2D, GentleBillow2D, Underwater2DAtmosphere) and repaints, so the preview
    // mermaid swims in the Scene view without entering play mode.
    void EditorPreviewTick()
    {
        double now = UnityEditor.EditorApplication.timeSinceStartup;
        float dt = Mathf.Clamp((float)(now - _lastEditorTickTime), 0f, 0.05f);
        _lastEditorTickTime = now;

        if (Application.isPlaying || UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (!animatePreview || dt <= 0f) return;
        if (root == null || swimmer == null || chain == null) return;

        ApplyLiveTick();
        swimmer.Step(dt);
        chain.TickAll(dt);

        UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
        UnityEditor.SceneView.RepaintAll();
    }

    void OnValidate()
    {
        if (!Application.isPlaying) SchedulePreviewRebuild();
    }

    void SchedulePreviewRebuild()
    {
        if (_previewQueued) return;
        _previewQueued = true;
        UnityEditor.EditorApplication.delayCall += () =>
        {
            _previewQueued = false;
            if (this == null) return;
            // Never (re)build a preview while entering play mode — that's how you end up
            // with a second, un-tuned mermaid in the game.
            if (Application.isPlaying || UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode) return;
            RebuildPreview();
        };
    }

    [ContextMenu("Rebuild Edit-Mode Preview")]
    public void RebuildPreview()
    {
        DestroyPreview();
        BuildPreview();
    }

    void BuildPreview()
    {
        ClearBuildState();
        var prev = new GameObject(PreviewRootName);
        prev.hideFlags = HideFlags.DontSave;
        prev.AddComponent<Mermaid2DPreviewMarker>();

        // The mermaid at her rest pose (chains don't simulate in edit mode). Lifted by the
        // forager's cruise height so she previews where she actually swims.
        BuildMermaid();
        if (root != null)
        {
            root.SetParent(prev.transform, true);
            root.position += Vector3.up * 1.2f;
        }

        if (spawnSeaweed)
        {
            var back = MakeSeaweedLayer("SeaweedBack", 170, -1.02f, 0.72f, -50, 1234, null, null);
            var front = MakeSeaweedLayer("SeaweedFront", 110, -1.18f, 1.08f, 20, 4321, null, null);
            back.transform.SetParent(prev.transform, true);
            front.transform.SetParent(prev.transform, true);
            back.GetComponent<Seaweed2D>().Build();
            front.GetComponent<Seaweed2D>().Build();
        }

        if (spawnAtmosphere)
        {
            var atmoGO = new GameObject("PreviewAtmosphere");
            atmoGO.transform.SetParent(prev.transform, false);
            var atmo = atmoGO.AddComponent<Underwater2DAtmosphere>();
            atmo.backdropSprite = backdropSprite;
            atmo.seabedSprite = seabedSprite;
            atmo.moteSprite = moteSprite;
            atmo.godRayMaterial = godRayMaterial;
            atmo.Build();
        }
    }
#endif

    int _lastAppliedFrameCap = -1;

    void ApplyFrameCap()
    {
        if (capFrameRate == _lastAppliedFrameCap) return;
        _lastAppliedFrameCap = capFrameRate;
        if (capFrameRate > 0)
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = capFrameRate;
        }
        else
        {
            QualitySettings.vSyncCount = 1;
            Application.targetFrameRate = -1;
        }
    }

    void EnsureCornerWidget()
    {
        if (!cornerWidget) return;
        if (FindAnyObjectByType<CornerWidget2D>() != null) return;
        var go = new GameObject("CornerWidget2D");
        var w = go.AddComponent<CornerWidget2D>();
        w.corner = widgetCorner;
        w.screenFraction = widgetScreenFraction;
        w.surroundColor = widgetSurroundColor;
        w.renderTextureSize = Mathf.Clamp(widgetRenderSize, 128, 2048);
        w.ringColor = goldColor;
    }

    // ---------------------------------------------------------------- build: mermaid

    void BuildMermaid()
    {
        var groupGO = new GameObject("Mermaid2D");
        groupGO.transform.position = (Vector3)spawnPosition;
        root = groupGO.transform;
        chain = groupGO.AddComponent<Mermaid2DBoneChain>();

        // Head = the driver. She faces +X, held away from the chest by her neck length.
        const float chestX = 0.50f;
        float headX = chestX + Mathf.Max(0.1f, neckLength);
        var headGO = new GameObject("Head");
        headGO.transform.SetParent(root, false);
        headGO.transform.localPosition = new Vector3(headX, 0f, 0f);
        swimmer = headGO.AddComponent<Mermaid2DSwimmer>();
        swimmer.porpoiseAmplitude = porpoiseAmplitude;
        swimmer.porpoiseFrequency = porpoiseFrequency;
        swimmer.cruiseSpeed = cruiseSpeed;
        driver = headGO.transform;
        chain.driver = driver;

        // Articulated neck: a short chain of lagged links between the skull and the chest,
        // so the neck undulates with the same traveling-wave lag as the rest of her. The
        // head's bob enters at the skull and rolls down the links into the body.
        var neckPoints = new List<Transform> { driver };
        Transform neckAnchor = driver;
        int links = Mathf.Clamp(neckLinkCount, 0, 6);
        for (int i = 1; i <= links; i++)
        {
            float t = i / (float)(links + 1);
            // Quick right under the skull, easing toward the chest's lag.
            float st = Mathf.Lerp(0.06f, neckSmoothTime, t);
            var link = MakeBone($"NeckLink{i:D2}", new Vector2(Mathf.Lerp(headX, chestX, t), 0f), neckAnchor, st);
            var linkMB = link.GetComponent<Mermaid2DBone>();
            linkMB.maxBendAngleDeg = neckMaxBendDeg;
            neckLinkBones.Add(linkMB);
            neckPoints.Add(link);
            neckAnchor = link;
        }

        // "Neck" is the neck BASE / chest bone (necklace, arms and hair colliders hang here).
        var neckBone = MakeBone("Neck", new Vector2(chestX, 0f), neckAnchor, neckSmoothTime);
        neckPoints.Add(neckBone);
        var torsoBone = MakeBone("Torso", new Vector2(0.05f, 0f), neckBone, torsoSmoothTime);
        var hipBone = MakeBone("Hip", new Vector2(-0.55f, 0f), torsoBone, hipSmoothTime);
        hipPoint = hipBone;

        // Core strength: the spine may lag, but only bends so far before it rights itself.
        foreach (var t in new[] { neckBone, torsoBone, hipBone })
        {
            var mb = t.GetComponent<Mermaid2DBone>();
            mb.maxBendAngleDeg = spineMaxBendDeg;
            spineBones.Add(mb);
        }

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

            // With custom arm art the tint is white (near) / gray (far) so the texture
            // shows as drawn but the far arm still recedes.
            Material armArt = RibbonArt(armMaterial, armTexture);
            Color armCol;
            if (armArt != null) armCol = near ? Color.white : new Color(0.68f, 0.68f, 0.68f, 1f);
            else armCol = near ? skinColor : skinColor * 0.68f;
            armCol.a = 1f;
            var armRibbon = MakeRibbon("ArmRibbon" + sfx, new[] { shoulder, elbow, hand },
                armWidthCurve, 1f, 14, armCol, armCol, near ? OrderNearArm : OrderFarArm, armArt);
            armRibbon.roundCaps = true;

            // Hands are ALWAYS SpriteRenderers too (paintable the same way as the head).
            var handArt = SpriteArt(handSprite, handTexture);
            bool customHand = handArt != null;
            if (!customHand) handArt = CircleSprite();
            Color handCol;
            if (customHand) handCol = near ? Color.white : new Color(0.68f, 0.68f, 0.68f, 1f);
            else { handCol = near ? skinColor : skinColor * 0.68f; handCol.a = 1f; }
            MakeSpriteFit("HandSprite" + sfx, hand, Vector2.zero, handArt, 0.17f, handCol,
                near ? OrderNearHand : OrderFarHand);

            if (goldJewelry && near)
                MakeQuad("Armband", elbow, new Vector2(0.10f, 0.02f), new Vector2(0.15f, 0.05f), 25f, goldColor, OrderNearArm + 1);
        }

        // Spine + arm bones capture their rest poses from the built layout.
        chain.Initialize();

        // Neck ribbon: slim skin-colored band lofted through the skull, the neck links and
        // the chest. Always procedural skin (a torso texture would map wrongly onto it).
        Color neckCol = skinColor; neckCol.a = 1f;
        var neckRibbon = MakeRibbon("NeckRibbon", neckPoints.ToArray(), neckWidthCurve, 1f,
            20, neckCol, neckCol, OrderTorso, null);
        neckRibbon.roundCaps = true;

        // Torso ribbon: chest → torso → hip, feminine profile, skin blending to gold at the
        // hip where the tail begins. The neck used to be the first 1/3 of this ribbon's
        // point-span (head→neck→torso→hip), so the body keeps its exact tuned silhouette by
        // sampling the saved width curve over its old [1/3, 1] section.
        Material torsoArt = RibbonArt(torsoMaterial, torsoTexture);
        Color torsoStart = torsoArt != null ? Color.white : skinColor;
        Color torsoEnd = torsoArt != null ? Color.white : Color.Lerp(hipColor, goldColor, 0.45f);
        torsoStart.a = 1f;
        torsoEnd.a = 1f;
        var torsoRibbon = MakeRibbon("TorsoRibbon", new[] { neckBone, torsoBone, hipBone },
            RemapCurveSection(torsoWidthCurve, 1f / 3f, 1f), 1f, 26, torsoStart, torsoEnd, OrderTorso, torsoArt);
        torsoRibbon.roundCaps = true;   // the chest cap doubles as her shoulder silhouette

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
            mb.maxBendAngleDeg = tailMaxBendDeg;
            tailFlukeBones.Add(mb);
            tailBoneSet.Add(mb);
            tailBonesOrdered.Add(mb);
            tailGameObjects.Add(seg.gameObject);
            prev = seg;
        }

        Material tailArt = RibbonArt(tailMaterial, tailTexture);
        var ribbon = MakeRibbon("TailRibbon", tubePoints, tailWidthCurve, 1f, 52,
            tailArt != null ? Color.white : goldColor,
            tailArt != null ? Color.white : goldDeepColor,
            OrderTail, tailArt);
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
                mb.maxBendAngleDeg = flukeMaxBendDeg;
                tailFlukeBones.Add(mb);
                flukeBoneSet.Add(mb);
                tailGameObjects.Add(seg.gameObject);
                prev = seg;
                prevWorldPos = boneRestPos;
            }

            Color flukeTipCol = Color.Lerp(goldDeepColor, hairShadowColor, 0.3f); flukeTipCol.a = 1f;
            Material flukeArt = RibbonArt(flukeMaterial, flukeTexture);
            var ribbon = MakeRibbon($"FlukeRibbon{suffix}", tubePoints, flukeWidthCurve, 1f, 34,
                flukeArt != null ? Color.white : goldDeepColor,
                flukeArt != null ? Color.white : flukeTipCol,
                OrderFluke, flukeArt);
            tailGameObjects.Add(ribbon.gameObject);
        }
    }

    void BuildHead(Transform headBone)
    {
        // Soft hair mass at the back/top of the head that the long strands flow out of.
        if (hairVolumeBlob)
        {
            var blob = MakeDisc("HairVolume", headBone, new Vector2(-0.16f, 0.13f), 0.34f, hairColor, OrderHairBlob);
            blob.transform.localScale = new Vector3(0.40f, 0.32f, 1f);   // squashed bean
            blob.AddComponent<GentleBillow2D>();
        }

        // Scalp anchor the hair bones hang from (live-editable via hairRootOffset).
        var scalpGO = new GameObject("ScalpAnchor");
        scalpGO.transform.SetParent(headBone, false);
        scalpGO.transform.localPosition = (Vector3)hairRootOffset;
        headScalp = scalpGO.transform;

        // Face group: holds the visual face (head sprite, features, crown). The swimmer
        // counter-rotates it against the swim pitch so her gaze stays pointed where she's
        // swimming, while the head BONE keeps the full pitch that whips the body wave.
        // Hair (scalp + volume blob) deliberately stays on the bone and rides the bob.
        var faceGO = new GameObject("FaceGroup");
        faceGO.transform.SetParent(headBone, false);
        Transform faceGroup = faceGO.transform;
        if (swimmer != null)
        {
            swimmer.faceGroup = faceGroup;
            swimmer.gazeStabilization = gazeStabilization;
        }

        // The head is ALWAYS a SpriteRenderer, so you can paint one in Photoshop and just
        // swap it (drag a PNG into headTexture on this component, or a Sprite into
        // headSprite — or even directly onto the HeadSprite object's SpriteRenderer).
        // Without custom art it shows a generated skin-colored circle + procedural face.
        var headArt = SpriteArt(headSprite, headTexture);
        bool customHead = headArt != null;
        if (!customHead) headArt = CircleSprite();
        MakeSpriteFit("HeadSprite", faceGroup, Vector2.zero, headArt, customHead ? 0.62f : 0.60f,
            customHead ? Color.white : skinColor, OrderHead);

        if (!customHead)
        {
            // Face: tilted almond eye + brow + a hint of lips (side view shows one of each).
            // Skipped entirely when you provide painted head art (paint the face in).
            var eyeCol = new Color(0.05f, 0.025f, 0.02f);
            var eye = MakeDisc("Eye", faceGroup, new Vector2(0.16f, 0.04f), 1f, eyeCol, OrderFace);
            eye.transform.localScale = new Vector3(0.058f, 0.028f, 1f);
            eye.transform.localRotation = Quaternion.Euler(0f, 0f, 8f);

            MakeQuad("Brow", faceGroup, new Vector2(0.165f, 0.135f), new Vector2(0.13f, 0.024f), 10f,
                new Color(0.12f, 0.05f, 0.04f), OrderFace);

            var lips = MakeDisc("Lips", faceGroup, new Vector2(0.275f, -0.075f), 1f,
                new Color(0.62f, 0.22f, 0.16f), OrderFace);
            lips.transform.localScale = new Vector3(0.035f, 0.02f, 1f);
        }

        if (goldJewelry)
        {
            BuildCrown(faceGroup);
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

    Material _hairArt;   // shared by all locks (explicit material or wrapped painted texture)

    void BuildHair()
    {
        if (headScalp == null) return;
        _hairArt = RibbonArt(hairMaterial, hairTexture);

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

            Color start, end;
            if (_hairArt != null)
            {
                start = Color.white;
                end = Color.white;
            }
            else
            {
                start = Color.Lerp(hairColor, Color.Lerp(hairColor, goldColor, 0.25f), Random.value * 0.5f);
                end = Color.Lerp(hairColor, hairShadowColor, 0.55f);
            }
            start.a = 1f; end.a = 1f;

            var ribbon = MakeRibbon($"HairRibbon{s:D2}", tubePoints, hairWidthCurve, hairWidthScale,
                44, start, end, order, _hairArt);
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
        for (int i = 0; i < neckLinkBones.Count; i++)
        {
            if (neckLinkBones[i] == null) continue;
            transforms.Add(neckLinkBones[i].transform);
            radii.Add(0.10f);
        }
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
        if (enableForaging && FindAnyObjectByType<GemGameManager>() == null)
        {
            // Legacy counter panel — only when the meta-game isn't in the scene.
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
            forager.gemSprite = gemSprite;
            forager.rockSprite = rockSprite;
        }
    }

    GameObject MakeSeaweedLayer(string name, int count, float rootY, float brightness, int order,
        int seed, Transform[] circles, float[] radii)
    {
        var go = new GameObject(name);
        // Blade vertices are computed in world coordinates (patchCenterX included), so the
        // mesh object itself must sit at the origin.
        go.transform.position = Vector3.zero;
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
        field.swayAmplitude = seaweedSwayAmplitude;
        field.swayFrequency = seaweedSwayFrequency;
        field.waveCount = seaweedWaveCount;
        field.flutterAmplitude = seaweedFlutterAmplitude;
        field.segments = Mathf.Clamp(seaweedSegments, 2, 24);
        field.scrollScale = seaweedScrollScale;
        seaweedLayers.Add(field);
        Material seaweedArt = RibbonArt(seaweedMaterial, seaweedTexture);
        field.useVertexTint = seaweedArt == null;

        var mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterial = seaweedArt != null ? seaweedArt : SpriteMat(Color.white);
        mr.sortingOrder = order;
        return go;
    }

    void EnsureAtmosphere()
    {
        if (!spawnAtmosphere) return;
        if (FindAnyObjectByType<Underwater2DAtmosphere>() != null) return;
        var atmo = new GameObject("Underwater2DAtmosphere").AddComponent<Underwater2DAtmosphere>();
        atmo.backdropSprite = backdropSprite;
        atmo.seabedSprite = seabedSprite;
        atmo.moteSprite = moteSprite;
        atmo.godRayMaterial = godRayMaterial;
    }

    void EnsureSparkles()
    {
        if (!spawnSparkles) return;
        if (FindAnyObjectByType<Sparkle2DSpawner>() != null) return;
        var go = new GameObject("Sparkles2D");
        var spawner = go.AddComponent<Sparkle2DSpawner>();
        spawner.handTransform = (handNear != null) ? handNear.transform : null;
        spawner.swimmer = swimmer;
        spawner.sparkleSprite = sparkleSprite;
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
        // Edit mode: the preview rebuilds via OnValidate and animates via EditorPreviewTick.
        if (!Application.isPlaying) return;

        // Covers Enter Play Mode Options (no scene reload), where Awake never re-runs.
        EnsureRuntimeBuild();

        ApplyLiveTick();

        // Shape changes — rebuild affected groups. (Edit mode instead rebuilds the whole
        // preview via OnValidate, so this only runs in play.)
        if (TailFlukeShapeChanged()) RebuildTailAndFluke();
        if (HairShapeChanged()) RebuildHair();
        SnapshotShapeValues();

        // Live frame cap changes.
        ApplyFrameCap();
    }

    // Everything live-editable that must hit the CURRENT build every frame. Shared verbatim
    // between play mode (Update above) and the edit-mode preview animation (EditorPreviewTick),
    // so the preview's motion tuning — flow multipliers, bend limits — matches play exactly.
    void ApplyLiveTick()
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
            swimmer.gazeStabilization = gazeStabilization;
        }

        // 3. Live joint constraints.
        if (handNear != null) handNear.maxBendAngleDeg = handMaxBendAngleDeg;
        if (handFar != null) handFar.maxBendAngleDeg = handMaxBendAngleDeg;
        if (elbowNear != null) elbowNear.maxBendAngleDeg = elbowMaxBendAngleDeg;
        if (elbowFar != null) elbowFar.maxBendAngleDeg = elbowMaxBendAngleDeg;

        // 3b. Live body structure (neck poise + spine core strength + tail/fluke wave limits).
        for (int i = 0; i < neckLinkBones.Count; i++)
            if (neckLinkBones[i] != null) neckLinkBones[i].maxBendAngleDeg = neckMaxBendDeg;
        for (int i = 0; i < spineBones.Count; i++)
            if (spineBones[i] != null) spineBones[i].maxBendAngleDeg = spineMaxBendDeg;
        for (int i = 0; i < tailBonesOrdered.Count; i++)
            if (tailBonesOrdered[i] != null) tailBonesOrdered[i].maxBendAngleDeg = tailMaxBendDeg;
        foreach (var fb in flukeBoneSet)
            if (fb != null) fb.maxBendAngleDeg = flukeMaxBendDeg;

        // 4. Live-editable hair root position + hair widths.
        if (headScalp != null) headScalp.localPosition = (Vector3)hairRootOffset;
        for (int i = 0; i < hairRibbons.Count; i++)
            if (hairRibbons[i] != null) hairRibbons[i].widthScale = hairWidthScale;

        // 5. Hair-body collider refresh.
        if (hairAvoidsHead != _lastHairAvoidsHead)
        {
            _lastHairAvoidsHead = hairAvoidsHead;
            RebuildHairColliders();
        }
        UpdateHairColliderRadii();

        // 6. Live seaweed motion (segments change rebuilds that bed's mesh).
        int swSeg = Mathf.Clamp(seaweedSegments, 2, 24);
        for (int i = 0; i < seaweedLayers.Count; i++)
        {
            var sw = seaweedLayers[i];
            if (sw == null) continue;
            sw.swayAmplitude = seaweedSwayAmplitude;
            sw.swayFrequency = seaweedSwayFrequency;
            sw.waveCount = seaweedWaveCount;
            sw.flutterAmplitude = seaweedFlutterAmplitude;
            sw.scrollScale = seaweedScrollScale;
            if (sw.segments != swSeg) { sw.segments = swSeg; sw.Build(); }
        }
    }

    // ---------------------------------------------------------------- cosmetics API

    /// <summary>
    /// Re-color hair and tail at runtime (used by the meta-game's cosmetics). Hair is
    /// rebuilt with the new color; tail/fluke ribbons and the hair blob re-tint in place.
    /// </summary>
    public void ApplyCosmeticPalette(Color newHair, Color newHairShadow, Color newGold, Color newGoldDeep)
    {
        hairColor = newHair;
        hairShadowColor = newHairShadow;
        goldColor = newGold;
        goldDeepColor = newGoldDeep;

        if (Application.isPlaying && root != null)
        {
            RebuildHair();

            var tail = root.Find("TailRibbon");
            if (tail != null && tailMaterial == null && tailTexture == null)
            {
                var rib = tail.GetComponent<Ribbon2D>();
                if (rib != null) { rib.colorStart = goldColor; rib.colorEnd = goldDeepColor; }
            }
            foreach (var lobeName in new[] { "FlukeRibbonUp", "FlukeRibbonDown" })
            {
                var lobe = root.Find(lobeName);
                if (lobe != null && flukeMaterial == null && flukeTexture == null)
                {
                    var rib = lobe.GetComponent<Ribbon2D>();
                    if (rib != null)
                    {
                        rib.colorStart = goldDeepColor;
                        Color tip = Color.Lerp(goldDeepColor, hairShadowColor, 0.3f); tip.a = 1f;
                        rib.colorEnd = tip;
                    }
                }
            }
            var blob = FindDeep(root, "HairVolume");
            if (blob != null)
            {
                var mr = blob.GetComponent<MeshRenderer>();
                if (mr != null && mr.material != null) mr.material.color = hairColor;
            }
            var torso = root.Find("TorsoRibbon");
            if (torso != null && torsoMaterial == null && torsoTexture == null)
            {
                var rib = torso.GetComponent<Ribbon2D>();
                if (rib != null)
                {
                    Color torsoEnd = Color.Lerp(hipColor, goldColor, 0.45f); torsoEnd.a = 1f;
                    rib.colorEnd = torsoEnd;
                }
            }
        }
    }

    static Transform FindDeep(Transform t, string name)
    {
        if (t.name == name) return t;
        for (int i = 0; i < t.childCount; i++)
        {
            var r = FindDeep(t.GetChild(i), name);
            if (r != null) return r;
        }
        return null;
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

    // When overrideMat is set the caller decides the vertex tints (usually white so the
    // artist's texture shows untinted).
    Ribbon2D MakeRibbon(string name, Transform[] points, AnimationCurve widthCurve, float widthScale,
        int samples, Color colorStart, Color colorEnd, int sortingOrder, Material overrideMat = null)
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
        mr.sharedMaterial = overrideMat != null ? overrideMat : SpriteMat(Color.white);
        mr.sortingOrder = sortingOrder;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        return ribbon;
    }

    // SpriteRenderer for a rigid part, uniformly scaled so the sprite's height matches
    // targetHeight in world units.
    GameObject MakeSpriteFit(string name, Transform parent, Vector2 localPos, Sprite sprite,
        float targetHeight, Color tint, int sortingOrder)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = (Vector3)localPos;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = tint;
        sr.sortingOrder = sortingOrder;
        float s = targetHeight / Mathf.Max(0.0001f, sprite.bounds.size.y);
        go.transform.localScale = new Vector3(s, s, 1f);
        return go;
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

    // A new curve equal to src over [from, to], renormalized to [0, 1]. Densely sampled with
    // smoothed tangents so the reshaped section keeps the original silhouette.
    static AnimationCurve RemapCurveSection(AnimationCurve src, float from, float to, int samples = 20)
    {
        var keys = new Keyframe[samples];
        for (int i = 0; i < samples; i++)
        {
            float t = i / (samples - 1f);
            keys[i] = new Keyframe(t, src.Evaluate(Mathf.Lerp(from, to, t)));
        }
        var c = new AnimationCurve(keys);
        for (int i = 0; i < samples; i++) c.SmoothTangents(i, 0f);
        return c;
    }

    // Effective ribbon override: explicit material wins, else wrap a painted texture.
    static Material RibbonArt(Material mat, Texture2D tex)
    {
        if (mat != null) return mat;
        if (tex != null) return new Material(Shader.Find("Sprites/Default")) { mainTexture = tex, color = Color.white };
        return null;
    }

    // Effective rigid-part sprite: explicit sprite wins, else wrap a painted texture.
    static Sprite SpriteArt(Sprite sprite, Texture2D tex)
    {
        if (sprite != null) return sprite;
        if (tex == null) return null;
        return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
    }

    // Generated anti-aliased white circle sprite — the placeholder for the head/hands
    // until you paint your own (tinted via SpriteRenderer.color).
    static Sprite _circleSprite;
    static Sprite CircleSprite()
    {
        if (_circleSprite != null) return _circleSprite;
        const int S = 64;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
        };
        var px = new Color32[S * S];
        float c = (S - 1) * 0.5f;
        float rMax = c - 1f;
        for (int y = 0; y < S; y++)
        {
            for (int x = 0; x < S; x++)
            {
                float dx = x - c, dy = y - c;
                float a = Mathf.Clamp01((rMax - Mathf.Sqrt(dx * dx + dy * dy)) / 1.5f);
                px[y * S + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        _circleSprite = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f);
        return _circleSprite;
    }
}
