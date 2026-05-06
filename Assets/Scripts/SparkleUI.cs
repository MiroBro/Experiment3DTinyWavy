using UnityEngine;

public class SparkleUI : MonoBehaviour
{
    public SparkleSpawner spawner;

    [Tooltip("Multiplier applied to spawnInterval each time the 'Faster' button is clicked. <1 = faster.")]
    [Range(0.1f, 0.99f)]
    public float fasterMultiplier = 0.75f;
    [Tooltip("Multiplier applied to sparkleValue each time the 'Bigger' button is clicked.")]
    [Range(1.01f, 5f)]
    public float biggerMultiplier = 1.5f;

    GUIStyle counterStyle;
    GUIStyle headerStyle;
    GUIStyle labelStyle;
    GUIStyle buttonStyle;
    GUIStyle boxStyle;

    void EnsureStyles()
    {
        if (counterStyle != null) return;
        counterStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 56,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.UpperLeft
        };
        counterStyle.normal.textColor = new Color(1f, 0.95f, 0.7f);

        headerStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 18,
            fontStyle = FontStyle.Bold
        };
        headerStyle.normal.textColor = new Color(1f, 0.85f, 0.4f);

        labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 16
        };
        labelStyle.normal.textColor = Color.white;

        buttonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 16,
            fontStyle = FontStyle.Bold
        };

        boxStyle = new GUIStyle(GUI.skin.box);
        var bg = new Texture2D(1, 1);
        bg.SetPixel(0, 0, new Color(0.05f, 0.10f, 0.20f, 0.78f));
        bg.Apply();
        boxStyle.normal.background = bg;
    }

    void OnGUI()
    {
        if (spawner == null) return;
        EnsureStyles();

        const float W = 360f;
        const float H = 235f;

        GUI.Box(new Rect(20, 20, W, H), GUIContent.none, boxStyle);

        GUI.Label(new Rect(40, 30, W - 30, 80), FormatNumber(spawner.counter), counterStyle);
        GUI.Label(new Rect(40, 100, W - 30, 25), "Sparkles", headerStyle);

        float rate = 1f / Mathf.Max(0.001f, spawner.spawnInterval);
        GUI.Label(new Rect(40, 140, 200, 30), $"Rate:  {rate:F1}/s", labelStyle);
        if (GUI.Button(new Rect(245, 138, 115, 32), "Faster", buttonStyle))
        {
            spawner.spawnInterval = Mathf.Max(0.02f, spawner.spawnInterval * fasterMultiplier);
        }

        GUI.Label(new Rect(40, 187, 200, 30), $"Value: {FormatNumber(spawner.sparkleValue)}", labelStyle);
        if (GUI.Button(new Rect(245, 185, 115, 32), "Bigger", buttonStyle))
        {
            spawner.sparkleValue *= biggerMultiplier;
        }
    }

    static string FormatNumber(double n)
    {
        if (n < 1000) return n.ToString("F0");
        if (n < 1_000_000) return (n / 1_000d).ToString("F2") + "K";
        if (n < 1_000_000_000) return (n / 1_000_000d).ToString("F2") + "M";
        if (n < 1e12) return (n / 1_000_000_000d).ToString("F2") + "B";
        if (n < 1e15) return (n / 1e12).ToString("F2") + "T";
        return n.ToString("0.00e0");
    }
}
