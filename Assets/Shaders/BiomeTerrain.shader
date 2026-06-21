// Blends up to 6 biomes across a flat ground mesh using inverse-distance
// weighting from per-biome world-space centres. Centres, per-biome surface
// params, falloff and count are pushed as shader globals by BiomeManager.cs.
//
// Use on a regular mesh (large plane / subdivided quad), NOT the Unity Terrain
// component — Terrain has its own splat-based shader contract.
//
// Textures use a single shared sampler so all 12 maps cost one sampler slot,
// staying well under the 16-sampler limit.
Shader "Riftborn/BiomeTerrain"
{
    Properties
    {
        [NoScaleOffset] _Albedo0 ("Biome 0 Albedo", 2D) = "white" {}
        [NoScaleOffset] _Albedo1 ("Biome 1 Albedo", 2D) = "white" {}
        [NoScaleOffset] _Albedo2 ("Biome 2 Albedo", 2D) = "white" {}
        [NoScaleOffset] _Albedo3 ("Biome 3 Albedo", 2D) = "white" {}
        [NoScaleOffset] _Albedo4 ("Biome 4 Albedo", 2D) = "white" {}
        [NoScaleOffset] _Albedo5 ("Biome 5 Albedo", 2D) = "white" {}

        [NoScaleOffset][Normal] _Normal0 ("Biome 0 Normal", 2D) = "bump" {}
        [NoScaleOffset][Normal] _Normal1 ("Biome 1 Normal", 2D) = "bump" {}
        [NoScaleOffset][Normal] _Normal2 ("Biome 2 Normal", 2D) = "bump" {}
        [NoScaleOffset][Normal] _Normal3 ("Biome 3 Normal", 2D) = "bump" {}
        [NoScaleOffset][Normal] _Normal4 ("Biome 4 Normal", 2D) = "bump" {}
        [NoScaleOffset][Normal] _Normal5 ("Biome 5 Normal", 2D) = "bump" {}
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        LOD 300

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        // ---- Scene-global biome layout (set by BiomeManager.Refresh) ----
        // _BiomeData[i]:   xy = world XZ centre, z = influence, w = unused
        // _BiomeParams[i]: x = tiling, y = smoothness, z = metallic, w = normal strength
        float4 _BiomeData[6];
        float4 _BiomeParams[6];
        float  _BiomeFalloff;
        int    _BiomeCount;

        TEXTURE2D(_Albedo0); TEXTURE2D(_Albedo1); TEXTURE2D(_Albedo2);
        TEXTURE2D(_Albedo3); TEXTURE2D(_Albedo4); TEXTURE2D(_Albedo5);
        TEXTURE2D(_Normal0); TEXTURE2D(_Normal1); TEXTURE2D(_Normal2);
        TEXTURE2D(_Normal3); TEXTURE2D(_Normal4); TEXTURE2D(_Normal5);
        // One sampler reused for all 12 maps (wrap/filter come from _Albedo0's import settings).
        SAMPLER(sampler_Albedo0);

        // MUST match BiomeField.ComputeWeights on the CPU side.
        void ComputeBiomeWeights(float2 worldXZ, out float w[6])
        {
            float total = 0.0;
            [unroll]
            for (int i = 0; i < 6; i++)
            {
                float d = distance(worldXZ, _BiomeData[i].xy);
                float wi = _BiomeData[i].z / pow(max(d, 1e-3), _BiomeFalloff);
                wi = (i < _BiomeCount) ? wi : 0.0;
                w[i] = wi;
                total += wi;
            }
            total = max(total, 1e-5);
            [unroll]
            for (int j = 0; j < 6; j++) w[j] /= total;
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
            #pragma target 3.0
            #pragma multi_compile_instancing

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ _FORWARD_PLUS
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float4 tangentWS  : TEXCOORD2; // xyz = tangent, w = bitangent sign
                float  fogFactor  : TEXCOORD3;
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
                OUT.fogFactor = ComputeFogFactor(posInputs.positionCS.z);
                return OUT;
            }

            half4 Frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                float2 worldXZ = IN.positionWS.xz;
                float w[6];
                ComputeBiomeWeights(worldXZ, w);

                half3 albedo = half3(0, 0, 0);
                half3 normalTS = half3(0, 0, 0);
                half smoothness = 0;
                half metallic = 0;

                #define ACCUM_BIOME(idx, ALB, NRM) \
                { \
                    float2 uv = worldXZ * _BiomeParams[idx].x; \
                    albedo += (half3)(w[idx] * SAMPLE_TEXTURE2D(ALB, sampler_Albedo0, uv).rgb); \
                    half3 n = UnpackNormalScale(SAMPLE_TEXTURE2D(NRM, sampler_Albedo0, uv), (half)_BiomeParams[idx].w); \
                    normalTS += (half3)(w[idx] * n); \
                    smoothness += (half)(w[idx] * _BiomeParams[idx].y); \
                    metallic += (half)(w[idx] * _BiomeParams[idx].z); \
                }
                ACCUM_BIOME(0, _Albedo0, _Normal0)
                ACCUM_BIOME(1, _Albedo1, _Normal1)
                ACCUM_BIOME(2, _Albedo2, _Normal2)
                ACCUM_BIOME(3, _Albedo3, _Normal3)
                ACCUM_BIOME(4, _Albedo4, _Normal4)
                ACCUM_BIOME(5, _Albedo5, _Normal5)
                #undef ACCUM_BIOME

                normalTS = normalize(normalTS + half3(0, 0, 1e-4));

                // Tangent -> world normal.
                float sgn = IN.tangentWS.w;
                float3 bitangent = sgn * cross(IN.normalWS, IN.tangentWS.xyz);
                half3x3 tbn = half3x3(IN.tangentWS.xyz, bitangent, IN.normalWS);
                half3 normalWS = NormalizeNormalPerPixel(mul(normalTS, tbn));

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
                surfaceData.metallic = saturate(metallic);
                surfaceData.smoothness = saturate(smoothness);
                surfaceData.normalTS = normalTS;
                surfaceData.occlusion = 1.0;
                surfaceData.alpha = 1.0;

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
            struct V { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; };

            V DNVert(A input)
            {
                V o;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return o;
            }

            half4 DNFrag(V input) : SV_Target
            {
                return half4(NormalizeNormalPerPixel(input.normalWS), 0.0);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
