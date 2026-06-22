using UnityEngine;

/// <summary>
/// Subtle, slow "underwater billow" — gently breathes the scale and rocks the rotation of
/// whatever it's on (used for the hair-volume blob so it drifts softly without obscuring the
/// face/body). Layered sines at incommensurate rates so it never looks like a loop.
/// </summary>
public class GentleBillow : MonoBehaviour
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
            Mathf.Sin(t * 1.1f), Mathf.Sin(t * 0.9f + 1f), Mathf.Sin(t * 0.7f + 2f)) * scaleAmp;
        transform.localRotation = baseRot * Quaternion.Euler(
            Mathf.Sin(t * 0.8f) * rotAmp, Mathf.Sin(t * 0.6f + 1f) * rotAmp, 0f);
    }
}
