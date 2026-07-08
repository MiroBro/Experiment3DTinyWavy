using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

/// <summary>
/// The glue for the mermaid gem game: owns the <see cref="MermaidGameState"/>, turns the
/// forager's rummages into item finds (rarity roll → item, charm-driven quality roll,
/// lustrous chance, buffs), runs the clickable rummage-glint minigame, applies locations
/// (world re-theme) and cosmetics, and auto-saves.
///
/// Lives in the scene; finds the runtime-built mermaid pieces in Start (after
/// Mermaid2DBootstrap's Awake has run).
/// </summary>
public class GemGameManager : MonoBehaviour
{
    public static GemGameManager Instance { get; private set; }

    [Header("Glints (click-to-help minigame)")]
    [Tooltip("Max glints that surface per rummage.")]
    public int glintsPerRummage = 3;
    [Tooltip("Seconds between glint spawns while she rummages.")]
    public Vector2 glintInterval = new Vector2(0.5f, 1.1f);
    public float glintLifetime = 1.4f;

    [Header("Wiring (found automatically)")]
    public Mermaid2DBootstrap bootstrap;
    public Mermaid2DForager forager;
    public Underwater2DAtmosphere atmosphere;
    public MermaidSurfaceTrip surfaceTrip;

    public MermaidGameState State { get; private set; }

    /// <summary>UI hooks.</summary>
    public System.Action<string> onToast;
    public System.Action onStateChanged;

    readonly List<RummageGlint> glints = new List<RummageGlint>();
    int glintClicksThisRummage;
    float glintSpawnTimer;
    int glintsSpawnedThisRummage;
    bool wasRummaging;
    float saveTimer;
    float defaultMinCruise = 1f, defaultMaxCruise = 5f;
    System.Random rng = new System.Random();

    readonly Dictionary<int, Sprite> itemSprites = new Dictionary<int, Sprite>();

    void Awake()
    {
        Instance = this;
        State = MermaidGameState.LoadOrNew();
    }

    bool _wired;

    void Start()
    {
        EnsureEventSystem();
        EnsureWiring();
    }

    // The mermaid may be built a frame late (Enter Play Mode Options), so wiring retries
    // from Update until the forager exists.
    void EnsureWiring()
    {
        if (_wired) return;
        if (bootstrap == null) bootstrap = FindAnyObjectByType<Mermaid2DBootstrap>();
        if (forager == null) forager = FindAnyObjectByType<Mermaid2DForager>();
        if (atmosphere == null) atmosphere = FindAnyObjectByType<Underwater2DAtmosphere>();
        if (forager == null) return;

        _wired = true;
        forager.gameManager = this;
        defaultMinCruise = forager.minCruise;
        defaultMaxCruise = forager.maxCruise;
        ApplyLocationVisuals();
        ApplyEquippedCosmetics();
        onStateChanged?.Invoke();
    }

    static void EnsureEventSystem()
    {
        if (FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() != null) return;
        var go = new GameObject("EventSystem");
        go.AddComponent<UnityEngine.EventSystems.EventSystem>();
        go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
    }

    void Update()
    {
        EnsureWiring();
        State.PruneBuffs();
        State.EnsureQuests();
        UpdateHaste();
        UpdateGlints();
        HandleClicks();

        saveTimer += Time.deltaTime;
        if (saveTimer > 30f) { saveTimer = 0f; State.Save(); }
    }

    void OnApplicationPause(bool paused) { if (paused) State?.Save(); }
    void OnApplicationQuit() { State?.Save(); }

    // ---------------------------------------------------------------- finds

    /// <summary>
    /// Called by the forager instead of its legacy gem/rock spawn. Rolls what she found.
    /// </summary>
    public void OnForageFind(Vector3 spot, Transform handForFollow)
    {
        if (State.SatchelFull)
        {
            Toast("Satchel is full! Sell some treasure.");
            glintClicksThisRummage = 0;
            return;
        }

        int finds = 1;
        if (State.HasBuff(MermaidGameDefs.BuffTwin) && rng.NextDouble() < 0.35) finds = 2;

        for (int i = 0; i < finds && !State.SatchelFull; i++)
        {
            var item = RollItem();
            int quality = RollQuality();
            bool lustrous = rng.NextDouble() < MermaidGameDefs.LustrousChance;

            bool newDiscovery = !State.discovered.Contains(item.id);
            State.AddItem(item.id, quality, lustrous);

            long gainedXp = item.xp * (quality + 1);
            if (State.HasBuff(MermaidGameDefs.BuffXp)) gainedXp *= 2;
            int levelsGained = State.GainXp(gainedXp);

            // Visual: pop the actual item out of the grass, colored like the item.
            Color popColor = lustrous ? Color.Lerp(item.color, Color.white, 0.5f) : item.color;
            CollectedItem2D.Spawn(spot + Vector3.right * (i * 0.15f), handForFollow, !item.isRock, popColor);

            if (lustrous) Toast($"LUSTROUS {item.name}!!");
            else if (newDiscovery) Toast($"New discovery: {item.name}!");
            else if (quality >= 5) Toast($"{MermaidGameDefs.QualityNames[quality]} {item.name}!");
            if (levelsGained > 0) Toast($"Level up! Now level {State.level}.");
        }

        glintClicksThisRummage = 0;
        State.Save();
        onStateChanged?.Invoke();
    }

    MermaidGameDefs.ItemDef RollItem()
    {
        // Rarity roll from global weights, then a random item of that band in this location.
        float total = 0f;
        foreach (var w in MermaidGameDefs.RarityWeights) total += w;
        float roll = (float)rng.NextDouble() * total;
        int rarity = 0;
        for (int i = 0; i < MermaidGameDefs.RarityWeights.Length; i++)
        {
            roll -= MermaidGameDefs.RarityWeights[i];
            if (roll <= 0f) { rarity = i; break; }
        }

        var candidates = new List<MermaidGameDefs.ItemDef>();
        foreach (var it in MermaidGameDefs.Items)
            if (it.location == State.currentLocation && it.rarity == rarity) candidates.Add(it);
        if (candidates.Count == 0) candidates.Add(MermaidGameDefs.Items[State.currentLocation * 20]);
        return candidates[rng.Next(candidates.Count)];
    }

    int RollQuality()
    {
        var charm = MermaidGameDefs.Charms[Mathf.Clamp(State.equippedCharm, 0, MermaidGameDefs.Charms.Length - 1)];
        float roll = (float)rng.NextDouble();
        int quality = 0;
        for (int i = 0; i < charm.qualityProbs.Length; i++)
        {
            roll -= charm.qualityProbs[i];
            if (roll <= 0f) { quality = i; break; }
        }

        // Kelp Tea buff: one bonus chance to grade up.
        if (State.HasBuff(MermaidGameDefs.BuffLuck) && rng.NextDouble() < 0.5) quality++;

        // Glint clicks: each click is worth GlintPower points; every 2 points is a
        // guaranteed grade-up chance (Cornerpond's OrbPower analog).
        int points = glintClicksThisRummage * State.GlintPower;
        quality += points / 2;
        if ((points % 2) == 1 && rng.NextDouble() < 0.5) quality++;

        return Mathf.Clamp(quality, 0, 6);
    }

    // ---------------------------------------------------------------- glints

    void UpdateGlints()
    {
        glints.RemoveAll(g => g == null);
        bool rummaging = forager != null && forager.IsRummaging;
        if (rummaging && !wasRummaging)
        {
            glintsSpawnedThisRummage = 0;
            glintClicksThisRummage = 0;
            glintSpawnTimer = Random.Range(glintInterval.x, glintInterval.y) * 0.5f;
        }
        wasRummaging = rummaging;
        if (!rummaging) return;

        glintSpawnTimer -= Time.deltaTime;
        if (glintSpawnTimer <= 0f && glintsSpawnedThisRummage < glintsPerRummage)
        {
            glintSpawnTimer = Random.Range(glintInterval.x, glintInterval.y);
            glintsSpawnedThisRummage++;

            Vector3 basePos = transform.position;
            if (forager.handNear != null) basePos = forager.handNear.transform.position;
            basePos += new Vector3(Random.Range(-0.55f, 0.55f), Random.Range(-0.30f, 0.05f), 0f);
            var glint = RummageGlint.Spawn(basePos, glintLifetime, 21);
            glint.onClicked = OnGlintClicked;
            glints.Add(glint);
        }
    }

    void OnGlintClicked(RummageGlint g)
    {
        glintClicksThisRummage++;
        State.GainXp(1);
    }

    void HandleClicks()
    {
        var mouse = Mouse.current;
        if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;
        if (glints.Count == 0) return;

        Vector2 world;
        if (!ScreenToWorld(mouse.position.ReadValue(), out world)) return;
        foreach (var g in glints)
            if (g != null && g.TryClick(world)) break;
    }

    /// <summary>Screen → game-world point, aware of the corner-widget RenderTexture.</summary>
    public static bool ScreenToWorld(Vector2 screenPos, out Vector2 world)
    {
        world = default;
        var cam = Camera.main;
        if (cam == null) return false;
        var widget = FindAnyObjectByType<CornerWidget2D>();
        if (widget != null && widget.enabled)
            return widget.ScreenToGameWorld(screenPos, cam, out world);
        Vector3 w = cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, 0f));
        world = new Vector2(w.x, w.y);
        return true;
    }

    // ---------------------------------------------------------------- buffs

    void UpdateHaste()
    {
        if (forager == null) return;
        if (State.HasBuff(MermaidGameDefs.BuffHaste))
        {
            forager.minCruise = 0.5f;
            forager.maxCruise = 1.4f;
        }
        else
        {
            forager.minCruise = defaultMinCruise;
            forager.maxCruise = defaultMaxCruise;
        }
    }

    public bool UseConsumable(int id)
    {
        if (id < 0 || id >= State.consumableCounts.Count || State.consumableCounts[id] <= 0) return false;
        State.consumableCounts[id]--;
        var def = MermaidGameDefs.Consumables[id];
        State.activeBuffs.Add(new MermaidGameState.Buff
        {
            id = id,
            endUnix = MermaidGameState.NowUnix() + (long)def.durationSeconds,
        });
        Toast($"{def.name} active!");
        State.Save();
        onStateChanged?.Invoke();
        return true;
    }

    // ---------------------------------------------------------------- shop actions

    bool Spend(long cost)
    {
        if (State.money < cost) { Toast("Not enough coins."); return false; }
        State.money -= cost;
        return true;
    }

    public void BuyCharm(int id)
    {
        var def = MermaidGameDefs.Charms[id];
        if (State.ownedCharms.Contains(id)) { State.equippedCharm = id; Changed(); return; }
        if (!Spend(def.price)) return;
        State.ownedCharms.Add(id);
        State.equippedCharm = id;
        Toast($"{def.name} equipped!");
        Changed();
    }

    public void BuySatchelUpgrade()
    {
        int next = State.satchelTier + 1;
        if (next >= MermaidGameDefs.SatchelSizes.Length) return;
        if (!Spend(MermaidGameDefs.SatchelPrices[next])) return;
        State.satchelTier = next;
        Toast($"Satchel upgraded: {State.SatchelCapacity} slots!");
        Changed();
    }

    public void BuyGlintUpgrade()
    {
        int next = State.glintTier + 1;
        if (next >= MermaidGameDefs.GlintPower.Length) return;
        if (!Spend(MermaidGameDefs.GlintPrices[next])) return;
        State.glintTier = next;
        Toast("Your glint-spotting is sharper!");
        Changed();
    }

    public void BuyConsumable(int id)
    {
        var def = MermaidGameDefs.Consumables[id];
        if (!Spend(def.price)) return;
        State.consumableCounts[id]++;
        Changed();
    }

    public void BuyOrEquipCosmetic(int id)
    {
        var def = MermaidGameDefs.Cosmetics[id];
        if (!State.ownedCosmetics.Contains(id))
        {
            if (!Spend(def.price)) return;
            State.ownedCosmetics.Add(id);
        }
        if (def.isTail) State.equippedTail = id; else State.equippedHair = id;
        ApplyEquippedCosmetics();
        Changed();
    }

    public void UnlockOrTravel(int locationId)
    {
        var def = MermaidGameDefs.Locations[locationId];
        if (!State.unlockedLocations.Contains(locationId))
        {
            if (!Spend(def.travelCost)) return;
            State.unlockedLocations.Add(locationId);
            Toast($"{def.name} unlocked!");
        }
        State.currentLocation = locationId;
        ApplyLocationVisuals();
        Changed();
    }

    public void SellAll()
    {
        long got = State.SellAll();
        if (got > 0) Toast($"Sold everything for {MermaidGameDefs.MoneyString(got)} coins!");
        Changed();
    }

    /// <summary>UI hook: send her up to the boat to sell her rocks to the crow.</summary>
    public void GoToSurface()
    {
        EnsureWiring();
        if (forager == null || forager.swimmer == null) return;
        if (surfaceTrip != null && surfaceTrip.IsActive) return;
        if (surfaceTrip == null)
        {
            var go = new GameObject("MermaidSurfaceTrip");
            surfaceTrip = go.AddComponent<MermaidSurfaceTrip>();
            surfaceTrip.manager = this;
            surfaceTrip.forager = forager;
            surfaceTrip.swimmer = forager.swimmer;
        }
        surfaceTrip.Begin();
    }

    /// <summary>
    /// Sell every ROCK stack in the satchel (gems stay — those go through My Gems). Called
    /// by the surface trip when she reaches the crow's boat. Returns coins earned.
    /// </summary>
    public long SellRocksToCrow(out int rocksSold)
    {
        rocksSold = 0;
        long total = 0;
        for (int i = State.inventory.Count - 1; i >= 0; i--)
        {
            var s = State.inventory[i];
            var def = MermaidGameDefs.Item(s.itemId);
            if (def == null || !def.isRock) continue;
            rocksSold += s.count;
            total += State.SellStack(s);
        }
        if (rocksSold > 0)
            Toast($"The crow collects {rocksSold} rock{(rocksSold == 1 ? "" : "s")}! +{MermaidGameDefs.MoneyString(total)} coins");
        else
            Toast("The crow caws — you have no rocks to sell.");
        Changed();
        return total;
    }

    public void SellStack(MermaidGameState.InvStack s)
    {
        long got = State.SellStack(s);
        Toast($"+{MermaidGameDefs.MoneyString(got)} coins");
        Changed();
    }

    public void HandInQuest(MermaidGameState.Quest q)
    {
        if (State.HandInQuest(q))
        {
            Toast($"Quest complete! +{MermaidGameDefs.MoneyString(q.rewardMoney)} coins, +{q.rewardXp} xp");
            Changed();
        }
        else Toast("You don't have those yet.");
    }

    void Changed() { State.Save(); onStateChanged?.Invoke(); }
    void Toast(string msg) { onToast?.Invoke(msg); }

    // ---------------------------------------------------------------- world application

    public void ApplyLocationVisuals()
    {
        var loc = MermaidGameDefs.Locations[Mathf.Clamp(State.currentLocation, 0, MermaidGameDefs.Locations.Length - 1)];
        if (atmosphere == null) atmosphere = FindAnyObjectByType<Underwater2DAtmosphere>();
        if (atmosphere != null)
        {
            atmosphere.deepColor = loc.waterDeep;
            atmosphere.horizonColor = loc.waterHorizon;
            atmosphere.seabedColor = loc.seabed;
            atmosphere.godRayColor = loc.godRay;
            atmosphere.Rebuild();
        }
        foreach (var field in FindObjectsByType<Seaweed2D>(FindObjectsSortMode.None))
        {
            field.colorA = loc.seaweedA;
            field.colorB = loc.seaweedB;
            field.Build();
        }
    }

    public void ApplyEquippedCosmetics()
    {
        if (bootstrap == null) return;
        var hair = MermaidGameDefs.Cosmetics[Mathf.Clamp(State.equippedHair, 0, MermaidGameDefs.Cosmetics.Length - 1)];
        var tail = MermaidGameDefs.Cosmetics[Mathf.Clamp(State.equippedTail, 0, MermaidGameDefs.Cosmetics.Length - 1)];
        bootstrap.ApplyCosmeticPalette(hair.primary, hair.secondary, tail.primary, tail.secondary);
    }

    // ---------------------------------------------------------------- item icons

    /// <summary>Small generated icon sprite for an item (cached). Diamond for gems, blob for rocks.</summary>
    public Sprite GetItemIcon(int itemId)
    {
        if (itemSprites.TryGetValue(itemId, out var cached) && cached != null) return cached;
        var def = MermaidGameDefs.Item(itemId);
        if (def == null) return null;

        const int S = 40;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        var px = new Color32[S * S];
        var c = def.color;
        var seedRng = new System.Random(itemId * 31 + 7);
        float cx = S * 0.5f, cy = S * 0.5f;
        for (int y = 0; y < S; y++)
        {
            for (int x = 0; x < S; x++)
            {
                float dx = (x - cx) / (S * 0.42f), dy = (y - cy) / (S * 0.46f);
                bool inside;
                if (def.isRock)
                {
                    float wob = 1f + 0.16f * Mathf.Sin(Mathf.Atan2(dy, dx) * 5f + itemId);
                    inside = (dx * dx + dy * dy) < wob * 0.75f;
                }
                else
                {
                    inside = (Mathf.Abs(dx) + Mathf.Abs(dy)) < 1f;   // diamond
                }
                if (inside)
                {
                    float bright = 0.75f + 0.45f * Mathf.Clamp01(0.5f - dy * 0.7f - dx * 0.25f);
                    var col = c * bright;
                    col.a = 1f;
                    px[y * S + x] = col;
                }
                else px[y * S + x] = new Color32(0, 0, 0, 0);
            }
        }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        var sprite = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100f);
        itemSprites[itemId] = sprite;
        return sprite;
    }
}
