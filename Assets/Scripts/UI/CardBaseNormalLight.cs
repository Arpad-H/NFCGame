using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Feeds UI/CardBase a light direction expressed in the card's own tangent space.
///
/// UI meshes are pre-transformed into canvas space before the draw call, so
/// unity_ObjectToWorld is meaningless inside a UI shader and the card's rotation
/// cannot be recovered there. We resolve the direction on the CPU instead, which
/// keeps the highlight anchored in screen space while the card fans, tilts or flips.
///
/// This forces a material instance per card, so it breaks canvas batching. Only add
/// it to cards that actually rotate — a card that merely translates keeps a constant
/// tangent basis and can use the shared material with a baked-in _LightDir.
/// </summary>
[RequireComponent(typeof(Image))]
public class CardBaseNormalLight : MonoBehaviour
{
    private static readonly int LightDirID = Shader.PropertyToID("_LightDir");

    [Tooltip("Direction the light travels FROM, in world space. Normalized on use.")]
    public Vector3 worldLightDirection = new Vector3(-0.4f, 0.6f, -0.7f);

    [Tooltip("Optional. If set, overrides worldLightDirection with this transform's -forward.")]
    public Transform lightSource;

    [Tooltip("Skip the per-frame update when the card is not rotating.")]
    public bool updateEveryFrame = true;

    private Image image;
    private Material instanced;
    private Quaternion lastRotation;

    private void Awake()
    {
        image = GetComponent<Image>();
        if (image.material == null || image.material.shader == null) return;

        // Graphic.material hands back the shared asset; clone so each card can carry
        // its own _LightDir without writing through to the project material.
        instanced = new Material(image.material);
        instanced.name = image.material.name + " (card instance)";
        image.material = instanced;
    }

    private void OnEnable()
    {
        lastRotation = Quaternion.identity; // force a push on the first frame
        Apply();
    }

    private void LateUpdate()
    {
        if (!updateEveryFrame) return;
        if (transform.rotation == lastRotation) return;
        Apply();
    }

    /// <summary>Recompute and push the tangent-space light direction. Safe to call manually.</summary>
    public void Apply()
    {
        if (instanced == null) return;

        Vector3 world = lightSource != null ? -lightSource.forward : worldLightDirection;
        if (world.sqrMagnitude < 1e-6f) return;
        world.Normalize();

        // Tangent basis the shader assumes: X = card right, Y = card up, Z = toward viewer.
        // Stating it explicitly beats relying on Unity's canvas facing convention.
        Vector3 tangent = new Vector3(
            Vector3.Dot(world, transform.right),
            Vector3.Dot(world, transform.up),
            Vector3.Dot(world, -transform.forward));

        instanced.SetVector(LightDirID, tangent);
        lastRotation = transform.rotation;
    }

    private void OnDestroy()
    {
        if (instanced == null) return;
        if (Application.isPlaying) Destroy(instanced);
        else DestroyImmediate(instanced);
    }
}
