# Pocket Ocean — Game Plan

The plan for our game. Companion docs: `Cornerpond-Scope.md` (reference census), `Mermaid-Collectibles-Design.md` (content lists), `Tuning-Numbers.md` (equations — this doc refines its numbers), `Art-Prompts.md` (asset generation).

## Identity

- **Working title: Pocket Ocean** (runner-ups: Cornercove, Glimmerdeep; "Sea Stones" viable as subtitle). Verify store-name collisions before committing.
- A corner-of-screen cozy idle collector: a hand-painted mermaid digs the seabed for pebbles, shells, clams, pretty stones and gems. Storybook art, thick unified ink outline, thin painted interior lines.
- Cornerpond's structure, ~**⅓ of its grind**: Cornerpond's true 100% runs 90–300 hours; ours targets **completion in 30–35 hours of mostly-background play** (~12 h to "seen everything", the rest a gentle mastery tail).

## Completion definition (design it, don't inherit it)

Cornerpond's 100% = every fish × every one of 7 ranks × shiny → hundreds of hours. Ours is a **3-star mastery journal** per item:

| Star | Requirement |
|---|---|
| ★ | dig the item once |
| ★★ | dig it at S rank or better |
| ★★★ | dig a Gleaming one |

100% = 161 items × 3 stars + all upgrades + full wardrobe + all biomes. Ranks E–S+ still exist and drive value/XP — they're just not all journal checkboxes.

## Core numbers (final)

### Rarity roll per dig
| Rarity | Odds | Per-item odds (÷ per-biome count) | Expected digs to find a specific one |
|---|---|---|---|
| Common (9/biome) | 55% | ~6.1% | ~16 |
| Rare (8/biome) | 30% | ~3.8% | ~27 |
| Epic (7/biome) | 12.5% | ~1.8% | ~56 |
| Legendary (2/biome) | 2.5% | ~1.25% | ~80 |

(Cornerpond: 61/30.2/8/0.8 — a specific legendary took ~250 catches.)
**Gleaming chance: 8%** base (Cornerpond 5%), ×2/×4/×8 with Pearlshine potions.

### Difficulty & ranks
- Per-item difficulty by rarity: **C ~1.2 · R ~2.0 · E ~4.2 · L ~7.0 · Secret ~6.5** (Cornerpond ×0.8).
- Rank base values (sell): **5, 10, 20, 40, 80, 160, 320** (keep — doubling feels great).
- Rank health (catch effort): **4, 7, 14, 26, 44, 78, 140** (Cornerpond's 216 S+ ceiling softened).
- Rank XP: **2, 4, 7, 12, 18, 25, 37** (keep).

### Equations (inherited from Cornerpond, unchanged)
```
value = rank_base × (1 + 0.5·difficulty^1.5) × (1 + weight_dev/7)
        × (1 + 0.1·rarity) × 3(gleaming) × barter_potion × (1 + 0.05·journal_level)
xp    = rank_xp × (1 + 0.5·difficulty^1.5) × (1 + |weight_dev|/6)
        × (1 + 0.1·rarity) × 3(gleaming) × wisdom_potion
xp_to_next_level = 25·level + 25
catch_health = rank_health × difficulty × (1 + weight_dev/20)
```

### Digging tools (bait equivalent) — better ranks sooner, cheaper
| Tool | Price | Level | Rank odds |
|---|---|---|---|
| Bare Hands | free | — | .90 E / .10 D |
| Driftwood Trowel | 300 | 2 | .35 / .55 / .10 |
| Whalebone Scoop | 600 | 7 | .15 / .45 / .35 / .05 |
| Copper Spade | 2,500 | 12 | shift up one rank |
| Glowshell Scoop | 7,000 | 18 | shift up |
| Moonsilver Trowel | 15,000 | 26 | shift up |
| Golden Spade | 25,000 | 34 | .35 / .45 / .20 over A/S/S+ |
| Crystal Trowel | 45,000 | 42 | .10 A / .55 S / .35 S+ |

### Upgrade tracks & prices (~40–50% of Cornerpond, same early-geometric/late-linear shape)
| Track | Tiers (prices) | Values | Sum |
|---|---|---|---|
| Dig Power | 100, 250, 500, 1800, 5000, 9000, 15000, 21000, 30000 | 2→110 | 82,650 |
| Glint-Click Power | same as Dig Power | 2→80 | 82,650 |
| Satchel Size | 100, 250, 500, 1200, 3500, 7000, 11000, 15000, 19000, 23000, 27000, 31000 | 15→400 | 138,550 |
| Treasure Case (favorites) | same as Satchel | 15→400 | 138,550 |
| Toolbelt (uses cap) | 100, 500, 1200, 2500, 5000, 7500, 10000, 14000, 18000, 22000 | 10→80 | 80,800 |
| Quest Slots | 300, 1200, 2400, 4800, 7200, 12000 (unlocks lvl 6) | 3→8 | 27,900 |
| Crow's Boat Shop | 3000, 6000, 9000, 12000 (unlocks lvl 15) | 3→6 slots | 30,000 |

**Total upgrade sink ≈ 581,000 coins** (+ tools ≈ 95,400 + travel 3,850 + potions ongoing → lifetime spend ≈ **700k**).

### Biome unlocks — reach the last biome sooner
| Biome | Travel | Level (Cornerpond) |
|---|---|---|
| Seagrass Shallows | free | — |
| Tidepool Coast | 100 | **12** (was 20) |
| Glacier Sea | 250 | **25** (was 40) |
| Shipwreck Deep | 500 | **40** (was 60) |
| Mirror Lagoon | 1,000 | **55** (was 80) |
| Starfall Trench | 2,000 | **70** (was 100) |
| Dreamtide | Dream Draught | — |

### Potions
Same 18-potion structure and effects as `Tuning-Numbers.md`; prices ×0.6 of Cornerpond (vials 350, potions 1,800, elixirs 9,000; essences 3,000/9,000/27,000). Essence rarity tables copy Cornerpond's shapes (Legendary Essence: 0 / .325 / .36 / .315).

## Wardrobe acquisition — Gift Clams (found, not bought)

- **Gift Clam** spawns roughly every **10 digs** (pity: guaranteed by 14). Pearlescent, gold-outlined, obviously special.
- Opening = **pick 1 of 3 cosmetic cards**, weighted toward categories the player owns least; card rarity mirrors item-rarity feel.
- ~149 cosmetics ÷ 1 per clam ≈ 149 clams ≈ **~1,500 digs to full wardrobe** — finishes naturally alongside the journal (see model below). Duplicates can't appear until the pool is empty; after that clams give coins/pearl dust.
- The crow sells potions + tools + occasionally 1 rotating cosmetic card pack; 2–3 starter cosmetics cheap in shop so the wardrobe system is discovered early.

## Minigame — no precision clicking, ever

Replaces Cornerpond's click-the-orb (cursor-accuracy) and our old glint-click. Two inputs, neither can be failed, both fully optional (idle completes everything, slower):

1. **Brush the Sand** — a found item appears as a buried mound at her dig spot; hold the mouse button ANYWHERE and wiggle the cursor to scrub the sand away, scratch-card style, until the item pops free with its rarity-colored outline. Motion matters, position doesn't. Untouched mounds self-reveal in a few seconds.
2. **Hold to Help** — press and hold anywhere while she rummages; she digs faster while held, with sparkles drifting from the cursor to her hands. Pure attention, zero skill. The "Glint-Click Power" upgrade track becomes **Helping Hands** (how much holding accelerates digs).

## Rarity visual language

Outline color per rarity on dug-up items: ink → blue-silver → violet (+ motes) → **gold + sparkle (Legendary)** → iridescent (Secret). Gleaming adds an animated rainbow sheen over any item.

## Playtime model (the math behind "25–35 hours")

Assumptions: effective dig cadence ~25–35s blending active play and background idling; average sale value across the run ~250–350 coins.

| Goal | Digs needed | Notes |
|---|---|---|
| ★ journal (each of 156 biome items once) | ~1,300–1,500 | dominated by legendaries at 1.25% each; coupon-collector per biome ≈ 220–250 digs × 6 |
| Full wardrobe via clams | ~1,500 | overlaps completely with the above |
| All upgrades + tools (~700k coins) | ~2,000–2,400 income digs | overlaps; late digs earn 400–800 each |
| ★★ (S+ on everything) + ★★★ (Gleaming everything) | +1,000–1,400 targeted digs | with Crystal Trowel (90% S/S+) and 8% Gleaming; Essences compress legendary hunting |
| Level 70 (last biome) | ~64k XP ≈ 1,600 digs | overlaps |
| **Total 100%** | **~2,900–3,400 digs** | **≈ 24–28 h focused → 30–35 h as normal corner-play** |

Cornerpond equivalent: 90 h to endgame, 100+ h personal-estimate for full journal, 301 h median on trackers. **We land at roughly ⅓ of that**, with the first "I've seen every item" milestone at ~12 h.

### Tuning dials if playtests run long/short
1. Legendary odds 2.5% ↔ 2–4% (biggest single dial).
2. Gift Clam cadence 10 ↔ 8–14 digs.
3. Dig cadence (bite time 10s base) — shortening helps everything proportionally.
4. Late-tier linear price steps (satchel +4,000s) — cheapest knob that doesn't touch feel.

## What we keep vs change (summary)

| System | Cornerpond | Pocket Ocean |
|---|---|---|
| Content scope | 161 fish, 7 biomes, 149 cosmetics, 18 potions, 8 baits, 7 upgrade tracks | identical counts, re-themed |
| Value/XP equations | — | identical |
| Rarity odds | 61/30.2/8/0.8 | 55/30/12.5/2.5 |
| Completion | all 7 ranks × all fish × shiny | 3-star mastery (once / S / Gleaming) |
| Cosmetics | bought | dug up — Gift Clams, pick 1 of 3 |
| Biome final unlock | level 100 | level 70 |
| Difficulty | C1.5→L8.7, S+ health 216 | ×0.8, S+ health 140 |
| Full completion time | 90–300 h | 30–35 h |
