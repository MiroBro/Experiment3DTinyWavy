Shader "Mermaid/MoodOverlay2D"
{
    // Fullscreen light-grade for the 2D scene: a camera-parented quad drawn over the whole
    // world (sortingOrder 500, transparent queue — under the uGUI). Pass 1 MULTIPLIES the
    // frame by _Tint (night/greenery dim and tint everything the camera rendered — sprites,
    // ribbons, sky, god rays, outline stroke; channels above 1 brighten), pass 2 ADDS a
    // _Glow haze on top (moon-mist, sun-sparkle). Both passes are untagged so URP renders
    // them in order as SRPDefaultUnlit.
    Properties
    {
        _Tint ("Multiply Tint", Color) = (1, 1, 1, 1)
        _Glow ("Additive Glow", Color) = (0, 0, 0, 1)
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }

        Pass // 1: frame × _Tint
        {
            Blend DstColor Zero
            ZWrite Off
            ZTest Always
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Tint; float4 _Glow;
            CBUFFER_END

            struct Attributes { float3 positionOS : POSITION; };
            struct Varyings { float4 positionHCS : SV_POSITION; };

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS);
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target { return half4(_Tint.rgb, 1); }
            ENDHLSL
        }

        Pass // 2: + _Glow
        {
            Blend One One
            ZWrite Off
            ZTest Always
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Tint; float4 _Glow;
            CBUFFER_END

            struct Attributes { float3 positionOS : POSITION; };
            struct Varyings { float4 positionHCS : SV_POSITION; };

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS);
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target { return half4(_Glow.rgb, 1); }
            ENDHLSL
        }
    }
}
