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

    void Start()
    {
        if (stationary)
        {
            stationaryBasePos = transform.position;
            Vector3 fwd = stationaryFacing.sqrMagnitude < 0.0001f ? Vector3.forward : stationaryFacing.normalized;
            stationaryBaseRot = Quaternion.LookRotation(fwd, Vector3.up);
            transform.rotation = stationaryBaseRot;
        }
        else
        {
            PickNewTarget();
            Vector3 toTarget = targetPos - transform.position;
            if (toTarget.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
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

        float yOffset = Mathf.Sin(porpoisePhase) * porpoiseAmplitude;
        float vy = Mathf.Cos(porpoisePhase) * porpoiseAmplitude * omega;
        float vz = cruiseSpeed;

        transform.position = stationaryBasePos + Vector3.up * yOffset;

        float pitchDeg = -Mathf.Atan2(vy, vz) * Mathf.Rad2Deg;
        transform.rotation = stationaryBaseRot * Quaternion.Euler(pitchDeg, 0f, 0f);

        SwimVelocity = stationaryBaseRot * new Vector3(0f, vy, vz);
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
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, turnLerp);
        }

        SwimVelocity = totalVel;
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
