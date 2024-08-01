Shader "Custom/BlendView"
{
    Properties
    {
        _EnvTex ("Env Texture", 2D) = "white" {}
        _EnvTexDepth ("Env Texture Depth", 2D) = "white" {}
        _OppTex ("Opp Texture", 2D) = "white" {}
        _OppTexDepth ("Opp Texture Depth", 2D) = "white" {}
        _MaskRatio ("Mask Ratio", Float) = 1.0
        _MaskCutoff ("Mask Cutoff", Float) = 0.1
        _ScreenRatio ("Screen Ratio", Float) = 1.0
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
            sampler2D _EnvTexDepth;
            sampler2D _OppTex;
            sampler2D _OppTexDepth;
            float _MaskRatio;
            float _MaskCutoff;
            float _ScreenRatio;

            fixed4 frag(v2f i) : SV_Target
            {
                float4 envColor = tex2D(_EnvTex, i.uv);
                float envDepth = tex2D(_EnvTexDepth, i.uv).r;
                float4 oppColor = tex2D(_OppTex, i.uv);
                float oppDepth = tex2D(_OppTexDepth, i.uv).r;

                if(oppDepth > envDepth)
                {
                    float2 off = i.uv - float2(0.5, 0.5);
                    off.x *= _ScreenRatio;
                    float dist2 = dot(off, off);
                    float margin = _MaskCutoff * dist2;

                    float coeff = smoothstep(_MaskRatio - margin, _MaskRatio + margin, dist2);
                    return lerp(oppColor, envColor, coeff);
                }
                else
                {
                    return envColor;
                }
            }
            ENDCG
        }
    }
    FallBack "Diffuse"
}
