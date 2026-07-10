#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using TMPro;
using LeTai.TrueShadow;

namespace RiftbornEditor
{
    /// <summary>
    /// Rescales a World-Space UI subtree so every element is authored at a higher
    /// pixel resolution, while a single compensating scale on the Canvas keeps the
    /// on-screen result identical.
    ///
    /// Why: True Shadow (and other capture-based effects) size their render textures
    /// from "mesh bounds * canvas.scaleFactor". On a World-Space canvas scaleFactor
    /// is always 1, so an element authored at ~1.3 units only gets a ~2px shadow.
    /// Blowing every rect/position/font up by 100x and shrinking the Canvas transform
    /// by the same factor gives the same visuals but 100x the effect resolution.
    ///
    /// Usage: open the CardV2 prefab in Prefab Mode, select the Canvas (or the card
    /// root), then run Tools > Riftborn > Rescale UI Resolution (x100). Run ONCE.
    /// Ctrl+Z reverts it. All Images in this card are Simple type, so slice borders
    /// need no special handling.
    /// </summary>
    public static class CardUIResolutionRescaler
    {
        const float SCALE = 100f;        // resolution multiplier
        const float INV   = 1f / SCALE;  // compensating Canvas scale

        [MenuItem("Tools/Riftborn/Rescale UI Resolution (x100)")]
        static void Rescale()
        {
            var sel = Selection.activeGameObject;
            if (!sel)
            {
                EditorUtility.DisplayDialog("Rescale UI Resolution",
                    "Select the card's Canvas (or the card root) first.", "OK");
                return;
            }

            var canvas = sel.GetComponentInChildren<Canvas>(true);
            if (!canvas)
            {
                EditorUtility.DisplayDialog("Rescale UI Resolution",
                    "No Canvas found in the selection.\nSelect the card's Canvas (or a parent of it).", "OK");
                return;
            }

            var root = (RectTransform)canvas.transform;

            Undo.RegisterFullObjectHierarchyUndo(root.gameObject, "Rescale UI Resolution");

            int rtCount = 0, tmpCount = 0, tsCount = 0;

            // 1) RectTransforms: rect sizes and positional offsets are absolute local units.
            //    Anchors (fractions) and pivots stay as-is. The root's own anchoredPosition
            //    is left alone because its parent is NOT part of the rescaled subtree.
            foreach (var rt in root.GetComponentsInChildren<RectTransform>(true))
            {
                rt.sizeDelta *= SCALE;
                if (rt != root)
                {
                    rt.anchoredPosition *= SCALE;
                    var lp = rt.localPosition;
                    lp.z *= SCALE;               // preserve any z-depth layering
                    rt.localPosition = lp;
                }
                rtCount++;
            }

            // 2) TextMeshPro: font size and margins are absolute local units.
            //    (character/word/line spacing are relative to font size, so they scale for free.)
            foreach (var t in root.GetComponentsInChildren<TMP_Text>(true))
            {
                t.fontSize    *= SCALE;
                t.fontSizeMin *= SCALE;
                t.fontSizeMax *= SCALE;
                t.margin      *= SCALE;
                EditorUtility.SetDirty(t);
                tmpCount++;
            }

            // 3) True Shadow: blur Size and Offset Distance are in the same units.
            //    Spread (0-1) and Offset Angle (degrees) are unitless -> leave them.
            foreach (var ts in root.GetComponentsInChildren<TrueShadow>(true))
            {
                ts.Size           *= SCALE;
                ts.OffsetDistance *= SCALE;
                EditorUtility.SetDirty(ts);
                tsCount++;
            }

            // 4) One compensating scale so the card renders identically.
            root.localScale *= INV;

            EditorUtility.SetDirty(root.gameObject);

            Debug.Log($"[Rescale UI] Scaled {rtCount} RectTransforms, {tmpCount} TMP texts, " +
                      $"{tsCount} True Shadow(s) by x{SCALE}; compensated Canvas scale by {INV}. " +
                      $"Verify the card, then save the prefab. (Ctrl+Z to revert.)");
        }
    }
}
#endif
