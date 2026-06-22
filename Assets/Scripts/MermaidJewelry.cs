using UnityEngine;

/// <summary>
/// Drapes the mermaid in glowing emissive-gold jewelry — a crown + forehead gem, a necklace,
/// and arm bands — parented to her bones so they ride along with the animation. The emission
/// means the scene bloom makes them shimmer like the gold in the reference art.
///
/// Built at runtime by <see cref="MermaidBootstrap"/>, which passes the bone references.
/// </summary>
public class MermaidJewelry : MonoBehaviour
{
    public Color goldColor = new Color(1.0f, 0.72f, 0.22f);
    [Tooltip("Emissive strength — keep low so it reads as polished gold, not a glowing lamp.")]
    public float emission = 0.5f;
    public Color gemColor = new Color(1.0f, 0.45f, 0.55f);

    Material _gold, _gem;

    /// <summary>Wire up and build. Any reference may be null and is simply skipped.</summary>
    public void Build(Transform head, Transform neck,
                      Transform elbowL, Transform elbowR,
                      Transform handL, Transform handR)
    {
        _gold = EmissiveMat(goldColor, emission);
        _gem = EmissiveMat(gemColor, emission * 3f);

        if (head != null)
        {
            // Crown: a tilted ring sitting on the brow, plus a small forehead gem.
            var crown = MakeRing("Crown", head, 0.30f, 0.028f, _gold);
            crown.localPosition = new Vector3(0f, 0.26f, 0.12f);
            crown.localRotation = Quaternion.Euler(18f, 0f, 0f);

            var gem = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            gem.name = "ForeheadGem";
            Strip(gem);
            gem.transform.SetParent(head, false);
            gem.transform.localPosition = new Vector3(0f, 0.16f, 0.40f);
            gem.transform.localScale = new Vector3(0.07f, 0.09f, 0.045f);
            gem.GetComponent<MeshRenderer>().sharedMaterial = _gem;
        }

        if (neck != null)
        {
            var necklace = MakeRing("Necklace", neck, 0.17f, 0.022f, _gold);
            necklace.localPosition = new Vector3(0f, -0.05f, 0f);
            necklace.localRotation = Quaternion.Euler(80f, 0f, 0f);
        }

        // Arm bands: a slim band at each elbow + wrist, sized close to the limb.
        AddBand("BandElbowL", elbowL, handL, 0.085f);
        AddBand("BandElbowR", elbowR, handR, 0.085f);
        AddBand("CuffL", handL, elbowL, 0.07f);
        AddBand("CuffR", handR, elbowR, 0.07f);
    }

    void AddBand(string name, Transform at, Transform toward, float major)
    {
        if (at == null) return;
        var ring = MakeRing(name, at, major, 0.016f, _gold);
        // Orient the ring's axis (local +Z) along the limb so it wraps like a bracelet.
        if (toward != null)
        {
            Vector3 axis = (toward.position - at.position);
            if (axis.sqrMagnitude > 1e-5f)
                ring.rotation = Quaternion.LookRotation(axis.normalized);
        }
    }

    Transform MakeRing(string name, Transform parent, float major, float minor, Material mat)
    {
        var go = new GameObject(name);
        go.AddComponent<MeshFilter>().sharedMesh = TorusMesh(major, minor, 28, 10);
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    static void Strip(GameObject go)
    {
        var c = go.GetComponent<Collider>();
        if (c != null) Destroy(c);
    }

    Material EmissiveMat(Color c, float strength)
    {
        var temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
        var mat = new Material(temp.GetComponent<Renderer>().sharedMaterial);
        Destroy(temp);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
        if (mat.HasProperty("_Color")) mat.color = c;
        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.9f);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.85f);
        mat.EnableKeyword("_EMISSION");
        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", c * strength);
        return mat;
    }

    // Torus in the XY plane, axis along local +Z (so LookRotation aligns it across a limb).
    static Mesh TorusMesh(float major, float minor, int seg, int sides)
    {
        var mesh = new Mesh { name = "Torus" };
        int vcount = seg * sides;
        var verts = new Vector3[vcount];
        var norms = new Vector3[vcount];
        var tris = new int[seg * sides * 6];
        int vi = 0;
        for (int i = 0; i < seg; i++)
        {
            float u = (float)i / seg * Mathf.PI * 2f;
            Vector3 center = new Vector3(Mathf.Cos(u) * major, Mathf.Sin(u) * major, 0f);
            Vector3 right = new Vector3(Mathf.Cos(u), Mathf.Sin(u), 0f);
            for (int j = 0; j < sides; j++)
            {
                float v = (float)j / sides * Mathf.PI * 2f;
                Vector3 n = right * Mathf.Cos(v) + Vector3.forward * Mathf.Sin(v);
                verts[vi] = center + n * minor;
                norms[vi] = n;
                vi++;
            }
        }
        int ti = 0;
        for (int i = 0; i < seg; i++)
            for (int j = 0; j < sides; j++)
            {
                int a = i * sides + j;
                int b = ((i + 1) % seg) * sides + j;
                int c = i * sides + (j + 1) % sides;
                int d = ((i + 1) % seg) * sides + (j + 1) % sides;
                tris[ti++] = a; tris[ti++] = b; tris[ti++] = c;
                tris[ti++] = c; tris[ti++] = b; tris[ti++] = d;
            }
        mesh.vertices = verts; mesh.normals = norms; mesh.triangles = tris;
        mesh.RecalculateBounds();
        return mesh;
    }
}
