using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DamageNumber : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI amountText;
    [SerializeField] private Image background;

    [SerializeField] private Color damageColor = new Color(0.85f, 0.15f, 0.15f);
    [SerializeField] private Color healColor   = new Color(0.15f, 0.85f, 0.25f);

    [SerializeField] private float floatDistance = 1f;
    [SerializeField] private float lifetime = 1.2f;

    public void Setup(int amount, bool isHeal)
    {
        amountText.text = (isHeal ? "+" : "-") + amount;
        if (background != null)
            background.color = isHeal ? healColor : damageColor;
        StartCoroutine(Animate());
    }

    private IEnumerator Animate()
    {
        CanvasGroup group = GetComponent<CanvasGroup>();
        if (group == null) group = gameObject.AddComponent<CanvasGroup>();

        Vector3 startPos = transform.position;
        float elapsed = 0f;

        while (elapsed < lifetime)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / lifetime;
            transform.position = startPos + Vector3.up * (floatDistance * t);
            group.alpha = 1f - t * t; // accelerates toward transparent
            yield return null;
        }

        Destroy(gameObject);
    }
}
