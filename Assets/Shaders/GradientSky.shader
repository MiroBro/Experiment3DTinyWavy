Shader "Skybox/UnderwaterGradient"
{
    // Cheap vertical gradient skybox for the underwater backdrop. Top = deeper/darker water,
    // horizon = brighter mid, bottom = dark seabed murk.
    Properties
    {
        _TopColor    ("Top Color", Color)     = (0.02, 0.10, 0.20, 1)
        _HorizonColor("Horizon Color", Color) = (0.06, 0.30, 0.40, 1)
        _BottomColor ("Bottom Color", Color)  = (0.01, 0.05, 0.09, 1)
        _Sharp       ("Horizon Sharpness", Float) = 1.4
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; float3 texcoord : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float3 dir : TEXCOORD0; };

            half4 _TopColor, _HorizonColor, _BottomColor;
            float _Sharp;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = v.texcoord;
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                float y = normalize(i.dir).y;
                float k = 1.0 / max(0.01, _Sharp);
                half3 col = (y > 0)
                    ? lerp(_HorizonColor.rgb, _TopColor.rgb,    pow(saturate(y),  k))
                    : lerp(_HorizonColor.rgb, _BottomColor.rgb, pow(saturate(-y), k));
                return half4(col, 1);
            }
            ENDCG
        }
    }
}
