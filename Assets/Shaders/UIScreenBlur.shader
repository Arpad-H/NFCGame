// Fullscreen Kawase blur used by UIScreenBlur via Graphics.Blit. One pass reads
// four diagonal taps _Offset texels apart and averages them; chaining passes with
// a growing offset builds a wide, smooth blur cheaply. Plain image-effect shader,
// so it runs under both the built-in and URP pipelines when driven by Graphics.Blit.
Shader "UI/UIScreenBlur"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Offset ("Blur Offset", Float) = 1.0
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float _Offset;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 o = _MainTex_TexelSize.xy * _Offset;
                fixed4 c  = tex2D(_MainTex, i.uv + float2(-o.x, -o.y));
                c        += tex2D(_MainTex, i.uv + float2( o.x, -o.y));
                c        += tex2D(_MainTex, i.uv + float2(-o.x,  o.y));
                c        += tex2D(_MainTex, i.uv + float2( o.x,  o.y));
                return c * 0.25;
            }
            ENDCG
        }
    }
}
