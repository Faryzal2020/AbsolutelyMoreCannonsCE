# Historical Documentation: Automated Ammo Fabricator (`AMC_AmmoFabricator`)

## MANDATORY DOCUMENTATION RULES
- No editing, erasing, or modifying what has already been written except for typos.
- Each entry starts with a datetime (`YYYY-MM-DD HH:mm:ss`) followed by bullet points.
- Each bullet point explains what was implemented and why (e.g., "Implemented the UI with `ITab_AmmoCapacitor.cs` because...").
- If the user reports a completed test, it means the last entry has been tested; note the test result based on the user's feedback.
- Every mistake and error traced based on a discovered error (e.g., from logs) must be written in a new entry.
- Fix implementations for the error must be in the same entry that responds to the test result of the previous entry.
- Each error/fix entry must state what the goal was, why it was not working, and what the mistake was.
- The procedure repeats for subsequent entries:
  `Initial Entry` -> `User test & report` -> `Error identification & Fixes entry` -> `User test & report` -> `Error identification & Fixes entry` -> etc.

---

## 2026-11-19 22:48:00 — Initial Implementation of Automated Ammo Fabricator

- **Goal:** Create a mid-to-end-game automated ammunition manufacturing machine (`AMC_AmmoFabricator`) based on `CNCmachine.xml` that eliminates multi-stack ingredient clutter and pawn hauling bottlenecks around crafting benches by storing raw materials in internal virtual capacitors and autonomously crafting ammunition via a custom 3-column UI.
- Implemented `CompProperties_AmmoCapacitor.cs` because the building needs XML-configurable properties for autonomous work speed (`workSpeedFactor`), output tile offset (`outputCellOffset`), default UI step size (`defaultStepSize`), and an extensible `<materials>` list categorizing projectile and fuel materials along with their substitution equivalence ratios (`equivalenceValue` and `canSubstitute`).
- Implemented `CompAmmoCapacitor.cs` and `AmmoCraftTicket` because the machine requires virtual integer storage (`storedAmounts` and `targetAmounts`) to hold thousands of Steel, Plasteel, FSX, Chemfuel, and Components without physical stack limits, autonomous tick-based crafting (`CompTick`) when powered and flicked on, automatic dropping of excess materials or finished ammo onto the XML-defined adjacent tile (`outputCellOffset`), and optional material substitution across same-category materials (disabled by default via `allowSubstitution = false`).
- Implemented `ITab_AmmoCapacitor.cs` because the machine replaces the vanilla `ITab_Bills` crafting interface with a dedicated 3-column UI:
  - Left column lists all craftable `CombatExtended.AmmoDef` recipes with thumbnail icons and a search filter.
  - Middle column displays the active autocrafting ticket queue with live progress bars, pause/delete controls, and a bottom detail panel for configuring One-Time batch counts or Continuous Auto start/stop (`minThreshold` / `maxThreshold`) rules.
  - Right column displays the internal material capacitors grouped by Projectile and Fuel categories, a configurable `+/-` step amount input field, a toggle for Material Substitution, and `-` / `+` target buttons.
- Implemented `WorkGiver_HaulToAmmoCapacitor.cs` and `JobDriver_HaulToAmmoCapacitor.cs` because colonists need a hauling AI job to automatically pick up raw materials on the map and deposit them into the machine's internal capacitor whenever `stored < target`.
- Implemented `Common/Defs/ThingDefs_Buildings/AmmoFabricator.xml` and `Common/Defs/JobDefs/Jobs_AmmoCapacitor.xml` because RimWorld requires `ThingDef`, `JobDef`, and `WorkGiverDef` definitions to register the building, its custom inspector tab, its material equivalence list, and its hauling jobs.
- Compiled `Assemblies/AbsolutelyMoreCannons.dll` in Release configuration with 0 warnings and 0 errors; awaiting user in-game testing and feedback.

---

## 2026-11-19 23:36:30 — Live In-Game Automated Playtest via `DevAgentBridge`, Error Identification & Fixes

- **Test Result of Previous Entry:** Implemented `DevAgentBridge.cs` (`http://127.0.0.1:8765/`) to launch RimWorld, load `TEST.rws`, spawn `AMC_AmmoFabricator`, open `ITab_AmmoCapacitor`, and test hauling, excess material dropping, autonomous crafting, and material substitution. During the initial live playtest runs, three errors were discovered from the in-game runtime logs:
  1. **Error 1 (`GenPlace.TryPlaceThing` `MissingMethodException`):**
     - **Goal:** Drop crafted ammunition and excess capacitor materials onto the configured adjacent tile (`outputCellOffset`) via `GenPlace.TryPlaceThing`.
     - **Why it was not working:** Calling `GenPlace.TryPlaceThing` threw `System.MissingMethodException: Method not found: 'Boolean Verse.GenPlace.TryPlaceThing(Verse.Thing, Verse.IntVec3, Verse.Map, Verse.ThingPlaceMode, Verse.Thing ByRef, System.Action`2<Verse.Thing,Int32>, System.Predicate`1<Verse.IntVec3>, Verse.Rot4)'`.
     - **What the mistake was:** The C# project compiles against `Krafs.Rimworld.Ref 1.5.4104`, whereas the local RimWorld runtime is `1.7.5013` (Waylith), where `GenPlace.TryPlaceThing` has an modified method signature.
     - **Fix Implementation:** Implemented `CompAmmoCapacitor.TryPlaceThingSafe` in `CompAmmoCapacitor.cs` (and updated `DevAgentBridge.cs`) to resolve and invoke `GenPlace.TryPlaceThing` dynamically via reflection across RimWorld versions.
  2. **Error 2 (`CompAmmoCapacitor.CompTick` never advancing crafting progress):**
     - **Goal:** Autonomously advance `ticket.workProgress` on every game tick when powered and switched on.
     - **Why it was not working:** Stepping 10,000 ticks left `workProgress` at `0.0` and `materialsLocked` at `false`.
     - **What the mistake was:** `AMC_AmmoFabricator` in `AmmoFabricator.xml` inherited from `BuildingBase` (unlike `CNCmachine`, which inherited from `BenchBase`). `BuildingBase` defaults to `<tickerType>Never</tickerType>` (or `Rare`), so RimWorld never invoked `CompTick()` on `AMC_AmmoFabricator`.
     - **Fix Implementation:** Added `<tickerType>Normal</tickerType>` to `AMC_AmmoFabricator` in `Common/Defs/ThingDefs_Buildings/AmmoFabricator.xml` so `CompAmmoCapacitor.CompTick()` executes every game tick.
  3. **Error 3 (`Widgets.ThingIcon` / `Widgets.DrawTextureFitted` `MissingMethodException` in `ITab_AmmoCapacitor`):**
     - **Goal:** Render ammunition and material thumbnail icons inside the 3-column `ITab_AmmoCapacitor` UI.
     - **Why it was not working:** Opening `ITab_AmmoCapacitor` logged `System.MissingMethodException: Method not found: 'Void Verse.Widgets.ThingIcon(...)'` (and subsequently `Widgets.DrawTextureFitted`), which aborted `DrawLeftAmmoList` mid-scrollview and caused `Mouse position stack is not empty. There were more calls to BeginScrollView than EndScrollView`.
     - **What the mistake was:** `Widgets.ThingIcon` and `Widgets.DrawTextureFitted` changed optional default parameters between `Krafs.Rimworld.Ref 1.5.4104` and RimWorld `1.7.5013`.
     - **Fix Implementation:** Implemented `DrawDefIcon(Rect rect, ThingDef def)` in `ITab_AmmoCapacitor.cs` using Unity's version-independent `GUI.DrawTexture(rect, icon, ScaleMode.ScaleToFit)` with `def.uiIcon` and `def.uiIconColor`.
- **Verification Re-Test Results (All Passed with 0 Mod Errors):**
  - **Spawning & Power:** `AMC_AmmoFabricator` spawned at `(145, 130)` with `outputCell` at `(145, 129)`, powered on, and discovered `931` craftable ammunition recipes.
  - **Colonist Hauling (`AMC_Job_HaulToAmmoCapacitor`):** Colonist `Hacke` automatically started `AMC_Job_HaulToAmmoCapacitor` and filled `Steel` capacitor storage to `225 / 225`.
  - **Excess Material Drop on `-` Button:** With `525` Steel stored (`225` hauled + `300` deposited), lowering `Steel` target to `150` immediately updated internal storage to `150 / 150` and popped `375` Steel onto the map (`1937 -> 2312`, difference `+375`).
  - **Autonomous Crafting:** Created a One-Time ticket for `MakeAmmo_600mmKarlGerat_HE` (`145,200` work, `434 Steel`, `253 FSX`, `1 ComponentIndustrial`). Stepping `10,000` ticks advanced `workProgress` to `40,008 / 145,200` (`27.6%`, `materialsLocked: true`), and stepping the remaining `27,000` ticks popped `1x Ammo_600mmKarlGerat_HE` (`60cm Sch.Btgr (HE)`) onto the output cell `(145, 129)` and removed the completed One-Time ticket.
  - **Material Substitution (`Steel` -> `Plasteel` at `4.0x` equivalence):** With `Steel = 0` and `Plasteel = 200`, verified that when `allowSubstitution = false` (default), the continuous ticket (`min=5, max=10`) waited without locking materials. When toggled to `allowSubstitution = true`, the machine immediately consumed `Ceil(434 / 4.0) = 109 Plasteel` (leaving exactly `91 Plasteel` in the capacitor) and began crafting (`materialsLocked: true`, `workProgress: 44.0`).
  - **Runtime Error Check:** Confirmed `0` errors from `AbsolutelyMoreCannons` in `Verse.Log.Messages` even while `ITab_AmmoCapacitor` was open and rendering.

---

## 2026-11-20 00:42:00 — User Test Report, Second Inspector Tab (`ITab_AmmoCapacitorUpgrades`), Tier Upgrades & Specialization Modules

- **User Test Result of Previous Entry:** User tested `AMC_AmmoFabricator` in-game and confirmed it works as expected so far; requested a second inspector tab UI alongside `Fab. Ammo` for **Machine Tier Upgrades** and **Specialization Modules**.
- **Goal:** Add a second inspector tab (`Upgrades`) alongside `Fab. Ammo` that supports:
  1. Multi-level XML-configurable machine upgrades (4 levels starting at `5 ComponentIndustrial`) where each level increases crafting speed by a percentage and refunds a percentage of recipe material costs back into the internal capacitor upon completing each batch.
  2. A specialization module bay (similar to turret FCS modules) that accepts crafted module items to modify crafting performance by caliber tier, starting with a placeholder `Cannon Focus Mod` (`AMC_Module_CannonFocus`) that speeds up Medium Caliber ammunition (`20mm` to `130mm`) by `+50%` while slowing other ammunition by `-20%`.
- Implemented `FabricatorModuleExtension.cs` (`DefModExtension`) and `AmmoCaliberTier` because the fabricator needs a universal method to classify "Medium Caliber" (`20mm` to `130mm`) across both AMC and Combat Extended ammunition defs by parsing millimeter (`20x138mm`, `105mm`), centimeter (`8.8cm`, `12.8cm`, `60cm`), and pounder (`6-pounder` = `57mm`) patterns from `defName`, `label`, and category labels (with a shell mass fallback heuristic for non-metric names).
- Updated `CompProperties_AmmoCapacitor.cs` to add `CapacitorUpgradeLevel` (`label`, `speedMultiplier`, `materialRebatePercent`, `costList`) and `<upgradeLevels>` list so all upgrade tiers, costs, speed bonuses, and material rebates are configurable in XML.
- Updated `CompAmmoCapacitor.cs` to implement `IThingHolder` for holding an installed module (`moduleContainer` / `targetModuleDef`), track `upgradeLevel`, deduct upgrade costs directly from internal capacitor storage (`TryPerformNextUpgrade`), apply combined upgrade and module speed multipliers (`GetEffectiveWorkSpeed`), and refund `CurrentRebatePercent` of recipe ingredients back into `storedAmounts` upon batch completion (`CompleteTicketBatch`).
- Implemented `ITab_AmmoCapacitorUpgrades.cs` (`labelKey = "Upgrades"`) because the building needs a second inspector tab button next to `Fab. Ammo`:
  - Left panel displays current tier status, all 4 XML-defined upgrade levels with speed and rebate bonuses, live capacitor material cost checks, and the **Upgrade to Level N** button.
  - Right panel displays the **Specialization Module Bay** (current installed module, eject button, or pending haul status) and a list of all available `FabricatorModuleExtension` items in the database with one-click colonist haul assignment.
- Updated `WorkGiver_HaulToAmmoCapacitor.cs` and `JobDriver_HaulToAmmoCapacitor.cs` so colonists automatically haul and install the assigned `targetModuleDef` item into the fabricator's module container.
- Created `Common/Defs/ThingDefs_Items/Items_FabricatorModules.xml` defining `AMC_Module_CannonFocus` (`Cannon Focus Mod`, using `Things/Item/FCS/FCS_Advanced` placeholder texture, `+50%` speed for `20mm~130mm` Medium Caliber, `-20%` speed for other ammo) and its crafting recipe `AMC_Make_Module_CannonFocus`.
- Updated `Common/Defs/ThingDefs_Buildings/AmmoFabricator.xml` to register `AbsolutelyMoreCannons.ITab_AmmoCapacitorUpgrades` in `<inspectorTabs>` alongside `ITab_AmmoCapacitor` and define 4 `<upgradeLevels>` (Level 1: `+25%` speed / `5%` rebate for `5 ComponentIndustrial`; Level 2: `+50%` speed / `10%` rebate; Level 3: `+100%` speed / `15%` rebate; Level 4: `+175%` speed / `25%` rebate).

---

## 2026-11-20 02:22:30 — Pawn-Delivered Material & Crafting-Skill Work Upgrade Flow

- **Goal:** Change the machine upgrade workflow so that instead of deducting materials from the internal capacitor, clicking `"Start Upgrade (Requires: X)"` pauses the machine's autonomous crafting, waits for colonists to haul and deliver the required upgrade materials directly to the machine, and then waits for a colonist with sufficient Crafting skill (`minCraftingSkill`) to come to the interaction cell and work on the machine (`workAmount`) to complete the upgrade.
- Updated `CompProperties_AmmoCapacitor.cs` (`CapacitorUpgradeLevel`) and `Common/Defs/ThingDefs_Buildings/AmmoFabricator.xml` to add XML-configurable `<workAmount>` (`2000` / `3500` / `5000` / `7500` ticks) and `<minCraftingSkill>` (`6` / `8` / `10` / `12`) for each of the 4 upgrade levels.
- Updated `CompAmmoCapacitor.cs` to add `isUpgrading`, `upgradeDeliveredAmounts`, and `upgradeWorkDone`:
  - When `StartUpgrade` is triggered, `isUpgrading` becomes `true` and `CompTick()` halts autonomous ammo crafting.
  - Tracks missing upgrade materials (`GetMissingUpgradeMaterials`) and delivered upgrade materials (`DepositUpgradeMaterial`).
  - If the user cancels an in-progress upgrade (`CancelUpgrade`) or the building is destroyed, any already-delivered upgrade materials are dropped onto the adjacent output tile.
  - Once `AreUpgradeMaterialsSatisfied` is true, `DoUpgradeWork(float workAmount, Pawn worker)` advances `upgradeWorkDone` until `UpgradeWorkTotal` is reached and increments `upgradeLevel`.
- Updated `WorkGiver_HaulToAmmoCapacitor.cs` and `JobDriver_HaulToAmmoCapacitor.cs` so hauling colonists prioritize delivering any missing upgrade materials (`comp.isUpgrading && !comp.AreUpgradeMaterialsSatisfied`) to `DepositUpgradeMaterial`.
- Implemented `WorkGiver_UpgradeAmmoCapacitor.cs`, `JobDriver_UpgradeAmmoCapacitor.cs`, and `AMC_Job_UpgradeAmmoCapacitor` / `AMC_WorkGiver_UpgradeAmmoCapacitor` in `Common/Defs/JobDefs/Jobs_AmmoCapacitor.xml` (`<workType>Crafting</workType>`) so a colonist with Crafting skill `>= minCraftingSkill` walks to the machine's interaction cell and performs the physical upgrade work with a progress bar and `ConstructMetal` effect.
- Updated `ITab_AmmoCapacitorUpgrades.cs` to replace the instant capacitor upgrade button with `"Start Upgrade (Requires: ...)"` (and a cancel button showing live delivery/work progress while an upgrade is active).
- Compiled `Assemblies/AbsolutelyMoreCannons.dll` in Release mode with 0 warnings and 0 errors.

---

## 2026-11-20 03:06:00 — User Test Report, 3000 Combined Storage Limit Bar, Upgrade Storage Buffs & Space Optimizer Mod

- **User Test Result of Previous Entry:** User tested the pawn-delivered upgrade materials and crafting-skill upgrade work flow in-game and confirmed everything works well.
- **Goal:**
  1. Limit total combined internal material storage of `AMC_AmmoFabricator` to `3000` base capacity (`<baseMaxStorage>3000</baseMaxStorage>`).
  2. Below the capacitor panel section in `ITab_AmmoCapacitor`, display a clean horizontal fill bar from left to right with centered text `"allocated / max"` (e.g., `"750 / 3000"`), keeping detailed breakdown information inside the hover tooltip.
  3. Add storage capacity buffs (`<storageMultiplier>`) to each of the 4 upgrade levels (e.g., Level 1 adds `+20%` storage).
  4. Add a new specialization module item `"Space Optimizer Mod"` (`AMC_Module_SpaceOptimizer`) that provides `+50%` total internal capacitor storage capacity.
- Updated `CompProperties_AmmoCapacitor.cs` and `FabricatorModuleExtension.cs` to add `baseMaxStorage = 3000` and `storageMultiplier = 1.0f` on both `CapacitorUpgradeLevel` and `FabricatorModuleExtension`.
- Updated `CompAmmoCapacitor.cs` to compute `CombinedStorageMultiplier`, `MaxTotalStorage`, `TotalAllocatedStorage`, and `TotalStoredMaterials`, clamp `SetTarget` so total allocated demand across all materials cannot exceed `MaxTotalStorage`, clamp `DepositMaterial` and material rebates to available total storage room, and clamp/drop excess allocations via `ClampAllocationsToMaxStorage()` if a storage-boosting module is ejected.
- Updated `ITab_AmmoCapacitor.cs` (`DrawRightCapacitors`) to reserve a `26px` horizontal progress bar at the bottom of the right capacitor panel showing only the left-to-right fill bar and centered `"{allocated} / {maxStorage}"` text, with a hover tooltip showing stored total, unallocated capacity, base capacity, upgrade bonus, and module bonus.
- Updated `ITab_AmmoCapacitorUpgrades.cs` to display the storage capacity bonus (`Storage: +20%` / `+40%` / `+70%` / `+100%`) on each upgrade tier card and dynamically format `Space Optimizer Mod` (`Capacitor Storage Capacity: +50%`) in the module list.
- Updated `Common/Defs/ThingDefs_Buildings/AmmoFabricator.xml` with `<baseMaxStorage>3000</baseMaxStorage>` and `<storageMultiplier>` values (`1.20`, `1.40`, `1.70`, `2.00`) across the 4 upgrade levels.
- Updated `Common/Defs/ThingDefs_Items/Items_FabricatorModules.xml` to add `AMC_Module_SpaceOptimizer` (`Space Optimizer Mod`, `+50%` capacitor storage, using `Things/Item/FCS/FCS_Basic` placeholder texture) and its crafting recipe `AMC_Make_Module_SpaceOptimizer`.
- Compiled `Assemblies/AbsolutelyMoreCannons.dll` in Release mode with 0 warnings and 0 errors.

---

## 2026-11-20 03:11:45 — Pseudolocalized Inspector Tab Label Error Identification & Fix

- **User Test Result of Previous Entry:** User reported a screenshot showing the two inspector tab buttons rendered with diacritical/accented characters (`Uṗğr̲àðèş` and `Fàþ. Aṁṁò`).
- **Error Identification & Fix:**
  - **Goal:** Display clean `"Fab. Ammo"` and `"Upgrades"` text labels on the two inspector tab buttons of `AMC_AmmoFabricator`.
  - **Why it was not working:** In RimWorld, `InspectTabBase` resolves its tab button text by calling `labelKey.Translate()`. When Developer Mode is active (`Prefs.DevMode == true`) and a string passed to `.Translate()` is not registered in a `Languages/English/Keyed/*.xml` translation file, RimWorld's `Translator.PseudoTranslated` automatically converts every ASCII letter into accented pseudolocalization characters (`a -> à`, `b -> þ`, `d -> ð`, `e -> è`, `g -> ğ`, `m -> ṁ`, `o -> ò`, `p -> ṗ`, `s -> ş`) to warn modders that the string is an untranslated key.
  - **What the mistake was:** `ITab_AmmoCapacitor` and `ITab_AmmoCapacitorUpgrades` set `labelKey = "Fab. Ammo"` and `labelKey = "Upgrades"` directly as raw English strings without creating a `Languages/English/Keyed/` XML file defining their translation keys.
  - **Fix Implementation:**
    1. Created `Languages/English/Keyed/AMC_Keys.xml` (and `Common/Languages/English/Keyed/AMC_Keys.xml`) defining `<AMC_TabAmmoCapacitor>Fab. Ammo</AMC_TabAmmoCapacitor>` and `<AMC_TabAmmoCapacitorUpgrades>Upgrades</AMC_TabAmmoCapacitorUpgrades>`.
    2. Updated `ITab_AmmoCapacitor.cs` to use `labelKey = "AMC_TabAmmoCapacitor"` and `ITab_AmmoCapacitorUpgrades.cs` to use `labelKey = "AMC_TabAmmoCapacitorUpgrades"`.

---

## 2026-11-20 03:21:00 — User Test Report, Probabilistic Material Rebates, Upgrade Card Tooltips & Text Clipping Fixes

- **User Test Result of Previous Entry:** User provided an in-game screenshot of the `Upgrades` tab (`ITab_AmmoCapacitorUpgrades`) and reported four UI/gameplay issues:
  1. In the upgrade tier cards, the meaning of `"Rebate: 5%"` and `"Skill: 6+"` was unclear without explanatory text or tooltips.
  2. Material rebates should not trigger on every single craft; instead, they should have a probability chance per completed batch (e.g., `"5% chance to return 10% of the materials for every craft"`).
  3. On the Level 3 upgrade card (`Precision Plasteel Press`), the third required material (`2x Advanced component`) was truncated to `"2x"` because the 4-stat line and single-line `22px` material label rect ran out of horizontal space.
  4. On the `Cannon Focus Mod` card in the right module bay, the second part of the effect line (`"| Other: -20% slower"`) was cut off after `"Other:"`.
- **Error Identification & Fixes:**
  - **Goal:** Make material rebates probabilistic (`rebateChance` + `materialRebatePercent`), prevent text truncation on multi-material upgrade cards and module effect summaries, and provide clear labels and hover tooltips explaining every stat.
  - **Why it was not working / What the mistake was:**
    - `CapacitorUpgradeLevel` only had a single `materialRebatePercent` field that refunded materials on 100% of completed batches.
    - In `ITab_AmmoCapacitorUpgrades.cs`, upgrade cards had `cardH = 74f` with a single `18px` line squeezing 4 stats (`Speed`, `Storage`, `Rebate`, `Skill`) and a `22px` height rect for `Required Materials`, which could not wrap `"15x Component | 100x Plasteel | 2x Advanced component"` onto a second line.
    - In `DrawRightModuleSlot` / `GetModuleEffectSummary`, both positive and negative caliber effects were joined onto a single horizontal line (`"Medium (20~130mm): +50% faster | Other: -20% slower"`) inside a `44px` label rect, causing `"Other: -20% slower"` to wrap and get clipped vertically.
  - **Fix Implementation:**
    1. Added `rebateChance` to `CapacitorUpgradeLevel` in `CompProperties_AmmoCapacitor.cs` (and `extraRebateChance` to `FabricatorModuleExtension.cs`), updated `CompAmmoCapacitor.CompleteTicketBatch` to roll `Rand.Value < CurrentRebateChance` before refunding `CurrentRebatePercent` of recipe ingredients, and configured `Common/Defs/ThingDefs_Buildings/AmmoFabricator.xml` with:
       - **Level 1:** `5%` chance to return `10%` of recipe materials
       - **Level 2:** `10%` chance to return `15%` of recipe materials
       - **Level 3:** `15%` chance to return `20%` of recipe materials
       - **Level 4:** `25%` chance to return `25%` of recipe materials
    2. Expanded `ITab_AmmoCapacitorUpgrades` window size to `820x520` and increased upgrade card height from `74px` to `92px`, splitting card stats across dedicated lines:
       - Line 1: `Level N: Title [STATUS]`
       - Line 2: `Speed: +X% (Yx) | Storage: +Z% (Total) | Req. Craft Skill: N+`
       - Line 3: `Material Rebate: X% chance to return Y% of recipe materials per craft`
       - Line 4 (`32px` height for 2-line wrapping): `Required Materials: 15x Component | 100x Plasteel | 2x Advanced component`
    3. Added detailed `TooltipHandler.TipRegion` tooltips to the top summary box, every upgrade tier card, and every module card explaining Crafting Speed, Capacitor Storage, Material Rebate chance/return, Required Colonist Crafting Skill, and Work Time.
    4. Formatted `GetModuleEffectSummary` across separate lines (`"Medium (20~130mm): +50% faster\nOther Calibers: 20% slower"`) and increased module card height from `86px` to `104px` (with a `62px` text rect) so `Cannon Focus Mod` displays its full effect without clipping.
- Compiled `Assemblies/AbsolutelyMoreCannons.dll` in Release mode with 0 warnings and 0 errors.
