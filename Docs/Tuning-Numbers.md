# Tuning Numbers — Cornerpond's actual equations + our easier version

Formulas reconstructed from Cornerpond A19's decompiled `Fish.gd` / `Stats.gd` (2026-07-27). Right column = recommended values for our game ("a little easier / more generous").

## 1. Catch chance per item

Every catch first rolls **rarity**, then picks uniformly among that biome's fish of that rarity:

| Rarity | Cornerpond prob | ÷ fish per biome | Per-item chance | Expected catches to see one | **Ours (easier)** |
|---|---|---|---|---|---|
| Common | 61% | ~9 | ~6.8% | ~15 | **55%** (~6.1% per item) |
| Rare | 30.2% | ~8 | ~3.8% | ~27 | **30%** (~3.8%) |
| Epic | 8% | ~7 | ~1.1% | ~88 | **12.5%** (~1.8%, ~56 catches) |
| Legendary | 0.8% | ~2 | **0.4%** | **~250** | **2.5%** (~1.25%, ~80 catches) |

The legendary 0.8% is Cornerpond's big grind wall — ~250 catches *per legendary*. Bumping to 2.5% keeps them special but findable within a play session or two (tuned for the 30–35 h completion target in `Pocket-Ocean-Plan.md`). (Rarity-boost potions then stack on top: Cornerpond's elixir tables for reference — Rare {20/71.2/8/0.8}, Epic {10/39.2/50/0.8}, Legendary {0/32.5/36/31.5}.)

Shiny/Gleaming chance: Cornerpond 5% base (×2/×4/×8 potions). **Ours: 8% base.**

## 2. Difficulty progression (how hard a catch fights)

Difficulty is a per-fish stat but in practice scales with **rarity, not biome** (biomes stay flat — progression gating is level locks + prices):

| Rarity | Cornerpond avg difficulty | **Ours** |
|---|---|---|
| Common | ~1.5 (range 1.1–2) | ~1.2 |
| Rare | ~2.5 | ~2.0 |
| Epic | ~5.2 | ~4.2 |
| Legendary | ~8.7 (max 9.2) | ~7.0 |
| Secret | 7.2–8.5 | ~6.5 |

Catch "health" (how much reeling before caught):
`health = rank_health[rank] × difficulty × (1 + weight_dev/20)`
Cornerpond rank_health = **4, 7, 14, 28, 54, 108, 216** (E→S+, ~doubling).
**Ours: 4, 7, 14, 26, 44, 78, 140** — same early feel, gentler S+ ceiling.

Catch timer: `time = 10s × (1 + weight_dev/8.5)`.

## 3. Sell value & XP equations (Cornerpond, use as-is)

```
value = rank_base[rank]                       # 5,10,20,40,80,160,320  (E→S+, doubling)
      × (1 + 0.5 × difficulty^1.5)            # rarity-difficulty drives value hard
      × (1 + weight_dev / 7)                  # heavier specimen = worth more
      × (1 + 0.1 × rarity)                    # mild rarity bonus
      × 3 if shiny
      × green_potion_multiplier
      × (1 + 0.05 × journal_level)            # repeat catches slowly appreciate
value = max(ceil(value), 1)
```

```
xp = rank_xp[rank]                            # 2,4,7,12,18,25,37
   × (1 + 0.5 × difficulty^1.5)
   × (1 + |weight_dev| / 6)
   × (1 + 0.1 × rarity)
   × 3 if shiny
   × yellow_potion_multiplier
```

Level curve: `xp_to_next_level ≈ 25 × level + 25` (linear — levels keep coming steadily forever; keep this, it feels good).

Sanity check of the value formula: a Common (d≈1.5) at rank E ≈ 5×1.9 ≈ **9 coins**; a Legendary (d≈8.7) at S+ ≈ 320×13.8 ≈ **4,430 coins** (before weight/shiny/journal bonuses). That spread is what makes legendaries exciting — keep it.

## 4. Price progression

Cornerpond's curves are hand-authored, not formulaic — but the pattern is: **early geometric (~×2–4 per tier), late linear**:

- Rod/Orb Power: 100, 400, 800, 3000, 8000, 15000, 25000, 35000, 50000
- Inventory: 150, 400, 800, 2000, 6000, 12000, then **linear +12000** per tier to 80000
- Baits: 500 → 1000 → 4000 → 12000 → 30000 → 50000 → 100000
- Cosmetics: hats 3000–10000, hairstyles 1000, colors 500, bobbers 500–5000

**Ours (≈40% cheaper, same shape):**
- Dig/Glint Power: 100, 250, 500, 1800, 5000, 9000, 15000, 21000, 30000
- Satchel: 100, 250, 500, 1200, 3500, 7000, then +7000 per tier to 49000
- Tools (bait equiv): 300 → 600 → 2500 → 7000 → 18000 → 30000 → 60000
- Travel costs: keep Cornerpond's (100/250/500/1000/2000) — they're already cheap flavor
- Colors 300, hairstyles 600 — cosmetics stay cheap because most outfits are FOUND, not bought (see below)

## 5. Rarity visual language (gold outlines idea — yes!)

Each item's outline color = its rarity, using the outline system:

| Rarity | Outline | Extra |
|---|---|---|
| Common | standard dark ink | — |
| Rare | deep blue-silver ink | — |
| Epic | violet ink | subtle sparkle motes |
| Legendary | **gold outline** | sparkle + soft glow pulse |
| Secret | iridescent/rainbow outline | mischievous twinkle |
| Gleaming (any) | adds animated rainbow sheen pass over the art | — |

(Cornerpond's rarity colors for reference: gray c2c2c2 / blue 4cb2ff / magenta e168f2 / orange ffa04e / purple 9277ff.)

## 6. Outfit acquisition — found, not bought

Design decision: buying outfits felt flat in Cornerpond → in our game outfits are **dug up**.

- Rare seabed spawn: a **Gift Clam** (visibly special — pearlescent, gold-outlined). Digging it opens a **pick-1-of-3 card choice** of cosmetics (headwear / charm / color / hairstyle cards, weighted toward slots you own least).
- Frequency: roughly every 20–30 digs, with a **pity counter** (guaranteed within 35). Card rarity mirrors item rarity odds.
- Duplicates auto-convert to coins (or "pearl dust" currency for reroll tokens later).
- The crow's boat shop still exists but sells only potions/tools + a rotating rare *card pack* — so shopping stays for consumables, delight stays in the dig.
- Keep 2–3 starter cosmetics purchasable cheap so early players see the wardrobe system exists.
