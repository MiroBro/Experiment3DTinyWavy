using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Builds the whole "gorgeous underwater" look at runtime (matching this project's procedural
/// style), all on cheap URP features so it stays friendly to weak machines:
///   • Post-processing Volume — ACES tonemapping, bloom (glows the gems/sparkles), a teal
///     color grade and a soft vignette.
///   • Atmosphere — tinted depth fog, a gradient skybox, and a trilight ambient fill.
///   • Drifting plankton motes (a light particle system with a generated soft-dot sprite).
///   • A handful of faked god-ray shafts and a caustic-dappled seabed floor.
///
/// Spawned by <see cref="MermaidBootstrap"/>. Every field is tweakable in the Inspector on the
/// runtime "UnderwaterAtmosphere" object while playing.
/// </summary>
public class UnderwaterAtmosphere : MonoBehaviour
{
    [Header("Water Mood")]
    public Color waterTint = new Color(0.22f, 0.58f, 0.62f);
    public Color deepColor = new Color(0.10f, 0.34f, 0.50f);
    public Color horizonColor = new Color(0.34f, 0.66f, 0.74f);
    [Tooltip("Exponential-squared fog density. Higher = murkier / shorter view distance. Low = bright, clear, sunny water.")]
    public float fogDensity = 0.014f;

    [Header("Lighting")]
    [Tooltip("Key (sun) light intensity — boosted for a bright, sunlit-shallows feel.")]
    public float sunIntensity = 1.7f;
    public Color sunColor = new Color(1f, 0.97f, 0.86f);
    [Tooltip("Overall ambient brightness multiplier.")]
    public float ambientIntensity = 1.6f;

    [Header("Post FX")]
    public float bloomIntensity = 0.9f;
    public float bloomThreshold = 0.9f;
    [Tooltip("Exposure lift — higher = brighter overall image.")]
    public float exposure = 0.35f;
    [Range(-100f, 100f)] public float saturation = 8f;
    [Range(0f, 1f)] public float vignette = 0.15f;
    [Tooltip("FXAA is the cheapest antialiasing; good for low-end. Turn off for absolute minimum cost.")]
    public bool fxaa = true;

    [Header("Motes (floating plankton)")]
    public int moteCount = 120;
    public Color moteColor = new Color(0.7f, 0.9f, 1f, 0.5f);

    [Header("God Rays")]
    public int godRayCount = 5;
    public Color godRayColor = new Color(0.45f, 0.75f, 0.85f);
    [Range(0f, 3f)] public float godRayIntensity = 0.55f;

    [Header("Seabed")]
    public bool spawnSeabed = true;
    public float seabedY = -1.08f;
    public float seabedSize = 44f;
    public Color seabedColor = new Color(0.05f, 0.16f, 0.13f);

    Volume _volume;

    void Start()
    {
        SetupFogAndAmbient();
        SetupKeyLight();
        SetupSkybox();
        SetupPostProcessing();
        SetupCamera();
        SetupSeabed();
        SetupGodRays();
        SetupMotes();
    }

    void SetupKeyLight()
    {
        // Brighten the existing directional "sun" for a sunlit-shallows look.
        Light sun = null;
        foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
            if (l.type == LightType.Directional) { sun = l; break; }
        if (sun == null)
        {
            var go = new GameObject("Sun");
            go.transform.SetParent(transform, false);
            go.transform.rotation = Quaternion.Euler(55f, -25f, 0f); // from above-ish
            sun = go.AddComponent<Light>();
            sun.type = LightType.Directional;
        }
        sun.color = sunColor;
        sun.intensity = sunIntensity;
    }

    void SetupFogAndAmbient()
    {
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = waterTint;
        RenderSettings.fogDensity = Mathf.Max(0f, fogDensity);

        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = horizonColor * 1.3f;
        RenderSettings.ambientEquatorColor = waterTint * 1.1f;
        RenderSettings.ambientGroundColor = deepColor;
        RenderSettings.ambientIntensity = ambientIntensity;
    }

    void SetupSkybox()
    {
        var shader = Shader.Find("Skybox/UnderwaterGradient");
        if (shader == null) return;
        var sky = new Material(shader);
        sky.SetColor("_TopColor", deepColor);
        sky.SetColor("_HorizonColor", horizonColor);
        sky.SetColor("_BottomColor", deepColor * 0.5f);
        RenderSettings.skybox = sky;
        DynamicGI.UpdateEnvironment();
    }

    void SetupPostProcessing()
    {
        var go = new GameObject("PostFX Volume");
        go.transform.SetParent(transform, false);
        _volume = go.AddComponent<Volume>();
        _volume.isGlobal = true;
        _volume.priority = 10f;

        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        _volume.sharedProfile = profile;

        var tone = profile.Add<Tonemapping>(true);
        tone.mode.Override(TonemappingMode.ACES);

        var bloom = profile.Add<Bloom>(true);
        bloom.intensity.Override(Mathf.Max(0f, bloomIntensity));
        bloom.threshold.Override(bloomThreshold);
        bloom.scatter.Override(0.72f);
        bloom.tint.Override(Color.Lerp(Color.white, waterTint, 0.25f));

        var color = profile.Add<ColorAdjustments>(true);
        color.postExposure.Override(exposure);
        color.saturation.Override(saturation);
        color.colorFilter.Override(Color.Lerp(Color.white, waterTint, 0.18f));

        var wb = profile.Add<WhiteBalance>(true);
        wb.temperature.Override(-14f); // cooler / bluer

        var vig = profile.Add<Vignette>(true);
        vig.intensity.Override(vignette);
        vig.smoothness.Override(0.6f);
        vig.color.Override(deepColor);
    }

    void SetupCamera()
    {
        var cam = Camera.main;
        if (cam == null) return;
        cam.clearFlags = CameraClearFlags.Skybox;
        var data = cam.GetUniversalAdditionalCameraData();
        if (data != null)
        {
            data.renderPostProcessing = true;
            data.antialiasing = fxaa ? AntialiasingMode.FastApproximateAntialiasing : AntialiasingMode.None;
        }
    }

    void SetupSeabed()
    {
        if (!spawnSeabed) return;
        var shader = Shader.Find("Seaweed/SeabedCaustics");
        if (shader == null) return;

        var go = GameObject.CreatePrimitive(PrimitiveType.Plane);
        go.name = "Seabed";
        go.transform.SetParent(transform, false);
        var col = go.GetComponent<Collider>();
        if (col != null) Destroy(col);
        // Unity Plane is 10x10 at scale 1.
        go.transform.position = new Vector3(0f, seabedY, 0f);
        go.transform.localScale = Vector3.one * (seabedSize / 10f);

        var mat = new Material(shader);
        mat.SetColor("_BaseColor", seabedColor);
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        go.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
    }

    void SetupGodRays()
    {
        var shader = Shader.Find("Seaweed/GodRay");
        if (shader == null) return;
        var mat = new Material(shader);
        mat.SetColor("_Color", godRayColor);
        mat.SetFloat("_Intensity", godRayIntensity);

        var parent = new GameObject("GodRays").transform;
        parent.SetParent(transform, false);

        var rng = new System.Random(99);
        for (int i = 0; i < godRayCount; i++)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = $"GodRay{i:D2}";
            q.transform.SetParent(parent, false);
            var col = q.GetComponent<Collider>();
            if (col != null) Destroy(col);

            float x = Mathf.Lerp(-9f, 9f, (float)rng.NextDouble());
            float z = Mathf.Lerp(-6f, 8f, (float)rng.NextDouble());
            q.transform.position = new Vector3(x, 3.5f, z);
            // Tall vertical shaft, tilted a little so they don't all face the same way.
            float yaw = Mathf.Lerp(-25f, 25f, (float)rng.NextDouble());
            float tilt = Mathf.Lerp(-10f, 10f, (float)rng.NextDouble());
            q.transform.rotation = Quaternion.Euler(tilt, yaw, 0f);
            float w = Mathf.Lerp(1.2f, 2.6f, (float)rng.NextDouble());
            q.transform.localScale = new Vector3(w, 11f, 1f);

            q.GetComponent<MeshRenderer>().sharedMaterial = mat;
            q.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        }
    }

    void SetupMotes()
    {
        var go = new GameObject("PlanktonMotes");
        go.transform.SetParent(transform, false);
        go.transform.position = new Vector3(0f, 0.5f, 0f);
        var ps = go.AddComponent<ParticleSystem>();

        // Renderer + soft additive dot material (generated, no texture asset needed).
        var psr = go.GetComponent<ParticleSystemRenderer>();
        var moteShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (moteShader == null) moteShader = Shader.Find("Sprites/Default");
        var mat = new Material(moteShader);
        var dot = MakeSoftDot();
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", dot);
        if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", dot);
        if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f); // transparent
        if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 1f);     // additive
        psr.sharedMaterial = mat;
        psr.renderMode = ParticleSystemRenderMode.Billboard;

        var main = ps.main;
        main.loop = true;
        main.startLifetime = 14f;
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.015f, 0.05f);
        main.startColor = moteColor;
        main.maxParticles = Mathf.Max(1, moteCount);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 0f;

        var emission = ps.emission;
        emission.rateOverTime = Mathf.Max(1, moteCount) / main.startLifetime.constant;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(18f, 6f, 14f);

        // Gentle drifting current.
        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(-0.05f, 0.05f);
        vel.y = new ParticleSystem.MinMaxCurve(0.01f, 0.06f);
        vel.z = new ParticleSystem.MinMaxCurve(-0.05f, 0.05f);

        // Fade in/out so they twinkle into existence.
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f),
                    new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(grad);

        ps.Play();
    }

    static Texture2D MakeSoftDot()
    {
        const int S = 32;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var c = (S - 1) * 0.5f;
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                float a = Mathf.Clamp01(1f - d);
                a = a * a; // soft falloff
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        tex.Apply();
        return tex;
    }
}
