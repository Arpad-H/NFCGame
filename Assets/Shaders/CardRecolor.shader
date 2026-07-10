// Gradient-maps a grayscale card base through per-region color ramps, driven by a
// packed mask. Runs on a CanvasRenderer, so it works on a world-space card in the
// hand and on a screen-space-overlay card in the library/preview with one material.
//
// Constraints:
//  - _BaseGray, _Mask and _NoiseTex must be imported with sRGB unchecked.
//  - The Image sprite is never sampled; it only decides the mesh. Use Type: Simple
//    with a full-rect, un-atlased sprite (or none) so the UVs stay 0..1.
//  - Per-card colors come from a material instance, not a MaterialPropertyBlock --
//    CanvasRenderer ignores property blocks. See CardThemeApplier.
Shader "UI/CardRecolor"
{
    Properties
    {
        // Bound by Image with the sprite's texture. Declared so Unity has somewhere
        // to put it; the shader deliberately never reads it.
        [PerRendererData] _MainTex ("Sprite Texture (unused)", 2D) = "white" {}

        [Header(Textures)]
        _BaseGray   ("Grayscale Base (R)",          2D) = "white" {}
        _Mask       ("Packed Mask (R stone G box B crack A silhouette)", 2D) = "black" {}
        _Overlay    ("Static Overlay (gold + rim)", 2D) = "black" {}
        _NoiseTex   ("Noise (tiling)",              2D) = "black" {}

        [Header(Stone Region)]
        _StoneShadow    ("Stone Shadow",    Color) = (0.051, 0.078, 0.141, 1)
        _StoneHighlight ("Stone Highlight", Color) = (0.427, 0.518, 0.659, 1)

        [Header(Text Box Region)]
        _BoxShadow    ("Box Shadow",    Color) = (0.180, 0.180, 0.188, 1)
        _BoxHighlight ("Box Highlight", Color) = (0.608, 0.608, 0.616, 1)

        [Header(Crack Emissive)]
        [HDR] _CrackEmissive ("Crack Emissive (HDR)", Color) = (0,0,0,1)
        _PulseSpeed  ("Pulse Speed (0 = off)", Range(0,8)) = 0
        _PulseFloor  ("Pulse Floor",           Range(0,1)) = 0.6

        [Header(Rolling Noise)]
        [HDR] _NoiseColor  ("Noise Color (HDR)",  Color) = (0,0,0,1)
        _NoiseStrength ("Noise Strength",         Range(0,2)) = 0
        _NoiseSpeed    ("Noise Pan Speed (xy)",   Vector) = (0.03, 0.05, 0, 0)
        _NoiseTiling   ("Noise Tiling (xy)",      Vector) = (2, 3, 0, 0)
        [Toggle(_NOISE_IN_CRACKS)] _NoiseInCracks ("Confine Noise To Cracks", Float) = 0

        [Header(Global)]
        _Tint  ("Global Tint / Fade", Color) = (1,1,1,1)

        _StencilComp      ("Stencil Comparison", Float) = 8
        _Stencil          ("Stencil ID",         Float) = 0
        _StencilOp        ("Stencil Operation",  Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask  ("Stencil Read Mask",  Float) = 255
        _ColorMask        ("Color Mask",         Float) = 15

        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"             = "Transparent"
            "IgnoreProjector"   = "True"
            "RenderType"        = "Transparent"
            "PreviewType"       = "Plane"
            "CanUseSpriteAtlas" = "False"
        }

        Stencil
        {
            Ref       [_Stencil]
            Comp      [_StencilComp]
            Pass      [_StencilOp]
            ReadMask  [_StencilReadMask]
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
            #pragma vertex   vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #pragma shader_feature_local _NOISE_IN_CRACKS

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

            sampler2D _BaseGray;  float4 _BaseGray_ST;
            sampler2D _Mask;
            sampler2D _Overlay;
            sampler2D _NoiseTex;

            fixed4 _StoneShadow;
            fixed4 _StoneHighlight;
            fixed4 _BoxShadow;
            fixed4 _BoxHighlight;
            half4  _CrackEmissive;
            half4  _NoiseColor;
            fixed4 _Tint;

            float4 _NoiseSpeed;
            float4 _NoiseTiling;
            half   _NoiseStrength;
            half   _PulseSpeed;
            half   _PulseFloor;

            float4 _ClipRect;

            v2f vert (appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex   = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _BaseGray);
                OUT.color    = v.color;
                return OUT;
            }

            fixed4 frag (v2f IN) : SV_Target
            {
                float2 uv = IN.texcoord;

                // ---- inputs -------------------------------------------------
                half  gray = tex2D(_BaseGray, uv).r;
                half4 mask = tex2D(_Mask,     uv);
                half4 ovr  = tex2D(_Overlay,  uv);

                half stoneMask = mask.r;
                half boxMask   = mask.g;
                half crackMask = mask.b;
                half silhouette= mask.a;

                // ---- gradient-map tint per region ---------------------------
                // lerp (not multiply) so shadows and highlights get independent
                // hues -- this is what keeps the hand-painted look.
                half3 stoneCol = lerp(_StoneShadow.rgb, _StoneHighlight.rgb, gray);
                half3 boxCol   = lerp(_BoxShadow.rgb,   _BoxHighlight.rgb,   gray);
                half3 col = stoneCol * stoneMask + boxCol * boxMask;

                // ---- rolling noise ------------------------------------------
                if (_NoiseStrength > 0.0)
                {
                    float2 nuv = uv * _NoiseTiling.xy + _Time.y * _NoiseSpeed.xy;
                    half n = tex2D(_NoiseTex, nuv).r;
                #ifdef _NOISE_IN_CRACKS
                    half region = crackMask;      // lava-in-the-cracks
                #else
                    half region = stoneMask;      // shimmer across the stone
                #endif
                    col += _NoiseColor.rgb * (n * _NoiseStrength * region);
                }

                // ---- crack emissive (HDR -> bloom) --------------------------
                half pulse = 1.0;
                if (_PulseSpeed > 0.0)
                    pulse = lerp(_PulseFloor, 1.0, 0.5 + 0.5 * sin(_Time.y * _PulseSpeed));
                col += _CrackEmissive.rgb * crackMask * pulse;

                // ---- composite static overlay (gold frame + rim) ------------
                // Straight alpha. Overlay texture is pre-dilated so its
                // transparent pixels carry sane RGB and won't fringe dark.
                col = lerp(col, ovr.rgb, ovr.a);

                // ---- output --------------------------------------------------
                // IN.color carries Image.color and any CanvasGroup alpha.
                fixed4 outColor;
                outColor.rgb = col * _Tint.rgb * IN.color.rgb;
                outColor.a   = silhouette * _Tint.a * IN.color.a;

                #ifdef UNITY_UI_CLIP_RECT
                outColor.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(outColor.a - 0.001);
                #endif

                return outColor;
            }
        ENDCG
        }
    }
}
