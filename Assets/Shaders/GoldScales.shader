Shader "Mermaid/GoldScales"
{
    // Gorgeous golden mermaid scales for the procedural tail/fluke tube. A fish-scale pattern
    // (offset rows of rounded cells) over a dark base that warms into gold toward the tip, with
    // scattered shimmering emissive "sequin" dots that glow under bloom — matching the reference.
    Properties
    {
        _BaseColor  ("Base (root)", Color)   = (0.06, 0.04, 0.02, 1)
        _GoldColor  ("Gold (tip)", Color)    = (0.85, 0.62, 0.18, 1)
        _EdgeColor  ("Scale Edge", Color)    = (0.20, 0.12, 0.03, 1)
        _DotColor   ("Sequin Glow", Color)   = (1.0, 0.82, 0.35, 1)
        _ScaleU     ("Scales Around", Float) = 10
        _ScaleV     ("Scales Along", Float)  = 28
        _DotStrength("Sequin Glow Strength", Range(0,6)) = 2.2
        _DotAmount  ("Sequin Amount", Range(0,1)) = 0.35
        _Shine      ("Shine", Range(0,1)) = 0.6
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
                float4 _BaseColor; float4 _GoldColor; float4 _EdgeColor; float4 _DotColor;
                float _ScaleU; float _ScaleV; float _DotStrength; float _DotAmount; float _Shine;
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

            float Hash (float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            half4 frag (Varyings IN) : SV_Target
            {
                // Scale cells: offset alternate rows for a fish-scale weave.
                float2 suv = IN.uv * float2(_ScaleU, _ScaleV);
                float rowId = floor(suv.y);
                suv.x += 0.5 * frac(rowId * 0.5) * 2.0; // stagger every other row
                float2 cellId = float2(floor(suv.x), rowId);
                float2 cell = frac(suv) - 0.5;

                // Rounded scale shape (teardrop-ish via vertical squash).
                float d = length(cell * float2(1.0, 1.25));
                float body = smoothstep(0.52, 0.40, d); // 1 inside scale, 0 at the gap/edge

                // Colour: dark base near the root, warming to gold toward the tip (uv.y along).
                float along = saturate(IN.uv.y);
                float3 scaleCol = lerp(_BaseColor.rgb, _GoldColor.rgb, along * along);
                float3 col = lerp(_EdgeColor.rgb, scaleCol, body);

                // Shimmering sequin dots on a subset of scales.
                float h = Hash(cellId);
                float twinkle = 0.5 + 0.5 * sin(_Time.y * 3.0 + h * 28.0);
                float isDot = step(1.0 - _DotAmount, h);
                float dotMask = isDot * smoothstep(0.30, 0.0, d) * twinkle;
                float3 emission = _DotColor.rgb * dotMask * _DotStrength;

                // Simple URP lighting + ambient, plus a gold sheen.
                float3 N = normalize(IN.normalWS);
                Light mainLight = GetMainLight();
                float ndl = saturate(dot(N, mainLight.direction)) * 0.75 + 0.25;
                float3 ambient = SampleSH(N);
                float3 lit = col * (mainLight.color * ndl + ambient);

                // A touch of view-based sheen so the gold reads as metallic-ish.
                float3 V = normalize(GetCameraPositionWS() - IN.posWS);
                float sheen = pow(saturate(dot(N, V)), 2.0) * _Shine * along;
                lit += _GoldColor.rgb * sheen * 0.5;

                return half4(lit + emission, 1.0);
            }
            ENDHLSL
        }
    }
}
