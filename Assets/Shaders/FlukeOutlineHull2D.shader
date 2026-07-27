Shader "Mermaid/FlukeOutlineHull2D"
{
    // Inverted-hull silhouette stroke for the true-3D fluke: the tube mesh re-drawn with
    // every vertex pushed outward along its 3D normal by _Width (world units), flat color,
    // sorted at the unified-outline depth (behind the whole mermaid stack). Because the
    // push is along 3D normals, an edge-on fin still grows a full-thickness stroke — the
    // failure mode of scaled 2D clones — and the shape is exactly the camera's view of the
    // fin, fattened. Unions seamlessly with the BlackClones outline.
    Properties
    {
        _Color ("Stroke", Color) = (0.05, 0.04, 0.06, 1)
        _Width ("Width (world units)", Float) = 0.06
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color; float _Width;
            CBUFFER_END

            struct Attributes { float3 positionOS:POSITION; float3 normalOS:NORMAL; };
            struct Varyings   { float4 positionHCS:SV_POSITION; };

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                float3 w = TransformObjectToWorld(IN.positionOS);
                float3 n = TransformObjectToWorldNormal(IN.normalOS);
                w += normalize(n) * _Width;
                OUT.positionHCS = TransformWorldToHClip(w);
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                return half4(_Color.rgb, 1.0);
            }
            ENDHLSL
        }
    }
}
