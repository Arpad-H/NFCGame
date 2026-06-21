using System;
using UnityEngine;

namespace Riftborn.Biomes
{
    // Owns the scene's biome layout. Each slot pairs a BiomeDefinition with an
    // (invisible) Transform that acts as the biome's "box of influence" centre.
    //
    // It does two jobs:
    //   1. Pushes the layout to the terrain shader as global arrays, so the
    //      ground blends all biomes via inverse-distance weighting.
    //   2. Answers the same blend query on the CPU (EvaluateWeights) so foliage
    //      and gameplay can ask "which biome is here?".
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class BiomeManager : MonoBehaviour
    {
        [Serializable]
        public class BiomeSlot
        {
            public BiomeDefinition definition;
            [Tooltip("The invisible box/marker whose world XZ position is the biome centre.")]
            public Transform center;
            [Tooltip("Relative pull of this biome. >1 makes it spread further than its neighbours.")]
            [Min(0f)] public float influence = 1f;
        }

        [Tooltip("Order matters: slot index maps to _Albedo0.._Albedo5 on the material.")]
        public BiomeSlot[] biomes = new BiomeSlot[BiomeField.MaxBiomes];

        [Tooltip("How sharply biomes give way to each other. Higher = harder, narrower borders.")]
        [Min(0.1f)] public float falloff = 3f;

        [Tooltip("Re-push the layout to the shader every frame. Enable only if the " +
                 "biome centres move at runtime; otherwise it's set once on enable.")]
        public bool updateEveryFrame = false;

        // Authoritative in-memory copy, also used for zero-alloc CPU evaluation.
        private readonly Vector2[] _centers = new Vector2[BiomeField.MaxBiomes];
        private readonly float[] _influences = new float[BiomeField.MaxBiomes];
        private readonly Vector4[] _gpuData = new Vector4[BiomeField.MaxBiomes];
        private readonly Vector4[] _gpuParams = new Vector4[BiomeField.MaxBiomes];
        private int _count;

        private static readonly int BiomeDataID = Shader.PropertyToID("_BiomeData");
        private static readonly int BiomeParamsID = Shader.PropertyToID("_BiomeParams");
        private static readonly int BiomeFalloffID = Shader.PropertyToID("_BiomeFalloff");
        private static readonly int BiomeCountID = Shader.PropertyToID("_BiomeCount");

        // Number of populated slots (highest filled index + 1).
        public int Count => _count;

        private void OnEnable() => Refresh();
        private void OnValidate() => Refresh();

        private void Update()
        {
            // In the editor (not playing) refresh continuously so dragging the
            // centre markers in the Scene view updates the blend live. At runtime
            // only refresh if the layout is animated.
            if (!Application.isPlaying || updateEveryFrame)
                Refresh();
        }

        // Rebuilds the cached arrays and pushes them to the shader as globals.
        public void Refresh()
        {
            _count = 0;
            for (int i = 0; i < BiomeField.MaxBiomes; i++)
            {
                BiomeSlot slot = (biomes != null && i < biomes.Length) ? biomes[i] : null;
                bool valid = slot != null && slot.center != null && slot.definition != null;

                if (valid)
                {
                    Vector3 p = slot.center.position;
                    float influence = Mathf.Max(0f, slot.influence);
                    BiomeDefinition d = slot.definition;

                    _centers[i] = new Vector2(p.x, p.z);
                    _influences[i] = influence;
                    _gpuData[i] = new Vector4(p.x, p.z, influence, 0f);
                    _gpuParams[i] = new Vector4(d.tiling, d.smoothness, d.metallic, d.normalStrength);
                    _count = i + 1;
                }
                else
                {
                    // Empty slot: zero influence so it never contributes.
                    _centers[i] = Vector2.zero;
                    _influences[i] = 0f;
                    _gpuData[i] = Vector4.zero;
                    _gpuParams[i] = new Vector4(1f, 0f, 0f, 1f);
                }
            }

            Shader.SetGlobalVectorArray(BiomeDataID, _gpuData);
            Shader.SetGlobalVectorArray(BiomeParamsID, _gpuParams);
            Shader.SetGlobalFloat(BiomeFalloffID, falloff);
            Shader.SetGlobalInt(BiomeCountID, _count);
        }

        // Normalized blend weights at a world position. result must be length >= MaxBiomes.
        public void EvaluateWeights(Vector3 worldPos, float[] result)
        {
            BiomeField.ComputeWeights(_centers, _influences, falloff, _count,
                new Vector2(worldPos.x, worldPos.z), result);
        }

        // Convenience: index of the dominant biome at a world position (-1 if none).
        public int DominantBiome(Vector3 worldPos, float[] scratch)
        {
            EvaluateWeights(worldPos, scratch);
            int best = -1;
            float bestW = 0f;
            for (int i = 0; i < _count; i++)
            {
                if (scratch[i] > bestW)
                {
                    bestW = scratch[i];
                    best = i;
                }
            }
            return best;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (biomes == null) return;
            foreach (var slot in biomes)
            {
                if (slot?.center == null) continue;
                Color c = slot.definition != null ? slot.definition.debugColor : Color.white;
                c.a = 1f;
                Gizmos.color = c;
                Vector3 pos = slot.center.position;
                float r = Mathf.Max(0.5f, slot.influence);
                Gizmos.DrawWireSphere(pos, r);
                Gizmos.DrawLine(pos, pos + Vector3.up * 5f);
                UnityEditor.Handles.color = c;
                UnityEditor.Handles.Label(pos + Vector3.up * 5.5f,
                    slot.definition != null ? slot.definition.displayName : "(empty)");
            }
        }
#endif
    }
}
