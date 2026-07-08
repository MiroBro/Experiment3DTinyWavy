using UnityEngine;

/// <summary>
/// 2D port of <see cref="GentleBillow"/>: subtle, slow underwater billow — gently breathes
/// the scale and rocks the in-plane rotation of whatever it's on (the hair-volume blob).
/// Layered sines at incommensurate rates so it never looks like a loop.
/// Runs in edit mode too (own clock) so the bootstrap's animated preview billows like play.
/// </summary>
[ExecuteAlways]
public class GentleBillow2D : MonoBehaviour
{
    public float scaleAmp = 0.04f;
    public float rotAmp = 5f;
    public float speed = 0.6f;

    Vector3 baseScale;
    Quaternion baseRot;
    float seed;
    bool captured;

    // Own clock: Time.time doesn't advance in edit mode, so accumulate our own.
    float clock;
    double lastRealTime;

    void OnEnable()
    {
        lastRealTime = Time.realtimeSinceStartupAsDouble;
    }

    void Capture()
    {
        baseScale = transform.localScale;
        baseRot = transform.localRotation;
        seed = Random.value * 10f;
        captured = true;
    }

    void Update()
    {
        if (!captured) Capture();

        float dt;
        if (Application.isPlaying) dt = Time.deltaTime;
        else
        {
            double now = Time.realtimeSinceStartupAsDouble;
            dt = Mathf.Clamp((float)(now - lastRealTime), 0f, 0.05f);
            lastRealTime = now;
        }
        clock += dt;

        float t = clock * speed + seed;
        transform.localScale = baseScale + new Vector3(
            Mathf.Sin(t * 1.1f), Mathf.Sin(t * 0.9f + 1f), 0f) * scaleAmp;
        transform.localRotation = baseRot * Quaternion.Euler(
            0f, 0f, Mathf.Sin(t * 0.8f) * rotAmp);
    }
}
