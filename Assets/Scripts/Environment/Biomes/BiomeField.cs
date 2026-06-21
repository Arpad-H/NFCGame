using UnityEngine;

namespace Riftborn.Biomes
{
    // Shared inverse-distance-weighting (IDW) math for the biome field.
    //
    // IMPORTANT: ComputeWeights MUST stay numerically identical to the
    // ComputeBiomeWeights function in BiomeTerrain.shader. The CPU side decides
    // where foliage spawns; the GPU side decides what the ground looks like. If
    // the two formulas drift, foliage will land in the "wrong" colour at borders.
    public static class BiomeField
    {
        // Fixed slot count: the shader unrolls 6 samples, so this is hard-wired.
        public const int MaxBiomes = 6;

        // Writes normalized (sum == 1) blend weights for worldXZ into result.
        // centers/influences are indexed by biome slot; only the first `count`
        // slots contribute. result must have length >= MaxBiomes.
        public static void ComputeWeights(
            Vector2[] centers, float[] influences, float falloff, int count,
            Vector2 worldXZ, float[] result)
        {
            float total = 0f;
            for (int i = 0; i < MaxBiomes; i++)
            {
                if (i >= count)
                {
                    result[i] = 0f;
                    continue;
                }

                float dist = Vector2.Distance(worldXZ, centers[i]);
                // Inverse distance, raised to `falloff` for sharper/softer edges.
                // max(dist, eps) keeps the centre finite instead of dividing by 0.
                float w = influences[i] / Mathf.Pow(Mathf.Max(dist, 1e-3f), falloff);
                result[i] = w;
                total += w;
            }

            total = Mathf.Max(total, 1e-5f);
            for (int i = 0; i < MaxBiomes; i++)
                result[i] /= total;
        }
    }
}
