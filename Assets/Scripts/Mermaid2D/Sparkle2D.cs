using UnityEngine;

/// <summary>
/// 2D port of <see cref="Sparkle"/>: a tiny golden diamond that drifts away from her hand,
/// spins, pulses, fades near end of life, and self-destroys.
/// </summary>
public class Sparkle2D : MonoBehaviour
{
    Vector2 velocity;
    float life;
    float maxLifetime;
    float maxDist;
    Vector3 spawnPos;
    Vector3 baseScale;
    float spinSpeedDeg;
    Material mat;

    public void Initialize(Vector2 worldVelocity, float lifetime, float despawnDistance, Material fadeMat)
    {
        velocity = worldVelocity;
        maxLifetime = Mathf.Max(0.05f, lifetime);
        life = maxLifetime;
        maxDist = Mathf.Max(0.1f, despawnDistance);
        spawnPos = transform.position;
        baseScale = transform.localScale;
        spinSpeedDeg = Random.Range(180f, 540f) * (Random.value < 0.5f ? -1f : 1f);
        mat = fadeMat;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        transform.position += (Vector3)(velocity * dt);
        transform.Rotate(0f, 0f, spinSpeedDeg * dt);

        // Pulse + fade-out near end of life.
        float aliveFrac = 1f - life / maxLifetime;
        float pulse = 1f + Mathf.Sin(Time.time * 12f) * 0.12f;
        float fade = (aliveFrac < 0.7f) ? 1f : Mathf.Lerp(1f, 0f, (aliveFrac - 0.7f) / 0.3f);
        transform.localScale = baseScale * pulse * Mathf.Max(0.001f, fade);
        if (mat != null)
        {
            var c = mat.color;
            c.a = fade;
            mat.color = c;
        }

        life -= dt;
        if (life <= 0f) { Destroy(gameObject); return; }

        if ((transform.position - spawnPos).sqrMagnitude > maxDist * maxDist)
            Destroy(gameObject);
    }

    void OnDestroy()
    {
        if (mat != null) Destroy(mat);
    }
}
