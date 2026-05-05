using UnityEngine;
using System.Collections.Generic;
using System.Text;

[DefaultExecutionOrder(-50)]
public class MermaidBoneChain : MonoBehaviour
{
    [Tooltip("Bones must be ordered parents-before-children. Each bone's anchor should appear earlier in this list (or be the driver / not in the list).")]
    public List<MermaidBone> bones = new List<MermaidBone>();

    [Tooltip("Optional reference to the chain driver (e.g., the Head). Used in debug logging only.")]
    public Transform driver;

    [Header("Debug")]
    [Tooltip("Periodically logs the chain state. Wave is working if y values differ across bones at any single timestamp — Head should oscillate fastest, Hand slowest.")]
    public bool debugLog = false;
    public float debugLogInterval = 0.4f;
    float nextLogTime;

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

        if (debugLog && Time.time >= nextLogTime)
        {
            nextLogTime = Time.time + debugLogInterval;
            DumpState();
        }
    }

    void DumpState()
    {
        var sb = new StringBuilder("[Mermaid t=");
        sb.Append(Time.time.ToString("F2")).Append("s] ");
        sb.Append("groupRot=").Append(transform.rotation.eulerAngles.ToString("F1")).Append(" | ");
        if (driver != null)
        {
            sb.Append("DRIVER ").Append(driver.name)
              .Append(" y=").Append(driver.position.y.ToString("F3"))
              .Append(" pitch=").Append(driver.rotation.eulerAngles.x.ToString("F1"))
              .Append(" | ");
        }
        for (int i = 0; i < bones.Count; i++)
        {
            var b = bones[i];
            if (b == null) continue;
            sb.Append(b.name)
              .Append(" y=").Append(b.transform.position.y.ToString("F3"))
              .Append(" st=").Append(b.smoothTime.ToString("F2"))
              .Append(" | ");
        }
        Debug.Log(sb.ToString());
    }
}
