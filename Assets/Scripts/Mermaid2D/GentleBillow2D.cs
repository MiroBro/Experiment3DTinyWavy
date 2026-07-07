using UnityEngine;

/// <summary>
/// 2D port of <see cref="GentleBillow"/>: subtle, slow underwater billow — gently breathes
/// the scale and rocks the in-plane rotation of whatever it's on (the hair-volume blob).
/// Layered sines at incommensurate rates so it never looks like a loop.
/// </summary>
public class GentleBillow2D : MonoBehaviour
{
    public float scaleAmp = 0.04f;
    public float rotAmp = 5f;
    public float speed = 0.6f;

    Vector3 baseScale;
    Quaternion baseRot;
    float seed;

    void Start()
    {
        baseScale = transform.localScale;
        baseRot = transform.localRotation;
        seed = Random.value * 10f;
    }

    void Update()
    {
        float t = Time.time * speed + seed;
        transform.localScale = baseScale + new Vector3(
            Mathf.Sin(t * 1.1f), Mathf.Sin(t * 0.9f + 1f), 0f) * scaleAmp;
        transform.localRotation = baseRot * Quaternion.Euler(
            0f, 0f, Mathf.Sin(t * 0.8f) * rotAmp);
    }
}
