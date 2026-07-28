Shader "Mermaid/FlukeToon2D"
{
    // BotW-style 2-tone cel shader for the true-3D fluke, so the fin reads as flat 2D art
    // next to the sprite mermaid. Shading comes from a FIXED stylized light direction (no
    // scene lights involved — perfectly consistent), quantized into two soft-edged bands,
    // plus a dark rim "ink" band at the silhouette standing in for the unified outline
    // (which deliberately skips the 3D fin). Transparent queue + ZWrite Off so the
    // MeshRenderer's sortingOrder interleaves it with the sprite sort.
    Properties
    {
        _MainTex   ("Pattern (RGB, optional)", 2D) = "white" {}
        _ToonBase  ("Lit Color", Color) = (1.0, 0.75, 0.28, 1)
        _ToonShade ("Shade Color", Color) = (0.62, 0.40, 0.10, 1)
        _ShadeAt   ("Band Threshold", Range(0.1, 0.9)) = 0.5
        _ShadeSoft ("Band Softness", Range(0.005, 0.3)) = 0.07
        _RootDarken("Root Darken", Range(0, 1)) = 0.30
        _InkColor  ("Rim Ink", Color) = (0.05, 0.04, 0.06, 1)
        _InkWidth  ("Rim Ink Width", Range(0, 0.6)) = 0.28
        _LightDir  ("Stylized Light Dir", Vector) = (-0.35, 0.75, -0.55, 0)
        _Iridescence ("Iridescence", Range(0, 1)) = 0
        _IridescenceScale ("Iridescence Band Scale", Range(0.5, 8)) = 3
        _Glitter   ("Glitter", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            ZWrite Off
            Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _ToonBase; float4 _ToonShade; float4 _InkColor; float4 _LightDir;
                float _ShadeAt; float _ShadeSoft; float _RootDarken; float _InkWidth;
                float _Iridescence; float _IridescenceScale; float _Glitter;
            CBUFFER_END

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            struct Attributes { float3 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; };
            struct Varyings   { float4 positionHCS:SV_POSITION; float3 normalWS:TEXCOORD0; float2 uv:TEXCOORD1; float3 posWS:TEXCOORD2; };

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                float3 w = TransformObjectToWorld(IN.positionOS);
                OUT.posWS = w;
                OUT.positionHCS = TransformWorldToHClip(w);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.uv = IN.uv;   // raw tube UV — _MainTex_ST applied at sample time
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                float3 N = normalize(IN.normalWS);
                float3 L = normalize(_LightDir.xyz);

                // Two-tone cel band. Half-lambert keeps the dark side from going pitch.
                float ndl = dot(N, L) * 0.5 + 0.5;
                float band = smoothstep(_ShadeAt - _ShadeSoft, _ShadeAt + _ShadeSoft, ndl);
                float3 col = lerp(_ToonShade.rgb, _ToonBase.rgb, band);

                // Optional pattern texture (fluke style designs). Defaults to white = no-op.
                // ST applied here, not in vert, so uv.y stays raw for the root darken below
                // (negative tiling x mirrors the pattern on the near lobe).
                float2 patUV = IN.uv * _MainTex_ST.xy + _MainTex_ST.zw;
                col *= SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, patUV).rgb;

                // Darken toward the root (uv.y = 0) so the fin melts into the tail tip.
                col *= lerp(1.0 - _RootDarken, 1.0, saturate(IN.uv.y));

                float3 V = normalize(GetCameraPositionWS() - IN.posWS);

                // Opal iridescence: the hue is driven by VIEW ANGLE (dot(N,V)), so every
                // ripple and twist of the fin rolls fresh rainbow bands across the surface
                // — the shimmer comes from the motion itself. Rainbow via a cosine palette,
                // blended toward white for a pastel, mother-of-pearl read instead of neon.
                if (_Iridescence > 0.001)
                {
                    float facing = dot(N, V);
                    float hueT = facing * _IridescenceScale + IN.uv.y * 1.7 + _Time.y * 0.25;
                    float3 rainbow = 0.5 + 0.5 * cos(6.2832 * (hueT + float3(0.0, 0.33, 0.67)));
                    rainbow = lerp(float3(1, 1, 1), rainbow, 0.65);
                    col = lerp(col, col * rainbow * 1.35 + rainbow * 0.18, _Iridescence);
                }

                // Holographic glitter: fixed micro-cells on the surface that each glint only
                // at their own narrow view angle — as the fin flexes, specks pop in and out
                // like real glitter catching light.
                if (_Glitter > 0.001)
                {
                    float2 cell = floor(IN.uv * float2(90.0, 240.0));
                    float h = frac(sin(dot(cell, float2(12.9898, 78.233))) * 43758.5453);
                    float wave = sin(h * 6.2832 + dot(N, V) * 12.0 + _Time.y * 2.0) * 0.5 + 0.5;
                    float glint = smoothstep(0.985, 1.0, wave);
                    col += _Glitter * glint * 0.9;
                }

                // Rim ink: where the surface turns edge-on to the camera, snap to the ink
                // color — a drawn contour that matches the BlackClones outline language.
                float fres = 1.0 - abs(dot(N, V));
                float ink = smoothstep(1.0 - _InkWidth, 1.0 - _InkWidth + 0.08, fres);
                col = lerp(col, _InkColor.rgb, ink);

                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
}
