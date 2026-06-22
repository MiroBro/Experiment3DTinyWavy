using UnityEngine;

/// <summary>
/// Holds the mermaid's foraging haul and draws a small counter panel, styled to match the
/// existing Sparkles UI and tucked just below it. Populated by <see cref="MermaidForager"/>.
/// </summary>
public class GemInventory : MonoBehaviour
{
    [Header("Read-only at runtime")]
    public int gems;
    public int rocks;

    public Color gemColor = new Color(0.45f, 0.85f, 1f);
    public Color rockColor = new Color(0.62f, 0.60f, 0.55f);

    GUIStyle headerStyle;
    GUIStyle countStyle;
    GUIStyle boxStyle;
    Texture2D swatchTex;

    public void AddGem() { gems++; }
    public void AddRock() { rocks++; }

    void EnsureStyles()
    {
        if (countStyle != null) return;

        headerStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };
        headerStyle.normal.textColor = new Color(0.7f, 0.9f, 1f);

        countStyle = new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Bold };
        countStyle.normal.textColor = Color.white;

        boxStyle = new GUIStyle(GUI.skin.box);
        var bg = new Texture2D(1, 1);
        bg.SetPixel(0, 0, new Color(0.05f, 0.10f, 0.20f, 0.78f));
        bg.Apply();
        boxStyle.normal.background = bg;

        swatchTex = new Texture2D(1, 1);
        swatchTex.SetPixel(0, 0, Color.white);
        swatchTex.Apply();
    }

    void OnGUI()
    {
        EnsureStyles();

        // Sits below the Sparkles box (which is 20,20 .. 380,255).
        const float X = 20f, Y = 270f, W = 360f, H = 110f;
        GUI.Box(new Rect(X, Y, W, H), GUIContent.none, boxStyle);
        GUI.Label(new Rect(X + 20, Y + 10, W - 30, 25), "Treasure", headerStyle);

        DrawEntry(X + 24, Y + 45, gemColor, gems);
        DrawEntry(X + 190, Y + 45, rockColor, rocks);
    }

    void DrawEntry(float x, float y, Color swatch, int count)
    {
        var prev = GUI.color;
        GUI.color = swatch;
        GUI.DrawTexture(new Rect(x, y + 6, 30, 30), swatchTex);
        GUI.color = prev;
        GUI.Label(new Rect(x + 42, y, 110, 44), count.ToString(), countStyle);
    }
}
