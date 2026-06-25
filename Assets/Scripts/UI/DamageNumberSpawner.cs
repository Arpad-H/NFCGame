using UnityEngine;

public class DamageNumberSpawner : MonoBehaviour
{
    public static DamageNumberSpawner Instance { get; private set; }

    [SerializeField] private GameObject damageNumberPrefab;
    // Offset applied on top of the card's world position before spawning
    [SerializeField] private Vector3 spawnOffset = new Vector3(0f, 0.3f, 0f);
    // Max random horizontal scatter so simultaneous hits don't overlap
    [SerializeField] private float horizontalScatter = 0.2f;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    public static void Spawn(Vector3 worldPosition, int amount, bool isHeal)
    {
        if (Instance == null || Instance.damageNumberPrefab == null)
        {
            Debug.LogWarning("DamageNumberSpawner: no instance or prefab assigned.");
            return;
        }

        Vector3 scatter = new Vector3(Random.Range(-Instance.horizontalScatter, Instance.horizontalScatter), 0f, 0f);
        Vector3 spawnPos = worldPosition + Instance.spawnOffset + scatter;

        GameObject obj = Instantiate(Instance.damageNumberPrefab, spawnPos, Instance.damageNumberPrefab.transform.rotation);
        obj.GetComponent<DamageNumber>().Setup(amount, isHeal);
    }
}
