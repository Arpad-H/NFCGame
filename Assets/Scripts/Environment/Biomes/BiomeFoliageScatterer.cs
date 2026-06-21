using UnityEngine;

namespace Riftborn.Biomes
{
    // Scatters foliage over a world-XZ area, choosing prefabs per the same biome
    // blend the terrain shader uses. Candidate points come from a jittered grid;
    // each is assigned a biome by weighted-random pick, then dropped onto the
    // ground with a raycast.
    public class BiomeFoliageScatterer : MonoBehaviour
    {
        [Header("References")]
        public BiomeManager biomeManager;

        [Header("Area (world XZ)")]
        public Vector2 areaCenter = Vector2.zero;
        public Vector2 areaSize = new(100f, 100f);

        [Tooltip("Candidate-point spacing. Smaller = denser potential foliage and slower generation.")]
        [Min(0.25f)] public float spacing = 2f;

        [Range(0f, 1f)]
        [Tooltip("Random offset per point as a fraction of spacing, so the result isn't visibly gridded.")]
        public float jitter = 0.85f;

        [Header("Ground")]
        [Tooltip("Layers the downward raycast will hit when looking for ground.")]
        public LayerMask groundMask = ~0;
        [Tooltip("Ray starts this far above y=0 and casts down twice this distance.")]
        public float rayHeight = 200f;

        [Header("Determinism")]
        [Tooltip("Use a fresh random seed on every play (true) or a fixed, repeatable one (false).")]
        public bool randomizeSeedEachPlay = true;
        public int seed = 12345;

        private Transform _container;
        private readonly float[] _weights = new float[BiomeField.MaxBiomes];

        private const string ContainerName = "Foliage";

        private void Start() => Generate();

        [ContextMenu("Generate")]
        public void Generate()
        {
            if (biomeManager == null)
            {
                Debug.LogWarning("BiomeFoliageScatterer: no BiomeManager assigned.", this);
                return;
            }

            biomeManager.Refresh();
            Clear();

            _container = new GameObject(ContainerName).transform;
            _container.SetParent(transform, false);

            var rng = new System.Random(randomizeSeedEachPlay ? System.Environment.TickCount : seed);

            Vector2 min = areaCenter - areaSize * 0.5f;
            int nx = Mathf.CeilToInt(areaSize.x / spacing);
            int nz = Mathf.CeilToInt(areaSize.y / spacing);

            for (int ix = 0; ix < nx; ix++)
            {
                for (int iz = 0; iz < nz; iz++)
                {
                    float jx = ((float)rng.NextDouble() - 0.5f) * jitter * spacing;
                    float jz = ((float)rng.NextDouble() - 0.5f) * jitter * spacing;
                    float wx = min.x + (ix + 0.5f) * spacing + jx;
                    float wz = min.y + (iz + 0.5f) * spacing + jz;

                    biomeManager.EvaluateWeights(new Vector3(wx, 0f, wz), _weights);

                    int biome = PickBiome(rng);
                    if (biome < 0) continue;

                    var def = biomeManager.biomes[biome]?.definition;
                    if (def == null || def.foliage.Count == 0) continue;

                    var entry = def.foliage[rng.Next(def.foliage.Count)];
                    if (entry.prefab == null) continue;
                    if (_weights[biome] < entry.minWeight) continue;
                    if (rng.NextDouble() > entry.spawnChance) continue;

                    if (!Physics.Raycast(new Vector3(wx, rayHeight, wz), Vector3.down,
                            out RaycastHit hit, rayHeight * 2f, groundMask, QueryTriggerInteraction.Ignore))
                        continue;

                    Place(entry, hit, rng);
                }
            }
        }

        // Weighted-random biome by current _weights. Returns -1 if there is no weight.
        private int PickBiome(System.Random rng)
        {
            float total = 0f;
            for (int i = 0; i < biomeManager.Count; i++) total += _weights[i];
            if (total <= 0f) return -1;

            float r = (float)rng.NextDouble() * total;
            float acc = 0f;
            for (int i = 0; i < biomeManager.Count; i++)
            {
                acc += _weights[i];
                if (r <= acc) return i;
            }
            return biomeManager.Count - 1;
        }

        private void Place(FoliageEntry entry, RaycastHit hit, System.Random rng)
        {
            var go = Instantiate(entry.prefab, hit.point, Quaternion.identity, _container);

            Quaternion rot = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
            if (entry.alignToGroundNormal)
                rot = Quaternion.FromToRotation(Vector3.up, hit.normal) * rot;

            if (entry.maxRandomTilt > 0f)
            {
                float tilt = (float)rng.NextDouble() * entry.maxRandomTilt;
                float dir = (float)rng.NextDouble() * 360f * Mathf.Deg2Rad;
                rot *= Quaternion.Euler(Mathf.Cos(dir) * tilt, 0f, Mathf.Sin(dir) * tilt);
            }
            go.transform.rotation = rot;

            float s = Mathf.Lerp(entry.scaleRange.x, entry.scaleRange.y, (float)rng.NextDouble());
            go.transform.localScale *= s;
        }

        [ContextMenu("Clear")]
        public void Clear()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (child.name != ContainerName) continue;
                if (Application.isPlaying) Destroy(child.gameObject);
                else DestroyImmediate(child.gameObject);
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(
                new Vector3(areaCenter.x, transform.position.y, areaCenter.y),
                new Vector3(areaSize.x, 0.1f, areaSize.y));
        }
#endif
    }
}
