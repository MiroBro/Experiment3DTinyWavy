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
    public float reachOutTime = 0.9f;
    [Tooltip("Seconds spent rummaging (hands sifting through the grass).")]
    public float rummageTime = 2.8f;
    [Tooltip("When she finishes a dig, her head/gaze leads back to the swim direction FIRST and the body rises to follow. This is how many seconds the head leads before the body starts coming up.")]
    public float headLeadTime = 0.35f;
    [Tooltip("Gaze smoothing while she lifts her head to swim off — snappier than the rummage focus time so the head clearly leads instead of lagging behind the body.")]
    [Range(0.05f, 1f)]
    public float exitLookSmoothTime = 0.16f;

    [Header("Reach")]
    [Tooltip("How far her hands extend DOWN toward the seabed while rummaging.")]
    public float reachDown = 1.1f;
    [Tooltip("Forward reach so the dig happens UNDER HER FACE rather than under her belly.")]
    public float reachForward = 1.4f;
    [Tooltip("Elbows follow the DOWNWARD reach at this fraction of the hands, for a natural arm line.")]
    [Range(0f, 1f)]
    public float elbowReachFraction = 0.6f;
    [Tooltip("Elbows follow the FORWARD reach only this much — kept small so the elbow stays behind the hand and the arm bends the natural way (a big value shoves the elbow past the hand and the arm kinks backwards).")]
    [Range(0f, 1f)]
    public float elbowForwardFraction = 0.22f;
    [Tooltip("Pin her hands to the SPOT ON THE GROUND where the dig started, instead of letting them ride up and down with the body bob. 1 = firmly planted (the body undulates above steady hands), 0 = old floating behavior.")]
    [Range(0f, 1f)]
    public float handGroundPin = 1f;
    [Tooltip("Fallback head tilt while rummaging, degrees — only used when lookAtHands is off.")]
    public float lookDownDeg = 34f;
    [Tooltip("Aim her face at the midpoint of her hands while rummaging, instead of the fixed lookDownDeg tilt.")]
    public bool lookAtHands = true;
    [Tooltip("Clamp on how far down she'll rotate her face to watch her hands, in degrees.")]
    [Range(0f, 130f)]
    public float lookAtHandsMaxDeg = 95f;
    [Tooltip("Seconds of smoothing on her rummage gaze. Higher = a calm, focused stare at the dig spot; lower = she twitchily tracks every hand stir.")]
    [Range(0.05f, 2f)]
    public float lookSmoothTime = 0.55f;
    [Tooltip("How the look-at-hands angle is split between BODY and FACE. 0 = face-only (eyes drop to the hands, body swims undisturbed), 1 = body-only (the whole neck/torso curls down, old behavior). ~0.3 = a gentle neck curl while the face does most of the looking.")]
    [Range(0f, 1f)]
    public float lookBodyShare = 0.3f;
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
    public float wiggleAmplitude = 0.10f;
    public float wiggleFrequency = 5f;
    [Tooltip("How far her HANDS rock/scoop at the wrist while digging (degrees). This is applied directly to the hand sprites (not the smoothed bones) so it reads crisply as fingers working the ground rather than a hand hovering on the spot.")]
    public float fingerDigAngle = 20f;
    [Tooltip("How fast the hands scoop while digging, oscillations per second-ish.")]
    public float fingerDigFrequency = 6.5f;

    [Header("Loot")]
    [Tooltip("Chance each find is a gem (vs a rock).")]
    [Range(0f, 1f)]
    public float gemChance = 0.55f;
    public Color gemColor = new Color(0.5f, 0.85f, 1f);
    public Color rockColor = new Color(0.6f, 0.58f, 0.52f);
    [Tooltip("Optional: your own gem sprite. When set, the procedural diamond polygon (and its color tint) is not used.")]
    public Sprite gemSprite;
    [Tooltip("Optional: your own rock sprite. When set, the procedural blob (and its color tint) is not used.")]
    public Sprite rockSprite;

    /// <summary>When set (by GemGameManager), finds are rolled by the meta-game instead of
    /// the simple gem/rock split above.</summary>
    [System.NonSerialized] public GemGameManager gameManager;

    /// <summary>
    /// While true the forager is dormant (pinned to Cruise, writes nothing to the swimmer
    /// or bones) so another driver — e.g. <see cref="MermaidSurfaceTrip"/> — can steer her.
    /// </summary>
    [System.NonSerialized] public bool suspended;

    enum Phase { Cruise, ReachIn, Rummage, ReachOut }
    Phase phase = Phase.Cruise;
    float phaseT;          // seconds elapsed in the current phase
    float cruiseTarget;    // randomized cruise duration
    float lookCurrentDeg;  // smoothed look-at-hands angle
    float lookVel;
    Vector2 lastWiggleMid; // last frame's hand-stir offset, excluded from the gaze target
    Vector3 digSpotNear, digSpotFar;   // world points the hands are pinned to while rummaging
    Transform handSpriteNear, handSpriteFar;          // hand visuals, scooped while digging
    Quaternion handSpriteNearRest, handSpriteFarRest; // their rest rotations
    bool handSpritesResolved;

    // Resume blend: when control returns from a suspension (surface trip), the body offset
    // glides from wherever the trip left her to this forager's own pose — no snap.
    bool wasSuspended;
    Vector2 resumeOffsetFrom;
    float resumeBlend;

    /// <summary>True while her hands are sifting through the grass (glints can surface).</summary>
    public bool IsRummaging => phase == Phase.Rummage;

    /// <summary>0 = cruising, 1 = fully in the rummage pose (eased). The bootstrap uses this
    /// to calm the arm flow while she digs.</summary>
    public float RummageEnvelope { get; private set; }

    void Start()
    {
        cruiseTarget = Random.Range(minCruise, maxCruise);
    }

    void Update()
    {
        if (suspended)
        {
            // Park in Cruise so she resumes with a fresh swim spell when control returns.
            phase = Phase.Cruise;
            phaseT = 0f;
            lookCurrentDeg = 0f;
            lookVel = 0f;
            RummageEnvelope = 0f;
            wasSuspended = true;
            return;
        }

        float dt = Time.deltaTime;
        phaseT += dt;

        if (wasSuspended)
        {
            wasSuspended = false;
            resumeOffsetFrom = swimmer != null ? swimmer.forageBodyOffsetWorld : Vector2.zero;
            resumeBlend = 1f;
        }
        if (resumeBlend > 0f) resumeBlend = Mathf.Max(0f, resumeBlend - dt / 1.6f);

        // Two envelopes, 0 = cruising, 1 = fully reached down:
        //   bodyEnv drives the body dip/rise + the slowdown + the hand reach,
        //   lookEnv drives the gaze.
        // They move together everywhere EXCEPT the exit, where the head leads: lookEnv
        // returns to forward first, then bodyEnv rises to follow.
        float bodyEnv = 0f;
        float lookEnv = 0f;
        switch (phase)
        {
            case Phase.Cruise:
                if (phaseT >= cruiseTarget) Enter(Phase.ReachIn);
                bodyEnv = 0f; lookEnv = 0f;
                break;

            case Phase.ReachIn:
                bodyEnv = Mathf.Clamp01(phaseT / Mathf.Max(0.01f, reachInTime));
                lookEnv = bodyEnv;
                if (phaseT >= reachInTime) Enter(Phase.Rummage);
                break;

            case Phase.Rummage:
                bodyEnv = 1f; lookEnv = 1f;
                if (phaseT >= rummageTime) { Collect(); Enter(Phase.ReachOut); }
                break;

            case Phase.ReachOut:
            {
                float total = Mathf.Max(0.01f, reachOutTime);
                float lead = Mathf.Clamp(headLeadTime, 0f, total * 0.9f);
                // Head/gaze rises back to the swim direction over the first `lead` seconds.
                lookEnv = 1f - Mathf.Clamp01(phaseT / Mathf.Max(0.01f, lead));
                // Body + hands hold in the dig pose through the lead, then rise to follow.
                bodyEnv = 1f - Mathf.Clamp01((phaseT - lead) / Mathf.Max(0.01f, total - lead));
                if (phaseT >= total)
                {
                    cruiseTarget = Random.Range(minCruise, maxCruise);
                    Enter(Phase.Cruise);
                }
                break;
            }
        }

        // The gaze snaps up quicker on the way out so the head visibly leads the body.
        float lookSmooth = (phase == Phase.ReachOut) ? exitLookSmoothTime : lookSmoothTime;

        ApplyMotion(bodyEnv, lookEnv, lookSmooth);
        ApplyReach(bodyEnv);
        ApplyFingerDig();
    }

    void Enter(Phase p)
    {
        phase = p;
        phaseT = 0f;
        // The moment the reach lands, remember WHERE the hands touched down (world space) —
        // that's the ground spot they stay pinned to for the whole rummage.
        if (p == Phase.Rummage)
        {
            if (handNear != null) digSpotNear = handNear.transform.position;
            if (handFar != null) digSpotFar = handFar.transform.position;
            digSpotNear.z = 0f; digSpotFar.z = 0f;
        }
    }

    void ApplyMotion(float bodyEnv, float lookEnv, float lookSmooth)
    {
        if (swimmer == null) return;
        float easedBody = Mathf.SmoothStep(0f, 1f, bodyEnv);
        RummageEnvelope = easedBody;
        swimmer.motionScale = Mathf.Lerp(1f, rummageMotionScale, easedBody);

        // Aim her face at the DIG SPOT (the hands' midpoint minus the stir wiggle), so her
        // gaze rests calmly on where she's digging instead of whipping after every hand
        // flick. lookSmooth rounds off what's left.
        float targetDeg = lookDownDeg;
        if (lookAtHands)
        {
            Vector3 hands;
            if (handNear != null && handFar != null)
                hands = (handNear.transform.position + handFar.transform.position) * 0.5f;
            else if (handNear != null) hands = handNear.transform.position;
            else if (handFar != null) hands = handFar.transform.position;
            else hands = swimmer.transform.position + Vector3.down;
            hands -= (Vector3)lastWiggleMid;

            Vector2 toHands = hands - swimmer.transform.position;
            if (toHands.sqrMagnitude > 0.01f)
            {
                // She faces +X; hands below-forward give a negative angle. lookDownDeg is
                // "degrees of downward face tilt", so negate.
                float angleDeg = Mathf.Atan2(toHands.y, toHands.x) * Mathf.Rad2Deg;
                targetDeg = Mathf.Clamp(-angleDeg, 0f, lookAtHandsMaxDeg);
            }
        }
        // The gaze rides the LOOK envelope (which leads on the way out), not the body one.
        float easedLook = Mathf.SmoothStep(0f, 1f, lookEnv);
        lookCurrentDeg = Mathf.SmoothDamp(lookCurrentDeg, targetDeg * easedLook, ref lookVel,
            Mathf.Max(0.05f, lookSmooth));

        // Split the look between the OUTER rotation (head bone → neck/body curl) and the
        // INNER one (face-only) so face focus is tunable without disturbing the body.
        float share = Mathf.Clamp01(lookBodyShare);
        swimmer.lookDownDeg = lookCurrentDeg * share;
        swimmer.faceLookDownDeg = lookCurrentDeg * (1f - share);
    }

    void ApplyReach(float bodyEnv)
    {
        // She faces screen-right; the reach points down-and-under regardless of how her
        // body is pitching with the porpoise.
        Vector2 fwd = Vector2.right;
        Vector2 down = Vector2.down;

        float eased = Mathf.SmoothStep(0f, 1f, bodyEnv);
        Vector2 dip = (down * reachDown + fwd * reachForward) * eased;

        // Body height: cruise lifted up over the grass, then descend + lean in over the dig
        // spot. Lerp between the two poses so she sinks to the seabed to rummage and rises
        // to swim.
        if (swimmer != null)
        {
            Vector2 cruisePose = Vector2.up * cruiseLift;
            Vector2 rummagePose = down * bodyDip + fwd * bodyLean;
            Vector2 pose = Vector2.Lerp(cruisePose, rummagePose, eased);
            // Glide in from wherever a suspension (surface trip) left her.
            if (resumeBlend > 0f)
            {
                float b = resumeBlend * resumeBlend * (3f - 2f * resumeBlend);
                pose = Vector2.Lerp(pose, resumeOffsetFrom, b);
            }
            swimmer.forageBodyOffsetWorld = pose;
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
        lastWiggleMid = (wNear + wFar) * 0.5f;

        // While rummaging, pin each hand to the WORLD spot where it touched down: the pinned
        // offset cancels the body bob out of the hand's target, so the hands work the ground
        // while the body undulates above them. (The arm can still be pulled short of the
        // spot at the top of a bob — rest-distance keeps it from stretching.)
        if (phase == Phase.Rummage && handGroundPin > 0f)
        {
            float pin = Mathf.Clamp01(handGroundPin);
            if (handNear != null)
            {
                Vector2 pinned = (Vector2)(digSpotNear - handNear.NaturalIdealPosition());
                handNear.reachOffsetWorld = Vector2.Lerp(dip, pinned, pin) + wNear;
            }
            if (handFar != null)
            {
                Vector2 pinned = (Vector2)(digSpotFar - handFar.NaturalIdealPosition());
                handFar.reachOffsetWorld = Vector2.Lerp(dip, pinned, pin) + wFar;
            }
        }
        else
        {
            if (handNear != null) handNear.reachOffsetWorld = dip + wNear;
            if (handFar != null) handFar.reachOffsetWorld = dip + wFar;
        }

        // Elbows keep the softer body-relative reach — they're allowed to undulate a little.
        // Down and forward follow separately: full-ish down, only a little forward, so the
        // elbow stays BEHIND the hand and the arm folds the natural way.
        Vector2 elbowDip = (down * (reachDown * elbowReachFraction)
                          + fwd * (reachForward * elbowForwardFraction)) * eased;
        if (elbowNear != null) elbowNear.reachOffsetWorld = elbowDip;
        if (elbowFar != null) elbowFar.reachOffsetWorld = elbowDip;
    }

    // Scoop the hand SPRITES at the wrist while digging. This rotates the sprites directly
    // (not the heavily-smoothed hand bones), so the quick digging motion actually shows —
    // otherwise the bone lag damps it into a hand that just hovers on the spot. The amplitude
    // rides RummageEnvelope, so it fades in as she reaches down and out as she lifts away.
    void ApplyFingerDig()
    {
        if (!handSpritesResolved) ResolveHandSprites();
        float env = RummageEnvelope;
        float t = Time.time * Mathf.Max(0.01f, fingerDigFrequency);
        ApplyScoop(handSpriteNear, handSpriteNearRest, t, env);
        ApplyScoop(handSpriteFar, handSpriteFarRest, t + 1.3f, env);   // hands work out of phase
    }

    void ApplyScoop(Transform spr, Quaternion rest, float t, float env)
    {
        if (spr == null) return;
        // A fast primary rock plus a faster harmonic so it reads as fingers clawing the
        // seabed, not a metronome. env fades the whole thing to rest when she's not digging.
        float claw = Mathf.Sin(t) * 0.7f + Mathf.Sin(t * 2.3f + 0.5f) * 0.3f;
        spr.localRotation = rest * Quaternion.Euler(0f, 0f, claw * fingerDigAngle * env);
    }

    void ResolveHandSprites()
    {
        handSpritesResolved = true;
        handSpriteNear = FindHandSprite(handNear);
        handSpriteFar = FindHandSprite(handFar);
        if (handSpriteNear != null) handSpriteNearRest = handSpriteNear.localRotation;
        if (handSpriteFar != null) handSpriteFarRest = handSpriteFar.localRotation;
    }

    static Transform FindHandSprite(Mermaid2DBone handBone)
    {
        if (handBone == null) return null;
        var sr = handBone.GetComponentInChildren<SpriteRenderer>();
        return sr != null ? sr.transform : null;
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

        // Meta-game mode: the manager decides what she found (items, quality, buffs, xp).
        if (gameManager != null)
        {
            gameManager.OnForageFind(spot, handForFollow);
            return;
        }

        bool isGem = Random.value < gemChance;
        Color c = isGem ? gemColor : rockColor;
        CollectedItem2D.Spawn(spot, handForFollow, isGem, c, isGem ? gemSprite : rockSprite);

        if (inventory != null)
        {
            if (isGem) inventory.AddGem();
            else inventory.AddRock();
        }
    }
}
