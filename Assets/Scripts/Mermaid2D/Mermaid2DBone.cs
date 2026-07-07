using UnityEngine;

/// <summary>
/// 2D port of <see cref="MermaidBone"/>: a lagged follower constrained to the XY plane.
/// Its anchor's world transform drives the ideal pose; SmoothDamp lag accumulates along a
/// chain to produce the traveling-wave look. Supports a symmetric bend cone, rest-distance
/// enforcement (no rubber-banding), circle avoidance (hair vs body) and a runtime reach
/// offset (forager dips the hands into the grass).
/// </summary>
public class Mermaid2DBone : MonoBehaviour
{
    [Tooltip("The bone this one follows. Its world transform drives the ideal pose for this bone.")]
    public Transform anchor;

    [Tooltip("Higher = more lag behind anchor. The lag accumulates along a chain to produce the traveling-wave look. Use 0 for rigid follow.")]
    public float smoothTime = 0.08f;

    [Tooltip("Bend cone: if > 0, the direction (anchor -> this bone) is clamped to within this many degrees of the rest direction.")]
    public float maxBendAngleDeg = 0f;

    [Tooltip("If true, the bone is forced to stay at its rest distance from the anchor (no rubber-band stretching).")]
    public bool enforceRestDistance = true;

    /// <summary>
    /// Optional circle colliders this bone is pushed out of after its position is computed.
    /// Shared arrays assigned at runtime (hair bones pointing at the body parts).
    /// </summary>
    [System.NonSerialized] public Transform[] avoidanceCircles;
    [System.NonSerialized] public float[] avoidanceRadii;

    /// <summary>
    /// Optional world-space offset added to this bone's ideal target each frame. Driven at
    /// runtime by <see cref="Mermaid2DForager"/> to make a hand/elbow reach toward the grass.
    /// The bend cone re-centres on the shifted target and the bone smooth-damps over.
    /// </summary>
    [System.NonSerialized] public Vector2 reachOffsetWorld;

    Vector3 localOffset;
    Quaternion localRotOffset;
    Vector3 vel;
    bool initialized;

    /// <summary>
    /// Skip the standard "capture from current pose" Initialize and provide explicit rest
    /// offsets directly. Used when a bone is built (or rebuilt mid-play) while the anchor is
    /// not at its rest pose.
    /// </summary>
    public void InitializeWithExplicitOffset(Vector3 explicitLocalOffset, Quaternion explicitLocalRotOffset)
    {
        localOffset = explicitLocalOffset;
        localRotOffset = explicitLocalRotOffset;
        vel = Vector3.zero;
        initialized = true;
    }

    /// <summary>
    /// Snapshot the rest offset from the anchor. Call after both bones are positioned in their rest pose.
    /// </summary>
    public void Initialize()
    {
        if (initialized) return;
        initialized = true;
        if (anchor == null) return;
        localOffset = anchor.InverseTransformPoint(transform.position);
        localRotOffset = Quaternion.Inverse(anchor.rotation) * transform.rotation;
        vel = Vector3.zero;
    }

    static Vector2 Rotate(Vector2 v, float degrees)
    {
        float rad = degrees * Mathf.Deg2Rad;
        float c = Mathf.Cos(rad), s = Mathf.Sin(rad);
        return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
    }

    public void Tick(float dt)
    {
        if (!initialized) Initialize();
        if (anchor == null) return;

        Vector3 anchorPos = anchor.position; anchorPos.z = 0f;

        Vector3 naturalIdeal = anchor.TransformPoint(localOffset); naturalIdeal.z = 0f;
        // Reach shifts the AIM target (so the bone rotates toward the reach pose), but the
        // rest LENGTH enforced below stays the natural bone length — the limb reorients to
        // reach WITHOUT stretching.
        Vector3 idealPos = naturalIdeal + (Vector3)reachOffsetWorld;

        Vector3 newPos;
        if (smoothTime <= Mathf.Epsilon)
            newPos = idealPos;
        else
            newPos = Vector3.SmoothDamp(transform.position, idealPos, ref vel, smoothTime, Mathf.Infinity, dt);
        newPos.z = 0f;

        // Symmetric bend cone: clamp the in-plane angle between (anchor -> bone) and the
        // rest direction (anchor -> ideal).
        Vector2 currentDirVec = (Vector2)(newPos - anchorPos);
        Vector2 restDirVec = (Vector2)(idealPos - anchorPos);
        float currentLen = currentDirVec.magnitude;
        float restDirLen = restDirVec.magnitude;
        if (maxBendAngleDeg > 0f && maxBendAngleDeg < 180f && currentLen > 0.0001f && restDirLen > 0.0001f)
        {
            float angle = Vector2.SignedAngle(restDirVec, currentDirVec);
            if (Mathf.Abs(angle) > maxBendAngleDeg)
            {
                Vector2 clampedDir = Rotate(restDirVec / restDirLen, Mathf.Sign(angle) * maxBendAngleDeg);
                newPos = anchorPos + (Vector3)(clampedDir * currentLen);
                vel = Vector3.zero;
            }
        }

        // Enforce rest distance to prevent rubber-band stretching (natural length, not the
        // reach-shifted one).
        if (enforceRestDistance)
        {
            Vector3 dirFromAnchor = newPos - anchorPos;
            float restLen = ((Vector2)(naturalIdeal - anchorPos)).magnitude;
            float len = dirFromAnchor.magnitude;
            if (len > 0.0001f && restLen > 0.0001f)
                newPos = anchorPos + (dirFromAnchor / len) * restLen;
        }

        // Circle avoidance: push the bone out of any body circle it has penetrated. Done
        // last so hair brushes along the body instead of clipping through it.
        if (avoidanceCircles != null && avoidanceRadii != null)
        {
            int n = Mathf.Min(avoidanceCircles.Length, avoidanceRadii.Length);
            for (int c = 0; c < n; c++)
            {
                var col = avoidanceCircles[c];
                if (col == null) continue;
                float r = avoidanceRadii[c];
                if (r <= 0f) continue;
                Vector2 toBone = (Vector2)(newPos - col.position);
                float distSq = toBone.sqrMagnitude;
                if (distSq < r * r && distSq > 0.000001f)
                {
                    float dist = Mathf.Sqrt(distSq);
                    Vector2 pushed = (Vector2)col.position + toBone * (r / dist);
                    newPos = new Vector3(pushed.x, pushed.y, 0f);
                }
            }
        }

        newPos.z = 0f;
        transform.position = newPos;

        Quaternion idealRot = anchor.rotation * localRotOffset;
        if (smoothTime <= Mathf.Epsilon)
            transform.rotation = idealRot;
        else
        {
            float lerp = 1f - Mathf.Exp(-(1f / Mathf.Max(0.01f, smoothTime)) * dt);
            transform.rotation = Quaternion.Slerp(transform.rotation, idealRot, lerp);
        }
    }
}
