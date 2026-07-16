using System;
using UnityEngine;

/// <summary>
/// Flips a panel's buttons in one after another when the panel is activated, in the
/// order you list them. Put it on the panel (TopLevelMenu, GameModesSelection, …) and
/// drag its <see cref="MenuFlipIn"/> buttons into <see cref="members"/> — that list *is*
/// the order, so resequencing them is a drag rather than an edit on every button.
///
/// Buttons in the list stay edge-on until their turn comes; a button with a
/// <see cref="MenuFlipIn"/> that this group doesn't own just flips on its own the moment
/// it's activated, so leaving one out of the list is visible rather than silent.
///
/// Leave the list empty and the group takes every MenuFlipIn beneath it in hierarchy
/// order, which is a reasonable default but is only as stable as the sibling order in
/// the scene — list them explicitly for anything you care about.
/// </summary>
[DisallowMultipleComponent]
public class MenuFlipGroup : MonoBehaviour
{
    [Tooltip("The buttons to flip, in the order they should flip. Leave empty to use " +
             "every MenuFlipIn under this object, in hierarchy order.")]
    [SerializeField] private MenuFlipIn[] members;

    [Header("Timing")]
    [Tooltip("Seconds between one button starting its flip and the next. Each flip rings " +
             "for longer than this, so the wobbles overlap — that's the point.")]
    [SerializeField, Min(0f)] private float stagger = 0.07f;
    [Tooltip("Seconds to wait before the first button flips, e.g. to let the panel " +
             "itself fade in first.")]
    [SerializeField, Min(0f)] private float startDelay = 0f;
    [Tooltip("Run the list back to front, so the last button listed flips first.")]
    [SerializeField] private bool reverse = false;
    [Tooltip("Flip on activation. Turn off to drive it from code or a UnityEvent only.")]
    [SerializeField] private bool playOnEnable = true;

    private bool _resolved;

    private void OnEnable()
    {
        if (playOnEnable) Play();
    }

    /// <summary>Flip every member in, staggered in list order.</summary>
    public void Play()
    {
        EnsureMembers();

        for (int i = 0; i < members.Length; i++)
        {
            MenuFlipIn member = members[i];
            if (member == null) continue;

            int slot = reverse ? (members.Length - 1 - i) : i;
            member.Play(startDelay + slot * stagger);
        }
    }

    /// <summary>Drop every member flat, killing any flips in flight.</summary>
    public void SnapHome()
    {
        EnsureMembers();
        foreach (MenuFlipIn member in members)
            if (member != null) member.SnapHome();
    }

    /// <summary>
    /// Whether this group drives that button — i.e. whether the button should wait to be
    /// played rather than flipping itself in on activation.
    /// </summary>
    public bool Owns(MenuFlipIn member)
    {
        EnsureMembers();
        return Array.IndexOf(members, member) >= 0;
    }

    // Called from MenuFlipIn.Owner as well as Play, which can land before this group's
    // Awake, so discovery has to be lazy rather than wired up in Awake.
    private void EnsureMembers()
    {
        if (_resolved) return;
        _resolved = true;

        if (members == null || members.Length == 0)
            members = GetComponentsInChildren<MenuFlipIn>(true);
    }
}
