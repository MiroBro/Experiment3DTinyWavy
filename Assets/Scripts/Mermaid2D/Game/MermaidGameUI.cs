using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// The whole game UI (Cornerpond-style): HUD (coins, level bar, satchel, location, buffs),
/// a button column, and popup panels — My Gems (sell), Journal, Quests, Shop
/// (Charms/Boosts/Upgrades/Cosmetics tabs) and Travel.
///
/// EDITOR-FRIENDLY BY DESIGN: this component generates its Canvas hierarchy ONCE in edit
/// mode (when it has no children), as real saveable scene objects — so every panel, button
/// and row template is visible and restylable in the editor before pressing Play. Repeating
/// rows (inventory stacks, shop entries, journal slots) are cloned at runtime from the
/// "Template" children — restyle a template and every row follows. Rename freely EXCEPT the
/// functional names (they're how the code finds its parts); right-click → "Regenerate UI"
/// resets everything to stock.
/// </summary>
[ExecuteAlways]
public class MermaidGameUI : MonoBehaviour
{
    static readonly Color PanelBg = new Color(0.05f, 0.10f, 0.20f, 0.88f);
    static readonly Color RowBg = new Color(0.10f, 0.17f, 0.30f, 0.85f);
    static readonly Color Accent = new Color(1f, 0.75f, 0.28f);
    static readonly Color BtnBg = new Color(0.13f, 0.30f, 0.45f, 0.95f);
    static readonly Color TextCol = new Color(0.92f, 0.96f, 1f);

    GemGameManager mgr;
    Font font;
    readonly List<GameObject> openable = new List<GameObject>();
    string toastMsg = "";
    float toastTimer;
    float hudTimer;
    int shopTab;
    int journalTab;

    Font UIFont
    {
        get
        {
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return font;
        }
    }

    // ---------------------------------------------------------------- lifecycle

    void OnEnable()
    {
        if (transform.childCount == 0) GenerateUI();
        else EnsureSurfaceButton();
    }

    void Start()
    {
        if (!Application.isPlaying) return;
        if (transform.childCount == 0) GenerateUI();
        else EnsureSurfaceButton();
        mgr = GemGameManager.Instance != null ? GemGameManager.Instance : FindAnyObjectByType<GemGameManager>();
        if (mgr != null)
        {
            mgr.onToast += ShowToast;
            mgr.onStateChanged += RefreshAll;
        }
        WireButtons();
        CloseAllPanels();
        RefreshAll();
    }

    void Update()
    {
        if (!Application.isPlaying) return;
        hudTimer -= Time.deltaTime;
        if (hudTimer <= 0f) { hudTimer = 0.25f; RefreshHud(); RefreshQuestTimer(); }

        if (toastTimer > 0f)
        {
            toastTimer -= Time.deltaTime;
            var t = FindText("Toast/ToastText");
            var img = FindComp<Image>("Toast");
            float a = Mathf.Clamp01(toastTimer / 0.6f);
            if (t != null) { t.text = toastMsg; var c = t.color; c.a = a; t.color = c; }
            if (img != null) { var c = img.color; c.a = 0.85f * a; img.color = c; }
        }
    }

    void ShowToast(string msg) { toastMsg = msg; toastTimer = 3f; }

    // ---------------------------------------------------------------- generation

    [ContextMenu("Regenerate UI")]
    public void RegenerateUI()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var child = transform.GetChild(i).gameObject;
            if (Application.isPlaying) Destroy(child); else DestroyImmediate(child);
        }
        GenerateUI();
    }

    void GenerateUI()
    {
        var canvas = Ensure<Canvas>(gameObject);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 120;
        var scaler = Ensure<CanvasScaler>(gameObject);
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600, 900);
        scaler.matchWidthOrHeight = 0.5f;
        Ensure<GraphicRaycaster>(gameObject);

        BuildHud();
        BuildButtonColumn();
        BuildToast();
        BuildInventoryPanel();
        BuildJournalPanel();
        BuildQuestsPanel();
        BuildShopPanel();
        BuildTravelPanel();
    }

    void BuildHud()
    {
        var hud = Panelish("HUD", new Vector2(0, 1), new Vector2(12, -12), new Vector2(300, 128), PanelBg);
        MakeText(hud, "MoneyText", "0 coins", 24, TextAnchor.MiddleLeft, Accent,
            new Vector2(0, 1), new Vector2(14, -8), new Vector2(272, 30));
        MakeText(hud, "LevelText", "Lv 1", 18, TextAnchor.MiddleLeft, TextCol,
            new Vector2(0, 1), new Vector2(14, -40), new Vector2(70, 24));
        var barBg = MakeImage(hud, "XpBarBg", new Color(0f, 0f, 0f, 0.5f),
            new Vector2(0, 1), new Vector2(88, -44), new Vector2(196, 14));
        var fill = MakeImage(barBg.gameObject, "XpBarFill", Accent,
            new Vector2(0, 0.5f), new Vector2(1, 1), new Vector2(1, 7));
        fill.rectTransform.anchorMin = new Vector2(0, 0);
        fill.rectTransform.anchorMax = new Vector2(0.5f, 1);
        fill.rectTransform.offsetMin = new Vector2(1, 1);
        fill.rectTransform.offsetMax = new Vector2(-1, -1);
        MakeText(hud, "SatchelText", "Satchel 0/20", 16, TextAnchor.MiddleLeft, TextCol,
            new Vector2(0, 1), new Vector2(14, -66), new Vector2(272, 22));
        MakeText(hud, "LocationText", "Seagrass Shallows", 16, TextAnchor.MiddleLeft, new Color(0.6f, 0.9f, 1f),
            new Vector2(0, 1), new Vector2(14, -88), new Vector2(272, 22));
        MakeText(hud, "BuffText", "", 13, TextAnchor.MiddleLeft, new Color(0.75f, 1f, 0.75f),
            new Vector2(0, 1), new Vector2(14, -108), new Vector2(272, 18));
    }

    void BuildButtonColumn()
    {
        var col = Child(gameObject, "Buttons");
        SetRect(col, new Vector2(0, 1), new Vector2(12, -152), new Vector2(120, 352));
        string[] names = { "GemsBtn", "JournalBtn", "QuestsBtn", "ShopBtn", "TravelBtn", "SurfaceBtn" };
        string[] labels = { "My Gems", "Journal", "Quests", "Shop", "Travel", "Surface" };
        for (int i = 0; i < names.Length; i++)
            MakeButton(col, names[i], labels[i], new Vector2(0, 1), new Vector2(0, -i * 52), new Vector2(120, 44));
    }

    // Injects the Surface button into a UI that was generated (and saved into the scene)
    // before the button existed — without forcing a full "Regenerate UI" that would throw
    // away the user's restyling.
    void EnsureSurfaceButton()
    {
        var col = transform.Find("Buttons");
        if (col == null || col.Find("SurfaceBtn") != null) return;
        int slot = col.childCount;
        MakeButton(col.gameObject, "SurfaceBtn", "Surface",
            new Vector2(0, 1), new Vector2(0, -slot * 52), new Vector2(120, 44));
    }

    void BuildToast()
    {
        var toast = Panelish("Toast", new Vector2(0.5f, 0), new Vector2(0, 24), new Vector2(520, 44), PanelBg);
        MakeText(toast, "ToastText", "", 20, TextAnchor.MiddleCenter, Accent,
            new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(500, 36));
    }

    GameObject MakeMainPanel(string name, string title, Vector2 size)
    {
        var panel = Panelish(name, new Vector2(0.5f, 0.5f), Vector2.zero, size, PanelBg);
        MakeText(panel, "Title", title, 26, TextAnchor.MiddleLeft, Accent,
            new Vector2(0, 1), new Vector2(18, -10), new Vector2(size.x - 90, 34));
        MakeButton(panel, "CloseBtn", "X", new Vector2(1, 1), new Vector2(-44, -8), new Vector2(38, 34));
        panel.SetActive(false);
        openable.Add(panel);
        return panel;
    }

    // ScrollRect with a masked viewport + vertical-layout content. Returns the content GO.
    GameObject MakeScrollList(GameObject panel, string name, Vector2 anchoredPos, Vector2 size)
    {
        var scrollGO = Child(panel, name);
        SetRect(scrollGO, new Vector2(0, 1), anchoredPos, size);
        var scroll = scrollGO.AddComponent<ScrollRect>();
        scroll.horizontal = false;

        var viewport = Child(scrollGO, "Viewport");
        SetRect(viewport, new Vector2(0, 1), Vector2.zero, size);
        viewport.AddComponent<RectMask2D>();
        var vpImg = viewport.AddComponent<Image>();
        vpImg.color = new Color(0f, 0f, 0f, 0.18f);

        var content = Child(viewport, "Content");
        SetRect(content, new Vector2(0, 1), Vector2.zero, new Vector2(size.x, 10));
        var layout = content.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 6;
        layout.padding = new RectOffset(6, 6, 6, 6);
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;
        layout.childControlHeight = false;
        layout.childControlWidth = true;
        var fitter = content.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.viewport = viewport.GetComponent<RectTransform>();
        scroll.content = content.GetComponent<RectTransform>();
        return content;
    }

    void BuildInventoryPanel()
    {
        var panel = MakeMainPanel("InventoryPanel", "My Gems", new Vector2(560, 520));
        MakeText(panel, "CapacityText", "0/20", 18, TextAnchor.MiddleRight, TextCol,
            new Vector2(1, 1), new Vector2(-260, -14), new Vector2(160, 26));
        MakeButton(panel, "SellAllBtn", "Sell All", new Vector2(1, 1), new Vector2(-96 - 88, -50), new Vector2(170, 36));
        MakeText(panel, "TotalText", "Worth: 0", 17, TextAnchor.MiddleLeft, Accent,
            new Vector2(0, 1), new Vector2(18, -52), new Vector2(260, 30));
        var content = MakeScrollList(panel, "Scroll", new Vector2(12, -94), new Vector2(536, 412));

        // Row template (disabled) — restyle this and every inventory row follows.
        var row = Panelish("RowTemplate", new Vector2(0, 1), Vector2.zero, new Vector2(520, 46), RowBg, content);
        MakeImage(row, "Icon", Color.white, new Vector2(0, 0.5f), new Vector2(26, 0), new Vector2(34, 34));
        MakeText(row, "Name", "Item", 17, TextAnchor.MiddleLeft, TextCol,
            new Vector2(0, 0.5f), new Vector2(50, 0), new Vector2(260, 40));
        MakeText(row, "Value", "12", 17, TextAnchor.MiddleRight, Accent,
            new Vector2(1, 0.5f), new Vector2(-170, 0), new Vector2(110, 40));
        MakeButton(row, "SellBtn", "Sell", new Vector2(1, 0.5f), new Vector2(-14 - 66, 0), new Vector2(80, 34));
        row.SetActive(false);
    }

    void BuildJournalPanel()
    {
        var panel = MakeMainPanel("JournalPanel", "Journal", new Vector2(640, 560));
        var tabs = Child(panel, "Tabs");
        SetRect(tabs, new Vector2(0, 1), new Vector2(12, -50), new Vector2(616, 34));
        for (int i = 0; i < MermaidGameDefs.Locations.Length; i++)
        {
            MakeButton(tabs, $"Tab{i}", MermaidGameDefs.Locations[i].name.Split(' ')[0],
                new Vector2(0, 0.5f), new Vector2(i * 122, 0), new Vector2(118, 30));
        }
        MakeText(panel, "DiscoveredText", "0/20 discovered", 16, TextAnchor.MiddleRight, TextCol,
            new Vector2(1, 1), new Vector2(-160, -14), new Vector2(300, 26));

        var gridGO = Child(panel, "Grid");
        SetRect(gridGO, new Vector2(0, 1), new Vector2(12, -92), new Vector2(616, 456));
        var grid = gridGO.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(118, 86);
        grid.spacing = new Vector2(6, 6);
        grid.padding = new RectOffset(4, 4, 4, 4);

        var slot = Panelish("SlotTemplate", new Vector2(0, 1), Vector2.zero, new Vector2(118, 86), RowBg, gridGO);
        MakeImage(slot, "Icon", Color.white, new Vector2(0.5f, 1), new Vector2(0, -26), new Vector2(38, 38));
        MakeText(slot, "Name", "???", 12, TextAnchor.UpperCenter, TextCol,
            new Vector2(0.5f, 0), new Vector2(0, 20), new Vector2(112, 34));
        slot.SetActive(false);
    }

    void BuildQuestsPanel()
    {
        var panel = MakeMainPanel("QuestsPanel", "Quests", new Vector2(600, 360));
        MakeText(panel, "TimerText", "New quests in 3:59:59", 16, TextAnchor.MiddleRight, TextCol,
            new Vector2(1, 1), new Vector2(-200, -14), new Vector2(340, 26));
        for (int i = 0; i < MermaidGameDefs.QuestCount; i++)
        {
            var row = Panelish($"QuestRow{i}", new Vector2(0, 1), new Vector2(16, -60 - i * 94), new Vector2(568, 86), RowBg, panel);
            MakeImage(row, "Icon", Color.white, new Vector2(0, 0.5f), new Vector2(32, 0), new Vector2(44, 44));
            MakeText(row, "Desc", "Collect 3x Sandy Pebble", 17, TextAnchor.UpperLeft, TextCol,
                new Vector2(0, 1), new Vector2(64, -10), new Vector2(330, 44));
            MakeText(row, "Reward", "Reward: 120 coins, 30 xp", 14, TextAnchor.LowerLeft, Accent,
                new Vector2(0, 0), new Vector2(64, 8), new Vector2(330, 26));
            MakeText(row, "Progress", "1/3", 20, TextAnchor.MiddleCenter, TextCol,
                new Vector2(1, 0.5f), new Vector2(-146, 0), new Vector2(64, 40));
            MakeButton(row, "HandInBtn", "Hand In", new Vector2(1, 0.5f), new Vector2(-14 - 52, 0), new Vector2(104, 40));
        }
    }

    void BuildShopPanel()
    {
        var panel = MakeMainPanel("ShopPanel", "Shop", new Vector2(640, 560));
        var tabs = Child(panel, "Tabs");
        SetRect(tabs, new Vector2(0, 1), new Vector2(12, -50), new Vector2(616, 34));
        string[] tabNames = { "Charms", "Boosts", "Upgrades", "Cosmetics" };
        for (int i = 0; i < tabNames.Length; i++)
            MakeButton(tabs, $"Tab{i}", tabNames[i], new Vector2(0, 0.5f), new Vector2(i * 152, 0), new Vector2(148, 30));

        var content = MakeScrollList(panel, "Scroll", new Vector2(12, -92), new Vector2(616, 456));
        var row = Panelish("RowTemplate", new Vector2(0, 1), Vector2.zero, new Vector2(600, 64), RowBg, content);
        MakeText(row, "Name", "Coral Comb", 18, TextAnchor.UpperLeft, TextCol,
            new Vector2(0, 1), new Vector2(14, -6), new Vector2(360, 26));
        MakeText(row, "Desc", "Description", 13, TextAnchor.UpperLeft, new Color(0.75f, 0.82f, 0.92f),
            new Vector2(0, 1), new Vector2(14, -30), new Vector2(400, 30));
        MakeText(row, "Price", "400", 18, TextAnchor.MiddleRight, Accent,
            new Vector2(1, 0.5f), new Vector2(-190, 0), new Vector2(130, 40));
        MakeButton(row, "BuyBtn", "Buy", new Vector2(1, 0.5f), new Vector2(-14 - 76, 0), new Vector2(128, 40));
        row.SetActive(false);
    }

    void BuildTravelPanel()
    {
        var panel = MakeMainPanel("TravelPanel", "Travel", new Vector2(560, 420));
        for (int i = 0; i < MermaidGameDefs.Locations.Length; i++)
        {
            var loc = MermaidGameDefs.Locations[i];
            var row = Panelish($"LocRow{i}", new Vector2(0, 1), new Vector2(16, -56 - i * 70), new Vector2(528, 62), RowBg, panel);
            MakeImage(row, "Swatch", loc.waterHorizon, new Vector2(0, 0.5f), new Vector2(30, 0), new Vector2(40, 40));
            MakeText(row, "Name", loc.name, 18, TextAnchor.MiddleLeft, TextCol,
                new Vector2(0, 0.5f), new Vector2(58, 0), new Vector2(250, 50));
            MakeText(row, "Cost", loc.travelCost > 0 ? MermaidGameDefs.MoneyString(loc.travelCost) : "Home", 15,
                TextAnchor.MiddleRight, Accent, new Vector2(1, 0.5f), new Vector2(-170, 0), new Vector2(110, 40));
            MakeButton(row, "GoBtn", "Go", new Vector2(1, 0.5f), new Vector2(-14 - 58, 0), new Vector2(92, 38));
        }
    }

    // ---------------------------------------------------------------- runtime wiring

    void WireButtons()
    {
        openable.Clear();
        foreach (var n in new[] { "InventoryPanel", "JournalPanel", "QuestsPanel", "ShopPanel", "TravelPanel" })
        {
            var p = transform.Find(n);
            if (p != null) openable.Add(p.gameObject);
        }

        Wire("Buttons/GemsBtn", () => TogglePanel("InventoryPanel", RefreshInventory));
        Wire("Buttons/JournalBtn", () => TogglePanel("JournalPanel", RefreshJournal));
        Wire("Buttons/QuestsBtn", () => TogglePanel("QuestsPanel", RefreshQuests));
        Wire("Buttons/ShopBtn", () => TogglePanel("ShopPanel", RefreshShop));
        Wire("Buttons/TravelBtn", () => TogglePanel("TravelPanel", RefreshTravel));
        Wire("Buttons/SurfaceBtn", () => { CloseAllPanels(); mgr?.GoToSurface(); });

        foreach (var p in openable)
        {
            var pp = p;
            Wire(pp.name + "/CloseBtn", () => pp.SetActive(false));
        }

        Wire("InventoryPanel/SellAllBtn", () => { mgr?.SellAll(); RefreshInventory(); });

        for (int i = 0; i < MermaidGameDefs.Locations.Length; i++)
        {
            int idx = i;
            Wire($"JournalPanel/Tabs/Tab{i}", () => { journalTab = idx; RefreshJournal(); });
            Wire($"TravelPanel/LocRow{i}/GoBtn", () => { mgr?.UnlockOrTravel(idx); RefreshTravel(); });
        }
        for (int i = 0; i < 4; i++)
        {
            int idx = i;
            Wire($"ShopPanel/Tabs/Tab{i}", () => { shopTab = idx; RefreshShop(); });
        }
        for (int i = 0; i < MermaidGameDefs.QuestCount; i++)
        {
            int idx = i;
            Wire($"QuestsPanel/QuestRow{i}/HandInBtn", () =>
            {
                if (mgr != null && idx < mgr.State.quests.Count)
                {
                    mgr.HandInQuest(mgr.State.quests[idx]);
                    RefreshQuests();
                }
            });
        }
    }

    void TogglePanel(string name, System.Action refresh)
    {
        var panel = transform.Find(name);
        if (panel == null) return;
        bool show = !panel.gameObject.activeSelf;
        CloseAllPanels();
        panel.gameObject.SetActive(show);
        if (show) refresh?.Invoke();
    }

    void CloseAllPanels()
    {
        foreach (var p in openable) if (p != null) p.SetActive(false);
    }

    // ---------------------------------------------------------------- refresh

    void RefreshAll()
    {
        RefreshHud();
        var inv = transform.Find("InventoryPanel");
        if (inv != null && inv.gameObject.activeSelf) RefreshInventory();
        var j = transform.Find("JournalPanel");
        if (j != null && j.gameObject.activeSelf) RefreshJournal();
        var q = transform.Find("QuestsPanel");
        if (q != null && q.gameObject.activeSelf) RefreshQuests();
        var s = transform.Find("ShopPanel");
        if (s != null && s.gameObject.activeSelf) RefreshShop();
        var t = transform.Find("TravelPanel");
        if (t != null && t.gameObject.activeSelf) RefreshTravel();
    }

    void RefreshHud()
    {
        if (mgr == null) return;
        var st = mgr.State;
        SetText("HUD/MoneyText", MermaidGameDefs.MoneyString(st.money) + " coins");
        SetText("HUD/LevelText", "Lv " + st.level);
        SetText("HUD/SatchelText", $"Satchel {st.UsedSlots}/{st.SatchelCapacity}" + (st.SatchelFull ? "  FULL!" : ""));
        SetText("HUD/LocationText", MermaidGameDefs.Locations[st.currentLocation].name);

        var fill = FindComp<Image>("HUD/XpBarBg/XpBarFill");
        if (fill != null)
        {
            float frac = Mathf.Clamp01((float)st.xp / Mathf.Max(1, MermaidGameDefs.XpToNext(st.level)));
            fill.rectTransform.anchorMax = new Vector2(Mathf.Max(0.02f, frac), 1f);
        }

        string buffs = "";
        foreach (var def in MermaidGameDefs.Consumables)
        {
            float left = st.BuffSecondsLeft(def.id);
            if (left > 0) buffs += $"{def.name.Split(' ')[0]} {(int)left / 60}:{(int)left % 60:D2}   ";
        }
        SetText("HUD/BuffText", buffs);
    }

    void RefreshQuestTimer()
    {
        if (mgr == null) return;
        var t = transform.Find("QuestsPanel");
        if (t == null || !t.gameObject.activeSelf) return;
        double s = mgr.State.QuestSecondsLeft();
        SetText("QuestsPanel/TimerText", $"New quests in {(int)(s / 3600)}:{(int)(s / 60) % 60:D2}:{(int)s % 60:D2}");
    }

    void RefreshInventory()
    {
        if (mgr == null) return;
        var st = mgr.State;
        SetText("InventoryPanel/CapacityText", $"{st.UsedSlots}/{st.SatchelCapacity}");
        long total = 0;
        foreach (var s in st.inventory) total += st.StackValue(s) * s.count;
        SetText("InventoryPanel/TotalText", "Worth: " + MermaidGameDefs.MoneyString(total));

        var content = transform.Find("InventoryPanel/Scroll/Viewport/Content");
        var template = content != null ? content.Find("RowTemplate") : null;
        if (content == null || template == null) return;
        ClearDynamic(content, template);

        foreach (var stack in st.inventory)
        {
            var s = stack;
            var def = MermaidGameDefs.Item(s.itemId);
            if (def == null) continue;
            var row = Instantiate(template.gameObject, content);
            row.name = "Row_" + def.id;
            row.SetActive(true);
            var icon = row.transform.Find("Icon")?.GetComponent<Image>();
            if (icon != null) icon.sprite = mgr.GetItemIcon(def.id);
            var qcol = MermaidGameDefs.QualityColors[Mathf.Clamp(s.quality, 0, 6)];
            SetChildText(row, "Name",
                (s.lustrous ? "Lustrous " : "") + $"{MermaidGameDefs.QualityNames[s.quality]} {def.name}  x{s.count}", qcol);
            SetChildText(row, "Value", MermaidGameDefs.MoneyString(st.StackValue(s) * s.count), Accent);
            var btn = row.transform.Find("SellBtn")?.GetComponent<Button>();
            if (btn != null) btn.onClick.AddListener(() => { mgr.SellStack(s); RefreshInventory(); });
        }
    }

    void RefreshJournal()
    {
        if (mgr == null) return;
        var st = mgr.State;
        int L = Mathf.Clamp(journalTab, 0, MermaidGameDefs.Locations.Length - 1);

        int found = 0;
        for (int i = 0; i < 20; i++) if (st.discovered.Contains(L * 20 + i)) found++;
        SetText("JournalPanel/DiscoveredText", $"{MermaidGameDefs.Locations[L].name}: {found}/20 discovered");

        var grid = transform.Find("JournalPanel/Grid");
        var template = grid != null ? grid.Find("SlotTemplate") : null;
        if (grid == null || template == null) return;
        ClearDynamic(grid, template);

        for (int i = 0; i < 20; i++)
        {
            int id = L * 20 + i;
            var def = MermaidGameDefs.Item(id);
            bool known = st.discovered.Contains(id);
            var slot = Instantiate(template.gameObject, grid);
            slot.name = "Slot_" + id;
            slot.SetActive(true);
            var icon = slot.transform.Find("Icon")?.GetComponent<Image>();
            if (icon != null)
            {
                icon.sprite = mgr.GetItemIcon(id);
                icon.color = known ? Color.white : new Color(0f, 0f, 0f, 0.85f);
            }
            SetChildText(slot, "Name", known ? def.name : "???",
                known ? MermaidGameDefs.RarityColors[def.rarity] : new Color(0.5f, 0.55f, 0.65f));
        }
    }

    void RefreshQuests()
    {
        if (mgr == null) return;
        var st = mgr.State;
        RefreshQuestTimer();
        for (int i = 0; i < MermaidGameDefs.QuestCount; i++)
        {
            var row = transform.Find($"QuestsPanel/QuestRow{i}");
            if (row == null) continue;
            bool has = i < st.quests.Count;
            row.gameObject.SetActive(has);
            if (!has) continue;
            var q = st.quests[i];
            var def = MermaidGameDefs.Item(q.itemId);
            var icon = row.Find("Icon")?.GetComponent<Image>();
            if (icon != null) icon.sprite = mgr.GetItemIcon(q.itemId);
            SetChildText(row.gameObject, "Desc", q.done ? "COMPLETE - thank you!" : $"Collect {q.needed}x {def.name}", TextCol);
            SetChildText(row.gameObject, "Reward", $"Reward: {MermaidGameDefs.MoneyString(q.rewardMoney)} coins, {q.rewardXp} xp", Accent);
            SetChildText(row.gameObject, "Progress", q.done ? "DONE" : $"{Mathf.Min(st.CountOf(q.itemId), q.needed)}/{q.needed}",
                q.done ? new Color(0.5f, 1f, 0.5f) : TextCol);
            var btn = row.Find("HandInBtn")?.GetComponent<Button>();
            if (btn != null) btn.interactable = !q.done && st.CountOf(q.itemId) >= q.needed;
        }
    }

    void RefreshShop()
    {
        if (mgr == null) return;
        var st = mgr.State;
        var content = transform.Find("ShopPanel/Scroll/Viewport/Content");
        var template = content != null ? content.Find("RowTemplate") : null;
        if (content == null || template == null) return;
        ClearDynamic(content, template);

        void AddRow(string name, string desc, string price, string btnLabel, bool interactable, System.Action onBuy)
        {
            var row = Instantiate(template.gameObject, content);
            row.SetActive(true);
            SetChildText(row, "Name", name, TextCol);
            SetChildText(row, "Desc", desc, new Color(0.75f, 0.82f, 0.92f));
            SetChildText(row, "Price", price, Accent);
            var btn = row.transform.Find("BuyBtn")?.GetComponent<Button>();
            if (btn != null)
            {
                btn.interactable = interactable;
                var label = btn.GetComponentInChildren<Text>();
                if (label != null) label.text = btnLabel;
                btn.onClick.AddListener(() => { onBuy?.Invoke(); RefreshShop(); });
            }
        }

        if (shopTab == 0)   // Charms
        {
            foreach (var c in MermaidGameDefs.Charms)
            {
                bool owned = st.ownedCharms.Contains(c.id);
                bool equipped = st.equippedCharm == c.id;
                AddRow(c.name, c.desc,
                    owned ? (equipped ? "EQUIPPED" : "owned") : MermaidGameDefs.MoneyString(c.price),
                    owned ? "Equip" : "Buy", !equipped, () => mgr.BuyCharm(c.id));
            }
        }
        else if (shopTab == 1)   // Boosts
        {
            foreach (var c in MermaidGameDefs.Consumables)
            {
                int count = st.consumableCounts[c.id];
                AddRow($"{c.name}  (have {count})", c.desc, MermaidGameDefs.MoneyString(c.price), "Buy", true,
                    () => mgr.BuyConsumable(c.id));
                if (count > 0)
                    AddRow($"   Use {c.name}", $"{(int)(c.durationSeconds / 60)} minutes", "", "Use", true,
                        () => mgr.UseConsumable(c.id));
            }
        }
        else if (shopTab == 2)   // Upgrades
        {
            if (st.satchelTier + 1 < MermaidGameDefs.SatchelSizes.Length)
                AddRow($"Satchel Upgrade  ({st.SatchelCapacity} → {MermaidGameDefs.SatchelSizes[st.satchelTier + 1]} slots)",
                    "Carry more treasure before selling.",
                    MermaidGameDefs.MoneyString(MermaidGameDefs.SatchelPrices[st.satchelTier + 1]), "Buy", true,
                    () => mgr.BuySatchelUpgrade());
            else AddRow("Satchel Upgrade", "Fully upgraded!", "MAX", "-", false, null);

            if (st.glintTier + 1 < MermaidGameDefs.GlintPower.Length)
                AddRow($"Keen Eyes  (glint power {st.GlintPower} → {MermaidGameDefs.GlintPower[st.glintTier + 1]})",
                    "Each clicked glint boosts find quality more.",
                    MermaidGameDefs.MoneyString(MermaidGameDefs.GlintPrices[st.glintTier + 1]), "Buy", true,
                    () => mgr.BuyGlintUpgrade());
            else AddRow("Keen Eyes", "Fully upgraded!", "MAX", "-", false, null);
        }
        else   // Cosmetics
        {
            foreach (var c in MermaidGameDefs.Cosmetics)
            {
                bool owned = st.ownedCosmetics.Contains(c.id);
                bool equipped = (c.isTail ? st.equippedTail : st.equippedHair) == c.id;
                AddRow(c.name, c.isTail ? "Tail finish" : "Hair dye",
                    owned ? (equipped ? "WEARING" : "owned") : MermaidGameDefs.MoneyString(c.price),
                    owned ? "Wear" : "Buy", !equipped, () => mgr.BuyOrEquipCosmetic(c.id));
            }
        }
    }

    void RefreshTravel()
    {
        if (mgr == null) return;
        var st = mgr.State;
        for (int i = 0; i < MermaidGameDefs.Locations.Length; i++)
        {
            var row = transform.Find($"TravelPanel/LocRow{i}");
            if (row == null) continue;
            bool unlocked = st.unlockedLocations.Contains(i);
            bool here = st.currentLocation == i;
            SetChildText(row.gameObject, "Cost",
                here ? "HERE" : unlocked ? "unlocked" : MermaidGameDefs.MoneyString(MermaidGameDefs.Locations[i].travelCost), Accent);
            var btn = row.Find("GoBtn")?.GetComponent<Button>();
            if (btn != null)
            {
                btn.interactable = !here;
                var label = btn.GetComponentInChildren<Text>();
                if (label != null) label.text = unlocked ? "Go" : "Unlock";
            }
        }
    }

    // ---------------------------------------------------------------- small helpers

    static void ClearDynamic(Transform parent, Transform template)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            var c = parent.GetChild(i);
            if (c != template) Destroy(c.gameObject);
        }
    }

    void Wire(string path, UnityEngine.Events.UnityAction action)
    {
        var t = transform.Find(path);
        var btn = t != null ? t.GetComponent<Button>() : null;
        if (btn == null) { Debug.LogWarning("MermaidGameUI: missing button " + path); return; }
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(action);
    }

    void SetText(string path, string value)
    {
        var t = FindText(path);
        if (t != null) t.text = value;
    }

    void SetChildText(GameObject row, string child, string value, Color color)
    {
        var t = row.transform.Find(child)?.GetComponent<Text>();
        if (t != null) { t.text = value; t.color = color; }
    }

    Text FindText(string path)
    {
        var t = transform.Find(path);
        return t != null ? t.GetComponent<Text>() : null;
    }

    T FindComp<T>(string path) where T : Component
    {
        var t = transform.Find(path);
        return t != null ? t.GetComponent<T>() : null;
    }

    static T Ensure<T>(GameObject go) where T : Component
    {
        var c = go.GetComponent<T>();
        return c != null ? c : go.AddComponent<T>();
    }

    GameObject Child(GameObject parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent.transform, false);
        return go;
    }

    void SetRect(GameObject go, Vector2 anchor, Vector2 anchoredPos, Vector2 size)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = anchor;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;
    }

    GameObject Panelish(string name, Vector2 anchor, Vector2 pos, Vector2 size, Color bg, GameObject parent = null)
    {
        var go = Child(parent != null ? parent : gameObject, name);
        SetRect(go, anchor, pos, size);
        var img = go.AddComponent<Image>();
        img.color = bg;
        return go;
    }

    Text MakeText(GameObject parent, string name, string content, int size, TextAnchor align, Color color,
        Vector2 anchor, Vector2 pos, Vector2 rectSize)
    {
        var go = Child(parent, name);
        SetRect(go, anchor, pos, rectSize);
        var t = go.AddComponent<Text>();
        t.font = UIFont;
        t.fontSize = size;
        t.alignment = align;
        t.color = color;
        t.text = content;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Truncate;
        t.raycastTarget = false;
        return t;
    }

    Image MakeImage(GameObject parent, string name, Color color, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        var go = Child(parent, name);
        SetRect(go, anchor, pos, size);
        var img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    Button MakeButton(GameObject parent, string name, string label, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        var go = Child(parent, name);
        SetRect(go, anchor, pos, size);
        var img = go.AddComponent<Image>();
        img.color = BtnBg;
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        var colors = btn.colors;
        colors.highlightedColor = new Color(1.2f * BtnBg.r, 1.2f * BtnBg.g, 1.2f * BtnBg.b, 1f);
        colors.pressedColor = Accent;
        btn.colors = colors;
        MakeText(go, "Label", label, 17, TextAnchor.MiddleCenter, TextCol,
            new Vector2(0.5f, 0.5f), Vector2.zero, size - new Vector2(6, 4));
        return btn;
    }
}
