Shader "Mermaid/SkinGold"
{
    // Skin with glowing gold markings woven in: thin gold "rib" lines across the chest band,
    // a sternum accent, and scattered gold sequins over the torso — emissive so the scene
    // bloom lights them like the gilded body in the reference. Uses the body tube's UVs
    // (U = around, V = along: 0 at the neck, 1 at the hip).
    Properties
    {
        _SkinColor  ("Skin", Color) = (0.46, 0.25, 0.14, 1)
        _GoldColor  ("Gold", Color) = (1.0, 0.75, 0.28, 1)
        _RibCount   ("Rib Count", Float) = 7
        _RibMin     ("Rib Region Min", Range(0,1)) = 0.18
        _RibMax     ("Rib Region Max", Range(0,1)) = 0.46
        _DotDensity ("Sequin Density", Float) = 22
        _DotAmount  ("Sequin Amount", Range(0,1)) = 0.16
        _Glow       ("Gold Glow", Range(0,4)) = 1.2
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _SkinColor; float4 _GoldColor;
                float _RibCount; float _RibMin; float _RibMax;
                float _DotDensity; float _DotAmount; float _Glow;
            CBUFFER_END

            struct Attributes { float3 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionHCS:SV_POSITION; float3 normalWS:TEXCOORD0; float2 uv:TEXCOORD1; float3 posWS:TEXCOORD2; };

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

            float Hash (float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }

            half4 frag (Varyings IN) : SV_Target
            {
                float U = IN.uv.x, V = IN.uv.y;

                // Rib lines across the chest band.
                float region = step(_RibMin, V) * step(V, _RibMax);
                float rib = smoothstep(0.06, 0.0, abs(frac(V * _RibCount) - 0.5)) * region;
                // Sternum accent near the (approx) front seam.
                float stern = smoothstep(0.03, 0.0, abs(frac(U) - 0.5)) * region;
                // Scattered gold sequins over the torso.
                float region2 = step(0.10, V) * step(V, 0.72);
                float2 cell = floor(float2(U * _DotDensity, V * _DotDensity * 1.6));
                float2 fr = frac(float2(U * _DotDensity, V * _DotDensity * 1.6)) - 0.5;
                float dots = step(1.0 - _DotAmount, Hash(cell)) * smoothstep(0.34, 0.1, length(fr)) * region2;

                float gold = saturate(rib + stern * 0.8 + dots);
                float3 base = lerp(_SkinColor.rgb, _GoldColor.rgb, gold);

                float3 N = normalize(IN.normalWS);
                Light mainLight = GetMainLight();
                float ndl = saturate(dot(N, mainLight.direction)) * 0.7 + 0.3;
                float3 lit = base * (mainLight.color * ndl + SampleSH(N));
                float3 emission = _GoldColor.rgb * gold * _Glow;

                return half4(lit + emission, 1.0);
            }
            ENDHLSL
        }
    }
}
