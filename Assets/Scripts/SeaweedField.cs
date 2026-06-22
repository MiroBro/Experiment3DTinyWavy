using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// A whole bed of seaweed in ONE combined mesh, animated entirely on the GPU by the
/// Seaweed/Flow shader. Each blade is a flat ribbon of quads baked into the shared mesh with
/// per-vertex data (height, sway direction, phase). The shader does the swaying and the
/// body-collision per vertex, so the only per-frame CPU work is pushing ~7 body-sphere
/// uniforms — thousands of blades stay a single cheap draw call.
///
/// This replaces the old one-GameObject-per-blade approach (which rebuilt thousands of meshes
/// on the CPU every frame and tanked the framerate).
///
/// Build happens at Start for runtime, or on demand via the "Rebuild Seaweed" context-menu
/// button (right-click the component) so you can place/preview a bed in the editor.
/// </summary>
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
[ExecuteAlways]
public class SeaweedField : MonoBehaviour
{
    [Header("Patch")]
    [Tooltip("World-space centre of the seabed the blades root to.")]
    public Vector3 patchCenter = new Vector3(0f, -1.05f, -1f);
    [Tooltip("Footprint of the patch on the seabed plane: X width, Z depth.")]
    public Vector2 patchSize = new Vector2(5f, 6f);
    [Tooltip("Blade count. The whole bed is one mesh + one draw call, so this is cheap — but more blades = more verts to bake.")]
    [Range(1, 20000)]
    public int bladeCount = 1500;
    public int seed = 1234;

    [Header("Blade Shape")]
    public Vector2 heightRange = new Vector2(0.55f, 0.95f);
    [Tooltip("Segments up each blade. 4 is plenty — the GPU sway bends smoothly between them.")]
    [Range(2, 8)]
    public int segments = 4;
    [Tooltip("Half-width of the blade at the root / tip.")]
    public float baseHalfWidth = 0.05f;
    public float tipHalfWidth = 0.014f;

    [Header("Underwater Flow (live)")]
    public float swayAmplitude = 0.35f;
    public float swayFrequency = 0.8f;
    public float waveCount = 1.1f;
    public float flutterAmplitude = 0.10f;
    [Range(0f, 1f)]
    public float rootDarken = 0.45f;

    [Header("Colour")]
    public Color colorA = new Color(0.10f, 0.42f, 0.20f);
    public Color colorB = new Color(0.22f, 0.62f, 0.34f);

    // Shared body-avoidance spheres (assigned by the bootstrap at runtime). Pushed to the
    // shader each frame so the grass parts around her body and digging hands.
    [System.NonSerialized] public Transform[] bodyColliders;
    [System.NonSerialized] public float[] bodyRadii;

    [Header("Treadmill Scroll")]
    [Tooltip("How fast the grass scrolls past her (to fake swimming forward) relative to her swim speed. 0 = static bed.")]
    public float scrollScale = 0.5f;
    // Assigned by the bootstrap — drives scroll speed (and stops it while she rummages).
    [System.NonSerialized] public MermaidSwimmer swimmer;
    Vector2 scroll;

    Material mat;
    Mesh mesh;
    readonly Vector4[] sphereBuf = new Vector4[16];
    static readonly int IdSpheres = Shader.PropertyToID("_BodySpheres");
    static readonly int IdCount   = Shader.PropertyToID("_BodyCount");
    static readonly int IdScroll = Shader.PropertyToID("_Scroll");
    static readonly int IdPatchCenter = Shader.PropertyToID("_PatchCenter");
    static readonly int IdPatchHalf = Shader.PropertyToID("_PatchHalf");

    void Start()
    {
        // At runtime always (re)build deterministically. In the editor, only auto-build if
        // nothing's there yet, so a hand-placed/previewed bed isn't clobbered on every reload.
        if (Application.isPlaying || GetComponent<MeshFilter>().sharedMesh == null)
            Rebuild();
        else
            EnsureMaterial();
    }

    [ContextMenu("Rebuild Seaweed")]
    public void Rebuild()
    {
        EnsureMaterial();
        BuildMesh();
    }

    void EnsureMaterial()
    {
        if (mat == null)
        {
            var shader = Shader.Find("Seaweed/Flow");
            if (shader == null)
            {
                Debug.LogError("SeaweedField: shader 'Seaweed/Flow' not found.");
                return;
            }
            mat = new Material(shader) { name = "SeaweedFlowMat" };
        }
        PushMaterialParams();
        var mr = GetComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
    }

    void PushMaterialParams()
    {
        if (mat == null) return;
        mat.SetFloat("_SwayAmp", swayAmplitude);
        mat.SetFloat("_SwayFreq", swayFrequency);
        mat.SetFloat("_WaveCount", waveCount);
        mat.SetFloat("_FlutterAmp", flutterAmplitude);
        mat.SetFloat("_RootDarken", rootDarken);
    }

    void BuildMesh()
    {
        var prev = Random.state;
        Random.InitState(seed);

        int N = Mathf.Max(2, segments);
        int rings = N + 1;
        int vpb = rings * 2;          // verts per blade
        int count = Mathf.Max(1, bladeCount);

        var verts  = new List<Vector3>(count * vpb);
        var uv0    = new List<Vector2>(count * vpb);
        var uv1    = new List<Vector4>(count * vpb);
        var uv2    = new List<Vector2>(count * vpb);   // per-vertex blade-root XZ (for treadmill wrap)
        var cols   = new List<Color>(count * vpb);
        var tris   = new List<int>(count * N * 6);

        for (int b = 0; b < count; b++)
        {
            float x = Random.Range(-0.5f, 0.5f) * patchSize.x;
            float z = Random.Range(-0.5f, 0.5f) * patchSize.y;
            Vector3 root = patchCenter + new Vector3(x, 0f, z);

            float height = Random.Range(heightRange.x, heightRange.y);

            // Random horizontal facing (wide axis) so the bed shows broad faces from every
            // camera angle, and an independent random sway/flow direction.
            float faceDeg = Random.Range(0f, 360f);
            Vector3 wide = new Vector3(Mathf.Cos(faceDeg * Mathf.Deg2Rad), 0f, Mathf.Sin(faceDeg * Mathf.Deg2Rad));
            float swayDeg = Random.Range(0f, 360f);
            Vector2 swayDir = new Vector2(Mathf.Cos(swayDeg * Mathf.Deg2Rad), Mathf.Sin(swayDeg * Mathf.Deg2Rad));
            float phase = Random.Range(0f, Mathf.PI * 2f);
            float amp = Random.Range(0.8f, 1.2f);

            Color shade = Color.Lerp(colorA, colorB, Random.value);
            Vector4 sway = new Vector4(swayDir.x, swayDir.y, phase, amp);

            int baseIdx = verts.Count;
            for (int i = 0; i < rings; i++)
            {
                float h = (float)i / N;
                Vector3 center = root + Vector3.up * (height * h);
                float halfW = Mathf.Lerp(baseHalfWidth, tipHalfWidth, h);

                Vector2 rootXZ = new Vector2(root.x, root.z);
                verts.Add(center - wide * halfW); uv0.Add(new Vector2(0f, h)); uv1.Add(sway); uv2.Add(rootXZ); cols.Add(shade);
                verts.Add(center + wide * halfW); uv0.Add(new Vector2(1f, h)); uv1.Add(sway); uv2.Add(rootXZ); cols.Add(shade);
            }

            for (int i = 0; i < N; i++)
            {
                int a = baseIdx + i * 2;     // left, ring i
                int bb = a + 1;              // right, ring i
                int c = baseIdx + (i + 1) * 2; // left, ring i+1
                int d = c + 1;              // right, ring i+1
                // Two tris per quad (double-sided via Cull Off in the shader).
                tris.Add(a); tris.Add(c); tris.Add(bb);
                tris.Add(bb); tris.Add(c); tris.Add(d);
            }
        }

        if (mesh == null)
        {
            mesh = new Mesh { name = "SeaweedBedMesh" };
        }
        mesh.Clear();
        mesh.indexFormat = (verts.Count > 65000)
            ? UnityEngine.Rendering.IndexFormat.UInt32
            : UnityEngine.Rendering.IndexFormat.UInt16;
        mesh.SetVertices(verts);
        mesh.SetUVs(0, uv0);
        mesh.SetUVs(1, uv1);
        mesh.SetUVs(2, uv2);
        mesh.SetColors(cols);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        // Pad bounds so GPU sway/collision displacement never frustum-culls the bed.
        var bnds = mesh.bounds; bnds.Expand(2f); mesh.bounds = bnds;

        GetComponent<MeshFilter>().sharedMesh = mesh;

        Random.state = prev;

        if (Application.isPlaying)
            Debug.Log($"SeaweedField: built {count} blades = {verts.Count} verts (1 mesh, 1 draw call) at {patchCenter}, patch {patchSize}.");
    }

    void Update()
    {
        // Keep live-tunable flow params flowing to the material (cheap).
        PushMaterialParams();

        if (mat == null) return;

        // Treadmill: scroll the bed opposite her forward motion so she appears to swim across
        // it, and ease the speed to ~0 while she rummages. The shader wraps blades within the
        // patch, so it's seamless and stays one draw call. Scroll is kept wrapped to the patch
        // size for float precision.
        if (swimmer != null && Application.isPlaying)
        {
            // Threshold sits above her rummage motionScale (~0.55) so the scroll comes to a
            // COMPLETE stop while she digs, and ramps back up to full as she resumes swimming.
            float factor = Mathf.SmoothStep(0.6f, 0.97f, swimmer.motionScale);
            Vector3 f = swimmer.transform.forward; f.y = 0f;
            if (f.sqrMagnitude > 1e-4f) f.Normalize();
            float speed = swimmer.cruiseSpeed * scrollScale * factor;
            scroll.x -= f.x * speed * Time.deltaTime;
            scroll.y -= f.z * speed * Time.deltaTime;
            scroll.x = Mathf.Repeat(scroll.x, Mathf.Max(0.01f, patchSize.x));
            scroll.y = Mathf.Repeat(scroll.y, Mathf.Max(0.01f, patchSize.y));
        }
        mat.SetVector(IdScroll, new Vector4(scroll.x, scroll.y, 0f, 0f));
        mat.SetVector(IdPatchCenter, new Vector4(patchCenter.x, patchCenter.z, 0f, 0f));
        mat.SetVector(IdPatchHalf, new Vector4(patchSize.x * 0.5f, patchSize.y * 0.5f, 0f, 0f));

        // Push body spheres to the shader so the grass parts around her. Only meaningful at
        // runtime (the procedural mermaid doesn't exist in edit mode).
        int n = 0;
        if (bodyColliders != null)
        {
            n = Mathf.Min(bodyColliders.Length, sphereBuf.Length);
            for (int i = 0; i < sphereBuf.Length; i++)
            {
                if (i < n && bodyColliders[i] != null)
                {
                    Vector3 p = bodyColliders[i].position;
                    float r = (bodyRadii != null && i < bodyRadii.Length) ? bodyRadii[i] : 0f;
                    sphereBuf[i] = new Vector4(p.x, p.y, p.z, r);
                }
                else sphereBuf[i] = Vector4.zero;
            }
        }
        mat.SetVectorArray(IdSpheres, sphereBuf);
        mat.SetInt(IdCount, n);
    }
}
