using UnityEngine;

/// <summary>
/// 2D port of <see cref="SparkleSpawner"/>: emits little golden sparkles from her trailing
/// hand at a steady cadence. Sparkles drift opposite her (virtual) swim direction with a
/// touch of scatter.
/// </summary>
public class Sparkle2DSpawner : MonoBehaviour
{
    [Header("Refs (auto-populated by Mermaid2DBootstrap)")]
    public Transform handTransform;
    public Mermaid2DSwimmer swimmer;

    [Header("Spawn Cadence")]
    [Tooltip("Seconds between sparkles. Smaller = faster.")]
    public float spawnInterval = 1.0f;

    [Header("Sparkle Look / Motion")]
    public float sparkleSize = 0.06f;
    public Color sparkleColor = new Color(1.0f, 0.85f, 0.40f);
    [Tooltip("Drift speed — the sparkle leaves her hand at this speed.")]
    public float sparkleSpeed = 1.6f;
    public float sparkleLifetime = 3.0f;
    public float sparkleDespawnDistance = 4.0f;
    [Tooltip("Random jitter added to each sparkle's drift direction.")]
    [Range(0f, 1f)]
    public float directionJitter = 0.18f;

    [Header("Read-only at runtime")]
    public double counter;

    public int sortingOrder = 13;

    float _timer;
    Mesh sparkleMesh;

    void Awake()
    {
        // A four-point star-ish diamond: bright center, colored points.
        sparkleMesh = new Mesh { name = "Sparkle2DMesh" };
        sparkleMesh.vertices = new[]
        {
            Vector3.zero,
            new Vector3(0f, 1f, 0f), new Vector3(0.35f, 0f, 0f),
            new Vector3(0f, -1f, 0f), new Vector3(-0.35f, 0f, 0f),
        };
        sparkleMesh.colors = new[]
        {
            Color.white,
            new Color(1f, 1f, 1f, 0.0f), new Color(1f, 1f, 1f, 0.6f),
            new Color(1f, 1f, 1f, 0.0f), new Color(1f, 1f, 1f, 0.6f),
        };
        sparkleMesh.triangles = new[] { 0, 1, 2, 0, 2, 3, 0, 3, 4, 0, 4, 1 };
        sparkleMesh.RecalculateBounds();
    }

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

        var go = new GameObject("Sparkle2D");
        Vector3 p = handTransform.position; p.z = 0f;
        go.transform.position = p;
        go.transform.localScale = new Vector3(sparkleSize, sparkleSize, 1f);

        go.AddComponent<MeshFilter>().sharedMesh = sparkleMesh;
        var mr = go.AddComponent<MeshRenderer>();
        var mat = new Material(Shader.Find("Sprites/Default")) { color = sparkleColor };
        mr.sharedMaterial = mat;
        mr.sortingOrder = sortingOrder;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;

        // Drift direction = opposite the (virtual) swim direction, with a little scatter.
        Vector2 swimDir = Vector2.right;
        if (swimmer != null)
        {
            Vector2 v = swimmer.SwimVelocity;
            if (v.sqrMagnitude > 0.0001f) swimDir = v.normalized;
        }
        Vector2 dir = -swimDir;
        if (directionJitter > 0f)
        {
            dir += new Vector2(
                Random.Range(-directionJitter, directionJitter),
                Random.Range(-directionJitter, directionJitter));
            if (dir.sqrMagnitude > 0.0001f) dir.Normalize();
            else dir = -swimDir;
        }

        var sparkle = go.AddComponent<Sparkle2D>();
        sparkle.Initialize(dir * sparkleSpeed, sparkleLifetime, sparkleDespawnDistance, mat);

        counter += 1.0;
    }
}
