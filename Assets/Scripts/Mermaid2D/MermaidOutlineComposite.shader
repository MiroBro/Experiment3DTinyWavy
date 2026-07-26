// Fullscreen pass for the mermaid outline: reads the coverage mask, dilates it by the
// stroke radius (ring taps), and draws the stroke color only OUTSIDE the silhouette —
// interior pixels are discarded so her internal art is untouched.
Shader "Hidden/Mermaid2D/OutlineComposite"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "MermaidOutlineComposite"
            Cull Off
            ZWrite Off
            ZTest Always
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            SAMPLER(mermaid_linear_clamp_sampler);

            float4 _OutlineColor;
            // xy = mask texel size, z = stroke radius in pixels.
            float4 _MaskTexelRadius;

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                float inside = SAMPLE_TEXTURE2D_X(_BlitTexture, mermaid_linear_clamp_sampler, uv).r;
                // Interior (and most of the anti-aliased rim): leave the body untouched.
                clip(0.35 - inside);

                float radiusPx = _MaskTexelRadius.z;
                float2 texel = _MaskTexelRadius.xy;
                float coverage = 0.0;

                // 3 rings x 16 directions, half-step stagger on odd rings for rounder strokes.
                UNITY_UNROLL
                for (int ring = 1; ring <= 3; ring++)
                {
                    float rad = radiusPx * ring / 3.0;
                    float stagger = (ring & 1) ? 0.5 : 0.0;
                    UNITY_UNROLL
                    for (int k = 0; k < 16; k++)
                    {
                        float ang = (k + stagger) * (6.2831853 / 16.0);
                        float2 offs = float2(cos(ang), sin(ang)) * rad * texel;
                        coverage = max(coverage,
                            SAMPLE_TEXTURE2D_X(_BlitTexture, mermaid_linear_clamp_sampler, uv + offs).r);
                    }
                }

                clip(coverage - 0.003);
                return half4(_OutlineColor.rgb, _OutlineColor.a * coverage);
            }
            ENDHLSL
        }
    }
}
