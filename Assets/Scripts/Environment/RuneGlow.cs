using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Riftborn.Environment
{
    // Drives the emissive "glow" of a single carved rune on the arena floor.
    //
    // The rune itself is a URP Decal Projector: its decal material writes the
    // rune's NORMAL map into the floor (via the DBuffer technique) so it reads as
    // physically engraved under scene lighting. That carved look is static. What
    // this component controls is the SECOND channel of the same decal material —
    // Emission — so the grooves can light up on command (spell cast, turn start,
    // channelling, etc.) and fade back down.
    //
    // Placement/size are NOT handled here: you position and scale the Decal
    // Projector by hand in the editor (its box gizmo is the rune's bounding box).
    // This component only owns the glow.
    //
    // Each projector gets its OWN material instance at runtime (the shared decal
    // material is cloned in Awake), so runes glow independently without every rune
    // sharing one emission colour. The clone is destroyed with the object.
    [RequireComponent(typeof(DecalProjector))]
    public class RuneGlow : MonoBehaviour
    {
        [Header("Glow appearance")]
        [Tooltip("Hue of the glow at intensity 1. Marked HDR so you can already " +
                 "push it past white for bloom in the editor if you prefer.")]
        [ColorUsage(true, true)]
        [SerializeField] private Color glowColor = new Color(0.3f, 0.8f, 1f, 1f);

        [Tooltip("Emission multiplier when the rune is idle. 0 = fully dark carved " +
                 "groove with no light; raise it for a faint always-on ember.")]
        [SerializeField] private float restIntensity = 0f;

        [Tooltip("Emission multiplier at full glow. Values > 1 drive bloom.")]
        [SerializeField] private float maxIntensity = 4f;

        [Header("Transition")]
        [Tooltip("Shapes the rise/fall when using GlowUp / GlowDown.")]
        [SerializeField] private AnimationCurve ease =
            AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Tooltip("Default seconds for a GlowUp / GlowDown that doesn't pass its own duration.")]
        [SerializeField] private float defaultDuration = 0.5f;

        // Emission property on URP's stock "Shader Graphs/Decal" shader. Exposed so
        // a custom decal Shader Graph with a differently-named emission port still works.
        [SerializeField] private string emissionProperty = "_EmissionColor";

        [Tooltip("Texture properties on the decal shader that carry the rune artwork. " +
                 "SetDecalTextures writes the per-resonance mask/normal into these.")]
        [SerializeField] private string maskProperty = "_MaskTex";
        [SerializeField] private string normalProperty = "_NormalMap";

        private DecalProjector _projector;
        private Material _mat;          // per-instance clone we are allowed to mutate
        private int _emissionId;
        private int _maskId;
        private int _normalId;
        private bool _initialized;      // property IDs resolved yet?
        private float _intensity;       // current emission multiplier
        private Coroutine _running;

        private void Awake()
        {
            if (!EnsureMaterial()) return;

            _intensity = restIntensity;
            Apply();
        }

        // Lazily resolve shader property IDs and give this rune its OWN material
        // clone. Idempotent and order-independent: Awake calls it to set up the
        // glow, and SetDecalTextures calls it in case a portal assigns the
        // resonance rune before this component's Awake has run. Returns false (and
        // disables the component) only when the projector has no material to clone.
        private bool EnsureMaterial()
        {
            if (!_initialized)
            {
                _projector = GetComponent<DecalProjector>();
                _emissionId = Shader.PropertyToID(emissionProperty);
                _maskId = Shader.PropertyToID(maskProperty);
                _normalId = Shader.PropertyToID(normalProperty);
                _initialized = true;
            }

            if (_mat != null) return true;

            // Clone so this rune's glow/textures are independent of every other rune
            // that shares the same source material asset. Without this, writes would
            // hit the shared asset and light up (or dirty) all of them.
            if (_projector.material == null)
            {
                Debug.LogWarning($"{name}: DecalProjector has no material; RuneGlow disabled.", this);
                enabled = false;
                return false;
            }

            _mat = Instantiate(_projector.material);
            _projector.material = _mat;
            // Emission may be toggled off on the source material; force it on so
            // driving the colour actually shows. Harmless if already enabled.
            _mat.EnableKeyword("_EMISSION");
            return true;
        }

        private void OnDestroy()
        {
            // We created this instance, so we clean it up.
            if (_mat != null)
                Destroy(_mat);
        }

        // ---- Public API ----------------------------------------------------

        // Snap to full glow / idle immediately (no transition).
        public void SetLit(bool lit) => SetIntensity(lit ? maxIntensity : restIntensity);

        // Normalised 0..1 glow, mapped between rest and max. Instant.
        public void SetGlow01(float t) =>
            SetIntensity(Mathf.LerpUnclamped(restIntensity, maxIntensity, t));

        // Direct control of the emission multiplier. Instant.
        public void SetIntensity(float intensity)
        {
            StopRunning();
            _intensity = intensity;
            Apply();
        }

        // Swap in the per-resonance rune artwork: silhouette mask + engraved normal
        // map. Called by Portal once the portal's resonance is decided, so every
        // portal projects its own floor rune from a shared decal shader. Writes to
        // this projector's private material clone, so portals don't affect each
        // other. Null textures are left untouched (keeps the source material's).
        public void SetDecalTextures(Texture mask, Texture normal)
        {
            if (!EnsureMaterial()) return;
            if (mask != null) _mat.SetTexture(_maskId, mask);
            if (normal != null) _mat.SetTexture(_normalId, normal);
        }

        // Smoothly ramp up to full glow. Returns the coroutine so callers can yield on it.
        public Coroutine GlowUp(float duration = -1f) => AnimateTo(maxIntensity, duration);

        // Smoothly fade back to idle.
        public Coroutine GlowDown(float duration = -1f) => AnimateTo(restIntensity, duration);

        // One up-then-down flash. Great for "rune reacts to an event" one-shots.
        public Coroutine Pulse(float upDuration = 0.15f, float downDuration = 0.6f)
        {
            StopRunning();
            _running = StartCoroutine(PulseRoutine(upDuration, downDuration));
            return _running;
        }

        // ---- Internals -----------------------------------------------------

        private Coroutine AnimateTo(float target, float duration)
        {
            StopRunning();
            if (duration < 0f) duration = defaultDuration;
            _running = StartCoroutine(RampRoutine(_intensity, target, duration));
            return _running;
        }

        private IEnumerator RampRoutine(float from, float to, float duration)
        {
            if (duration <= 0f)
            {
                _intensity = to;
                Apply();
                _running = null;
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                float t = ease.Evaluate(elapsed / duration);
                _intensity = Mathf.LerpUnclamped(from, to, t);
                Apply();
                elapsed += Time.deltaTime;
                yield return null;
            }
            _intensity = to;
            Apply();
            _running = null;
        }

        private IEnumerator PulseRoutine(float upDuration, float downDuration)
        {
            yield return RampRoutine(_intensity, maxIntensity, upDuration);
            yield return RampRoutine(maxIntensity, restIntensity, downDuration);
        }

        private void StopRunning()
        {
            if (_running != null)
            {
                StopCoroutine(_running);
                _running = null;
            }
        }

        // Push the current intensity into the decal's emission colour. The rune's
        // Emission MAP (the mask) confines this glow to the carved strokes; here we
        // only scale its colour/brightness.
        private void Apply()
        {
            if (_mat == null) return;
            _mat.SetColor(_emissionId, glowColor * _intensity);
        }
    }
}
