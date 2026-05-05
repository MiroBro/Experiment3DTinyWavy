using UnityEngine;

public class MermaidBone : MonoBehaviour
{
    [Tooltip("The bone this one follows. Its world transform drives the ideal pose for this bone.")]
    public Transform anchor;

    [Tooltip("Higher = more lag behind anchor. The lag accumulates along a chain to produce the traveling-wave look. Use 0 for rigid follow.")]
    public float smoothTime = 0.08f;

    [Tooltip("If > 0, the direction (this bone -> anchor) is clamped to a cone of this many degrees around the rest direction. Use this to prevent joints from bending backwards. 0 = no constraint.")]
    public float maxBendAngleDeg = 0f;

    [Tooltip("If true, the bone is forced to stay at its rest distance from the anchor (no rubber-band stretching). Recommended on for arm/leg/spine — real bones don't stretch.")]
    public bool enforceRestDistance = true;

    Vector3 localOffset;
    Quaternion localRotOffset;
    Vector3 vel;
    bool initialized;

    /// <summary>
    /// Snapshot the rest offset from the anchor. Call after both bones are positioned in their rest pose.
    /// </summary>
    public void Initialize()
    {
        if (anchor == null) { initialized = true; return; }
        localOffset = anchor.InverseTransformPoint(transform.position);
        localRotOffset = Quaternion.Inverse(anchor.rotation) * transform.rotation;
        vel = Vector3.zero;
        initialized = true;
    }

    public void Tick(float dt)
    {
        if (!initialized) Initialize();
        if (anchor == null) return;

        Vector3 idealPos = anchor.TransformPoint(localOffset);

        Vector3 newPos;
        if (smoothTime <= Mathf.Epsilon)
            newPos = idealPos;
        else
            newPos = Vector3.SmoothDamp(transform.position, idealPos, ref vel, smoothTime, Mathf.Infinity, dt);

        // Bend cone: clamp the deviation of (anchor->newPos) from (anchor->idealPos)
        // to a cone of maxBendAngleDeg. Prevents joints from flopping past their rest
        // direction in unnatural ways.
        if (maxBendAngleDeg > 0f)
        {
            Vector3 currentDir = newPos - anchor.position;
            Vector3 restDir = idealPos - anchor.position;
            float currentLen = currentDir.magnitude;
            float restLen = restDir.magnitude;
            if (currentLen > 0.0001f && restLen > 0.0001f)
            {
                Vector3 currentN = currentDir / currentLen;
                Vector3 restN = restDir / restLen;
                float angle = Vector3.Angle(currentN, restN);
                if (angle > maxBendAngleDeg)
                {
                    float t = maxBendAngleDeg / Mathf.Max(angle, 0.0001f);
                    Vector3 clampedN = Vector3.Slerp(restN, currentN, t).normalized;
                    newPos = anchor.position + clampedN * currentLen;
                    // Kill velocity component pushing past the cone so we don't fight the constraint.
                    vel = Vector3.zero;
                }
            }
        }

        // Enforce rest distance: clamp the magnitude of (newPos - anchor.position) to
        // exactly the rest distance. Without this the SmoothDamp lag can place the bone
        // far from the anchor during fast motion, making the connecting cylinder appear
        // to stretch like rubber.
        if (enforceRestDistance)
        {
            Vector3 dirFromAnchor = newPos - anchor.position;
            float restLen = (idealPos - anchor.position).magnitude;
            float currentLen = dirFromAnchor.magnitude;
            if (currentLen > 0.0001f && restLen > 0.0001f)
            {
                newPos = anchor.position + (dirFromAnchor / currentLen) * restLen;
            }
        }

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
