using UnityEngine;

/// <summary>
/// 2D analog of <see cref="TubeRenderer"/>/<see cref="MermaidBodyTube"/>: a flat ribbon mesh
/// in the XY plane, lofted along a Catmull-Rom spline through a set of live bone transforms,
/// with a per-length half-width curve and a start→end vertex-color gradient. Rebuilds every
/// LateUpdate from the bone positions, so it deforms with the rig exactly like the 3D tubes.
///
/// UVs are laid out so a painted texture reads exactly as drawn ON SCREEN for this rig
/// (parts extend right-to-left, she faces right): image X runs along the ribbon with
/// image LEFT (u=0) = the far tip and image RIGHT (u=1) = the attachment end (first
/// point); image Y spans the width. Paint what you see, drag the PNG in, done.
///
/// Performance: triangles and UVs are static per topology — only vertices (and colors, when
/// the gradient fields change) are re-uploaded each frame. Depth is handled purely by
/// MeshRenderer.sortingOrder.
/// </summary>
[ExecuteAlways]
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
    [Tooltip("Vertex tint at the first point. Multiplies the material/texture. White = untinted art.")]
    public Color colorStart = Color.white;
    [Tooltip("Vertex tint at the last point. Multiplies the material/texture. White = untinted art.")]
    public Color colorEnd = Color.white;
    [Tooltip("Add semicircular caps at both ends so the ribbon doesn't end in a hard chop.")]
    public bool roundCaps = true;
    [Range(2, 12)]
    public int capSegments = 5;
    [Tooltip("Extra half-width added uniformly along the whole ribbon (world units). Used by the outline system: an outline clone is the same ribbon with extraWidth = stroke width.")]
    public float extraWidth = 0f;

    [Header("3D Ribbon Twist (out-of-plane tilt illusion)")]
    [Tooltip("Rotate each cross-section around the centerline tangent so the ribbon tilts out of the screen plane. Under the 2D camera this foreshortens the width, and a traveling wave rolls a twist down the length — the flat texture reads as a 3D ribbon (the fluke trick).")]
    public bool twist3D = false;
    [Tooltip("Constant tilt out of the screen plane, degrees. 0 = flat to camera (full width), 90 = edge-on sliver. ~40 reads as a fin lying mostly horizontal.")]
    public float twistBaseDeg = 40f;
    [Tooltip("Traveling twist-wave amplitude, degrees on top of the base tilt.")]
    public float twistWaveDeg = 32f;
    [Tooltip("Twist-wave beat, Hz.")]
    public float twistWaveHz = 0.83f;
    [Tooltip("How many full wave cycles fit along the ribbon length. <1 = one gentle roll, higher = corkscrew.")]
    public float twistWaveCycles = 0.7f;
    [Tooltip("Phase offset, degrees — give paired ribbons (two fluke lobes) different offsets so they alternate instead of moving in lockstep.")]
    public float twistPhaseDeg = 0f;
    [Tooltip("Twist strength along the length: X 0 = first point (keep it 0 there so the ribbon stays welded flat to whatever it attaches to), X 1 = far tip.")]
    public AnimationCurve twistEnvelope = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [Tooltip("Fake-lighting strength: darkens the vertex color toward edge-on so the roll catches 'light'. 0 = geometry only (outline clones use 0).")]
    [Range(0f, 1f)]
    public float twistShade = 0.55f;
    [Tooltip("Extra tint multiplied in while the BACK face shows (twist past 90°) — slightly darker/cooler sells the ribbon flipping over.")]
    public Color twistBackTint = new Color(0.80f, 0.74f, 0.86f);
    [Tooltip("Fake 3/4-view shear: how much of the out-of-plane roll leans into a VISIBLE vertical offset. 0 = strict side view (the roll only pinches the width — reads flat); ~0.45 = the surface visibly leans and the edges undulate like a waving flag. Negative flips the apparent camera side.")]
    public float twistViewSkew = 0.45f;
    [Tooltip("Motion-driven roll: extra twist from the centerline's local bend, in twist-degrees per (degree of bend per world unit). The lagged bones bend when the tail whips, so rolls chase the actual motion instead of the clock. ~0.6; 0 = clock wave only.")]
    public float twistCurvatureGain = 0.6f;
    [Tooltip("OUTLINE-ONLY width floor (used when extraWidth > 0, i.e. on outline clones): the stroke's apparent cross-section never drops below this fraction of the untwisted width, and eases back to the flat orientation while held there — the ink stays a plump leaf while the art inside rolls edge-on. 0 = stroke pinches with the art.")]
    [Range(0f, 1f)]
    public float twistMinWidthFrac = 0.65f;
    [Tooltip("External per-frame multiplier on the WAVE amplitude only (not the base tilt) — the bootstrap feeds the swim liveliness in here so the ribbon calms when she stops.")]
    [System.NonSerialized] public float twistAnimScale = 1f;

    Mesh mesh;
    Vector3[] center;
    Vector3[] vertsBuf;
    Color[] colsBuf;
    Vector2[] uvsBuf;
    int[] trisBuf;
    bool topologyDirty;
    Color lastColorStart, lastColorEnd;

    void Awake()
    {
        EnsureMesh();
    }

    // Also recovers after editor domain reloads (non-serialized mesh field goes null).
    void EnsureMesh()
    {
        if (mesh != null) return;
        mesh = new Mesh { name = "Ribbon2D" };
        mesh.MarkDynamic();
        GetComponent<MeshFilter>().sharedMesh = mesh;
        vertsBuf = null;   // force a full topology rebuild into the fresh mesh
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
        EnsureMesh();
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
        int capVerts = roundCaps ? 2 * cap : 0;   // (cap-1 fan points + 1 fan center) per end
        int totalV = N * 2 + capVerts;
        int totalT = ((N - 1) * 2 + (roundCaps ? 2 * cap : 0)) * 3;
        if (vertsBuf == null || vertsBuf.Length != totalV)
        {
            vertsBuf = new Vector3[totalV];
            colsBuf = new Color[totalV];
            uvsBuf = new Vector2[totalV];
            trisBuf = new int[totalT];
            topologyDirty = true;
        }

        // A twisting ribbon re-shades its vertex colors every frame (facing changes).
        bool colorsDirty = topologyDirty || twist3D || colorStart != lastColorStart || colorEnd != lastColorEnd;

        // Twist phase clock. Time.time is frozen in edit mode, where the bootstrap's
        // animated preview repaints continuously — realtimeSinceStartup keeps the wave
        // rolling there too. Source ribbon and its outline clone both sample this within
        // the same frame, so their geometry stays in step.
        float clockPhase = 0f;
        if (twist3D)
        {
            float clock = Application.isPlaying ? Time.time : Time.realtimeSinceStartup;
            clockPhase = clock * twistWaveHz * 2f * Mathf.PI + twistPhaseDeg * Mathf.Deg2Rad;
        }

        // 2) Two verts per sample. Flat mode: offset along the in-plane normal. Twist mode:
        //    the cross-section rotates around the tangent by tw — the in-plane extent
        //    shrinks with cos(tw) (orthographic foreshortening) and the rest leaves the
        //    plane as z (invisible to the 2D camera, harmless for sorting which is purely
        //    sortingOrder). extraWidth (outline stroke) is added AFTER foreshortening so
        //    the ink stays a constant thickness around the narrowed silhouette.
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
            float baseHalf = Mathf.Max(0.0005f, widthCurve.Evaluate(t01) * widthScale);
            float extra = Mathf.Max(0f, extraWidth);

            float cw = 1f, sw = 0f;
            if (twist3D)
            {
                float tw = TwistAngle(t01, clockPhase);
                if (twistCurvatureGain != 0f && i > 0 && i < N - 1)
                {
                    // Motion-driven roll: local bend of the lagged centerline (degrees per
                    // world unit) feeds the twist, so rolls travel with the wave the bones
                    // are actually carrying instead of a fixed-clock sine.
                    Vector3 d0 = center[i] - center[i - 1];
                    Vector3 d1 = center[i + 1] - center[i];
                    float segLen = Mathf.Max(1e-4f, d1.magnitude);
                    float bendPerUnit = Vector2.SignedAngle(d0, d1) / segLen;
                    float env = twistEnvelope != null ? Mathf.Clamp01(twistEnvelope.Evaluate(t01)) : t01;
                    tw += Mathf.Clamp(bendPerUnit * twistCurvatureGain, -60f, 60f) * Mathf.Deg2Rad * env;
                }
                cw = Mathf.Cos(tw); sw = Mathf.Sin(tw);
            }
            // Sign of cw carries through so the texture mirror-flips when the back shows.
            Vector3 off = normal * (baseHalf * cw) + new Vector3(0f, 0f, baseHalf * sw);
            // Oblique 3/4-view: shear the out-of-plane part into visible Y. The two verts
            // get +off/−off, so the cross-section visibly LEANS on screen and a traveling
            // roll makes the edges undulate — the waving-flag/ribbon read the strict
            // side-on projection can't produce on its own.
            if (twist3D) off.y += off.z * twistViewSkew;
            // Outline stroke: extend the cross-section by `extra` along its own ON-SCREEN
            // direction. This must stay sign-continuous — a naive ±normal*extra flips sides
            // wherever the roll crosses edge-on, twisting the stroke quad into a bowtie
            // that pinches to nothing and flickers as the wave travels.
            if (extra > 0f)
            {
                if (twist3D)
                {
                    float lenScr = Mathf.Sqrt(off.x * off.x + off.y * off.y);
                    float floorHalf = baseHalf * Mathf.Clamp01(twistMinWidthFrac);
                    if (lenScr >= floorHalf)
                    {
                        if (lenScr > 1e-4f) off *= (lenScr + extra) / lenScr;
                        else off += normal * extra;   // degenerate sliver, no floor set
                    }
                    else
                    {
                        // Hold the stroke at the floor width, easing its direction back to
                        // the flat orientation, so the ink stays a plump leaf while the art
                        // inside rolls edge-on.
                        float t = floorHalf > 1e-6f ? lenScr / floorHalf : 0f;
                        Vector3 dir = new Vector3(off.x, off.y, 0f) + normal * (floorHalf * (1f - t));
                        float dl = dir.magnitude;
                        dir = dl > 1e-6f ? dir / dl : normal;
                        off = dir * (floorHalf + extra);
                    }
                }
                else off += normal * extra;
            }

            vertsBuf[i * 2] = center[i] + off;
            vertsBuf[i * 2 + 1] = center[i] - off;
            if (colorsDirty)
            {
                Color col = ShadeTwist(Color.Lerp(colorStart, colorEnd, t01), cw);
                colsBuf[i * 2] = col;
                colsBuf[i * 2 + 1] = col;
            }
            if (topologyDirty)
            {
                // u runs tip(0) → attachment(1); v: +normal side is the ribbon's lower
                // edge for right-to-left parts, so it takes the image's bottom (v=0).
                uvsBuf[i * 2] = new Vector2(1f - t01, 0f);
                uvsBuf[i * 2 + 1] = new Vector2(1f - t01, 1f);
            }
        }

        if (topologyDirty)
        {
            int ti0 = 0;
            for (int i = 0; i < N - 1; i++)
            {
                int r0 = i * 2, r1 = (i + 1) * 2;
                trisBuf[ti0++] = r0; trisBuf[ti0++] = r1; trisBuf[ti0++] = r0 + 1;
                trisBuf[ti0++] = r0 + 1; trisBuf[ti0++] = r1; trisBuf[ti0++] = r1 + 1;
            }
            // Cap triangle indices are laid out by BuildCap below (their layout is also
            // static per topology, but the vertex positions they reference move each frame).
        }

        // 3) Semicircular end caps: fan from +normal through -tangent (start) / +tangent
        //    (end) to -normal. Vertex positions update every frame; indices/uvs are stable.
        if (roundCaps)
        {
            int ti = (N - 1) * 6;
            int v = N * 2;
            float capExtra = Mathf.Max(0f, extraWidth);
            // Caps shrink and shade with the end twist, so a rolled-over tip keeps a
            // proportionate rounded end instead of a full-width lollipop.
            float cw0 = 1f, cw1 = 1f;
            if (twist3D)
            {
                cw0 = Mathf.Cos(TwistAngle(0f, clockPhase));
                cw1 = Mathf.Cos(TwistAngle(1f, clockPhase));
            }
            // Outline clones (capExtra > 0) hold the same apparent-width floor as the strip.
            float capFrac0 = Mathf.Abs(cw0), capFrac1 = Mathf.Abs(cw1);
            if (twist3D && capExtra > 0f)
            {
                capFrac0 = Mathf.Max(capFrac0, Mathf.Clamp01(twistMinWidthFrac));
                capFrac1 = Mathf.Max(capFrac1, Mathf.Clamp01(twistMinWidthFrac));
            }
            ti = BuildCap(v, 0, center[0], center[0] - center[1],
                widthCurve.Evaluate(0f) * widthScale * capFrac0 + capExtra,
                ShadeTwist(Color.Lerp(colorStart, colorEnd, 0f), cw0), 0f, cap, ti, colorsDirty);
            v += cap;
            BuildCap(v, (N - 1) * 2, center[N - 1], center[N - 1] - center[N - 2],
                widthCurve.Evaluate(1f) * widthScale * capFrac1 + capExtra,
                ShadeTwist(Color.Lerp(colorStart, colorEnd, 1f), cw1), 1f, cap, ti, colorsDirty);
        }

        // 4) Upload. Full re-upload only when topology changed; otherwise just positions
        //    (and colors when the gradient fields were edited).
        if (topologyDirty)
        {
            mesh.Clear();
            mesh.vertices = vertsBuf;
            mesh.uv = uvsBuf;
            mesh.colors = colsBuf;
            mesh.triangles = trisBuf;
            topologyDirty = false;
        }
        else
        {
            mesh.vertices = vertsBuf;
            if (colorsDirty) mesh.colors = colsBuf;
        }
        lastColorStart = colorStart;
        lastColorEnd = colorEnd;
        mesh.RecalculateBounds();
    }

    // Out-of-plane rotation of the cross-section at t01 along the ribbon: base tilt plus a
    // wave traveling first-point → tip, all scaled by the length envelope so the attachment
    // end stays flat/welded.
    float TwistAngle(float t01, float clockPhase)
    {
        float env = twistEnvelope != null ? Mathf.Clamp01(twistEnvelope.Evaluate(t01)) : t01;
        float wave = Mathf.Sin(clockPhase - t01 * twistWaveCycles * 2f * Mathf.PI);
        return (twistBaseDeg + wave * twistWaveDeg * twistAnimScale) * Mathf.Deg2Rad * env;
    }

    // Fake lighting for the twist: full-bright facing the camera, dark toward edge-on, and
    // the back face (cos < 0) additionally takes twistBackTint so the flip reads.
    Color ShadeTwist(Color col, float cosTw)
    {
        if (!twist3D || twistShade <= 0f) return col;
        float facing = Mathf.Abs(cosTw);
        float bright = Mathf.Lerp(1f, Mathf.Lerp(0.35f, 1f, facing), twistShade);
        if (cosTw < 0f)
        {
            col.r *= twistBackTint.r;
            col.g *= twistBackTint.g;
            col.b *= twistBackTint.b;
        }
        col.r *= bright; col.g *= bright; col.b *= bright;
        return col;
    }

    // Fan of `cap` triangles bulging in `outDir` between edge verts (edgeIdx, edgeIdx+1).
    int BuildCap(int vStart, int edgeIdx, Vector3 c, Vector3 outDir, float halfW, Color col,
        float vCoord, int cap, int ti, bool colorsDirty)
    {
        halfW = Mathf.Max(0.0005f, halfW);
        if (outDir.sqrMagnitude < 1e-10f) outDir = Vector3.left;
        outDir.Normalize();
        Vector3 n = vertsBuf[edgeIdx] - c;
        if (n.sqrMagnitude < 1e-10f) n = new Vector3(-outDir.y, outDir.x, 0f) * halfW;

        int centerV = vStart;
        vertsBuf[centerV] = c;
        if (colorsDirty) colsBuf[centerV] = col;
        uvsBuf[centerV] = new Vector2(1f - vCoord, 0.5f);

        int prev = edgeIdx;                     // starts at +normal edge vertex
        for (int k = 1; k < cap; k++)
        {
            float ang = Mathf.PI * k / cap;     // sweep +normal -> outDir -> -normal
            Vector3 dir = n * Mathf.Cos(ang) + outDir * (halfW * Mathf.Sin(ang));
            int idx = vStart + k;
            vertsBuf[idx] = c + dir;
            if (colorsDirty) colsBuf[idx] = col;
            uvsBuf[idx] = new Vector2(1f - vCoord, 0.5f);
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
