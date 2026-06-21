using System.Collections;
using System.Threading.Tasks;
using GameSystems;
using UnityEngine;

// Plays the cast animation for spell cards. Spells are never fielded; instead
// the card slides up from the bottom of the screen to the center over a fixed
// duration, then is removed. GameManager evaluates the spell effect only after
// Play() completes (the card is already gone by then).
public class SpellCastAnimator : MonoBehaviour
{
    private static SpellCastAnimator instance;

    // Self-bootstrapping singleton: no scene wiring required. The card prefab
    // and camera are supplied/looked up at call time.
    public static SpellCastAnimator Instance
    {
        get
        {
            if (instance == null)
                instance = new GameObject(nameof(SpellCastAnimator)).AddComponent<SpellCastAnimator>();
            return instance;
        }
    }

    private const float SlideDuration = 1.5f; // full bottom -> center travel
    private const float StartViewportY = 0f;  // bottom edge of the screen
    private const float EndViewportY = 0.5f;  // vertical center of the screen
    private const float ScaleMultiplier = 1.5f; // spell shows bigger than a fielded card

    public Task Play(FieldableCardInstance spell, PlayerSide side, GameObject cardPrefab)
    {
        var tcs = new TaskCompletionSource<bool>();
        StartCoroutine(SlideRoutine(spell, side, cardPrefab, tcs));
        return tcs.Task;
    }

    private IEnumerator SlideRoutine(FieldableCardInstance spell, PlayerSide side, GameObject cardPrefab,
        TaskCompletionSource<bool> tcs)
    {
        Camera cam = Camera.main;

        // Same orientation as fielded cards so the spell reads correctly under
        // the top-down camera.
        CardVisualizer visual = Instantiate(cardPrefab, Vector3.zero, Quaternion.Euler(90, 0, 0))
            .GetComponent<CardVisualizer>();
        visual.Setup(spell, side);
        visual.transform.localScale *= ScaleMultiplier; // larger than a fielded card

        // Cards live on the y = 0 plane; the camera looks straight down, so its
        // height is the forward distance to that plane for ViewportToWorldPoint.
        float planeDistance = Mathf.Abs(cam.transform.position.y);

        float elapsed = 0f;
        while (elapsed < SlideDuration)
        {
            // Ease-out: races up to near the center, then decelerates into it.
            float p = elapsed / SlideDuration;
            float t = 1f - Mathf.Pow(1f - p, 4f);
            float vy = Mathf.Lerp(StartViewportY, EndViewportY, t);
            visual.transform.position = cam.ViewportToWorldPoint(new Vector3(0.5f, vy, planeDistance));
            elapsed += Time.deltaTime;
            yield return null;
        }

        Destroy(visual.gameObject);
        tcs.SetResult(true);
    }
}
