# Cornerpond — Exact Content Census

Decoded from the installed game's data files (build A19, Godot 4.4.1 — `FishStore.gdc` / `ShopItemStore.gdc` inside `Cornerpond.pck`) on 2026-07-26. These are the actual data-table counts, not estimates.

## The headline numbers

| Category | Count | Detail |
|---|---|---|
| **Biomes** | **7** (+1 hidden) | 6 travel-shop biomes + Dreamworld (potion-gated) + internal "SecretLocation" |
| **Unique fish** | **161** | exactly 26 per standard biome × 6, + 5 secret fish |
| **Catch ranks** | **7** | E, D, C, B, A, S, S+ (values double per rank) |
| **Rarity tiers** | **5** | Common 53 · Rare 47 · Epic 43 · Legendary 13 · Secret 5 |
| **Size classes** | **5** | Tiny, Small, (normal), Big, Huge |
| **Shiny variants** | all fish | 5% base chance, boostable ×2/×4/×8 |
| **Potions/consumables** | **18** | see below |
| **Baits** | **8** | rank-probability tables, level-locked |
| **Upgrade tracks** | **7** | 62 purchase tiers total |
| **Cosmetic items** | **149** | see breakdown |

## Biomes

| Biome | Travel cost | Level lock |
|---|---|---|
| Lake | free | — |
| Beach | 100 | 20 |
| Arctic | 250 | 40 |
| Catacombs | 500 | 60 |
| Sky | 1,000 | 80 |
| Cosmos | 2,000 | 100 |
| Dreamworld | — (drink a **Dream Potion**; effect ends → back to Lake) | — |

Dreamworld has **no unique fish** — all 156 standard fish live 26-per-biome in the other six. The 5 secret fish (Whackerfrog, Potionfish, Youfish, Buckaroo, Snowball) belong to an internal "SecretLocation".

## Potions & consumables (18)

| Family | Tiers | Prices | Effect |
|---|---|---|---|
| White (shiny chance) | Vial / Potion / Elixir | 600 / 3,000 / 15,000 | ×2 / ×4 / ×8 shiny |
| Blue (bite time) | Vial / Potion / Elixir | 600 / 3,000 / 15,000 | −20% / −40% / −80% |
| Yellow (XP) | Vial / Potion / Elixir | 600 / 3,000 / 15,000 | +15% / +30% / +60% |
| Green (sell value) | Vial / Potion / Elixir | 600 / 3,000 / 15,000 | +15% / +30% / +60% |
| Rarity elixirs | Rare / Epic / Legendary | 5,000 / 15,000 / 45,000 | boosts that rarity |
| Rain Potion | — | 10,000 | rain in current biome |
| Patience Potion | — | 10,000 | 2× time to catch |
| Dream Potion | — | not sold in shop | teleports to Dreamworld |

All buffs run 60s (Rain/Patience) or 15s (others).

## Baits (8)

| Bait | Price | Level | Rank odds |
|---|---|---|---|
| Worm | free | — | .95 / .05 |
| Minnow | 500 | 2 | .40 / .55 / .05 |
| Cheese | 1,000 | 8 | .15 / .45 / .35 / .05 |
| Dragonfly | 4,000 | 14 | .15 / .45 / .35 / .05 (shifted up) |
| Glowgrub | 12,000 | 21 | .15 / .45 / .35 / .05 (shifted up) |
| Moon Jelly | 30,000 | 30 | .15 / .45 / .35 / .05 (shifted up) |
| Golden Beetle | 50,000 | 40 | .35 / .45 / .20 (top ranks) |
| Crystal Worm | 100,000 | 50 | .10 / .55 / .35 (A/S/S+) |

## Upgrade tracks (7 — 62 tiers total)

| Track | Tiers | Price range | Value range |
|---|---|---|---|
| Rod Power | 9 | 100 → 50,000 | 2 → 110 |
| Orb (click) Power | 9 | 100 → 50,000 | 2 → 80 |
| Fish Inventory | 12 | 150 → 80,000 | 15 → 400 slots |
| Favorites Inventory | 12 | 150 → 80,000 | 15 → 400 slots |
| Max Bait | 10 | 200 → 50,000 | 10 → 80 |
| Quest slots | 6 (unlocks lvl 7) | 500 → 20,000 | 3 → 8 |
| Mouse Shop size | 4 (unlocks lvl 20) | 5,000 → 20,000 | 3 → 6 slots |

## Cosmetics (149 purchasable items)

| Category | Count | Notes |
|---|---|---|
| Hats | 22 (+ "None") | 3,000–10,000; 16 are Mouse-Shop rotation items |
| Hairstyles | 17 | Hair1 free, rest 1,000 |
| Bobbers | 20 | 10 are Mouse-Shop items; joke prices (Dollar 1, Clover 4,444) |
| Pants styles | 2 | Medium (free), Short |
| Sock styles | 4 | None, Short, Medium, Long |
| **Skin tones** | **15** | 4 natural free (Light/Tan/Dark/Darker) + 11 fun colors @500 |
| Hair colors | 15 | White free, rest 500 |
| Eye colors | 11 | all 500 |
| Top colors | 14 | Red free |
| Bottom colors | 13 | Black free |
| Sock colors | 15 | White free |

Every color is a two-tone pair (main + shade) applied via palette-swap shader.

## Other systems (for scope reference)

- Level curve reaches 100+ (biome locks at 20/40/60/80/100; achievements at 10/50/100).
- Quests: 3–8 concurrent, from questable biomes only.
- Mouse Shop: rotating 3–6 slot stock; items carry a `mouse_prob` appearance weight.
- Journal tracks per fish: each rank caught (7), shiny, weight record → the "completion space" is 161 × 7 ranks × shiny.
