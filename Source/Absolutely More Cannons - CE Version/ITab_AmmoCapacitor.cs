using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace AbsolutelyMoreCannons
{
    public class ITab_AmmoCapacitor : ITab
    {
        private Vector2 leftScrollPos = Vector2.zero;
        private Vector2 midTicketScrollPos = Vector2.zero;
        private Vector2 rightCapacitorScrollPos = Vector2.zero;

        private string searchQuery = "";
        private RecipeDef selectedRecipe;
        private AmmoCraftTicket selectedTicket;

        // Detail editing buffers
        private bool editIsContinuous = false;
        private int editCountToCraft = 5;
        private string editCountBuffer = "5";
        private int editMinThreshold = 75;
        private string editMinBuffer = "75";
        private int editMaxThreshold = 750;
        private string editMaxBuffer = "750";

        private string stepSizeBuffer = "75";
        private Thing lastSelectedThing;

        private static readonly Color PanelBgColor = new Color(0.13f, 0.14f, 0.16f, 0.95f);
        private static readonly Color RowAltColor = new Color(1f, 1f, 1f, 0.04f);
        private static readonly Color SelectedRowColor = new Color(0.25f, 0.48f, 0.72f, 0.45f);
        private static readonly Color ProgressBarBgColor = new Color(0.08f, 0.08f, 0.08f, 0.9f);
        private static readonly Color ProgressBarFillColor = new Color(0.22f, 0.62f, 0.32f, 0.85f);
        private static readonly Color CapacitorBarFillColor = new Color(0.24f, 0.52f, 0.78f, 0.65f);

        public ITab_AmmoCapacitor()
        {
            size = new Vector2(920f, 540f);
            labelKey = "AMC_TabAmmoCapacitor";
            tutorTag = "AmmoCapacitor";
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

            if (lastSelectedThing != SelThing)
            {
                lastSelectedThing = SelThing;
                stepSizeBuffer = comp.uiStepSize.ToString();
            }

            Text.Font = GameFont.Small;
            Rect mainRect = new Rect(0f, 0f, size.x, size.y).ContractedBy(10f);

            // 3 Columns: Left (260px), Middle (350px), Right (270px)
            float leftWidth = 255f;
            float rightWidth = 275f;
            float spacing = 10f;
            float midWidth = mainRect.width - leftWidth - rightWidth - (spacing * 2f);

            Rect leftRect = new Rect(mainRect.x, mainRect.y + 18f, leftWidth, mainRect.height - 18f);
            Rect midRect = new Rect(leftRect.xMax + spacing, mainRect.y + 18f, midWidth, mainRect.height - 18f);
            Rect rightRect = new Rect(midRect.xMax + spacing, mainRect.y + 18f, rightWidth, mainRect.height - 18f);

            DrawLeftAmmoList(leftRect, comp);
            DrawMiddleTicketsAndDetail(midRect, comp);
            DrawRightCapacitors(rightRect, comp);
        }

        private void SelectRecipeForNewTicket(RecipeDef recipe)
        {
            selectedRecipe = recipe;
            selectedTicket = null;
            editIsContinuous = false;
            editCountToCraft = 5;
            editCountBuffer = "5";
            editMinThreshold = 75;
            editMinBuffer = "75";
            editMaxThreshold = 750;
            editMaxBuffer = "750";
        }

        private void SelectExistingTicket(AmmoCraftTicket ticket)
        {
            selectedTicket = ticket;
            selectedRecipe = ticket?.recipe;
            if (ticket != null)
            {
                editIsContinuous = ticket.isContinuous;
                editCountToCraft = ticket.countToCraft;
                editCountBuffer = ticket.countToCraft.ToString();
                editMinThreshold = ticket.minThreshold;
                editMinBuffer = ticket.minThreshold.ToString();
                editMaxThreshold = ticket.maxThreshold;
                editMaxBuffer = ticket.maxThreshold.ToString();
            }
        }

        private void DrawLeftAmmoList(Rect rect, CompAmmoCapacitor comp)
        {
            Widgets.DrawBoxSolidWithOutline(rect, PanelBgColor, Color.gray);
            Rect inner = rect.ContractedBy(6f);

            // Header
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(inner.x, inner.y, inner.width, 22f), "<b>Craftable Ammunition</b>");

            // Search bar
            Rect searchLabelRect = new Rect(inner.x, inner.y + 24f, 52f, 24f);
            Widgets.Label(searchLabelRect, "Filter:");
            Rect searchFieldRect = new Rect(searchLabelRect.xMax, inner.y + 24f, inner.width - 78f, 24f);
            searchQuery = Widgets.TextField(searchFieldRect, searchQuery ?? "");
            Rect clearBtnRect = new Rect(searchFieldRect.xMax + 2f, inner.y + 24f, 24f, 24f);
            if (Widgets.ButtonText(clearBtnRect, "X"))
            {
                searchQuery = "";
                GUI.FocusControl(null);
            }

            // Scrollable list of ammo recipes
            Rect listOutRect = new Rect(inner.x, inner.y + 54f, inner.width, inner.height - 54f);
            List<RecipeDef> recipes = CompAmmoCapacitor.AllAmmoRecipes;
            if (!string.IsNullOrWhiteSpace(searchQuery))
            {
                string q = searchQuery.Trim().ToLowerInvariant();
                recipes = recipes.Where(r =>
                    (r.ProducedThingDef?.label != null && r.ProducedThingDef.label.ToLowerInvariant().Contains(q)) ||
                    (r.label != null && r.label.ToLowerInvariant().Contains(q))
                ).ToList();
            }

            float rowHeight = 32f;
            Rect viewRect = new Rect(0f, 0f, listOutRect.width - 16f, recipes.Count * rowHeight);

            Widgets.BeginScrollView(listOutRect, ref leftScrollPos, viewRect);
            for (int i = 0; i < recipes.Count; i++)
            {
                RecipeDef r = recipes[i];
                ThingDef ammoDef = r.ProducedThingDef;
                int batchCount = (r.products != null && r.products.Count > 0) ? r.products[0].count : 1;

                Rect rowRect = new Rect(0f, i * rowHeight, viewRect.width, rowHeight - 1f);
                if (selectedTicket == null && selectedRecipe == r)
                {
                    Widgets.DrawBoxSolid(rowRect, SelectedRowColor);
                }
                else if (i % 2 == 1)
                {
                    Widgets.DrawBoxSolid(rowRect, RowAltColor);
                }
                Widgets.DrawHighlightIfMouseover(rowRect);

                // Thumbnail icon
                if (ammoDef != null)
                {
                    Rect iconRect = new Rect(rowRect.x + 2f, rowRect.y + 2f, 26f, 26f);
                    DrawDefIcon(iconRect, ammoDef);
                }

                // Label
                Rect labelRect = new Rect(rowRect.x + 32f, rowRect.y + 4f, rowRect.width - 34f, 24f);
                string displayLabel = ammoDef != null ? $"{ammoDef.LabelCap} (x{batchCount})" : r.LabelCap;
                Text.Font = GameFont.Tiny;
                Widgets.Label(labelRect, displayLabel);
                Text.Font = GameFont.Small;

                if (Widgets.ButtonInvisible(rowRect))
                {
                    SelectRecipeForNewTicket(r);
                    GUI.FocusControl(null);
                }
            }
            Widgets.EndScrollView();
        }

        private void DrawMiddleTicketsAndDetail(Rect rect, CompAmmoCapacitor comp)
        {
            float topHeight = rect.height * 0.46f;
            Rect topRect = new Rect(rect.x, rect.y, rect.width, topHeight - 4f);
            Rect botRect = new Rect(rect.x, rect.y + topHeight + 4f, rect.width, rect.height - topHeight - 4f);

            // ==================== TOP: ACTIVE AUTOCRAFT TICKETS ====================
            Widgets.DrawBoxSolidWithOutline(topRect, PanelBgColor, Color.gray);
            Rect topInner = topRect.ContractedBy(6f);

            Widgets.Label(new Rect(topInner.x, topInner.y, topInner.width, 22f),
                $"<b>Active Autocrafting Queue ({comp.tickets.Count})</b>");

            Rect ticketListOut = new Rect(topInner.x, topInner.y + 24f, topInner.width, topInner.height - 24f);
            float ticketRowHeight = 46f;
            Rect ticketView = new Rect(0f, 0f, ticketListOut.width - 16f, Mathf.Max(1, comp.tickets.Count) * ticketRowHeight);

            Widgets.BeginScrollView(ticketListOut, ref midTicketScrollPos, ticketView);
            if (comp.tickets.Count == 0)
            {
                GUI.color = Color.gray;
                Widgets.Label(new Rect(4f, 6f, ticketView.width - 8f, 40f),
                    "No active tickets. Click an ammunition type on the left to configure a crafting ticket.");
                GUI.color = Color.white;
            }
            else
            {
                for (int i = 0; i < comp.tickets.Count; i++)
                {
                    AmmoCraftTicket t = comp.tickets[i];
                    ThingDef ammoDef = t.ProducedAmmoDef;
                    Rect row = new Rect(0f, i * ticketRowHeight, ticketView.width, ticketRowHeight - 2f);

                    if (selectedTicket == t)
                        Widgets.DrawBoxSolid(row, SelectedRowColor);
                    else if (i % 2 == 1)
                        Widgets.DrawBoxSolid(row, RowAltColor);

                    Widgets.DrawHighlightIfMouseover(row);

                    // Icon
                    if (ammoDef != null)
                    {
                        DrawDefIcon(new Rect(row.x + 2f, row.y + 4f, 28f, 28f), ammoDef);
                    }

                    // Title & Mode
                    int mapOwned = (ammoDef != null && SelThing?.Map != null)
                        ? SelThing.Map.resourceCounter.GetCount(ammoDef)
                        : 0;
                    string modeSummary = t.isContinuous
                        ? $"Auto [{t.minThreshold}~{t.maxThreshold}] (Owned: {mapOwned})"
                        : $"One-Time: {t.countToCraft} batches left";

                    Text.Font = GameFont.Tiny;
                    Rect titleRect = new Rect(row.x + 34f, row.y + 2f, row.width - 96f, 20f);
                    Widgets.Label(titleRect, $"<b>{ammoDef?.LabelCap ?? t.recipe?.LabelCap}</b> - {modeSummary}");

                    // Progress bar
                    Rect barRect = new Rect(row.x + 34f, row.y + 23f, row.width - 98f, 16f);
                    Widgets.DrawBoxSolid(barRect, ProgressBarBgColor);
                    float pct = Mathf.Clamp01(t.workProgress / t.TotalWorkAmount);
                    Widgets.DrawBoxSolid(new Rect(barRect.x, barRect.y, barRect.width * pct, barRect.height), ProgressBarFillColor);

                    string barState;
                    if (t.paused)
                        barState = "Paused";
                    else if (!comp.ShouldTicketRun(t))
                        barState = $"Standby (Owned {mapOwned} >= {t.minThreshold})";
                    else if (!t.materialsLocked && !comp.TryCheckOrConsumeIngredients(t.recipe, false, out string miss))
                        barState = miss ?? "Missing Materials";
                    else
                        barState = $"{pct * 100f:F0}%";

                    Text.Anchor = TextAnchor.MiddleCenter;
                    Widgets.Label(barRect, barState);
                    Text.Anchor = TextAnchor.UpperLeft;
                    Text.Font = GameFont.Small;

                    // Pause button & Delete button
                    Rect pauseBtn = new Rect(row.xMax - 58f, row.y + 10f, 26f, 24f);
                    if (Widgets.ButtonText(pauseBtn, t.paused ? ">" : "||"))
                    {
                        t.paused = !t.paused;
                    }
                    TooltipHandler.TipRegion(pauseBtn, t.paused ? "Resume Ticket" : "Pause Ticket");

                    Rect delBtn = new Rect(row.xMax - 28f, row.y + 10f, 26f, 24f);
                    if (Widgets.ButtonText(delBtn, "X"))
                    {
                        if (selectedTicket == t) selectedTicket = null;
                        comp.tickets.RemoveAt(i);
                        break;
                    }
                    TooltipHandler.TipRegion(delBtn, "Cancel & Remove Ticket");

                    // Select row for editing
                    Rect clickArea = new Rect(row.x, row.y, row.width - 62f, row.height);
                    if (Widgets.ButtonInvisible(clickArea))
                    {
                        SelectExistingTicket(t);
                        GUI.FocusControl(null);
                    }
                }
            }
            Widgets.EndScrollView();

            // ==================== BOTTOM: TICKET DETAIL & CONFIG ====================
            Widgets.DrawBoxSolidWithOutline(botRect, PanelBgColor, Color.gray);
            Rect botInner = botRect.ContractedBy(8f);

            if (selectedRecipe == null)
            {
                GUI.color = Color.gray;
                Widgets.Label(botInner, "Select an ammunition from the left list to create a new crafting ticket, or click an existing ticket above to modify its rules.");
                GUI.color = Color.white;
                return;
            }

            ThingDef selAmmo = selectedRecipe.ProducedThingDef;
            int selBatchCount = (selectedRecipe.products != null && selectedRecipe.products.Count > 0)
                ? selectedRecipe.products[0].count
                : 1;

            // Icon + Header
            if (selAmmo != null)
            {
                DrawDefIcon(new Rect(botInner.x, botInner.y, 40f, 40f), selAmmo);
                Widgets.InfoCardButton(botInner.x + 42f, botInner.y, selAmmo);
            }

            int currentMapCount = (selAmmo != null && SelThing?.Map != null)
                ? SelThing.Map.resourceCounter.GetCount(selAmmo)
                : 0;
            float effSpeed = comp.GetEffectiveWorkSpeed(selAmmo);
            float workSeconds = (selectedRecipe.WorkAmountTotal(null) / Mathf.Max(0.1f, effSpeed)) / 60f;

            Rect headerLabelRect = new Rect(botInner.x + 68f, botInner.y, botInner.width - 68f, 44f);
            Widgets.Label(headerLabelRect,
                $"<b>{selAmmo?.LabelCap ?? selectedRecipe.LabelCap}</b> (Produces x{selBatchCount})\n" +
                $"Owned on map: <b>{currentMapCount}</b> | Time per batch: <b>{workSeconds:F1}s</b> ({effSpeed:0.#}x)");

            // Ingredients breakdown
            float curY = botInner.y + 46f;
            List<string> ingParts = new List<string>();
            foreach (var ing in selectedRecipe.ingredients)
            {
                ThingDef ingDef = ing.IsFixedIngredient ? ing.FixedIngredient : ing.filter?.AllowedThingDefs?.FirstOrDefault();
                if (ingDef != null)
                {
                    int req = Mathf.CeilToInt(ing.GetBaseCount());
                    int have = comp.GetStored(ingDef);
                    string color = have >= req ? "#88FF88" : "#FF9988";
                    ingParts.Add($"{ingDef.LabelCap}: <color={color}>{have}/{req}</color>");
                }
            }
            Text.Font = GameFont.Tiny;
            Widgets.Label(new Rect(botInner.x, curY, botInner.width, 32f),
                "<b>Required per batch:</b> " + string.Join("  |  ", ingParts));
            Text.Font = GameFont.Small;
            curY += 34f;

            // Mode Radio Buttons: One-Time vs Auto/Continuous
            Rect oneTimeRadio = new Rect(botInner.x, curY, 145f, 24f);
            if (Widgets.RadioButtonLabeled(oneTimeRadio, "One-Time Craft", !editIsContinuous))
            {
                editIsContinuous = false;
            }

            Rect autoRadio = new Rect(botInner.x + 160f, curY, 175f, 24f);
            if (Widgets.RadioButtonLabeled(autoRadio, "Auto / Continuous", editIsContinuous))
            {
                editIsContinuous = true;
            }
            curY += 30f;

            if (!editIsContinuous)
            {
                Rect lblRect = new Rect(botInner.x, curY, 180f, 24f);
                Widgets.Label(lblRect, $"Batches to craft (x{selBatchCount} each):");
                Rect fieldRect = new Rect(lblRect.xMax + 4f, curY, 80f, 24f);
                Widgets.TextFieldNumeric(fieldRect, ref editCountToCraft, ref editCountBuffer, 1, 99999);
                curY += 56f;
            }
            else
            {
                Rect minLbl = new Rect(botInner.x, curY, 200f, 24f);
                Widgets.Label(minLbl, "Start crafting when owned <");
                Rect minField = new Rect(minLbl.xMax + 4f, curY, 80f, 24f);
                Widgets.TextFieldNumeric(minField, ref editMinThreshold, ref editMinBuffer, 0, 999999);
                curY += 28f;

                Rect maxLbl = new Rect(botInner.x, curY, 200f, 24f);
                Widgets.Label(maxLbl, "Stop crafting when owned >=");
                Rect maxField = new Rect(maxLbl.xMax + 4f, curY, 80f, 24f);
                Widgets.TextFieldNumeric(maxField, ref editMaxThreshold, ref editMaxBuffer, 1, 999999);
                curY += 28f;
            }

            // Action button: Create or Update Ticket
            Rect actionBtnRect = new Rect(botInner.x, botInner.yMax - 30f, botInner.width, 28f);
            if (selectedTicket != null)
            {
                if (Widgets.ButtonText(actionBtnRect, "Save Changes to Selected Ticket"))
                {
                    selectedTicket.isContinuous = editIsContinuous;
                    selectedTicket.countToCraft = Mathf.Max(1, editCountToCraft);
                    selectedTicket.minThreshold = Mathf.Max(0, editMinThreshold);
                    selectedTicket.maxThreshold = Mathf.Max(selectedTicket.minThreshold + 1, editMaxThreshold);
                }
            }
            else
            {
                if (Widgets.ButtonText(actionBtnRect, "+ Add Autocraft Ticket"))
                {
                    AmmoCraftTicket newTicket = new AmmoCraftTicket
                    {
                        recipe = selectedRecipe,
                        isContinuous = editIsContinuous,
                        countToCraft = Mathf.Max(1, editCountToCraft),
                        minThreshold = Mathf.Max(0, editMinThreshold),
                        maxThreshold = Mathf.Max(editMinThreshold + 1, editMaxThreshold)
                    };
                    comp.tickets.Add(newTicket);
                    SelectExistingTicket(newTicket);
                }
            }
        }

        private void DrawRightCapacitors(Rect rect, CompAmmoCapacitor comp)
        {
            Widgets.DrawBoxSolidWithOutline(rect, PanelBgColor, Color.gray);
            Rect inner = rect.ContractedBy(6f);

            // Title
            Widgets.Label(new Rect(inner.x, inner.y, inner.width, 22f), "<b>Internal Material Capacitors</b>");

            // Step Size numeric input
            Rect stepLabelRect = new Rect(inner.x, inner.y + 24f, 115f, 24f);
            Widgets.Label(stepLabelRect, "+/- Step Amount:");
            Rect stepFieldRect = new Rect(stepLabelRect.xMax, inner.y + 24f, 70f, 24f);
            int stepVal = comp.uiStepSize;
            Widgets.TextFieldNumeric(stepFieldRect, ref stepVal, ref stepSizeBuffer, 1, 10000);
            comp.uiStepSize = Mathf.Max(1, stepVal);

            // Material Substitution Checkbox (disabled by default)
            Rect subCheckRect = new Rect(inner.x, inner.y + 52f, inner.width, 24f);
            Widgets.CheckboxLabeled(subCheckRect, "Allow Material Substitution", ref comp.allowSubstitution);
            TooltipHandler.TipRegion(subCheckRect,
                "When enabled, if a recipe runs out of its primary material (e.g. Steel or FSX), the machine will substitute other stored materials in the same category using their equivalence ratio (e.g. Plasteel or Chemfuel).");

            // Scrollable Capacitor List grouped by Projectile & Fuel (reserve 32px at bottom for total storage bar)
            float bottomBarH = 26f;
            Rect listOut = new Rect(inner.x, inner.y + 80f, inner.width, inner.height - 80f - bottomBarH - 6f);
            var entries = comp.Props.materials ?? new List<CapacitorMaterialEntry>();
            var projectileEntries = entries.Where(e => e.thingDef != null && e.category == CapacitorCategory.Projectile).ToList();
            var fuelEntries = entries.Where(e => e.thingDef != null && e.category == CapacitorCategory.Fuel).ToList();

            float rowH = 44f;
            float headerH = 24f;
            float totalHeight = (headerH * 2f) + ((projectileEntries.Count + fuelEntries.Count) * rowH) + 12f;

            Rect viewRect = new Rect(0f, 0f, listOut.width - 16f, totalHeight);
            Widgets.BeginScrollView(listOut, ref rightCapacitorScrollPos, viewRect);

            float y = 0f;
            Widgets.Label(new Rect(0f, y, viewRect.width, headerH), "<b>Projectile & Component Materials</b>");
            y += headerH;

            foreach (var entry in projectileEntries)
            {
                DrawCapacitorRow(new Rect(0f, y, viewRect.width, rowH - 2f), comp, entry);
                y += rowH;
            }

            y += 6f;
            Widgets.Label(new Rect(0f, y, viewRect.width, headerH), "<b>Fuel & Propellant Materials</b>");
            y += headerH;

            foreach (var entry in fuelEntries)
            {
                DrawCapacitorRow(new Rect(0f, y, viewRect.width, rowH - 2f), comp, entry);
                y += rowH;
            }

            Widgets.EndScrollView();

            // Bottom Horizontal Storage Allocation Bar ("allocated / max")
            Rect storageBarRect = new Rect(inner.x, inner.yMax - bottomBarH, inner.width, bottomBarH);
            int allocated = comp.TotalAllocatedStorage;
            int maxStorage = Mathf.Max(1, comp.MaxTotalStorage);
            float allocPct = Mathf.Clamp01((float)allocated / maxStorage);

            Widgets.DrawBoxSolidWithOutline(storageBarRect, ProgressBarBgColor, Color.gray);
            if (allocPct > 0f)
            {
                Rect fillRect = new Rect(storageBarRect.x + 1f, storageBarRect.y + 1f, (storageBarRect.width - 2f) * allocPct, storageBarRect.height - 2f);
                Widgets.DrawBoxSolid(fillRect, CapacitorBarFillColor);
            }

            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(storageBarRect, $"{allocated} / {maxStorage}");
            Text.Anchor = TextAnchor.UpperLeft;

            float upgBonusPct = (comp.CurrentUpgradeStorageMultiplier - 1f) * 100f;
            float modBonusPct = (comp.CurrentModuleStorageMultiplier - 1f) * 100f;
            TooltipHandler.TipRegion(storageBarRect,
                $"Total Internal Storage Allocation: {allocated} / {maxStorage}\n" +
                $"Currently Stored in Capacitors: {comp.TotalStoredMaterials} / {maxStorage}\n" +
                $"Unallocated Capacity Remaining: {comp.RemainingUnallocatedStorage}\n\n" +
                $"Base Capacity: {comp.Props.baseMaxStorage}\n" +
                $"Upgrade Tier Bonus: +{upgBonusPct:F0}%\n" +
                $"Module Bonus: +{modBonusPct:F0}%");
        }

        private void DrawCapacitorRow(Rect row, CompAmmoCapacitor comp, CapacitorMaterialEntry entry)
        {
            ThingDef def = entry.thingDef;
            int stored = comp.GetStored(def);
            int target = comp.GetTarget(def);

            Widgets.DrawBoxSolid(row, RowAltColor);

            // Background capacity fill bar
            if (target > 0 || stored > 0)
            {
                float fillPct = target > 0 ? Mathf.Clamp01((float)stored / target) : 1f;
                Rect fillRect = new Rect(row.x, row.y, row.width * fillPct, row.height);
                Widgets.DrawBoxSolid(fillRect, CapacitorBarFillColor);
            }

            // Icon
            DrawDefIcon(new Rect(row.x + 2f, row.y + 6f, 28f, 28f), def);

            // Label & Equivalence info
            Text.Font = GameFont.Tiny;
            string eqText = (entry.canSubstitute && entry.equivalenceValue > 0f)
                ? $" (Eq: {entry.equivalenceValue:0.##}x)"
                : "";
            Rect nameRect = new Rect(row.x + 34f, row.y + 2f, row.width - 96f, 18f);
            Widgets.Label(nameRect, $"<b>{def.LabelCap}</b>{eqText}");

            // Stored / Target readout
            Rect countRect = new Rect(row.x + 34f, row.y + 20f, row.width - 96f, 18f);
            string haulNote = stored < target ? " <color=#FFCC66>(Hauling)</color>" : "";
            Widgets.Label(countRect, $"{stored} / {target}{haulNote}");
            Text.Font = GameFont.Small;

            // '-' and '+' buttons
            Rect minusBtn = new Rect(row.xMax - 58f, row.y + 8f, 26f, 26f);
            if (Widgets.ButtonText(minusBtn, "-"))
            {
                comp.SetTarget(def, Mathf.Max(0, target - comp.uiStepSize));
            }
            TooltipHandler.TipRegion(minusBtn, $"Decrease target by {comp.uiStepSize}.\nIf stored exceeds new target, excess drops adjacent to machine.");

            Rect plusBtn = new Rect(row.xMax - 28f, row.y + 8f, 26f, 26f);
            if (Widgets.ButtonText(plusBtn, "+"))
            {
                comp.SetTarget(def, target + comp.uiStepSize);
            }
            TooltipHandler.TipRegion(plusBtn, $"Increase target by {comp.uiStepSize}.\nPawns will haul {def.LabelCap} to reach target.");
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
