// Fullscreen passes for the mermaid outline. Separable dilation: pass 0 stretches the
// coverage mask by the stroke radius along ONE axis (run once horizontally into a temp
// target); pass 1 stretches that along the other axis (completing an exact square
// dilation) and draws the stroke color only OUTSIDE the original silhouette — interior
// pixels are discarded so her internal art is untouched, and gaps narrower than the
// stroke seal shut just like the black-clone mode.
Shader "Hidden/Mermaid2D/OutlineComposite"
{
    HLSLINCLUDE
    // URP Core.hlsl must come first: it defines TEXTURE2D_X, which Blit.hlsl uses.
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

    SAMPLER(mermaid_linear_clamp_sampler);

    // xy = mask texel size, z = stroke radius in pixels.
    float4 _MaskTexelRadius;

    // Max of _BlitTexture along dir (one texel per step), out to the stroke radius.
    float DirectionalMax(float2 uv, float2 dir)
    {
        int n = (int)ceil(_MaskTexelRadius.z);
        float m = 0.0;
        UNITY_LOOP
        for (int t = -n; t <= n; t++)
        {
            m = max(m, SAMPLE_TEXTURE2D_X(_BlitTexture, mermaid_linear_clamp_sampler,
                uv + dir * t).r);
        }
        return m;
    }
    ENDHLSL

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "MermaidOutlineDilateH"
            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragDilate

            half4 FragDilate(Varyings input) : SV_Target
            {
                float m = DirectionalMax(input.texcoord, float2(_MaskTexelRadius.x, 0.0));
                return half4(m, m, m, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "MermaidOutlineComposite"
            Cull Off
            ZWrite Off
            ZTest Always
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragComposite

            // The ORIGINAL (undilated) mask — the inside/outside test.
            TEXTURE2D_X(_MermaidMask);

            float4 _OutlineColor;
            // 1 = show the raw coverage mask as a white overlay (diagnostics).
            float _DebugView;

            half4 FragComposite(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                float inside = SAMPLE_TEXTURE2D_X(_MermaidMask, mermaid_linear_clamp_sampler, uv).r;
                if (_DebugView > 0.5)
                    return half4(1.0, 1.0, 1.0, inside * 0.8);
                // Interior (and most of the anti-aliased rim): leave the body untouched.
                clip(0.35 - inside);

                // _BlitTexture is the horizontally-dilated mask; finish with the vertical max.
                float coverage = DirectionalMax(uv, float2(0.0, _MaskTexelRadius.y));
                clip(coverage - 0.003);
                return half4(_OutlineColor.rgb, _OutlineColor.a * coverage);
            }
            ENDHLSL
        }
    }
}
