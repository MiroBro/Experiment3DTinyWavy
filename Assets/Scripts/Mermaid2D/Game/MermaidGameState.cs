using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Player state for the mermaid gem game: money, XP/level, satchel inventory, journal
/// discoveries, charms/upgrades/consumables/cosmetics, active buffs and 4-hour quests.
/// Saved as JSON in Application.persistentDataPath. Pure data + rules; no Unity scene
/// dependencies (so it stays trivially testable).
/// </summary>
[Serializable]
public class MermaidGameState
{
    public int version = 1;
    public long money = 0;
    public long xp = 0;
    public int level = 1;
    public int currentLocation = 0;
    public List<int> unlockedLocations = new List<int> { 0 };

    [Serializable]
    public class InvStack
    {
        public int itemId;
        public int quality;      // 0..6
        public bool lustrous;
        public int count;
    }
    public List<InvStack> inventory = new List<InvStack>();

    public List<int> discovered = new List<int>();       // item ids ever found
    public List<int> ownedCharms = new List<int> { 0 };
    public int equippedCharm = 0;
    public int satchelTier = 0;
    public int glintTier = 0;
    public List<int> consumableCounts = new List<int> { 0, 0, 0, 0 };
    public List<int> ownedCosmetics = new List<int> { 0, 6 };
    public int equippedHair = 0;
    public int equippedTail = 6;

    [Serializable]
    public class Buff { public int id; public long endUnix; }
    public List<Buff> activeBuffs = new List<Buff>();

    [Serializable]
    public class Quest
    {
        public int itemId;
        public int needed;
        public long rewardMoney;
        public long rewardXp;
        public bool done;
    }
    public List<Quest> quests = new List<Quest>();
    public long questResetUnix = 0;

    public long totalCollected = 0;
    public long totalQuestsDone = 0;

    // ---------------------------------------------------------------- derived
    public int SatchelCapacity => MermaidGameDefs.SatchelSizes[Mathf.Clamp(satchelTier, 0, MermaidGameDefs.SatchelSizes.Length - 1)];
    public int UsedSlots { get { int n = 0; foreach (var s in inventory) n += s.count; return n; } }
    public bool SatchelFull => UsedSlots >= SatchelCapacity;
    public int GlintPower => MermaidGameDefs.GlintPower[Mathf.Clamp(glintTier, 0, MermaidGameDefs.GlintPower.Length - 1)];

    public static long NowUnix() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    public bool HasBuff(int id)
    {
        long now = NowUnix();
        foreach (var b in activeBuffs)
            if (b.id == id && b.endUnix > now) return true;
        return false;
    }

    public float BuffSecondsLeft(int id)
    {
        long now = NowUnix();
        long best = 0;
        foreach (var b in activeBuffs)
            if (b.id == id && b.endUnix > now) best = Math.Max(best, b.endUnix - now);
        return best;
    }

    public void PruneBuffs()
    {
        long now = NowUnix();
        activeBuffs.RemoveAll(b => b.endUnix <= now);
    }

    // ---------------------------------------------------------------- inventory
    public bool AddItem(int itemId, int quality, bool lustrous)
    {
        if (SatchelFull) return false;
        var stack = inventory.FirstOrDefault(s => s.itemId == itemId && s.quality == quality && s.lustrous == lustrous);
        if (stack == null)
        {
            stack = new InvStack { itemId = itemId, quality = quality, lustrous = lustrous, count = 0 };
            inventory.Add(stack);
        }
        stack.count++;
        if (!discovered.Contains(itemId)) discovered.Add(itemId);
        totalCollected++;
        return true;
    }

    public long StackValue(InvStack s)
    {
        var def = MermaidGameDefs.Item(s.itemId);
        if (def == null) return 0;
        float v = def.baseValue * MermaidGameDefs.QualityValueMult[Mathf.Clamp(s.quality, 0, 6)];
        if (s.lustrous) v *= MermaidGameDefs.LustrousValueMult;
        return (long)Mathf.Ceil(v * MermaidGameDefs.SellMultiplier(level));
    }

    public long SellAll()
    {
        long total = 0;
        foreach (var s in inventory) total += StackValue(s) * s.count;
        inventory.Clear();
        money += total;
        return total;
    }

    public long SellStack(InvStack s)
    {
        long total = StackValue(s) * s.count;
        inventory.Remove(s);
        money += total;
        return total;
    }

    /// <summary>Count of an item across qualities (for quest hand-in).</summary>
    public int CountOf(int itemId)
    {
        int n = 0;
        foreach (var s in inventory) if (s.itemId == itemId) n += s.count;
        return n;
    }

    /// <summary>Remove n of an item, cheapest quality first (quests shouldn't eat your Flawless).</summary>
    public void RemoveItems(int itemId, int n)
    {
        foreach (var s in inventory.Where(s => s.itemId == itemId).OrderBy(s => s.quality).ThenBy(s => s.lustrous).ToList())
        {
            if (n <= 0) break;
            int take = Math.Min(n, s.count);
            s.count -= take;
            n -= take;
            if (s.count <= 0) inventory.Remove(s);
        }
    }

    // ---------------------------------------------------------------- xp / level
    /// <returns>Number of levels gained.</returns>
    public int GainXp(long amount)
    {
        xp += amount;
        int gained = 0;
        while (xp >= MermaidGameDefs.XpToNext(level))
        {
            xp -= MermaidGameDefs.XpToNext(level);
            level++;
            gained++;
        }
        return gained;
    }

    // ---------------------------------------------------------------- quests
    public void EnsureQuests(System.Random rng = null)
    {
        long now = NowUnix();
        if (quests.Count == MermaidGameDefs.QuestCount && now < questResetUnix) return;
        rng = rng ?? new System.Random(unchecked((int)now));

        quests.Clear();
        // Quests ask for items from unlocked locations, biased toward common/uncommon so
        // they're completable by playing normally (Cornerpond quests work the same way).
        var pool = MermaidGameDefs.Items
            .Where(it => unlockedLocations.Contains(it.location) && it.rarity <= 2)
            .ToList();
        for (int i = 0; i < MermaidGameDefs.QuestCount && pool.Count > 0; i++)
        {
            var item = pool[rng.Next(pool.Count)];
            pool.RemoveAll(p => p.id == item.id);
            int needed = item.rarity == 2 ? 1 : rng.Next(2, 5);
            long baseVal = item.baseValue * needed;
            quests.Add(new Quest
            {
                itemId = item.id,
                needed = needed,
                rewardMoney = (long)(baseVal * MermaidGameDefs.QuestMoneyMult) + 10,
                rewardXp = (long)(item.xp * needed * MermaidGameDefs.QuestXpMult) + 5,
                done = false,
            });
        }
        // Align the reset to now + 4h.
        questResetUnix = now + (long)(MermaidGameDefs.QuestResetHours * 3600.0);
    }

    public double QuestSecondsLeft() => Math.Max(0, questResetUnix - NowUnix());

    /// <summary>Try to hand a quest in. Returns false if not enough items.</summary>
    public bool HandInQuest(Quest q)
    {
        if (q == null || q.done) return false;
        if (CountOf(q.itemId) < q.needed) return false;
        RemoveItems(q.itemId, q.needed);
        money += q.rewardMoney;
        GainXp(q.rewardXp);
        q.done = true;
        totalQuestsDone++;
        return true;
    }

    // ---------------------------------------------------------------- persistence
    static string SavePath => System.IO.Path.Combine(Application.persistentDataPath, "mermaid2d_save.json");

    public void Save()
    {
        try
        {
            System.IO.File.WriteAllText(SavePath, JsonUtility.ToJson(this));
        }
        catch (Exception e) { Debug.LogWarning("Mermaid save failed: " + e.Message); }
    }

    public static MermaidGameState LoadOrNew()
    {
        try
        {
            if (System.IO.File.Exists(SavePath))
            {
                var st = JsonUtility.FromJson<MermaidGameState>(System.IO.File.ReadAllText(SavePath));
                if (st != null)
                {
                    st.PostLoadFix();
                    return st;
                }
            }
        }
        catch (Exception e) { Debug.LogWarning("Mermaid load failed: " + e.Message); }
        var fresh = new MermaidGameState();
        fresh.EnsureQuests();
        return fresh;
    }

    void PostLoadFix()
    {
        if (unlockedLocations == null || unlockedLocations.Count == 0) unlockedLocations = new List<int> { 0 };
        if (ownedCharms == null || ownedCharms.Count == 0) ownedCharms = new List<int> { 0 };
        while (consumableCounts.Count < MermaidGameDefs.Consumables.Length) consumableCounts.Add(0);
        if (ownedCosmetics == null || ownedCosmetics.Count == 0) ownedCosmetics = new List<int> { 0, 6 };
        PruneBuffs();
        EnsureQuests();
    }
}
