using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Drives one history tile. Every serialized reference is OPTIONAL: Bind()
// null-checks each field, so a prefab variant only needs to wire the parts it
// actually shows. A "duel" prefab wires sourceImage + targetImage; a
// "multi-target" prefab wires sourceImage + targetsContainer + targetIconPrefab
// and leaves targetImage empty. Build the visuals in the editor and drag the
// matching fields onto this component.
//
// Click-to-inspect: the tile raises Clicked with itself; HistoryBarUI relays the
// entry (whose Source / Targets expose the live CardInstance refs) so a detail
// popup can highlight the involved cards.
public class HistoryTile : MonoBehaviour, IPointerClickHandler
{
    [Header("Actors")]
    [Tooltip("Portrait of the acting card (attacker / spell / played minion).")]
    [SerializeField] private Image sourceImage;
    [Tooltip("Portrait of the single target (duel tile). Leave empty on the multi-target prefab.")]
    [SerializeField] private Image targetImage;
    [SerializeField] private TextMeshProUGUI sourceName;
    [SerializeField] private TextMeshProUGUI targetName;

    [Header("Amount / icons")]
    [Tooltip("Damage or healing number. Hidden when the entry has no amount.")]
    [SerializeField] private TextMeshProUGUI amountText;
    [Tooltip("Skull (or similar) shown when the hit killed its target.")]
    [SerializeField] private GameObject killIcon;
    [Tooltip("Optional connector (e.g. an arrow) shown only when there is a target.")]
    [SerializeField] private GameObject arrow;

    [Header("Multi-target (optional)")]
    [Tooltip("Container the per-target icons are instantiated into for multi-target entries.")]
    [SerializeField] private RectTransform targetsContainer;
    [Tooltip("Image prefab instantiated once per affected target.")]
    [SerializeField] private Image targetIconPrefab;

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

    private readonly List<GameObject> spawnedTargetIcons = new();

    public void Bind(HistoryEntry entry)
    {
        Entry = entry;
        if (entry == null) return;

        SetActor(sourceImage, sourceName, entry.Source);

        if (entry.IsMultiTarget)
        {
            BindMultiTarget(entry.Targets);
            if (targetImage != null) targetImage.gameObject.SetActive(false);
            if (targetName != null) targetName.gameObject.SetActive(false);
        }
        else
        {
            HistoryActor? target = entry.Target;
            if (target.HasValue) SetActor(targetImage, targetName, target.Value);
            else HideActor(targetImage, targetName);
        }

        if (amountText != null)
        {
            bool showAmount = entry.Amount > 0;
            amountText.gameObject.SetActive(showAmount);
            if (showAmount) amountText.text = entry.Amount.ToString();
        }

        if (killIcon != null) killIcon.SetActive(entry.Lethal);
        if (arrow != null) arrow.SetActive(entry.Targets.Count > 0);
        if (tintTarget != null) tintTarget.color = ColorFor(entry.Kind);
    }

    // Instantiates one icon per affected target. This is the "programmatically
    // add an image per affected minion" path for spells/minions that hit several
    // targets at once.
    private void BindMultiTarget(IReadOnlyList<HistoryActor> targets)
    {
        ClearTargetIcons();
        if (targetsContainer == null || targetIconPrefab == null) return;

        foreach (HistoryActor actor in targets)
        {
            Image icon = Instantiate(targetIconPrefab, targetsContainer);
            icon.sprite = actor.Icon;
            icon.enabled = actor.Icon != null; // player targets have no art
            icon.gameObject.SetActive(true);
            spawnedTargetIcons.Add(icon.gameObject);
        }
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

    private static void HideActor(Image image, TextMeshProUGUI label)
    {
        if (image != null) image.gameObject.SetActive(false);
        if (label != null) label.gameObject.SetActive(false);
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

    private void ClearTargetIcons()
    {
        foreach (GameObject go in spawnedTargetIcons)
        {
            if (go != null) Destroy(go);
        }
        spawnedTargetIcons.Clear();
    }

    private void OnDestroy() => ClearTargetIcons();
}
