using UnityEngine;

/// <summary>
/// 2D port of <see cref="CollectedItem"/>: a little gem or rock that pops out of the grass
/// when the mermaid finds it — scales up, drifts up toward her hand, spins, then fades and
/// self-destroys. Purely visual; the inventory count is incremented by the forager at spawn.
///
/// Gems are faceted diamond polygons with a bright glint gradient; rocks are lumpy blobs —
/// or, if a custom sprite is passed in, that sprite (untinted, so your art shows as-drawn).
/// </summary>
public class CollectedItem2D : MonoBehaviour
{
    public const int SortingOrder = 12;

    Transform follow;        // optional: a hand to drift toward
    Vector3 startPos;
    Vector3 baseScale;
    float life, maxLife;
    float spinDeg;
    Material mat;            // procedural-mesh path: fade via material alpha
    SpriteRenderer spriteRend; // custom-sprite path: fade via sprite color alpha

    public static CollectedItem2D Spawn(Vector3 worldPos, Transform followTarget, bool isGem,
        Color color, Sprite customSprite = null)
    {
        var go = new GameObject(isGem ? "Gem2D" : "Rock2D");
        worldPos.z = 0f;
        go.transform.position = worldPos;

        var item = go.AddComponent<CollectedItem2D>();

        if (customSprite != null)
        {
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = customSprite;
            sr.sortingOrder = SortingOrder;
            item.spriteRend = sr;
            // Auto-fit the sprite to the same world height the procedural shapes use.
            float targetH = isGem ? 0.38f : 0.28f;
            float s = targetH / Mathf.Max(0.0001f, customSprite.bounds.size.y);
            item.baseScale = new Vector3(s, s, 1f);
        }
        else
        {
            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            mf.sharedMesh = isGem ? BuildGemMesh(color) : BuildRockMesh(color);
            item.mat = new Material(Shader.Find("Sprites/Default")) { color = Color.white };
            mr.sharedMaterial = item.mat;
            mr.sortingOrder = SortingOrder;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            item.baseScale = isGem ? new Vector3(0.14f, 0.20f, 1f) : new Vector3(0.17f, 0.14f, 1f);
        }

        item.follow = followTarget;
        item.startPos = worldPos;
        go.transform.localScale = item.baseScale;
        go.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(-25f, 25f));
        item.maxLife = 1.1f;
        item.life = item.maxLife;
        item.spinDeg = Random.Range(120f, 260f) * (Random.value < 0.5f ? -1f : 1f);
        return item;
    }

    // Faceted diamond: bright at the crown, deep at the pavilion, with a pale glint center.
    static Mesh BuildGemMesh(Color c)
    {
        Vector3[] rim =
        {
            new Vector3(0f, 0.95f, 0f),
            new Vector3(0.55f, 0.30f, 0f),
            new Vector3(0.36f, -0.72f, 0f),
            new Vector3(0f, -1f, 0f),
            new Vector3(-0.36f, -0.72f, 0f),
            new Vector3(-0.55f, 0.30f, 0f),
        };
        var verts = new Vector3[rim.Length + 1];
        var cols = new Color[rim.Length + 1];
        verts[0] = Vector3.zero;
        cols[0] = Color.Lerp(c, Color.white, 0.55f);
        for (int i = 0; i < rim.Length; i++)
        {
            verts[i + 1] = rim[i];
            float bright = Mathf.InverseLerp(-1f, 0.95f, rim[i].y);
            cols[i + 1] = Color.Lerp(c * 0.55f, Color.Lerp(c, Color.white, 0.25f), bright);
            cols[i + 1].a = 1f;
        }
        return FanMesh("GemMesh2D", verts, cols);
    }

    // Lumpy blob with a hint of top light.
    static Mesh BuildRockMesh(Color c)
    {
        const int n = 9;
        var verts = new Vector3[n + 1];
        var cols = new Color[n + 1];
        verts[0] = Vector3.zero;
        cols[0] = c;
        for (int i = 0; i < n; i++)
        {
            float ang = i * 2f * Mathf.PI / n;
            float r = Random.Range(0.72f, 1.05f);
            verts[i + 1] = new Vector3(Mathf.Cos(ang) * r, Mathf.Sin(ang) * r, 0f);
            float bright = 0.85f + 0.3f * Mathf.Max(0f, Mathf.Sin(ang));
            cols[i + 1] = c * bright;
            cols[i + 1].a = 1f;
        }
        return FanMesh("RockMesh2D", verts, cols);
    }

    static Mesh FanMesh(string name, Vector3[] verts, Color[] cols)
    {
        int rim = verts.Length - 1;
        var tris = new int[rim * 3];
        int t = 0;
        for (int i = 0; i < rim; i++)
        {
            tris[t++] = 0;
            tris[t++] = 1 + i;
            tris[t++] = 1 + (i + 1) % rim;
        }
        var m = new Mesh { name = name };
        m.vertices = verts;
        m.colors = cols;
        m.triangles = tris;
        m.RecalculateBounds();
        return m;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        life -= dt;
        if (life <= 0f) { Destroy(gameObject); return; }

        float aliveFrac = 1f - life / maxLife;   // 0 -> 1 over its lifetime

        // Drift: rise out of the grass, easing toward the hand if we have one.
        Vector3 target = (follow != null) ? follow.position + Vector3.up * 0.05f
                                          : startPos + Vector3.up * 0.5f;
        target.z = 0f;
        transform.position = Vector3.Lerp(startPos, target, Mathf.SmoothStep(0f, 1f, aliveFrac))
                             + Vector3.up * (aliveFrac * 0.15f);

        transform.Rotate(0f, 0f, spinDeg * dt);

        // Pop in fast, hold, then shrink/fade out at the end.
        float popIn = Mathf.Clamp01(aliveFrac / 0.18f);
        float fadeOut = (aliveFrac < 0.7f) ? 1f : Mathf.Lerp(1f, 0f, (aliveFrac - 0.7f) / 0.3f);
        transform.localScale = baseScale * (0.2f + 0.8f * popIn) * Mathf.Max(0.001f, fadeOut);
        if (spriteRend != null)
        {
            var sc = spriteRend.color;
            sc.a = fadeOut;
            spriteRend.color = sc;
        }
        else if (mat != null)
        {
            var mc = mat.color;
            mc.a = fadeOut;
            mat.color = mc;
        }
    }

    void OnDestroy()
    {
        if (mat != null) Destroy(mat);
        var mf = GetComponent<MeshFilter>();
        if (mf != null && mf.sharedMesh != null) Destroy(mf.sharedMesh);
    }
}
