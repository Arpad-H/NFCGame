using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Drives one history tile. Every serialized reference is OPTIONAL: Bind()
// null-checks each field, so a prefab variant only needs to wire the parts it
// actually shows.
//
// Targets are ALWAYS rendered the same way — one DamageTargetView per affected
// target, stacked into targetsContainer (a VerticalLayoutGroup). There is no
// single- vs multi-target branch: a lone target just fills the container with a
// single view. Wire sourceImage for the actor and targetsContainer +
// targetViewPrefab for the target stack.
//
// Click-to-inspect: the tile raises Clicked with itself; HistoryBarUI relays the
// entry (whose Source / Targets expose the live CardInstance refs) so a detail
// popup can highlight the involved cards.
public class HistoryTile : MonoBehaviour, IPointerClickHandler
{
    [Header("Actor")]
    [Tooltip("Portrait of the acting card (attacker / spell / played minion).")]
    [SerializeField] private Image sourceImage;
    [SerializeField] private TextMeshProUGUI sourceName;

    [Header("Targets")]
    [Tooltip("Container the per-target views are stacked into (a VerticalLayoutGroup).")]
    [SerializeField] private RectTransform targetsContainer;
    [Tooltip("View instantiated once per affected target — the DamageTarget prefab.")]
    [SerializeField] private DamageTargetView targetViewPrefab;

    [Header("Overlays (optional)")]
    [Tooltip("Skull (or similar) shown when the hit killed its target.")]
    [SerializeField] private GameObject killIcon;
    [Tooltip("Optional connector (e.g. an arrow) shown only when there is a target.")]
    [SerializeField] private GameObject arrow;

    [Header("Kind tint (optional)")]
    [Tooltip("Graphic tinted by action kind — e.g. the tile background. Leave empty to skip tinting.")]
    [SerializeField] private Graphic tintTarget;
    [SerializeField] private Color attackColor = Color.white;
    [SerializeField] private Color healColor = new Color(0.55f, 1f, 0.6f);
    [SerializeField] private Color playColor = new Color(0.8f, 0.85f, 1f);

    // The entry this tile currently shows. Its Source/Targets carry live
    // CardInstance refs for click-to-inspect.
    public HistoryEntry Entry { get; private set; }
    public event Action<HistoryTile> Clicked;

    // Pool of target views, in sibling order. Seeded in Awake with any view
    // already placed under the container in the editor (the design-time preview
    // element), then grown by cloning targetViewPrefab as entries need more.
    private readonly List<DamageTargetView> targetViews = new();

    private void Awake()
    {
        if (targetsContainer != null)
            targetsContainer.GetComponentsInChildren(true, targetViews);
    }

    public void Bind(HistoryEntry entry)
    {
        Entry = entry;
        if (entry == null) return;

        SetActor(sourceImage, sourceName, entry.Source);
        BindTargets(entry.Targets, entry.Amount);

        if (killIcon != null) killIcon.SetActive(entry.Lethal);
        if (arrow != null) arrow.SetActive(entry.Targets.Count > 0);
        if (tintTarget != null) tintTarget.color = ColorFor(entry.Kind);
    }

    // Populates one view per target (single or many — same path). Every target
    // currently shows the entry's Amount; per-target amounts would need the
    // model to carry an amount per target.
    private void BindTargets(IReadOnlyList<HistoryActor> targets, int amount)
    {
        if (targetsContainer == null) return;

        for (int i = 0; i < targets.Count; i++)
        {
            DamageTargetView view = GetOrCreateView(i);
            if (view == null) break; // no prefab to grow the pool with
            view.gameObject.SetActive(true);
            view.Set(targets[i], amount);
        }

        // Hide any pooled views this entry doesn't need (e.g. the preview
        // element when the entry has no targets).
        for (int i = targets.Count; i < targetViews.Count; i++)
            if (targetViews[i] != null) targetViews[i].gameObject.SetActive(false);
    }

    private DamageTargetView GetOrCreateView(int index)
    {
        if (index < targetViews.Count) return targetViews[index];
        if (targetViewPrefab == null) return null;
        DamageTargetView view = Instantiate(targetViewPrefab, targetsContainer);
        targetViews.Add(view);
        return view;
    }

    private static void SetActor(Image image, TextMeshProUGUI label, HistoryActor actor)
    {
        if (image != null)
        {
            image.sprite = actor.Icon;
            image.enabled = actor.Icon != null; // hide the image for art-less players
            image.gameObject.SetActive(true);
        }
        if (label != null)
        {
            label.gameObject.SetActive(true);
            label.text = actor.Name;
        }
    }

    private Color ColorFor(HistoryKind kind)
    {
        switch (kind)
        {
            case HistoryKind.Heal: return healColor;
            case HistoryKind.Play: return playColor;
            default: return attackColor;
        }
    }

    public void OnPointerClick(PointerEventData eventData) => Clicked?.Invoke(this);
}
