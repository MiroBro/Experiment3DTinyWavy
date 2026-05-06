using UnityEngine;

public class SparkleSpawner : MonoBehaviour
{
    [Header("Refs (auto-populated by MermaidBootstrap)")]
    public Transform handTransform;
    public MermaidSwimmer swimmer;

    [Header("Spawn Cadence")]
    [Tooltip("Seconds between sparkles. Smaller = faster.")]
    public float spawnInterval = 1.0f;

    [Header("Counter")]
    [Tooltip("How much each sparkle adds to the counter.")]
    public double sparkleValue = 1.0;

    [Header("Sparkle Look / Motion")]
    public Vector3 sparkleScale = new Vector3(0.12f, 0.12f, 0.12f);
    public Color sparkleColor = new Color(1.0f, 0.85f, 0.40f);
    [Tooltip("Brightness multiplier for the sparkle's emissive color.")]
    public float emissionStrength = 4f;
    [Tooltip("Drift speed (m/s) — the sparkle leaves her hand at this speed.")]
    public float sparkleSpeed = 1.6f;
    [Tooltip("Seconds before the sparkle automatically despawns.")]
    public float sparkleLifetime = 3.0f;
    [Tooltip("Despawn distance from the hand. The sparkle is destroyed when it gets this far.")]
    public float sparkleDespawnDistance = 4.0f;
    [Tooltip("Random jitter added to each sparkle's drift direction (0 = straight back, 0.3 = some scatter).")]
    [Range(0f, 1f)]
    public float directionJitter = 0.18f;

    [Header("Read-only at runtime")]
    public double counter;

    float _timer;

    void Update()
    {
        if (handTransform == null) return;
        _timer += Time.deltaTime;
        if (_timer >= spawnInterval)
        {
            _timer -= spawnInterval;
            SpawnSparkle();
        }
    }

    public void SpawnSparkle()
    {
        if (handTransform == null) return;

        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "Sparkle";
        var col = go.GetComponent<Collider>();
        if (col != null) Destroy(col);
        go.transform.position = handTransform.position;
        go.transform.localScale = sparkleScale;

        // Emissive material so it pops against the body.
        var rend = go.GetComponent<Renderer>();
        var mat = new Material(rend.sharedMaterial);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", sparkleColor);
        if (mat.HasProperty("_Color")) mat.color = sparkleColor;
        if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", sparkleColor * emissionStrength);
        mat.EnableKeyword("_EMISSION");
        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        rend.material = mat;

        // Drift direction = opposite the (virtual) swim direction, with a little scatter.
        Vector3 swimDir = Vector3.forward;
        if (swimmer != null)
        {
            Vector3 v = swimmer.SwimVelocity;
            if (v.sqrMagnitude > 0.0001f) swimDir = v.normalized;
        }
        Vector3 dir = -swimDir;
        if (directionJitter > 0f)
        {
            dir += new Vector3(
                Random.Range(-directionJitter, directionJitter),
                Random.Range(-directionJitter, directionJitter),
                Random.Range(-directionJitter, directionJitter));
            if (dir.sqrMagnitude > 0.0001f) dir.Normalize();
            else dir = -swimDir;
        }

        var sparkle = go.AddComponent<Sparkle>();
        sparkle.Initialize(dir * sparkleSpeed, sparkleLifetime, sparkleDespawnDistance);

        counter += sparkleValue;
    }
}
