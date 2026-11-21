using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace AbsolutelyMoreCannons
{
    /// <summary>
    /// Drives situational AI "thought" bubbles on the Ammo Fabricator using XML-defined
    /// <see cref="FabricatorTopicDef"/> glossaries and dynamic situational weighting.
    /// </summary>
    public class CompFabricatorThoughts : ThingComp
    {
        public CompProperties_FabricatorThoughts Props => (CompProperties_FabricatorThoughts)props;

        private CompPowerTrader powerComp;
        private CompFlickable flickComp;
        private CompBreakdownable breakdownComp;
        private CompAmmoCapacitor ammoCapacitorComp;
        private CompFabricatorEye eyeComp;

        private int nextThoughtTicks = -1;
        private int lastBubbleTick = -99999;

        private bool wasPowered = true;
        private bool wasBrokenDown = false;
        private bool wasUpgrading = false;
        private bool cachedHostilesPresent = false;
        private bool cachedMultipleFabricators = false;
        private string cachedNearbyCorpseLabel = null;
        private bool cachedNearbyCorpseIsColonist = false;
        private string cachedNearbyTurretLabel = null;
        private bool cachedTurretFiringNearby = false;
        private float cachedColonyWealth = 0f;
        private int lastMapCheckTick = -99999;

        private int lastCraftTick = 0;
        private int lastKnownTotalCrafted = 0;
        private readonly HashSet<int> knownColonistIds = new HashSet<int>();
        private string pendingNewColonistName = null;

        private readonly Dictionary<string, int> topicLastUsedTick = new Dictionary<string, int>();
        private readonly Dictionary<string, int> lineLastUsedTick = new Dictionary<string, int>();

        private struct SituationContext
        {
            public HashSet<FabricatorSituationTag> activeTags;
            public bool isPoweredAndActive;
            public Pawn trackedPawn;
            public string activeAmmoLabel;
            public string installedModuleLabel;
            public int upgradeLevel;
            public string upgradeLabel;
            public float storageFillRatio;
            public bool hostilesPresent;
            public string nearbyCorpseLabel;
            public float colonyWealth;
            public string newColonistLabel;
            public string trackedWeaponLabel;
            public int trackedCraftSkill;
            public int trackedMoodPct;
            public string weatherLabel;
            public float ambientTempC;
            public int hourOfDay;
            public int totalCrafted;
            public int queueCount;
            public string nearbyTurretLabel;
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            powerComp = parent.GetComp<CompPowerTrader>();
            flickComp = parent.GetComp<CompFlickable>();
            breakdownComp = parent.GetComp<CompBreakdownable>();
            ammoCapacitorComp = parent.GetComp<CompAmmoCapacitor>();
            eyeComp = parent.GetComp<CompFabricatorEye>();

            wasPowered = powerComp == null || powerComp.PowerOn;
            wasBrokenDown = breakdownComp != null && breakdownComp.BrokenDown;
            wasUpgrading = ammoCapacitorComp != null && ammoCapacitorComp.isUpgrading;
            lastCraftTick = Find.TickManager.TicksGame;
            lastKnownTotalCrafted = ammoCapacitorComp?.totalAmmoCrafted ?? 0;

            knownColonistIds.Clear();
            if (parent.Map?.mapPawns != null)
            {
                foreach (Pawn p in parent.Map.mapPawns.FreeColonists)
                {
                    if (p != null) knownColonistIds.Add(p.thingIDNumber);
                }
            }

            if (nextThoughtTicks < 0)
            {
                ScheduleNextThought();
            }
        }

        public override void CompTick()
        {
            base.CompTick();
            if (!parent.Spawned || parent.Map == null) return;
            if (!(TurretBarrelAnimationMod.settings?.enableFabricatorBubbles ?? true)) return;

            CheckStateTransitions();

            if (nextThoughtTicks > 0)
            {
                nextThoughtTicks--;
                return;
            }

            ScheduleNextThought();
            TryEmitThought(eventTagFilter: null, ignoreGlobalCooldown: false);
        }

        private void CheckStateTransitions()
        {
            bool poweredNow = powerComp == null || powerComp.PowerOn;
            if (poweredNow != wasPowered)
            {
                wasPowered = poweredNow;
                if (!poweredNow)
                {
                    TryEmitThought("PowerLost", ignoreGlobalCooldown: false);
                }
                else
                {
                    TryEmitThought("PowerRestored", ignoreGlobalCooldown: false);
                }
            }

            bool brokenNow = breakdownComp != null && breakdownComp.BrokenDown;
            if (brokenNow && !wasBrokenDown)
            {
                TryEmitThought("BrokenDown", ignoreGlobalCooldown: false);
            }
            wasBrokenDown = brokenNow;

            bool upgradingNow = ammoCapacitorComp != null && ammoCapacitorComp.isUpgrading;
            if (wasUpgrading && !upgradingNow)
            {
                TryEmitThought("UpgradeComplete", ignoreGlobalCooldown: false);
            }
            wasUpgrading = upgradingNow;

            if (ammoCapacitorComp != null)
            {
                int currentTotal = ammoCapacitorComp.totalAmmoCrafted;
                if (currentTotal > lastKnownTotalCrafted)
                {
                    int step = Mathf.Max(1, Props.productionMilestoneStep);
                    if (currentTotal / step > lastKnownTotalCrafted / step)
                    {
                        TryEmitThought("MilestoneReached", ignoreGlobalCooldown: false);
                    }
                    lastKnownTotalCrafted = currentTotal;
                }
            }
        }

        private void ScheduleNextThought()
        {
            float freqMult = Mathf.Clamp(
                TurretBarrelAnimationMod.settings?.fabricatorBubbleFrequencyMultiplier ?? 1f,
                0.25f,
                3.0f);

            int minTicks = Mathf.Max(120, Mathf.RoundToInt(Props.minIntervalTicks / freqMult));
            int maxTicks = Mathf.Max(minTicks, Mathf.RoundToInt(Props.maxIntervalTicks / freqMult));
            nextThoughtTicks = Rand.RangeInclusive(minTicks, maxTicks);
        }

        /// <summary>
        /// Evaluates situational weights and attempts to emit a thought bubble.
        /// Can also be invoked externally for specific event tags.
        /// </summary>
        public bool TryEmitThought(string eventTagFilter = null, bool ignoreGlobalCooldown = false, bool logDebugWeights = false)
        {
            if (!parent.Spawned || parent.Map == null) return false;
            if (!(TurretBarrelAnimationMod.settings?.enableFabricatorBubbles ?? true)) return false;

            int now = Find.TickManager.TicksGame;
            if (!ignoreGlobalCooldown && now - lastBubbleTick < Props.eventCooldownTicks)
            {
                return false;
            }

            SituationContext ctx = BuildSituationContext(now);
            List<FabricatorTopicDef> allTopics = DefDatabase<FabricatorTopicDef>.AllDefsListForReading;
            if (allTopics == null || allTopics.Count == 0) return false;

            var candidates = new List<(FabricatorTopicDef topic, float weight)>();
            float totalWeight = 0f;

            for (int i = 0; i < allTopics.Count; i++)
            {
                FabricatorTopicDef topic = allTopics[i];
                if (topic == null || topic.lines == null || topic.lines.Count == 0) continue;

                if (!string.IsNullOrEmpty(eventTagFilter))
                {
                    if (!string.Equals(topic.eventTag, eventTagFilter, StringComparison.OrdinalIgnoreCase))
                        continue;
                }
                else if (!string.IsNullOrEmpty(topic.eventTag))
                {
                    // Event-only topics are reserved for explicit state transitions
                    continue;
                }

                float weight = ComputeTopicWeight(topic, ctx, now, ignoreCooldown: !string.IsNullOrEmpty(eventTagFilter));
                if (weight <= 0f) continue;

                candidates.Add((topic, weight));
                totalWeight += weight;
            }

            if (logDebugWeights)
            {
                LogCandidateWeights(candidates, ctx);
            }

            if (candidates.Count == 0 || totalWeight <= 0f) return false;

            FabricatorTopicDef chosenTopic = PickWeightedTopic(candidates, totalWeight);
            if (chosenTopic == null) return false;

            if (!TryPickWeightedLine(chosenTopic, ctx, now, out FabricatorTopicLine chosenLine, out int lineIdx))
            {
                return false;
            }

            string formatted = FormatLine(chosenLine.text, ctx);
            if (string.IsNullOrWhiteSpace(formatted)) return false;

            MapComponent_FabricatorBubbles manager = parent.Map.GetComponent<MapComponent_FabricatorBubbles>();
            if (manager == null) return false;

            Vector3 anchorWorld = GetBubbleAnchorWorldPos();
            manager.AddBubble(parent, anchorWorld, formatted, Props.bubbleDurationTicks);

            lastBubbleTick = now;
            topicLastUsedTick[chosenTopic.defName] = now;
            lineLastUsedTick[$"{chosenTopic.defName}:{lineIdx}"] = now;
            return true;
        }

        private SituationContext BuildSituationContext(int now)
        {
            var tags = new HashSet<FabricatorSituationTag>();

            bool switchedOff = flickComp != null && !flickComp.SwitchIsOn;
            bool brokenDown = breakdownComp != null && breakdownComp.BrokenDown;
            bool powered = powerComp == null || powerComp.PowerOn;
            bool active = !switchedOff && !brokenDown && powered;

            if (switchedOff) tags.Add(FabricatorSituationTag.SwitchedOff);
            if (brokenDown) tags.Add(FabricatorSituationTag.BrokenDown);
            if (!powered) tags.Add(FabricatorSituationTag.LowPower);

            bool crafting = active && ammoCapacitorComp != null && ammoCapacitorComp.IsCraftingNow;
            if (crafting)
            {
                tags.Add(FabricatorSituationTag.Crafting);
                lastCraftTick = now;
            }
            else if (active)
            {
                tags.Add(FabricatorSituationTag.Idle);
                if (ammoCapacitorComp != null && ammoCapacitorComp.isUpgrading)
                {
                    lastCraftTick = now;
                }
                else if (now - lastCraftTick >= Props.idleTooLongTicks)
                {
                    tags.Add(FabricatorSituationTag.IdleTooLong);
                }
            }

            float fillRatio = 0f;
            int totalCrafted = 0;
            int queueCount = 0;
            if (ammoCapacitorComp != null && ammoCapacitorComp.MaxTotalStorage > 0)
            {
                fillRatio = (float)ammoCapacitorComp.TotalStoredMaterials / ammoCapacitorComp.MaxTotalStorage;
                if (fillRatio <= Props.lowMaterialRatio)
                    tags.Add(FabricatorSituationTag.LowMaterials);
                if (fillRatio >= Props.fullStorageRatio)
                    tags.Add(FabricatorSituationTag.FullStorage);
                if (ammoCapacitorComp.isUpgrading)
                    tags.Add(FabricatorSituationTag.Upgrading);
                if (ammoCapacitorComp.HasModule)
                    tags.Add(FabricatorSituationTag.HasModule);

                totalCrafted = ammoCapacitorComp.totalAmmoCrafted;
                queueCount = ammoCapacitorComp.tickets?.Count ?? 0;
                if (totalCrafted >= Props.highProductionThreshold)
                    tags.Add(FabricatorSituationTag.HighProduction);
            }

            Pawn tracked = (active && eyeComp != null) ? eyeComp.TrackedPawn : null;
            string trackedWeaponLabel = null;
            int trackedCraftSkill = 0;
            int trackedMoodPct = 100;
            if (tracked != null)
            {
                tags.Add(FabricatorSituationTag.TrackingPawn);
                if (tracked.IsPrisoner)
                {
                    tags.Add(FabricatorSituationTag.TrackedPawnIsPrisoner);
                }
                if (IsPawnMissingArm(tracked))
                {
                    tags.Add(FabricatorSituationTag.TrackedPawnMissingArm);
                }
                if (tracked.equipment?.Primary != null)
                {
                    tags.Add(FabricatorSituationTag.TrackedPawnArmed);
                    trackedWeaponLabel = tracked.equipment.Primary.LabelShort;
                }
                if (tracked.health?.hediffSet != null &&
                    (tracked.health.summaryHealth.SummaryHealthPercent < 0.9f || tracked.health.hediffSet.BleedRateTotal > 0.01f))
                {
                    tags.Add(FabricatorSituationTag.TrackedPawnInjured);
                }
                if (tracked.InMentalState)
                {
                    tags.Add(FabricatorSituationTag.TrackedPawnMentalBreak);
                }
                if (tracked.Drafted)
                {
                    tags.Add(FabricatorSituationTag.TrackedPawnDrafted);
                }
                if (tracked.skills != null)
                {
                    trackedCraftSkill = tracked.skills.GetSkill(SkillDefOf.Crafting)?.Level ?? 0;
                }
                if (tracked.needs?.mood != null)
                {
                    trackedMoodPct = Mathf.RoundToInt(tracked.needs.mood.CurLevelPercentage * 100f);
                }
            }

            if (now - lastMapCheckTick >= Props.hostileCheckIntervalTicks)
            {
                lastMapCheckTick = now;
                RefreshMapScanCache();
            }
            if (cachedHostilesPresent)
            {
                tags.Add(FabricatorSituationTag.HostilesPresent);
            }
            if (cachedMultipleFabricators)
            {
                tags.Add(FabricatorSituationTag.MultipleFabricators);
            }
            if (!string.IsNullOrEmpty(cachedNearbyCorpseLabel))
            {
                tags.Add(FabricatorSituationTag.CorpseNearby);
                if (cachedNearbyCorpseIsColonist)
                    tags.Add(FabricatorSituationTag.ColonistCorpseNearby);
            }
            if (!string.IsNullOrEmpty(cachedNearbyTurretLabel))
            {
                tags.Add(FabricatorSituationTag.NearTurret);
                if (cachedTurretFiringNearby)
                    tags.Add(FabricatorSituationTag.TurretFiringNearby);
            }
            if (cachedColonyWealth >= Props.highWealthThreshold)
            {
                tags.Add(FabricatorSituationTag.HighWealth);
            }
            else if (cachedColonyWealth <= Props.lowWealthThreshold)
            {
                tags.Add(FabricatorSituationTag.LowWealth);
            }

            WeatherDef curWeather = parent.Map.weatherManager?.curWeather;
            string weatherLabel = curWeather?.label ?? "weather";
            if (curWeather != null && (curWeather.rainRate > 0.01f || curWeather.snowRate > 0.01f || curWeather.windSpeedFactor > 1.5f))
            {
                tags.Add(FabricatorSituationTag.BadWeather);
            }
            if (parent.Map.gameConditionManager != null && parent.Map.gameConditionManager.ConditionIsActive(GameConditionDefOf.ToxicFallout))
            {
                tags.Add(FabricatorSituationTag.ToxicFallout);
            }
            float ambientTemp = parent.AmbientTemperature;
            if (ambientTemp <= Props.extremeColdTempC)
            {
                tags.Add(FabricatorSituationTag.ExtremeCold);
            }
            else if (ambientTemp >= Props.extremeHeatTempC)
            {
                tags.Add(FabricatorSituationTag.ExtremeHeat);
            }
            int hour = GenLocalDate.HourInteger(parent.Map);
            if (hour >= 22 || hour < 5)
            {
                tags.Add(FabricatorSituationTag.NightTime);
            }

            string ammoLabel = ammoCapacitorComp?.ActiveTicket?.ProducedAmmoDef?.label ?? "ammunition";
            string moduleLabel = ammoCapacitorComp?.InstalledModule?.LabelShort ?? "module";
            int upgLevel = ammoCapacitorComp?.upgradeLevel ?? 0;
            string upgLabel = ammoCapacitorComp?.CurrentUpgradeDef?.label ?? $"Tier {upgLevel}";

            return new SituationContext
            {
                activeTags = tags,
                isPoweredAndActive = active,
                trackedPawn = tracked,
                activeAmmoLabel = ammoLabel,
                installedModuleLabel = moduleLabel,
                upgradeLevel = upgLevel,
                upgradeLabel = upgLabel,
                storageFillRatio = fillRatio,
                hostilesPresent = cachedHostilesPresent,
                nearbyCorpseLabel = cachedNearbyCorpseLabel,
                colonyWealth = cachedColonyWealth,
                newColonistLabel = pendingNewColonistName ?? "rookie",
                trackedWeaponLabel = trackedWeaponLabel,
                trackedCraftSkill = trackedCraftSkill,
                trackedMoodPct = trackedMoodPct,
                weatherLabel = weatherLabel,
                ambientTempC = ambientTemp,
                hourOfDay = hour,
                totalCrafted = totalCrafted,
                queueCount = queueCount,
                nearbyTurretLabel = cachedNearbyTurretLabel
            };
        }

        private float ComputeTopicWeight(FabricatorTopicDef topic, SituationContext ctx, int now, bool ignoreCooldown)
        {
            if (!ctx.isPoweredAndActive && !topic.allowWhenUnpowered)
                return 0f;

            if (!ignoreCooldown && topic.cooldownTicks > 0 && topicLastUsedTick.TryGetValue(topic.defName, out int lastTick))
            {
                if (now - lastTick < topic.cooldownTicks)
                    return 0f;
            }

            if (topic.requiredTags != null)
            {
                for (int i = 0; i < topic.requiredTags.Count; i++)
                {
                    if (!ctx.activeTags.Contains(topic.requiredTags[i]))
                        return 0f;
                }
            }

            if (topic.forbiddenTags != null)
            {
                for (int i = 0; i < topic.forbiddenTags.Count; i++)
                {
                    if (ctx.activeTags.Contains(topic.forbiddenTags[i]))
                        return 0f;
                }
            }

            if (!HasEligibleLine(topic, ctx))
                return 0f;

            float weight = Mathf.Max(0f, topic.baseWeight);
            if (weight <= 0f) return 0f;

            // Boost weight for matching situational tags
            if (topic.boostedTags != null && topic.boostMultiplier > 0f)
            {
                for (int i = 0; i < topic.boostedTags.Count; i++)
                {
                    if (ctx.activeTags.Contains(topic.boostedTags[i]))
                    {
                        weight *= topic.boostMultiplier;
                    }
                }
            }

            // Dynamic situational pressure adjustments
            if (ctx.activeTags.Contains(FabricatorSituationTag.LowMaterials) &&
                topic.requiredTags.Contains(FabricatorSituationTag.LowMaterials) &&
                ctx.storageFillRatio < 0.05f)
            {
                weight *= 1.5f;
            }

            if (ctx.trackedPawn != null &&
                topic.boostedTags.Contains(FabricatorSituationTag.TrackingPawn) &&
                ctx.trackedPawn.skills != null)
            {
                int craftSkill = ctx.trackedPawn.skills.GetSkill(SkillDefOf.Crafting)?.Level ?? 0;
                if (craftSkill >= 12 || craftSkill <= 3)
                {
                    weight *= 1.25f;
                }
            }

            // Recency decay to prevent repeating the same topic back-to-back
            if (topic.cooldownTicks > 0 && topicLastUsedTick.TryGetValue(topic.defName, out int prevTick))
            {
                int elapsed = now - prevTick;
                if (elapsed < topic.cooldownTicks * 3)
                {
                    weight *= Mathf.Clamp(topic.recencyPenalty, 0.05f, 1f);
                }
            }

            return weight;
        }

        private static bool IsLineEligible(FabricatorTopicLine line, SituationContext ctx)
        {
            if (line == null || string.IsNullOrEmpty(line.text) || line.weight <= 0f) return false;
            if (line.requiresTrackedPawn && ctx.trackedPawn == null) return false;
            if (line.requiresCrafting && !ctx.activeTags.Contains(FabricatorSituationTag.Crafting)) return false;
            if (line.requiresModule && !ctx.activeTags.Contains(FabricatorSituationTag.HasModule)) return false;
            if (line.requiresCorpse && string.IsNullOrEmpty(ctx.nearbyCorpseLabel)) return false;
            if (line.requiresTurret && string.IsNullOrEmpty(ctx.nearbyTurretLabel)) return false;
            if (line.requiresTrackedPawnWeapon && string.IsNullOrEmpty(ctx.trackedWeaponLabel)) return false;
            if (ctx.upgradeLevel < line.minUpgradeLevel) return false;
            return true;
        }

        private static bool HasEligibleLine(FabricatorTopicDef topic, SituationContext ctx)
        {
            for (int i = 0; i < topic.lines.Count; i++)
            {
                if (IsLineEligible(topic.lines[i], ctx))
                    return true;
            }
            return false;
        }

        private static FabricatorTopicDef PickWeightedTopic(List<(FabricatorTopicDef topic, float weight)> candidates, float totalWeight)
        {
            float roll = Rand.Range(0f, totalWeight);
            float cumulative = 0f;
            for (int i = 0; i < candidates.Count; i++)
            {
                cumulative += candidates[i].weight;
                if (roll <= cumulative)
                    return candidates[i].topic;
            }
            return candidates[candidates.Count - 1].topic;
        }

        private bool TryPickWeightedLine(FabricatorTopicDef topic, SituationContext ctx, int now, out FabricatorTopicLine chosen, out int chosenIdx)
        {
            chosen = null;
            chosenIdx = -1;

            var eligible = new List<(FabricatorTopicLine line, int idx, float weight)>();
            float total = 0f;

            for (int i = 0; i < topic.lines.Count; i++)
            {
                FabricatorTopicLine line = topic.lines[i];
                if (!IsLineEligible(line, ctx)) continue;

                float w = line.weight;
                string key = $"{topic.defName}:{i}";
                if (lineLastUsedTick.TryGetValue(key, out int lastTick) && now - lastTick < 7200)
                {
                    w *= 0.2f;
                }

                eligible.Add((line, i, w));
                total += w;
            }

            if (eligible.Count == 0 || total <= 0f) return false;

            float roll = Rand.Range(0f, total);
            float cumulative = 0f;
            for (int i = 0; i < eligible.Count; i++)
            {
                cumulative += eligible[i].weight;
                if (roll <= cumulative)
                {
                    chosen = eligible[i].line;
                    chosenIdx = eligible[i].idx;
                    return true;
                }
            }

            chosen = eligible[eligible.Count - 1].line;
            chosenIdx = eligible[eligible.Count - 1].idx;
            return true;
        }

        private static string FormatLine(string template, SituationContext ctx)
        {
            if (string.IsNullOrEmpty(template)) return string.Empty;

            string result = template;
            if (result.Contains("{PAWN}"))
            {
                string pawnName = ctx.trackedPawn != null ? ctx.trackedPawn.LabelShort : "colonist";
                result = result.Replace("{PAWN}", pawnName);
            }
            if (result.Contains("{AMMO}"))
            {
                result = result.Replace("{AMMO}", ctx.activeAmmoLabel);
            }
            if (result.Contains("{MODULE}"))
            {
                result = result.Replace("{MODULE}", ctx.installedModuleLabel);
            }
            if (result.Contains("{UPGRADE}"))
            {
                result = result.Replace("{UPGRADE}", ctx.upgradeLabel);
            }
            if (result.Contains("{CORPSE}"))
            {
                result = result.Replace("{CORPSE}", ctx.nearbyCorpseLabel ?? "that body");
            }
            if (result.Contains("{WEALTH}"))
            {
                result = result.Replace("{WEALTH}", ctx.colonyWealth.ToString("N0"));
            }
            if (result.Contains("{NEW_COLONIST}"))
            {
                result = result.Replace("{NEW_COLONIST}", ctx.newColonistLabel ?? "rookie");
            }
            if (result.Contains("{WEAPON}"))
            {
                result = result.Replace("{WEAPON}", ctx.trackedWeaponLabel ?? "weapon");
            }
            if (result.Contains("{CRAFT_SKILL}"))
            {
                result = result.Replace("{CRAFT_SKILL}", ctx.trackedCraftSkill.ToString());
            }
            if (result.Contains("{MOOD}"))
            {
                result = result.Replace("{MOOD}", $"{ctx.trackedMoodPct}%");
            }
            if (result.Contains("{WEATHER}"))
            {
                result = result.Replace("{WEATHER}", ctx.weatherLabel ?? "weather");
            }
            if (result.Contains("{TEMP}"))
            {
                result = result.Replace("{TEMP}", $"{Mathf.RoundToInt(ctx.ambientTempC)}°C");
            }
            if (result.Contains("{HOUR}"))
            {
                result = result.Replace("{HOUR}", $"{ctx.hourOfDay:00}:00");
            }
            if (result.Contains("{TOTAL_CRAFTED}"))
            {
                result = result.Replace("{TOTAL_CRAFTED}", ctx.totalCrafted.ToString("N0"));
            }
            if (result.Contains("{QUEUE_COUNT}"))
            {
                result = result.Replace("{QUEUE_COUNT}", ctx.queueCount.ToString());
            }
            if (result.Contains("{STORAGE_PCT}"))
            {
                result = result.Replace("{STORAGE_PCT}", $"{Mathf.RoundToInt(ctx.storageFillRatio * 100f)}%");
            }
            if (result.Contains("{TURRET}"))
            {
                result = result.Replace("{TURRET}", ctx.nearbyTurretLabel ?? "turret");
            }
            return result;
        }

        private void RefreshMapScanCache()
        {
            Map map = parent.Map;
            if (map == null) return;

            cachedHostilesPresent = GenHostility.AnyHostileActiveThreatToPlayer(map);
            cachedMultipleFabricators = HasMultipleFabricatorsOnMap(map, parent.def);
            cachedColonyWealth = map.wealthWatcher?.WealthTotal ?? 0f;

            cachedNearbyCorpseLabel = null;
            cachedNearbyCorpseIsColonist = false;
            cachedNearbyTurretLabel = null;
            cachedTurretFiringNearby = false;

            int corpsePriority = 0; // 1 = animal, 2 = humanlike, 3 = colonist
            foreach (Thing t in GenRadial.RadialDistinctThingsAround(parent.Position, map, Props.nearbyScanRadius, true))
            {
                if (t == null || t == parent || !t.Spawned) continue;

                if (t is Corpse corpse)
                {
                    Pawn inner = corpse.InnerPawn;
                    int prio = 1;
                    if (inner != null && inner.IsColonist) prio = 3;
                    else if (inner != null && inner.RaceProps != null && inner.RaceProps.Humanlike) prio = 2;

                    if (prio > corpsePriority)
                    {
                        corpsePriority = prio;
                        cachedNearbyCorpseLabel = inner != null ? inner.LabelShort : corpse.LabelShort;
                        cachedNearbyCorpseIsColonist = prio == 3;
                    }
                }
                else if (t is Building_Turret turret)
                {
                    bool firing = turret.CurrentTarget.IsValid;
                    if (cachedNearbyTurretLabel == null || (firing && !cachedTurretFiringNearby))
                    {
                        cachedNearbyTurretLabel = turret.LabelShort;
                        cachedTurretFiringNearby = firing;
                    }
                }
            }

            if (map.mapPawns != null)
            {
                foreach (Pawn p in map.mapPawns.FreeColonists)
                {
                    if (p != null && knownColonistIds.Add(p.thingIDNumber))
                    {
                        pendingNewColonistName = p.LabelShort;
                        TryEmitThought("NewColonist", ignoreGlobalCooldown: false);
                        break;
                    }
                }
            }
        }

        private Vector3 GetBubbleAnchorWorldPos()
        {
            Vector3 basePos = eyeComp != null ? eyeComp.EyeWorldPos : parent.DrawPos;
            return basePos + Props.bubbleWorldOffset;
        }

        private static bool IsPawnMissingArm(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null) return false;
            var missingParts = pawn.health.hediffSet.GetMissingPartsCommonAncestors();
            if (missingParts == null) return false;

            for (int i = 0; i < missingParts.Count; i++)
            {
                BodyPartDef partDef = missingParts[i]?.Part?.def;
                if (partDef == BodyPartDefOf.Arm || partDef == BodyPartDefOf.Shoulder)
                {
                    return true;
                }
            }
            return false;
        }

        private static bool HasMultipleFabricatorsOnMap(Map map, ThingDef fabricatorDef)
        {
            if (map?.listerBuildings == null || fabricatorDef == null) return false;
            int count = 0;
            foreach (Building b in map.listerBuildings.AllBuildingsColonistOfDef(fabricatorDef))
            {
                if (b != null && b.Spawned)
                {
                    count++;
                    if (count > 1) return true;
                }
            }
            return false;
        }

        private static void LogCandidateWeights(List<(FabricatorTopicDef topic, float weight)> candidates, SituationContext ctx)
        {
            string tagsStr = string.Join(", ", ctx.activeTags);
            Log.Message($"[AMC][FabricatorThoughts] Active tags: [{tagsStr}] | Candidates: {candidates.Count}");
            for (int i = 0; i < candidates.Count; i++)
            {
                Log.Message($"  - {candidates[i].topic.defName}: {candidates[i].weight:F2}");
            }
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo g in base.CompGetGizmosExtra())
            {
                yield return g;
            }

            if (Prefs.DevMode)
            {
                yield return new Command_Action
                {
                    defaultLabel = "Dev: Trigger AI Thought",
                    defaultDesc = "Forces the Ammo Fabricator to evaluate situational weights and emit a thought bubble.",
                    action = () =>
                    {
                        bool emitted = TryEmitThought(eventTagFilter: null, ignoreGlobalCooldown: true, logDebugWeights: true);
                        if (!emitted)
                        {
                            Messages.Message("No eligible FabricatorTopicDef matched current state.", MessageTypeDefOf.RejectInput, historical: false);
                        }
                    }
                };
            }
        }
    }
}
