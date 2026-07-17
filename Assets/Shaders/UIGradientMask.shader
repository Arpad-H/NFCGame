// Soft/gradient masking for UGUI. Unity's Mask component is stencil-based, so it can only
// ever be binary: a pixel is masked or it isn't. This shader instead multiplies the graphic's
// own alpha by a mask value, so white = fully visible, black = fully hidden, and everything
// in between fades — which is what a stencil physically cannot express.
//
// It lives on the thing being masked, not on a parent. Put a material using this shader on
// the Image / RawImage you want to fade. Two mask sources multiply together, and both default
// to "fully visible", so a fresh material is a no-op until you configure one:
//   * Mask Texture — any greyscale gradient (white visible / black hidden).
//   * Shape        — a procedural Linear or Radial falloff, for when you'd rather not author
//                    a texture just to fade something out to one side.
//
// UV Source picks where the mask is sampled. SpriteUV is right for a plain Image or RawImage
// (its UVs already run 0-1 across the rect). Switch to RectUV — and add the UIGradientMaskRect
// component — for Sliced/Tiled/Filled Images, or to have one parent rect drive a whole group.
Shader "UI/GradientMask"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}

        [Header(Mask Texture)]
        [Space(4)]
        _MaskTex ("Mask   white visible   black hidden", 2D) = "white" {}
        [Enum(Luminance,0,Red,1,Green,2,Blue,3,Alpha,4)] _MaskChannel ("Mask Channel", Float) = 0
        [Enum(SpriteUV,0,RectUV,1)] _MaskUVSource ("UV Source", Float) = 0

        [Header(Shape)]
        [Space(4)]
        [Enum(None,0,Linear,1,Radial,2)] _ShapeType ("Shape", Float) = 0
        _ShapeAngle ("Angle (deg)", Range(0, 360)) = 0
        _ShapeCenterX ("Center X", Range(0, 1)) = 0.5
        _ShapeCenterY ("Center Y", Range(0, 1)) = 0.5
        // Distance (0-1 across the rect) where the shape reaches black / white. Start > End
        // flips the fade, which is how you make Radial keep the middle and fade to the edges.
        _ShapeStart ("Fade Start", Range(-1, 2)) = 0
        _ShapeEnd ("Fade End", Range(-1, 2)) = 1
        // Keeps Radial circular on a non-square rect. Set to Width / Height.
        _ShapeAspect ("Aspect (W / H)", Float) = 1

        [Header(Mask Shaping)]
        [Space(4)]
        // Contrast on the combined mask. Set Min = Max for a hard cutoff instead of a fade.
        _MaskMin ("Remap Black Point", Range(0, 1)) = 0
        _MaskMax ("Remap White Point", Range(0, 1)) = 1
        _MaskPower ("Gamma / Bias", Range(0.1, 5)) = 1
        [Toggle] _MaskInvert ("Invert", Float) = 0
        // 0 = mask off (fully visible), 1 = mask fully applied. Tween this to fade a mask in.
        _MaskStrength ("Mask Strength", Range(0, 1)) = 1

        // ---- UGUI / masking plumbing (leave at defaults) ----
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
                float4 vertex    : POSITION;
                float4 color     : COLOR;
                float2 texcoord  : TEXCOORD0;
                float2 texcoord1 : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex    : SV_POSITION;
                fixed4 color     : COLOR;
                float2 texcoord  : TEXCOORD0;
                float2 maskcoord : TEXCOORD1;
                float2 shapecoord: TEXCOORD2;
                float4 worldPosition : TEXCOORD3;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;

            sampler2D _MaskTex;
            float4 _MaskTex_ST;
            float  _MaskChannel;
            float  _MaskUVSource;

            float  _ShapeType;
            float  _ShapeAngle;
            float  _ShapeCenterX;
            float  _ShapeCenterY;
            float  _ShapeStart;
            float  _ShapeEnd;
            float  _ShapeAspect;

            float  _MaskMin;
            float  _MaskMax;
            float  _MaskPower;
            float  _MaskInvert;
            float  _MaskStrength;

            static const float PI = 3.14159265359;

            // Remap x from the a..b range onto 0..1. Tolerates a == b (hard cutoff at a) and
            // a > b (reversed fade) without dividing by zero.
            float Remap01(float x, float a, float b)
            {
                float range = b - a;
                float safe = max(abs(range), 1e-5) * (range < 0.0 ? -1.0 : 1.0);
                return saturate((x - a) / safe);
            }

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(v.vertex);
                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);

                // RectUV comes from UIGradientMaskRect, which writes 0-1 rect coords into UV1.
                float2 maskUV = _MaskUVSource < 0.5 ? v.texcoord : v.texcoord1;
                OUT.maskcoord = TRANSFORM_TEX(maskUV, _MaskTex);
                OUT.shapecoord = maskUV;

                OUT.color = v.color;
                return OUT;
            }

            float SampleMaskTex(float2 uv)
            {
                fixed4 m = tex2D(_MaskTex, uv);
                if (_MaskChannel < 0.5) return dot(m.rgb, float3(0.299, 0.587, 0.114));
                if (_MaskChannel < 1.5) return m.r;
                if (_MaskChannel < 2.5) return m.g;
                if (_MaskChannel < 3.5) return m.b;
                return m.a;
            }

            float SampleShape(float2 uv)
            {
                if (_ShapeType < 0.5) return 1.0;   // None

                float2 p = uv - float2(_ShapeCenterX, _ShapeCenterY);

                float d;
                if (_ShapeType < 1.5)               // Linear
                {
                    float a = _ShapeAngle * (PI / 180.0);
                    d = dot(p, float2(cos(a), sin(a))) + 0.5;
                }
                else                                // Radial
                {
                    d = length(float2(p.x * _ShapeAspect, p.y)) * 2.0;
                }

                return Remap01(d, _ShapeStart, _ShapeEnd);
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                fixed4 color = (tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd) * IN.color;

                // Both sources are "white = visible", so combining them is a plain multiply.
                float mask = SampleMaskTex(IN.maskcoord) * SampleShape(IN.shapecoord);

                mask = lerp(mask, 1.0 - mask, _MaskInvert);
                mask = Remap01(mask, _MaskMin, _MaskMax);
                mask = pow(mask, _MaskPower);
                mask = lerp(1.0, mask, _MaskStrength);

                color.a *= mask;

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
