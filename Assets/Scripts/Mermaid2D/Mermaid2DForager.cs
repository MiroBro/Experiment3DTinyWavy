using UnityEngine;

/// <summary>
/// 2D port of <see cref="MermaidForager"/>: drives the mermaid's foraging loop. She cruises
/// for a random spell (lifted up over the grass), then pauses — slowing to a faint
/// undulation — descends, stretches her hands down into the seaweed and rummages, finds a
/// gem or rock that pops into her inventory, and resumes swimming.
///
/// The pause is sold by <see cref="Mermaid2DSwimmer.motionScale"/> (slow forward flow +
/// faint bob) and by dipping the hand/elbow bones via their
/// <see cref="Mermaid2DBone.reachOffsetWorld"/>. Wired up at runtime by Mermaid2DBootstrap.
/// </summary>
public class Mermaid2DForager : MonoBehaviour
{
    [Header("Refs (auto-populated by Mermaid2DBootstrap)")]
    public Mermaid2DSwimmer swimmer;
    public Mermaid2DBone handNear, handFar;
    public Mermaid2DBone elbowNear, elbowFar;
    public GemInventory inventory;

    [Header("Cadence")]
    [Tooltip("Random seconds spent cruising between rummages.")]
    public float minCruise = 1f;
    public float maxCruise = 5f;
    [Tooltip("Seconds to ease into / out of the reach.")]
    public float reachInTime = 0.6f;
    public float reachOutTime = 0.7f;
    [Tooltip("Seconds spent rummaging (hands sifting through the grass).")]
    public float rummageTime = 2.8f;

    [Header("Reach")]
    [Tooltip("How far her hands extend DOWN toward the seabed while rummaging.")]
    public float reachDown = 1.1f;
    [Tooltip("Forward reach so the dig happens UNDER HER FACE rather than under her belly.")]
    public float reachForward = 0.4f;
    [Tooltip("Elbows follow the reach at this fraction of the hands, for a natural arm line.")]
    [Range(0f, 1f)]
    public float elbowReachFraction = 0.6f;
    [Tooltip("Degrees she tips her head down to look at her hands while rummaging.")]
    public float lookDownDeg = 34f;
    [Tooltip("Extra downward lean at the dig (on top of dropping from cruiseLift).")]
    public float bodyDip = 0.1f;
    [Tooltip("How high above her base she cruises when NOT rummaging — keeps her swimming well up over the grass. She dives down from here to dig at the roots.")]
    public float cruiseLift = 1.2f;
    [Tooltip("How far her whole body leans FORWARD over the dig spot.")]
    public float bodyLean = 0.15f;
    [Tooltip("How much swim motion she keeps while rummaging. ~0.55 keeps a clear body undulation going while she digs so she doesn't look stiff.")]
    [Range(0f, 1f)]
    public float rummageMotionScale = 0.55f;

    [Header("Rummage Wiggle")]
    [Tooltip("How far her hands stir/dig around the spot.")]
    public float wiggleAmplitude = 0.14f;
    public float wiggleFrequency = 6.5f;

    [Header("Loot")]
    [Tooltip("Chance each find is a gem (vs a rock).")]
    [Range(0f, 1f)]
    public float gemChance = 0.55f;
    public Color gemColor = new Color(0.5f, 0.85f, 1f);
    public Color rockColor = new Color(0.6f, 0.58f, 0.52f);

    enum Phase { Cruise, ReachIn, Rummage, ReachOut }
    Phase phase = Phase.Cruise;
    float phaseT;          // seconds elapsed in the current phase
    float cruiseTarget;    // randomized cruise duration

    void Start()
    {
        cruiseTarget = Random.Range(minCruise, maxCruise);
    }

    void Update()
    {
        float dt = Time.deltaTime;
        phaseT += dt;

        // reachEnv: 0 = cruising, 1 = fully reached down. Drives both the slowdown and the dip.
        float reachEnv = 0f;
        switch (phase)
        {
            case Phase.Cruise:
                if (phaseT >= cruiseTarget) Enter(Phase.ReachIn);
                reachEnv = 0f;
                break;

            case Phase.ReachIn:
                reachEnv = Mathf.Clamp01(phaseT / Mathf.Max(0.01f, reachInTime));
                if (phaseT >= reachInTime) Enter(Phase.Rummage);
                break;

            case Phase.Rummage:
                reachEnv = 1f;
                if (phaseT >= rummageTime) { Collect(); Enter(Phase.ReachOut); }
                break;

            case Phase.ReachOut:
                reachEnv = 1f - Mathf.Clamp01(phaseT / Mathf.Max(0.01f, reachOutTime));
                if (phaseT >= reachOutTime)
                {
                    cruiseTarget = Random.Range(minCruise, maxCruise);
                    Enter(Phase.Cruise);
                }
                break;
        }

        ApplyMotion(reachEnv);
        ApplyReach(reachEnv);
    }

    void Enter(Phase p) { phase = p; phaseT = 0f; }

    void ApplyMotion(float reachEnv)
    {
        if (swimmer == null) return;
        float eased = Mathf.SmoothStep(0f, 1f, reachEnv);
        swimmer.motionScale = Mathf.Lerp(1f, rummageMotionScale, eased);
        swimmer.lookDownDeg = lookDownDeg * eased;
    }

    void ApplyReach(float reachEnv)
    {
        // She faces screen-right; the reach points down-and-under regardless of how her
        // body is pitching with the porpoise.
        Vector2 fwd = Vector2.right;
        Vector2 down = Vector2.down;

        float eased = Mathf.SmoothStep(0f, 1f, reachEnv);
        Vector2 dip = (down * reachDown + fwd * reachForward) * eased;

        // Body height: cruise lifted up over the grass, then descend + lean in over the dig
        // spot. Lerp between the two poses so she sinks to the seabed to rummage and rises
        // to swim.
        if (swimmer != null)
        {
            Vector2 cruisePose = Vector2.up * cruiseLift;
            Vector2 rummagePose = down * bodyDip + fwd * bodyLean;
            swimmer.forageBodyOffsetWorld = Vector2.Lerp(cruisePose, rummagePose, eased);
        }

        // Rummage motion: a stirring sweep along the seabed + a digging in/out bob + a slow
        // erratic drift, so it looks like she's sifting through the grass rather than
        // waving. Opposite phase per hand.
        Vector2 wNear = Vector2.zero, wFar = Vector2.zero;
        if (phase == Phase.Rummage)
        {
            float t = Time.time * wiggleFrequency;
            wNear = fwd * (Mathf.Sin(t) * wiggleAmplitude)
                  + down * (Mathf.Sin(t * 1.7f) * wiggleAmplitude * 0.7f)
                  + fwd * (Mathf.Sin(t * 0.43f) * wiggleAmplitude * 0.5f);
            wFar = fwd * (Mathf.Sin(t + 2.1f) * wiggleAmplitude)
                 + down * (Mathf.Cos(t * 1.6f) * wiggleAmplitude * 0.7f)
                 + fwd * (Mathf.Cos(t * 0.37f) * wiggleAmplitude * 0.5f);
        }

        if (handNear != null) handNear.reachOffsetWorld = dip + wNear;
        if (handFar != null) handFar.reachOffsetWorld = dip + wFar;
        Vector2 elbowDip = dip * elbowReachFraction;
        if (elbowNear != null) elbowNear.reachOffsetWorld = elbowDip;
        if (elbowFar != null) elbowFar.reachOffsetWorld = elbowDip;
    }

    void Collect()
    {
        // Find spot: just below the midpoint of her hands, i.e. down in the grass.
        Vector3 spot;
        Transform handForFollow = null;
        if (handNear != null && handFar != null)
        {
            spot = (handNear.transform.position + handFar.transform.position) * 0.5f;
            handForFollow = handNear.transform;
        }
        else if (handNear != null) { spot = handNear.transform.position; handForFollow = handNear.transform; }
        else if (handFar != null) { spot = handFar.transform.position; handForFollow = handFar.transform; }
        else spot = transform.position;
        spot += Vector3.down * 0.12f;
        spot.z = 0f;

        bool isGem = Random.value < gemChance;
        Color c = isGem ? gemColor : rockColor;
        CollectedItem2D.Spawn(spot, handForFollow, isGem, c);

        if (inventory != null)
        {
            if (isGem) inventory.AddGem();
            else inventory.AddRock();
        }
    }
}
