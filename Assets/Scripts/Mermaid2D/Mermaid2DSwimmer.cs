using UnityEngine;

/// <summary>
/// 2D port of <see cref="MermaidSwimmer"/>'s stationary mode: the head (driver) stays in
/// place and plays the procedural swim — a porpoise bob plus a virtual forward velocity that
/// the hair/tail/seaweed react to. She faces screen-right (+X). No twirl in 2D.
///
/// The forager drives <see cref="motionScale"/>, <see cref="lookDownDeg"/> and
/// <see cref="forageBodyOffsetWorld"/> so she slows, dips and looks down while rummaging.
/// </summary>
public class Mermaid2DSwimmer : MonoBehaviour
{
    [Header("Dolphin Porpoise")]
    public float porpoiseAmplitude = 0.29f;
    public float porpoiseFrequency = 0.83f;
    [Tooltip("Virtual forward speed — what hair/tail/seaweed-scroll react to.")]
    public float cruiseSpeed = 3f;

    [Header("Foraging (runtime, driven by Mermaid2DForager)")]
    [Tooltip("1 = full swim. Driven toward ~0.55 by the forager so she slows and only faintly undulates while rummaging. Scales forward flow + body pitch; the bob keeps a faint floor so she never goes fully rigid.")]
    [Range(0f, 1f)]
    public float motionScale = 1f;
    [Tooltip("Lowest fraction of the porpoise bob that remains when motionScale is 0 — the faint undulation she keeps while stopped to rummage.")]
    [Range(0f, 1f)]
    public float faintUndulationFloor = 0.55f;
    [Tooltip("Extra world-space translation of her whole body, driven by the forager so she cruises lifted over the grass and dips down+forward to dig.")]
    public Vector2 forageBodyOffsetWorld = Vector2.zero;
    [Tooltip("Extra downward head pitch (degrees) added on top of the swim pose, driven by the forager. The neck/torso follow with the chain's lag, so her upper body curls to look down at her hands.")]
    public float lookDownDeg = 0f;

    /// <summary>
    /// Virtual swim velocity in world space — what hair/tail/seaweed should react to.
    /// Non-zero even though she stays in place (treadmill illusion).
    /// </summary>
    public Vector2 SwimVelocity { get; private set; }

    Vector3 basePos;
    float porpoisePhase;

    void Start()
    {
        basePos = transform.position;
        basePos.z = 0f;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= Mathf.Epsilon) return;

        float omega = porpoiseFrequency * 2f * Mathf.PI;
        porpoisePhase += dt * omega;

        // motionScale scales how "alive" the swim is. The bob keeps a faint floor so she
        // still gently undulates while stopped; pitch + forward flow scale all the way down
        // so she levels out and her hair/tail settle as she pauses to rummage.
        float m = Mathf.Clamp01(motionScale);
        float ampScale = Mathf.Lerp(Mathf.Clamp01(faintUndulationFloor), 1f, m);

        float yOffset = Mathf.Sin(porpoisePhase) * porpoiseAmplitude * ampScale;
        float vy = Mathf.Cos(porpoisePhase) * porpoiseAmplitude * ampScale * omega;

        // Facing +X: rising = nose up = positive Z rotation. lookDown tips the nose down.
        float pitchDeg = Mathf.Atan2(vy, Mathf.Max(0.01f, cruiseSpeed)) * Mathf.Rad2Deg * m - lookDownDeg;

        transform.position = basePos + Vector3.up * yOffset + (Vector3)forageBodyOffsetWorld;
        transform.rotation = Quaternion.Euler(0f, 0f, pitchDeg);

        // Forward virtual flow fades with motionScale so hair/tail/scroll settle when she stops.
        SwimVelocity = new Vector2(cruiseSpeed * m, vy);
    }
}
