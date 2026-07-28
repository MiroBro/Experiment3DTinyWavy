using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Fluke DESIGN presets for the true-3D fluke system: each style defines both the fin's
/// SHAPE (how many flaps per lobe, each flap's direction/length/floppiness, and a dense
/// silhouette profile — jagged, scalloped, split, streamers...) and its LOOK (a procedurally
/// generated pattern texture + toon palette). Classic is the original hand-tuned leaf and
/// keeps using the serialized flukeWidthCurve / toon colors, bit-identical to before.
/// The two lobes always share one design; the near lobe samples the pattern texture with
/// U mirrored, so it reads as the mirrored twin of the far lobe.
/// </summary>
public enum Fluke3DStyle
{
    Classic,        // the tuned leaf that shipped — serialized curve + toon colors
    TealCrescent,   // sickle blade, sharp point, uni-colored teal gradient
    CoralJag,       // rounded paddle with a sawtooth edge, coral/cream stripes
    BettaSplit,     // TWO flaps per lobe like a split betta tail, sparkly violet
    GoldfishFan,    // broad scalloped fan, fish-scale pattern
    SilkStreamers,  // THREE floppy ribbon streamers per lobe, iridescent silk
}

public static class FlukeStyles
{
    /// One flap = one bone chain + one tube. angleDeg splays the flap within the fin plane
    /// (multiplied by the lobe's side, so the two lobes mirror). lagScale multiplies the
    /// bone lag — higher = floppier, fabric-like. profile picks a Profile() shape (0 =
    /// "use the serialized flukeWidthCurve").
    public struct FlapDef
    {
        public float angleDeg;
        public float lengthScale;
        public float widthScale;
        public float lagScale;
        public int profile;

        public FlapDef(float angleDeg, float lengthScale, float widthScale, float lagScale, int profile)
        {
            this.angleDeg = angleDeg;
            this.lengthScale = lengthScale;
            this.widthScale = widthScale;
            this.lagScale = lagScale;
            this.profile = profile;
        }
    }

    static readonly FlapDef[] ClassicFlaps = { new FlapDef(0f, 1f, 1f, 1f, 0) };
    static readonly FlapDef[] CrescentFlaps = { new FlapDef(0f, 1.08f, 1f, 1f, 1) };
    static readonly FlapDef[] JagFlaps = { new FlapDef(0f, 0.92f, 1.15f, 1f, 2) };
    static readonly FlapDef[] BettaFlaps =
    {
        new FlapDef(-9f, 1.05f, 1.00f, 1.25f, 3),
        new FlapDef(14f, 0.78f, 0.85f, 1.45f, 3),
    };
    static readonly FlapDef[] FanFlaps = { new FlapDef(0f, 0.85f, 1.45f, 0.9f, 4) };
    static readonly FlapDef[] StreamerFlaps =
    {
        new FlapDef(-13f, 1.30f, 1f, 1.7f, 5),
        new FlapDef(3f, 1.05f, 1f, 1.9f, 5),
        new FlapDef(18f, 0.80f, 1f, 2.1f, 5),
    };

    public static FlapDef[] GetFlaps(Fluke3DStyle style)
    {
        switch (style)
        {
            case Fluke3DStyle.TealCrescent: return CrescentFlaps;
            case Fluke3DStyle.CoralJag: return JagFlaps;
            case Fluke3DStyle.BettaSplit: return BettaFlaps;
            case Fluke3DStyle.GoldfishFan: return FanFlaps;
            case Fluke3DStyle.SilkStreamers: return StreamerFlaps;
            default: return ClassicFlaps;
        }
    }

    /// Mesh rings per bone segment — edge detail (teeth, scallops) needs more silhouette
    /// resolution than one ring per bone. Classic stays at 1 = bit-identical mesh.
    public static int Subdivisions(Fluke3DStyle style)
    {
        switch (style)
        {
            case Fluke3DStyle.Classic: return 1;
            case Fluke3DStyle.CoralJag: return 6;
            case Fluke3DStyle.GoldfishFan: return 5;
            default: return 4;
        }
    }

    /// Dense silhouette profiles: half-width in world units at t (0 = root, 1 = tip),
    /// calibrated to the classic curve's ~0.18 peak. Profile 0 is handled by the caller
    /// (the serialized flukeWidthCurve).
    public static float Profile(int profile, float t)
    {
        t = Mathf.Clamp01(t);
        // Leaf envelope, safe to raise to fractional powers: float sin(π) lands a hair
        // BELOW zero, and Pow(negative, fraction) = NaN — which nuked the whole mesh
        // (invalid MinMaxAABB, invisible fluke) on the profiles that shape their tip
        // with a fractional exponent. Clamp before Pow, always.
        float Leaf(float shape) => Mathf.Max(0f, Mathf.Sin(Mathf.PI * Mathf.Pow(t, shape)));
        switch (profile)
        {
            case 1: // crescent sickle: swells early, long taper to a sharp point
            {
                float w = 0.21f * Leaf(0.62f) * (1f - 0.30f * t);
                return Mathf.Max(0.004f, w);
            }
            case 2: // rounded paddle + sawtooth trailing edge
            {
                float body = 0.20f * Mathf.Pow(Leaf(0.55f), 0.5f);
                float tooth = 1f - 2f * Mathf.Abs(Mathf.Repeat(t * 7f, 1f) - 0.5f);   // 0..1 triangle, 7 teeth
                float jag = 0.38f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.40f, 0.65f, t)) * tooth;
                return Mathf.Max(0.004f, body * (1f - jag));
            }
            case 3: // slender betta leaf
            {
                float w = 0.155f * Leaf(0.78f);
                return Mathf.Max(0.004f, w);
            }
            case 4: // broad fan, flat-topped, with smooth scallops on the outer half
            {
                float body = 0.20f * Mathf.Pow(Leaf(0.5f), 0.35f);
                float bump = 0.5f + 0.5f * Mathf.Cos(t * 5f * 2f * Mathf.PI);
                float scallop = 0.13f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.30f, 0.55f, t)) * bump;
                return Mathf.Max(0.004f, body * (1f - scallop));
            }
            case 5: // skinny silk streamer
            {
                float w = 0.075f * Mathf.Pow(Leaf(0.6f), 0.55f);
                return Mathf.Max(0.004f, w);
            }
            default:
                return 0.15f;
        }
    }

    public static string DisplayName(Fluke3DStyle style)
    {
        switch (style)
        {
            case Fluke3DStyle.TealCrescent: return "Teal Crescent";
            case Fluke3DStyle.CoralJag: return "Coral Jag";
            case Fluke3DStyle.BettaSplit: return "Betta Split";
            case Fluke3DStyle.GoldfishFan: return "Goldfish Fan";
            case Fluke3DStyle.SilkStreamers: return "Silk Streamers";
            default: return "Classic Gold";
        }
    }

    /// Toon palette per style. Textured styles keep the base near-white so the pattern
    /// texture carries the hue; the shade side is tinted so cel bands stay colorful.
    public static Color ToonBase(Fluke3DStyle style)
    {
        switch (style)
        {
            case Fluke3DStyle.TealCrescent: return new Color(1f, 1f, 1f);
            case Fluke3DStyle.CoralJag: return new Color(1f, 0.98f, 0.95f);
            case Fluke3DStyle.BettaSplit: return new Color(1f, 0.96f, 1f);
            case Fluke3DStyle.GoldfishFan: return new Color(1f, 1f, 0.96f);
            case Fluke3DStyle.SilkStreamers: return new Color(1f, 1f, 1f);
            default: return Color.white;   // Classic uses the serialized field instead
        }
    }

    public static Color ToonShade(Fluke3DStyle style)
    {
        switch (style)
        {
            case Fluke3DStyle.TealCrescent: return new Color(0.50f, 0.68f, 0.72f);
            case Fluke3DStyle.CoralJag: return new Color(0.75f, 0.52f, 0.48f);
            case Fluke3DStyle.BettaSplit: return new Color(0.55f, 0.44f, 0.70f);
            case Fluke3DStyle.GoldfishFan: return new Color(0.72f, 0.52f, 0.40f);
            case Fluke3DStyle.SilkStreamers: return new Color(0.62f, 0.60f, 0.74f);
            default: return Color.white;
        }
    }

    // ------------------------------------------------------------------ pattern textures

    const int TexSize = 256;
    static readonly Dictionary<Fluke3DStyle, Texture2D> texCache = new Dictionary<Fluke3DStyle, Texture2D>();

    /// Procedural pattern texture for the style (null for Classic = untextured toon).
    /// Cached; regenerated if the cached object was destroyed (play-mode transitions).
    public static Texture2D GetTexture(Fluke3DStyle style)
    {
        if (style == Fluke3DStyle.Classic) return null;
        if (texCache.TryGetValue(style, out var cached) && cached != null) return cached;

        var tex = new Texture2D(TexSize, TexSize, TextureFormat.RGBA32, false)
        {
            name = $"FlukePattern_{style}",
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.HideAndDontSave,
        };
        var px = new Color[TexSize * TexSize];
        switch (style)
        {
            case Fluke3DStyle.TealCrescent: FillTeal(px); break;
            case Fluke3DStyle.CoralJag: FillStripes(px); break;
            case Fluke3DStyle.BettaSplit: FillSparkle(px); break;
            case Fluke3DStyle.GoldfishFan: FillScales(px); break;
            case Fluke3DStyle.SilkStreamers: FillIridescent(px); break;
        }
        tex.SetPixels(px);
        tex.Apply(false, true);
        texCache[style] = tex;
        return tex;
    }

    // v runs 0 at the lobe ROOT → 1 at the TIP (TubeRenderer's along-length UV).
    // u wraps around the cross-section ring.

    static float Hash(int x, int y)
    {
        // deterministic per-pixel noise, tileable because it's pure position hash
        float h = Mathf.Sin(x * 12.9898f + y * 78.233f) * 43758.5453f;
        return h - Mathf.Floor(h);
    }

    static void FillTeal(Color[] px)
    {
        // Uni-colored: deep teal melting to bright aqua toward the tip, whisper of grain.
        Color root = new Color(0.09f, 0.42f, 0.50f);
        Color tip = new Color(0.42f, 0.88f, 0.86f);
        for (int y = 0; y < TexSize; y++)
        {
            float v = y / (float)(TexSize - 1);
            Color band = Color.Lerp(root, tip, Mathf.Pow(v, 1.3f));
            for (int x = 0; x < TexSize; x++)
            {
                float n = (Hash(x, y) - 0.5f) * 0.05f;
                px[y * TexSize + x] = new Color(band.r + n, band.g + n, band.b + n, 1f);
            }
        }
    }

    static void FillStripes(Color[] px)
    {
        // Bold coral / cream bands marching root → tip, soft-edged.
        Color a = new Color(0.96f, 0.44f, 0.36f);
        Color b = new Color(1.00f, 0.93f, 0.80f);
        for (int y = 0; y < TexSize; y++)
        {
            float v = y / (float)(TexSize - 1);
            float tri = Mathf.PingPong(v * 7f, 1f);
            float s = Mathf.SmoothStep(0.40f, 0.60f, tri);
            Color band = Color.Lerp(a, b, s);
            for (int x = 0; x < TexSize; x++)
                px[y * TexSize + x] = band;
        }
    }

    static void FillSparkle(Color[] px)
    {
        // Deep violet → magenta silk, dusted with seeded glitter (soft gaussian dots).
        Color root = new Color(0.27f, 0.14f, 0.46f);
        Color tip = new Color(0.60f, 0.22f, 0.60f);
        for (int y = 0; y < TexSize; y++)
        {
            float v = y / (float)(TexSize - 1);
            Color band = Color.Lerp(root, tip, v);
            for (int x = 0; x < TexSize; x++)
            {
                float n = (Hash(x, y) - 0.5f) * 0.06f;
                px[y * TexSize + x] = new Color(band.r + n, band.g + n, band.b + n, 1f);
            }
        }
        var rng = new System.Random(20260728);
        for (int i = 0; i < 150; i++)
        {
            int cx = rng.Next(TexSize), cy = rng.Next(TexSize);
            float rad = 1.2f + 2.0f * (float)rng.NextDouble();
            float bright = 0.55f + 0.45f * (float)rng.NextDouble();
            int ext = Mathf.CeilToInt(rad * 2f);
            for (int dy = -ext; dy <= ext; dy++)
            {
                for (int dx = -ext; dx <= ext; dx++)
                {
                    // wrap so the sparkle field tiles seamlessly
                    int x = (cx + dx + TexSize) % TexSize;
                    int y = (cy + dy + TexSize) % TexSize;
                    float g = bright * Mathf.Exp(-(dx * dx + dy * dy) / (rad * rad));
                    int idx = y * TexSize + x;
                    Color c = px[idx];
                    px[idx] = new Color(
                        Mathf.Min(1f, c.r + g),
                        Mathf.Min(1f, c.g + g * 0.9f),
                        Mathf.Min(1f, c.b + g), 1f);
                }
            }
        }
    }

    static void FillScales(Color[] px)
    {
        // Overlapping fish scales: an offset grid of rim-shaded discs, goldfish orange
        // lightening toward the tip. Cell sizes divide 256 so the pattern tiles.
        Color body = new Color(1.00f, 0.62f, 0.20f);
        Color rim = new Color(0.70f, 0.33f, 0.10f);
        const float cellW = 32f, rowH = 32f;
        float rad = cellW * 0.68f;
        for (int y = 0; y < TexSize; y++)
        {
            float v = y / (float)(TexSize - 1);
            Color bodyRow = Color.Lerp(body, Color.Lerp(body, Color.white, 0.35f), v);
            for (int x = 0; x < TexSize; x++)
            {
                int ry = Mathf.FloorToInt(y / rowH);
                Color col = bodyRow;
                // Check this row and the row above; the LOWER row's scales overlap upward,
                // so take the first (lowest) disc that contains the pixel.
                for (int rr = ry; rr <= ry + 1; rr++)
                {
                    float cy = rr * rowH;
                    float off = (rr & 1) == 0 ? 0f : cellW * 0.5f;
                    float cx = Mathf.Round((x - off) / cellW) * cellW + off;
                    float dx = x - cx, dy = y - cy;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) / rad;
                    if (d <= 1f)
                    {
                        float edge = Mathf.SmoothStep(0.70f, 1f, d);
                        col = Color.Lerp(bodyRow, rim, edge);
                        break;
                    }
                }
                px[y * TexSize + x] = col;
            }
        }
    }

    static void FillIridescent(Color[] px)
    {
        // Soft pastel hue sweep along the streamer + faint diagonal shimmer bands.
        for (int y = 0; y < TexSize; y++)
        {
            float v = y / (float)(TexSize - 1);
            for (int x = 0; x < TexSize; x++)
            {
                float u = x / (float)(TexSize - 1);
                float hue = Mathf.Repeat(0.48f + 0.42f * v, 1f);
                float shimmer = 0.06f * Mathf.Sin((u * 4f + v * 9f) * 2f * Mathf.PI)
                              + 0.04f * Mathf.Sin(v * 10f * Mathf.PI);
                Color c = Color.HSVToRGB(hue, 0.38f, Mathf.Clamp01(0.97f + shimmer));
                px[y * TexSize + x] = new Color(c.r, c.g, c.b, 1f);
            }
        }
    }
}
