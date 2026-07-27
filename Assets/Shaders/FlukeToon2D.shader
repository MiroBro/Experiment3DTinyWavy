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
        _ToonBase  ("Lit Color", Color) = (1.0, 0.75, 0.28, 1)
        _ToonShade ("Shade Color", Color) = (0.62, 0.40, 0.10, 1)
        _ShadeAt   ("Band Threshold", Range(0.1, 0.9)) = 0.5
        _ShadeSoft ("Band Softness", Range(0.005, 0.3)) = 0.07
        _RootDarken("Root Darken", Range(0, 1)) = 0.30
        _InkColor  ("Rim Ink", Color) = (0.05, 0.04, 0.06, 1)
        _InkWidth  ("Rim Ink Width", Range(0, 0.6)) = 0.28
        _LightDir  ("Stylized Light Dir", Vector) = (-0.35, 0.75, -0.55, 0)
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
                float4 _ToonBase; float4 _ToonShade; float4 _InkColor; float4 _LightDir;
                float _ShadeAt; float _ShadeSoft; float _RootDarken; float _InkWidth;
            CBUFFER_END

            struct Attributes { float3 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; };
            struct Varyings   { float4 positionHCS:SV_POSITION; float3 normalWS:TEXCOORD0; float2 uv:TEXCOORD1; float3 posWS:TEXCOORD2; };

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                float3 w = TransformObjectToWorld(IN.positionOS);
                OUT.posWS = w;
                OUT.positionHCS = TransformWorldToHClip(w);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.uv = IN.uv;
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

                // Darken toward the root (uv.y = 0) so the fin melts into the tail tip.
                col *= lerp(1.0 - _RootDarken, 1.0, saturate(IN.uv.y));

                // Rim ink: where the surface turns edge-on to the camera, snap to the ink
                // color — a drawn contour that matches the BlackClones outline language.
                float3 V = normalize(GetCameraPositionWS() - IN.posWS);
                float fres = 1.0 - abs(dot(N, V));
                float ink = smoothstep(1.0 - _InkWidth, 1.0 - _InkWidth + 0.08, fres);
                col = lerp(col, _InkColor.rgb, ink);

                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
}
