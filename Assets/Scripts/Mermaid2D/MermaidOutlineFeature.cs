using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Screen-space unified outline for the mermaid ("Option B"). Two render-graph passes:
///   1. Mask — every renderer carrying <see cref="MermaidRenderingLayer"/> is re-drawn with
///      an override material into an offscreen coverage texture (white where she is, using
///      each part's real texture alpha, so painted sprites mask exactly).
///   2. Composite — a fullscreen pass dilates that mask by the stroke radius and draws the
///      outline color wherever the dilated mask reaches OUTSIDE her silhouette. Drawn after
///      transparents; interior pixels are discarded, so her internal art is untouched and
///      the stroke reads as sitting behind her.
///
/// Config is pushed every frame by Mermaid2DBootstrap via the statics below (the bootstrap
/// is the single source of truth for tuning, same as everything else in the scene).
/// </summary>
public class MermaidOutlineFeature : ScriptableRendererFeature
{
    /// <summary>Rendering-layer bit the bootstrap stamps on all mermaid renderers.</summary>
    public const uint MermaidRenderingLayer = 1u << 8;

    // Live config, written by Mermaid2DBootstrap. Off until a bootstrap asks for it.
    public static bool Enabled;
    public static float WorldWidth = 0.045f;
    public static Color StrokeColor = Color.black;
    // Diagnostic: composite shows the raw coverage mask as a white overlay instead of the
    // stroke, so a dead outline can be split into "mask empty" vs "composite broken".
    public static bool DebugViewMask;

    static bool _loggedMissingShaders;
    static bool _loggedActive;

    [Tooltip("Override-material shader that writes the mermaid's coverage into the mask.")]
    public Shader maskShader;
    [Tooltip("Fullscreen shader that dilates the mask and composites the stroke.")]
    public Shader compositeShader;

    Material _maskMat;
    Material _dilateMat;
    Material _compositeMat;
    OutlinePass _pass;

    public override void Create()
    {
        _pass = new OutlinePass { renderPassEvent = RenderPassEvent.AfterRenderingTransparents };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (!Enabled || WorldWidth <= 0.0001f) return;
        var camType = renderingData.cameraData.cameraType;
        if (camType != CameraType.Game && camType != CameraType.SceneView) return;

        if (maskShader == null) maskShader = Shader.Find("Hidden/Mermaid2D/OutlineMask");
        if (compositeShader == null) compositeShader = Shader.Find("Hidden/Mermaid2D/OutlineComposite");
        if (maskShader == null || compositeShader == null)
        {
            if (!_loggedMissingShaders)
            {
                _loggedMissingShaders = true;
                Debug.LogWarning("MermaidOutlineFeature: outline shaders not found (MermaidOutlineMask/MermaidOutlineComposite failed to import?) — ScreenSpace outline disabled.");
            }
            return;
        }
        if (_maskMat == null) _maskMat = CoreUtils.CreateEngineMaterial(maskShader);
        // Two instances of the same shader (pass 0 dilate / pass 1 composite) so the two
        // draws never fight over shared material uniforms.
        if (_dilateMat == null) _dilateMat = CoreUtils.CreateEngineMaterial(compositeShader);
        if (_compositeMat == null) _compositeMat = CoreUtils.CreateEngineMaterial(compositeShader);

        _pass.Setup(_maskMat, _dilateMat, _compositeMat);
        renderer.EnqueuePass(_pass);

        if (!_loggedActive)
        {
            _loggedActive = true;
            Debug.Log($"MermaidOutlineFeature: ScreenSpace outline passes enqueued (camera '{renderingData.cameraData.camera.name}').");
        }
    }

    protected override void Dispose(bool disposing)
    {
        CoreUtils.Destroy(_maskMat);
        CoreUtils.Destroy(_dilateMat);
        CoreUtils.Destroy(_compositeMat);
        _maskMat = null;
        _dilateMat = null;
        _compositeMat = null;
    }

    class OutlinePass : ScriptableRenderPass
    {
        static readonly List<ShaderTagId> ShaderTags = new List<ShaderTagId>
        {
            new ShaderTagId("SRPDefaultUnlit"),
            new ShaderTagId("UniversalForward"),
            new ShaderTagId("UniversalForwardOnly"),
        };
        static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
        static readonly int TexelRadiusId = Shader.PropertyToID("_MaskTexelRadius");
        static readonly int DebugViewId = Shader.PropertyToID("_DebugView");
        static readonly int MermaidMaskId = Shader.PropertyToID("_MermaidMask");
        const int PassDilateH = 0;
        const int PassComposite = 1;

        Material _maskMat;
        Material _dilateMat;
        Material _compositeMat;

        public void Setup(Material maskMat, Material dilateMat, Material compositeMat)
        {
            _maskMat = maskMat;
            _dilateMat = dilateMat;
            _compositeMat = compositeMat;
        }

        class MaskPassData
        {
            public RendererListHandle rendererList;
        }

        class DilatePassData
        {
            public TextureHandle mask;
            public Material material;
            public Vector4 texelRadius;
        }

        class CompositePassData
        {
            public TextureHandle mask;
            public TextureHandle dilated;
            public Material material;
            public Vector4 texelRadius;
            public Color color;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var resourceData = frameData.Get<UniversalResourceData>();
            var cameraData = frameData.Get<UniversalCameraData>();
            var renderingData = frameData.Get<UniversalRenderingData>();
            var lightData = frameData.Get<UniversalLightData>();

            var desc = cameraData.cameraTargetDescriptor;
            desc.depthBufferBits = 0;
            desc.msaaSamples = 1;
            desc.graphicsFormat = GraphicsFormat.R8G8B8A8_UNorm;
            TextureHandle mask = UniversalRenderer.CreateRenderGraphTexture(
                renderGraph, desc, "MermaidOutlineMask", true);

            using (var builder = renderGraph.AddRasterRenderPass<MaskPassData>(
                "Mermaid Outline Mask", out var passData))
            {
                var drawSettings = RenderingUtils.CreateDrawingSettings(
                    ShaderTags, renderingData, cameraData, lightData, SortingCriteria.CommonTransparent);
                drawSettings.overrideMaterial = _maskMat;
                var filterSettings = new FilteringSettings(
                    RenderQueueRange.all, -1, MermaidRenderingLayer);
                passData.rendererList = renderGraph.CreateRendererList(
                    new RendererListParams(renderingData.cullResults, drawSettings, filterSettings));

                builder.UseRendererList(passData.rendererList);
                builder.SetRenderAttachment(mask, 0);
                builder.AllowPassCulling(false);
                builder.SetRenderFunc((MaskPassData data, RasterGraphContext ctx) =>
                {
                    ctx.cmd.DrawRendererList(data.rendererList);
                });
            }

            // Stroke radius in pixels from the world-space width (orthographic 2D camera).
            var cam = cameraData.camera;
            float radiusPx = cam.orthographic
                ? WorldWidth * desc.height / (2f * Mathf.Max(0.0001f, cam.orthographicSize))
                : WorldWidth * 100f;
            radiusPx = Mathf.Clamp(radiusPx, 0.5f, 64f);
            var texelRadius = new Vector4(1f / desc.width, 1f / desc.height, radiusPx, 0f);

            // Separable dilation, first leg: stretch the mask horizontally by the radius.
            TextureHandle dilated = UniversalRenderer.CreateRenderGraphTexture(
                renderGraph, desc, "MermaidOutlineDilate", true);

            using (var builder = renderGraph.AddRasterRenderPass<DilatePassData>(
                "Mermaid Outline Dilate", out var passData))
            {
                passData.mask = mask;
                passData.material = _dilateMat;
                passData.texelRadius = texelRadius;

                builder.UseTexture(mask);
                builder.SetRenderAttachment(dilated, 0);
                builder.AllowPassCulling(false);
                builder.SetRenderFunc((DilatePassData data, RasterGraphContext ctx) =>
                {
                    data.material.SetVector(TexelRadiusId, data.texelRadius);
                    Blitter.BlitTexture(ctx.cmd, data.mask, new Vector4(1f, 1f, 0f, 0f), data.material, PassDilateH);
                });
            }

            // Second leg runs inside the composite: vertical stretch of the horizontal
            // result, then the stroke is drawn wherever that reaches outside the original.
            using (var builder = renderGraph.AddRasterRenderPass<CompositePassData>(
                "Mermaid Outline Composite", out var passData))
            {
                passData.mask = mask;
                passData.dilated = dilated;
                passData.material = _compositeMat;
                passData.texelRadius = texelRadius;
                passData.color = StrokeColor;

                builder.UseTexture(mask);
                builder.UseTexture(dilated);
                builder.SetRenderAttachment(resourceData.activeColorTexture, 0);
                builder.AllowPassCulling(false);
                builder.SetRenderFunc((CompositePassData data, RasterGraphContext ctx) =>
                {
                    data.material.SetColor(OutlineColorId, data.color);
                    data.material.SetVector(TexelRadiusId, data.texelRadius);
                    data.material.SetFloat(DebugViewId, DebugViewMask ? 1f : 0f);
                    data.material.SetTexture(MermaidMaskId, data.mask);
                    Blitter.BlitTexture(ctx.cmd, data.dilated, new Vector4(1f, 1f, 0f, 0f), data.material, PassComposite);
                });
            }
        }
    }
}
