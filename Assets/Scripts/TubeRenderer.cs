using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
[DefaultExecutionOrder(50)]
public class TubeRenderer : MonoBehaviour
{
    [Tooltip("Control points the tube passes through. At least 2.")]
    public Transform[] points;
    [Tooltip("Per-point radius. Length should match points; if shorter, last entry is reused.")]
    public float[] radii;
    [Tooltip("Optional dense half-width profile sampled by normalized position along the tube (0 = first point, 1 = last point). When set (length >= 2) it OVERRIDES radii, letting the silhouette carry far more detail than one value per control point (jagged / scalloped edges) — pair with subdivisions so the mesh has rings to show it.")]
    public float[] radiusProfile;
    [Tooltip("Mesh rings per control-point segment. 1 = one ring per point (classic behavior). Higher values Catmull-Rom-interpolate the spine between control points and sample the radius per ring, so fine silhouette detail doesn't require more physics bones.")]
    [Range(1, 8)]
    public int subdivisions = 1;
    [Tooltip("Number of sides around the tube cross-section. Higher = smoother silhouette.")]
    [Range(3, 32)]
    public int sides = 16;
    [Tooltip("Cross-section aspect ratio. 1 = round; <1 = flat (wide horizontal, thin vertical) for a fluke; >1 = tall narrow. Used as a fallback if aspectRatios is null/empty.")]
    [Range(0.05f, 4f)]
    public float aspectRatio = 1f;
    [Tooltip("Optional per-control-point aspect ratio. If set and matches points length, this overrides the single aspectRatio above. Lets you taper from round at one end to flat at the other.")]
    public float[] aspectRatios;
    public enum FrameMode { ParallelTransport, WorldUpAligned, FixedReference }
    [Tooltip("ParallelTransport: smooth, no pinching — best for ROUND tubes that wave through any orientation. WorldUpAligned: keeps the cross-section's wide axis horizontal — best for FLAT flukes/wings so they don't twist around their own axis. FixedReference: locks the wide axis to referenceAxis (re-orthogonalized per ring) — best for FLAT near-VERTICAL ribbons (seaweed) where WorldUpAligned would go degenerate and ParallelTransport would let the ribbon spin/flicker around its length.")]
    public FrameMode frameMode = FrameMode.ParallelTransport;
    [Tooltip("World-space wide-axis reference for FixedReference mode. Should not run parallel to the tube's length (e.g. for an upright blade pick a HORIZONTAL axis perpendicular to the blade's sway direction).")]
    public Vector3 referenceAxis = Vector3.right;
    [Tooltip("Add flat round caps so the tube isn't open at the ends.")]
    public bool capEnds = true;

    [Header("Rim Ripple (skirt-hem flutter, 0 = off)")]
    [Tooltip("World-unit amplitude of a traveling wave that displaces the WIDE edges of a flat tube along its thin axis — the rim flutters faster than the bone wave, like a skirt hem. The center of each ring stays on the bones. 0 disables (default; the 3D scene never sets this).")]
    public float rippleAmplitude = 0f;
    [Tooltip("How many ripple waves fit along the tube's length.")]
    public float rippleCycles = 2.2f;
    [Tooltip("Ripple speed, waves per second (travels root → tip).")]
    public float rippleHz = 1.7f;
    [Tooltip("Concentrates the flutter at the wide rim: displacement scales with |cos(ring angle)|^bias, so higher = only the outermost edge moves.")]
    public float rippleEdgeBias = 2f;
    [Tooltip("true = the two wide edges curl OPPOSITE ways (rotational hem twist — the lever-arm look flat tubes had from rotation lag); false = the whole rim flaps together like a flag edge.")]
    public bool rippleCurl = true;
    [Tooltip("Phase offset in degrees, so paired tubes (two fluke lobes) don't flutter in sync.")]
    public float ripplePhaseDeg = 0f;

    Mesh mesh;
    Vector3[] vertsBuf;
    Vector2[] uvsBuf;
    int[] trisBuf;
    Vector3[] localPts;
    Vector3[] ringPts;
    float[] ringRad;
    Vector3[] tangents;

    void Awake()
    {
        EnsureMesh();
    }

    // Also recovers after editor domain reloads (the non-serialized mesh field goes null) —
    // needed by the 2D scene's edit-mode preview, which builds tubes outside play mode.
    void EnsureMesh()
    {
        if (mesh != null) return;
        mesh = new Mesh();
        mesh.name = "TubeMesh";
        mesh.MarkDynamic();
        GetComponent<MeshFilter>().sharedMesh = mesh;
        vertsBuf = null;   // force a full buffer rebuild into the fresh mesh
    }

    void LateUpdate()
    {
        EnsureMesh();
        if (points == null || points.Length < 2 || mesh == null) return;
        Build();
    }

    float RadiusAtPoint(int i)
    {
        return (radii != null && i < radii.Length)
            ? radii[i]
            : (radii != null && radii.Length > 0 ? radii[radii.Length - 1] : 0.1f);
    }

    static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t, t3 = t2 * t;
        return 0.5f * ((2f * p1)
            + (-p0 + p2) * t
            + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2
            + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
    }

    void Build()
    {
        int N = points.Length;
        int sub = Mathf.Max(1, subdivisions);
        int R = (N - 1) * sub + 1;              // ring count (== N when sub == 1)
        int S = Mathf.Max(3, sides);
        int totalVerts = R * S + (capEnds ? 2 : 0);
        int totalTriIdx = ((R - 1) * S * 2 + (capEnds ? S * 2 : 0)) * 3;

        if (vertsBuf == null || vertsBuf.Length != totalVerts)
        {
            vertsBuf = new Vector3[totalVerts];
            uvsBuf = new Vector2[totalVerts];
            trisBuf = new int[totalTriIdx];
        }
        if (localPts == null || localPts.Length != N)
            localPts = new Vector3[N];
        if (ringPts == null || ringPts.Length != R)
        {
            ringPts = new Vector3[R];
            ringRad = new float[R];
            tangents = new Vector3[R];
        }

        // 1) Convert control points to this object's local frame.
        for (int i = 0; i < N; i++)
        {
            if (points[i] == null) localPts[i] = (i > 0) ? localPts[i - 1] : Vector3.zero;
            else localPts[i] = transform.InverseTransformPoint(points[i].position);
        }

        // 1b) Ring positions: the control points themselves, or a Catmull-Rom spline through
        //     them when subdivided (fine silhouette detail without extra bones).
        if (sub == 1)
        {
            for (int i = 0; i < N; i++) ringPts[i] = localPts[i];
        }
        else
        {
            int rIdx = 0;
            for (int i = 0; i < N - 1; i++)
            {
                Vector3 p0 = localPts[Mathf.Max(i - 1, 0)];
                Vector3 p1 = localPts[i];
                Vector3 p2 = localPts[i + 1];
                Vector3 p3 = localPts[Mathf.Min(i + 2, N - 1)];
                for (int k = 0; k < sub; k++)
                    ringPts[rIdx++] = CatmullRom(p0, p1, p2, p3, k / (float)sub);
            }
            ringPts[rIdx] = localPts[N - 1];
        }

        // 1c) Ring radii: dense profile when provided, else per-point radii (lerped between
        //     control points for subdivided rings).
        bool useProfile = radiusProfile != null && radiusProfile.Length >= 2;
        for (int j = 0; j < R; j++)
        {
            float tj = (R > 1) ? j / (float)(R - 1) : 0f;
            if (useProfile)
            {
                float x = tj * (radiusProfile.Length - 1);
                int i0 = Mathf.Clamp(Mathf.FloorToInt(x), 0, radiusProfile.Length - 1);
                int i1 = Mathf.Min(i0 + 1, radiusProfile.Length - 1);
                ringRad[j] = Mathf.Lerp(radiusProfile[i0], radiusProfile[i1], x - i0);
            }
            else if (sub == 1)
            {
                ringRad[j] = RadiusAtPoint(j);
            }
            else
            {
                float x = tj * (N - 1);
                int i0 = Mathf.Clamp(Mathf.FloorToInt(x), 0, N - 1);
                int i1 = Mathf.Min(i0 + 1, N - 1);
                ringRad[j] = Mathf.Lerp(RadiusAtPoint(i0), RadiusAtPoint(i1), x - i0);
            }
        }

        // 2) Tangents at each ring: midpoint direction for interior, edge direction at ends.
        //    Defensive against degenerate (zero) tangents — fall back to the previous good one.
        Vector3 lastGoodTangent = Vector3.forward;
        for (int i = 0; i < R; i++)
        {
            Vector3 t;
            if (i == 0)
            {
                Vector3 d = ringPts[1] - ringPts[0];
                t = (d.sqrMagnitude > 0.000001f) ? d.normalized : lastGoodTangent;
            }
            else if (i == R - 1)
            {
                Vector3 d = ringPts[i] - ringPts[i - 1];
                t = (d.sqrMagnitude > 0.000001f) ? d.normalized : lastGoodTangent;
            }
            else
            {
                Vector3 a = ringPts[i] - ringPts[i - 1];
                Vector3 b = ringPts[i + 1] - ringPts[i];
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

        // Rim ripple clock (realtime in edit mode, where Time.time is frozen but the 2D
        // scene's animated preview repaints continuously). Flutter scales with the LOCAL
        // radius (like a real hem: wide fabric flutters, the tapering tip barely moves) —
        // find the max radius so the widest ring gets the full amplitude.
        bool rippling = rippleAmplitude != 0f;
        float ripplePhase = 0f;
        float rippleMaxRad = 0f;
        if (rippling)
        {
            float clock = Application.isPlaying ? Time.time : Time.realtimeSinceStartup;
            ripplePhase = clock * rippleHz * 2f * Mathf.PI + ripplePhaseDeg * Mathf.Deg2Rad;
            for (int i = 0; i < R; i++)
                rippleMaxRad = Mathf.Max(rippleMaxRad, ringRad[i]);
            if (rippleMaxRad < 1e-5f) rippling = false;
        }

        int v = 0;
        for (int i = 0; i < R; i++)
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
            else if (frameMode == FrameMode.WorldUpAligned) // recompute r from world up at each ring.
            {
                r = Vector3.Cross(Vector3.up, t);
                if (r.sqrMagnitude < 0.0001f) r = Vector3.Cross(Vector3.right, t);
            }
            else // FixedReference: wide axis tracks a fixed world direction, re-orthogonalized below.
            {
                r = referenceAxis;
                if (r.sqrMagnitude < 1e-6f) r = Vector3.right;
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

            float radius = ringRad[i];

            float ar = aspectRatio;
            if (aspectRatios != null && aspectRatios.Length > 0)
            {
                float x = ((R > 1) ? i / (float)(R - 1) : 0f) * (N - 1);
                int i0 = Mathf.Clamp(Mathf.FloorToInt(x), 0, N - 1);
                int i1 = Mathf.Min(i0 + 1, N - 1);
                float a0 = (i0 < aspectRatios.Length) ? aspectRatios[i0] : aspectRatios[aspectRatios.Length - 1];
                float a1 = (i1 < aspectRatios.Length) ? aspectRatios[i1] : aspectRatios[aspectRatios.Length - 1];
                ar = Mathf.Lerp(a0, a1, x - i0);
            }

            // Ripple wave for this ring: travels root → tip, faded to zero at the root so
            // the tube stays welded to its attachment, and scaled by the local radius so
            // the flutter peters out with the leaf taper instead of fattening the tip.
            float ringWave = 0f;
            if (rippling)
            {
                float along = (R > 1) ? i / (float)(R - 1) : 0f;
                ringWave = Mathf.Sin(along * rippleCycles * 2f * Mathf.PI - ripplePhase)
                         * rippleAmplitude * along * (radius / rippleMaxRad);
            }

            for (int k = 0; k < S; k++)
            {
                float angle = k * 2f * Mathf.PI / S;
                float cosA = Mathf.Cos(angle);
                Vector3 dir = cosA * r + Mathf.Sin(angle) * u * ar;
                Vector3 vert = ringPts[i] + dir * radius;
                if (rippling)
                {
                    // Edge weight: 1 at the wide rim (cos ±1), 0 at the thin faces. Signed
                    // (curl) rocks the two edges opposite ways — the rotational lever-arm
                    // flutter; unsigned flaps the whole rim together.
                    float edge = Mathf.Pow(Mathf.Abs(cosA), Mathf.Max(0.1f, rippleEdgeBias));
                    if (rippleCurl) edge *= Mathf.Sign(cosA);
                    vert += u * (ringWave * edge);
                }
                vertsBuf[v++] = vert;
            }
            prevTangent = t;
        }

        int startCapIdx = -1, endCapIdx = -1;
        if (capEnds)
        {
            startCapIdx = v;
            vertsBuf[v++] = ringPts[0];
            endCapIdx = v;
            vertsBuf[v++] = ringPts[R - 1];
        }

        // 4) Triangles — body quads.
        int t_idx = 0;
        for (int i = 0; i < R - 1; i++)
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
            // End cap (faces +tangent[R-1]).
            int endRow = (R - 1) * S;
            for (int k = 0; k < S; k++)
            {
                int kNext = (k + 1) % S;
                trisBuf[t_idx++] = endCapIdx;
                trisBuf[t_idx++] = endRow + k;
                trisBuf[t_idx++] = endRow + kNext;
            }
        }

        // Generate UVs: U around the ring [0..1), V along the tube length [0..1].
        // This lets shaders (e.g. Hair/Curly) reference position along and around the tube.
        int uvIdx = 0;
        for (int i = 0; i < R; i++)
        {
            float vCoord = (R > 1) ? (float)i / (R - 1) : 0f;
            for (int k = 0; k < S; k++)
            {
                float uCoord = (float)k / S;
                uvsBuf[uvIdx++] = new Vector2(uCoord, vCoord);
            }
        }
        if (capEnds)
        {
            uvsBuf[uvIdx++] = new Vector2(0.5f, 0f); // start cap center
            uvsBuf[uvIdx++] = new Vector2(0.5f, 1f); // end cap center
        }

        mesh.Clear();
        mesh.vertices = vertsBuf;
        mesh.uv = uvsBuf;
        mesh.triangles = trisBuf;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }
}
