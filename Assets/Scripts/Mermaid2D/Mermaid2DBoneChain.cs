using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 2D port of <see cref="MermaidBoneChain"/>: ticks every bone in order each LateUpdate.
/// Bones must be ordered parents-before-children (each bone's anchor appears earlier in the
/// list, or is the driver / not in the list).
/// </summary>
[DefaultExecutionOrder(-50)]
public class Mermaid2DBoneChain : MonoBehaviour
{
    [Tooltip("Bones must be ordered parents-before-children.")]
    public List<Mermaid2DBone> bones = new List<Mermaid2DBone>();

    [Tooltip("Optional reference to the chain driver (e.g., the Head). For inspection only.")]
    public Transform driver;

    public void Initialize()
    {
        for (int i = 0; i < bones.Count; i++)
            if (bones[i] != null) bones[i].Initialize();
    }

    void LateUpdate()
    {
        float dt = Time.deltaTime;
        for (int i = 0; i < bones.Count; i++)
            if (bones[i] != null) bones[i].Tick(dt);
    }
}
