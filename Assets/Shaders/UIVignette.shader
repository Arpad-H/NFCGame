// A procedural vignette for a full-screen UGUI graphic. Unlike URP's post-process Vignette
// (which only darkens the 3D render and never touches Screen Space - Overlay UI), this draws
// as a normal UI quad, so it sits over everything on its canvas — menus, cards, HUD — and works
// the same in any scene regardless of the camera stack or render pipeline.
//
// The graphic is transparent in the middle and fades to _Color at the edges. The mask is built
// from the rect's own 0-1 UVs, so nothing needs to be authored — drive it entirely from the
// four sliders. The vignette tint comes from the vertex colour (i.e. the Graphic's Color), so
// UIVignette can expose it as a plain Color field.
Shader "UI/Vignette"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}

        [Header(Vignette)]
        [Space(4)]
        // 0 = no darkening at the sides (off), 1 = darkening reaches the centre.
        _Intensity ("Intensity", Range(0, 1)) = 0.4
        // Width of the fade band from clear to fully tinted. 0 = a hard ring.
        _Smoothness ("Smoothness", Range(0.001, 1)) = 0.5
        // 0 = ellipse that follows the screen aspect (corners darken evenly), 1 = a true circle.
        _Rounded ("Roundness", Range(0, 1)) = 0
        _CenterX ("Center X", Range(0, 1)) = 0.5
        _CenterY ("Center Y", Range(0, 1)) = 0.5

        // ---- UGUI plumbing (leave at defaults; driven by the Canvas / Mask) ----
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
        CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex        : SV_POSITION;
                fixed4 color         : COLOR;
                float2 texcoord      : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;

            float _Intensity;
            float _Smoothness;
            float _Rounded;
            float _CenterX;
            float _CenterY;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(v.vertex);
                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                OUT.color = v.color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                // Tint comes from the Graphic colour; _MainTex stays white so this is a pure fill.
                fixed4 color = (tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd) * IN.color;

                // Distance from the configured centre, 0 in the middle and ~1 at the sides.
                float aspect = _ScreenParams.x / max(_ScreenParams.y, 1e-4);
                float2 d = (IN.texcoord - float2(_CenterX, _CenterY)) * 2.0;
                d.x *= lerp(aspect, 1.0, _Rounded);
                float dist = length(d);

                // Intensity slides the inner edge of the fade inward; Smoothness sets its width.
                float start = 1.0 - _Intensity;
                float mask = smoothstep(start, start + _Smoothness, dist);

                color.a *= saturate(mask);

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif

                return color;
            }
        ENDCG
        }
    }
}
