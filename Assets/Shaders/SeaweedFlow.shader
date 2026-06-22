Shader "Seaweed/Flow"
{
    // Single-mesh, GPU-animated seaweed. Every blade lives in one combined mesh; this shader
    // sways each vertex (a traveling underwater wave) and pushes it out of the mermaid's body
    // spheres, all on the GPU. The CPU only updates a handful of sphere uniforms per frame, so
    // thousands of blades cost one draw call.
    //
    // Per-vertex data baked by SeaweedField:
    //   uv0      = (side 0/1, heightFraction 0..1)
    //   TEXCOORD1= (swayDir.x, swayDir.z, phaseOffset, perBladeAmp)
    //   color    = per-blade green tint
    Properties
    {
        _RootDarken ("Root Darken", Range(0,1)) = 0.45
        _SwayFreq   ("Sway Frequency", Float) = 0.8
        _SwayAmp    ("Sway Amplitude", Float) = 0.35
        _WaveCount  ("Wave Count", Float) = 1.1
        _FlutterAmp ("Flutter Amplitude", Float) = 0.10
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        Cull Off // thin blades — show both faces

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            #define MAX_BODY 16
            // Outside the per-material CBUFFER on purpose (array uniforms set via SetVectorArray).
            float4 _BodySpheres[MAX_BODY]; // xyz = world centre, w = radius
            int    _BodyCount;

            CBUFFER_START(UnityPerMaterial)
                float _RootDarken;
                float _SwayFreq;
                float _SwayAmp;
                float _WaveCount;
                float _FlutterAmp;
                float4 _Scroll;       // xy = world XZ scroll offset
                float4 _PatchCenter;  // xy = patch centre XZ
                float4 _PatchHalf;    // xy = patch half-size XZ
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float4 swayData   : TEXCOORD1;
                float2 rootXZ     : TEXCOORD2;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float4 color       : COLOR;
                float  h           : TEXCOORD0;
            };

            Varyings vert (Attributes IN)
            {
                Varyings OUT;

                float  h     = IN.uv.y;
                float3 sway  = float3(IN.swayData.x, 0, IN.swayData.y);
                float3 flut  = float3(-IN.swayData.y, 0, IN.swayData.x);
                float  phase = IN.swayData.z;
                float  amp   = IN.swayData.w;

                float3 wpos = TransformObjectToWorld(IN.positionOS);

                // Treadmill: shift the whole blade by the scroll, wrapping within the patch
                // footprint (so blades that pass behind her reappear ahead). All of a blade's
                // verts share rootXZ, so the blade moves rigidly.
                float2 lo = _PatchCenter.xy - _PatchHalf.xy;
                float2 sizef = max(2.0 * _PatchHalf.xy, 0.001);
                float2 wrapped = lo + frac((IN.rootXZ + _Scroll.xy - lo) / sizef) * sizef;
                wpos.xz += (wrapped - IN.rootXZ);

                // Traveling-wave sway, growing toward the tip so the base stays planted.
                float bend    = pow(h, 1.5);
                float twoPi   = 6.2831853; // local name — TWO_PI is already a URP macro
                float wave    = sin(_Time.y * _SwayFreq * twoPi + phase - h * _WaveCount * twoPi);
                float lateral = (_SwayAmp * amp) * bend * (0.55 + 0.45 * wave);
                float side    = _FlutterAmp * bend * sin(_Time.y * _SwayFreq * 4.0 + phase * 1.3);
                // Blades always wave (underwater current) — independent of whether the bed is
                // scrolling. Only the treadmill scroll (handled on the CPU) starts/stops.
                wpos += sway * lateral + flut * side;

                // Push out of the body spheres so she parts the grass as she hovers / rummages.
                [loop]
                for (int bi = 0; bi < _BodyCount; bi++)
                {
                    float3 c = _BodySpheres[bi].xyz;
                    float  r = _BodySpheres[bi].w;
                    float3 to = wpos - c;
                    float  d  = length(to);
                    if (d < r && d > 1e-4)
                        wpos += to * ((r - d) / d);
                }

                OUT.positionHCS = TransformWorldToHClip(wpos);
                OUT.color = IN.color;
                OUT.h = h;
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                float darken = lerp(_RootDarken, 1.0, IN.h);
                return half4(IN.color.rgb * darken, 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
