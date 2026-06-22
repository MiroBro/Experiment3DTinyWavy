using UnityEngine;

/// <summary>
/// A smooth body part lofted along a spline through a set of bones — the same idea that made
/// the lathe-bottles look good: one continuous surface from a shaped profile, not assembled
/// primitives. The centerline is a Catmull-Rom spline through the bone positions (so it bends
/// with the animation), and the radius along it comes from a profile curve (e.g. wide bust →
/// pinched waist → hips for a torso, or a taper for an arm). Cross-sections can be elliptical.
///
/// Rebuilds every LateUpdate from the live bone positions, so it deforms with the rig exactly
/// like the tail tube does.
/// </summary>
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
[DefaultExecutionOrder(60)]
public class MermaidBodyTube : MonoBehaviour
{
    public Transform[] bones;        // ordered along the part (e.g. neck → torso → hip)
    [Range(4, 64)] public int samples = 24;
    [Range(3, 32)] public int sides = 16;
    [Tooltip("Cross-section flatten: 1 = round, <1 = flatter front-to-back.")]
    public float aspect = 0.85f;
    [Tooltip("Radius along the part. X = 0 at the first bone, 1 at the last.")]
    public AnimationCurve radius = AnimationCurve.Constant(0, 1, 0.2f);
    public bool capEnds = true;

    Mesh mesh;
    Vector3[] center;
    Vector3[] vbuf; Vector2[] uvbuf; int[] tbuf;

    void Awake()
    {
        mesh = new Mesh { name = "BodyTube" };
        mesh.MarkDynamic();
        GetComponent<MeshFilter>().sharedMesh = mesh;
    }

    void LateUpdate()
    {
        if (bones == null || bones.Length < 2) return;
        Build();
    }

    static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t, t3 = t2 * t;
        return 0.5f * ((2f * p1) + (-p0 + p2) * t
            + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2
            + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
    }

    void Build()
    {
        int B = bones.Length;
        int N = samples + 1;
        if (center == null || center.Length != N) center = new Vector3[N];

        // Sample a smooth centerline through the bones (world → local).
        for (int i = 0; i < N; i++)
        {
            float u = (float)i / (N - 1) * (B - 1);
            int seg = Mathf.Clamp(Mathf.FloorToInt(u), 0, B - 2);
            float t = u - seg;
            Vector3 p0 = bones[Mathf.Max(seg - 1, 0)].position;
            Vector3 p1 = bones[seg].position;
            Vector3 p2 = bones[seg + 1].position;
            Vector3 p3 = bones[Mathf.Min(seg + 2, B - 1)].position;
            center[i] = transform.InverseTransformPoint(CatmullRom(p0, p1, p2, p3, t));
        }

        int S = Mathf.Max(3, sides);
        int totalV = N * S + (capEnds ? 2 : 0);
        int totalT = ((N - 1) * S * 2 + (capEnds ? S * 2 : 0)) * 3;
        if (vbuf == null || vbuf.Length != totalV) { vbuf = new Vector3[totalV]; uvbuf = new Vector2[totalV]; tbuf = new int[totalT]; }

        // Parallel-transport frame (smooth, no pinch) — same as the tail tube.
        Vector3 prevT = (center[1] - center[0]).normalized;
        if (prevT.sqrMagnitude < 1e-6f) prevT = Vector3.forward;
        Vector3 up = (Mathf.Abs(Vector3.Dot(prevT, Vector3.up)) > 0.99f) ? Vector3.right : Vector3.up;
        Vector3 r = Vector3.Cross(up, prevT).normalized;
        Vector3 u2 = Vector3.Cross(prevT, r).normalized;

        int v = 0;
        for (int i = 0; i < N; i++)
        {
            Vector3 tan;
            if (i == 0) tan = (center[1] - center[0]);
            else if (i == N - 1) tan = (center[N - 1] - center[N - 2]);
            else tan = (center[i + 1] - center[i - 1]);
            tan = tan.sqrMagnitude > 1e-8f ? tan.normalized : prevT;

            Quaternion delta = Quaternion.FromToRotation(prevT, tan);
            r = (delta * r).normalized;
            u2 = Vector3.Cross(tan, r).normalized;
            r = Vector3.Cross(u2, tan).normalized;

            float rad = Mathf.Max(0.001f, radius.Evaluate((float)i / (N - 1)));
            for (int k = 0; k < S; k++)
            {
                float a = k * 2f * Mathf.PI / S;
                Vector3 dir = Mathf.Cos(a) * r + Mathf.Sin(a) * u2 * aspect;
                vbuf[v] = center[i] + dir * rad;
                uvbuf[v] = new Vector2((float)k / S, (float)i / (N - 1));
                v++;
            }
            prevT = tan;
        }

        int capA = -1, capB = -1;
        if (capEnds) { capA = v; vbuf[v] = center[0]; uvbuf[v++] = new Vector2(0.5f, 0f); capB = v; vbuf[v] = center[N - 1]; uvbuf[v++] = new Vector2(0.5f, 1f); }

        int ti = 0;
        for (int i = 0; i < N - 1; i++)
        {
            int row0 = i * S, row1 = (i + 1) * S;
            for (int k = 0; k < S; k++)
            {
                int kn = (k + 1) % S;
                tbuf[ti++] = row0 + k; tbuf[ti++] = row0 + kn; tbuf[ti++] = row1 + k;
                tbuf[ti++] = row0 + kn; tbuf[ti++] = row1 + kn; tbuf[ti++] = row1 + k;
            }
        }
        if (capEnds)
        {
            for (int k = 0; k < S; k++) { int kn = (k + 1) % S; tbuf[ti++] = capA; tbuf[ti++] = kn; tbuf[ti++] = k; }
            int er = (N - 1) * S;
            for (int k = 0; k < S; k++) { int kn = (k + 1) % S; tbuf[ti++] = capB; tbuf[ti++] = er + k; tbuf[ti++] = er + kn; }
        }

        mesh.Clear();
        mesh.vertices = vbuf; mesh.uv = uvbuf; mesh.triangles = tbuf;
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
    }
}
