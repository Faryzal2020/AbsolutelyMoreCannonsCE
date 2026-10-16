# AMC Turret Balance Review (2026-10-16)

Static review of every turret def on the `testing` branch. No live testing; all numbers are derived from XML
(`Common/Defs`), re-extracted with `docs/scripts/extract_turrets.py` on the current files.

## Method and assumptions

- **Resource value** uses vanilla market prices: Steel 1.9, Plasteel 9, Component 32, Spacer component 150, FSX 7.5 (CE), Antigrain warhead ~4500.
- **Sustained RPM** = shots per minute including warmup, cooldown, burst spacing and magazine reload, assuming ammo is always available (autoloader or hauler).
- **Kill area per minute** = sustained RPM x explosion area (pi r^2) of the best HE shell. Any CE pawn inside a 100+ dmg Bomb radius is dead or crippled, so radius drives anti-infantry kill capability far more than damage number does. Damage above ~150 mostly matters against buildings.
- **Armor penetration saturates.** CE armor: flak vest ~6 mm, marine ~18, centipede ~20, tunneler 24. Anything with sharp AP above ~30 mm already penetrates every pawn and mech. AP of 100–400 mm only matters against buildings or vehicle mods. 20mm AP (36) is "enough".
- **Research** is quoted as AMC points only; vanilla gates are called out separately (Precision Rifling 1400, Mortars 2000, Fabrication 4000, Advanced Fabrication 4000, Machining 1000).
- **Ammo burn** = ammo value per round x sustained RPM = silver per minute of continuous fire. This is the real limiter for the big guns; a CNC machine at x4 speed needs ~80 s of crafting per 155mm shell (18800 work), so a GCT firing 9/min needs several modules just to keep up.

Scores are 1–10 within the whole mod. AI = anti-infantry, AA = anti-armor/mech, SI = siege/indirect (incl. settlement shelling), LG = logistics/uptime (magazine, crew exposure, power, ammo price), VAL = what you get for the cost + research.

## Scoreboard

### Autocannons (gate: HeavyCannons 200 + HeavyAutocannons 200, needs Fabrication 4000)

| Turret | Cost (silver) | Research | Sust. RPM | Best AP (dmg/AP) | HE | Kill area/min | Ammo $/min | AI | AA | LG | VAL | Verdict |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Flak 38 | 317 | 200 | 114 | 43/36 | none | – | 234 | 4 | 3 | 7 | 7 | Fine, but arrives after Fabrication when CE's autocannon turret already exists |
| Flak M42 | 514 | 500 | 165 | 90/67 | 29 r0.5 | 130 | 1556 | 6 | 5 | 8 | **9** | Best value autocannon. Mag 300, no power |
| DCA 40 | 634 | 500 | 114 | 123/94 | 36 r1.0 | 359 | 1651 | 6 | 5 | 6 | 7 | Good HE, enclosed |
| FV 510 Warrior | 811 | 800 | 65 | 71/110 | 20 r0.5 | 51 | 389 | 3 | 5 | 6 | **3** | 39 ticks between shots; a third of the Flak M42's output for 60% more cost |
| Kugelblitz | 1074 | 1000 | 279 | 74/95 | 15 r0.5 | 219 | 1615 | 7 | 6 | 7 | 8 | Cheapest 30mm AP (3 silver/rd) |
| BMP-2 | 994 | 1000 | 200 | 69/82 | 22 r0.5 | 157 | 1249 | 6 | 6 | 7 | 6 | Side-grade to Kugelblitz; APDS costs plasteel |
| SPz Marder | 899 | 800 | 279 | 42/57 | 16 r0.5 | 219 | 1224 | 6 | 4 | 8 | 6 | Most accurate AC (sights 2.5, spread 0.01), mag 1250 |
| M247 | 1832 | 1200 | 194 | 123/94 | 36 r1.0 | 611 | 2808 | 8 | 6 | 8 | 5 | Good gun, but 4 spacer components |
| SIDAM 25 | 1783 | 1200 | 379 | 56/81 | 23 r0.5 | 298 | 1646 | 7 | 5 | 8 | **4** | 6 spacer components (~900 silver) for a Kugelblitz-class output |

### Cannons (gate: HeavyCannons 200, needs Precision Rifling 1400 only — earliest branch)

| Turret | Cost | Research | Sust. RPM | AP shell | HE shell | Kill area/min | Ammo $/min | AI | AA | LG | VAL | Verdict |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| AEC Deacon | 634 | 500 | 13 | 183/116 (15 $/rd) | none | – | ~200 | 2 | 7 | 6 | 6 | Cheap anti-mech sniper; **APDS recipe bug** (see bugs) |
| Pak 40 | 476 | 500 | 9.2 | 399/138 +599 r1 | 105 r2 | 116 | 612 | 4 | 6 | 3 | 7 | Solid entry gun; mag 1 |
| Flak 41 | 571 | 700 | 6.3 | 570/230 +855 r1 | 132 r2.5 | 124 | 680 | 4 | 7 | 3 | **3** | Same range (100) and same kill area as Pak 40 for more cost/research/ammo |
| BS-3 | 698 | 1000 | 5.2 | 637/239 +956 r1 | 209 r3 | 148 | 812 | 5 | 7 | 3 | **4** | Research 1000 > T-54's 800 while being worse |
| Pak 44 | 933 | 1200 | 3.6 | 858/252 +1287 r1 | 289 r3.5 | 140 | 793 | 5 | 8 | 3 | **3** | 15 s cooldown; the Gvozdika (+220 silver) dominates it |
| M26 | 1200 | 500 | 7.0 | 510/173 +766 r1 | 126 r2.5 | 138 | 660 | 5 | 7 | 8 | 7 | Research 500 is very cheap for an enclosed, autoloaded 90mm |
| T-54 | 1359 | 800 | 7.9 | 637/239 | 209 r3 | 222 | 1224 | 6 | 7 | 8 | 7 | Best "cannon" HE output |
| R3-T106 | 603 | 1000 | 20.6 | HEAT 248/400 +209 r1 | r1.5 | 145 | 2131 | 6 | 8 | 5 | 8 | Highest cannon DPS, 180 HP, ammo-hungry; fine as glass cannon |
| Leopard 1A5 | 1717 | 1200 | 8.5 | APFSDS 182/408; HEAT 248/400 +855 r1 | r1.5 | 60 | 942 | 5 | 9 | 9 | 6 | Fair for top-tier direct fire |

### Howitzers (gate: Howitzers 500 + HeavyCannons 200, needs Mortars 2000 + Fabrication 4000)

| Turret | Cost | Research | Sust. RPM | HE | Kill area/min | Ammo $/rd | Shelling tiles | Mag | AI | SI | LG | VAL | Verdict |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| le.IG 18 | 634 | 500 | 9.2 | **328 r4.0** | **464** | 110 | 6 | 1 | 9 | 6 | 4 | **10** | Outlier: a 75mm doing 3x the Pak 40's 75mm HE, out-killing every howitzer below 155mm at a third the price |
| sIG 33 | 1091 | 700 | 3.9 | 479 r5.0 | 304 | 304 | 6 | 1 | 7 | 6 | 3 | 8 | Strong per shell; exposed gunner, 15 s reload |
| 2S1 Gvozdika | 1155 | 700 | 3.8 | 279 r3.5 | 148 | 183 | 18 | 40 | 5 | 7 | 7 | **4** | 15 s cooldown makes it the weakest howitzer per silver |
| 2S3 Akatsiya | 1441 | 1000 | 3.8 | 386 r4.5 | 245 | 297 | 24 | 46 | 6 | 8 | 7 | 5 | Same 15 s cooldown; loses to sIG 33 in raw output |
| 2S19 Msta | 2385 | 1500 | 6.3 | 386 r4.5 | 400 | 297 | 24 | 50 | 7 | 8 | 8 | 5 | Half the GCT's output for 94% of its price |
| 155mm GCT | 2545 | 2000 | 9.2 (3-rd bursts) | 549 r5.5 | **874** | 376 | 24 | 42 | 9 | 9 | 8 | 8 | Best howitzer by a wide margin |
| 9.2in BL Mk.1 | 1580 | 1200 | 1.7 | 462 r5.0 | 133 | **700** | 12 | 1 | 5 | 6 | 2 | **2** | 233mm shell weaker than the 150mm sIG 33's; 35 s reload; 70 000 build work |
| Karl-Gerät | 3810 | 2500 | 1.5 | 3870 r16 | 1191 | 2754 (253 FSX) | 6 | 1 | 10 | 7 | 1 | 5 | Specialty siege piece; shelling range 6 undersells it |
| 2A3 Kondensator | 4850 | 2500 | 0.9 | 31620 r50 (antigrain) | – | ~5600 | 24 (dmg 5.2) | 1 | – | 10 | 1 | – | Antigrain delivery system; min range 100 |

### Naval guns (gate: NavalGuns 1000 + both branches ≈ 1900; RF line needs Advanced Fabrication)

| Turret | Cost | Research | Sust. RPM | HE | Kill area/min | Ammo $/min | Indirect | Shelling | Mag | AI | SI | LG | VAL | Verdict |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 12.7cm SK C/34 | 2960 | 500 | 10.9 | 200 r3 | 308 | 2307 | no | – | **1** | 6 | 0 | 2 | **2** | 2x the T-54's price for +38% RPM, no mag, no indirect, no enclosure |
| 15cm KC/36 | 3680 | 1000+100 | 6.3 | 299 r4 | 317 | 2261 | no | – | **1** | 6 | 0 | 2 | **2** | Loses to the Msta at 65% of its price |
| 15cm KC/36T | 4950 | 1500+100 | 12.6 | 299 r4 | 634 | 4514 | no | – | 2 | 7 | 0 | 3 | **3** | Twin for 100 plasteel; still no indirect |
| 20.3cm SK C/34 | 5900 | 2000+100 | 9.6 | 490 r5 (+airburst, APHE 1704) | 751 | 5956 | **no** | – | 55 | 8 | 0 | 7 | 4 | 7x7 fortress gun with no indirect mode |
| 12.7cm Type C | 4210 | 1500+100 | 9.6 | 218 r3 | 271 | 1689 | yes | 18 | 80 | 6 | 7 | 6 | **2** | Half the Mark 16's output at 125% of its price |
| 5"/54 Mark 16 | 3370 | 500+100 | 14.9 | 262 r3.5 (+APHE) | 574 | 3527 | yes | 24 | 50 | 8 | 8 | 8 | 7 | The naval baseline; good |
| Otobreda 127/54C | 3810 | 1000+100 | 46.5 | 262 r3.5 (+APHE) | **1790** | 10995 | yes | 24 | 85 | 10 | 9 | 7 | **9** | 3x the Mark 16 for +440 silver |
| 100mm Creusot-Loire | 4510 | 1500+100 | 39.7 | 139 r2.5 | 780 | 4548 | yes | 18 | 100 | 7 | 7 | 8 | **4** | Costs more than the Otobreda, does 45% of its work |
| 12cm TAK120 | 5320 | 2000+100 | 56.5 | 262 r3.5 (+APHE) | **2173** | 11029 | yes | 18 | 52 | 10 | 8 | 7 | 6 | Top DPS; the price is justified |
| OTO Melara 76/62C | 5740 | 2500+100 | 46.0 | 132 r2.5 (+SAP, airburst, guided) | 903 | 3288 | yes | 18 | 200 | 7 | 7 | 8 | **4** | Most expensive turret and research in the mod for 76mm HE |

### Rotary cannons (gate: RotaryCannons 800 + HeavyAutocannons chain ≈ 1200, needs Fabrication)

| Turret | Cost | Research | Fire rate | Ammo | Range | Mag | Ammo $/min | AI | AA | LG | VAL | Verdict |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| M61 Vulcan | 1717 | 500 | 1500/3000 RPM, bursts 20/50/100 | CE 20x102 (44/30; ~1 $/rd) | 75 | 1500 | ~2000 | 9 | 4 | 8 | **10** | 15x a CE autocannon turret's output at the same price, plus CIWS and enclosure |
| GAU-8 Avenger | 3827 | 1000 | 1200–3900 RPM, bursts 20/50/100 | CE 30x173 (80/70; ~3 $/rd) | **110** | 1500 | ~9600 | 10 | 7 | 7 | **9** | Highest DPS in the mod; priced like a mid naval gun; not enclosed |

### Unmanned (gate: Unmanned* 2000–3000 each + parent turret + Automated_FCS 1000 after Advanced Fabrication)

| Turret | Cost | Total AMC research | Sust. RPM | Notes | AI | AA | SI | LG | VAL | Verdict |
|---|---|---|---|---|---|---|---|---|---|---|
| Auto Flak 38 | 979 (+FCS) | 3100 + 1000 | 74 | 3 s aim, 43/36 | 4 | 3 | – | 7 | **2** | CE autocannon turret (1600 research, 1480 silver, unmanned) is better in every way |
| Auto SPz Marder | 1339 (+FCS) | 3400 + 1000 | 183 | mag 1250 | 6 | 4 | – | 8 | 4 | OK automation |
| Auto DCA 40 | 1383 (+FCS) | 3100 + 1000 | 75 | HE r1 | 6 | 5 | – | 7 | 4 | OK |
| Auto AEC Deacon | 1328 (+FCS) | 4200 + 1000 | 8.5 | AP only, 3 s aim | 2 | 7 | – | 7 | **1** | A 3000-point research line unlocks exactly this one turret |
| Auto 2S19 Msta | 3070 (+FCS) | 5900 + 1000 | 5.0 | direct range capped at 120 (manned is map-wide) | 6 | – | 8 | 9 | 4 | Pay +700 silver and +3700 research to lose 20% RPM; frees a colonist |
| Auto Mark 16 | 3945 (+FCS) | 6200 + 1000 | 9.4 | mag 200 | 7 | – | 8 | 9 | 4 | Same pattern |

## Undervalued turrets (players would skip these)

1. **9.2in BL Howitzer Mk.1** – 233mm shell does 462 dmg r5, less than the 150mm sIG 33 (479 r5) and the 155 (549 r5.5), reload 35 s, 700 silver per shell, 70 000 build work. Expect roughly 900–1000 dmg, r6.5–7, reload ~25 s, shelling 18 for the size.
2. **12.7cm SK C/34** – entry naval gun with a 1-round magazine, no indirect, no shelling, exposed gunner, 5 spacer components. A T-54 at 1359 silver does the same job. Needs a magazine (20+) and the indirect/shelling kit the other 127s have, or a price near 2000.
3. **15cm KC/36 and KC/36T** – no indirect, no shelling, 1–2 round magazines, 3680 / 4950 silver. The Msta (2385) and GCT (2545) beat both. Add indirect mode and a magazine, or treat them as cheap "naval cannons" at ~2000 / 3000.
4. **12.7cm Type C** – twin mount firing 9.6 RPM with 218-dmg shells at 4210 silver, next to the Mark 16 (3370 silver, 14.9 RPM, 262 dmg, 24-tile shelling). Cooldown 12 → 6 or price → ~2500.
5. **100mm Creusot-Loire** – 4510 silver (30 comps, 8 spacer) for 45% of the Otobreda's output at a higher price. Price → ~2800 or RPM up.
6. **OTO Melara 76/62C** – most expensive turret and research in the mod, 900 W, firing 76mm HE (132 r2.5). Airburst and guided rounds are its justification; if those are not decisive in play, drop to ~6 spacer comps and research 1500.
7. **Flak 41** – same range (100), same kill area as the Pak 40, 20% more cost, 40% more research, 60% dearer shells, 4x4 footprint. The real gun's identity is range: give it 120–130 range or 6 s cooldown.
8. **BS-3** – costs 1000 research while the T-54 (same ammo, 50% faster, enclosed, 34-round magazine) costs 800 and the M26 costs 500. Research → 600–700.
9. **Pak 44** – 15 s cooldown gives 3.6 RPM; the Gvozdika at +220 silver has the same radius, a 40-round magazine, enclosure, indirect and shelling. Cooldown → 10–12.
10. **2S1 Gvozdika and 2S3 Akatsiya** – both 15 s cooldown while the Msta/GCT fire every 9 s. Per silver they are the weakest howitzers (148 / 245 cells per minute). Cooldown → 10 for both, or Gvozdika → 1000 silver.
11. **2S19 Msta** – 94% of the GCT's price for 46% of its output. Either Msta cooldown 9 → 6, or GCT burst 3 → 2.
12. **FV 510 Warrior** – 39 ticks between burst shots gives 65 sustained RPM; the Flak M42 (514 silver, 500 research) does 165 RPM with a heavier shell. Ticks → 20–24, or research 800 → 500.
13. **SIDAM 25** – 6 spacer components (900 of its 1783 silver) for Kugelblitz-class output, next to a Vulcan at 1717 silver and 2 spacer components. Spacer → 2.
14. **M247** – 4 spacer components; 2 would match its output tier.
15. **Auto Flak 38 / Auto Deacon** – the unmanned small guns sit behind Advanced Fabrication + 2000–3000-point gates + FCS research and still underperform CE's own unmanned autocannon turret (Heavy Turrets 1600). UnmannedLargeCannons at 3000 points unlocks one AP-only 57mm. Gates → ~1200, and give the U-Deacon the Pak 40's HE or let it share the 57mm HE.

## Overvalued turrets (too strong for what they cost)

1. **7.5 cm le.IG 18** – 328 dmg r4.0 on a 75mm shell (the Pak 40's 75mm HE is 105 r2). At 634 silver and 9.2 RPM it out-kills everything up to the 152mm guns; 464 cells/min vs 148 for the Gvozdika. Expected: ~120 dmg r2.5–3, which still makes it a fine cheap infantry gun. Note the shell also eats 10.5 FSX per round.
2. **M61 Vulcan** – 1500–3000 RPM in 20–100-round bursts, 1500-round magazine, 1-second reload, CE 20x102 ammo at ~1 silver per round, CIWS, enclosed, at 1717 silver / 500 research. CE's autocannon turret at the same price sustains ~130 RPM. Either price it like a naval gun (spacer 6+, ~3000 silver) or gate RotaryCannons behind Advanced Fabrication.
3. **GAU-8 Avenger** – up to 3900 RPM (more than one shot per tick), 80-dmg/70-AP rounds, range 110, sights 3.5, spread 0.29, for 3827 silver. It outguns the 203mm naval gun at 65% of the cost and 70% of the research. Same remedies; also it is not enclosed while the cheaper Vulcan is.
4. **Otobreda 127/54C** – 46.5 RPM with 262-dmg r3.5 shells (1790 cells/min, the best per-silver figure of all big guns) for +440 silver over the Mark 16. Its 11 000 silver/min ammo burn self-limits, but between the two the Mark 16 becomes pointless. Burst 3 → 2 or cooldown 2 → 3.
5. **155mm GCT** – not broken, but its 3-round burst doubles the Msta at near-identical price. See Msta note.
6. **R3-T106** – 20 RPM of 400-AP HEAT for 603 silver is the best anti-mech value in the mod; 180 HP and 2100 silver/min of ammo keep it honest. Acceptable as a glass cannon.

## Progression and research issues

- **Autocannons require Fabrication (4000).** The Flak 38 (150 steel, 1 component) is designed as an early turret, but it unlocks after Fabrication, when the player already has CE's autocannon turret (Heavy Turrets 1600). Suggest HeavyAutocannons prerequisite → Machining or GunTurrets, and put Fabrication on the modern IFV turrets (BMP-2, Marder, SIDAM, M247, FV510, Kugelblitz) individually.
- **Howitzers require Mortars + Fabrication.** Same problem for the le.IG 18, sIG 33 and BL Mk.1; WWII pieces could need Mortars only.
- **Cannon research costs are inverted:** M26 500 < Flak 41 700 < T-54 800 < BS-3 1000, while actual strength is M26 ≈ T-54 > BS-3 > Flak 41. Suggested: Flak 41 500, BS-3 700, T-54 900, M26 900.
- **Naval entry is a trap:** the first naval unlock (SK C/34, 500) is the worst naval gun; the Mark 16 (RF line, 500 + Advanced Fabrication) is the real entry point.
- **Unmanned gates** (2000–3000 per family) plus Automated_FCS (1000, needs Advanced Fabrication) plus FCS_2 (1000) put the small unmanned autocannons ~7000 points behind CE's unmanned autocannon turret with no performance edge. Rotary cannons, by contrast, are unlocked for 1200 points after Fabrication.
- **Direct-fire range:** howitzers and naval guns in direct mode have verb ranges of 3550–33540 (map-wide), while cannons top out at 130 and unmanned howitzers are capped at 120. If this is used as engagement range, the Pak 44 / Leopard's "long range" is not a selling point; the Gvozdika outranges them for +220 silver.
- **Manned vs unmanned trade:** every unmanned conversion costs more silver, 3000+ extra research, an FCS, and loses 20–40% RPM to the 3 s aim time. The only gain is one freed colonist. That is a fair design, but the small autocannon conversions do not clear the CE-turret bar.

## Bugs found while extracting

1. `Common/Defs/Ammo/57mmOQF.xml`: the APDS recipe (`MakeAmmo_57mmOQF_APDS`) produces `Ammo_57mmOQF_AP`, so 57mm APDS is uncraftable (trade only). The docs extractor therefore also shows the wrong AP recipe cost on the site.
2. No naval gun has `CompProperties_EnclosedTurret`; the GAU-8 lacks it while the Vulcan has it. The 634-silver DCA 40 protects its gunner, the 5900-silver 203mm does not.
3. Stale description strings: 2S1 Gvozdika says Power 400 / HP 2000 (def: 200 / 720); Auto Flak 38 says HP 390 and 500 (def: 340); Auto SPz Marder says HP 546 and 1100 (def: 475); Pak 40 says "Explosion Radius: 3" (def: 2).
4. `docs/data/turrets_data.json` is from 2026-08-28 and predates the naval-gun def changes; rerun `docs/scripts/extract_turrets.py` before publishing the site.

## Howitzers re-read under design intent (crowd deterrent, indirect fire)

Intent from the author: small/early howitzers are crowd deterrents; their value is indirect fire at a price below naval guns; low caliber = low radius and low effective accuracy because of mortar scatter; higher tiers should get better accuracy and muzzle velocity; shell damage is secondary because shells rarely hit.

What the XML currently does:

| Indirect weapon | circularError | indirectFirePenalty | sway | sights | charges |
|---|---|---|---|---|---|
| le.IG 18, sIG 33, BL Mk.1, Karl | 1 | 0.125 | 0.9 | 1 | 30/60/90/120 |
| Gvozdika, Akatsiya, Msta | 1 | 0.125 | 0.9 | 1 | 50–150 |
| 155 GCT | 1 | 0.125 | 0.1 | 1 | 50–150 |
| 2A3 | 1 | 0.125 | 0.1 | 1 | 70–160 |
| All naval guns | 1 | 0.125 | 0.2 | 4–5 | 50–150 |
| Auto Msta / Auto Mark 16 | 1 | 0.125 | 0.4 / 0.1 | 2.9 | 30–90 |

- Scatter is identical for every howitzer (CE 1, penalty 0.125): ~8–9 cell miss radius at 60 cells, ~13 at 100. Only the GCT has lower sway. The accuracy tiering exists only between howitzers and naval guns, and it favours naval guns (sights 4–5, sway 0.2).
- CE's CompCharges selects the lowest charge that reaches the target, so higher charge speeds only extend max range. For a given range a higher charge means a higher arc and a longer flight time (worse vs moving targets). `TurretChargeBoostExtension` (chargeOffset) is the lever that forces flatter, faster shots; it is only applied to the 76mm OTO Melara.
- With scatter fixed, hit probability on a group scales with shell radius squared, so radius (not damage) is the shell stat that matters. Approximate coverage of the scatter disc at 60 cells: r2.5 ≈ 9%, r4 ≈ 22%, r5.5 ≈ 42%. "Kill area per minute" (RPM x area) remains the right deterrent proxy.
- Warmup is 0.5 s on every manned arcing gun; unmanned Msta/Mark 16 get 30–90 charges while their manned versions get 50–150.

Re-verdicts under the intent:

- le.IG 18: r4.0 / 328 dmg contradicts "low caliber = low radius". Suggest r2.5, ~120 dmg, keep 9 RPM.
- sIG 33: r5.0 exceeds the 152mm shells (r4.5). Suggest r4.0.
- Gvozdika / Akatsiya: 15 s cooldown with 8-cell scatter is not a deterrent. Suggest 8–10 s and tighter scatter than WWII guns.
- "Build 2–3 cheap ones" does not pay: 3x Gvozdika (~3500 silver) ≈ 440 cells/min vs 1x GCT (~2550 silver) ≈ 870 cells/min.
- BL Mk.1 at 1.7 RPM cannot deter; as a siege piece it needs the bigger radius and longer shelling range noted above.

Suggested tier knobs (XML only):

| Tier | circularError | indirectFirePenalty | sway | cooldown |
|---|---|---|---|---|
| WWII (le.IG 18, sIG 33, BL Mk.1, Karl) | 1.0 | 0.125 | 0.9 | as is |
| Cold War (Gvozdika, Akatsiya) | 0.75 | 0.10 | 0.6 | 8–10 s |
| Modern (Msta, GCT) | 0.5 | 0.075 | 0.3 | 9 s; GCT burst 2 |
| Naval | keep | keep | 0.2 | decide whether naval should out-shoot modern howitzers indirectly |

Optionally add `TurretChargeBoostExtension` to the modern howitzers so "better muzzle velocity" produces flatter, faster shots against moving targets.

## Changes applied (2026-10-16, howitzer pass)

Per the author's direction: le.IG 18 accuracy is the baseline; long-barrel howitzers get better accuracy; sIG 33 keeps 152mm-class yield with slower fire and baseline accuracy; 122mm and 152mm gain a fragmentation airburst shell that is strong against infantry and weak against armor.

| Def | Before | After |
|---|---|---|
| le.IG 18 HE (both variants) | 328 dmg, r4.0, 6L/3S frags, 10.5 FSX/rd, shelling dmg 0.26, MV 130 | 120 dmg, r2.5, 8L/4S frags, 2 FSX/rd, shelling dmg 0.10, MV 82, work 3600/rd |
| sIG 33 HE (both variants) | 479 dmg, r5.0, 40L/6S, 17 FSX | 386 dmg, r4.5, 40L/4S, 13 FSX, shelling dmg 0.316, MV 349 (matches 152mm OF-540) |
| Gvozdika | CE 1, IFpen 0.125, sway 0.9, sights 1, cooldown 15 | CE 0.6, IFpen 0.09, sway 0.5, sights 1.5 (both modes), cooldown 10 |
| Akatsiya | same as above | same as Gvozdika |
| Msta | CE 1, IFpen 0.125, sway 0.9, sights 1 | CE 0.5, IFpen 0.075, sway 0.4, sights 2 (both modes) |
| GCT | CE 1, IFpen 0.125, direct sway 0.9, sights 1 | CE 0.5, IFpen 0.075, direct sway 0.4, sights 2 |
| 122mm set | HE only | + `Ammo_122mm2A12_AB` airburst: direct 30 Airburst_Fragment + 30 Fragment_Small (Flak, proximity 5); indirect 45 + 30 (altitude 15); recipe 302 steel / 4 comp / 30 FSX per 5 |
| 152mm set (Akatsiya, Msta, Auto Msta) | HE only | + `Ammo_152mm2A65_AB` airburst: direct 40 + 40; indirect 60 + 40; recipe 88 steel / 2 comp / 11 FSX per 1 |

Notes:
- Airburst shells use only `Airburst_Fragment` (40 dmg, 10.5 mm AP) and `Fragment_Small` (12 dmg, 2 mm AP), so they do not penetrate marine armor (18) or centipede plating (20). No `Airburst_Fragment_Large` on purpose.
- The Gvozdika/Akatsiya cooldown cut to 10 s is the one change not asked for by name; it is what makes "sIG 33 has a lower fire rate than its long-barrel sibling" true (sIG 33 stays at 15 s) and it serves the crowd-deterrent intent. Revert to 15 if unwanted.
- Airburst ammo icons reuse the caliber's HE texture (`122mm2A12HE`, `152mm2A65HE`); a dedicated AB icon can be dropped in later.
- Turret description strings still quote the old numbers (they were already stale before this pass).
- Resulting deterrent proxy (cells/min): le.IG 18 464 → 180; sIG 33 304 → 248; Gvozdika 148 → 215; Akatsiya 245 → 363; Msta 400; GCT 874.

## Bug fixes applied (2026-10-16)

1. `Common/Defs/Ammo/57mmOQF.xml`: `MakeAmmo_57mmOQF_APDS` now produces `Ammo_57mmOQF_APDS` (was `Ammo_57mmOQF_AP`). 57mm APDS is craftable; AP now reads 78 steel per 10 as intended.
2. `CompProperties_EnclosedTurret` (bullet 1.0 / explosive 0.8 / temperature 0.5, hide pawn) added to every naval gun base def (direct and indirect) and to the GAU-8 Avenger, matching the Msta's values.
3. Description strings corrected to the def values: Gvozdika (Power 200, HP 720, cooldown 10 s, airburst listed), Akatsiya (Power 200, HP 1280, cooldown 10 s, airburst listed), Msta (HP 2625, airburst listed), GCT (HP 1720), le.IG 18 (unpowered, HP 315, reload 6 s, 120 dmg r2.5), sIG 33 (unpowered, HP 495, reload 15 s, 386 dmg r4.5), Pak 40 (HE radius 2), Auto Flak 38 (HP 340), Auto SPz Marder (HP 475, cooldown 1.0 s).
4. `docs/data/turrets_data.json` and `docs/images/extracted/` regenerated from the current defs with `docs/scripts/extract_turrets.py` (adds the two airburst ammo icons, refreshes the Type C image).

Note: the TurretEditor MCP server in this session reads `~/Documents/AbsolutelyMoreCannonsCE` (commit c1b0319), not this Steam Mods checkout, so its audit does not reflect these edits. Validation here was an XML parse of every def file plus the docs extractor.
