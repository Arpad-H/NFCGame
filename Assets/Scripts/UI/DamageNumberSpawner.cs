using UnityEngine;

public class DamageNumberSpawner : MonoBehaviour
{
    public static DamageNumberSpawner Instance { get; private set; }

    [SerializeField] private GameObject damageNumberPrefab;
    // Offset applied on top of the card's world position before spawning
    [SerializeField] private Vector3 spawnOffset = new Vector3(0f, 0.3f, 0f);
    // Max random horizontal scatter so simultaneous hits don't overlap
    [SerializeField] private float horizontalScatter = 0.2f;
    // Extra push along world X, applied only to the two blows of a lane clash.
    // Those minions have met in the middle and overlap, so their numbers would
    // otherwise land on the same point; each is pushed back toward its owner.
    // A minion damaged while standing in its own slot needs none of this.
    [SerializeField] private float clashSeparation = 0.45f;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    // clashDirection: 0 for a minion hit where it stands (the common case);
    // -1/+1 during a clash, pointing back toward the damaged minion's own side.
    public static void Spawn(Vector3 worldPosition, int amount, bool isHeal, float clashDirection = 0f)
    {
        if (Instance == null || Instance.damageNumberPrefab == null)
        {
            Debug.LogWarning("DamageNumberSpawner: no instance or prefab assigned.");
            return;
        }

        float x = Random.Range(-Instance.horizontalScatter, Instance.horizontalScatter)
                  + clashDirection * Instance.clashSeparation;
        Vector3 spawnPos = worldPosition + Instance.spawnOffset + new Vector3(x, 0f, 0f);

        GameObject obj = Instantiate(Instance.damageNumberPrefab, spawnPos, Instance.damageNumberPrefab.transform.rotation);
        obj.GetComponent<DamageNumber>().Setup(amount, isHeal);
    }
}
