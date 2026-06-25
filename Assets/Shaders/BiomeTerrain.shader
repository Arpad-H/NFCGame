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
        // _BiomeBox[i]:    xy = rectangle half-extents (world units), z = yaw (radians), w = unused
        // _BiomeParams[i]: x = tiling, y = smoothness, z = metallic, w = normal strength
        float4 _BiomeData[6];
        float4 _BiomeBox[6];
        float4 _BiomeParams[6];
        float  _BiomeFalloff;
        int    _BiomeCount;
        float  _BiomeWarpAmp;   // border-wobble max displacement (world units)
        float  _BiomeWarpScale; // border-wobble noise frequency

        // ---- Border-warp noise (domain warping) --------------------------------
        // MUST match Hash2 / ValueNoise / Fbm / Warp in BiomeField.cs. Integer hash
        // (no sin) so CPU foliage and GPU ground agree on where each border lands.
        float Hash2(int x, int y)
        {
            uint h = (uint)x * 374761393u + (uint)y * 668265263u;
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return h / 4294967295.0;
        }

        float ValueNoise(float2 p)
        {
            float2 ip = floor(p);
            int xi = (int)ip.x;
            int yi = (int)ip.y;
            float2 f = p - ip;
            float a = Hash2(xi,     yi);
            float b = Hash2(xi + 1, yi);
            float c = Hash2(xi,     yi + 1);
            float d = Hash2(xi + 1, yi + 1);
            float2 u = f * f * (3.0 - 2.0 * f);
            return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
        }

        float Fbm(float2 p)
        {
            float sum = 0.0;
            float amp = 0.5;
            [unroll]
            for (int o = 0; o < 3; o++)
            {
                sum += amp * ValueNoise(p);
                p *= 2.0;
                amp *= 0.5;
            }
            return sum / 0.875;
        }

        // Domain-warp worldXZ so the rectangle borders become wavy.
        float2 WarpXZ(float2 p)
        {
            if (_BiomeWarpAmp <= 0.0 || _BiomeWarpScale <= 0.0) return p;
            float2 sp = p * _BiomeWarpScale;
            float nx = Fbm(sp);
            float ny = Fbm(sp + float2(113.5, 71.3));
            return p + _BiomeWarpAmp * float2(nx * 2.0 - 1.0, ny * 2.0 - 1.0);
        }

        TEXTURE2D(_Albedo0); TEXTURE2D(_Albedo1); TEXTURE2D(_Albedo2);
        TEXTURE2D(_Albedo3); TEXTURE2D(_Albedo4); TEXTURE2D(_Albedo5);
        TEXTURE2D(_Normal0); TEXTURE2D(_Normal1); TEXTURE2D(_Normal2);
        TEXTURE2D(_Normal3); TEXTURE2D(_Normal4); TEXTURE2D(_Normal5);
        // One inline sampler reused for all 12 maps: stays under the sampler limit
        // AND forces Repeat wrap regardless of each texture's import settings
        // (UVs are world-space, so the ground must tile, not clamp).
        SAMPLER(sampler_linear_repeat);

        // Exterior distance from worldXZ to biome i's rotated rectangle: 0 inside the
        // box, Euclidean distance to the nearest edge outside. MUST match
        // BiomeField.BoxDistance on the CPU side.
        float BoxDistance(float2 worldXZ, int i)
        {
            float yaw = _BiomeBox[i].z;
            float c = cos(yaw);
            float s = sin(yaw);
            float2 rel = worldXZ - _BiomeData[i].xy;
            // Rotate the offset into the box's local frame (inverse Unity Y rotation).
            float2 local = float2(c * rel.x - s * rel.y, s * rel.x + c * rel.y);
            float2 q = max(abs(local) - _BiomeBox[i].xy, 0.0);
            return length(q);
        }

        // MUST match BiomeField.ComputeWeights on the CPU side.
        void ComputeBiomeWeights(float2 worldXZ, out float w[6])
        {
            // Warp membership only; texture UVs still use the real worldXZ.
            worldXZ = WarpXZ(worldXZ);

            float total = 0.0;
            [unroll]
            for (int i = 0; i < 6; i++)
            {
                float d = BoxDistance(worldXZ, i);
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
            #pragma target 3.5 // integer/bitwise ops for the border-warp hash
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
                    albedo += (half3)(w[idx] * SAMPLE_TEXTURE2D(ALB, sampler_linear_repeat, uv).rgb); \
                    half3 n = UnpackNormalScale(SAMPLE_TEXTURE2D(NRM, sampler_linear_repeat, uv), (half)_BiomeParams[idx].w); \
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
