Shader "Custom/AccumuView"
{
    Properties
    {
        _EnvTex ("Env Texture", 2D) = "white" {}
        _AccTex ("Acc Texture", 2D) = "white" {}
        _MaskRatio ("Mask Ratio", Float) = 1.0
        _ScreenRatio ("Screen Ratio", Float) = 1.0
        [ShowAsVector2] _PlayerPosition ("Position", Vector) = (0, 0, 0, 0)
    }
    SubShader
    {
        // No culling or depth
        Cull Off ZWrite Off ZTest Always Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            sampler2D _EnvTex;
            sampler2D _AccTex;
            float _MaskRatio;
            float _ScreenRatio;
            float2 _PlayerPosition;

            fixed4 frag(v2f i) : SV_Target
            {
                float4 envColor = tex2D(_EnvTex, i.uv);
                float4 accColor = tex2D(_AccTex, i.uv);

                float2 off = i.uv - _PlayerPosition;
                off.x *= _ScreenRatio;

                if(off.x > _MaskRatio || off.x < -_MaskRatio ||
                    off.y > _MaskRatio || off.y < -_MaskRatio)
                {
                    return float4(accColor.rgb, 1.0);
                }
                else
                {
                    return float4(envColor.rgb, 1.0);
                }
            }
            ENDCG
        }
    }
    FallBack "Diffuse"
}
