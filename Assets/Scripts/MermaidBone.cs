using UnityEngine;

public class MermaidBone : MonoBehaviour
{
    [Tooltip("The bone this one follows. Its world transform drives the ideal pose for this bone.")]
    public Transform anchor;

    [Tooltip("Higher = more lag behind anchor. The lag accumulates along a chain to produce the traveling-wave look. Use 0 for rigid follow.")]
    public float smoothTime = 0.08f;

    [Tooltip("Bend cone: if > 0, the direction (anchor -> this bone) is clamped to within this many degrees of the rest direction. SYMMETRIC if bendReferenceAnchor is null. ASYMMETRIC if bendReferenceAnchor is set: tight in the backward/sideways directions, but opens up freely in the natural fold direction.")]
    public float maxBendAngleDeg = 0f;

    [Tooltip("If true, the bone is forced to stay at its rest distance from the anchor (no rubber-band stretching). Recommended on for arm/leg/spine — real bones don't stretch.")]
    public bool enforceRestDistance = true;

    [Tooltip("Optional secondary anchor — the anchor's anchor — used to define the natural fold direction. For an arm-elbow-hand chain set this on the HAND to the SHOULDER. When set, the cone above becomes asymmetric: tight in backward/sideways directions, fully open in the natural fold direction.")]
    public Transform bendReferenceAnchor;

    [Header("Debug")]
    [Tooltip("Logs to Console whenever a constraint fires (cone clamp or hyperextension clamp). Use this to verify the constraint is doing anything.")]
    public bool debugLog = false;
    [Tooltip("Read-only at runtime: the current dot product (lower segment direction · natural fold normal). Positive = on natural-fold side. Negative = hyperextended.")]
    public float currentDotN;
    [Tooltip("Read-only at runtime: the current angular deviation (degrees) from the rest direction. The symmetric cone clamps when this exceeds maxBendAngleDeg.")]
    public float currentDeviationDeg;

    Vector3 localOffset;
    Quaternion localRotOffset;
    Vector3 vel;
    bool initialized;

    // Asymmetric fold data
    Vector3 restFoldNormal_anchorLocal;
    bool hasFoldData;

    /// <summary>
    /// Skip the standard "capture from current pose" Initialize and provide explicit
    /// rest offsets directly. Useful when a bone is rebuilt mid-play and the anchor
    /// is no longer at its rest pose (so the standard Initialize would capture wrong
    /// values).
    /// </summary>
    public void InitializeWithExplicitOffset(Vector3 explicitLocalOffset, Quaternion explicitLocalRotOffset)
    {
        localOffset = explicitLocalOffset;
        localRotOffset = explicitLocalRotOffset;
        vel = Vector3.zero;
        hasFoldData = false;
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

        // Asymmetric fold: capture the natural fold direction at rest. We define the
        // fold normal as the perpendicular component of (anchor -> this) relative to
        // (bendReferenceAnchor -> anchor), and store it in the anchor's local frame so
        // it rotates with the anchor at runtime.
        hasFoldData = false;
        if (bendReferenceAnchor != null)
        {
            Vector3 upperDir = anchor.position - bendReferenceAnchor.position;
            Vector3 lowerDir = transform.position - anchor.position;
            float upperLen = upperDir.magnitude;
            float lowerLen = lowerDir.magnitude;
            if (upperLen > 0.0001f && lowerLen > 0.0001f)
            {
                Vector3 U = upperDir / upperLen;
                Vector3 L = lowerDir / lowerLen;
                Vector3 perp = L - Vector3.Dot(L, U) * U;
                if (perp.sqrMagnitude > 0.0001f)
                {
                    Vector3 foldNormalWorld = perp.normalized;
                    restFoldNormal_anchorLocal = anchor.InverseTransformDirection(foldNormalWorld);
                    hasFoldData = true;
                }
            }
        }
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

        // Bend cone — symmetric by default, asymmetric (open in natural-fold direction)
        // when bendReferenceAnchor + fold data are present.
        Vector3 currentDirVec = newPos - anchor.position;
        Vector3 restDirVec = idealPos - anchor.position;
        float currentLen_cone = currentDirVec.magnitude;
        float restLen_cone = restDirVec.magnitude;
        if (currentLen_cone > 0.0001f && restLen_cone > 0.0001f)
        {
            Vector3 cN = currentDirVec / currentLen_cone;
            Vector3 rN = restDirVec / restLen_cone;
            float angle = Vector3.Angle(cN, rN);
            currentDeviationDeg = angle;

            // Optionally compute dotN for inspection only.
            if (hasFoldData && bendReferenceAnchor != null)
            {
                Vector3 N_for_dot = anchor.TransformDirection(restFoldNormal_anchorLocal);
                if (N_for_dot.sqrMagnitude > 0.0001f)
                    currentDotN = Vector3.Dot(cN, N_for_dot.normalized);
            }

            if (maxBendAngleDeg > 0f && maxBendAngleDeg < 180f)
            {
                // Compute the effective cone limit: open it up if the deviation is in the
                // natural fold direction (+N).
                float effectiveMax = maxBendAngleDeg;
                if (hasFoldData && bendReferenceAnchor != null && angle > 0.01f)
                {
                    Vector3 devDir = cN - Vector3.Dot(cN, rN) * rN;
                    if (devDir.sqrMagnitude > 0.0001f)
                    {
                        devDir.Normalize();
                        Vector3 N_world = anchor.TransformDirection(restFoldNormal_anchorLocal);
                        if (N_world.sqrMagnitude > 0.0001f)
                        {
                            N_world.Normalize();
                            float devDotN = Vector3.Dot(devDir, N_world);
                            if (devDotN > 0f)
                            {
                                // Deviation is in natural fold direction: relax up to 180°.
                                effectiveMax = Mathf.Lerp(maxBendAngleDeg, 180f, devDotN);
                            }
                        }
                    }
                }

                if (angle > effectiveMax)
                {
                    float t = effectiveMax / Mathf.Max(angle, 0.0001f);
                    Vector3 clampedN = Vector3.Slerp(rN, cN, t).normalized;
                    newPos = anchor.position + clampedN * currentLen_cone;
                    vel = Vector3.zero;
                    if (debugLog) Debug.Log($"[{name}] cone clamp: angle={angle:F1}° > effMax={effectiveMax:F1}° (base={maxBendAngleDeg:F1}°)");
                }
            }
        }

        // Enforce rest distance to prevent rubber-band stretching.
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
