using UnityEngine;

/// <summary>
/// A little gem or rock that pops out of the grass when the mermaid finds it: scales up,
/// drifts up toward a target (her hand), spins, then fades and self-destroys. Purely visual —
/// the inventory count is incremented by the forager at spawn time.
/// </summary>
public class CollectedItem : MonoBehaviour
{
    Transform follow;        // optional: a hand to drift toward
    Vector3 startPos;
    Vector3 baseScale;
    float life, maxLife;
    float spinDeg;
    Vector3 spinAxis;

    /// <summary>
    /// Spawn a collected item.
    /// </summary>
    /// <param name="worldPos">Where it emerges (in the grass).</param>
    /// <param name="followTarget">Hand to drift toward; may be null.</param>
    /// <param name="isGem">Gem (faceted + emissive) vs rock (rounded, matte).</param>
    /// <param name="color">Tint.</param>
    public static CollectedItem Spawn(Vector3 worldPos, Transform followTarget, bool isGem, Color color)
    {
        // Octahedron-ish gem from a scaled cube turned on its corner; rock is a lumpy sphere.
        var go = GameObject.CreatePrimitive(isGem ? PrimitiveType.Cube : PrimitiveType.Sphere);
        go.name = isGem ? "Gem" : "Rock";
        var col = go.GetComponent<Collider>();
        if (col != null) Destroy(col);

        go.transform.position = worldPos;
        if (isGem)
        {
            go.transform.rotation = Quaternion.Euler(35f, 45f, 15f);
            go.transform.localScale = new Vector3(0.14f, 0.20f, 0.14f);
        }
        else
        {
            go.transform.rotation = Random.rotation;
            go.transform.localScale = new Vector3(0.17f, 0.14f, 0.16f);
        }

        var rend = go.GetComponent<Renderer>();
        var mat = new Material(rend.sharedMaterial);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Color")) mat.color = color;
        if (isGem && mat.HasProperty("_EmissionColor"))
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", color * 2.2f);
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        rend.material = mat;

        var item = go.AddComponent<CollectedItem>();
        item.follow = followTarget;
        item.startPos = worldPos;
        item.baseScale = go.transform.localScale;
        item.maxLife = 1.1f;
        item.life = item.maxLife;
        item.spinDeg = Random.Range(120f, 260f);
        item.spinAxis = Random.onUnitSphere;
        return item;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        life -= dt;
        if (life <= 0f) { Destroy(gameObject); return; }

        float aliveFrac = 1f - life / maxLife;   // 0 -> 1 over its lifetime

        // Drift: rise out of the grass, easing toward the hand if we have one.
        Vector3 target = (follow != null) ? follow.position + Vector3.up * 0.05f
                                          : startPos + Vector3.up * 0.5f;
        transform.position = Vector3.Lerp(startPos, target, Mathf.SmoothStep(0f, 1f, aliveFrac))
                             + Vector3.up * (aliveFrac * 0.15f);

        transform.Rotate(spinAxis, spinDeg * dt, Space.World);

        // Pop in fast, hold, then shrink/fade out at the end.
        float popIn = Mathf.Clamp01(aliveFrac / 0.18f);
        float fadeOut = (aliveFrac < 0.7f) ? 1f : Mathf.Lerp(1f, 0f, (aliveFrac - 0.7f) / 0.3f);
        transform.localScale = baseScale * (0.2f + 0.8f * popIn) * fadeOut;
    }
}
