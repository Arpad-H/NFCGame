using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

// Plays the "burn to ash" death effect for a fielded minion.
//
// A board minion (BoardTokenVisualizer) is a world-space uGUI canvas made of
// several Images + TMP numbers. To burn it as ONE cohesive sheet we flatten it:
// render just that token into a transparent RenderTexture with a dedicated
// top-down ortho camera (the game camera is already top-down ortho, so the RT
// maps 1:1 onto a flat quad placed where the token was). We then destroy the
// live token and let a single burn shader dissolve the snapshot, with a small
// rising ember/ash particle burst on top.
//
// Scene-singleton, mirroring DamageNumberSpawner: Portal.RemoveCard calls
// BurnDeathEffect.Instance.Play(...) on the death path only. If no instance was
// placed in the scene one is auto-created with the defaults below, so the effect
// works with zero wiring; drop the component onto a GameObject to tune it.
public class BurnDeathEffect : MonoBehaviour
{
    private static BurnDeathEffect _instance;

    public static BurnDeathEffect Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindAnyObjectByType<BurnDeathEffect>();
                if (_instance == null)
                {
                    var go = new GameObject("BurnDeathEffect (auto)");
                    _instance = go.AddComponent<BurnDeathEffect>();
                }
            }

            return _instance;
        }
    }

    [Header("Timing")]
    [Tooltip("Seconds for the sheet to fully burn away. Driven on unscaledDeltaTime " +
             "so it plays even if the death sequence touches Time.timeScale.")]
    [SerializeField] private float burnDuration = 0.5f;

    [Header("Shader / material")]
    [Tooltip("Optional explicit shader reference. Leave empty to resolve " +
             "\"Riftborn/BurnDeath\" via Shader.Find (fine in the editor; assign " +
             "this — or add the shader to Always Included Shaders — for builds).")]
    [SerializeField] private Shader burnShader;

    [Header("Burn look")]
    [SerializeField] private float noiseScale = 6f;
    [Range(0, 1)] [SerializeField] private float noiseAmp = 0.35f;
    [Range(0.001f, 0.5f)] [SerializeField] private float edgeWidth = 0.09f;
    [Range(0.001f, 0.6f)] [SerializeField] private float charWidth = 0.16f;
    [Range(0.001f, 0.6f)] [SerializeField] private float ashWidth = 0.10f;
    [Range(0, 1)] [SerializeField] private float charDarkness = 0.06f;
    [SerializeField] private float emberIntensity = 3f;
    [Tooltip("UV-space direction the flame climbs. Default (0,1): from the bottom " +
             "edge upward, like paper catching from below.")]
    [SerializeField] private Vector2 burnDirection = new Vector2(0f, 1f);

    [Header("Ember ramp (white -> yellow -> orange -> violet fringe)")]
    [ColorUsage(true, true)] [SerializeField] private Color emberWhite = new Color(1f, 0.95f, 0.6f, 1f);
    [ColorUsage(true, true)] [SerializeField] private Color emberYellow = new Color(1f, 0.72f, 0.2f, 1f);
    [ColorUsage(true, true)] [SerializeField] private Color emberOrange = new Color(1f, 0.36f, 0.06f, 1f);
    [ColorUsage(true, true)] [SerializeField] private Color emberViolet = new Color(0.55f, 0.16f, 0.85f, 1f);
    [SerializeField] private Color charColor = new Color(0.02f, 0.02f, 0.03f, 1f);

    [Header("Capture")]
    [Tooltip("Extra world-space margin around the token so the ember glow and ash " +
             "aren't clipped by the quad/RT edge.")]
    [SerializeField] private float capturePadding = 0.35f;
    [Tooltip("Texture resolution per world unit of the captured token. Higher = crisper, heavier.")]
    [SerializeField] private float pixelsPerUnit = 240f;
    [SerializeField] private int maxTextureSize = 1024;
    [Tooltip("Layer the token is briefly moved to so the capture camera renders it " +
             "alone. Must be an empty/dedicated layer (see TagManager: 'FXCapture').")]
    [SerializeField] private string captureLayerName = "FXCapture";
    [Tooltip("Fallback layer index if captureLayerName isn't defined in the project.")]
    [SerializeField] private int captureLayerFallback = 7;
    [Tooltip("Nudge the burn quad this far toward the camera (world +Y) to avoid " +
             "z-fighting the board where the token sat.")]
    [SerializeField] private float quadLift = 0.05f;
    [Tooltip("Mirror the captured sheet vertically. Toggle this if the burn shows " +
             "up flipped/mirrored in the editor (a platform camera->RT flip).")]
    [SerializeField] private bool flipCaptureV = false;

    [Header("Ember / ash particles")]
    [SerializeField] private bool spawnParticles = true;
    [Tooltip("Optional custom burst. If set, this prefab is spawned at the token " +
             "instead of the code-built spark system.")]
    [SerializeField] private ParticleSystem emberBurstPrefab;
    [SerializeField] private int particleCount = 26;
    [SerializeField] private float particleRiseSpeed = 1.6f;
    [SerializeField] private float particleLifetime = 0.55f;
    [SerializeField] private Vector2 particleSize = new Vector2(0.04f, 0.12f);
    [ColorUsage(true, true)] [SerializeField] private Color particleColorHot = new Color(1f, 0.7f, 0.2f, 1f);
    [ColorUsage(true, true)] [SerializeField] private Color particleColorCool = new Color(0.5f, 0.18f, 0.75f, 1f);

    public float BurnDuration => burnDuration;

    // --- shared, allocation-light resources -------------------------------------
    private Material burnMaterial;              // shared; every quad overrides via MPB
    private Camera captureCamera;               // reused across deaths
    private int captureLayer = -1;

    private static readonly Vector3[] CornerBuffer = new Vector3[4];
    private static Mesh quadMesh;

    private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
    private static readonly int BurnAmountId = Shader.PropertyToID("_BurnAmount");
    private static readonly int NoiseScaleId = Shader.PropertyToID("_NoiseScale");
    private static readonly int NoiseAmpId = Shader.PropertyToID("_NoiseAmp");
    private static readonly int EdgeWidthId = Shader.PropertyToID("_EdgeWidth");
    private static readonly int CharWidthId = Shader.PropertyToID("_CharWidth");
    private static readonly int AshWidthId = Shader.PropertyToID("_AshWidth");
    private static readonly int CharDarknessId = Shader.PropertyToID("_CharDarkness");
    private static readonly int EmberIntensityId = Shader.PropertyToID("_EmberIntensity");
    private static readonly int FlipVId = Shader.PropertyToID("_FlipV");
    private static readonly int BurnDirId = Shader.PropertyToID("_BurnDir");
    private static readonly int EmberWhiteId = Shader.PropertyToID("_EmberWhite");
    private static readonly int EmberYellowId = Shader.PropertyToID("_EmberYellow");
    private static readonly int EmberOrangeId = Shader.PropertyToID("_EmberOrange");
    private static readonly int EmberVioletId = Shader.PropertyToID("_EmberViolet");
    private static readonly int CharColorId = Shader.PropertyToID("_CharColor");

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        captureLayer = LayerMask.NameToLayer(captureLayerName);
        if (captureLayer < 0) captureLayer = captureLayerFallback;
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
        if (captureCamera != null) Destroy(captureCamera.gameObject);
    }

    // Flattens the token, hands the burn off to an independent quad, and destroys
    // the live token. Returns true if it took ownership of the token's GameObject
    // (caller must NOT also destroy it); false if anything was missing and the
    // caller should fall back to a plain Destroy.
    public bool Play(BoardTokenVisualizer visual)
    {
        if (visual == null) return false;
        if (!EnsureMaterial()) return false;

        // World-space footprint of the active token graphics on the board (XZ),
        // padded so the ember glow has room to bloom past the art.
        if (!TryGetTokenBounds(visual, out Vector3 center, out float width, out float depth))
            return false;

        float paddedW = width + capturePadding * 2f;
        float paddedD = depth + capturePadding * 2f;

        // Any failure inside the capture/spawn must degrade gracefully to "no burn,
        // token just vanishes" and surface ONE clear error, never spam the console
        // per death or per frame. On throw we return false so RemoveCard falls back
        // to a plain Destroy.
        RenderTexture rt = null;
        try
        {
            rt = CaptureToTexture(visual, center, paddedW, paddedD);
            if (rt == null) return false;

            // The snapshot is all we need — retire the live token. SetActive(false)
            // is immediate (Destroy is deferred to end of frame), so a second death
            // in the same batch can't catch this token still lit on the capture
            // layer, and the main camera never draws it under the quad this frame.
            visual.gameObject.SetActive(false);
            Destroy(visual.gameObject);

            SpawnBurnQuad(rt, center, paddedW, paddedD);
            if (spawnParticles) SpawnEmberBurst(center, width, depth);
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"BurnDeathEffect: capture/spawn failed, falling back to plain removal. {e}");
            if (rt != null) { rt.Release(); Destroy(rt); }
            return false;
        }
    }

    private bool EnsureMaterial()
    {
        if (burnMaterial != null) return true;
        Shader shader = burnShader != null ? burnShader : Shader.Find("Riftborn/BurnDeath");
        if (shader == null)
        {
            Debug.LogWarning("BurnDeathEffect: shader 'Riftborn/BurnDeath' not found; skipping burn.");
            return false;
        }

        burnMaterial = new Material(shader) { name = "BurnDeath (shared)" };
        return true;
    }

    // Union of every active Image/TMP rect on the token, in world space, reduced
    // to a centre + X/Z extents on the board plane.
    private static bool TryGetTokenBounds(BoardTokenVisualizer visual, out Vector3 center,
        out float width, out float depth)
    {
        center = visual.transform.position;
        width = depth = 0f;

        float minX = float.PositiveInfinity, maxX = float.NegativeInfinity;
        float minZ = float.PositiveInfinity, maxZ = float.NegativeInfinity;
        float sumY = 0f;
        int count = 0;

        var graphics = visual.GetComponentsInChildren<Graphic>(false);
        foreach (var g in graphics)
        {
            if (g == null || !g.isActiveAndEnabled) continue;
            var rt = g.rectTransform;
            rt.GetWorldCorners(CornerBuffer);
            for (int i = 0; i < 4; i++)
            {
                Vector3 c = CornerBuffer[i];
                if (c.x < minX) minX = c.x;
                if (c.x > maxX) maxX = c.x;
                if (c.z < minZ) minZ = c.z;
                if (c.z > maxZ) maxZ = c.z;
                sumY += c.y;
                count++;
            }
        }

        if (count == 0 || minX > maxX) return false;

        center = new Vector3((minX + maxX) * 0.5f, sumY / count, (minZ + maxZ) * 0.5f);
        width = Mathf.Max(0.01f, maxX - minX);
        depth = Mathf.Max(0.01f, maxZ - minZ);
        return true;
    }

    private RenderTexture CaptureToTexture(BoardTokenVisualizer visual, Vector3 center,
        float paddedW, float paddedD)
    {
        // RT sized to the token's aspect, scaled down uniformly if either axis
        // would exceed maxTextureSize so the aspect is preserved under the clamp.
        float scale = pixelsPerUnit;
        float maxDim = Mathf.Max(paddedW, paddedD) * scale;
        if (maxDim > maxTextureSize) scale *= maxTextureSize / maxDim;
        int w = Mathf.Clamp(Mathf.CeilToInt(paddedW * scale), 16, maxTextureSize);
        int h = Mathf.Clamp(Mathf.CeilToInt(paddedD * scale), 16, maxTextureSize);

        // NB: needs a depth-stencil buffer (24), not 0. Under Unity 6's URP
        // RenderGraph a camera render target with no depth format fails
        // ValidateTextureDesc ("Texture was created with no format"), which aborts
        // the whole render graph and cascades into Decal/ZBinning job errors.
        var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32)
        {
            name = "BurnCapture",
            useMipMap = false,
            autoGenerateMips = false,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        rt.Create();

        Camera cam = EnsureCaptureCamera();
        cam.orthographic = true;
        cam.orthographicSize = paddedD * 0.5f;
        cam.aspect = (float)w / h;               // match the RT exactly (== paddedW/paddedD unclamped)
        cam.transform.SetPositionAndRotation(center + Vector3.up * 50f, Quaternion.Euler(90f, 0f, 0f));
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 120f;

        // Render the token in isolation: move it (and children) to the capture
        // layer, which is the only layer the capture camera sees. Neighbours on
        // other layers never bleed into the snapshot. The token is destroyed right
        // after, so we don't bother restoring layers.
        SetLayerRecursive(visual.gameObject, captureLayer);
        cam.cullingMask = 1 << captureLayer;

        var request = new RenderPipeline.StandardRequest();
        if (RenderPipeline.SupportsRenderRequest(cam, request))
        {
            request.destination = rt;
            RenderPipeline.SubmitRenderRequest(cam, request);
        }
        else
        {
            // No supported render request (unexpected under URP). Don't fall back to
            // cam.Render() — under an SRP it spams "not supported" errors every call.
            // Bail so the burn is skipped cleanly (caller does a plain Destroy).
            Debug.LogWarning("BurnDeathEffect: camera render requests unsupported; skipping burn.");
            rt.Release();
            Destroy(rt);
            return null;
        }

        return rt;
    }

    private Camera EnsureCaptureCamera()
    {
        if (captureCamera != null) return captureCamera;

        var go = new GameObject("BurnCaptureCamera") { hideFlags = HideFlags.HideAndDontSave };
        go.transform.SetParent(transform, false);
        captureCamera = go.AddComponent<Camera>();
        captureCamera.enabled = false;                 // we drive it manually via render requests
        captureCamera.clearFlags = CameraClearFlags.SolidColor;
        captureCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
        captureCamera.allowHDR = false;
        captureCamera.allowMSAA = false;
        captureCamera.useOcclusionCulling = false;

        var data = go.AddComponent<UniversalAdditionalCameraData>();
        data.renderPostProcessing = false;             // no bloom/tonemap baked into the snapshot
        data.renderShadows = false;
        data.requiresColorOption = CameraOverrideOption.Off;
        data.requiresDepthOption = CameraOverrideOption.Off;

        return captureCamera;
    }

    private void SpawnBurnQuad(RenderTexture rt, Vector3 center, float paddedW, float paddedD)
    {
        var go = new GameObject("BurnQuad");
        go.transform.SetPositionAndRotation(center + Vector3.up * quadLift, Quaternion.Euler(90f, 0f, 0f));
        go.transform.localScale = new Vector3(paddedW, paddedD, 1f);

        var mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = GetQuadMesh();

        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = burnMaterial;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

        // Per-instance block: the RT + all look params, so the shared material is
        // never mutated and simultaneous burns stay independent.
        var mpb = new MaterialPropertyBlock();
        mpb.SetTexture(MainTexId, rt);
        mpb.SetFloat(BurnAmountId, 0f);
        mpb.SetFloat(NoiseScaleId, noiseScale);
        mpb.SetFloat(NoiseAmpId, noiseAmp);
        mpb.SetFloat(EdgeWidthId, edgeWidth);
        mpb.SetFloat(CharWidthId, charWidth);
        mpb.SetFloat(AshWidthId, ashWidth);
        mpb.SetFloat(CharDarknessId, charDarkness);
        mpb.SetFloat(EmberIntensityId, emberIntensity);
        mpb.SetFloat(FlipVId, flipCaptureV ? 1f : 0f);
        mpb.SetVector(BurnDirId, new Vector4(burnDirection.x, burnDirection.y, 0f, 0f));
        mpb.SetColor(EmberWhiteId, emberWhite);
        mpb.SetColor(EmberYellowId, emberYellow);
        mpb.SetColor(EmberOrangeId, emberOrange);
        mpb.SetColor(EmberVioletId, emberViolet);
        mpb.SetColor(CharColorId, charColor);
        mr.SetPropertyBlock(mpb);

        StartCoroutine(BurnRoutine(go, mr, mpb, rt));
    }

    private IEnumerator BurnRoutine(GameObject quad, Renderer rend, MaterialPropertyBlock mpb, RenderTexture rt)
    {
        float dur = Mathf.Max(0.0001f, burnDuration);
        float e = 0f;
        while (e < dur)
        {
            mpb.SetFloat(BurnAmountId, e / dur);
            rend.SetPropertyBlock(mpb);          // no allocation: same block reused
            e += Time.unscaledDeltaTime;
            yield return null;
        }

        mpb.SetFloat(BurnAmountId, 1f);
        rend.SetPropertyBlock(mpb);

        Destroy(quad);
        rt.Release();
        Destroy(rt);
    }

    // --- ember/ash particle burst -----------------------------------------------
    private void SpawnEmberBurst(Vector3 center, float width, float depth)
    {
        if (emberBurstPrefab != null)
        {
            var custom = Instantiate(emberBurstPrefab, center, Quaternion.identity);
            var cm = custom.main;
            cm.stopAction = ParticleSystemStopAction.Destroy;
            custom.Play();
            return;
        }

        var go = new GameObject("EmberBurst");
        go.transform.position = center;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.useUnscaledTime = true;                 // survives Time.timeScale like the burn
        main.duration = Mathf.Max(burnDuration, 0.1f);
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(particleLifetime * 0.6f, particleLifetime);
        main.startSpeed = new ParticleSystem.MinMaxCurve(particleRiseSpeed * 0.5f, particleRiseSpeed);
        main.startSize = new ParticleSystem.MinMaxCurve(particleSize.x, particleSize.y);
        main.startColor = new ParticleSystem.MinMaxGradient(particleColorHot, particleColorCool);
        main.gravityModifier = -0.05f;               // slight lift so motes drift upward
        main.maxParticles = Mathf.Max(8, particleCount + 8);
        main.stopAction = ParticleSystemStopAction.Destroy;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)particleCount) });

        // Seed motes across the token's footprint, biased upward (toward camera).
        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(Mathf.Max(0.05f, width), 0.02f, Mathf.Max(0.05f, depth));

        // Rise + fade: motes gain upward velocity, shrink and dim into ash.
        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.World;
        vel.y = new ParticleSystem.MinMaxCurve(particleRiseSpeed * 0.4f, particleRiseSpeed);

        var colOverLife = ps.colorOverLifetime;
        colOverLife.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.4f), new GradientAlphaKey(0f, 1f) });
        colOverLife.color = new ParticleSystem.MinMaxGradient(grad);

        var sizeOverLife = ps.sizeOverLifetime;
        sizeOverLife.enabled = true;
        var sizeCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.1f));
        sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        ps.Play();
    }

    private static void SetLayerRecursive(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform child in go.transform)
            SetLayerRecursive(child.gameObject, layer);
    }

    private static Mesh GetQuadMesh()
    {
        if (quadMesh != null) return quadMesh;

        quadMesh = new Mesh { name = "BurnQuad" };
        // Unit quad in local XY (centred). With the quad rotated Euler(90,0,0) the
        // local +Y maps to world +Z and local +X to world +X, matching the capture
        // camera's screen axes, so UVs line up with the RT 1:1.
        quadMesh.vertices = new[]
        {
            new Vector3(-0.5f, -0.5f, 0f),
            new Vector3(0.5f, -0.5f, 0f),
            new Vector3(0.5f, 0.5f, 0f),
            new Vector3(-0.5f, 0.5f, 0f)
        };
        quadMesh.uv = new[]
        {
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0f, 1f)
        };
        quadMesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        quadMesh.RecalculateBounds();
        return quadMesh;
    }
}
