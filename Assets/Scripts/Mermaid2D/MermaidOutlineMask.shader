// Override material for the mermaid-outline mask pass: draws every mermaid part as pure
// coverage (texture alpha x vertex alpha) into the offscreen mask. BlendOp Max so
// overlapping parts merge into one silhouette instead of accumulating.
Shader "Hidden/Mermaid2D/OutlineMask"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" }
        Pass
        {
            Name "MermaidOutlineMask"
            Cull Off
            ZWrite Off
            ZTest Always
            Blend One One
            BlendOp Max

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half coverage = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).a * input.color.a;
                return half4(coverage, coverage, coverage, coverage);
            }
            ENDHLSL
        }
    }
}
