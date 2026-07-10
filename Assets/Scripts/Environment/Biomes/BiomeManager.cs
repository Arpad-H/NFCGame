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
            [Tooltip("The invisible marker defining this biome's rectangle: its world XZ " +
                     "position is the centre, its X/Z scale is the rectangle size, and its " +
                     "Y rotation orients the rectangle. Move/scale/rotate it to author the zone.")]
            public Transform center;
            [Tooltip("Opacity / prominence multiplier for this biome's element layer. " +
                     "1 = full strength inside its box. >1 reaches full opacity sooner and " +
                     "wins in overlaps; <1 keeps it more translucent over the neutral base.")]
            [Min(0f)] public float influence = 1f;
        }

        [Tooltip("Order matters: slot index maps to _Albedo0.._Albedo5 on the material.")]
        public BiomeSlot[] biomes = new BiomeSlot[BiomeField.MaxBiomes];

        [Header("Border variety")]
        [Tooltip("Wobble applied to the borders so they aren't perfectly straight, in world " +
                 "units of max displacement. 0 = crisp straight edges.")]
        [Min(0f)] public float warpAmplitude = 6f;
        [Tooltip("Noise frequency of the border wobble. Smaller = larger, smoother waves; " +
                 "larger = busier, choppier edges. ~1 / wavelength in world units.")]
        [Min(0f)] public float warpScale = 0.05f;

        [Header("Neutral base layer")]
        [Tooltip("Shared ground shown wherever no biome covers (the neutral battlefield in the " +
                 "middle). Its TEXTURES go in the material's Base Albedo / Base Normal slots; " +
                 "this definition only supplies the tiling/surface/grade. Leave null for plain defaults.")]
        public BiomeDefinition baseDefinition;

        [Header("Global color grade (applied to the whole composited ground)")]
        [Range(0f, 2f)]
        [Tooltip("Master saturation. <1 calms the board; the 'pick one saturation, let hue vary' rule. 1 = unchanged.")]
        public float globalSaturation = 1f;
        [Range(0f, 1f)]
        [Tooltip("Low end of the unified value range. Raise to lift the darkest areas toward a shared brightness.")]
        public float globalValueMin = 0f;
        [Range(0f, 1f)]
        [Tooltip("High end of the unified value range. Lower to pull bright areas down. " +
                 "min/max squeeze every biome's luminance into one shared band (kills value contrast).")]
        public float globalValueMax = 1f;
        [Range(0f, 2f)]
        [Tooltip("Master brightness multiplier applied last. 1 = unchanged.")]
        public float globalBrightness = 1f;

        [Tooltip("Re-push the layout to the shader every frame. Enable only if the " +
                 "biome centres move at runtime; otherwise it's set once on enable.")]
        public bool updateEveryFrame = false;

        // Authoritative in-memory copy, also used for zero-alloc CPU evaluation.
        private readonly Vector2[] _centers = new Vector2[BiomeField.MaxBiomes];
        private readonly Vector2[] _halfExtents = new Vector2[BiomeField.MaxBiomes];
        private readonly float[] _yaws = new float[BiomeField.MaxBiomes];
        private readonly float[] _influences = new float[BiomeField.MaxBiomes];
        private readonly float[] _coverageFades = new float[BiomeField.MaxBiomes];
        private readonly Vector4[] _gpuData = new Vector4[BiomeField.MaxBiomes];
        private readonly Vector4[] _gpuBox = new Vector4[BiomeField.MaxBiomes];
        private readonly Vector4[] _gpuParams = new Vector4[BiomeField.MaxBiomes];
        private readonly Vector4[] _gpuGrade = new Vector4[BiomeField.MaxBiomes];
        private int _count;

        private static readonly int BiomeDataID = Shader.PropertyToID("_BiomeData");
        private static readonly int BiomeBoxID = Shader.PropertyToID("_BiomeBox");
        private static readonly int BiomeParamsID = Shader.PropertyToID("_BiomeParams");
        private static readonly int BiomeGradeID = Shader.PropertyToID("_BiomeGrade");
        private static readonly int BiomeCountID = Shader.PropertyToID("_BiomeCount");
        private static readonly int BiomeWarpAmpID = Shader.PropertyToID("_BiomeWarpAmp");
        private static readonly int BiomeWarpScaleID = Shader.PropertyToID("_BiomeWarpScale");
        private static readonly int BaseParamsID = Shader.PropertyToID("_BaseParams");
        private static readonly int BaseGradeID = Shader.PropertyToID("_BaseGrade");
        private static readonly int GradeSaturationID = Shader.PropertyToID("_GradeSaturation");
        private static readonly int GradeValueMinID = Shader.PropertyToID("_GradeValueMin");
        private static readonly int GradeValueMaxID = Shader.PropertyToID("_GradeValueMax");
        private static readonly int GradeBrightnessID = Shader.PropertyToID("_GradeBrightness");

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
                    Transform t = slot.center;
                    Vector3 p = t.position;
                    // Rectangle: marker XZ scale is the full size, Y rotation orients it.
                    Vector3 scale = t.lossyScale;
                    Vector2 half = new Vector2(Mathf.Abs(scale.x), Mathf.Abs(scale.z)) * 0.5f;
                    float yaw = t.eulerAngles.y * Mathf.Deg2Rad;
                    float influence = Mathf.Max(0f, slot.influence);
                    BiomeDefinition d = slot.definition;
                    float coverageFade = Mathf.Max(0f, d.coverageFade);

                    _centers[i] = new Vector2(p.x, p.z);
                    _halfExtents[i] = half;
                    _yaws[i] = yaw;
                    _influences[i] = influence;
                    _coverageFades[i] = coverageFade;
                    _gpuData[i] = new Vector4(p.x, p.z, influence, 0f);
                    // box.w carries the coverage fade band (free slot, kept in sync with the shader).
                    _gpuBox[i] = new Vector4(half.x, half.y, yaw, coverageFade);
                    _gpuParams[i] = new Vector4(d.tiling, d.smoothness, d.metallic, d.normalStrength);
                    _gpuGrade[i] = new Vector4(d.tint.r, d.tint.g, d.tint.b, d.saturation);
                    _count = i + 1;
                }
                else
                {
                    // Empty slot: zero influence so it never contributes.
                    _centers[i] = Vector2.zero;
                    _halfExtents[i] = Vector2.zero;
                    _yaws[i] = 0f;
                    _influences[i] = 0f;
                    _coverageFades[i] = 0f;
                    _gpuData[i] = Vector4.zero;
                    _gpuBox[i] = Vector4.zero;
                    _gpuParams[i] = new Vector4(1f, 0f, 0f, 1f);
                    _gpuGrade[i] = new Vector4(1f, 1f, 1f, 1f); // identity tint/saturation
                }
            }

            // Neutral base surface + grade. Textures live on the material (_BaseAlbedo/_BaseNormal);
            // here we only push tiling/surface and the base's own tint/saturation.
            BiomeDefinition b = baseDefinition;
            Vector4 baseParams = b != null
                ? new Vector4(b.tiling, b.smoothness, b.metallic, b.normalStrength)
                : new Vector4(0.1f, 0f, 0f, 1f);
            Vector4 baseGrade = b != null
                ? new Vector4(b.tint.r, b.tint.g, b.tint.b, b.saturation)
                : new Vector4(1f, 1f, 1f, 1f);

            Shader.SetGlobalVectorArray(BiomeDataID, _gpuData);
            Shader.SetGlobalVectorArray(BiomeBoxID, _gpuBox);
            Shader.SetGlobalVectorArray(BiomeParamsID, _gpuParams);
            Shader.SetGlobalVectorArray(BiomeGradeID, _gpuGrade);
            Shader.SetGlobalInt(BiomeCountID, _count);
            Shader.SetGlobalFloat(BiomeWarpAmpID, warpAmplitude);
            Shader.SetGlobalFloat(BiomeWarpScaleID, warpScale);
            Shader.SetGlobalVector(BaseParamsID, baseParams);
            Shader.SetGlobalVector(BaseGradeID, baseGrade);
            Shader.SetGlobalFloat(GradeSaturationID, globalSaturation);
            Shader.SetGlobalFloat(GradeValueMinID, globalValueMin);
            Shader.SetGlobalFloat(GradeValueMaxID, globalValueMax);
            Shader.SetGlobalFloat(GradeBrightnessID, globalBrightness);
        }

        // Normalized blend weights at a world position. result must be length >= MaxBiomes.
        public void EvaluateWeights(Vector3 worldPos, float[] result)
        {
            BiomeField.ComputeWeights(_centers, _halfExtents, _yaws, _influences, _coverageFades,
                warpAmplitude, warpScale, _count, new Vector2(worldPos.x, worldPos.z), result);
        }

        // Total element coverage at a world position in [0,1] (1 = full element, 0 = neutral
        // base). Matches the shader's base-vs-element lerp so foliage can avoid neutral zones.
        public float EvaluateCoverage(Vector3 worldPos)
        {
            return BiomeField.ComputeTotalCoverage(_centers, _halfExtents, _yaws, _influences, _coverageFades,
                warpAmplitude, warpScale, _count, new Vector2(worldPos.x, worldPos.z));
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
            Matrix4x4 prev = Gizmos.matrix;
            foreach (var slot in biomes)
            {
                if (slot?.center == null) continue;
                Color c = slot.definition != null ? slot.definition.debugColor : Color.white;
                c.a = 1f;
                Gizmos.color = c;
                Vector3 pos = slot.center.position;

                // Draw the biome rectangle exactly as the SDF sees it: marker XZ
                // scale = size, marker Y rotation = orientation. Must mirror Refresh().
                Vector3 scale = slot.center.lossyScale;
                float yawDeg = slot.center.eulerAngles.y;
                Gizmos.matrix = Matrix4x4.TRS(pos, Quaternion.Euler(0f, yawDeg, 0f), Vector3.one);
                Gizmos.DrawWireCube(Vector3.zero,
                    new Vector3(Mathf.Abs(scale.x), 0.1f, Mathf.Abs(scale.z)));
                Gizmos.matrix = prev;

                Gizmos.DrawLine(pos, pos + Vector3.up * 5f);
                UnityEditor.Handles.color = c;
                UnityEditor.Handles.Label(pos + Vector3.up * 5.5f,
                    slot.definition != null ? slot.definition.displayName : "(empty)");
            }
            Gizmos.matrix = prev;
        }
#endif
    }
}
