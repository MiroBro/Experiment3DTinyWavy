using UnityEngine;

/// <summary>
/// The "help her find it" mechanic (Cornerpond's clickable fishing orbs, mermaid-flavored):
/// while she rummages, glints of buried treasure briefly shine up through the stirred silt
/// near her hands. Click one before it fades and she "spots it" — each click boosts the
/// quality roll of the find she's about to make. Idle play still works; attentive play
/// finds better gems.
///
/// Visual: a pulsing golden star that grows in, shimmers, and fades. Click feedback: a
/// quick white flash + it pops away.
/// </summary>
public class RummageGlint : MonoBehaviour
{
    public float lifetime = 1.4f;
    public float clickRadius = 0.35f;
    public System.Action<RummageGlint> onClicked;

    float age;
    bool clicked;
    Material mat;
    Transform core;

    public static RummageGlint Spawn(Vector3 worldPos, float lifetime, int sortingOrder)
    {
        var go = new GameObject("RummageGlint");
        worldPos.z = 0f;
        go.transform.position = worldPos;
        var glint = go.AddComponent<RummageGlint>();
        glint.lifetime = lifetime;

        // 4-point star with a bright core (same star mesh style as the sparkles, bigger).
        var mesh = new Mesh { name = "GlintMesh" };
        mesh.vertices = new[]
        {
            Vector3.zero,
            new Vector3(0f, 1f, 0f), new Vector3(0.28f, 0f, 0f),
            new Vector3(0f, -1f, 0f), new Vector3(-0.28f, 0f, 0f),
            new Vector3(0.55f, 0.55f, 0f), new Vector3(0.55f, -0.55f, 0f),
            new Vector3(-0.55f, -0.55f, 0f), new Vector3(-0.55f, 0.55f, 0f),
        };
        Color c0 = Color.white;
        Color cTip = new Color(1f, 0.9f, 0.5f, 0f);
        mesh.colors = new[] { c0, cTip, cTip, cTip, cTip, cTip, cTip, cTip, cTip };
        mesh.triangles = new[] { 0, 1, 5, 0, 5, 2, 0, 2, 6, 0, 6, 3, 0, 3, 7, 0, 7, 4, 0, 4, 8, 0, 8, 1 };
        mesh.RecalculateBounds();

        var coreGO = new GameObject("Core");
        coreGO.transform.SetParent(go.transform, false);
        coreGO.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = coreGO.AddComponent<MeshRenderer>();
        glint.mat = new Material(Shader.Find("Sprites/Default")) { color = new Color(1f, 0.85f, 0.35f, 0f) };
        mr.sharedMaterial = glint.mat;
        mr.sortingOrder = sortingOrder;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        glint.core = coreGO.transform;
        return glint;
    }

    /// <summary>Called by the manager when the player clicks near this glint.</summary>
    public bool TryClick(Vector2 worldPoint)
    {
        if (clicked) return false;
        if (((Vector2)transform.position - worldPoint).sqrMagnitude > clickRadius * clickRadius) return false;
        clicked = true;
        age = Mathf.Min(age, lifetime - 0.22f);   // short white pop-out
        if (mat != null) mat.color = Color.white;
        onClicked?.Invoke(this);
        return true;
    }

    public bool WasClicked => clicked;

    void Update()
    {
        age += Time.deltaTime;
        if (age >= lifetime) { Destroy(gameObject); return; }

        float t = age / lifetime;
        float grow = Mathf.Clamp01(age / 0.2f);
        float fade = (t < 0.75f) ? 1f : 1f - (t - 0.75f) / 0.25f;
        float pulse = 1f + Mathf.Sin(Time.time * 9f) * 0.15f;
        float size = 0.22f * grow * pulse * (clicked ? 1.6f : 1f);
        if (core != null)
        {
            core.localScale = new Vector3(size, size, 1f);
            core.localRotation = Quaternion.Euler(0f, 0f, Time.time * 40f);
        }
        if (mat != null)
        {
            var c = mat.color;
            c.a = fade * (clicked ? 1f : 0.9f);
            mat.color = c;
        }
    }

    void OnDestroy()
    {
        if (mat != null) Destroy(mat);
        var mf = GetComponentInChildren<MeshFilter>();
        if (mf != null && mf.sharedMesh != null) Destroy(mf.sharedMesh);
    }
}
