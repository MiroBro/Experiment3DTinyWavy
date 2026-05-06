using UnityEngine;

public class Sparkle : MonoBehaviour
{
    Vector3 velocity;
    float life;
    float maxLifetime;
    float maxDist;
    Vector3 spawnPos;
    Vector3 baseScale;
    float spinSpeedDeg;
    Vector3 spinAxis;

    public void Initialize(Vector3 worldVelocity, float lifetime, float despawnDistance)
    {
        velocity = worldVelocity;
        maxLifetime = Mathf.Max(0.05f, lifetime);
        life = maxLifetime;
        maxDist = Mathf.Max(0.1f, despawnDistance);
        spawnPos = transform.position;
        baseScale = transform.localScale;
        spinSpeedDeg = Random.Range(180f, 540f);
        spinAxis = Random.onUnitSphere;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        transform.position += velocity * dt;
        transform.Rotate(spinAxis, spinSpeedDeg * dt, Space.World);

        // Pulse + fade-out near end of life.
        float aliveFrac = 1f - life / maxLifetime;
        float pulse = 1f + Mathf.Sin(Time.time * 12f) * 0.12f;
        float fade = (aliveFrac < 0.7f) ? 1f : Mathf.Lerp(1f, 0f, (aliveFrac - 0.7f) / 0.3f);
        transform.localScale = baseScale * pulse * fade;

        life -= dt;
        if (life <= 0f) { Destroy(gameObject); return; }

        if ((transform.position - spawnPos).sqrMagnitude > maxDist * maxDist)
            Destroy(gameObject);
    }
}
