using UnityEngine;

namespace Riftborn.Biomes
{
    // Shared inverse-distance-weighting (IDW) math for the biome field.
    //
    // Each biome is a rectangle (rotated box) rather than a point: the distance
    // used for weighting is the distance to the box surface, which is 0 anywhere
    // inside the rectangle. That gives every biome a flat full-intensity interior
    // and pushes all the blending out to the gaps/edges between rectangles, so
    // neighbours only mix near the borders instead of everywhere but a single peak.
    //
    // IMPORTANT: ComputeWeights MUST stay numerically identical to the
    // ComputeBiomeWeights function in BiomeTerrain.shader. The CPU side decides
    // where foliage spawns; the GPU side decides what the ground looks like. If
    // the two formulas drift, foliage will land in the "wrong" colour at borders.
    public static class BiomeField
    {
        // Fixed slot count: the shader unrolls 6 samples, so this is hard-wired.
        public const int MaxBiomes = 6;

        // ---- Border-warp noise (domain warping) ------------------------------
        // Pushes the sample position around with coherent noise before biome
        // membership is evaluated, so the straight rectangle edges become
        // organic, wavy borders. Uses an integer hash (NOT a sin-based one) so
        // the result is identical on the GPU, where the same maths runs per pixel.
        // Hash2 / ValueNoise / Fbm / Warp MUST match BiomeTerrain.shader exactly.

        // 2D integer hash -> [0,1). Pure uint arithmetic: wraps mod 2^32 the same
        // way in C# (unchecked) and HLSL, so CPU and GPU agree bit-for-bit.
        private static float Hash2(int x, int y)
        {
            uint h = (uint)x * 374761393u + (uint)y * 668265263u;
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return h / 4294967295f;
        }

        // Bilinear value noise with smoothstep interpolation, ~[0,1).
        private static float ValueNoise(Vector2 p)
        {
            int xi = Mathf.FloorToInt(p.x);
            int yi = Mathf.FloorToInt(p.y);
            float fx = p.x - xi;
            float fy = p.y - yi;
            float a = Hash2(xi, yi);
            float b = Hash2(xi + 1, yi);
            float c = Hash2(xi, yi + 1);
            float d = Hash2(xi + 1, yi + 1);
            float ux = fx * fx * (3f - 2f * fx);
            float uy = fy * fy * (3f - 2f * fy);
            return Mathf.Lerp(Mathf.Lerp(a, b, ux), Mathf.Lerp(c, d, ux), uy);
        }

        // 3-octave fractal noise, normalized to ~[0,1).
        private static float Fbm(Vector2 p)
        {
            float sum = 0f;
            float amp = 0.5f;
            for (int o = 0; o < 3; o++)
            {
                sum += amp * ValueNoise(p);
                p *= 2f;
                amp *= 0.5f;
            }
            return sum / 0.875f; // 0.5 + 0.25 + 0.125
        }

        // Domain-warp worldXZ. amp = max displacement (world units), scale = noise
        // frequency. Two decorrelated fbm samples drive X and Z independently.
        public static Vector2 Warp(Vector2 p, float amp, float scale)
        {
            if (amp <= 0f || scale <= 0f) return p;
            Vector2 sp = p * scale;
            float nx = Fbm(sp);
            float ny = Fbm(sp + new Vector2(113.5f, 71.3f));
            return p + amp * new Vector2(nx * 2f - 1f, ny * 2f - 1f);
        }

        // Exterior distance from worldXZ to a rotated rectangle. Returns 0 when the
        // point is inside the box (so the biome is at full strength there) and the
        // Euclidean distance to the nearest edge when outside. yaw is in radians.
        // MUST match BoxDistance in BiomeTerrain.shader.
        public static float BoxDistance(Vector2 p, Vector2 center, Vector2 halfExtent, float yaw)
        {
            float c = Mathf.Cos(yaw);
            float s = Mathf.Sin(yaw);
            Vector2 rel = p - center;
            // Rotate the offset into the box's local frame (inverse of a Unity Y rotation).
            Vector2 local = new Vector2(c * rel.x - s * rel.y, s * rel.x + c * rel.y);
            float qx = Mathf.Max(Mathf.Abs(local.x) - halfExtent.x, 0f);
            float qy = Mathf.Max(Mathf.Abs(local.y) - halfExtent.y, 0f);
            return Mathf.Sqrt(qx * qx + qy * qy);
        }

        // Writes normalized (sum == 1) blend weights for worldXZ into result.
        // centers/halfExtents/yaws/influences are indexed by biome slot; only the
        // first `count` slots contribute. result must have length >= MaxBiomes.
        public static void ComputeWeights(
            Vector2[] centers, Vector2[] halfExtents, float[] yaws, float[] influences,
            float falloff, float warpAmp, float warpScale, int count, Vector2 worldXZ, float[] result)
        {
            // Warp the lookup position so all borders become wavy; biome membership
            // (and therefore foliage) follows the same warp the shader uses.
            worldXZ = Warp(worldXZ, warpAmp, warpScale);

            float total = 0f;
            for (int i = 0; i < MaxBiomes; i++)
            {
                if (i >= count)
                {
                    result[i] = 0f;
                    continue;
                }

                float dist = BoxDistance(worldXZ, centers[i], halfExtents[i], yaws[i]);
                // Inverse distance, raised to `falloff` for sharper/softer edges.
                // max(dist, eps) keeps the interior finite (and dominant) instead of
                // dividing by 0; everywhere inside the rectangle resolves to ~1.
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
