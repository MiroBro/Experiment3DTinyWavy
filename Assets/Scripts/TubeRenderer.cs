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
    [Tooltip("Cross-section aspect ratio. 1 = round; <1 = flat (wide horizontal, thin vertical) for a fluke; >1 = tall narrow.")]
    [Range(0.05f, 4f)]
    public float aspectRatio = 1f;
    public enum FrameMode { ParallelTransport, WorldUpAligned }
    [Tooltip("ParallelTransport: smooth, no pinching — best for ROUND tubes that wave through any orientation. WorldUpAligned: keeps the cross-section's wide axis horizontal — best for FLAT flukes/wings so they don't twist around their own axis.")]
    public FrameMode frameMode = FrameMode.ParallelTransport;
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
        //    Defensive against degenerate (zero) tangents — fall back to the previous good one.
        Vector3 lastGoodTangent = Vector3.forward;
        for (int i = 0; i < N; i++)
        {
            Vector3 t;
            if (i == 0)
            {
                Vector3 d = localPts[1] - localPts[0];
                t = (d.sqrMagnitude > 0.000001f) ? d.normalized : lastGoodTangent;
            }
            else if (i == N - 1)
            {
                Vector3 d = localPts[i] - localPts[i - 1];
                t = (d.sqrMagnitude > 0.000001f) ? d.normalized : lastGoodTangent;
            }
            else
            {
                Vector3 a = localPts[i] - localPts[i - 1];
                Vector3 b = localPts[i + 1] - localPts[i];
                Vector3 sum = a + b;
                if (sum.sqrMagnitude > 0.000001f) t = sum.normalized;
                else if (a.sqrMagnitude > 0.000001f) t = a.normalized;
                else if (b.sqrMagnitude > 0.000001f) t = b.normalized;
                else t = lastGoodTangent;
            }
            tangents[i] = t;
            lastGoodTangent = t;
        }

        // 3) Vertex rings.
        //   ParallelTransport: carry the frame from ring to ring, rotated by
        //     FromToRotation(prevTangent, currentTangent). Smooth, no pinching, but
        //     for elliptical cross-sections (aspectRatio != 1) the ellipse rotates
        //     around the tangent as the tube waves — looks like "pulsing" on a fluke.
        //   WorldUpAligned: each ring's right axis is computed independently from
        //     world up. Keeps an ellipse's wide axis horizontal in world space.
        Vector3 prevTangent = tangents[0];
        Vector3 r, u;
        if (frameMode == FrameMode.ParallelTransport)
        {
            Vector3 refUp = (Mathf.Abs(Vector3.Dot(prevTangent, Vector3.up)) > 0.99f)
                ? Vector3.right : Vector3.up;
            r = Vector3.Cross(refUp, prevTangent);
        }
        else
        {
            r = Vector3.Cross(Vector3.up, prevTangent);
            if (r.sqrMagnitude < 0.0001f) r = Vector3.Cross(Vector3.right, prevTangent);
        }
        if (r.sqrMagnitude < 0.0001f) r = Vector3.right;
        r = r.normalized;
        u = Vector3.Cross(prevTangent, r).normalized;

        int v = 0;
        for (int i = 0; i < N; i++)
        {
            Vector3 t = tangents[i];

            if (frameMode == FrameMode.ParallelTransport)
            {
                if (i > 0)
                {
                    Quaternion delta = Quaternion.FromToRotation(prevTangent, t);
                    r = delta * r;
                    u = delta * u;
                }
            }
            else // WorldUpAligned: recompute r from world up at each ring.
            {
                r = Vector3.Cross(Vector3.up, t);
                if (r.sqrMagnitude < 0.0001f) r = Vector3.Cross(Vector3.right, t);
            }

            // Re-orthogonalize against the current tangent (kills numerical drift /
            // fixes the rare case where r ended up nearly parallel to t).
            r -= Vector3.Dot(r, t) * t;
            if (r.sqrMagnitude < 0.0001f)
            {
                r = (Mathf.Abs(t.y) > 0.99f) ? Vector3.Cross(Vector3.right, t) : Vector3.Cross(Vector3.up, t);
            }
            r = r.normalized;
            u = Vector3.Cross(t, r).normalized;

            float radius = (radii != null && i < radii.Length)
                ? radii[i]
                : (radii != null && radii.Length > 0 ? radii[radii.Length - 1] : 0.1f);

            for (int k = 0; k < S; k++)
            {
                float angle = k * 2f * Mathf.PI / S;
                Vector3 dir = Mathf.Cos(angle) * r + Mathf.Sin(angle) * u * aspectRatio;
                vertsBuf[v++] = localPts[i] + dir * radius;
            }
            prevTangent = t;
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
