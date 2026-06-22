Shader "Seaweed/SeabedCaustics"
{
    // Seabed floor with animated underwater caustics (the rippling light dapples). Pure
    // procedural — no textures. Cheap: a couple of sin layers sharpened into bright cells.
    Properties
    {
        _BaseColor    ("Seabed Color", Color)   = (0.05, 0.16, 0.13, 1)
        _CausticColor ("Caustic Color", Color)  = (0.35, 0.85, 0.75, 1)
        _Scale        ("Caustic Scale", Float)  = 0.7
        _Speed        ("Caustic Speed", Float)  = 0.35
        _Strength     ("Caustic Strength", Range(0,2)) = 0.6
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _CausticColor;
                float _Scale;
                float _Speed;
                float _Strength;
            CBUFFER_END

            struct Attributes { float3 positionOS : POSITION; };
            struct Varyings { float4 positionHCS : SV_POSITION; float3 worldPos : TEXCOORD0; };

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                float3 w = TransformObjectToWorld(IN.positionOS);
                OUT.worldPos = w;
                OUT.positionHCS = TransformWorldToHClip(w);
                return OUT;
            }

            float Caustic (float2 p, float t)
            {
                float c = 0;
                c += sin(p.x * 1.0 + t)        * cos(p.y * 1.0 - t);
                c += sin(p.x * 1.7 - t * 1.3 + 1.3) * cos(p.y * 1.3 + t * 0.9);
                c = c * 0.5 + 0.5;            // -> 0..1
                return pow(saturate(c), 4.0); // sharpen into bright cells
            }

            half4 frag (Varyings IN) : SV_Target
            {
                float t = _Time.y * _Speed;
                float c = Caustic(IN.worldPos.xz * _Scale, t);
                half3 col = _BaseColor.rgb + _CausticColor.rgb * (c * _Strength);
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}
