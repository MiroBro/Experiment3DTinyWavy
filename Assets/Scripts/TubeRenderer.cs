using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
[DefaultExecutionOrder(50)]
public class TubeRenderer : MonoBehaviour
{
    [Tooltip("Control points the tube passes through. At least 2.")]
    public Transform[] points;
    [Tooltip("Per-point radius. Length should match points; if shorter, last entry is reused.")]
    public float[] radii;
    [Tooltip("Number of sides around the tube cross-section. Higher = smoother silhouette.")]
    [Range(3, 32)]
    public int sides = 16;
    [Tooltip("Add flat round caps so the tube isn't open at the ends.")]
    public bool capEnds = true;

    Mesh mesh;
    Vector3[] vertsBuf;
    int[] trisBuf;
    Vector3[] localPts;
    Vector3[] tangents;

    void Awake()
    {
        mesh = new Mesh();
        mesh.name = "TubeMesh";
        mesh.MarkDynamic();
        GetComponent<MeshFilter>().sharedMesh = mesh;
    }

    void LateUpdate()
    {
        if (points == null || points.Length < 2 || mesh == null) return;
        Build();
    }

    void Build()
    {
        int N = points.Length;
        int S = Mathf.Max(3, sides);
        int totalVerts = N * S + (capEnds ? 2 : 0);
        int totalTriIdx = ((N - 1) * S * 2 + (capEnds ? S * 2 : 0)) * 3;

        if (vertsBuf == null || vertsBuf.Length != totalVerts)
        {
            vertsBuf = new Vector3[totalVerts];
            trisBuf = new int[totalTriIdx];
        }
        if (localPts == null || localPts.Length != N)
        {
            localPts = new Vector3[N];
            tangents = new Vector3[N];
        }

        // 1) Convert control points to this object's local frame.
        for (int i = 0; i < N; i++)
        {
            if (points[i] == null) localPts[i] = (i > 0) ? localPts[i - 1] : Vector3.zero;
            else localPts[i] = transform.InverseTransformPoint(points[i].position);
        }

        // 2) Tangents at each control point: midpoint direction for interior, edge direction at ends.
        for (int i = 0; i < N; i++)
        {
            if (i == 0)
            {
                tangents[i] = (localPts[1] - localPts[0]).normalized;
            }
            else if (i == N - 1)
            {
                tangents[i] = (localPts[i] - localPts[i - 1]).normalized;
            }
            else
            {
                Vector3 a = (localPts[i] - localPts[i - 1]).normalized;
                Vector3 b = (localPts[i + 1] - localPts[i]).normalized;
                Vector3 sum = a + b;
                tangents[i] = (sum.sqrMagnitude > 0.0001f) ? sum.normalized : a;
            }
        }

        // 3) Vertex rings. Use a stable "up" reference and project perpendicular to the
        //    tangent at each control point. Adequate for a tail that's mostly horizontal.
        Vector3 refUp = Vector3.up;
        int v = 0;
        for (int i = 0; i < N; i++)
        {
            Vector3 t = tangents[i];
            Vector3 r = Vector3.Cross(refUp, t);
            if (r.sqrMagnitude < 0.0001f)
            {
                r = Vector3.Cross(Vector3.right, t);
                if (r.sqrMagnitude < 0.0001f) r = Vector3.up;
            }
            r.Normalize();
            Vector3 u = Vector3.Cross(t, r).normalized;

            float radius = (radii != null && i < radii.Length)
                ? radii[i]
                : (radii != null && radii.Length > 0 ? radii[radii.Length - 1] : 0.1f);

            for (int k = 0; k < S; k++)
            {
                float angle = k * 2f * Mathf.PI / S;
                Vector3 dir = Mathf.Cos(angle) * r + Mathf.Sin(angle) * u;
                vertsBuf[v++] = localPts[i] + dir * radius;
            }
        }

        int startCapIdx = -1, endCapIdx = -1;
        if (capEnds)
        {
            startCapIdx = v;
            vertsBuf[v++] = localPts[0];
            endCapIdx = v;
            vertsBuf[v++] = localPts[N - 1];
        }

        // 4) Triangles — body quads.
        int t_idx = 0;
        for (int i = 0; i < N - 1; i++)
        {
            int row0 = i * S;
            int row1 = (i + 1) * S;
            for (int k = 0; k < S; k++)
            {
                int kNext = (k + 1) % S;
                trisBuf[t_idx++] = row0 + k;
                trisBuf[t_idx++] = row0 + kNext;
                trisBuf[t_idx++] = row1 + k;

                trisBuf[t_idx++] = row0 + kNext;
                trisBuf[t_idx++] = row1 + kNext;
                trisBuf[t_idx++] = row1 + k;
            }
        }

        if (capEnds)
        {
            // Start cap (faces outward = -tangent[0]).
            for (int k = 0; k < S; k++)
            {
                int kNext = (k + 1) % S;
                trisBuf[t_idx++] = startCapIdx;
                trisBuf[t_idx++] = kNext;
                trisBuf[t_idx++] = k;
            }
            // End cap (faces +tangent[N-1]).
            int endRow = (N - 1) * S;
            for (int k = 0; k < S; k++)
            {
                int kNext = (k + 1) % S;
                trisBuf[t_idx++] = endCapIdx;
                trisBuf[t_idx++] = endRow + k;
                trisBuf[t_idx++] = endRow + kNext;
            }
        }

        mesh.Clear();
        mesh.vertices = vertsBuf;
        mesh.triangles = trisBuf;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }
}
