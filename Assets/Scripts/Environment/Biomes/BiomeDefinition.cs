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
