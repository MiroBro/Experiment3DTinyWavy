using UnityEngine;

public class LinkVisual : MonoBehaviour
{
    public Transform a;
    public Transform b;
    [Tooltip("Cylinder radius in world units (assuming this object's parent has uniform scale 1).")]
    public float radius = 0.1f;

    void LateUpdate()
    {
        if (a == null || b == null) return;
        Vector3 ab = b.position - a.position;
        float len = ab.magnitude;
        if (len < 0.0001f) return;

        transform.position = (a.position + b.position) * 0.5f;
        transform.rotation = Quaternion.FromToRotation(Vector3.up, ab / len);
        transform.localScale = new Vector3(radius * 2f, len * 0.5f, radius * 2f);
    }
}
