// IconSocketURP.shader
// URP UI shader that "sockets" a sprite: it casts a soft drop shadow + optional
// contact AO behind the icon, all derived at runtime from whatever sprite's alpha
// is bound to _MainTex - so swapping the icon needs no extra setup.
//
// IMPORTANT for the shadow to be visible:
//   * The sprite's import "Mesh Type" must be Full Rect (NOT Tight). A tight mesh
//     hugs the opaque pixels and clips the offset/blurred shadow away.
//   * The source texture needs transparent padding around the icon so the offset +
//     blur stay in-bounds (a UI Image can only draw inside its own quad).
//   * On the Image component: Image Type = Simple, "Use Sprite Mesh" = OFF.
//   IconSocket.cs enforces the Image-side settings; the texture/mesh side is an
//   import setting you set on the sprite asset.
//
// By default the icon itself is passed through untouched (neutral grade); the
// grade block only kicks in if you push Saturation/Brightness/Tint away from
// their identity values.
//
// NOTE: like the Built-in version, this is a minimal shader and does NOT include
// the full stencil / _ClipRect handling of Unity's real UI-Default shader. If you
// need RectMask2D masking, merge URP's UI-Default clip handling into this Pass.

Shader "UI/IconSocketURP"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _ShadowOffset ("Shadow Offset (texels)", Vector) = (6, -8, 0, 0)
        _ShadowBlur ("Shadow Blur Radius (texels)", Range(0,16)) = 5
        _ShadowStrength ("Shadow Strength", Range(0,2)) = 1.0
        _ShadowFalloff ("Shadow Falloff (lower = denser)", Range(0.2,3)) = 0.7
        _ShadowColor ("Shadow Color (alpha scales it)", Color) = (0,0,0,0.8)

        _AOBlur ("Contact AO Blur Radius (texels)", Range(0,16)) = 6
        _AOStrength ("Contact AO Strength", Range(0,1)) = 0.0

        // Grade block - identity defaults, so the icon is untouched unless you
        // deliberately dial these.
        _Saturation ("Saturation", Range(0,2)) = 1.0
        _Brightness ("Brightness", Range(0,2)) = 1.0
        _AmbientTint ("Ambient Tint Color", Color) = (0.35,0.4,0.45,1)
        _AmbientTintAmount ("Ambient Tint Amount", Range(0,1)) = 0.0

        // Standard UI plumbing so this still behaves under Canvas/Graphic raycasting etc.
        [HideInInspector] _StencilComp ("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil ("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp ("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255
        [HideInInspector] _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
            "RenderPipeline"="UniversalPipeline"
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
            Name "IconSocket"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            // Per-texture engine-set value, not a per-material property - kept
            // outside the cbuffer like Unity's own shaders do.
            float4 _MainTex_TexelSize;

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;

                float2 _ShadowOffset;
                float _ShadowBlur;
                float _ShadowStrength;
                float _ShadowFalloff;
                float4 _ShadowColor;

                float _AOBlur;
                float _AOStrength;

                float _Saturation;
                float _Brightness;
                float4 _AmbientTint;
                float _AmbientTintAmount;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float4 color       : COLOR;
                float2 uv          : TEXCOORD0;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                OUT.color = IN.color * _Color;
                return OUT;
            }

            // 12-tap (2-ring) circular blur of the alpha channel around uv, radius
            // in texels. Reading straight from _MainTex means the shadow/AO shape
            // automatically matches whatever icon sprite is currently bound.
            float SampleAlphaBlurred(float2 uv, float radiusPx)
            {
                float2 texel = _MainTex_TexelSize.xy;
                float2 r = texel * radiusPx;

                float a = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).a * 3.0;

                // inner ring (full radius)
                a += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2( 1, 0) * r).a;
                a += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(-1, 0) * r).a;
                a += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2( 0, 1) * r).a;
                a += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2( 0,-1) * r).a;
                a += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2( 0.7, 0.7) * r).a;
                a += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(-0.7, 0.7) * r).a;
                a += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2( 0.7,-0.7) * r).a;
                a += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(-0.7,-0.7) * r).a;

                // outer ring (half radius) fills the core so the shadow stays dense
                a += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2( 0.5, 0.5) * r).a;
                a += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(-0.5, 0.5) * r).a;
                a += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2( 0.5,-0.5) * r).a;
                a += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(-0.5,-0.5) * r).a;

                return a / 15.0;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 uv = IN.uv;
                float2 texel = _MainTex_TexelSize.xy;

                half4 icon = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv) * IN.color;

                // ---- cast shadow: blurred + offset alpha of the icon itself ----
                float2 shadowUV = uv - _ShadowOffset * texel;
                float shadowA = SampleAlphaBlurred(shadowUV, _ShadowBlur);
                // falloff < 1 lifts the mid/low alpha so the shadow reads as denser,
                // then strength (can exceed 1) and the color's own alpha scale it.
                shadowA = pow(saturate(shadowA), _ShadowFalloff);
                shadowA = saturate(shadowA * _ShadowStrength * _ShadowColor.a);

                // ---- optional contact AO: blurred alpha, no offset ----
                float aoA = SampleAlphaBlurred(uv, _AOBlur) * _AOStrength;

                // shadow/AO only exist where the icon isn't covering the pixel, so
                // the icon itself is never darkened.
                float bg = saturate(shadowA + aoA) * (1.0 - icon.a);

                // ---- grade the icon (identity by default -> passes through) ----
                float3 c = icon.rgb;
                float luma = dot(c, float3(0.299, 0.587, 0.114));
                c = lerp(float3(luma, luma, luma), c, _Saturation);
                c *= _Brightness;
                c = lerp(c, _AmbientTint.rgb, _AmbientTintAmount);
                c = saturate(c);

                // composite: shadow/AO behind, icon on top (premultiply-style over)
                half4 result;
                result.rgb = lerp(_ShadowColor.rgb, c, icon.a);
                result.a = saturate(bg + icon.a);
                return result;
            }
            ENDHLSL
        }
    }
}
