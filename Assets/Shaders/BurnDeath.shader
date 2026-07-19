// Burn-to-ash death shader for fielded minions (Riftborn).
//
// Fed a FLATTENED snapshot of the dying board token (rendered to a transparent
// RenderTexture by BurnDeathEffect) as _MainTex. Drives a single float
// _BurnAmount 0->1: the sheet dissolves from one edge, a hot ember band rides
// the burn front (white->yellow->orange with a violet arcane fringe), the paper
// chars to near-black just ahead of the front, and everything behind the front
// crumbles to nothing (ash — the drifting motes are a particle system, not this
// shader). Warm on purpose so it sits on the amber board.
//
// URP unlit, transparent, two-sided (the token is viewed straight top-down).
// All colours/widths are pushed per-instance through a MaterialPropertyBlock, so
// one shared material serves every simultaneous burn with no shared-state mutation.
Shader "Riftborn/BurnDeath"
{
    Properties
    {
        [MainTexture] _MainTex ("Flattened Minion (RT)", 2D) = "white" {}
        _BurnAmount ("Burn Amount", Range(0,1)) = 0

        _NoiseScale ("Noise Scale", Float) = 6
        _NoiseAmp   ("Noise Amplitude", Range(0,1)) = 0.35
        _EdgeWidth  ("Ember Edge Width", Range(0.001,0.5)) = 0.09
        _CharWidth  ("Char Width", Range(0.001,0.6)) = 0.16
        _AshWidth   ("Ash Crumble Width", Range(0.001,0.6)) = 0.10
        _CharDarkness ("Char Darkness", Range(0,1)) = 0.06
        _EmberIntensity ("Ember Intensity", Float) = 3.0
        // Direction (in UV space) the burn climbs toward. Default: from the bottom
        // edge (v=0) upward toward v=1, like paper catching from below.
        _BurnDir ("Burn Direction (uv)", Vector) = (0,1,0,0)
        // Mirrors the whole effect vertically (art + burn), to undo a platform
        // camera->RenderTexture flip. Set by BurnDeathEffect.flipCaptureV.
        [Toggle] _FlipV ("Flip V", Float) = 0

        [HDR] _EmberWhite  ("Ember Core (white/yellow)", Color) = (1,0.95,0.6,1)
        [HDR] _EmberYellow ("Ember Yellow", Color) = (1,0.72,0.2,1)
        [HDR] _EmberOrange ("Ember Orange", Color) = (1,0.36,0.06,1)
        [HDR] _EmberViolet ("Ember Violet Fringe", Color) = (0.55,0.16,0.85,1)
        _CharColor  ("Char Color", Color) = (0.02,0.02,0.03,1)
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Transparent"
            "Queue"="Transparent"
            "RenderPipeline"="UniversalPipeline"
            "IgnoreProjector"="True"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest LEqual
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float  _BurnAmount, _NoiseScale, _NoiseAmp, _EdgeWidth;
                float  _CharWidth, _AshWidth, _CharDarkness, _EmberIntensity, _FlipV;
                float4 _BurnDir;
                float4 _EmberWhite, _EmberYellow, _EmberOrange, _EmberViolet, _CharColor;
            CBUFFER_END

            // --- procedural value-noise fBm (no _NoiseTex asset needed) ---------
            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 345.45));
                p += dot(p, p + 34.345);
                return frac(p.x * p.y);
            }

            float vnoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                float a = hash21(i);
                float b = hash21(i + float2(1, 0));
                float c = hash21(i + float2(0, 1));
                float d = hash21(i + float2(1, 1));
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            float fbm(float2 p)
            {
                float v = 0.0;
                float amp = 0.5;
                [unroll] for (int i = 0; i < 4; i++)
                {
                    v += amp * vnoise(p);
                    p *= 2.0;
                    amp *= 0.5;
                }
                return v;
            }

            // Ember hue by heat: x=0 cool outer fringe (violet) -> x=1 white-hot core.
            float3 EmberRamp(float x)
            {
                float3 c = lerp(_EmberViolet.rgb, _EmberOrange.rgb, smoothstep(0.0, 0.35, x));
                c = lerp(c, _EmberYellow.rgb, smoothstep(0.35, 0.7, x));
                c = lerp(c, _EmberWhite.rgb,  smoothstep(0.7, 1.0, x));
                return c;
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;                  // flip + tiling applied in frag
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // One uv for both the art and the burn field, so a V-flip mirrors
                // the whole effect coherently.
                float2 uv = IN.uv;
                if (_FlipV > 0.5) uv.y = 1.0 - uv.y;
                uv = TRANSFORM_TEX(uv, _MainTex);

                half4 albedo = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);

                // Position along the burn axis (0 at the edge that ignites first),
                // jittered by fBm so the front is a ragged paper edge, not a line.
                float2 dir = normalize(_BurnDir.xy + float2(1e-5, 1e-5));
                float axis = dot(uv - 0.5, dir) + 0.5;
                float n = fbm(uv * _NoiseScale);
                float t = saturate(axis + (n - 0.5) * _NoiseAmp);

                // Burn front sweeps past [-edge] (nothing lit at amount 0) through
                // to fully past the sheet at amount 1.
                float front = lerp(-_EdgeWidth, 1.0 + _EdgeWidth + _AshWidth, _BurnAmount);
                float d = t - front; // >0 intact ahead of the flame, <0 consumed behind

                // Anything well behind the front has crumbled to ash — drop it.
                if (d < -_AshWidth) discard;

                // Sheet opacity: solid ahead of the flame, fading to nothing as it
                // crumbles behind. Gated by the token's own silhouette alpha.
                float sheet = smoothstep(-_AshWidth, 0.0, d) * albedo.a;

                // Char: darken the paper toward soot as the flame approaches and
                // just after it passes. _CharDarkness sets how much of the original
                // art survives in the charred band (0 ~ pure soot, 1 ~ art kept).
                float charT = 1.0 - smoothstep(0.0, _CharWidth, abs(d));
                float3 charTarget = albedo.rgb * _CharDarkness + _CharColor.rgb;
                float3 paper = lerp(albedo.rgb, charTarget, charT);

                // Ember band straddling the front, only within the silhouette.
                float band = 1.0 - saturate(abs(d) / _EdgeWidth);
                float heat = smoothstep(0.0, 1.0, band);
                float emberA = heat * step(0.003, albedo.a);
                float3 ember = EmberRamp(heat) * heat * _EmberIntensity;

                float outA = max(sheet, emberA);
                if (outA < 0.003) discard;

                // Straight-alpha: charred paper + additive-feeling ember on top
                // (ember rgb can exceed 1 so scene bloom catches the hot edge).
                float3 outC = paper + ember;
                return half4(outC, outA);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
