using System.Collections;
using UnityEngine;

// Runtime "crumble" for a dying portal. On death it swaps the intact portal
// frame model for the pre-separated pieces model (portalInPieces) placed exactly
// over it, then drops each shard to the board with a short, deterministic,
// gravity-like fall.
//
// The fall is procedural, NOT physics, on purpose: shards only translate down,
// tip a little around their own origin, and drift within a small clamped radius,
// so the debris pile never spreads past the portal's own footprint (they may
// clip into each other, which is fine). One-shot — Play is called once by Portal
// the frame its HP first reaches 0.
public class PortalCrumble : MonoBehaviour
{
    [Header("Pieces model")]
    [Tooltip("The separated portal model (portalInPieces). Instantiated over the " +
             "intact frame on death; each child mesh becomes a falling shard.")]
    [SerializeField] private GameObject piecesPrefab;

    [Tooltip("Fine-tune the spawned pieces to line up with the intact frame, in the " +
             "active side visual's local space. Leave at defaults if the two models " +
             "share an origin (they should, being the same portal).")]
    [SerializeField] private Vector3 alignmentPositionOffset = Vector3.zero;
    [SerializeField] private Vector3 alignmentEulerOffset = Vector3.zero;
    [SerializeField] private float alignmentScaleMultiplier = 1f;

    [Header("Fall")]
    [Tooltip("Seconds a shard takes to drop from its start height to the ground.")]
    [SerializeField] private float fallDuration = 0.8f;
    [Tooltip("Random extra delay (0..this) before each shard starts falling, so the " +
             "portal collapses as a cascade rather than all at once.")]
    [SerializeField] private float fallStagger = 0.25f;
    [Tooltip("Ground the shards settle at, as a world-Y offset from the portal's " +
             "base. 0 rests pivots on the board plane; negative lets them sink/clip in.")]
    [SerializeField] private float groundYOffset = 0f;

    [Header("Containment")]
    [Tooltip("Max horizontal drift (world units) a shard may wander as it falls. " +
             "Small keeps the pile inside the portal's footprint.")]
    [SerializeField] private float maxHorizontalDrift = 0.15f;
    [Tooltip("Max tumble (degrees) a shard rotates through on the way down. Shards " +
             "spin around their own origin, so this stays contained.")]
    [SerializeField] private float maxTumbleDegrees = 40f;
    [Tooltip("Small upward hop as a shard lands, as a fraction of its fall height. " +
             "0 = dead stop, no bounce.")]
    [Range(0f, 0.3f)] [SerializeField] private float bounce = 0.06f;

    private bool played;

    // Called by Portal the frame its health first reaches 0. activeVisual is the
    // side visual currently shown (it owns the intact frame). Safe to call once;
    // repeat calls are ignored.
    public void Play(GameObject activeVisual)
    {
        if (played) return;
        played = true;

        if (piecesPrefab == null)
        {
            Debug.LogWarning($"{name}: PortalCrumble has no piecesPrefab assigned; portal will not crumble.");
            return;
        }
        if (activeVisual == null)
        {
            Debug.LogWarning($"{name}: PortalCrumble.Play got a null activeVisual; nothing to crumble.");
            return;
        }

        // The only MeshRenderers under a side visual are the portal frame itself
        // (the decal is a DecalProjector, the health label is uGUI/CanvasRenderer,
        // the spot lights are Lights). So these renderers ARE the frame: use their
        // model root as the alignment reference and their material for the shards.
        var frameRenderers = activeVisual.GetComponentsInChildren<MeshRenderer>(true);
        Transform frameRoot = ResolveFrameRoot(activeVisual.transform, frameRenderers);
        Material frameMat = frameRenderers.Length > 0 ? frameRenderers[0].sharedMaterial : null;

        // Spawn the pieces over the frame. Copying the frame model root's local
        // transform overlays them when both FBX share an origin (same portal); the
        // alignment offsets are there to nudge if they don't.
        GameObject debris = Instantiate(piecesPrefab, activeVisual.transform);
        if (frameRoot != null)
        {
            debris.transform.localPosition = frameRoot.localPosition;
            debris.transform.localRotation = frameRoot.localRotation;
            debris.transform.localScale = frameRoot.localScale;
        }
        else
        {
            debris.transform.localPosition = Vector3.zero;
            debris.transform.localRotation = Quaternion.identity;
        }
        debris.transform.localPosition += alignmentPositionOffset;
        debris.transform.localRotation *= Quaternion.Euler(alignmentEulerOffset);
        debris.transform.localScale *= alignmentScaleMultiplier;

        // Hide the intact frame now that the pieces sit on top of it.
        if (frameRoot != null) frameRoot.gameObject.SetActive(false);

        // Drop every shard (each child mesh of the pieces model).
        var shards = debris.GetComponentsInChildren<MeshRenderer>(true);
        float groundY = transform.position.y + groundYOffset;
        foreach (var sr in shards)
        {
            if (frameMat != null) sr.sharedMaterial = frameMat;
            StartCoroutine(FallShard(sr.transform, groundY));
        }
    }

    // Topmost ancestor of a frame renderer that is still a direct child of the
    // side visual — i.e. the imported model instance root we align the pieces to.
    private static Transform ResolveFrameRoot(Transform visualRoot, MeshRenderer[] frameRenderers)
    {
        if (frameRenderers.Length == 0) return null;
        Transform t = frameRenderers[0].transform;
        while (t.parent != null && t.parent != visualRoot) t = t.parent;
        return t;
    }

    private IEnumerator FallShard(Transform shard, float groundY)
    {
        // Snapshot the start pose in WORLD space so "down" is always world -Y,
        // independent of how the portal (or the debris root) is oriented.
        Vector3 startPos = shard.position;
        Quaternion startRot = shard.rotation;

        // Target: essentially the same footprint (small clamped jitter), resting on
        // the ground plane.
        Vector2 drift = Random.insideUnitCircle * maxHorizontalDrift;
        Vector3 endPos = new Vector3(startPos.x + drift.x, groundY, startPos.z + drift.y);
        float fallHeight = Mathf.Max(0f, startPos.y - groundY);

        // Tumble: a small rotation about a random axis, reached by the time it lands.
        Vector3 axis = Random.onUnitSphere;
        float tumbleAngle = Random.Range(0f, maxTumbleDegrees);
        Quaternion endRot = Quaternion.AngleAxis(tumbleAngle, axis) * startRot;

        float delay = Random.Range(0f, fallStagger);
        float waited = 0f;
        while (waited < delay)
        {
            waited += Time.deltaTime;
            yield return null;
        }

        float dur = Mathf.Max(0.0001f, fallDuration);
        float e = 0f;
        while (e < dur)
        {
            float p = e / dur;

            // Accelerating drop for Y (p^2 ≈ gravity feel); ease-out drift for XZ.
            float y = Mathf.Lerp(startPos.y, endPos.y, p * p);
            if (bounce > 0f && p > 0.85f)
            {
                // Single small hop over the last 15% of the fall, back to rest at p=1.
                float b = Mathf.Sin((p - 0.85f) / 0.15f * Mathf.PI);
                y += b * bounce * fallHeight;
            }

            Vector3 pos = Vector3.Lerp(startPos, endPos, EaseOutCubic(p));
            pos.y = y;
            shard.SetPositionAndRotation(pos, Quaternion.Slerp(startRot, endRot, EaseOutCubic(p)));

            e += Time.deltaTime;
            yield return null;
        }

        shard.SetPositionAndRotation(endPos, endRot);
    }

    private static float EaseOutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);
}
