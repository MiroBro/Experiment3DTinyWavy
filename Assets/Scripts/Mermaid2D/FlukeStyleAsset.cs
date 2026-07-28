using UnityEngine;

/// <summary>
/// Procedural pattern painted onto a fluke design (FlukeStyleAsset). Gradient/Stripes/
/// Sparkle/Scales/Iridescent are generated from the asset's two pattern colors; None =
/// untextured cel look; a customTexture on the asset overrides the pattern entirely.
/// </summary>
public enum FlukePattern { None, Gradient, Stripes, Sparkle, Scales, Iridescent }

/// <summary>
/// One fluke DESIGN as an editable asset: silhouette (flap layout + width profile), look
/// (pattern texture + toon palette) and motion feel (lag, rim ripple, water drag) all in
/// one place. Create via Assets > Create > Mermaid > Fluke Style. Drop assets into the
/// bootstrap's flukeStyleAssets wardrobe — or just put them in a Resources/FlukeStyles
/// folder, which the bootstrap auto-loads (sorted by name) when the wardrobe is empty.
/// Shape/look edits rebuild the fin LIVE (play mode and the editor preview); motion
/// fields apply every tick.
/// </summary>
[CreateAssetMenu(fileName = "FlukeStyle", menuName = "Mermaid/Fluke Style")]
public class FlukeStyleAsset : ScriptableObject
{
    [System.Serializable]
    public class Flap
    {
        [Tooltip("Splays this fin sideways within the fluke plane, degrees. 0 = straight along the lobe. Several flaps at different angles = a fan of independent fins (each gets its own physics chain). The two lobes mirror automatically.")]
        public float angleDeg;
        [Tooltip("Multiplier on the bootstrap's fluke3DLength. 0.8 = stubby, 1.3 = long.")]
        public float lengthScale = 1f;
        [Tooltip("Multiplier on the width profile — how broad this fin is.")]
        public float widthScale = 1f;
        [Tooltip("Floppiness: multiplies the bone lag. 1 = normal, 2 = loose silk, below 1 = stiffer.")]
        public float lagScale = 1f;
    }

    [Tooltip("Name shown on the play-mode cycle button. Empty = the asset's file name.")]
    public string displayName = "";

    [Header("Shape")]
    [Tooltip("Fins per lobe. One entry = a classic single fin; more = split/streamer designs.")]
    public Flap[] flaps = { new Flap() };
    [Tooltip("Built-in silhouette when Use Custom Profile is OFF: 1 crescent sickle, 2 jagged paddle, 3 slender leaf, 4 scalloped fan, 5 skinny streamer, 6 wavy petal.")]
    [Range(1, 6)] public int builtInProfile = 1;
    [Tooltip("ON = draw your own silhouette with the curve below instead of a built-in.")]
    public bool useCustomProfile;
    [Tooltip("Custom silhouette: half-width of the fin in WORLD units along its length (0 = root at the tail tip, 1 = fin tip). The classic leaf peaks around 0.18 — stay in that ballpark. Sharp dips = notches; bumps = scallops (raise Subdivisions so the mesh can show them).")]
    public AnimationCurve customProfile = new AnimationCurve(
        new Keyframe(0f, 0.05f, 1.29f, 1.29f),
        new Keyframe(0.632f, 0.180f, -0.246f, -0.246f),
        new Keyframe(0.939f, 0.033f, -0.139f, -0.139f),
        new Keyframe(1f, 0.004f, -1f, -1f));
    [Tooltip("Mesh rings per physics bone — silhouette resolution. 4 for smooth shapes, 6+ for sharp teeth. (Bone count itself lives on the bootstrap.)")]
    [Range(1, 8)] public int subdivisions = 4;
    [Tooltip("Cross-section flatness of the fin. Smaller = thinner blade.")]
    [Range(0.05f, 1f)] public float aspect = 0.21f;

    [Header("Look")]
    [Tooltip("Procedural pattern generated from the two colors below. None = flat cel only.")]
    public FlukePattern pattern = FlukePattern.Gradient;
    [Tooltip("Pattern color A: gradient root / stripe A / sparkle base / scale body / iridescent start hue.")]
    public Color patternColorA = new Color(0.09f, 0.42f, 0.50f);
    [Tooltip("Pattern color B: gradient tip / stripe B / sparkle tip / scale rim / iridescent end hue.")]
    public Color patternColorB = new Color(0.42f, 0.88f, 0.86f);
    [Tooltip("Optional painted texture — overrides the procedural pattern. Image V runs root → tip, U wraps around the fin's thickness; the near lobe samples it mirrored automatically.")]
    public Texture2D customTexture;
    [Tooltip("Cel lit-side color, multiplied with the pattern — keep near-white and let the pattern carry the hue.")]
    public Color toonBase = Color.white;
    [Tooltip("Cel shade-side color — the tint of the shadow band.")]
    public Color toonShade = new Color(0.6f, 0.6f, 0.65f);

    [Header("Motion (live, per-design)")]
    [Tooltip("Bone lag at the fin ROOT, seconds (× the bootstrap's global/fluke flow multipliers).")]
    [Range(0.01f, 1f)] public float baseLag = 0.1f;
    [Tooltip("Bone lag at the fin TIP, seconds — the traveling-wave control. Bigger root→tip spread = more sinus waves strung along the fin.")]
    [Range(0.02f, 2f)] public float tipLag = 0.3f;
    [Tooltip("Skirt-hem rim flutter amplitude, world units. 0 = off.")]
    [Range(0f, 0.4f)] public float rippleAmp = 0.07f;
    [Tooltip("Flutter waves along the fin.")]
    [Range(0.2f, 6f)] public float rippleCycles = 2.2f;
    [Tooltip("Flutter speed, waves per second.")]
    [Range(0f, 6f)] public float rippleHz = 1.7f;
    [Tooltip("Concentrates flutter at the outer rim; higher = only the very edge moves.")]
    [Range(0.5f, 6f)] public float rippleEdgeBias = 2f;
    [Tooltip("true = the two wide edges curl opposite ways (rotational flutter).")]
    public bool rippleCurl = true;
    [Tooltip("Water drag: how strongly apparent swim speed streams THIS fin out behind her (lag ÷ (1 + this × speed)). 0 = speed-blind.")]
    [Range(0f, 3f)] public float dragStretch = 0.9f;
    [Tooltip("Water drag: how much apparent speed tightens THIS fin's bend limit (fraction removed at full cruise).")]
    [Range(0f, 1f)] public float dragStiffen = 0.4f;

    public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;

    /// Half-width of the fin (world units) at t: 0 = root, 1 = tip.
    public float EvaluateProfile(float t)
    {
        if (useCustomProfile && customProfile != null && customProfile.length > 0)
            return Mathf.Max(0.004f, customProfile.Evaluate(Mathf.Clamp01(t)));
        return FlukeStyles.Profile(builtInProfile, t);
    }

    /// Hash of everything that requires a REBUILD when edited (shape + look). Motion and
    /// toon colors are pushed live every tick and deliberately excluded — editing them
    /// must not reset the fin.
    public int ContentHash()
    {
        unchecked
        {
            int h = 17;
            h = h * 31 + subdivisions;
            h = h * 31 + builtInProfile;
            h = h * 31 + (useCustomProfile ? 1 : 0);
            h = h * 31 + aspect.GetHashCode();
            h = h * 31 + (int)pattern;
            h = h * 31 + patternColorA.GetHashCode();
            h = h * 31 + patternColorB.GetHashCode();
            h = h * 31 + (customTexture != null ? customTexture.GetHashCode() : 0);
            if (flaps != null)
            {
                for (int i = 0; i < flaps.Length; i++)
                {
                    var f = flaps[i];
                    if (f == null) continue;
                    h = h * 31 + f.angleDeg.GetHashCode();
                    h = h * 31 + f.lengthScale.GetHashCode();
                    h = h * 31 + f.widthScale.GetHashCode();
                    h = h * 31 + f.lagScale.GetHashCode();
                }
            }
            if (useCustomProfile && customProfile != null)
                for (int i = 0; i <= 8; i++)
                    h = h * 31 + customProfile.Evaluate(i / 8f).GetHashCode();
            return h;
        }
    }
}
