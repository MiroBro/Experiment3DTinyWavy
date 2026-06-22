using UnityEngine;

/// <summary>
/// Drives the mermaid's foraging loop: she cruises for a random spell, then pauses (slowing
/// to a faint undulation), stretches her hands down into the seaweed and rummages, finds a
/// gem or rock that pops into her inventory, and resumes swimming.
///
/// Her body barely moves — the pause is sold by <see cref="MermaidSwimmer.motionScale"/>
/// (slow forward flow + faint bob) and by dipping the hand/elbow bones via their
/// <see cref="MermaidBone.reachOffsetWorld"/>. Wired up at runtime by MermaidBootstrap.
/// </summary>
public class MermaidForager : MonoBehaviour
{
    [Header("Refs (auto-populated by MermaidBootstrap)")]
    public MermaidSwimmer swimmer;
    public MermaidBone handL, handR;
    public MermaidBone elbowL, elbowR;
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
    [Tooltip("How far her hands extend DOWN toward the seabed while rummaging. Sized so her arms reach the seaweed roots on the floor (seabed ~1.05 below her); push higher only if the seabed is deeper.")]
    public float reachDown = 1.1f;
    [Tooltip("Forward reach so the dig happens UNDER HER FACE rather than under her belly. Kept small relative to reachDown so her arms point DOWN to the roots, not forward up toward her face.")]
    public float reachForward = 0.4f;
    [Tooltip("Elbows follow the reach at this fraction of the hands, for a natural arm line.")]
    [Range(0f, 1f)]
    public float elbowReachFraction = 0.6f;
    [Tooltip("Degrees she tips her head down to look at her hands while rummaging.")]
    public float lookDownDeg = 34f;
    [Tooltip("How far her whole body leans DOWN toward the seabed as she digs in (helps her hands reach the floor).")]
    public float bodyDip = 0.22f;
    [Tooltip("How high above her base she cruises when NOT rummaging — keeps her swimming up over the grass (about one body thickness). She descends from here to dig.")]
    public float cruiseLift = 0.6f;
    [Tooltip("How far her whole body leans FORWARD over the dig spot.")]
    public float bodyLean = 0.15f;
    [Tooltip("How much swim motion she keeps while rummaging. 0 = dead stop/flat; ~0.55 keeps a clear body undulation (arc + bob) going while she digs so she doesn't look stiff.")]
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
        swimmer.suppressTwirl = reachEnv > 0.01f;
        swimmer.lookDownDeg = lookDownDeg * eased;
    }

    void ApplyReach(float reachEnv)
    {
        // Build a stable horizontal frame from her facing so the reach points DOWN-and-under
        // regardless of how her body is pitching with the porpoise.
        Vector3 fwd = Vector3.forward;
        if (swimmer != null)
        {
            Vector3 f = swimmer.transform.forward; f.y = 0f;
            if (f.sqrMagnitude > 1e-4f) fwd = f.normalized;
        }
        Vector3 right = Vector3.Cross(Vector3.up, fwd);

        float eased = Mathf.SmoothStep(0f, 1f, reachEnv);
        Vector3 dip = (Vector3.down * reachDown + fwd * reachForward) * eased;

        // Body height: cruise lifted up over the grass, then descend + lean in over the dig
        // spot. Lerp between the two poses so she sinks to the seabed to rummage and rises to
        // swim. The rummage pose is unchanged from before, so her reach to the roots still lands.
        if (swimmer != null)
        {
            Vector3 cruisePose = Vector3.up * cruiseLift;
            Vector3 rummagePose = Vector3.down * bodyDip + fwd * bodyLean;
            swimmer.forageBodyOffsetWorld = Vector3.Lerp(cruisePose, rummagePose, eased);
        }

        // Rummage motion: a stirring circle in the seabed plane + a digging in/out bob along
        // the reach + a slow erratic drift, so it looks like she's sifting through the grass
        // rather than waving. Opposite phase per hand.
        Vector3 wL = Vector3.zero, wR = Vector3.zero;
        if (phase == Phase.Rummage)
        {
            float t = Time.time * wiggleFrequency;
            wL = (right * Mathf.Sin(t) + fwd * Mathf.Cos(t * 1.3f)) * wiggleAmplitude
               + Vector3.down * (Mathf.Sin(t * 1.7f) * wiggleAmplitude * 0.7f)
               + right * (Mathf.Sin(t * 0.43f) * wiggleAmplitude * 0.5f);
            wR = (right * Mathf.Sin(t + 2.1f) + fwd * Mathf.Cos(t * 1.3f + 1f)) * wiggleAmplitude
               + Vector3.down * (Mathf.Cos(t * 1.6f) * wiggleAmplitude * 0.7f)
               + fwd * (Mathf.Cos(t * 0.37f) * wiggleAmplitude * 0.5f);
        }

        if (handL != null) handL.reachOffsetWorld = dip + wL;
        if (handR != null) handR.reachOffsetWorld = dip + wR;
        Vector3 elbowDip = dip * elbowReachFraction;
        if (elbowL != null) elbowL.reachOffsetWorld = elbowDip;
        if (elbowR != null) elbowR.reachOffsetWorld = elbowDip;
    }

    void Collect()
    {
        // Find spot: just below the midpoint of her hands, i.e. down in the grass.
        Vector3 spot;
        Transform handForFollow = null;
        if (handL != null && handR != null)
        {
            spot = (handL.transform.position + handR.transform.position) * 0.5f;
            handForFollow = handR.transform;
        }
        else if (handR != null) { spot = handR.transform.position; handForFollow = handR.transform; }
        else if (handL != null) { spot = handL.transform.position; handForFollow = handL.transform; }
        else spot = transform.position;
        spot += Vector3.down * 0.12f;

        bool isGem = Random.value < gemChance;
        Color c = isGem ? gemColor : rockColor;
        CollectedItem.Spawn(spot, handForFollow, isGem, c);

        if (inventory != null)
        {
            if (isGem) inventory.AddGem();
            else inventory.AddRock();
        }
    }
}
