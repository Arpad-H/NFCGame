using UnityEngine;

namespace Riftborn.Biomes
{
    // Shared compositing math for the biome field.
    //
    // Each biome is a rectangle (rotated box). Its presence at a point is its OWN
    // opacity alone: full (1) anywhere inside the rectangle, fading to 0 over a
    // per-biome `coverageFade` band beyond the edge (times the biome's influence
    // multiplier). This is deliberately INDEPENDENT of every other biome — moving
    // a biome does not change where another one reaches. Wherever the summed
    // opacity is < 1 the shared neutral base shows through; where biomes overlap,
    // their opacities are normalized only to decide the hue mix in that overlap.
    // (This replaced an inverse-distance partition whose extents depended on the
    // relative distance between biomes, so two biomes always met in a moving seam
    // instead of each staying a self-contained pool.)
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

        // Per-biome coverage (vignette): 1 inside the rectangle, fading to 0 over a
        // `fade`-wide band beyond the edge, so the element dissolves into the shared
        // neutral base. MUST match Coverage in BiomeTerrain.shader. dist is the box
        // exterior distance from BoxDistance; smoothstep matches HLSL smoothstep(0,fade,dist).
        public static float Coverage(float dist, float fade)
        {
            if (fade <= 0f) return 0f;
            float t = Mathf.Clamp01(dist / fade);
            float s = t * t * (3f - 2f * t); // smoothstep
            return 1f - s;
        }

        // Total element coverage at worldXZ in [0,1]: how much element (vs. neutral base)
        // shows here. Used by foliage to stay out of the neutral zone. Warps the lookup
        // exactly like ComputeWeights / the shader so it lines up with the ground.
        public static float ComputeTotalCoverage(
            Vector2[] centers, Vector2[] halfExtents, float[] yaws, float[] influences, float[] coverageFades,
            float warpAmp, float warpScale, int count, Vector2 worldXZ)
        {
            worldXZ = Warp(worldXZ, warpAmp, warpScale);

            float cov = 0f;
            for (int i = 0; i < count; i++)
            {
                float dist = BoxDistance(worldXZ, centers[i], halfExtents[i], yaws[i]);
                cov += influences[i] * Coverage(dist, coverageFades[i]);
            }
            return Mathf.Min(cov, 1f);
        }

        // Writes the per-biome HUE-MIX weights for worldXZ into result. These are each
        // biome's OWN opacity (influence * own-box coverage) normalized to sum 1, so they
        // only describe the blend between biomes that overlap here — they do NOT describe
        // element-vs-base (that's ComputeTotalCoverage). The first `count` slots contribute.
        // result must have length >= MaxBiomes. MUST match ComputeBiomeWeights in the shader.
        public static void ComputeWeights(
            Vector2[] centers, Vector2[] halfExtents, float[] yaws, float[] influences,
            float[] coverageFades, float warpAmp, float warpScale, int count, Vector2 worldXZ, float[] result)
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
                // Own opacity only: full inside the rectangle, fading over its own band.
                // Independent of every other biome's position.
                float a = influences[i] * Coverage(dist, coverageFades[i]);
                result[i] = a;
                total += a;
            }

            total = Mathf.Max(total, 1e-5f);
            for (int i = 0; i < MaxBiomes; i++)
                result[i] /= total;
        }
    }
}
