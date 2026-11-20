using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace AbsolutelyMoreCannons
{
    public class ITab_AmmoCapacitorUpgrades : ITab
    {
        private Vector2 upgradeScrollPos = Vector2.zero;
        private Vector2 moduleScrollPos = Vector2.zero;

        private static readonly Color PanelBgColor = new Color(0.13f, 0.14f, 0.16f, 0.95f);
        private static readonly Color CardBgColor = new Color(0.18f, 0.20f, 0.23f, 0.90f);
        private static readonly Color UnlockedCardColor = new Color(0.16f, 0.34f, 0.22f, 0.85f);
        private static readonly Color NextCardColor = new Color(0.22f, 0.36f, 0.54f, 0.85f);
        private static readonly Color ActiveModuleColor = new Color(0.20f, 0.42f, 0.62f, 0.55f);

        public ITab_AmmoCapacitorUpgrades()
        {
            size = new Vector2(820f, 520f);
            labelKey = "AMC_TabAmmoCapacitorUpgrades";
            tutorTag = "AmmoCapacitorUpgrades";
        }

        public override bool IsVisible
        {
            get
            {
                if (SelThing == null || SelThing.Faction != Faction.OfPlayer) return false;
                return SelThing.TryGetComp<CompAmmoCapacitor>() != null;
            }
        }

        private CompAmmoCapacitor Comp => SelThing?.TryGetComp<CompAmmoCapacitor>();

        protected override void FillTab()
        {
            var comp = Comp;
            if (comp == null) return;

            Text.Font = GameFont.Small;
            Rect mainRect = new Rect(0f, 0f, size.x, size.y).ContractedBy(10f);

            float spacing = 10f;
            float leftWidth = (mainRect.width - spacing) * 0.52f;
            float rightWidth = mainRect.width - leftWidth - spacing;

            Rect leftRect = new Rect(mainRect.x, mainRect.y + 18f, leftWidth, mainRect.height - 18f);
            Rect rightRect = new Rect(leftRect.xMax + spacing, mainRect.y + 18f, rightWidth, mainRect.height - 18f);

            DrawLeftUpgradeLevels(leftRect, comp);
            DrawRightModuleSlot(rightRect, comp);
        }

        private void DrawLeftUpgradeLevels(Rect rect, CompAmmoCapacitor comp)
        {
            Widgets.DrawBoxSolidWithOutline(rect, PanelBgColor, Color.gray);
            Rect inner = rect.ContractedBy(8f);

            int curLv = comp.upgradeLevel;
            int maxLv = comp.MaxUpgradeLevel;

            Widgets.Label(new Rect(inner.x, inner.y, inner.width, 24f),
                $"<b>Machine Tier Upgrades (Level {curLv} / {maxLv})</b>");

            // Current active stats summary box
            Rect summaryRect = new Rect(inner.x, inner.y + 26f, inner.width, 58f);
            Widgets.DrawBoxSolid(summaryRect, CardBgColor);
            Rect sumInner = summaryRect.ContractedBy(6f);

            float curSpeedMult = comp.CurrentUpgradeSpeedMultiplier;
            float curRebateChance = comp.CurrentRebateChance;
            float curRebatePct = comp.CurrentRebatePercent;
            string lvLabel = comp.CurrentUpgradeDef?.label ?? "Standard Factory Configuration";

            Text.Font = GameFont.Tiny;
            string rebateSummary = curRebateChance > 0f && curRebatePct > 0f
                ? $"{curRebateChance * 100f:F0}% chance to refund {curRebatePct * 100f:F0}% of recipe material cost"
                : "None (0% chance)";
            Widgets.Label(sumInner,
                $"<b>Current Tier:</b> {lvLabel}\n" +
                $"<b>Base Work Speed:</b> {comp.Props.workSpeedFactor * curSpeedMult:0.##}x (+{(curSpeedMult - 1f) * 100f:F0}%)  |  " +
                $"<b>Max Storage:</b> {comp.MaxTotalStorage} (+{(comp.CombinedStorageMultiplier - 1f) * 100f:F0}%)\n" +
                $"<b>Material Rebate:</b> {rebateSummary}");
            Text.Font = GameFont.Small;
            TooltipHandler.TipRegion(summaryRect,
                "Active machine performance stats from unlocked Upgrade Tiers and Installed Modules.\n\n" +
                "- Base Work Speed: Autonomous work ticks progressed per game tick when powered.\n" +
                "- Max Storage: Maximum combined material capacity across all internal capacitors.\n" +
                "- Material Rebate: On every completed ammo batch, rolls the Rebate Chance; if triggered, returns the Rebate % of the recipe's ingredient costs back into the internal capacitors.");

            // Scrollable list of all XML upgrade levels
            Rect listOut = new Rect(inner.x, summaryRect.yMax + 8f, inner.width, inner.height - 100f - 36f);
            var levels = comp.Props.upgradeLevels ?? new List<CapacitorUpgradeLevel>();
            float cardH = 92f;
            Rect viewRect = new Rect(0f, 0f, listOut.width - 16f, Mathf.Max(1, levels.Count) * (cardH + 6f));

            Widgets.BeginScrollView(listOut, ref upgradeScrollPos, viewRect);
            for (int i = 0; i < levels.Count; i++)
            {
                int levelNum = i + 1;
                CapacitorUpgradeLevel lvl = levels[i];
                Rect card = new Rect(0f, i * (cardH + 6f), viewRect.width, cardH);

                bool isUnlocked = curLv >= levelNum;
                bool isNext = curLv + 1 == levelNum;

                Color bg = isUnlocked ? UnlockedCardColor : (isNext ? NextCardColor : CardBgColor);
                Widgets.DrawBoxSolidWithOutline(card, bg, isNext ? Color.cyan : Color.gray);
                Widgets.DrawHighlightIfMouseover(card);
                Rect cInner = card.ContractedBy(6f);

                string statusTag = isUnlocked
                    ? "<color=#88FF88>[UNLOCKED]</color>"
                    : (isNext
                        ? (comp.isUpgrading ? "<color=#FFCC66>[UPGRADING IN PROGRESS]</color>" : "<color=#88DDFF>[NEXT UPGRADE]</color>")
                        : "<color=#AAAAAA>[LOCKED]</color>");

                Text.Font = GameFont.Tiny;
                Widgets.Label(new Rect(cInner.x, cInner.y, cInner.width, 18f),
                    $"<b>Level {levelNum}: {lvl.label}</b>  {statusTag}");

                Widgets.Label(new Rect(cInner.x, cInner.y + 18f, cInner.width, 18f),
                    $"Speed: <b>+{(lvl.speedMultiplier - 1f) * 100f:F0}%</b> | " +
                    $"Storage: <b>+{(lvl.storageMultiplier - 1f) * 100f:F0}%</b> | " +
                    $"Rebate chance: <b>{lvl.rebateChance * 100f:F0}%</b>");

                Widgets.Label(new Rect(cInner.x, cInner.y + 36f, cInner.width, 18f),
                    $"<b>Required Skill</b>: Crafting lv <b>{lvl.minCraftingSkill}+</b>");

                // Cost breakdown (shows delivered/required when actively upgrading this level, with 2 lines height so 3+ materials wrap cleanly)
                List<string> costStrings = new List<string>();
                if (lvl.costList != null)
                {
                    foreach (var cost in lvl.costList)
                    {
                        if (cost.thingDef == null) continue;
                        if (isNext && comp.isUpgrading)
                        {
                            int delivered = comp.GetUpgradeDelivered(cost.thingDef);
                            string col = delivered >= cost.count ? "#88FF88" : "#FFCC66";
                            costStrings.Add($"{cost.thingDef.LabelCap}: <color={col}>{delivered}/{cost.count}</color>");
                        }
                        else
                        {
                            costStrings.Add($"{cost.count}x {cost.thingDef.LabelCap}");
                        }
                    }
                }
                string prefix = (isNext && comp.isUpgrading)
                    ? (comp.AreUpgradeMaterialsSatisfied
                        ? $"<b>Work Progress:</b> <color=#88FF88>{(comp.upgradeWorkDone / comp.UpgradeWorkTotal) * 100f:F0}%</color>  |  "
                        : "<b>Delivered:</b> ")
                    : "<b>Required Materials:</b> ";

                Widgets.Label(new Rect(cInner.x, cInner.y + 54f, cInner.width, 32f),
                    prefix + (costStrings.Count > 0 ? string.Join("  |  ", costStrings) : "Free"));
                Text.Font = GameFont.Small;

                string fullCosts = (lvl.costList != null && lvl.costList.Count > 0)
                    ? string.Join("\n", lvl.costList.Where(c => c.thingDef != null).Select(c => $"  - {c.count}x {c.thingDef.LabelCap}"))
                    : "  - Free";
                TooltipHandler.TipRegion(card,
                    $"Level {levelNum}: {lvl.label}\n\n" +
                    $"Effects when unlocked:\n" +
                    $"  - Crafting Speed: +{(lvl.speedMultiplier - 1f) * 100f:F0}% ({comp.Props.workSpeedFactor * lvl.speedMultiplier:0.##} work/tick)\n" +
                    $"  - Capacitor Storage: +{(lvl.storageMultiplier - 1f) * 100f:F0}% ({Mathf.RoundToInt(comp.Props.baseMaxStorage * lvl.storageMultiplier)} total capacity)\n" +
                    $"  - Material Rebate: {lvl.rebateChance * 100f:F0}% chance on every completed ammo batch to refund {lvl.materialRebatePercent * 100f:F0}% of that recipe's material cost back into the internal capacitors.\n\n" +
                    $"Upgrade Requirements:\n" +
                    $"  - Colonist Crafting Skill: {lvl.minCraftingSkill}+ (to perform the upgrade work)\n" +
                    $"  - Upgrade Work Time: {lvl.workAmount / 60f:0.#}s ({lvl.workAmount:F0} work ticks)\n" +
                    $"  - Required Materials:\n{fullCosts}");
            }
            Widgets.EndScrollView();

            // Bottom Upgrade Button
            Rect btnRect = new Rect(inner.x, inner.yMax - 32f, inner.width, 30f);
            if (curLv >= maxLv)
            {
                GUI.enabled = false;
                Widgets.ButtonText(btnRect, "Maximum Upgrade Level Reached");
                GUI.enabled = true;
            }
            else if (comp.isUpgrading)
            {
                string progText = comp.AreUpgradeMaterialsSatisfied
                    ? $"Upgrading ({(comp.upgradeWorkDone / comp.UpgradeWorkTotal) * 100f:F0}% - Skill {comp.UpgradeMinCraftingSkill}+) — Click to Cancel"
                    : "Waiting for Material Delivery — Click to Cancel Upgrade";
                if (Widgets.ButtonText(btnRect, progText))
                {
                    comp.CancelUpgrade();
                    Messages.Message($"Cancelled upgrade on {SelThing.LabelCap}. Any delivered materials were dropped.", SelThing, MessageTypeDefOf.NeutralEvent);
                }
            }
            else
            {
                var next = comp.NextUpgradeDef;
                string reqSummary = (next?.costList != null && next.costList.Count > 0)
                    ? string.Join(", ", next.costList.Where(c => c.thingDef != null).Select(c => $"{c.count} {c.thingDef.LabelCap}"))
                    : "Free";
                string btnLabel = $"Start Upgrade (Requires: {reqSummary})";

                if (Widgets.ButtonText(btnRect, btnLabel))
                {
                    if (comp.StartUpgrade(out string msg))
                    {
                        Messages.Message(msg, SelThing, MessageTypeDefOf.NeutralEvent);
                    }
                }
                TooltipHandler.TipRegion(btnRect,
                    $"Pauses autonomous ammo crafting and orders colonists to deliver:\n  {reqSummary}\n\n" +
                    $"Once materials are delivered, a colonist with Crafting skill {next?.minCraftingSkill ?? 6}+ will work on the machine ({(next?.workAmount ?? 2500f) / 60f:0.#}s base work) to complete Level {curLv + 1}.");
            }
        }

        private void DrawRightModuleSlot(Rect rect, CompAmmoCapacitor comp)
        {
            Widgets.DrawBoxSolidWithOutline(rect, PanelBgColor, Color.gray);
            Rect inner = rect.ContractedBy(8f);

            Widgets.Label(new Rect(inner.x, inner.y, inner.width, 24f),
                "<b>Specialization Module Bay</b>");

            // Current installed module box
            Rect slotRect = new Rect(inner.x, inner.y + 26f, inner.width, 118f);
            Widgets.DrawBoxSolidWithOutline(slotRect, CardBgColor, Color.gray);
            Rect sInner = slotRect.ContractedBy(8f);

            Thing installed = comp.InstalledModule;
            if (installed != null)
            {
                DrawDefIcon(new Rect(sInner.x, sInner.y, 44f, 44f), installed.def);
                Text.Font = GameFont.Tiny;
                Rect titleRect = new Rect(sInner.x + 52f, sInner.y, sInner.width - 52f, 64f);
                Widgets.Label(titleRect,
                    $"<b>Installed:</b> <color=#88FF88>{installed.def.LabelCap}</color>\n" +
                    GetModuleEffectSummary(installed.def));
                Text.Font = GameFont.Small;

                Rect ejectBtn = new Rect(sInner.x, sInner.yMax - 28f, sInner.width, 26f);
                if (Widgets.ButtonText(ejectBtn, "Eject Installed Module"))
                {
                    comp.targetModuleDef = null;
                    comp.EjectInstalledModule();
                    Messages.Message($"Ejected specialization module from {SelThing.LabelCap}.", SelThing, MessageTypeDefOf.NeutralEvent);
                }
            }
            else if (comp.targetModuleDef != null)
            {
                DrawDefIcon(new Rect(sInner.x, sInner.y, 44f, 44f), comp.targetModuleDef);
                Text.Font = GameFont.Tiny;
                Rect titleRect = new Rect(sInner.x + 52f, sInner.y, sInner.width - 52f, 64f);
                Widgets.Label(titleRect,
                    $"<b>Pending Haul:</b> <color=#FFCC66>{comp.targetModuleDef.LabelCap}</color>\n" +
                    "Waiting for a colonist to haul and install this module.");
                Text.Font = GameFont.Small;

                Rect cancelBtn = new Rect(sInner.x, sInner.yMax - 28f, sInner.width, 26f);
                if (Widgets.ButtonText(cancelBtn, "Cancel Module Installation"))
                {
                    comp.targetModuleDef = null;
                }
            }
            else
            {
                GUI.color = Color.gray;
                Widgets.Label(sInner,
                    "<b>No Specialization Module Installed</b>\n" +
                    "Select a module below to assign a colonist to haul and install it into the fabricator.");
                GUI.color = Color.white;
            }

            // List of available module definitions in the database
            Widgets.Label(new Rect(inner.x, slotRect.yMax + 10f, inner.width, 22f),
                "<b>Available Fabricator Modules</b>");

            Rect modListOut = new Rect(inner.x, slotRect.yMax + 34f, inner.width, inner.height - slotRect.height - 64f);
            List<ThingDef> allModules = CompAmmoCapacitor.AllModuleDefs;
            float modCardH = 104f;
            Rect modView = new Rect(0f, 0f, modListOut.width - 16f, Mathf.Max(1, allModules.Count) * (modCardH + 6f));

            Widgets.BeginScrollView(modListOut, ref moduleScrollPos, modView);
            Map map = SelThing?.Map;
            for (int i = 0; i < allModules.Count; i++)
            {
                ThingDef mDef = allModules[i];
                Rect row = new Rect(0f, i * (modCardH + 6f), modView.width, modCardH);

                bool isInstalled = installed != null && installed.def == mDef;
                bool isTargeted = comp.targetModuleDef == mDef;

                Widgets.DrawBoxSolidWithOutline(row, (isInstalled || isTargeted) ? ActiveModuleColor : CardBgColor, Color.gray);
                Widgets.DrawHighlightIfMouseover(row);
                Rect rInner = row.ContractedBy(6f);

                DrawDefIcon(new Rect(rInner.x, rInner.y + 4f, 34f, 34f), mDef);
                Widgets.InfoCardButton(rInner.x + 36f, rInner.y + 6f, mDef);

                int mapCount = map != null ? map.resourceCounter.GetCount(mDef) : 0;
                Text.Font = GameFont.Tiny;
                Widgets.Label(new Rect(rInner.x + 62f, rInner.y, rInner.width - 62f, 62f),
                    $"<b>{mDef.LabelCap}</b> (On map: {mapCount})\n" +
                    GetModuleEffectSummary(mDef));
                Text.Font = GameFont.Small;

                TooltipHandler.TipRegion(row,
                    $"{mDef.LabelCap}\n\n{mDef.description}\n\nEffects:\n{GetModuleEffectSummary(mDef)}");

                Rect assignBtn = new Rect(rInner.x, rInner.yMax - 24f, rInner.width, 22f);
                if (isInstalled)
                {
                    GUI.enabled = false;
                    Widgets.ButtonText(assignBtn, "Currently Installed");
                    GUI.enabled = true;
                }
                else if (isTargeted)
                {
                    GUI.enabled = false;
                    Widgets.ButtonText(assignBtn, "Queued for Colonist Hauling...");
                    GUI.enabled = true;
                }
                else
                {
                    if (Widgets.ButtonText(assignBtn, mapCount > 0 ? "Assign Module for Installation" : "Assign Module (None in Stockpile)"))
                    {
                        comp.targetModuleDef = mDef;
                        if (comp.HasModule && comp.InstalledModule.def != mDef)
                        {
                            comp.EjectInstalledModule();
                        }
                        Messages.Message($"Assigned {mDef.LabelCap} to {SelThing.LabelCap}. Colonists will haul and install it.", SelThing, MessageTypeDefOf.NeutralEvent);
                    }
                }
            }
            Widgets.EndScrollView();
        }

        private static string GetModuleEffectSummary(ThingDef moduleDef)
        {
            var ext = moduleDef?.GetModExtension<FabricatorModuleExtension>();
            if (ext == null) return moduleDef?.description ?? "";

            List<string> parts = new List<string>();
            if (Mathf.Abs(ext.matchedSpeedMultiplier - 1f) > 0.001f || Mathf.Abs(ext.otherSpeedMultiplier - 1f) > 0.001f)
            {
                string matchSign = ext.matchedSpeedMultiplier >= 1f
                    ? $"+{(ext.matchedSpeedMultiplier - 1f) * 100f:F0}% faster"
                    : $"{(1f - ext.matchedSpeedMultiplier) * 100f:F0}% slower";
                string otherSign = ext.otherSpeedMultiplier >= 1f
                    ? $"+{(ext.otherSpeedMultiplier - 1f) * 100f:F0}% faster"
                    : $"{(1f - ext.otherSpeedMultiplier) * 100f:F0}% slower";
                parts.Add($"{ext.targetCaliberTier} ({ext.minCaliberMm:F0}~{ext.maxCaliberMm:F0}mm): <color=#88FF88>{matchSign}</color>\nOther Calibers: <color=#FF9988>{otherSign}</color>");
            }

            if (Mathf.Abs(ext.storageMultiplier - 1f) > 0.001f)
            {
                string storSign = ext.storageMultiplier >= 1f
                    ? $"+{(ext.storageMultiplier - 1f) * 100f:F0}%"
                    : $"{(ext.storageMultiplier - 1f) * 100f:F0}%";
                parts.Add($"Capacitor Storage Capacity: <color=#88FF88>{storSign}</color>");
            }

            if (ext.extraRebateChance > 0.001f || ext.extraRebatePercent > 0.001f)
            {
                parts.Add($"Material Rebate: <color=#88FF88>+{ext.extraRebateChance * 100f:F0}% chance / +{ext.extraRebatePercent * 100f:F0}% return</color>");
            }

            return parts.Count > 0 ? string.Join("\n", parts) : (moduleDef?.description ?? "");
        }

        private static void DrawDefIcon(Rect rect, ThingDef def)
        {
            if (def == null) return;
            Texture2D icon = def.uiIcon;
            if (icon != null && icon != BaseContent.BadTex)
            {
                Color prev = GUI.color;
                GUI.color = def.uiIconColor;
                GUI.DrawTexture(rect, icon, ScaleMode.ScaleToFit);
                GUI.color = prev;
            }
        }
    }
}
