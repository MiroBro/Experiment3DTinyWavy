using UnityEngine;

public class MermaidSwimmer : MonoBehaviour
{
    [Header("Mode")]
    [Tooltip("If true, the body stays in place and just plays the procedural swim animation (porpoise + virtual forward velocity for hair/tail). Toggle off later to enable real wandering.")]
    public bool stationary = true;
    [Tooltip("World-space direction she 'pretends' to swim toward in stationary mode.")]
    public Vector3 stationaryFacing = Vector3.forward;

    [Header("Wander Volume (used when not stationary)")]
    public Vector3 swimVolumeCenter = Vector3.zero;
    public Vector3 swimVolumeSize = new Vector3(40f, 8f, 40f);
    public float arrivalDistance = 1.5f;

    [Header("Speed & Turning")]
    [Tooltip("Forward swim speed. Real velocity when wandering, virtual forward speed when stationary.")]
    public float cruiseSpeed = 3f;
    public float maxSpeed = 6f;
    public float accelSharpness = 2.5f;
    public float turnSharpness = 4f;

    [Header("Dolphin Porpoise")]
    public float porpoiseAmplitude = 0.35f;
    public float porpoiseFrequency = 0.9f;

    [Header("Twirl / Pirouette")]
    [Tooltip("If true, the body periodically rolls a full turn around its forward axis (a mermaid pirouette).")]
    public bool enableTwirl = true;
    [Tooltip("Seconds between twirls (idle gap between pirouettes).")]
    public float twirlInterval = 5f;
    [Tooltip("Seconds the twirl takes to complete from start back to original pose.")]
    public float twirlDuration = 1.5f;
    [Tooltip("How many full turns per twirl. 1 = single pirouette, 2 = double, etc.")]
    [Range(1, 5)]
    public int twirlTurns = 1;
    [Tooltip("Easing of the roll. X = 0..1 (start..end of twirl), Y = 0..1 (rotation fraction). Ease-in-out feels like a deliberate pirouette; linear feels constant-speed.")]
    public AnimationCurve twirlCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [Tooltip("Radius of the circular sweep her body traces during a twirl, in the plane perpendicular to her forward axis. 0 = pure spin in place. ~0.2–0.5 looks like a dancerly mermaid arc.")]
    [Range(0f, 1.5f)]
    public float twirlRadius = 0.25f;

    [Header("Foraging (runtime, driven by MermaidForager)")]
    [Tooltip("1 = full swim. Driven toward ~0.15 by the forager so she slows to a near-stop and only faintly undulates while rummaging. Scales forward flow + body pitch; the bob is kept above a faint floor so she never goes fully rigid.")]
    [Range(0f, 1f)]
    public float motionScale = 1f;
    [Tooltip("Lowest fraction of the porpoise bob that remains when motionScale is 0 — the 'faint undulation' she keeps while stopped to rummage. Higher = she keeps undulating more visibly while digging.")]
    [Range(0f, 1f)]
    public float faintUndulationFloor = 0.55f;
    [Tooltip("Extra world-space translation of her whole body, driven by the forager so she leans in (down + forward) toward the seabed while rummaging. The chain follows, so her upper body dips toward her hands.")]
    public Vector3 forageBodyOffsetWorld = Vector3.zero;
    [Tooltip("When true, no NEW twirl will start (a twirl already in progress still finishes). The forager sets this while she's rummaging so she doesn't pirouette mid-dig.")]
    public bool suppressTwirl = false;
    [Tooltip("Extra downward head pitch (degrees) added on top of the swim pose. Driven by the forager so she tips her head to look down at her hands while rummaging. The neck/torso follow with the chain's lag, so her upper body curls to look down.")]
    public float lookDownDeg = 0f;

    [Header("Debug")]
    public bool drawGizmos = true;

    /// <summary>
    /// Virtual swim velocity in world space — what hair/tail/body-undulation should react to.
    /// Non-zero even when <see cref="stationary"/> is true.
    /// </summary>
    public Vector3 SwimVelocity { get; private set; }

    Vector3 targetPos;
    Vector3 velocity;
    float porpoisePhase;
    Vector3 stationaryBasePos;
    Quaternion stationaryBaseRot;

    // Twirl state. Roll is composed on top of the body's "natural" rotation each frame
    // so the chain's lag/wave reads as a flowing pirouette.
    Quaternion baseRotation = Quaternion.identity;
    float twirlIdleTimer;
    float twirlElapsed;
    bool twirlActive;

    void Start()
    {
        if (stationary)
        {
            stationaryBasePos = transform.position;
            Vector3 fwd = stationaryFacing.sqrMagnitude < 0.0001f ? Vector3.forward : stationaryFacing.normalized;
            stationaryBaseRot = Quaternion.LookRotation(fwd, Vector3.up);
            transform.rotation = stationaryBaseRot;
            baseRotation = stationaryBaseRot;
        }
        else
        {
            PickNewTarget();
            Vector3 toTarget = targetPos - transform.position;
            if (toTarget.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
            baseRotation = transform.rotation;
        }
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= Mathf.Epsilon) return;

        if (stationary) UpdateStationary(dt);
        else UpdateSwimming(dt);
    }

    void UpdateStationary(float dt)
    {
        float omega = porpoiseFrequency * 2f * Mathf.PI;
        porpoisePhase += dt * omega;

        // motionScale scales how "alive" the swim is. The bob keeps a faint floor so she
        // still gently undulates while stopped; pitch + forward flow scale all the way down
        // so she levels out and her hair/tail settle as she pauses to rummage.
        float m = Mathf.Clamp01(motionScale);
        float ampScale = Mathf.Lerp(Mathf.Clamp01(faintUndulationFloor), 1f, m);

        float yOffset = Mathf.Sin(porpoisePhase) * porpoiseAmplitude * ampScale;
        float vy = Mathf.Cos(porpoisePhase) * porpoiseAmplitude * ampScale * omega;
        float vzFull = cruiseSpeed;

        float pitchDeg = -Mathf.Atan2(vy, vzFull) * Mathf.Rad2Deg * m + lookDownDeg;
        baseRotation = stationaryBaseRot * Quaternion.Euler(pitchDeg, 0f, 0f);

        var twirl = TickTwirl(dt);
        Vector3 twirlOffsetWorld = baseRotation * twirl.offsetLocal;

        transform.position = stationaryBasePos + Vector3.up * yOffset + twirlOffsetWorld + forageBodyOffsetWorld;
        transform.rotation = baseRotation * Quaternion.Euler(0f, 0f, twirl.rollDeg);

        // Forward virtual flow (what hair/tail react to) fades with motionScale so the
        // streaming hair settles when she stops.
        SwimVelocity = stationaryBaseRot * new Vector3(0f, vy, vzFull * m);
    }

    void UpdateSwimming(float dt)
    {
        Vector3 toTarget = targetPos - transform.position;
        float dist = toTarget.magnitude;
        if (dist < arrivalDistance) { PickNewTarget(); toTarget = targetPos - transform.position; dist = toTarget.magnitude; }

        Vector3 desiredDir = (dist > 0.001f) ? toTarget / dist : transform.forward;
        Vector3 desiredVel = desiredDir * cruiseSpeed;

        float accelLerp = 1f - Mathf.Exp(-accelSharpness * dt);
        velocity = Vector3.Lerp(velocity, desiredVel, accelLerp);
        if (velocity.magnitude > maxSpeed) velocity = velocity.normalized * maxSpeed;

        float omega = porpoiseFrequency * 2f * Mathf.PI;
        porpoisePhase += dt * omega;
        float speedScale = Mathf.Clamp01(velocity.magnitude / Mathf.Max(0.01f, cruiseSpeed));
        float verticalRate = Mathf.Cos(porpoisePhase) * porpoiseAmplitude * omega * speedScale;

        Vector3 totalVel = velocity + Vector3.up * verticalRate;
        transform.position += totalVel * dt;

        if (totalVel.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(totalVel.normalized, Vector3.up);
            float turnLerp = 1f - Mathf.Exp(-turnSharpness * dt);
            baseRotation = Quaternion.Slerp(baseRotation, targetRot, turnLerp);
        }

        var twirl = TickTwirl(dt);
        Vector3 twirlOffsetWorld = baseRotation * twirl.offsetLocal;
        transform.position += twirlOffsetWorld;
        transform.rotation = baseRotation * Quaternion.Euler(0f, 0f, twirl.rollDeg);

        SwimVelocity = totalVel;
    }

    struct TwirlState { public float rollDeg; public Vector3 offsetLocal; }

    TwirlState TickTwirl(float dt)
    {
        TwirlState output = default;
        if (!enableTwirl) return output;

        if (twirlActive)
        {
            twirlElapsed += dt;
            float t = twirlElapsed / Mathf.Max(0.001f, twirlDuration);
            if (t >= 1f)
            {
                // Twirl complete — body returns to its original pose & position
                // (0 deg == 360 deg, and (1-cos(2π))=0, sin(2π)=0).
                twirlActive = false;
                twirlElapsed = 0f;
                twirlIdleTimer = 0f;
                return output;
            }

            float curveT = twirlCurve.Evaluate(t);
            int turns = Mathf.Max(1, twirlTurns);
            output.rollDeg = curveT * 360f * turns;

            if (twirlRadius > 0.001f)
            {
                // Body traces a circle of radius r in the plane perpendicular to
                // its forward axis. The (1-cos, sin) parametrization starts and ends
                // exactly at (0,0) so there's no positional snap into / out of the twirl.
                float angleRad = curveT * 2f * Mathf.PI * turns;
                output.offsetLocal = new Vector3(
                    (1f - Mathf.Cos(angleRad)) * twirlRadius,
                    Mathf.Sin(angleRad) * twirlRadius,
                    0f);
            }
            return output;
        }

        // Don't start a new twirl while the forager has asked for quiet (mid-rummage).
        if (suppressTwirl) return output;

        twirlIdleTimer += dt;
        if (twirlIdleTimer >= twirlInterval)
        {
            twirlActive = true;
            twirlElapsed = 0f;
        }
        return output;
    }

    void PickNewTarget()
    {
        Vector3 half = swimVolumeSize * 0.5f;
        targetPos = swimVolumeCenter + new Vector3(
            Random.Range(-half.x, half.x),
            Random.Range(-half.y, half.y),
            Random.Range(-half.z, half.z)
        );
    }

    void OnDrawGizmosSelected()
    {
        if (!drawGizmos || stationary) return;
        Gizmos.color = new Color(0.4f, 0.9f, 1f, 0.15f);
        Gizmos.DrawWireCube(swimVolumeCenter, swimVolumeSize);
        if (Application.isPlaying)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(targetPos, 0.25f);
            Gizmos.DrawLine(transform.position, targetPos);
        }
    }
}
