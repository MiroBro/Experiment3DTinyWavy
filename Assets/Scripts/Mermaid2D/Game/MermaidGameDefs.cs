using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Static game data for the mermaid gem-collecting meta-game, heavily inspired by
/// Cornerpond's structure (5 locations × 20 items, 7 quality grades, tiered equipment that
/// shifts the quality distribution, consumable buffs, cosmetics, 4-hour quests).
///
/// Scaling notes (measured from Cornerpond): quality/rank value multipliers DOUBLE per
/// grade (10→20→40→80→160→320); equipment prices grow roughly ×3–5 per tier
/// (100, 400, 800, 3000, 8000, 15000, 25000, 35000, 50000); inventory upgrades run
/// 150→80000 for 15→300 slots; the best "bait" costs 100k. We follow the same shapes.
/// </summary>
public static class MermaidGameDefs
{
    // ---------------------------------------------------------------- quality grades
    public static readonly string[] QualityNames =
        { "Rough", "Chipped", "Smooth", "Polished", "Gleaming", "Radiant", "Flawless" };
    // Cornerpond rank base values double each grade — same curve, normalized to ×1 base.
    public static readonly float[] QualityValueMult = { 1f, 2f, 4f, 8f, 16f, 32f, 64f };
    public static readonly Color[] QualityColors =
    {
        new Color(0.65f, 0.65f, 0.65f), new Color(0.75f, 0.72f, 0.60f),
        new Color(0.55f, 0.80f, 0.55f), new Color(0.45f, 0.75f, 0.95f),
        new Color(0.75f, 0.55f, 0.95f), new Color(0.98f, 0.70f, 0.30f),
        new Color(1.00f, 0.35f, 0.55f),
    };

    public static readonly string[] RarityNames = { "Common", "Uncommon", "Rare", "Epic", "Legendary" };
    public static readonly Color[] RarityColors =
    {
        new Color(0.72f, 0.72f, 0.72f), new Color(0.45f, 0.85f, 0.45f),
        new Color(0.35f, 0.65f, 1.00f), new Color(0.80f, 0.45f, 1.00f),
        new Color(1.00f, 0.75f, 0.25f),
    };
    // Chance weights for which rarity band a find comes from (shifted by charms/buffs).
    public static readonly float[] RarityWeights = { 52f, 28f, 13f, 5.5f, 1.5f };

    /// <summary>Chance a find is a "Lustrous" (shiny) variant — Cornerpond-style rare thrill.</summary>
    public const float LustrousChance = 1f / 180f;
    public const float LustrousValueMult = 8f;

    // ---------------------------------------------------------------- items
    [System.Serializable]
    public class ItemDef
    {
        public int id;              // global id = location*20 + index
        public string name;
        public int location;
        public int rarity;          // 0..4
        public int baseValue;
        public int xp;
        public Color color;
        public bool isRock;         // rock silhouette vs faceted gem
    }

    // ---------------------------------------------------------------- locations
    [System.Serializable]
    public class LocationDef
    {
        public int id;
        public string name;
        public long travelCost;
        // Visual palette applied to the world when she travels here.
        public Color waterDeep, waterHorizon, seaweedA, seaweedB, seabed, godRay;
    }

    public static readonly LocationDef[] Locations =
    {
        new LocationDef { id = 0, name = "Seagrass Shallows", travelCost = 0,
            waterDeep = new Color(0.10f, 0.34f, 0.50f), waterHorizon = new Color(0.34f, 0.66f, 0.74f),
            seaweedA = new Color(0.10f, 0.42f, 0.20f), seaweedB = new Color(0.22f, 0.62f, 0.34f),
            seabed = new Color(0.16f, 0.14f, 0.10f), godRay = new Color(0.45f, 0.75f, 0.85f) },
        new LocationDef { id = 1, name = "Coral Gardens", travelCost = 2500,
            waterDeep = new Color(0.16f, 0.22f, 0.46f), waterHorizon = new Color(0.55f, 0.55f, 0.75f),
            seaweedA = new Color(0.55f, 0.25f, 0.30f), seaweedB = new Color(0.85f, 0.45f, 0.35f),
            seabed = new Color(0.22f, 0.14f, 0.12f), godRay = new Color(0.85f, 0.60f, 0.65f) },
        new LocationDef { id = 2, name = "Kelp Forest", travelCost = 20000,
            waterDeep = new Color(0.05f, 0.22f, 0.24f), waterHorizon = new Color(0.20f, 0.48f, 0.42f),
            seaweedA = new Color(0.12f, 0.32f, 0.12f), seaweedB = new Color(0.35f, 0.52f, 0.18f),
            seabed = new Color(0.10f, 0.12f, 0.08f), godRay = new Color(0.55f, 0.80f, 0.55f) },
        new LocationDef { id = 3, name = "Twilight Trench", travelCost = 120000,
            waterDeep = new Color(0.06f, 0.05f, 0.20f), waterHorizon = new Color(0.22f, 0.16f, 0.42f),
            seaweedA = new Color(0.16f, 0.10f, 0.30f), seaweedB = new Color(0.30f, 0.22f, 0.52f),
            seabed = new Color(0.08f, 0.07f, 0.14f), godRay = new Color(0.55f, 0.45f, 0.90f) },
        new LocationDef { id = 4, name = "Abyssal Ruins", travelCost = 600000,
            waterDeep = new Color(0.03f, 0.05f, 0.09f), waterHorizon = new Color(0.14f, 0.20f, 0.28f),
            seaweedA = new Color(0.10f, 0.16f, 0.16f), seaweedB = new Color(0.24f, 0.32f, 0.28f),
            seabed = new Color(0.10f, 0.09f, 0.06f), godRay = new Color(0.85f, 0.75f, 0.45f) },
    };

    // 20 names per location: 8 common, 5 uncommon, 4 rare, 2 epic, 1 legendary.
    static readonly string[][] ItemNames =
    {
        new[] { "Sandy Pebble", "Smooth Skipping Stone", "Mossy Chip", "Tideworn Shard",
                "Speckled Cockle Stone", "Gray Whorl", "Kelp-Tangled Nub", "Salt-Crusted Lump",
                "Seafoam Quartz", "Green Beach Glass", "Banded Agate", "Sunset Coral Bead", "Tiny Pearl",
                "Milk Opal Chip", "Blue Lace Agate", "Golden Nugget", "Moon Shell Fossil",
                "Tidal Sapphire", "Mermaid's Tear", "Heart of the Shallows" },
        new[] { "Coral Crumb", "Pink Rubble", "Sun-Bleached Knob", "Parrotfish Pebble",
                "Fan Coral Chip", "Reef Bone", "Orange Whorl", "Barnacle Button",
                "Rose Quartz", "Amber Bead", "Fire Coral Gem", "Peach Pearl", "Striped Carnelian",
                "Coral Rose Crystal", "Anemone Opal", "Reef Sunstone", "Ruby Chip",
                "Blood Coral Crown", "Garden Ruby", "Eye of the Reef" },
        new[] { "Kelp-Wrapped Stone", "Olive Pebble", "Driftwood Knot", "Holdfast Rock",
                "Forest Chip", "Murk Nub", "Snail Shell Shard", "Moss Lump",
                "Jade Button", "Green Amber", "Serpentine Disc", "Olivine Cluster", "Urchin Spine",
                "Deep Jade Idol", "Malachite Swirl", "Golden Kelp Drop", "Emerald Chip",
                "Forest Emerald", "Leviathan Scale", "Heart of the Kelp" },
        new[] { "Ash Pebble", "Trench Shard", "Basalt Chip", "Pumice Knob",
                "Vent Glass", "Dusk Nub", "Pale Bone Stone", "Cold Seep Lump",
                "Amethyst Point", "Violet Glass", "Lapis Chip", "Dusky Pearl", "Glowshrimp Amber",
                "Abyssal Amethyst", "Starlight Opal", "Indigo Tourmaline", "Night Sapphire",
                "Void Pearl", "Twilight Diamond", "Tear of the Deep" },
        new[] { "Ruin Rubble", "Carved Fragment", "Sunken Brick", "Verdigris Chip",
                "Old Coin Blank", "Mosaic Shard", "Anchor Rust Lump", "Temple Pebble",
                "Bronze Coin", "Silver Ring Shard", "Etched Tablet Chip", "Gilded Tile", "Ancient Bead",
                "Golden Idol Finger", "Crown Jewel Shard", "Royal Signet", "Lost Locket",
                "Siren's Diadem Gem", "Leviathan Idol", "Crown of the Drowned King" },
    };

    // Per-index rarity band: 8 / 5 / 4 / 2 / 1.
    public static int RarityOfIndex(int idx)
    {
        if (idx < 8) return 0;
        if (idx < 13) return 1;
        if (idx < 17) return 2;
        if (idx < 19) return 3;
        return 4;
    }

    static ItemDef[] _items;
    public static ItemDef[] Items
    {
        get
        {
            if (_items != null) return _items;
            // Location base values scale ~×5 per location; rarity ~×2.5–3 per band —
            // together roughly matching Cornerpond's value spread from Lake carp to
            // Cosmos legendaries.
            float[] locBase = { 6f, 30f, 150f, 750f, 3800f };
            float[] rarityMult = { 1f, 2.6f, 7f, 20f, 60f };
            var list = new List<ItemDef>();
            for (int L = 0; L < Locations.Length; L++)
            {
                for (int i = 0; i < 20; i++)
                {
                    int rarity = RarityOfIndex(i);
                    // Deterministic per-item color within the location's palette family.
                    var rng = new System.Random(L * 1000 + i * 7 + 13);
                    float hueBase = new[] { 0.45f, 0.02f, 0.30f, 0.72f, 0.11f }[L];
                    float hue = Mathf.Repeat(hueBase + (float)rng.NextDouble() * 0.22f - 0.11f, 1f);
                    float sat = rarity == 0 ? 0.12f + (float)rng.NextDouble() * 0.15f
                                            : 0.55f + (float)rng.NextDouble() * 0.4f;
                    float val = rarity == 0 ? 0.45f + (float)rng.NextDouble() * 0.2f
                                            : 0.7f + (float)rng.NextDouble() * 0.3f;
                    float variation = 0.85f + (float)rng.NextDouble() * 0.4f;
                    list.Add(new ItemDef
                    {
                        id = L * 20 + i,
                        name = ItemNames[L][i],
                        location = L,
                        rarity = rarity,
                        baseValue = Mathf.Max(1, Mathf.RoundToInt(locBase[L] * rarityMult[rarity] * variation)),
                        xp = Mathf.Max(1, Mathf.RoundToInt(2f * Mathf.Pow(3f, rarity) * (1f + L * 0.8f))),
                        color = Color.HSVToRGB(hue, Mathf.Clamp01(sat), Mathf.Clamp01(val)),
                        isRock = rarity == 0,
                    });
                }
            }
            _items = list.ToArray();
            return _items;
        }
    }

    public static ItemDef Item(int id) => (id >= 0 && id < Items.Length) ? Items[id] : null;

    // ---------------------------------------------------------------- charms (equipment)
    // The Cornerpond bait system: each tier has a probability distribution over the 7
    // quality grades. Better charm = mass shifts toward Flawless. Prices ~×3.5/tier.
    [System.Serializable]
    public class CharmDef
    {
        public int id;
        public string name;
        public string desc;
        public long price;
        public float[] qualityProbs;   // 7 entries, sums to 1
    }

    public static readonly CharmDef[] Charms =
    {
        new CharmDef { id = 0, name = "Woven Kelp Gloves",  price = 0,
            desc = "Her bare-handed start. Finds are mostly rough.",
            qualityProbs = new[] { 0.95f, 0.05f, 0f, 0f, 0f, 0f, 0f } },
        new CharmDef { id = 1, name = "Coral Comb",          price = 400,
            desc = "Combs the silt gently. Chipped finds become common.",
            qualityProbs = new[] { 0.55f, 0.40f, 0.05f, 0f, 0f, 0f, 0f } },
        new CharmDef { id = 2, name = "Pearl Bracelet",      price = 1500,
            desc = "A soft glow guides her hands to smoother stones.",
            qualityProbs = new[] { 0.30f, 0.45f, 0.22f, 0.03f, 0f, 0f, 0f } },
        new CharmDef { id = 3, name = "Sea-Glass Lantern",   price = 6000,
            desc = "Lights up the roots. Polished finds appear.",
            qualityProbs = new[] { 0.15f, 0.35f, 0.35f, 0.13f, 0.02f, 0f, 0f } },
        new CharmDef { id = 4, name = "Silver Trident Pick", price = 25000,
            desc = "Pries treasures free without a scratch.",
            qualityProbs = new[] { 0.05f, 0.22f, 0.38f, 0.25f, 0.09f, 0.01f, 0f } },
        new CharmDef { id = 5, name = "Enchanted Conch",     price = 90000,
            desc = "Whispers where the gleaming ones hide.",
            qualityProbs = new[] { 0f, 0.10f, 0.28f, 0.34f, 0.20f, 0.07f, 0.01f } },
        new CharmDef { id = 6, name = "Moonstone Diadem",    price = 300000,
            desc = "Moonlight follows her fingers into the sand.",
            qualityProbs = new[] { 0f, 0.03f, 0.15f, 0.30f, 0.30f, 0.17f, 0.05f } },
        new CharmDef { id = 7, name = "Crown of the Tides",  price = 1000000,
            desc = "The sea itself offers up its flawless heart.",
            qualityProbs = new[] { 0f, 0f, 0.06f, 0.20f, 0.32f, 0.28f, 0.14f } },
    };

    // ---------------------------------------------------------------- upgrades
    // Satchel (inventory size): Cornerpond runs 15→300 slots at 150→80000.
    public static readonly int[] SatchelSizes = { 20, 30, 45, 70, 100, 150, 220, 300 };
    public static readonly long[] SatchelPrices = { 0, 150, 2000, 6000, 12000, 24000, 48000, 80000 };
    // Glint power (Cornerpond OrbPower): how strongly each clicked glint boosts quality.
    public static readonly int[] GlintPower = { 1, 2, 3, 5, 8, 12 };
    public static readonly long[] GlintPrices = { 0, 300, 1200, 5000, 18000, 60000 };

    // ---------------------------------------------------------------- consumables
    [System.Serializable]
    public class ConsumableDef
    {
        public int id;
        public string name;
        public string desc;
        public long price;
        public float durationSeconds;
    }

    public const int BuffLuck = 0, BuffHaste = 1, BuffTwin = 2, BuffXp = 3;
    public static readonly ConsumableDef[] Consumables =
    {
        new ConsumableDef { id = BuffLuck,  name = "Luminous Kelp Tea", price = 900,
            desc = "10 min: quality rolls get a second chance to grade up.", durationSeconds = 600 },
        new ConsumableDef { id = BuffHaste, name = "Bubble of Haste",   price = 600,
            desc = "10 min: she cruises less and rummages far more often.", durationSeconds = 600 },
        new ConsumableDef { id = BuffTwin,  name = "Pearl Dust",        price = 1500,
            desc = "10 min: 35% chance each find comes up twice.", durationSeconds = 600 },
        new ConsumableDef { id = BuffXp,    name = "Moon Nectar",       price = 1200,
            desc = "15 min: +100% XP from every find.", durationSeconds = 900 },
    };

    // ---------------------------------------------------------------- cosmetics
    [System.Serializable]
    public class CosmeticDef
    {
        public int id;
        public string name;
        public long price;
        public bool isTail;            // false = hair dye, true = tail finish
        public Color primary;          // hair color / tail gold
        public Color secondary;        // hair shadow tint / tail deep gold
    }

    public static readonly CosmeticDef[] Cosmetics =
    {
        new CosmeticDef { id = 0, name = "Crimson Hair (classic)", price = 0, isTail = false,
            primary = new Color(0.80f, 0.14f, 0.09f), secondary = new Color(0.30f, 0.08f, 0.05f) },
        new CosmeticDef { id = 1, name = "Golden Blonde Hair", price = 8000, isTail = false,
            primary = new Color(0.95f, 0.78f, 0.35f), secondary = new Color(0.55f, 0.38f, 0.12f) },
        new CosmeticDef { id = 2, name = "Raven Black Hair", price = 8000, isTail = false,
            primary = new Color(0.16f, 0.14f, 0.20f), secondary = new Color(0.05f, 0.05f, 0.09f) },
        new CosmeticDef { id = 3, name = "Sea-Green Hair", price = 15000, isTail = false,
            primary = new Color(0.20f, 0.70f, 0.55f), secondary = new Color(0.06f, 0.30f, 0.24f) },
        new CosmeticDef { id = 4, name = "Amethyst Hair", price = 40000, isTail = false,
            primary = new Color(0.62f, 0.35f, 0.85f), secondary = new Color(0.25f, 0.10f, 0.40f) },
        new CosmeticDef { id = 5, name = "Pearl White Hair", price = 100000, isTail = false,
            primary = new Color(0.92f, 0.92f, 0.95f), secondary = new Color(0.55f, 0.58f, 0.66f) },

        new CosmeticDef { id = 6, name = "Golden Tail (classic)", price = 0, isTail = true,
            primary = new Color(1f, 0.75f, 0.28f), secondary = new Color(0.62f, 0.40f, 0.10f) },
        new CosmeticDef { id = 7, name = "Silver Pearl Tail", price = 12000, isTail = true,
            primary = new Color(0.80f, 0.86f, 0.92f), secondary = new Color(0.42f, 0.48f, 0.58f) },
        new CosmeticDef { id = 8, name = "Rose Gold Tail", price = 25000, isTail = true,
            primary = new Color(0.95f, 0.60f, 0.55f), secondary = new Color(0.60f, 0.30f, 0.28f) },
        new CosmeticDef { id = 9, name = "Emerald Tail", price = 60000, isTail = true,
            primary = new Color(0.25f, 0.80f, 0.45f), secondary = new Color(0.08f, 0.42f, 0.22f) },
        new CosmeticDef { id = 10, name = "Obsidian Tail", price = 150000, isTail = true,
            primary = new Color(0.22f, 0.20f, 0.28f), secondary = new Color(0.08f, 0.07f, 0.12f) },
        new CosmeticDef { id = 11, name = "Opal Tail", price = 400000, isTail = true,
            primary = new Color(0.85f, 0.75f, 0.95f), secondary = new Color(0.45f, 0.55f, 0.75f) },
    };

    // ---------------------------------------------------------------- quests & leveling
    public const int QuestCount = 3;
    public const double QuestResetHours = 4.0;
    public const float QuestMoneyMult = 1.8f;    // reward premium over raw sell value
    public const float QuestXpMult = 3f;

    public static long XpToNext(int level) => (long)(50.0 * System.Math.Pow(level, 1.35));
    public static float SellMultiplier(int level) => 1f + 0.02f * (level - 1);

    public static string MoneyString(long amount)
    {
        if (amount >= 1000000000L) return (amount / 1000000000.0).ToString("0.##") + "B";
        if (amount >= 1000000L) return (amount / 1000000.0).ToString("0.##") + "M";
        if (amount >= 10000L) return (amount / 1000.0).ToString("0.#") + "k";
        return amount.ToString("N0");
    }
}
