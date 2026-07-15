Shader "UI/Gradient"
{
    Properties
    {
        [Header(Gradient Shape)]
        [Space(4)]
        // 0 Linear | 1 Radial | 2 Angular (conic) | 3 Diamond | 4 Reflected (mirrored linear)
        [Enum(Linear,0,Radial,1,Angular,2,Diamond,3,Reflected,4)] _GradientType ("Type", Float) = 0
        _Angle    ("Angle (deg)", Range(0, 360)) = 0
        _CenterX  ("Center X", Range(0, 1)) = 0.5
        _CenterY  ("Center Y", Range(0, 1)) = 0.5
        _Scale    ("Scale", Range(0.01, 8)) = 1
        _Offset   ("Offset", Range(-1, 1)) = 0
        // Corrects radial / angular / diamond for non-square rects. Set to Width / Height.
        _Aspect   ("Aspect (W / H)", Float) = 1

        [Header(Wrapping and Easing)]
        [Space(4)]
        [Enum(Clamp,0,Repeat,1,Mirror,2)] _RepeatMode ("Repeat Mode", Float) = 0
        _Power    ("Gamma / Bias", Range(0.1, 5)) = 1
        _Smooth   ("Smooth Stops", Range(0, 1)) = 0

        [Header(Color Stops)]
        [Space(4)]
        [HDR] _Color0 ("Color 0", Color) = (1, 0.2, 0.2, 1)
        _Pos0     ("Position 0", Range(0, 1)) = 0.0
        [HDR] _Color1 ("Color 1", Color) = (1, 0.85, 0.2, 1)
        _Pos1     ("Position 1", Range(0, 1)) = 0.33
        [HDR] _Color2 ("Color 2", Color) = (0.2, 0.7, 1, 1)
        _Pos2     ("Position 2", Range(0, 1)) = 0.66
        [HDR] _Color3 ("Color 3", Color) = (0.6, 0.2, 1, 1)
        _Pos3     ("Position 3", Range(0, 1)) = 1.0

        [Header(Texture Blend)]
        [Space(4)]
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        // 0 = ignore sprite RGB, 1 = multiply gradient by sprite RGB
        _TexColorBlend ("Sprite Color Blend", Range(0, 1)) = 0
        // 0 = ignore sprite alpha, 1 = mask gradient by sprite alpha (use for icons / shapes)
        _TexAlphaBlend ("Sprite Alpha Mask", Range(0, 1)) = 1

        [Header(Output)]
        [Space(4)]
        _Alpha    ("Overall Alpha", Range(0, 1)) = 1
        _Dither   ("Dither (anti-band)", Range(0, 2)) = 0.6

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
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                float4 screenPos : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;

            float  _GradientType;
            float  _Angle;
            float  _CenterX;
            float  _CenterY;
            float  _Scale;
            float  _Offset;
            float  _Aspect;

            float  _RepeatMode;
            float  _Power;
            float  _Smooth;

            fixed4 _Color0; fixed4 _Color1; fixed4 _Color2; fixed4 _Color3;
            float  _Pos0;   float  _Pos1;   float  _Pos2;   float  _Pos3;

            float  _TexColorBlend;
            float  _TexAlphaBlend;
            float  _Alpha;
            float  _Dither;

            static const float PI = 3.14159265359;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(v.vertex);
                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                OUT.screenPos = ComputeScreenPos(OUT.vertex);
                OUT.color = v.color;
                return OUT;
            }

            // Apply the chosen wrap mode to a raw gradient coordinate.
            float ApplyRepeat(float t)
            {
                if (_RepeatMode == 1)        // Repeat
                    return frac(t);
                else if (_RepeatMode == 2)   // Mirror (ping-pong)
                    return 1.0 - abs(frac(t * 0.5) * 2.0 - 1.0);
                return saturate(t);          // Clamp
            }

            // Piecewise 4-stop gradient lookup.
            fixed4 SampleGradient(float t)
            {
                float f1 = saturate((t - _Pos0) / max(_Pos1 - _Pos0, 1e-5));
                float f2 = saturate((t - _Pos1) / max(_Pos2 - _Pos1, 1e-5));
                float f3 = saturate((t - _Pos2) / max(_Pos3 - _Pos2, 1e-5));

                // Optional smoothstep easing on each transition.
                f1 = lerp(f1, f1 * f1 * (3.0 - 2.0 * f1), _Smooth);
                f2 = lerp(f2, f2 * f2 * (3.0 - 2.0 * f2), _Smooth);
                f3 = lerp(f3, f3 * f3 * (3.0 - 2.0 * f3), _Smooth);

                fixed4 c = _Color0;
                c = lerp(c, _Color1, f1);
                c = lerp(c, _Color2, f2);
                c = lerp(c, _Color3, f3);
                return c;
            }

            // Map local UV -> gradient coordinate t (pre-wrap).
            float GradientCoord(float2 uv)
            {
                float2 center = float2(_CenterX, _CenterY);
                float a = _Angle * (PI / 180.0);
                float2 dir = float2(cos(a), sin(a));

                // Centered, aspect-corrected position.
                float2 p = uv - center;
                float2 pa = float2(p.x * _Aspect, p.y);

                float t;
                if (_GradientType == 1)          // Radial
                {
                    t = length(pa) * 2.0 * _Scale;
                }
                else if (_GradientType == 2)     // Angular / conic
                {
                    float ang = atan2(pa.y, pa.x) - a;
                    t = frac(ang / (2.0 * PI) + 1.0) * _Scale;
                }
                else if (_GradientType == 3)     // Diamond
                {
                    t = (abs(pa.x) + abs(pa.y)) * 2.0 * _Scale;
                }
                else if (_GradientType == 4)     // Reflected (mirror from center)
                {
                    t = abs(dot(p, dir)) * 2.0 * _Scale;
                }
                else                              // Linear
                {
                    t = dot(p, dir) * _Scale + 0.5;
                }

                return t + _Offset;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                // ----- gradient -----
                float t = GradientCoord(IN.texcoord);
                t = ApplyRepeat(t);
                t = pow(saturate(t), _Power);

                fixed4 grad = SampleGradient(t);

                // ----- sprite blend -----
                fixed4 tex = tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd;
                grad.rgb = lerp(grad.rgb, grad.rgb * tex.rgb, _TexColorBlend);
                grad.a  *= lerp(1.0, tex.a, _TexAlphaBlend);

                // ----- UI tint (Image color / CanvasGroup) & overall alpha -----
                fixed4 color = grad * IN.color;
                color.a *= _Alpha;

                // ----- ordered-ish dither to kill 8-bit banding -----
                float2 sp = IN.screenPos.xy / max(IN.screenPos.w, 1e-5) * _ScreenParams.xy;
                float d = frac(sin(dot(sp, float2(12.9898, 78.233))) * 43758.5453) - 0.5;
                color.rgb += d * (_Dither / 255.0);

                // ----- UGUI clip & alpha clip -----
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
