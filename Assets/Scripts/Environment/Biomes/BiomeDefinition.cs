using System;
using System.Collections.Generic;
using UnityEngine;

namespace Riftborn.Biomes
{
    // One biome's authoring data. The slot a definition occupies in
    // BiomeManager.biomes (0..5) maps to _Albedo0/_Normal0 .. _Albedo5/_Normal5
    // on the terrain material, so keep the texture slots and this list in order.
    [CreateAssetMenu(menuName = "Riftborn/Biome Definition", fileName = "Biome")]
    public class BiomeDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string displayName = "Biome";
        [Tooltip("Editor-only colour used for gizmos / debugging.")]
        public Color debugColor = Color.green;

        [Header("Surface (pushed to the terrain shader)")]
        [Tooltip("World-space texture tiling. UV = worldXZ * tiling, so 0.1 = one tile every 10 units.")]
        public float tiling = 0.1f;
        [Range(0f, 1f)] public float smoothness = 0.1f;
        [Range(0f, 1f)] public float metallic = 0f;
        [Range(0f, 2f)] public float normalStrength = 1f;

        [Header("Coverage / vignette")]
        [Tooltip("World-unit fade band BEYOND this biome's rectangle edge over which its " +
                 "texture (and foliage) dissolves into the shared neutral base. The box " +
                 "interior is always full element; only the outside fades, so the element " +
                 "is a self-contained pool the size of its box (+ this band), independent " +
                 "of the other biomes. Leave GAPS between boxes wider than ~2x this value " +
                 "and the neutral base shows through in the gap. Bigger = softer/wider edge.")]
        [Min(0f)] public float coverageFade = 10f;

        [Header("Color grade (per-biome, applied before the global grade)")]
        [Tooltip("Multiplied onto this biome's albedo. White = unchanged. Nudge a biome's hue/value here.")]
        public Color tint = Color.white;
        [Range(0f, 2f)]
        [Tooltip("Per-biome saturation. 1 = unchanged, 0 = greyscale, >1 = more vivid. " +
                 "Pull a screaming biome down here, then let the global grade unify the rest.")]
        public float saturation = 1f;

        [Header("Foliage")]
        public List<FoliageEntry> foliage = new();
    }

    [Serializable]
    public class FoliageEntry
    {
        public GameObject prefab;

        [Range(0f, 1f)]
        [Tooltip("Chance to spawn at each candidate point that lands in this biome.")]
        public float spawnChance = 0.5f;

        [Range(0f, 1f)]
        [Tooltip("Skip placement if this biome's blend weight here is below this. " +
                 "Keeps a biome from bleeding its foliage into neighbours at borders.")]
        public float minWeight = 0.4f;

        [Tooltip("Uniform scale is picked randomly in this range and multiplied onto the prefab scale.")]
        public Vector2 scaleRange = new(0.85f, 1.2f);

        [Tooltip("Rotate the instance so its up axis follows the ground normal.")]
        public bool alignToGroundNormal = false;

        [Range(0f, 45f)]
        [Tooltip("Extra random tilt (degrees) for a less uniform look.")]
        public float maxRandomTilt = 0f;
    }
}
