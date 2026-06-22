Shader "Seaweed/GodRay"
{
    // Cheap faked underwater light shaft. Applied to tall vertical quads. Additive, soft edges
    // (bright centre, fades at the sides and toward the bottom), with a slow shimmer. No real
    // volumetrics — just a few of these read convincingly as sun rays through water.
    Properties
    {
        _Color     ("Color", Color) = (0.45, 0.75, 0.85, 1)
        _Speed     ("Shimmer Speed", Float) = 1.0
        _Intensity ("Intensity", Range(0,3)) = 0.6
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Blend One One      // additive
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Speed;
                float _Intensity;
            CBUFFER_END

            struct Attributes { float3 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionHCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                float3 w = TransformObjectToWorld(IN.positionOS);
                OUT.positionHCS = TransformWorldToHClip(w);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                // Soft horizontal falloff (bright centre column).
                float edge = sin(IN.uv.x * 3.14159265);
                edge *= edge;
                // Brighter at the top (uv.y = 0), fading toward the bottom.
                float vert = saturate(1.0 - IN.uv.y);
                // Slow shimmer along the shaft.
                float shimmer = 0.65 + 0.35 * sin(IN.uv.x * 11.0 + _Time.y * _Speed * 6.2831853);
                float a = edge * vert * shimmer * _Intensity;
                return half4(_Color.rgb * a, a);
            }
            ENDHLSL
        }
    }
}
