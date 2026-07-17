using UnityEngine;
using GameSystems;

/// <summary>
/// Coin-flip turn indicator built for a strictly top-down orthographic camera.
///
/// The Main Camera looks straight down (-Y), so the coin's up-axis points right at
/// the lens and <b>real vertical height is invisible on screen</b>. Everything here
/// is therefore faked in the screen plane. With this camera the mapping is:
///     screen-right = world +X,  screen-up = world +Z,  toward-lens = world +Y.
///
/// Behaviour:
///   • Rest  – the coin hovers at a socket and "rolls on its side": its up-axis is
///             tilted a few degrees and that tilt sweeps around in a cone
///             (precession), the way a coin wobbles as it settles (an Euler's disk).
///   • Ramp  – as the turn timer enters its critical period the tilt grows, the
///             sweep speeds up and a tremor is added, so the coin looks about to
///             topple. It stays put until the turn actually changes.
///   • Flip  – on a turn change the coin arcs to the other socket. The arc is faked
///             with a screen-up (+Z) parabola + an apex scale-pop (faking a rise
///             toward the lens) + a drop shadow left behind on the ground. The
///             tumble itself is a genuine 3D rotation — a flat disc spun about a
///             horizontal axis really does foreshorten to a line and back under an
///             orthographic camera, so it reads as a coin flip. Whole-number flips
///             always land front-face up.
///
/// Wiring: drop this on the "coinTurnIndicator" root. The coin/socket transforms
/// are auto-found by child name if left unassigned. <see cref="TurnIndicator"/>
/// forwards the turn-change and timer callbacks here.
/// </summary>
[DisallowMultipleComponent]
public class CoinTurnIndicator : MonoBehaviour
{
    [Header("Scene refs (auto-found by child name if empty)")]
    [Tooltip("The disc that flips. Child named 'coin'.")]
    [SerializeField] Transform coin;
    [Tooltip("Left landing marker. Child named 'socketleft'.")]
    [SerializeField] Transform socketLeft;
    [Tooltip("Right landing marker. Child named 'socketRight'.")]
    [SerializeField] Transform socketRight;

    [Header("Rest hover — precession cone ('rolling on its side')")]
    [Tooltip("Cone half-angle of the coin's up-axis while calm (degrees).")]
    [SerializeField] float restTiltDeg = 3.5f;
    [Tooltip("Cone half-angle at full instability, just before it topples (degrees).")]
    [SerializeField] float criticalTiltDeg = 20f;
    [Tooltip("How fast the tilt direction sweeps around the cone while calm (deg/s).")]
    [SerializeField] float restPrecessionSpeed = 45f;
    [Tooltip("Sweep speed at full instability (deg/s) — the frantic settling roll.")]
    [SerializeField] float criticalPrecessionSpeed = 280f;
    [Tooltip("Random tremor added to the tilt at full instability (degrees).")]
    [SerializeField] float criticalJitterDeg = 6f;
    [Tooltip("Subtle screen-vertical (+Z) hover bob amplitude while resting (world units).")]
    [SerializeField] float hoverBob = 0.1f;
    [SerializeField] float hoverBobSpeed = 1.6f;

    [Header("Instability ramp")]
    [Tooltip("Instability starts ramping once the remaining-time fraction (0..1) drops " +
             "below this. 0.25 of a 60s turn = the last 15s.")]
    [Range(0.05f, 0.6f)]
    [SerializeField] float criticalFraction = 0.25f;
    [Tooltip("How quickly the wobble eases toward its target level (per second).")]
    [SerializeField] float instabilityLerp = 3f;

    [Header("Flip flight (arc + tumble)")]
    [SerializeField] float flightDuration = 0.9f;
    [Tooltip("Whole tumbles during the flight. Whole numbers always land front-up.")]
    [SerializeField] int fullFlips = 3;
    [Tooltip("Local axis the coin tumbles about. X = classic coin-toss (horizontal " +
             "edge line as it flips end-over-end toward the far socket).")]
    [SerializeField] Vector3 tumbleAxis = Vector3.right;
    [Tooltip("Screen-vertical (+Z) height of the faked arc, in world units.")]
    [SerializeField] float arcScreenHeight = 4f;
    [Tooltip("Real +Y lift during flight. Invisible to the top-down lens; only keeps " +
             "the coin rendering above its shadow and the board.")]
    [SerializeField] float arcRealLift = 3f;
    [Tooltip("Extra scale at the apex, faking a rise toward the lens (0.5 = +50%).")]
    [SerializeField] float apexScalePop = 0.5f;

    [Header("Shadow (left behind on the ground to sell height)")]
    [SerializeField] bool useShadow = true;
    [Tooltip("Optional custom shadow sprite. If empty, a soft circle is generated at runtime.")]
    [SerializeField] Sprite shadowSprite;
    [SerializeField] Color shadowColor = new Color(0f, 0f, 0f, 0.35f);
    [Tooltip("Shadow diameter in world units (roughly the coin's size).")]
    [SerializeField] float shadowDiameter = 2.6f;
    [Tooltip("Lift above the socket so the shadow clears the board surface (world Y).")]
    [SerializeField] float shadowYOffset = 0.03f;
    [Tooltip("How far the shadow sits 'below' the coin on screen (world -Z). A small " +
             "value reads as a hovering coin's drop shadow.")]
    [SerializeField] float shadowScreenDrop = 0.3f;
    [Tooltip("Sorting order for the shadow sprite (keep below the coin).")]
    [SerializeField] int shadowSortingOrder = -20;

    // ── runtime state ────────────────────────────────────────────────────────
    PlayerSide? currentSide;     // socket the coin currently belongs to
    Vector3 coinBaseScale;
    float precessPhase;          // sweeping tilt azimuth (deg)
    float hoverPhase;
    float instability;           // 0 calm .. 1 about to topple (smoothed)
    float targetInstability;

    bool flying;
    float flightT;               // 0..1 across the flight
    Vector3 flightFrom, flightTo;
    Quaternion flightStartRot;

    Transform shadow;
    SpriteRenderer shadowRenderer;

    void Awake()
    {
        if (coin == null) coin = transform.Find("coin");
        if (socketLeft == null) socketLeft = transform.Find("socketleft");
        if (socketRight == null) socketRight = transform.Find("socketRight");

        if (coin == null)
        {
            Debug.LogWarning("[CoinTurnIndicator] No 'coin' child found — disabling.", this);
            enabled = false;
            return;
        }
        coinBaseScale = coin.localScale;
        if (useShadow) SetupShadow();
    }

    // ── public API (called by TurnIndicator) ─────────────────────────────────

    /// Move the coin to the given side's socket. The first call (or a call for the
    /// side it already sits on) snaps instantly; otherwise it flips through the air.
    public void SwitchTo(PlayerSide side)
    {
        if (!enabled || coin == null) return;

        if (currentSide == null)
        {
            SnapTo(side);
            return;
        }
        if (currentSide == side && !flying)
        {
            SnapTo(side); // already here — just re-settle cleanly
            return;
        }

        flightFrom = SocketPos(currentSide.Value);
        flightTo = SocketPos(side);
        flightStartRot = coin.rotation;
        flightT = 0f;
        flying = true;
        currentSide = side;
    }

    /// Feed the active player's remaining-time fraction (1 = fresh, 0 = expired).
    public void SetTimeFraction(float fill)
    {
        // 0 while there's comfortable time left; ramps to 1 across the critical tail.
        targetInstability = Mathf.Clamp01(1f - Mathf.InverseLerp(0f, criticalFraction, fill));
    }

    // ── internals ─────────────────────────────────────────────────────────────

    Transform SocketTransform(PlayerSide side) => side == PlayerSide.Left ? socketLeft : socketRight;

    Vector3 SocketPos(PlayerSide side)
    {
        Transform t = SocketTransform(side);
        return t != null ? t.position : coin.position;
    }

    void SnapTo(PlayerSide side)
    {
        currentSide = side;
        flying = false;
        flightT = 0f;
        instability = 0f;
        targetInstability = 0f;
        precessPhase = 0f;
        coin.position = SocketPos(side);
        coin.rotation = Quaternion.identity; // front face up
        coin.localScale = coinBaseScale;
        UpdateShadow(SocketPos(side), 0f);
    }

    void Update()
    {
        if (coin == null) return;
        float dt = Time.deltaTime;

        if (flying) { UpdateFlight(dt); return; }
        if (currentSide == null) return; // not placed yet — leave as authored

        UpdateRest(dt);
    }

    void UpdateRest(float dt)
    {
        instability = Mathf.MoveTowards(instability, targetInstability, instabilityLerp * dt);

        float tilt = Mathf.Lerp(restTiltDeg, criticalTiltDeg, instability);
        if (instability > 0.001f)
        {
            // Smooth tremor so the wobble looks nervous, not jittery-noise.
            float tremor = (Mathf.PerlinNoise(Time.time * 9f, 0.37f) - 0.5f) * 2f;
            tilt += tremor * criticalJitterDeg * instability;
        }

        float sweep = Mathf.Lerp(restPrecessionSpeed, criticalPrecessionSpeed, instability);
        precessPhase = Mathf.Repeat(precessPhase + sweep * dt, 360f);

        // Precession: tilt the coin by 'tilt' about a horizontal axis, and sweep that
        // axis around +Y so the up-axis traces a cone (its high point races around the
        // rim, like a settling Euler's disk). Tilting about a *sweeping horizontal
        // axis* — rather than spinning about +Y — keeps the face image upright and
        // readable instead of pinwheeling. At tilt→0 this is identity (front up).
        Vector3 tiltAxis = Quaternion.AngleAxis(precessPhase, Vector3.up) * Vector3.right;
        coin.rotation = Quaternion.AngleAxis(tilt, tiltAxis);

        hoverPhase += hoverBobSpeed * dt;
        float bob = Mathf.Sin(hoverPhase) * hoverBob * (1f + instability);
        Vector3 rest = SocketPos(currentSide.Value) + Vector3.forward * bob;
        coin.position = rest;
        coin.localScale = coinBaseScale;

        UpdateShadow(SocketPos(currentSide.Value), 0f);
    }

    void UpdateFlight(float dt)
    {
        flightT += flightDuration > 0f ? dt / flightDuration : 1f;
        float t = Mathf.Clamp01(flightT);
        float e = Mathf.SmoothStep(0f, 1f, t);   // eased progress (slow launch & landing)
        float parab = 4f * e * (1f - e);         // 0 → 1 → 0, apex at the midpoint

        Vector3 ground = Vector3.Lerp(flightFrom, flightTo, e);
        Vector3 pos = ground
                      + Vector3.forward * (arcScreenHeight * parab)  // visible screen-up arc
                      + Vector3.up * (arcRealLift * parab);          // invisible; render separation
        coin.position = pos;

        // Genuine tumble. Whole turns land back at front-up (identity).
        Quaternion tumble = Quaternion.AngleAxis(fullFlips * 360f * e, tumbleAxis.normalized);
        // Blend out of the resting tilt for the first slice so there's no snap at launch.
        coin.rotation = t < 0.12f ? Quaternion.Slerp(flightStartRot, tumble, t / 0.12f) : tumble;

        coin.localScale = coinBaseScale * (1f + apexScalePop * parab);

        UpdateShadow(ground, parab);

        if (t >= 1f)
        {
            flying = false;
            instability = 0f;
            targetInstability = 0f;
            precessPhase = 0f;
            coin.position = flightTo;
            coin.rotation = Quaternion.identity; // land front-up
            coin.localScale = coinBaseScale;
            UpdateShadow(flightTo, 0f);
        }
    }

    // ── shadow ────────────────────────────────────────────────────────────────

    void SetupShadow()
    {
        var go = new GameObject("coinShadow");
        go.transform.SetParent(transform, false);
        // Lay the sprite flat in the world XZ plane so it faces the top-down lens.
        go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        shadow = go.transform;
        shadowRenderer = go.AddComponent<SpriteRenderer>();
        shadowRenderer.sprite = shadowSprite != null ? shadowSprite : GenerateSoftCircle();
        shadowRenderer.color = shadowColor;
        shadowRenderer.sortingOrder = shadowSortingOrder;
        shadowRenderer.enabled = false; // stays hidden until the coin is first placed
    }

    void UpdateShadow(Vector3 groundPos, float lift01)
    {
        if (shadow == null) return;
        shadowRenderer.enabled = true;

        shadow.position = groundPos
                          + Vector3.up * shadowYOffset
                          - Vector3.forward * shadowScreenDrop; // sit 'below' the coin on screen

        float shrink = 1f - 0.35f * lift01;                    // smaller when airborne
        shadow.localScale = Vector3.one * shadowDiameter * shrink;

        Color c = shadowColor;
        c.a = shadowColor.a * (1f - 0.45f * lift01);           // fainter when airborne
        shadowRenderer.color = c;
    }

    // Soft radial-falloff circle so we don't depend on any imported sprite asset.
    static Sprite GenerateSoftCircle()
    {
        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        float r = size * 0.5f;
        var px = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r)) / r;
                float a = Mathf.Clamp01(1f - d);
                a *= a;                                        // soften the edge
                px[y * size + x] = new Color(1f, 1f, 1f, a);
            }
        }
        tex.SetPixels(px);
        tex.Apply();
        // pixelsPerUnit = size → the sprite is 1×1 world unit before the transform scales it.
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    // ── editor helpers (tune the flip without entering a match) ────────────────
    [ContextMenu("Test flip → Left")]
    void TestFlipLeft() { if (Application.isPlaying) SwitchTo(PlayerSide.Left); }

    [ContextMenu("Test flip → Right")]
    void TestFlipRight() { if (Application.isPlaying) SwitchTo(PlayerSide.Right); }
}
