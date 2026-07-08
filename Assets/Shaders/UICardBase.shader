// UI-Default with a normal map and an analytic light.
//
// A screen-space canvas has no lights and no view vector, so the lighting here
// is faked: _LightDir is a direction in the card's own tangent space (X = right,
// Y = up, Z = toward the viewer), pushed from CardBaseNormalLight.cs so it stays
// fixed in screen space while the card rotates. The viewer is assumed to sit at
// (0,0,1), which is exact for a flat quad facing the camera.
//
// Constraints:
//  - _MainTex is sampled with atlas UVs; _BumpMap has its own 0..1 space. Keep the
//    base sprite OUT of any sprite atlas or the two will disagree.
//  - Image Type must be Simple. 9-slice tiles UVs and will tile the normal map.
//  - Import the normal map as Texture Type: Normal map (Create from Grayscale).
Shader "UI/CardBase"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        [Normal] _BumpMap ("Normal Map", 2D) = "bump" {}
        _BumpScale ("Normal Strength", Range(0, 4)) = 1

        // Overwritten per-card by CardBaseNormalLight. Default: light from upper-left, in front.
        _LightDir ("Light Dir (tangent space)", Vector) = (-0.4, 0.6, 0.7, 0)
        _ReliefStrength ("Relief Strength", Range(0, 2)) = 1
        _SpecStrength ("Specular Strength", Range(0, 2)) = 0.35
        _Gloss ("Gloss", Range(1, 128)) = 24
        _SpecTint ("Specular Tint", Color) = (1,1,1,1)

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
            "CanUseSpriteAtlas" = "False"
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
                float2 bumpcoord     : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4    _MainTex_ST;
            sampler2D _BumpMap;
            float4    _BumpMap_ST;
            half      _BumpScale;

            float4 _LightDir;
            half   _ReliefStrength;
            half   _SpecStrength;
            half   _Gloss;
            fixed4 _SpecTint;

            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                OUT.bumpcoord = TRANSFORM_TEX(v.texcoord, _BumpMap);
                OUT.color = v.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                half4 color = (tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd) * IN.color;

                // Decode the tangent-space normal by hand so _BumpScale works whether
                // or not the texture was imported with DXTnm compression.
                half3 n = UnpackNormal(tex2D(_BumpMap, IN.bumpcoord));
                n.xy *= _BumpScale;
                n.z = sqrt(saturate(1.0 - dot(n.xy, n.xy)));

                half3 L = normalize(_LightDir.xyz);
                const half3 V = half3(0, 0, 1);

                // Half-lambert, remapped so a flat texel (n = +Z) leaves the pixel
                // untouched. Only the cracks and bevels shift.
                half diff = dot(n, L) * 0.5 + 0.5;
                half shade = lerp(1.0, diff * 2.0, _ReliefStrength);
                color.rgb *= shade;

                half3 H = normalize(L + V);
                half spec = pow(saturate(dot(n, H)), _Gloss) * _SpecStrength;
                color.rgb += spec * _SpecTint.rgb * color.a;

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
