using UnityEngine;

/// <summary>
/// 2D analog of <see cref="TubeRenderer"/>/<see cref="MermaidBodyTube"/>: a flat ribbon mesh
/// in the XY plane, lofted along a Catmull-Rom spline through a set of live bone transforms,
/// with a per-length half-width curve and a start→end vertex-color gradient. Rebuilds every
/// LateUpdate from the bone positions, so it deforms with the rig exactly like the 3D tubes.
///
/// Rendered with an unlit vertex-color shader (Sprites/Default); depth is handled purely by
/// MeshRenderer.sortingOrder.
/// </summary>
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
[DefaultExecutionOrder(60)]
public class Ribbon2D : MonoBehaviour
{
    [Tooltip("Control points the ribbon passes through. At least 2.")]
    public Transform[] points;
    [Tooltip("Half-width along the ribbon. X = 0 at the first point, 1 at the last.")]
    public AnimationCurve widthCurve = AnimationCurve.Constant(0f, 1f, 0.1f);
    [Tooltip("Multiplier on the width curve.")]
    public float widthScale = 1f;
    [Tooltip("Centerline sample count. More = smoother bends.")]
    [Range(2, 96)]
    public int samples = 24;
    public Color colorStart = Color.white;
    public Color colorEnd = Color.white;
    [Tooltip("Add semicircular caps at both ends so the ribbon doesn't end in a hard chop.")]
    public bool roundCaps = true;
    [Range(2, 12)]
    public int capSegments = 5;

    Mesh mesh;
    Vector3[] center;
    Vector3[] vertsBuf;
    Color[] colsBuf;
    int[] trisBuf;

    void Awake()
    {
        mesh = new Mesh { name = "Ribbon2D" };
        mesh.MarkDynamic();
        GetComponent<MeshFilter>().sharedMesh = mesh;
    }

    static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t, t3 = t2 * t;
        return 0.5f * ((2f * p1) + (-p0 + p2) * t
            + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2
            + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
    }

    void LateUpdate()
    {
        if (points == null || points.Length < 2 || mesh == null) return;
        Build();
    }

    void Build()
    {
        int B = points.Length;
        int N = Mathf.Max(2, samples);
        if (center == null || center.Length != N) center = new Vector3[N];

        // 1) Sample a smooth centerline through the bones (world -> local, z flattened).
        for (int i = 0; i < N; i++)
        {
            float u = (float)i / (N - 1) * (B - 1);
            int seg = Mathf.Clamp(Mathf.FloorToInt(u), 0, B - 2);
            float t = u - seg;
            Vector3 p0 = PointPos(Mathf.Max(seg - 1, 0));
            Vector3 p1 = PointPos(seg);
            Vector3 p2 = PointPos(seg + 1);
            Vector3 p3 = PointPos(Mathf.Min(seg + 2, B - 1));
            Vector3 c = transform.InverseTransformPoint(CatmullRom(p0, p1, p2, p3, t));
            c.z = 0f;
            center[i] = c;
        }

        int cap = roundCaps ? Mathf.Max(2, capSegments) : 0;
        int capVerts = roundCaps ? 2 * (cap - 1) + 2 : 0;   // fan points + 2 fan centers
        int totalV = N * 2 + capVerts;
        int totalT = ((N - 1) * 2 + (roundCaps ? 2 * cap : 0)) * 3;
        if (vertsBuf == null || vertsBuf.Length != totalV)
        {
            vertsBuf = new Vector3[totalV];
            colsBuf = new Color[totalV];
            trisBuf = new int[totalT];
        }

        // 2) Two verts per sample, offset along the in-plane normal.
        Vector3 lastGoodTangent = Vector3.right;
        for (int i = 0; i < N; i++)
        {
            Vector3 tan;
            if (i == 0) tan = center[1] - center[0];
            else if (i == N - 1) tan = center[N - 1] - center[N - 2];
            else tan = center[i + 1] - center[i - 1];
            if (tan.sqrMagnitude > 1e-10f) { tan.Normalize(); lastGoodTangent = tan; }
            else tan = lastGoodTangent;

            Vector3 normal = new Vector3(-tan.y, tan.x, 0f);
            float t01 = (float)i / (N - 1);
            float halfW = Mathf.Max(0.0005f, widthCurve.Evaluate(t01) * widthScale);
            Color col = Color.Lerp(colorStart, colorEnd, t01);

            vertsBuf[i * 2] = center[i] + normal * halfW;
            vertsBuf[i * 2 + 1] = center[i] - normal * halfW;
            colsBuf[i * 2] = col;
            colsBuf[i * 2 + 1] = col;
        }

        int ti = 0;
        for (int i = 0; i < N - 1; i++)
        {
            int r0 = i * 2, r1 = (i + 1) * 2;
            trisBuf[ti++] = r0; trisBuf[ti++] = r1; trisBuf[ti++] = r0 + 1;
            trisBuf[ti++] = r0 + 1; trisBuf[ti++] = r1; trisBuf[ti++] = r1 + 1;
        }

        // 3) Semicircular end caps: fan from +normal through -tangent (start) / +tangent
        //    (end) to -normal.
        if (roundCaps)
        {
            int v = N * 2;
            ti = BuildCap(v, 0, center[0], (center[0] - center[1]), widthCurve.Evaluate(0f) * widthScale, colsBuf[0], cap, ti);
            v += (cap - 1) + 1;
            ti = BuildCap(v, (N - 1) * 2, center[N - 1], (center[N - 1] - center[N - 2]), widthCurve.Evaluate(1f) * widthScale, colsBuf[(N - 1) * 2], cap, ti);
        }

        mesh.Clear();
        mesh.vertices = vertsBuf;
        mesh.colors = colsBuf;
        mesh.triangles = trisBuf;
        mesh.RecalculateBounds();
    }

    // Fan of `cap` triangles bulging in `outDir` between edge verts (edgeIdx, edgeIdx+1).
    int BuildCap(int vStart, int edgeIdx, Vector3 c, Vector3 outDir, float halfW, Color col, int cap, int ti)
    {
        halfW = Mathf.Max(0.0005f, halfW);
        if (outDir.sqrMagnitude < 1e-10f) outDir = Vector3.left;
        outDir.Normalize();
        Vector3 n = vertsBuf[edgeIdx] - c;
        if (n.sqrMagnitude < 1e-10f) n = new Vector3(-outDir.y, outDir.x, 0f) * halfW;

        int centerV = vStart;
        vertsBuf[centerV] = c;
        colsBuf[centerV] = col;

        int prev = edgeIdx;                     // starts at +normal edge vertex
        for (int k = 1; k < cap; k++)
        {
            float ang = Mathf.PI * k / cap;     // sweep +normal -> outDir -> -normal
            Vector3 dir = n * Mathf.Cos(ang) + outDir * (halfW * Mathf.Sin(ang));
            int idx = vStart + k;
            vertsBuf[idx] = c + dir;
            colsBuf[idx] = col;
            trisBuf[ti++] = centerV; trisBuf[ti++] = prev; trisBuf[ti++] = idx;
            prev = idx;
        }
        trisBuf[ti++] = centerV; trisBuf[ti++] = prev; trisBuf[ti++] = edgeIdx + 1;
        return ti;
    }

    Vector3 PointPos(int i)
    {
        var t = points[i];
        if (t == null) return (i > 0 && points[i - 1] != null) ? points[i - 1].position : Vector3.zero;
        return t.position;
    }
}
