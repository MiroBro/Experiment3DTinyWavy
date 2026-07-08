using UnityEngine;

/// <summary>
/// Sits on the root of the edit-mode preview world. The preview is never saved, but a
/// DontSave object can survive the transition into play mode — so this marker kills its
/// own hierarchy the instant play mode starts, before anything can render or tick.
/// (The bootstrap also sweeps for these as a second line of defense.)
/// </summary>
[DefaultExecutionOrder(-200)]
public class Mermaid2DPreviewMarker : MonoBehaviour
{
    void Awake()
    {
        if (Application.isPlaying)
        {
            gameObject.SetActive(false);   // vanish this frame
            Destroy(gameObject);
        }
    }

    // Second net: with Enter Play Mode Options (no scene reload) Awake is never re-called
    // on play, so the preview would survive and masquerade as the real (badly tuned!)
    // mermaid. Update always runs in play mode.
    void Update()
    {
        if (Application.isPlaying)
        {
            gameObject.SetActive(false);
            Destroy(gameObject);
        }
    }
}
