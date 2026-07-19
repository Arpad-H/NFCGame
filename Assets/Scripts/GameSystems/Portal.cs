using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using GameSystems;
using JetBrains.Annotations;
using Riftborn.Environment;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public class Portal : MonoBehaviour, ITargetable
{
    public PlayerSide ownerSide;
    public Resonance resonance;
    public GameObject LeftPortalVisual;
    public GameObject RightPortalVisual;
    private TextMeshProUGUI identityText;
    private GameObject activeVisual; // the side visual currently shown (owns the live decal projector)

    // Resolved from activeVisual in SelectSide: the floor decal is the visible
    // portal a spawned minion erupts from, and its optional glow is pulsed on spawn.
    private DecalProjector portalDecal;
    private Transform portalMouth;
    private RuneGlow portalGlow;

    // Spot lights beneath the active side visual, paired with the intensity each
    // was authored with in the prefab. ApplyColor recolours them to the resonance
    // and rescales that base intensity so every hue reads about equally bright.
    private Light[] portalLights;
    private float[] portalLightBaseIntensities;

    private MaterialPropertyBlock propBlock;

    private List<(FieldableCardInstance context, BoardTokenVisualizer visual)> cardsInPortal
        = new List<(FieldableCardInstance, BoardTokenVisualizer)>();

    public ResonanceLibrary resonanceLibrary; //TODO move this
    public GameObject tempCardPrefab; //TODO move this
    public float cardSpacing = 1f;
    public float cardStartX = 2f;
    public int laneIndex; // 0 = top, 1 = middle, 2 = bottom

    [Header("Portal health")]
    [Tooltip("Hit points this portal starts with. When it reaches 0 its owner loses this lane.")]
    public int maxPortalHealth = 15;
    public int CurrentPortalHealth { get; private set; }
    public bool IsDestroyed => CurrentPortalHealth <= 0;
    [Tooltip("Optional TMP label showing the portal's current HP. Leave empty to track HP without a display.")]
    public TextMeshProUGUI portalHealthTextLeft;
    public TextMeshProUGUI portalHealthTextRight;

    // Assigned by Board.SetUpBoard so portal damage can raise board events
    // (OnPortalDamaged). Null in scenes without a board (menu prefabs).
    public Board Board { get; set; }

    // Curse: incoming damage is multiplied while turns remain. Ticks down once
    // per turn end (Board.TickPortalDamageMultipliers) — the same cadence as
    // minion status durations, so "2 turns" means the same thing everywhere.
    private int damageMultiplier = 1;
    private int damageMultiplierTurns;

    public void ApplyDamageMultiplier(int multiplier, int turns)
    {
        damageMultiplier = multiplier;
        damageMultiplierTurns = Mathf.Max(damageMultiplierTurns, turns);
        Debug.Log($"{this} takes {multiplier}x damage for {damageMultiplierTurns} turn(s).");
    }

    public void TickDamageMultiplier()
    {
        if (damageMultiplierTurns > 0 && --damageMultiplierTurns == 0)
        {
            damageMultiplier = 1;
            Debug.Log($"{this} damage multiplier expired.");
        }
    }

    [Header("Minion spawn animation")]
    [Tooltip("Portal half-width along the lane (world units) that must be kept clear during the reveal. 0 = auto-measure from the decal's size.")]
    public float spawnPortalHalfWidth = 0f;
    [Tooltip("Extra clearance (world units) left between each parted card and the edge of the exposed portal.")]
    public float spawnRevealGap = 0.35f;
    [Tooltip("Seconds for the covering cards to part aside and reveal the portal.")]
    public float spawnRevealDuration = 0.16f;
    [Tooltip("Seconds for the existing cards to spring back into their slots while the minion is airborne.")]
    public float spawnReturnDuration = 0.55f;
    [Tooltip("Seconds the new minion spends flying out of the portal to its slot.")]
    public float spawnFlightDuration = 0.7f;
    [Tooltip("Peak height (world units) of the minion's arc as it erupts from the portal.")]
    public float spawnArcHeight = 3.5f;
    [Tooltip("Size the emerging minion starts at, relative to its final fielded size.")]
    public float spawnStartScale = 0.55f;
    [Tooltip("Total tumble the minion spins through on its way to the slot, in degrees.")]
    public float spawnTumbleAngle = 520f;
    [Tooltip("Local axis the airborne minion tumbles around. Forward (Z) is the card's own normal, giving a flat spin that never flickers edge-on; use right/up for an end-over-end flip.")]
    public Vector3 spawnTumbleAxis = Vector3.forward;

    [Header("Death reflow")]
    [Tooltip("Seconds survivors take to slide into the gap left by a burned minion (ease-out).")]
    public float reflowTweenDuration = 0.25f;
    [Tooltip("Start the survivor reflow before the burn fully finishes, for a snappier feel.")]
    public bool earlyReflow = false;
    [Tooltip("When earlyReflow is on, fraction of the burn duration to wait before sliding survivors in.")]
    [Range(0f, 1f)] public float earlyReflowFraction = 0.7f;

    // Restart-safe survivor-reflow tween kicked off by a burning death (see ScheduleReflow).
    private Coroutine reflowRoutine;


    public GameObject portalPrefabDeath;
    public GameObject portalPrefabLife;
    public GameObject portalPrefabDarkness;
    public GameObject portalPrefabHoly;
    public GameObject portalPrefabPlague;
    public GameObject portalPrefabPsychic;

    public BoardTokenVisualizer GetVisualizer(FieldableCardInstance instance)
    {
        var match = cardsInPortal.Find(x => x.context == instance);
        return match.visual;
    }

    void OnValidate()
    {
        if (LeftPortalVisual == null || RightPortalVisual == null) return;
        SelectSide(ownerSide);
    }

    void SelectSide(PlayerSide newSide)
    {
        if (ownerSide == PlayerSide.Left)
        {
            RightPortalVisual.SetActive(true);
            LeftPortalVisual.SetActive(false);
            activeVisual = RightPortalVisual;
            identityText = RightPortalVisual.GetComponentInChildren<TextMeshProUGUI>();

        }
        else
        {
            RightPortalVisual.SetActive(false);
            LeftPortalVisual.SetActive(true);
            activeVisual = LeftPortalVisual;
            identityText = LeftPortalVisual.GetComponentInChildren<TextMeshProUGUI>();

        }

        // The floor decal under the shown side visual is the portal the minion
        // flies out of; grab its transform (and any glow) as the spawn origin.
        if (activeVisual != null)
        {
            portalDecal = activeVisual.GetComponentInChildren<DecalProjector>(true);
            portalMouth = portalDecal != null ? portalDecal.transform : activeVisual.transform;
            portalGlow = activeVisual.GetComponentInChildren<RuneGlow>(true);

            // Snapshot the spot lights and their authored intensities now, while
            // they still hold prefab values — ApplyColor scales from this baseline
            // so re-tinting never compounds.
            portalLights = activeVisual.GetComponentsInChildren<Light>(true);
            portalLightBaseIntensities = new float[portalLights.Length];
            for (int i = 0; i < portalLights.Length; i++)
                portalLightBaseIntensities[i] = portalLights[i].intensity;
        }
    }

    void Awake()
    {
        cardsInPortal.Clear();
        propBlock = new MaterialPropertyBlock();
        SelectSide(ownerSide);
        CurrentPortalHealth = maxPortalHealth;
        UpdatePortalHealthDisplay();
    }

    // ── Portal health (ITargetable) ──────────────────────────────────────────
    // A portal is the lane's "face": once no front minion guards it, attacking
    // minions and hero-targeting card effects hit the portal directly. Draining
    // it to 0 loses the lane for this portal's owner — Board.ResolveDecidedLanes
    // reads IsDestroyed after combat to award the lane and clear it.

    public async Task TakeDamage(DamageEventData damageEventData)
    {
        if (IsDestroyed) return;

        // Portals have no shield — the whole hit lands on health.
        int amount = Mathf.Max(0, damageEventData.Amount);
        if (damageMultiplierTurns > 0) amount *= damageMultiplier; // Curse
        CurrentPortalHealth = Mathf.Max(0, CurrentPortalHealth - amount);
        UpdatePortalHealthDisplay();

        if (AudioManager.Instance != null) AudioManager.Instance.PlayMinionClashSound();
        if (amount > 0) DamageNumberSpawner.Spawn(GetActiveHealthLabelPosition(), amount, false);

        // Portals raise no entity-local events (they hold no triggers), but the
        // board hears about the hit so cards can react (Cepter of Osiris).
        if (amount > 0 && Board != null)
        {
            await Board.RaiseEvent(new GameEvent(GameEventType.OnPortalDamaged, null,
                new PortalDamagedEventData(this, amount, damageEventData)));
        }
    }

    public Task Heal(HealEventData healEventData)
    {
        if (IsDestroyed) return Task.CompletedTask; // a shattered portal can't be repaired
        int amount = Mathf.Max(0, healEventData.Amount);
        CurrentPortalHealth = Mathf.Min(CurrentPortalHealth + amount, maxPortalHealth);
        UpdatePortalHealthDisplay();
        if (amount > 0) DamageNumberSpawner.Spawn(GetActiveHealthLabelPosition(), amount, true);
        return Task.CompletedTask;
    }

    public Task ModifyStat(MinionStats stat, int amount)
    {
        // Portals only carry health; attack is meaningless for them.
        if (stat == MinionStats.Health)
        {
            CurrentPortalHealth = Mathf.Clamp(CurrentPortalHealth + amount, 0, maxPortalHealth);
            UpdatePortalHealthDisplay();
        }

        return Task.CompletedTask;
    }

    // Reconfigures this portal's full health pool, e.g. scripted setups shrink
    // it so lanes resolve in a few hits. Resets current health to the new max,
    // so call it during setup, not mid-match. Safe before or after Awake.
    public void SetMaxHealth(int newMax)
    {
        maxPortalHealth = Mathf.Max(1, newMax);
        CurrentPortalHealth = maxPortalHealth;
        UpdatePortalHealthDisplay();
    }

    private void UpdatePortalHealthDisplay()
    {
        if(ownerSide == PlayerSide.Left)
        {
            
            if (portalHealthTextLeft != null)
            {
                portalHealthTextLeft.text = CurrentPortalHealth.ToString();
            }
          
        }
        else if (ownerSide == PlayerSide.Right)
        {
            if (portalHealthTextRight != null)
            {
                portalHealthTextRight.text = CurrentPortalHealth.ToString();
            }
        }
           
    }

    // World position of the HP label on this portal's active (owner's) side —
    // the same text UpdatePortalHealthDisplay writes to. Minions that hammer an
    // undefended portal lunge at this instead of the prefab's centre, so the
    // blow reads as landing on the number it's draining. Falls back to the
    // portal centre if the label isn't wired up.
    public Vector3 GetActiveHealthLabelPosition()
    {
        var activeText = ownerSide == PlayerSide.Left ? portalHealthTextLeft : portalHealthTextRight;
        return activeText != null ? activeText.transform.position : transform.position;
    }

    // Readable in event/combat logs (combat history tolerates a non-card target
    // via HistoryActor.FromTarget's default branch).
    public override string ToString()
    {
        return $"Portal[{ownerSide} L{laneIndex} HP {CurrentPortalHealth}/{maxPortalHealth}]";
    }

    public void SetResonanceType(ResonanceType type)
    {
        resonance = resonanceLibrary.GetResonance(type);
        if (!resonance)
        {
            Debug.LogError("Resonance not found: " + type);
            return;
        }

        identityText.text = resonance.identity;
       // SwapModel(resonance.ResonanceType);
        ApplyColor(resonance.color);
        ApplyDecal(resonance);
    }

    // Shader texture slots on the CustomDecal graph that carry the rune artwork.
    private static readonly int MaskTexId = Shader.PropertyToID("_MaskTex");
    private static readonly int NormalMapId = Shader.PropertyToID("_NormalMap");

    // Pushes this resonance's floor rune (mask + normal) onto the decal
    // projector(s) under the active side visual. Every projector is given its OWN
    // material instance, so portals show independent runes even though they all
    // start from one shared decal material. If a projector carries a RuneGlow
    // (which owns the material clone and drives its emission), we route through it
    // instead so we don't create a second, conflicting clone.
    private void ApplyDecal(Resonance res)
    {
        if (activeVisual == null) return;

        foreach (var projector in activeVisual.GetComponentsInChildren<DecalProjector>(true))
        {
            var glow = projector.GetComponent<RuneGlow>();
            if (glow != null)
            {
                glow.SetDecalTextures(res.decalMask, res.decalNormal);
                continue;
            }

            var mat = projector.material;
            if (mat == null) continue;

            // First touch: clone the shared material so this portal is independent.
            // Instantiate tags the copy's name with " (Instance)"; reuse that on
            // repeat calls rather than leaking a fresh clone every SetResonanceType.
            if (!mat.name.EndsWith("(Instance)"))
            {
                mat = Instantiate(mat);
                projector.material = mat;
            }

            if (res.decalMask != null) mat.SetTexture(MaskTexId, res.decalMask);
            if (res.decalNormal != null) mat.SetTexture(NormalMapId, res.decalNormal);
        }
    }

    private void DeactivateAllPortalls()
    {
        portalPrefabDeath.SetActive(false);
        portalPrefabLife.SetActive(false);
        portalPrefabPlague.SetActive(false);
        portalPrefabPsychic.SetActive(false);
        portalPrefabHoly.SetActive(false);
        portalPrefabDarkness.SetActive(false);
    }

    private void SwapModel(ResonanceType resonanceType)
    {
        DeactivateAllPortalls();
        switch (resonanceType)
        {
            case ResonanceType.Darkness:
                portalPrefabDarkness.SetActive(true);
                break;
            case ResonanceType.Death:
                portalPrefabDeath.SetActive(true);
                break;
            case ResonanceType.Life:
                portalPrefabLife.SetActive(true);
                break;
            case ResonanceType.Plague:
                portalPrefabPlague.SetActive(true);
                break;
            case ResonanceType.Psychic:
                portalPrefabPsychic.SetActive(true);
                break;
            case ResonanceType.Holy:
                portalPrefabHoly.SetActive(true);
                break;
        }
    }

    [Header("Spot light colour")]
    [Tooltip("Even out how bright each resonance's spot light reads. The eye sees " +
             "green/red far brighter than blue at equal intensity, so without this " +
             "some lanes glow much harder than others. When on, each light's " +
             "intensity is scaled by targetLuminance / colourLuminance so all hues " +
             "land near the same apparent brightness.")]
    public bool normalizeSpotLightBrightness = true;

    [Tooltip("Apparent brightness every resonance light is pulled toward. This is " +
             "the perceived luminance (0..1) at which a colour's intensity is left " +
             "unchanged; brighter colours are dimmed, dimmer ones boosted. Raise " +
             "for overall brighter lights, lower for dimmer.")]
    [Range(0.05f, 1f)]
    public float spotLightTargetLuminance = 0.3f;

    [Tooltip("Bounds on the per-colour intensity multiplier. The upper bound stops " +
             "a very dark hue (pure blue) from driving the light to an extreme " +
             "intensity; the lower bound keeps a bright hue from being dimmed to nothing.")]
    public Vector2 spotLightIntensityScaleRange = new Vector2(0.3f, 5f);

    // Rec. 709 perceived luminance. Green contributes ~0.72 and blue only ~0.07,
    // so equal-intensity lights of different hues read at wildly different
    // brightness — this is the weight we divide back out to equalise them.
    private static float PerceivedLuminance(Color c)
        => 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;

    // Tints the portal's spot lights to this resonance's colour. Like ApplyDecal,
    // we only touch the active side visual (the inactive side is disabled, so its
    // lights are left alone). Hue always comes from the resonance; intensity is
    // either left as authored or normalised so every resonance reads equally bright.
    private void ApplyColor(Color newColor)
    {
        if (portalLights == null) return;

        float scale = 1f;
        if (normalizeSpotLightBrightness)
        {
            float lum = PerceivedLuminance(newColor);
            scale = lum > 0.0001f
                ? Mathf.Clamp(spotLightTargetLuminance / lum,
                              spotLightIntensityScaleRange.x, spotLightIntensityScaleRange.y)
                : spotLightIntensityScaleRange.y;
        }

        for (int i = 0; i < portalLights.Length; i++)
        {
            var light = portalLights[i];
            if (light == null) continue;
            light.color = newColor;
            light.intensity = portalLightBaseIntensities[i] * scale;
        }
    }

    public async Task AddCard(FieldableCardInstance cardInstance)
    {
        BoardTokenVisualizer visual = Instantiate(tempCardPrefab, Vector3.zero, Quaternion.Euler(90, 0, 0))
            .GetComponent<BoardTokenVisualizer>();

        visual.Setup(cardInstance, ownerSide);

        // Tint the token's rune glows to the card's own resonance and light them
        // whenever one of its effects actually fires (see FlashEffectGlow).
        Resonance cardResonance = resonanceLibrary != null
            ? resonanceLibrary.GetResonance(cardInstance.SourceCard.resonance)
            : null;
        if (cardResonance != null) visual.SetResonanceGlowColor(cardResonance.color);
        cardInstance.OnEffectTriggered += visual.FlashEffectGlow;

        if (cardInstance is MinionInstance minion)
        {
            minion.OnStatsChanged += visual.UpdateStatsDisplay;
            // Resolve the portal at death time: lane shifts can move the card
            // to another portal after this subscription was made.
            minion.OnDeath += () => cardInstance.SourcePortal?.RemoveCard(cardInstance, playDeathBurn: true);
            minion.OnStatusEffectAdded += visual.ApplyStatusEffect;
            minion.OnStatusEffectRemoved += visual.RemoveStatusEffect;
            // Only the two blows of a clash need separating: the minions overlap
            // in the middle of the lane, so each number is pushed back toward its
            // own half of the board. Lane shifts only move cards within one side,
            // so the side captured here holds for the card's lifetime.
            float clashDirection = ownerSide == PlayerSide.Left ? -1f : 1f;
            minion.OnDamageDealt += (amount, isClashHit) =>
                DamageNumberSpawner.Spawn(visual.transform.position, amount, false,
                    isClashHit ? clashDirection : 0f);
            minion.OnHealReceived += amount =>
                DamageNumberSpawner.Spawn(visual.transform.position, amount, true);
        }

        FieldableCardInstance currentLastCardInPortal = cardsInPortal.Count > 0 ? cardsInPortal[^1].context : null;
        if (currentLastCardInPortal != null && cardInstance is ItemInstance item)
        {
            await currentLastCardInPortal.AttachCardToThis(item
                .GetSuppliedRunes()); //only items and spells activate effect activating runes
            if (currentLastCardInPortal is MinionInstance minionInstance) item.ItemHolder = minionInstance;
            else if (currentLastCardInPortal is ItemInstance itemInstance) item.ItemHolder = itemInstance.ItemHolder;
            //update visual of current last card in portal to reflect that it is now covered by another card, if there is one.
            var lastBoardTokenVisualizer = cardsInPortal[^1].visual;
            lastBoardTokenVisualizer.UpdateFieldCoverDisplay();
        }

        visual.UpdateFieldCoverDisplay();
        cardsInPortal.Add((cardInstance, visual));

        // Minions are spat out of the portal with a reveal-and-arc animation;
        // items/other cards just snap into their stacked slot as before.
        if (cardInstance is MinionInstance)
            await AnimateMinionSpawn(visual, cardInstance);
        else
            UpdateCardPositions();
    }

    // World-space rest pose of the card at the given stack index. Index 0 sits
    // closest to the portal; higher indices march outward along the lane.
    private Vector3 LayoutPosition(int index)
    {
        float sign = ownerSide == PlayerSide.Left ? -1f : 1f;
        float x = (cardStartX + index * cardSpacing) * sign;
        return new Vector3(x, 0f, transform.position.z);
    }

    private void UpdateCardPositions()
    {
        for (int i = 0; i < cardsInPortal.Count; i++)
        {
            cardsInPortal[i].visual.transform.position = LayoutPosition(i);
        }
    }

    // The portal's centre and half-width along the lane axis (world X) that the
    // reveal must keep clear. Measured from the decal projector's own footprint
    // (its image-plane X/Y size in world space, ignoring projection depth), so it
    // tracks the actual rune bounding box; spawnPortalHalfWidth overrides it.
    private void GetPortalSpanX(out float centerX, out float halfX)
    {
        centerX = portalMouth != null ? portalMouth.position.x : transform.position.x;

        if (spawnPortalHalfWidth > 0f)
        {
            halfX = spawnPortalHalfWidth;
            return;
        }

        if (portalDecal != null)
        {
            Transform pt = portalDecal.transform;
            Vector3 hs = portalDecal.size * 0.5f;
            halfX = Mathf.Abs(pt.TransformVector(new Vector3(hs.x, 0f, 0f)).x)
                  + Mathf.Abs(pt.TransformVector(new Vector3(0f, hs.y, 0f)).x);
            return;
        }

        halfX = cardSpacing * 0.5f;
    }

    // Fling the freshly-added minion (already the outermost entry in the stack)
    // out of the portal. Completes once it has tumbled into its slot, so the
    // caller's OnPlayed/battlecry only fires after the unit has arrived. The
    // card's "on played" SFX is started mid-routine, as the minion leaves the
    // portal mouth, so the clip plays over the flight instead of after it.
    private Task AnimateMinionSpawn(BoardTokenVisualizer newVisual, FieldableCardInstance cardInstance)
    {
        var tcs = new TaskCompletionSource<bool>();
        StartCoroutine(SpawnRoutine(newVisual, cardInstance, tcs));
        return tcs.Task;
    }

    private IEnumerator SpawnRoutine(BoardTokenVisualizer newVisual, FieldableCardInstance cardInstance,
        TaskCompletionSource<bool> tcs)
    {
        int newIndex = cardsInPortal.Count - 1;
        Vector3 home = LayoutPosition(newIndex);

        // Everything already on the field parts aside to bare the portal, then
        // springs back. Each card slides toward whichever side of the portal it
        // sits on, only as far as it takes to clear the decal's footprint — so a
        // card covering the left of the portal exits left, one on the right exits
        // right, and cards already clear of it don't move.
        int existing = newIndex;
        var slots = new Vector3[existing];
        var recoiled = new Vector3[existing];
        GetPortalSpanX(out float portalCenterX, out float portalHalfX);
        float clear = portalHalfX + cardSpacing * 0.5f + spawnRevealGap;
        for (int i = 0; i < existing; i++)
        {
            slots[i] = LayoutPosition(i);
            float dir = slots[i].x >= portalCenterX ? 1f : -1f;
            float edge = portalCenterX + dir * clear; // nearest fully-clear position on that side
            float x = dir > 0f ? Mathf.Max(slots[i].x, edge) : Mathf.Min(slots[i].x, edge);
            recoiled[i] = new Vector3(x, slots[i].y, slots[i].z);
        }

        // Erupt from the portal decal itself (ground level); fall back to the
        // slot if the mouth couldn't be resolved so the card still shows.
        Vector3 launch = portalMouth != null
            ? new Vector3(portalMouth.position.x, 0f, portalMouth.position.z)
            : home;

        Transform t = newVisual.transform;
        Quaternion rest = Quaternion.Euler(90f, 0f, 0f); // fielded-card orientation
        Vector3 fullScale = t.localScale;
        Vector3 startScale = fullScale * spawnStartScale;
        Vector3 tumbleAxis = spawnTumbleAxis == Vector3.zero ? Vector3.right : spawnTumbleAxis.normalized;

        // Pin it at the portal immediately so it never flashes at its slot first.
        t.position = launch;
        t.localScale = startScale;
        t.rotation = rest * Quaternion.AngleAxis(spawnTumbleAngle, tumbleAxis);

        if (portalGlow != null) portalGlow.Pulse();

        // Phase 1 — existing cards rubberband outward to reveal the portal.
        float e = 0f;
        while (e < spawnRevealDuration)
        {
            float k = EaseOutCubic(spawnRevealDuration > 0f ? e / spawnRevealDuration : 1f);
            for (int i = 0; i < existing; i++)
                cardsInPortal[i].visual.transform.position = Vector3.LerpUnclamped(slots[i], recoiled[i], k);
            e += Time.deltaTime;
            yield return null;
        }
        for (int i = 0; i < existing; i++)
            cardsInPortal[i].visual.transform.position = recoiled[i];

        // Phase 2 — minion arcs out and tumbles home while the cards spring back.
        cardInstance.StartOnPlayedAudio();
        e = 0f;
        float flight = Mathf.Max(0.0001f, spawnFlightDuration);
        while (e < flight)
        {
            float p = e / flight;

            float travel = EaseOutCubic(p);
            Vector3 ground = Vector3.LerpUnclamped(launch, home, travel);
            float lift = spawnArcHeight * 4f * p * (1f - p); // parabola: 0 at ends, peak mid-flight
            t.position = new Vector3(ground.x, ground.y + lift, ground.z);
            t.rotation = rest * Quaternion.AngleAxis(spawnTumbleAngle * (1f - travel), tumbleAxis);
            t.localScale = Vector3.LerpUnclamped(startScale, fullScale, EaseOutCubic(Mathf.Clamp01(p * 1.5f)));

            float rk = EaseOutBack(spawnReturnDuration > 0f ? Mathf.Clamp01(e / spawnReturnDuration) : 1f);
            for (int i = 0; i < existing; i++)
                cardsInPortal[i].visual.transform.position = Vector3.LerpUnclamped(recoiled[i], slots[i], rk);

            e += Time.deltaTime;
            yield return null;
        }

        // Settle everything onto its exact rest pose.
        t.position = home;
        t.rotation = rest;
        t.localScale = fullScale;
        for (int i = 0; i < existing; i++)
            cardsInPortal[i].visual.transform.position = slots[i];

        tcs.SetResult(true);
    }

    private static float EaseOutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);

    // Overshoots slightly past the target near the end before settling — gives
    // the returning cards a springy snap-back.
    private static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        float u = t - 1f;
        return 1f + c3 * u * u * u + c1 * u * u;
    }

    public int GetCardCount()
    {
        return cardsInPortal.Count;
    }

    public void RemoveCard(FieldableCardInstance cardInstance, bool playDeathBurn = false)
    {
        int index = cardsInPortal.FindIndex(c => c.context == cardInstance);
        if (index == -1) return;

        // Any continuous effects granted by this card end when it leaves the
        // field (covers items removed when their holder dies — no death batch).
        cardInstance.Board?.AuraRegistry.UnregisterAllFrom(cardInstance);

        // An item's stat buffs are equipment, not battlecries: "Holder: +X"
        // must not outlive the item (e.g. the holder keeping +2 ATK after the
        // item was discarded off the board). Minions are exempt — their
        // on-play buffs are permanent by design.
        if (cardInstance is ItemInstance leavingItem)
        {
            cardInstance.Board?.RemoveModifiersGrantedBy(cardInstance);

            // Same equipment rule for statuses the item put on its HOLDER
            // (lantern's damage limit, amulette's damage block, mask's infect-
            // on-attack). Holder-scoped on purpose: statuses the item applied
            // to OTHERS (Hidden Grenade's death-stun) are applied by the very
            // cascade that removes the item and must survive it. Fire-and-
            // forget: the removal path is synchronous in practice, and this
            // method must stay callable from the sync death cascade.
            if (leavingItem.ItemHolder != null)
            {
                _ = leavingItem.ItemHolder.RemoveStatusEffectsFrom(leavingItem);
            }
        }

        // Retire the visual. On the death path a minion burns to ash rather than
        // just vanishing: hand the token to BurnDeathEffect, which flattens it to
        // a texture, destroys it, and plays the burn on a detached quad. Every
        // other removal path (discard, lane clear, dependent-item cascade) just
        // destroys it as before. A failed/absent burn falls back to Destroy.
        var visual = cardsInPortal[index].visual;
        bool burned = false;
        if (playDeathBurn && cardInstance is MinionInstance && BurnDeathEffect.Instance != null)
        {
            burned = BurnDeathEffect.Instance.Play(visual);
        }
        if (!burned)
        {
            Destroy(visual.gameObject);
        }

        // remove from list
        cardsInPortal.RemoveAt(index);

        if (index < cardsInPortal.Count)
        {
            var nextCard = cardsInPortal[index];
            nextCard.context.DetachCardFromThis();
            nextCard.visual.UpdateFieldCoverDisplay();

            if (nextCard.context is ItemInstance)
            {
                RemoveCard(nextCard.context); //recursivly removes spells or items that depend on a minion to be present
            }
        }

        // A burn leaves the corpse's slot open while the ash plays there, then
        // slides survivors into the gap with a tween; every other path snaps.
        if (burned)
        {
            ScheduleReflow();
        }
        else
        {
            UpdateCardPositions();
        }
    }

    // Slide survivors into their new slots after a burning death, instead of
    // snapping. The delay lets the ash effect play in the vacated slot first (or
    // overlaps it when earlyReflow is on). Restart-safe: batched deaths reset the
    // timer, and the final tween always targets the current layout.
    private void ScheduleReflow()
    {
        if (reflowRoutine != null) StopCoroutine(reflowRoutine);
        reflowRoutine = StartCoroutine(ReflowRoutine());
    }

    private IEnumerator ReflowRoutine()
    {
        float burn = BurnDeathEffect.Instance != null ? BurnDeathEffect.Instance.BurnDuration : 0.5f;
        float delay = earlyReflow ? burn * earlyReflowFraction : burn;

        float wait = 0f;
        while (wait < delay)
        {
            wait += Time.unscaledDeltaTime;
            yield return null;
        }

        // Snapshot AFTER the delay so the tween reflects whatever the list is now.
        int n = cardsInPortal.Count;
        var start = new Vector3[n];
        var target = new Vector3[n];
        for (int i = 0; i < n; i++)
        {
            var v = cardsInPortal[i].visual;
            start[i] = v != null ? v.transform.position : LayoutPosition(i);
            target[i] = LayoutPosition(i);
        }

        float dur = Mathf.Max(0.0001f, reflowTweenDuration);
        float e = 0f;
        while (e < dur)
        {
            float k = EaseOutCubic(e / dur);
            for (int i = 0; i < n && i < cardsInPortal.Count; i++)
            {
                if (cardsInPortal[i].visual != null)
                    cardsInPortal[i].visual.transform.position = Vector3.LerpUnclamped(start[i], target[i], k);
            }

            e += Time.unscaledDeltaTime;
            yield return null;
        }

        UpdateCardPositions();
        reflowRoutine = null;
    }

    public FieldableCardInstance GetCard(int index)
    {
        if (index < 0 || index >= cardsInPortal.Count) return null;
        return cardsInPortal[index].context;
    }

    // The card sitting directly beneath the given one in the stack (the card it
    // was placed on top of), or null if it's at the bottom or not present. Used
    // when discarding a rune-supplying item so the card it was activating can
    // release those runes.
    public FieldableCardInstance GetCardDirectlyBelow(FieldableCardInstance card)
    {
        int index = cardsInPortal.FindIndex(c => c.context == card);
        if (index <= 0) return null;
        return cardsInPortal[index - 1].context;
    }

    public MinionInstance GetMinion(int n)
    {
        int count = 0;

        foreach (var entry in cardsInPortal)
        {
            if (entry.context.SourceCard.cardType is MinionType)
            {
                if (count == n)
                    return entry.context as MinionInstance;

                count++;
            }
        }

        return null; // not enough minions
    }

    public int GetMinionPosition(FieldableCardInstance fieldableCardInstance)
    {
        int count = 0;

        foreach (var entry in cardsInPortal)
        {
            if (entry.context.SourceCard.cardType is MinionType)
            {
                if (entry.context == fieldableCardInstance)
                    return count;

                count++;
            }
        }

        return -1; // not found or not a minion
    }

    public List<MinionInstance> GetAllMinionsInPortal()
    {
        List<MinionInstance> minions = new List<MinionInstance>();

        foreach (var entry in cardsInPortal)
        {
            if (entry.context.SourceCard.cardType is MinionType) minions.Add(entry.context as MinionInstance);
        }

        return minions;
    }

    public List<FieldableCardInstance> GetAllCardsInPortal()
    {
        List<FieldableCardInstance> cards = new List<FieldableCardInstance>();
        foreach (var entry in cardsInPortal) cards.Add(entry.context);
        return cards;
    }

    // First minion that can be picked by default attack targeting. A TAUNTING
    // minion is picked before anything else and even while stealthed (you can't
    // taunt and hide); otherwise stealthed units are skipped (Stealth =
    // untargetable, not damage-immune).
    public MinionInstance GetFirstTargetableMinion()
    {
        foreach (var entry in cardsInPortal)
        {
            if (entry.context is MinionInstance minion && minion.HasStatusEffect(StatusEffectType.Taunt))
                return minion;
        }

        foreach (var entry in cardsInPortal)
        {
            if (entry.context is MinionInstance minion && !minion.HasStatusEffect(StatusEffectType.Stealth))
                return minion;
        }

        return null;
    }

    // Moves a minion to the front (combat position) or back of this portal.
    // The minion's attached items/spells directly follow it in the stack and
    // move with it as one block, so holder relationships stay intact.
    public void MoveMinion(MinionInstance minion, bool toFront)
    {
        int index = cardsInPortal.FindIndex(c => c.context == minion);
        if (index == -1) return;

        int blockEnd = index + 1;
        while (blockEnd < cardsInPortal.Count && cardsInPortal[blockEnd].context is not MinionInstance)
            blockEnd++;

        var block = cardsInPortal.GetRange(index, blockEnd - index);
        cardsInPortal.RemoveRange(index, blockEnd - index);

        if (toFront) cardsInPortal.InsertRange(0, block);
        else cardsInPortal.AddRange(block);

        UpdateCardPositions();
    }

    // Removes and returns the full card stack without destroying visuals or
    // detaching anything — used by Board.ShiftLanes to move stacks between
    // portals. Pair with ReceiveCards on the destination portal.
    public List<(FieldableCardInstance context, BoardTokenVisualizer visual)> TakeAllCards()
    {
        var taken = new List<(FieldableCardInstance, BoardTokenVisualizer)>(cardsInPortal);
        cardsInPortal.Clear();
        return taken;
    }

    public void ReceiveCards(List<(FieldableCardInstance context, BoardTokenVisualizer visual)> cards, Lane lane)
    {
        foreach (var entry in cards)
        {
            entry.context.SetSourcePortal(this).SetTargetLane(lane);
            cardsInPortal.Add(entry);
        }

        UpdateCardPositions();
    }
}