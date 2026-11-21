using System;
using System.Collections.Generic;
using System.Linq;
using CombatExtended;
using RimWorld;
using UnityEngine;
using Verse;

namespace AbsolutelyMoreCannons
{
    public class AmmoCraftTicket : IExposable
    {
        public RecipeDef recipe;
        public bool isContinuous = false;
        public int countToCraft = 5;
        public int minThreshold = 75;
        public int maxThreshold = 750;

        public float workProgress = 0f;
        public bool materialsLocked = false;
        public bool autoCraftingActive = false;
        public bool paused = false;

        public ThingDef ProducedAmmoDef => recipe?.ProducedThingDef;

        public int ProducedCountPerBatch
        {
            get
            {
                if (recipe == null || recipe.products == null || recipe.products.Count == 0)
                    return 1;
                return recipe.products[0].count;
            }
        }

        public float TotalWorkAmount
        {
            get
            {
                if (recipe == null) return 1000f;
                return Mathf.Max(1f, recipe.WorkAmountTotal(null));
            }
        }

        public void ExposeData()
        {
            Scribe_Defs.Look(ref recipe, "recipe");
            Scribe_Values.Look(ref isContinuous, "isContinuous", false);
            Scribe_Values.Look(ref countToCraft, "countToCraft", 5);
            Scribe_Values.Look(ref minThreshold, "minThreshold", 75);
            Scribe_Values.Look(ref maxThreshold, "maxThreshold", 750);
            Scribe_Values.Look(ref workProgress, "workProgress", 0f);
            Scribe_Values.Look(ref materialsLocked, "materialsLocked", false);
            Scribe_Values.Look(ref autoCraftingActive, "autoCraftingActive", false);
            Scribe_Values.Look(ref paused, "paused", false);
        }
    }

    public class CompAmmoCapacitor : ThingComp, IThingHolder
    {
        private Dictionary<ThingDef, int> storedAmounts = new Dictionary<ThingDef, int>();
        private Dictionary<ThingDef, int> targetAmounts = new Dictionary<ThingDef, int>();
        public List<AmmoCraftTicket> tickets = new List<AmmoCraftTicket>();

        public bool allowSubstitution = false;
        public int uiStepSize = 75;

        // Upgrade level (0 = Base, 1..N = Props.upgradeLevels[upgradeLevel - 1])
        public int upgradeLevel = 0;

        // Active upgrade project state (pauses autonomous crafting until materials are delivered & pawn finishes upgrading)
        public bool isUpgrading = false;
        private Dictionary<ThingDef, int> upgradeDeliveredAmounts = new Dictionary<ThingDef, int>();
        public float upgradeWorkDone = 0f;

        // Module container & target module to haul
        private ThingOwner moduleContainer;
        public ThingDef targetModuleDef;

        private CompPowerTrader powerComp;
        private CompFlickable flickComp;
        private CompBreakdownable breakdownComp;

        /// <summary>
        /// True during ticks where the fabricator is actively advancing work on a crafting ticket.
        /// </summary>
        public bool IsCraftingNow { get; private set; }

        /// <summary>
        /// Cumulative number of ammunition items produced by this fabricator.
        /// </summary>
        public int totalAmmoCrafted = 0;

        /// <summary>
        /// The ticket currently being crafted on this tick, if any.
        /// </summary>
        public AmmoCraftTicket ActiveTicket { get; private set; }

        private static List<RecipeDef> cachedAmmoRecipes;
        private static List<ThingDef> cachedModuleDefs;

        public CompProperties_AmmoCapacitor Props => (CompProperties_AmmoCapacitor)props;

        public CompAmmoCapacitor()
        {
            moduleContainer = new ThingOwner<Thing>(this);
        }

        public ThingOwner GetDirectlyHeldThings()
        {
            if (moduleContainer == null)
            {
                moduleContainer = new ThingOwner<Thing>(this);
            }
            return moduleContainer;
        }

        public void GetChildHolders(List<IThingHolder> outHolders)
        {
            ThingOwnerUtility.AppendThingHoldersFromThings(outHolders, GetDirectlyHeldThings());
        }

        public Thing InstalledModule => (moduleContainer != null && moduleContainer.Count > 0) ? moduleContainer[0] : null;
        public bool HasModule => InstalledModule != null;

        public FabricatorModuleExtension ActiveModuleExtension => InstalledModule?.def?.GetModExtension<FabricatorModuleExtension>();

        public static List<ThingDef> AllModuleDefs
        {
            get
            {
                if (cachedModuleDefs == null)
                {
                    cachedModuleDefs = DefDatabase<ThingDef>.AllDefsListForReading
                        .Where(d => d.HasModExtension<FabricatorModuleExtension>())
                        .OrderBy(d => d.label)
                        .ToList();
                }
                return cachedModuleDefs;
            }
        }

        public int MaxUpgradeLevel => Props?.upgradeLevels?.Count ?? 0;

        public CapacitorUpgradeLevel CurrentUpgradeDef
        {
            get
            {
                if (upgradeLevel <= 0 || Props?.upgradeLevels == null || Props.upgradeLevels.Count == 0)
                    return null;
                int idx = Mathf.Clamp(upgradeLevel - 1, 0, Props.upgradeLevels.Count - 1);
                return Props.upgradeLevels[idx];
            }
        }

        public CapacitorUpgradeLevel NextUpgradeDef
        {
            get
            {
                if (Props?.upgradeLevels == null || upgradeLevel >= Props.upgradeLevels.Count)
                    return null;
                return Props.upgradeLevels[Mathf.Max(0, upgradeLevel)];
            }
        }

        public float CurrentUpgradeSpeedMultiplier => CurrentUpgradeDef?.speedMultiplier ?? 1.0f;
        public float CurrentUpgradeStorageMultiplier => CurrentUpgradeDef?.storageMultiplier ?? 1.0f;
        public float CurrentModuleStorageMultiplier => ActiveModuleExtension?.storageMultiplier ?? 1.0f;

        /// <summary>
        /// Combined storage multiplier from machine upgrade level and installed specialization module (additive bonus percentages).
        /// For example, Level 1 (+20%) + Space Optimizer Mod (+50%) = 1.0 + 0.20 + 0.50 = 1.70x (5100 max storage).
        /// </summary>
        public float CombinedStorageMultiplier
        {
            get
            {
                float upgBonus = CurrentUpgradeStorageMultiplier - 1.0f;
                float modBonus = CurrentModuleStorageMultiplier - 1.0f;
                return Mathf.Max(0.1f, 1.0f + upgBonus + modBonus);
            }
        }

        public int MaxTotalStorage => Mathf.RoundToInt((Props?.baseMaxStorage ?? 3000) * CombinedStorageMultiplier);

        public int TotalAllocatedStorage
        {
            get
            {
                EnsureMaterialDictionariesInitialized();
                if (targetAmounts == null) return 0;
                int sum = 0;
                foreach (var kvp in targetAmounts)
                {
                    if (kvp.Value > 0) sum += kvp.Value;
                }
                return sum;
            }
        }

        public int TotalStoredMaterials
        {
            get
            {
                EnsureMaterialDictionariesInitialized();
                if (storedAmounts == null) return 0;
                int sum = 0;
                foreach (var kvp in storedAmounts)
                {
                    if (kvp.Value > 0) sum += kvp.Value;
                }
                return sum;
            }
        }

        public int RemainingUnallocatedStorage => Mathf.Max(0, MaxTotalStorage - TotalAllocatedStorage);

        public void ClampAllocationsToMaxStorage()
        {
            EnsureMaterialDictionariesInitialized();
            int excessAlloc = TotalAllocatedStorage - MaxTotalStorage;
            if (excessAlloc <= 0 || Props?.materials == null) return;

            for (int i = Props.materials.Count - 1; i >= 0 && excessAlloc > 0; i--)
            {
                ThingDef def = Props.materials[i].thingDef;
                if (def == null) continue;
                int curTarget = GetTarget(def);
                if (curTarget <= 0) continue;

                int reduceBy = Mathf.Min(curTarget, excessAlloc);
                SetTarget(def, curTarget - reduceBy);
                excessAlloc -= reduceBy;
            }
        }

        public float CurrentRebateChance
        {
            get
            {
                float baseChance = CurrentUpgradeDef?.rebateChance ?? 0f;
                float modChance = ActiveModuleExtension?.extraRebateChance ?? 0f;
                return Mathf.Clamp01(baseChance + modChance);
            }
        }

        public float CurrentRebatePercent
        {
            get
            {
                float baseRebate = CurrentUpgradeDef?.materialRebatePercent ?? 0f;
                float modRebate = ActiveModuleExtension?.extraRebatePercent ?? 0f;
                return Mathf.Clamp01(baseRebate + modRebate);
            }
        }

        public float GetEffectiveWorkSpeed(ThingDef producedAmmoDef)
        {
            float speed = Props.workSpeedFactor * CurrentUpgradeSpeedMultiplier;
            var modExt = ActiveModuleExtension;
            if (modExt != null && producedAmmoDef != null)
            {
                speed *= modExt.GetSpeedMultiplierFor(producedAmmoDef);
            }
            return Mathf.Max(0.05f, speed);
        }

        public int GetUpgradeDelivered(ThingDef def)
        {
            if (def == null || upgradeDeliveredAmounts == null) return 0;
            return upgradeDeliveredAmounts.TryGetValue(def, out int val) ? val : 0;
        }

        public int GetUpgradeRequired(ThingDef def)
        {
            var next = NextUpgradeDef;
            if (def == null || next?.costList == null) return 0;
            int total = 0;
            for (int i = 0; i < next.costList.Count; i++)
            {
                if (next.costList[i].thingDef == def)
                    total += next.costList[i].count;
            }
            return total;
        }

        public int GetUpgradeMissing(ThingDef def)
        {
            if (!isUpgrading) return 0;
            return Mathf.Max(0, GetUpgradeRequired(def) - GetUpgradeDelivered(def));
        }

        public IEnumerable<ThingDefCountClass> GetMissingUpgradeMaterials()
        {
            if (!isUpgrading) yield break;
            var next = NextUpgradeDef;
            if (next?.costList == null) yield break;

            foreach (var cost in next.costList)
            {
                if (cost.thingDef == null || cost.count <= 0) continue;
                int missing = cost.count - GetUpgradeDelivered(cost.thingDef);
                if (missing > 0)
                {
                    yield return new ThingDefCountClass(cost.thingDef, missing);
                }
            }
        }

        public bool AreUpgradeMaterialsSatisfied
        {
            get
            {
                if (!isUpgrading) return false;
                var next = NextUpgradeDef;
                if (next == null) return false;
                if (next.costList == null || next.costList.Count == 0) return true;

                foreach (var cost in next.costList)
                {
                    if (cost.thingDef == null || cost.count <= 0) continue;
                    if (GetUpgradeDelivered(cost.thingDef) < cost.count)
                        return false;
                }
                return true;
            }
        }

        public float UpgradeWorkTotal => Mathf.Max(1f, NextUpgradeDef?.workAmount ?? 2500f);
        public int UpgradeMinCraftingSkill => NextUpgradeDef?.minCraftingSkill ?? 6;

        public bool StartUpgrade(out string message)
        {
            var next = NextUpgradeDef;
            if (next == null)
            {
                message = "Maximum upgrade level reached.";
                return false;
            }
            if (isUpgrading)
            {
                message = "An upgrade is already in progress.";
                return false;
            }

            if (upgradeDeliveredAmounts == null)
                upgradeDeliveredAmounts = new Dictionary<ThingDef, int>();
            else
                upgradeDeliveredAmounts.Clear();

            upgradeWorkDone = 0f;
            isUpgrading = true;
            message = $"Started upgrade to Level {upgradeLevel + 1} ({next.label}). Machine paused while colonists deliver materials and perform the upgrade.";
            return true;
        }

        public void CancelUpgrade()
        {
            if (!isUpgrading) return;
            isUpgrading = false;
            upgradeWorkDone = 0f;

            if (upgradeDeliveredAmounts != null && parent?.Map != null)
            {
                foreach (var kvp in upgradeDeliveredAmounts)
                {
                    if (kvp.Key != null && kvp.Value > 0)
                    {
                        DropMaterialOut(kvp.Key, kvp.Value);
                    }
                }
                upgradeDeliveredAmounts.Clear();
            }
        }

        public int DepositUpgradeMaterial(Thing thing, int count)
        {
            if (!isUpgrading || thing == null || count <= 0) return 0;
            if (upgradeDeliveredAmounts == null)
                upgradeDeliveredAmounts = new Dictionary<ThingDef, int>();

            ThingDef def = thing.def;
            int missing = GetUpgradeMissing(def);
            int actual = Mathf.Min(count, thing.stackCount, missing);
            if (actual <= 0) return 0;

            if (!upgradeDeliveredAmounts.ContainsKey(def))
                upgradeDeliveredAmounts[def] = 0;

            upgradeDeliveredAmounts[def] += actual;
            thing.SplitOff(actual).Destroy(DestroyMode.Vanish);
            return actual;
        }

        public bool DoUpgradeWork(float workAmount, Pawn worker)
        {
            if (!isUpgrading || !AreUpgradeMaterialsSatisfied) return false;
            upgradeWorkDone += workAmount;
            if (upgradeWorkDone >= UpgradeWorkTotal)
            {
                CompleteUpgrade(worker);
                return true;
            }
            return false;
        }

        public void CompleteUpgrade(Pawn worker = null)
        {
            var next = NextUpgradeDef;
            isUpgrading = false;
            upgradeWorkDone = 0f;
            upgradeDeliveredAmounts?.Clear();
            upgradeLevel++;

            string workerStr = worker != null ? $" by {worker.LabelShort}" : "";
            Messages.Message($"{parent.LabelCap} upgraded to Level {upgradeLevel} ({next?.label ?? "Max"}){workerStr}!", parent, MessageTypeDefOf.PositiveEvent);
        }

        public void EjectInstalledModule()
        {
            if (moduleContainer == null || moduleContainer.Count == 0 || parent?.Map == null) return;
            IntVec3 dropCell = GetOutputCell();
            List<Thing> toEject = moduleContainer.ToList();
            moduleContainer.Clear();
            foreach (Thing t in toEject)
            {
                TryPlaceThingSafe(t, dropCell, parent.Map, ThingPlaceMode.Near, out _);
            }
            ClampAllocationsToMaxStorage();
        }

        public void InstallModuleDirect(Thing moduleItem)
        {
            if (moduleItem == null) return;
            if (HasModule)
            {
                EjectInstalledModule();
            }
            Thing single = moduleItem.stackCount > 1 ? moduleItem.SplitOff(1) : moduleItem;
            if (single.Spawned) single.DeSpawn();
            GetDirectlyHeldThings().TryAdd(single);
            targetModuleDef = null;
        }

        public static List<RecipeDef> AllAmmoRecipes
        {
            get
            {
                if (cachedAmmoRecipes == null)
                {
                    cachedAmmoRecipes = DefDatabase<RecipeDef>.AllDefsListForReading
                        .Where(r => r.products != null &&
                                    r.products.Count > 0 &&
                                    r.products[0].thingDef is AmmoDef &&
                                    r.ingredients != null &&
                                    r.ingredients.Count > 0)
                        .OrderBy(r => r.products[0].thingDef.label)
                        .ToList();
                }
                return cachedAmmoRecipes;
            }
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            powerComp = parent.GetComp<CompPowerTrader>();
            flickComp = parent.GetComp<CompFlickable>();
            breakdownComp = parent.GetComp<CompBreakdownable>();

            if (!respawningAfterLoad)
            {
                uiStepSize = Props.defaultStepSize;
            }
            EnsureMaterialDictionariesInitialized();
        }

        public void EnsureMaterialDictionariesInitialized()
        {
            if (storedAmounts == null) storedAmounts = new Dictionary<ThingDef, int>();
            if (targetAmounts == null) targetAmounts = new Dictionary<ThingDef, int>();

            if (Props?.materials == null) return;
            foreach (var entry in Props.materials)
            {
                if (entry.thingDef == null) continue;
                if (!storedAmounts.ContainsKey(entry.thingDef))
                    storedAmounts[entry.thingDef] = 0;
                if (!targetAmounts.ContainsKey(entry.thingDef))
                    targetAmounts[entry.thingDef] = 0;
            }
        }

        public bool IsPoweredAndActive
        {
            get
            {
                if (parent == null || !parent.Spawned) return false;
                if (flickComp != null && !flickComp.SwitchIsOn) return false;
                if (breakdownComp != null && breakdownComp.BrokenDown) return false;
                if (powerComp != null && !powerComp.PowerOn) return false;
                return true;
            }
        }

        public int GetStored(ThingDef def)
        {
            if (def == null || storedAmounts == null) return 0;
            return storedAmounts.TryGetValue(def, out int val) ? val : 0;
        }

        public int GetTarget(ThingDef def)
        {
            if (def == null || targetAmounts == null) return 0;
            return targetAmounts.TryGetValue(def, out int val) ? val : 0;
        }

        public void SetTarget(ThingDef def, int newTarget)
        {
            if (def == null) return;
            EnsureMaterialDictionariesInitialized();

            int oldTarget = GetTarget(def);
            int otherAllocated = TotalAllocatedStorage - oldTarget;
            int maxAllowedForThis = Mathf.Max(0, MaxTotalStorage - otherAllocated);
            int clamped = Mathf.Clamp(newTarget, 0, maxAllowedForThis);
            targetAmounts[def] = clamped;

            int currentStored = GetStored(def);
            if (currentStored > clamped)
            {
                int excess = currentStored - clamped;
                storedAmounts[def] = clamped;
                DropMaterialOut(def, excess);
            }
        }

        public int DepositMaterial(Thing thing, int count)
        {
            if (thing == null || count <= 0) return 0;
            EnsureMaterialDictionariesInitialized();

            ThingDef def = thing.def;
            int roomInTotal = Mathf.Max(0, MaxTotalStorage - TotalStoredMaterials);
            int actualCount = Mathf.Min(count, thing.stackCount, roomInTotal);
            if (actualCount <= 0) return 0;
            if (!storedAmounts.ContainsKey(def))
                storedAmounts[def] = 0;

            storedAmounts[def] += actualCount;
            thing.SplitOff(actualCount).Destroy(DestroyMode.Vanish);
            return actualCount;
        }

        public IntVec3 GetOutputCell()
        {
            if (parent == null || !parent.Spawned) return IntVec3.Invalid;

            IntVec3 candidate = parent.Position + Props.outputCellOffset.RotatedBy(parent.Rotation);
            if (candidate.InBounds(parent.Map) && candidate.Walkable(parent.Map))
            {
                return candidate;
            }

            if (parent.def.hasInteractionCell)
            {
                IntVec3 interact = parent.InteractionCell;
                if (interact.InBounds(parent.Map) && interact.Walkable(parent.Map))
                    return interact;
            }

            foreach (IntVec3 adj in GenAdj.CellsAdjacent8Way(parent))
            {
                if (adj.InBounds(parent.Map) && adj.Walkable(parent.Map))
                    return adj;
            }

            return parent.Position;
        }

        public void DropMaterialOut(ThingDef def, int totalCount)
        {
            if (def == null || totalCount <= 0 || parent?.Map == null) return;

            IntVec3 dropCell = GetOutputCell();
            int remaining = totalCount;
            int stackLimit = Mathf.Max(1, def.stackLimit);

            while (remaining > 0)
            {
                int batch = Mathf.Min(remaining, stackLimit);
                Thing stack = ThingMaker.MakeThing(def);
                stack.stackCount = batch;
                TryPlaceThingSafe(stack, dropCell, parent.Map, ThingPlaceMode.Near, out _);
                remaining -= batch;
            }
        }

        public static bool TryPlaceThingSafe(Thing thing, IntVec3 center, Map map, ThingPlaceMode mode, out Thing placedThing)
        {
            placedThing = thing;
            if (thing == null || map == null) return false;
            try
            {
                var methods = typeof(GenPlace).GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                foreach (var m in methods)
                {
                    if (m.Name != "TryPlaceThing") continue;
                    var parms = m.GetParameters();
                    if (parms.Length >= 4 &&
                        parms[0].ParameterType == typeof(Thing) &&
                        parms[1].ParameterType == typeof(IntVec3) &&
                        parms[2].ParameterType == typeof(Map) &&
                        parms[3].ParameterType == typeof(ThingPlaceMode))
                    {
                        object[] args = new object[parms.Length];
                        args[0] = thing;
                        args[1] = center;
                        args[2] = map;
                        args[3] = mode;
                        int outIdx = -1;
                        for (int i = 4; i < parms.Length; i++)
                        {
                            if (parms[i].ParameterType.IsByRef)
                            {
                                args[i] = null;
                                outIdx = i;
                            }
                            else if (parms[i].HasDefaultValue)
                            {
                                args[i] = parms[i].DefaultValue;
                            }
                            else if (parms[i].ParameterType.IsValueType)
                            {
                                args[i] = Activator.CreateInstance(parms[i].ParameterType);
                            }
                            else
                            {
                                args[i] = null;
                            }
                        }
                        object res = m.Invoke(null, args);
                        if (outIdx >= 0 && args[outIdx] is Thing pt)
                        {
                            placedThing = pt;
                        }
                        return res is bool b && b;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[AMC] TryPlaceThingSafe fallback to GenSpawn: {ex.Message}");
                placedThing = GenSpawn.Spawn(thing, center, map);
                return placedThing != null;
            }
            return false;
        }

        public ThingDef GetNextNeededHaulMaterial()
        {
            EnsureMaterialDictionariesInitialized();
            if (Props?.materials == null) return null;

            foreach (var entry in Props.materials)
            {
                if (entry.thingDef == null) continue;
                int stored = GetStored(entry.thingDef);
                int target = GetTarget(entry.thingDef);
                if (stored < target)
                {
                    return entry.thingDef;
                }
            }
            return null;
        }

        public IEnumerable<ThingDef> GetAllNeededHaulMaterials()
        {
            EnsureMaterialDictionariesInitialized();
            if (Props?.materials == null) yield break;

            foreach (var entry in Props.materials)
            {
                if (entry.thingDef == null) continue;
                if (GetStored(entry.thingDef) < GetTarget(entry.thingDef))
                {
                    yield return entry.thingDef;
                }
            }
        }

        public CapacitorMaterialEntry GetMaterialEntry(ThingDef def)
        {
            if (def == null || Props?.materials == null) return null;
            for (int i = 0; i < Props.materials.Count; i++)
            {
                if (Props.materials[i].thingDef == def)
                    return Props.materials[i];
            }
            return null;
        }

        /// <summary>
        /// Checks if the capacitor has enough materials (including substitution if enabled) to craft the recipe once.
        /// If consume == true, deducts the required materials from storedAmounts.
        /// </summary>
        public bool TryCheckOrConsumeIngredients(RecipeDef recipe, bool consume, out string missingReason)
        {
            missingReason = null;
            if (recipe == null || recipe.ingredients == null) return false;

            EnsureMaterialDictionariesInitialized();

            // Work on a temporary copy of stored amounts so multi-ingredient checks don't double-spend
            Dictionary<ThingDef, int> workingStored = new Dictionary<ThingDef, int>(storedAmounts);

            foreach (IngredientCount ing in recipe.ingredients)
            {
                ThingDef reqDef = ing.IsFixedIngredient
                    ? ing.FixedIngredient
                    : ing.filter?.AllowedThingDefs?.FirstOrDefault();

                if (reqDef == null) continue;

                int reqCount = Mathf.CeilToInt(ing.GetBaseCount());
                if (reqCount <= 0) continue;

                int availableDirect = workingStored.TryGetValue(reqDef, out int val) ? val : 0;
                int directUse = Mathf.Min(availableDirect, reqCount);
                workingStored[reqDef] = availableDirect - directUse;
                int deficit = reqCount - directUse;

                if (deficit > 0)
                {
                    CapacitorMaterialEntry reqEntry = GetMaterialEntry(reqDef);
                    if (!allowSubstitution || reqEntry == null || !reqEntry.canSubstitute || reqEntry.equivalenceValue <= 0f)
                    {
                        missingReason = $"Missing {deficit}x {reqDef.LabelCap}";
                        return false;
                    }

                    // Needed value in base category units
                    float remainingBaseValueNeeded = deficit * reqEntry.equivalenceValue;

                    // Look for substitutes in the same category that have canSubstitute = true
                    foreach (var subEntry in Props.materials)
                    {
                        if (subEntry.thingDef == null || subEntry.thingDef == reqDef) continue;
                        if (subEntry.category != reqEntry.category || !subEntry.canSubstitute || subEntry.equivalenceValue <= 0f) continue;

                        int subStored = workingStored.TryGetValue(subEntry.thingDef, out int sVal) ? sVal : 0;
                        if (subStored <= 0) continue;

                        int subUnitsNeeded = Mathf.CeilToInt(remainingBaseValueNeeded / subEntry.equivalenceValue);
                        int subUnitsUsed = Mathf.Min(subStored, subUnitsNeeded);

                        workingStored[subEntry.thingDef] = subStored - subUnitsUsed;
                        remainingBaseValueNeeded -= subUnitsUsed * subEntry.equivalenceValue;

                        if (remainingBaseValueNeeded <= 0.001f)
                        {
                            remainingBaseValueNeeded = 0f;
                            break;
                        }
                    }

                    if (remainingBaseValueNeeded > 0.001f)
                    {
                        int stillMissingDirect = Mathf.CeilToInt(remainingBaseValueNeeded / reqEntry.equivalenceValue);
                        missingReason = $"Missing {stillMissingDirect}x {reqDef.LabelCap} (or substitute)";
                        return false;
                    }
                }
            }

            if (consume)
            {
                foreach (var kvp in workingStored)
                {
                    storedAmounts[kvp.Key] = kvp.Value;
                }
            }

            return true;
        }

        public bool ShouldTicketRun(AmmoCraftTicket ticket)
        {
            if (ticket == null || ticket.recipe == null || ticket.paused) return false;

            if (!ticket.isContinuous)
            {
                return ticket.countToCraft > 0;
            }

            ThingDef ammoDef = ticket.ProducedAmmoDef;
            if (ammoDef == null || parent?.Map == null) return false;

            int ownedOnMap = parent.Map.resourceCounter.GetCount(ammoDef);

            if (ticket.autoCraftingActive)
            {
                if (ownedOnMap >= ticket.maxThreshold)
                {
                    ticket.autoCraftingActive = false;
                    return false;
                }
                return true;
            }
            else
            {
                if (ownedOnMap < ticket.minThreshold)
                {
                    ticket.autoCraftingActive = true;
                    return true;
                }
                return false;
            }
        }

        public override void CompTick()
        {
            base.CompTick();
            IsCraftingNow = false;
            ActiveTicket = null;

            if (!IsPoweredAndActive) return;
            // Machine stops autonomous crafting while waiting for upgrade materials or upgrade work
            if (isUpgrading) return;
            if (tickets == null || tickets.Count == 0) return;

            for (int i = 0; i < tickets.Count; i++)
            {
                AmmoCraftTicket ticket = tickets[i];
                if (!ShouldTicketRun(ticket)) continue;

                // Lock / consume ingredients at the start of a batch
                if (!ticket.materialsLocked)
                {
                    if (TryCheckOrConsumeIngredients(ticket.recipe, consume: true, out _))
                    {
                        ticket.materialsLocked = true;
                    }
                    else
                    {
                        // Cannot afford this ticket right now; move to next ticket in queue
                        continue;
                    }
                }

                // Advance work progress on this active ticket (accounting for upgrade level & module multipliers)
                ticket.workProgress += GetEffectiveWorkSpeed(ticket.ProducedAmmoDef);
                IsCraftingNow = true;
                ActiveTicket = ticket;

                // Push heat if building defines heatPerTickWhileWorking
                if (parent.def.building != null && parent.def.building.heatPerTickWhileWorking > 0f && parent.IsHashIntervalTick(60))
                {
                    GenTemperature.PushHeat(parent.Position, parent.Map, parent.def.building.heatPerTickWhileWorking * 60f);
                }

                if (ticket.workProgress >= ticket.TotalWorkAmount)
                {
                    CompleteTicketBatch(ticket);
                    if (!ticket.isContinuous && ticket.countToCraft <= 0)
                    {
                        tickets.RemoveAt(i);
                    }
                }

                // Only craft one active ticket at a time
                break;
            }
        }

        private void CompleteTicketBatch(AmmoCraftTicket ticket)
        {
            ticket.workProgress = 0f;
            ticket.materialsLocked = false;

            if (ticket.recipe?.products != null)
            {
                foreach (var prod in ticket.recipe.products)
                {
                    if (prod.thingDef != null && prod.count > 0)
                    {
                        DropMaterialOut(prod.thingDef, prod.count);
                        totalAmmoCrafted += prod.count;
                    }
                }
            }

            // Material Rebate: roll CurrentRebateChance on each completed batch; if triggered, return CurrentRebatePercent of recipe ingredient costs
            float rebateChance = CurrentRebateChance;
            float rebatePct = CurrentRebatePercent;
            if (rebateChance > 0f && rebatePct > 0f && ticket.recipe?.ingredients != null && Rand.Value < rebateChance)
            {
                EnsureMaterialDictionariesInitialized();
                foreach (var ing in ticket.recipe.ingredients)
                {
                    ThingDef ingDef = ing.IsFixedIngredient
                        ? ing.FixedIngredient
                        : ing.filter?.AllowedThingDefs?.FirstOrDefault();
                    if (ingDef == null) continue;

                    float exactRebate = ing.GetBaseCount() * rebatePct;
                    int guaranteed = Mathf.FloorToInt(exactRebate);
                    float fractionalChance = exactRebate - guaranteed;
                    int totalRefund = guaranteed + (Rand.Value < fractionalChance ? 1 : 0);

                    if (totalRefund > 0)
                    {
                        if (!storedAmounts.ContainsKey(ingDef))
                            storedAmounts[ingDef] = 0;
                        int room = Mathf.Max(0, MaxTotalStorage - TotalStoredMaterials);
                        int toStore = Mathf.Min(totalRefund, room);
                        if (toStore > 0)
                            storedAmounts[ingDef] += toStore;
                    }
                }
            }

            if (!ticket.isContinuous)
            {
                ticket.countToCraft = Mathf.Max(0, ticket.countToCraft - 1);
            }
            else
            {
                // Re-check map count immediately after spawning
                ThingDef ammoDef = ticket.ProducedAmmoDef;
                if (ammoDef != null && parent?.Map != null)
                {
                    int owned = parent.Map.resourceCounter.GetCount(ammoDef);
                    if (owned >= ticket.maxThreshold)
                    {
                        ticket.autoCraftingActive = false;
                    }
                }
            }
        }

        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            base.PostDestroy(mode, previousMap);
            if (previousMap != null)
            {
                if (moduleContainer != null && moduleContainer.Count > 0)
                {
                    List<Thing> mods = moduleContainer.ToList();
                    moduleContainer.Clear();
                    foreach (Thing m in mods)
                    {
                        TryPlaceThingSafe(m, parent.Position, previousMap, ThingPlaceMode.Near, out _);
                    }
                }

                if (storedAmounts != null)
                {
                    foreach (var kvp in storedAmounts)
                    {
                        if (kvp.Key == null || kvp.Value <= 0) continue;
                        int remaining = kvp.Value;
                        int stackLimit = Mathf.Max(1, kvp.Key.stackLimit);
                        while (remaining > 0)
                        {
                            int batch = Mathf.Min(remaining, stackLimit);
                            Thing stack = ThingMaker.MakeThing(kvp.Key);
                            stack.stackCount = batch;
                            TryPlaceThingSafe(stack, parent.Position, previousMap, ThingPlaceMode.Near, out _);
                            remaining -= batch;
                        }
                    }
                    storedAmounts.Clear();
                }

                if (upgradeDeliveredAmounts != null)
                {
                    foreach (var kvp in upgradeDeliveredAmounts)
                    {
                        if (kvp.Key == null || kvp.Value <= 0) continue;
                        int remaining = kvp.Value;
                        int stackLimit = Mathf.Max(1, kvp.Key.stackLimit);
                        while (remaining > 0)
                        {
                            int batch = Mathf.Min(remaining, stackLimit);
                            Thing stack = ThingMaker.MakeThing(kvp.Key);
                            stack.stackCount = batch;
                            TryPlaceThingSafe(stack, parent.Position, previousMap, ThingPlaceMode.Near, out _);
                            remaining -= batch;
                        }
                    }
                    upgradeDeliveredAmounts.Clear();
                }
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Collections.Look(ref storedAmounts, "storedAmounts", LookMode.Def, LookMode.Value);
            Scribe_Collections.Look(ref targetAmounts, "targetAmounts", LookMode.Def, LookMode.Value);
            Scribe_Collections.Look(ref tickets, "tickets", LookMode.Deep);
            Scribe_Values.Look(ref allowSubstitution, "allowSubstitution", false);
            Scribe_Values.Look(ref uiStepSize, "uiStepSize", 75);
            Scribe_Values.Look(ref upgradeLevel, "upgradeLevel", 0);
            Scribe_Values.Look(ref isUpgrading, "isUpgrading", false);
            Scribe_Values.Look(ref totalAmmoCrafted, "totalAmmoCrafted", 0);
            Scribe_Collections.Look(ref upgradeDeliveredAmounts, "upgradeDeliveredAmounts", LookMode.Def, LookMode.Value);
            Scribe_Values.Look(ref upgradeWorkDone, "upgradeWorkDone", 0f);
            Scribe_Deep.Look(ref moduleContainer, "moduleContainer", this);
            Scribe_Defs.Look(ref targetModuleDef, "targetModuleDef");

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (tickets == null) tickets = new List<AmmoCraftTicket>();
                if (upgradeDeliveredAmounts == null) upgradeDeliveredAmounts = new Dictionary<ThingDef, int>();
                if (moduleContainer == null) moduleContainer = new ThingOwner<Thing>(this);
                EnsureMaterialDictionariesInitialized();
            }
        }

        public override string CompInspectStringExtra()
        {
            EnsureMaterialDictionariesInitialized();
            string status;

            if (isUpgrading)
            {
                if (!AreUpgradeMaterialsSatisfied)
                {
                    var missingList = GetMissingUpgradeMaterials().Select(c => $"{c.count}x {c.thingDef.LabelCap}");
                    status = $"Status: Upgrading to Lv.{upgradeLevel + 1} (Awaiting delivery: {string.Join(", ", missingList)})";
                }
                else
                {
                    float upgPct = Mathf.Clamp01(upgradeWorkDone / UpgradeWorkTotal) * 100f;
                    status = $"Status: Upgrading to Lv.{upgradeLevel + 1} (Requires Crafter Skill {UpgradeMinCraftingSkill}+ | Work: {upgPct:F0}%)";
                }
            }
            else if (!IsPoweredAndActive)
            {
                status = "Status: Offline (Unpowered or Switched Off)";
            }
            else
            {
                AmmoCraftTicket active = tickets?.FirstOrDefault(t => ShouldTicketRun(t) && (t.materialsLocked || TryCheckOrConsumeIngredients(t.recipe, false, out _)));
                if (active != null)
                {
                    float pct = (active.workProgress / active.TotalWorkAmount) * 100f;
                    float effSpeed = GetEffectiveWorkSpeed(active.ProducedAmmoDef);
                    status = $"Crafting: {active.ProducedAmmoDef?.LabelCap ?? active.recipe.LabelCap} ({pct:F0}% | Speed: {effSpeed:0.#}x)";
                }
                else if (tickets != null && tickets.Any(t => ShouldTicketRun(t)))
                {
                    status = "Status: Waiting for capacitor materials";
                }
                else
                {
                    status = "Status: Idle";
                }
            }

            string subStr = allowSubstitution ? "Enabled" : "Disabled";
            string upgStr = upgradeLevel > 0
                ? $"Lv.{upgradeLevel} (+{(CurrentUpgradeSpeedMultiplier - 1f) * 100f:F0}% Spd, {CurrentRebateChance * 100f:F0}% chance for {CurrentRebatePercent * 100f:F0}% Rebate)"
                : "Lv.0 (Base)";
            string modStr = HasModule
                ? InstalledModule.def.LabelCap.ToString()
                : (targetModuleDef != null ? $"Awaiting {targetModuleDef.LabelCap}" : "None");

            return $"{status}\nActive Tickets: {tickets?.Count ?? 0} | Substitution: {subStr}\nUpgrade: {upgStr} | Module: {modStr}";
        }
    }
}
