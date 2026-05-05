Shader "Hair/Curly"
{
    Properties
    {
        [Header(Base)]
        _BaseColor ("Base Color", Color) = (0.55, 0.18, 0.08, 1)
        _BaseMap ("Base Map (optional)", 2D) = "white" {}

        [Header(Curl Bands)]
        _CurlFrequency ("Curl Frequency (cycles per strand)", Range(1, 200)) = 35
        _CurlDepth ("Curl Light/Dark Depth", Range(0, 1)) = 0.45
        _CurlSharpness ("Curl Sharpness", Range(0, 1)) = 0.4
        _CurlPhaseScramble ("Cross-strand Phase Scramble", Range(0, 1)) = 0.6

        [Header(Texture Noise)]
        _NoiseScale ("Noise Scale", Range(0.1, 200)) = 60
        _NoiseStrength ("Noise Strength", Range(0, 0.5)) = 0.12

        [Header(Lighting)]
        _ShadowColor ("Curl Shadow Color", Color) = (0.18, 0.07, 0.04, 1)
        _AmbientStrength ("Ambient Strength", Range(0, 2)) = 0.5
        _RimColor ("Rim / Sheen Color", Color) = (1.0, 0.7, 0.45, 1)
        _RimPower ("Rim Power", Range(0.5, 16)) = 4
        _RimStrength ("Rim Strength", Range(0, 2)) = 0.55
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float fogFactor : TEXCOORD3;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float _CurlFrequency;
                float _CurlDepth;
                float _CurlSharpness;
                float _CurlPhaseScramble;
                float _NoiseScale;
                float _NoiseStrength;
                float4 _ShadowColor;
                float _AmbientStrength;
                float4 _RimColor;
                float _RimPower;
                float _RimStrength;
            CBUFFER_END

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            // Cheap deterministic 2D hash → noise.
            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float valueNoise2D(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = hash21(i);
                float b = hash21(i + float2(1, 0));
                float c = hash21(i + float2(0, 1));
                float d = hash21(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs vertexInput = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionCS = vertexInput.positionCS;
                OUT.positionWS = vertexInput.positionWS;
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                OUT.fogFactor = ComputeFogFactor(OUT.positionCS.z);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 N = normalize(IN.normalWS);
                Light mainLight = GetMainLight();
                float NdotL = saturate(dot(N, mainLight.direction));

                // Base color (optional texture).
                half4 baseTex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);
                half3 baseColor = baseTex.rgb * _BaseColor.rgb;

                // Curl banding along the strand length (uv.y). A small per-strand phase
                // offset (using uv.x) means adjacent strands aren't all in lockstep.
                float phase = IN.uv.x * _CurlPhaseScramble * 6.28318;
                float curlRaw = sin(IN.uv.y * _CurlFrequency * 6.28318 + phase);

                // Sharpness: bias toward square-wave (peaked highlights, soft shadows).
                float curlShape = sign(curlRaw) * pow(abs(curlRaw), max(0.05, 1.0 - _CurlSharpness));
                float curlBrightness = 1.0 + curlShape * _CurlDepth;

                // Fuzzy noise overlay — sells the textured/fibrous look.
                float n = valueNoise2D(IN.uv * _NoiseScale);
                float noiseFactor = lerp(1.0 - _NoiseStrength, 1.0 + _NoiseStrength, n);

                // Shaded color: split between lit color and curl-shadow color along NdotL,
                // then modulated by the curl band and noise.
                half3 lit = lerp(baseColor * _ShadowColor.rgb, baseColor, NdotL);
                lit *= mainLight.color;
                lit *= curlBrightness * noiseFactor;

                // Ambient fill (uses RenderSettings.ambientLight via SH).
                half3 ambient = SampleSH(N);
                lit += baseColor * ambient * _AmbientStrength;

                // Rim sheen — wet/lustrous highlight on the silhouette.
                float3 V = normalize(_WorldSpaceCameraPos - IN.positionWS);
                float rim = pow(saturate(1.0 - dot(N, V)), _RimPower);
                lit += _RimColor.rgb * rim * _RimStrength;

                lit = MixFog(lit, IN.fogFactor);
                return half4(lit, baseTex.a * _BaseColor.a);
            }
            ENDHLSL
        }
    }

    Fallback "Universal Render Pipeline/Lit"
}
