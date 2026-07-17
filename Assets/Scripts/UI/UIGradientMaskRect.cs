using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Feeds the "RectUV" mode of the <c>UI/GradientMask</c> shader by writing normalised
/// 0-1 rect coordinates into each vertex's UV1.
///
/// You only need this for two cases. A plain Image or RawImage already has UVs running
/// 0-1 across its rect, so the shader's default SpriteUV mode masks it correctly with no
/// component at all. But a Sliced / Tiled / Filled Image repeats or clips those UVs, so
/// the mask would tile or stretch along with the sprite — this component gives the shader
/// a clean rect-space coordinate that ignores how the sprite itself is laid out.
///
/// The second case is masking a *group*. Point <see cref="maskArea"/> at a shared parent
/// RectTransform and every graphic referencing it samples the same gradient in that
/// parent's space, so one fade spans the whole group and each child gets the slice of it
/// that it physically covers. That is the closest equivalent to a soft Mask component:
/// the material still has to be on each masked graphic, since a gradient can't be
/// expressed in the stencil buffer a real Mask uses.
///
/// Remember to set the material's UV Source to RectUV — this component does nothing on
/// its own. UV1 only survives to the shader if the Canvas emits it, so this switches on
/// the TexCoord1 shader channel for you.
/// </summary>
[AddComponentMenu("UI/Effects/UI Gradient Mask Rect")]
[DisallowMultipleComponent]
[RequireComponent(typeof(Graphic))]
public class UIGradientMaskRect : BaseMeshEffect
{
    [Tooltip("Rect the gradient is measured across. Leave empty to use this graphic's own " +
             "rect. Set it to a shared parent to stretch one gradient over a group of graphics.")]
    [SerializeField] RectTransform maskArea;

    /// <summary>Rect the gradient spans; null means this graphic's own rect.</summary>
    public RectTransform MaskArea
    {
        get => maskArea;
        set
        {
            if (maskArea == value) return;
            maskArea = value;
            if (graphic != null) graphic.SetVerticesDirty();
        }
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        EnableTexCoord1();
    }

    public override void ModifyMesh(VertexHelper vh)
    {
        if (!IsActive() || graphic == null) return;

        // Cheap to re-assert, and the graphic can be reparented into a different Canvas.
        EnableTexCoord1();

        RectTransform self = graphic.rectTransform;
        RectTransform area = maskArea != null ? maskArea : self;
        Rect rect = area.rect;
        if (rect.width <= 0f || rect.height <= 0f) return;

        bool crossRect = area != self;
        UIVertex vertex = default;

        for (int i = 0; i < vh.currentVertCount; i++)
        {
            vh.PopulateUIVertex(ref vertex, i);

            Vector3 p = vertex.position;
            if (crossRect) p = area.InverseTransformPoint(self.TransformPoint(p));

            vertex.uv1 = new Vector4((p.x - rect.xMin) / rect.width,
                                     (p.y - rect.yMin) / rect.height,
                                     0f, 0f);
            vh.SetUIVertex(vertex, i);
        }
    }

    void EnableTexCoord1()
    {
        Canvas canvas = graphic != null ? graphic.canvas : null;
        if (canvas != null) canvas.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1;
    }

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        base.OnValidate();
        EnableTexCoord1();
    }
#endif
}
