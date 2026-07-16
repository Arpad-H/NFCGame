using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Reveals a player's three selected resonance coins with a staggered coin-toss.
///
/// Each slot starts as an "empty" placeholder coin resting in its socket. When
/// <see cref="ShowResonances"/> is called the placeholder is flipped up into the air
/// like a tossed coin (spinning end-over-end via an X-axis rotation, which in the
/// orthographic menu canvas reads as a coin flip). While it is airborne the socket it
/// left behind fades in underneath; the coin swaps to its real resonance sprite at the
/// edge-on point of the spin (so the swap is invisible), then falls back to exactly
/// where it started, the socket fades away, and it settles with a little squash.
/// A small per-coin delay staggers the three so they don't move in unison.
///
/// The component is layout-agnostic: on the first reveal it lets the container's
/// <see cref="HorizontalLayoutGroup"/> (if any) place the coins once, snapshots those
/// resting positions, then disables the group so the flight isn't fought over. Sockets
/// are snapped to sit directly behind their coin, so they don't need to be laid-out
/// siblings — they can be authored anywhere under this object.
///
/// Everything runs on unscaled time so it works while the game is paused, matching the
/// rest of the menu juice.
/// </summary>
[DisallowMultipleComponent]
public class ResonanceCoinReveal : MonoBehaviour
{
    [Header("Coins & sockets")]
    [Tooltip("The coin Images that flip, in slot order. Leave empty to auto-find " +
             "children named 'resonance*' (sorted by name).")]
    [SerializeField] private Image[] coins;
    [Tooltip("The socket Image left behind under each coin while it is airborne, one " +
             "per coin. Leave empty to auto-find children named 'socket*'.")]
    [SerializeField] private Image[] sockets;

    [Header("Toss")]
    [Tooltip("How high (in this rect's local units) the coin rises at the peak of the toss.")]
    [SerializeField] private float arcHeight = 90f;
    [Tooltip("Seconds for one coin's full up-and-down flight.")]
    [SerializeField] private float flightDuration = 0.7f;
    [Tooltip("Whole end-over-end spins during the flight. Kept an integer so the coin " +
             "lands face-up.")]
    [SerializeField, Min(1)] private int spins = 3;
    [Tooltip("Extra size at the peak of the arc, faking the coin coming toward the " +
             "camera (0.15 = up to 15% bigger mid-air).")]
    [SerializeField] private float peakScaleBoost = 0.15f;
    [Tooltip("Seconds between each coin starting its toss.")]
    [SerializeField] private float stagger = 0.12f;

    [Header("Socket fade")]
    [Tooltip("Fraction of the flight over which the left-behind socket fades in.")]
    [SerializeField, Range(0.01f, 0.5f)] private float socketFadeIn = 0.15f;
    [Tooltip("Fraction of the flight (at the end) over which the socket fades back out, " +
             "so it is gone as the coin lands.")]
    [SerializeField, Range(0.01f, 0.5f)] private float socketFadeOut = 0.2f;

    [Header("Landing")]
    [Tooltip("Squash punch applied when the coin lands, as a fraction of its scale.")]
    [SerializeField, Range(0f, 0.5f)] private float landSquash = 0.14f;
    [SerializeField] private float landSquashDuration = 0.28f;

    // Cached per-coin resting state, snapshotted on first reveal.
    private RectTransform[] _coinRects;
    private Vector2[] _restPos;
    private Vector3[] _baseScale;
    private Sprite[] _placeholder;
    private Tween[] _flights;
    private bool _initialized;

    private void OnDisable()
    {
        // Panels get toggled with SetActive; leave things resting so a re-show is clean.
        KillFlights();
        if (!_initialized) return;
        for (int i = 0; i < _coinRects.Length; i++)
            RestCoin(i, _placeholder[i]);
    }

    /// <summary>Play the staggered coin-toss, landing each coin on the matching sprite.</summary>
    /// <param name="faces">Resolved resonance sprites in slot order. A null entry keeps
    /// that coin on its placeholder.</param>
    public void ShowResonances(IReadOnlyList<Sprite> faces)
    {
        EnsureInitialized();
        KillFlights();

        for (int i = 0; i < _coinRects.Length; i++)
        {
            Sprite face = (faces != null && i < faces.Count) ? faces[i] : null;
            _flights[i] = BuildFlight(i, face, i * stagger);
        }
    }

    /// <summary>Snap every coin back to its resting placeholder with the socket hidden.</summary>
    public void ResetToPlaceholder()
    {
        EnsureInitialized();
        KillFlights();
        for (int i = 0; i < _coinRects.Length; i++)
            RestCoin(i, _placeholder[i]);
    }

    private Tween BuildFlight(int i, Sprite face, float delay)
    {
        RectTransform rect = _coinRects[i];
        Vector2 rest = _restPos[i];
        Vector3 baseScale = _baseScale[i];
        Image socket = i < sockets.Length ? sockets[i] : null;

        // Start resting on the placeholder; the socket waits, invisible, behind the coin.
        RestCoin(i, _placeholder[i]);

        float totalSpin = 360f * spins;
        float swapT = EdgeOnNearestMidFlight(totalSpin);
        bool swapped = false;

        return DOVirtual.Float(0f, 1f, flightDuration, t =>
            {
                // Physical toss: linear time, parabolic height, constant spin.
                float arc = 4f * t * (1f - t);                 // 0 -> 1 -> 0, peak at mid-flight
                rect.anchoredPosition = rest + Vector2.up * (arcHeight * arc);
                rect.localScale = baseScale * (1f + peakScaleBoost * arc);
                rect.localRotation = Quaternion.Euler(totalSpin * t, 0f, 0f);

                if (!swapped && face != null && t >= swapT)
                {
                    coins[i].sprite = face;                    // hidden: coin is edge-on here
                    swapped = true;
                }

                if (socket != null)
                    SetAlpha(socket, SocketAlpha(t));
            })
            .SetEase(Ease.Linear)
            .SetDelay(delay)
            .SetUpdate(true)
            .OnComplete(() =>
            {
                if (face != null) coins[i].sprite = face;
                rect.anchoredPosition = rest;
                rect.localRotation = Quaternion.identity;
                rect.localScale = baseScale;
                if (socket != null) SetSocketVisible(socket, false);

                if (landSquash > 0f)
                    rect.DOPunchScale(baseScale * landSquash, landSquashDuration, 6, 0.6f)
                        .SetUpdate(true);
            });
    }

    // Socket is fully visible through the flight, fading in at the toss and out on landing.
    private float SocketAlpha(float t)
    {
        float rising = Mathf.InverseLerp(0f, socketFadeIn, t);          // 0 -> 1
        float falling = 1f - Mathf.InverseLerp(1f - socketFadeOut, 1f, t); // 1 -> 0
        return Mathf.Clamp01(Mathf.Min(rising, falling));
    }

    // The spin passes edge-on (invisible) every 90° + 180°k; pick the crossing closest to
    // mid-flight so the sprite swap is both hidden and roughly at the apex.
    private static float EdgeOnNearestMidFlight(float totalSpin)
    {
        float best = 0.5f;
        float bestDist = float.MaxValue;
        for (float deg = 90f; deg < totalSpin; deg += 180f)
        {
            float t = deg / totalSpin;
            float dist = Mathf.Abs(t - 0.5f);
            if (dist < bestDist) { bestDist = dist; best = t; }
        }
        return best;
    }

    private void RestCoin(int i, Sprite sprite)
    {
        RectTransform rect = _coinRects[i];
        rect.anchoredPosition = _restPos[i];
        rect.localRotation = Quaternion.identity;
        rect.localScale = _baseScale[i];
        if (sprite != null) coins[i].sprite = sprite;
        if (i < sockets.Length && sockets[i] != null)
            SetSocketVisible(sockets[i], false);
    }

    private void EnsureInitialized()
    {
        if (_initialized) return;

        AutoDiscover();

        int n = coins.Length;
        _coinRects = new RectTransform[n];
        _restPos = new Vector2[n];
        _baseScale = new Vector3[n];
        _placeholder = new Sprite[n];
        _flights = new Tween[n];
        for (int i = 0; i < n; i++)
        {
            _coinRects[i] = (RectTransform)coins[i].transform;
            _baseScale[i] = _coinRects[i].localScale;
            _placeholder[i] = coins[i].sprite;
        }

        // Let the layout group place the coins once (with sockets pulled out so they don't
        // consume slots), snapshot those positions, then stop the group fighting the toss.
        var layout = GetComponent<HorizontalLayoutGroup>();
        bool canRebuild = layout != null && layout.isActiveAndEnabled && gameObject.activeInHierarchy;
        if (canRebuild)
        {
            var hidden = new List<GameObject>();
            foreach (var s in sockets)
                if (s != null && s.gameObject.activeSelf) { s.gameObject.SetActive(false); hidden.Add(s.gameObject); }

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)transform);

            for (int i = 0; i < n; i++) _restPos[i] = _coinRects[i].anchoredPosition;

            layout.enabled = false;
            foreach (var go in hidden) go.SetActive(true);
        }
        else
        {
            for (int i = 0; i < n; i++) _restPos[i] = _coinRects[i].anchoredPosition;
        }

        // Park each socket directly behind its coin, invisible until a toss.
        for (int i = 0; i < sockets.Length && i < n; i++)
        {
            if (sockets[i] == null) continue;
            var srect = (RectTransform)sockets[i].transform;
            srect.anchoredPosition = _restPos[i];
            srect.SetSiblingIndex(_coinRects[i].GetSiblingIndex()); // draw behind the coin
            SetSocketVisible(sockets[i], false);
        }

        _initialized = true;
    }

    private void AutoDiscover()
    {
        if (coins == null || coins.Length == 0)
        {
            var found = new List<Image>();
            foreach (Transform child in transform)
            {
                if (!child.name.ToLowerInvariant().StartsWith("resonance")) continue;
                var img = child.GetComponent<Image>();
                if (img != null) found.Add(img);
            }
            found.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            coins = found.ToArray();
        }

        if (sockets == null || sockets.Length == 0)
        {
            var found = new List<Image>();
            foreach (Transform child in transform)
            {
                if (!child.name.ToLowerInvariant().StartsWith("socket")) continue;
                var img = child.GetComponent<Image>();
                if (img != null) found.Add(img);
            }
            sockets = found.ToArray();
        }

        if (sockets == null) sockets = new Image[0];
    }

    private static void SetSocketVisible(Image socket, bool visible)
    {
        socket.gameObject.SetActive(visible);
        SetAlpha(socket, visible ? 1f : 0f);
    }

    private static void SetAlpha(Image img, float a)
    {
        if (!img.gameObject.activeSelf) img.gameObject.SetActive(true);
        Color c = img.color;
        c.a = a;
        img.color = c;
    }

    private void KillFlights()
    {
        if (_flights == null) return;
        for (int i = 0; i < _flights.Length; i++)
        {
            _flights[i]?.Kill();
            _flights[i] = null;
        }
    }
}
