// Lit floor shader that carves a procedural grid of grooves into the surface.
//
// The grooves are generated in WORLD XZ (not mesh UV), so they tile seamlessly,
// stay a fixed real-world size no matter how the plane is scaled, and line up
// across separate meshes. Each groove perturbs the surface NORMAL (so scene
// light catches the walls, exactly like the reference seam), darkens the albedo,
// and adds ambient occlusion in the channel. The DepthNormals pass writes the
// grooved normal too, so the project's SSAO deepens the grooves for free.
//
// Use on a flat, horizontal floor mesh (the arena plane). The groove normal is
// built assuming up = +Y; on a steeply tilted mesh the tilt direction would be
// off. Base albedo/normal use the mesh UVs so this can replace M_Arena and keep
// its textures; only the grid rides on world space.
Shader "Riftborn/ArenaGrooveGrid"
{
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
        _BaseColor ("Tint", Color) = (1, 1, 1, 1)
        [Normal] _BumpMap ("Base Normal", 2D) = "bump" {}
        _BumpScale ("Base Normal Scale", Float) = 1
        _Smoothness ("Smoothness", Range(0, 1)) = 0.2
        _Metallic ("Metallic", Range(0, 1)) = 0

        [Header(Groove Grid)]
        [Space(4)]
        _CellSize ("Cell Size XZ (world units)", Vector) = (1, 1, 0, 0)
        _GrooveWidth ("Groove Width (world units)", Float) = 0.06
        _GrooveDepth ("Groove Depth (normal tilt)", Range(0, 4)) = 1
        _GrooveDarkness ("Groove Albedo Darken", Range(0, 1)) = 0.55
        _GrooveAO ("Groove Ambient Occlusion", Range(0, 1)) = 0.5
        _GrooveColor ("Groove Tint", Color) = (0.35, 0.28, 0.18, 1)
        _GrooveColorBlend ("Groove Tint Blend", Range(0, 1)) = 0.25
        _GrooveSmoothness ("Groove Smoothness", Range(0, 1)) = 0.1

        [Header(Unevenness)]
        [Space(4)]
        _GrooveWaviness ("Waviness (world units)", Float) = 0.15
        _GrooveNoiseScale ("Noise Scale", Float) = 0.5
        _GrooveUnevenness ("Depth Unevenness", Range(0, 1)) = 0.6
        _GridOffset ("Grid Offset XZ", Vector) = (0, 0, 0, 0)
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        LOD 300

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        // All material properties in one CBUFFER shared across passes -> SRP Batcher compatible.
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4  _BaseColor;
            half   _BumpScale;
            half   _Smoothness;
            half   _Metallic;
            float4 _CellSize;
            float  _GrooveWidth;
            half   _GrooveDepth;
            half   _GrooveDarkness;
            half   _GrooveAO;
            half4  _GrooveColor;
            half   _GrooveColorBlend;
            half   _GrooveSmoothness;
            float  _GrooveWaviness;
            float  _GrooveNoiseScale;
            half   _GrooveUnevenness;
            float4 _GridOffset;
        CBUFFER_END

        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);

        // ---- Value noise for organic groove variation --------------------------
        // Coherent 2D noise (frac hash -> bilinear -> 3-octave fBm). Used to wander
        // the grid lines and vary their depth so they read as hand-cracked seams
        // rather than ruler-straight lines. No bitwise ops, so no target 3.5 needed.
        float Hash21(float2 p)
        {
            p = frac(p * float2(123.34, 345.45));
            p += dot(p, p + 34.345);
            return frac(p.x * p.y);
        }

        float ValueNoise(float2 p)
        {
            float2 i = floor(p);
            float2 f = frac(p);
            float2 w = f * f * (3.0 - 2.0 * f);
            float a = Hash21(i);
            float b = Hash21(i + float2(1.0, 0.0));
            float c = Hash21(i + float2(0.0, 1.0));
            float d = Hash21(i + float2(1.0, 1.0));
            return lerp(lerp(a, b, w.x), lerp(c, d, w.x), w.y);
        }

        float Fbm(float2 p)
        {
            float s = 0.0;
            float amp = 0.5;
            [unroll]
            for (int o = 0; o < 3; o++)
            {
                s += amp * ValueNoise(p);
                p *= 2.0;
                amp *= 0.5;
            }
            return s / 0.875;
        }

        // Procedural groove grid from world XZ.
        //   grooveNormalWS : world-space tilt to ADD to the surface normal (y = 0).
        //   mask           : 0 on the flat surface .. 1 at a groove bottom (for AO / albedo).
        void ComputeGrooveGrid(float3 positionWS, out float3 grooveNormalWS, out float mask)
        {
            // Independent X and Z spacing -> rectangular cells.
            float2 cell = max(_CellSize.xy, 1e-4);
            // Keep the groove from swallowing the whole cell (per axis).
            float2 halfW = clamp(_GrooveWidth * 0.5, 1e-4, cell * 0.49);

            // Wander: displace the sample position with coherent noise before the grid
            // is evaluated, so the straight lines become wavy, organic seams.
            float2 warp = float2(0.0, 0.0);
            if (_GrooveWaviness > 0.0)
            {
                float2 sp = positionWS.xz * _GrooveNoiseScale;
                warp.x = Fbm(sp) * 2.0 - 1.0;
                warp.y = Fbm(sp + float2(71.3, 19.7)) * 2.0 - 1.0;
                warp *= _GrooveWaviness;
            }

            // Signed position within the cell: 0 exactly on a grid line, +/-0.5 mid-cell.
            float2 p = (positionWS.xz + _GridOffset.xy + warp) / cell;
            float2 u = frac(p + 0.5) - 0.5;
            float2 dLine = abs(u) * cell;             // world distance to the nearest line, per axis

            // Uneven depth/width ALONG the groove: stretches run deep and wide, others
            // fade to almost nothing, like a real crack breathing along its length.
            float uneven = 1.0;
            if (_GrooveUnevenness > 0.0)
            {
                float n = Fbm(positionWS.xz * _GrooveNoiseScale * 0.5 + float2(37.1, 11.9));
                uneven = lerp(1.0, saturate(n * 1.4), _GrooveUnevenness);
            }
            float2 hw = max(halfW * uneven, 1e-4);

            // Anti-aliased membership: 1 inside the groove wall, fading over one pixel at the rim.
            float2 aa = fwidth(dLine) + 1e-5;
            float2 inG = 1.0 - smoothstep(hw - aa, hw + aa, dLine);

            // Valley depth: 1 at the line centre, 0 at the groove edge. Deepest point drives AO.
            float2 valley = saturate(1.0 - dLine / hw) * inG;
            mask = max(valley.x, valley.y) * uneven;

            // Flat-chamfer walls: constant tilt across each wall, flipping sign at the
            // groove centre to form the V. inG fades the tilt in at the rim so there is
            // no hard normal seam where the groove meets the flat surface. Scaled by
            // `uneven` so shallow stretches tilt less.
            float2 slope = sign(u) * inG;
            grooveNormalWS = float3(-slope.x, 0.0, -slope.y) * uneven;
        }
        ENDHLSL

        // ---------------------------------------------------------------------
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
            #pragma multi_compile _ _FORWARD_PLUS
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DBuffer.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float4 tangentWS  : TEXCOORD2; // xyz = tangent, w = bitangent sign
                float2 uv         : TEXCOORD3;
                float  fogFactor  : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes IN)
            {
                Varyings OUT = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                VertexPositionInputs posInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs nrmInputs = GetVertexNormalInputs(IN.normalOS, IN.tangentOS);

                OUT.positionCS = posInputs.positionCS;
                OUT.positionWS = posInputs.positionWS;
                OUT.normalWS = nrmInputs.normalWS;
                OUT.tangentWS = float4(nrmInputs.tangentWS, IN.tangentOS.w * GetOddNegativeScale());
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                OUT.fogFactor = ComputeFogFactor(posInputs.positionCS.z);
                return OUT;
            }

            half4 Frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                // ---- Base surface (mesh UV) ----
                half4 baseTex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);
                half3 albedo = baseTex.rgb * _BaseColor.rgb;
                half3 baseNormalTS = UnpackNormalScale(
                    SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, IN.uv), _BumpScale);

                // Base tangent-space normal -> world.
                float sgn = IN.tangentWS.w;
                float3 bitangent = sgn * cross(IN.normalWS, IN.tangentWS.xyz);
                half3x3 tbn = half3x3(IN.tangentWS.xyz, bitangent, IN.normalWS);
                float3 baseNormalWS = NormalizeNormalPerPixel(mul(baseNormalTS, tbn));

                // ---- Groove grid (world XZ) ----
                float3 grooveNWS;
                float grooveMask;
                ComputeGrooveGrid(IN.positionWS, grooveNWS, grooveMask);

                float3 normalWS = NormalizeNormalPerPixel(baseNormalWS + grooveNWS * _GrooveDepth);

                // Darken + tint the channel, and matte it down a touch (dust in the crevice).
                albedo *= lerp(1.0h, _GrooveDarkness, grooveMask);
                albedo = lerp(albedo, _GrooveColor.rgb, grooveMask * _GrooveColorBlend);
                half smoothness = lerp(_Smoothness, _GrooveSmoothness, grooveMask);
                half occlusion = lerp(1.0h, _GrooveAO, grooveMask);

                // ---- Standard URP PBR lighting ----
                InputData inputData = (InputData)0;
                inputData.positionWS = IN.positionWS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(IN.positionWS);
            #if defined(MAIN_LIGHT_CALCULATE_SHADOWS)
                inputData.shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
            #else
                inputData.shadowCoord = float4(0, 0, 0, 0);
            #endif
                inputData.fogCoord = IN.fogFactor;
                inputData.bakedGI = SampleSH(normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(IN.positionCS);
                inputData.shadowMask = half4(1, 1, 1, 1);

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = saturate(albedo);
                surfaceData.metallic = saturate(_Metallic);
                surfaceData.smoothness = saturate(smoothness);
                surfaceData.normalTS = half3(0, 0, 1);
                surfaceData.occlusion = occlusion;
                surfaceData.alpha = 1.0;

                // Receive URP DBuffer decals (the rune projectors) so they blend onto the
                // floor's albedo + normal on top of the groove grid. Must come after both
                // surfaceData and inputData.normalWS are filled and before lighting.
            #if defined(_DBUFFER)
                ApplyDecalToSurfaceData(IN.positionCS, surfaceData, inputData);
            #endif

                half4 color = UniversalFragmentPBR(inputData, surfaceData);
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                return color;
            }
            ENDHLSL
        }

        // ---------------------------------------------------------------------
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }

            ZWrite On ZTest LEqual ColorMask 0 Cull Back

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct V { float4 positionCS : SV_POSITION; };

            float4 GetShadowPositionHClip(A input)
            {
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
            #else
                float3 lightDirectionWS = _LightDirection;
            #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
            #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #endif
                return positionCS;
            }

            V ShadowVert(A input)
            {
                V o;
                o.positionCS = GetShadowPositionHClip(input);
                return o;
            }

            half4 ShadowFrag(V input) : SV_Target { return 0; }
            ENDHLSL
        }

        // ---------------------------------------------------------------------
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }

            ZWrite On ColorMask 0 Cull Back

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag

            struct A { float4 positionOS : POSITION; };
            struct V { float4 positionCS : SV_POSITION; };

            V DepthVert(A input)
            {
                V o;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return o;
            }

            half DepthFrag(V input) : SV_Target { return 0; }
            ENDHLSL
        }

        // ---------------------------------------------------------------------
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }

            ZWrite On Cull Back

            HLSLPROGRAM
            #pragma vertex DNVert
            #pragma fragment DNFrag

            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct V
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
            };

            V DNVert(A input)
            {
                V o;
                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                o.positionCS = posInputs.positionCS;
                o.positionWS = posInputs.positionWS;
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return o;
            }

            half4 DNFrag(V input) : SV_Target
            {
                // Feed the grooved normal to SSAO so it deepens the channels too.
                float3 grooveNWS;
                float grooveMask;
                ComputeGrooveGrid(input.positionWS, grooveNWS, grooveMask);
                float3 n = NormalizeNormalPerPixel(input.normalWS + grooveNWS * _GrooveDepth);
                return half4(n, 0.0);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
