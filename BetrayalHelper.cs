using ExileCore;
using ExileCore.PoEMemory;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.Elements;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared;
using ExileCore.Shared.Enums;
using ExileCore.Shared.Helpers;
using ImGuiNET;
using SharpDX;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using static System.Formats.Asn1.AsnWriter;
using static System.Net.Mime.MediaTypeNames;
using Vector2N = System.Numerics.Vector2;

namespace BetrayalHelper
{
    public class BetrayalHelper : BaseSettingsPlugin<BetrayalHelperSettings>
    {
        private Action ShowSyndicateBoard;
        List<BetrayalMaster> ListBetrayalMasters = new List<BetrayalMaster>();
        bool badJobsAreFull = false;
        bool hasClosedWindow = false;
        bool _closedOverviewForCurrentPanel = false;
        private readonly HashSet<string> _readySafehouses = new(StringComparer.OrdinalIgnoreCase);


        List<string> allMasters = new List<string>
        {
            "Leo",
            "Jorgin",
            "Vagan",
            "Tora",
            "Janus",
            "Korell",
            "Guff",
            "Elreon",
            "Cameria",
            "It That Fled",
            "Vorici",
            "Aisling",
            "Haku",
            "Hillock",
            "Gravicius",
            "Riker",
            "Rin"


        };
        public List<string> _missingMastersFromBoard;
        public List<string> _mastersToBePresent;
        public List<string> _jobsToGainIntelligence;
        public List<string> _jobsToNOTGainIntelligence;



        string _eventAction = "";
        string _eventText = "";
        string _jobEvent ="";
        string _masterText = "";
        RectangleF _SpecialButton = new RectangleF();
        RectangleF _ReleaseButton = new RectangleF();
        RectangleF _InterrogateButton = new RectangleF();
        BetrayalMaster _eventMaster = new BetrayalMaster();
        List<BetrayalMaster> _eventMasters = new List<BetrayalMaster>();
        List<Outcome> _lastOutcomes = new List<Outcome>();
        ActionType? _lastRecommendedAction;
        int _lastRecommendedScore;
        bool? _lastInterrogationQueueFull;
        string _lastInterrogationCounterText = "not checked";
        string _interrogationQueueEventKey = "";
        bool _cachedInterrogationQueueFull;
        string _faqSearch = "";
        string _relationshipNormalization = "none (no relationship preview)";

        private static readonly (string Category, string Question, string Answer)[] FaqEntries =
        {
            ("Overview", "What does BetrayalHelper do?", "It reads the visible Immortal Syndicate board and encounter choice, scores Special, Interrogate and Release, then highlights the highest-scoring button. It never clicks for you."),
            ("Overview", "What is the main strategy?", "Safehouse Rewards uses a 5-5-2-2 board: two compact Farm divisions with two desired members and two Filler divisions with five members."),
            ("Configuration", "What is the difference between Farm/Filler and Run/Skip?", "Farm/Filler describes desired member placement. Run/Skip independently describes whether intelligence and completion of that safehouse are desirable."),
            ("Configuration", "Does member order or a preferred leader matter?", "No. Configured members are an unordered set. The plugin does not try to make a particular member leader; it only observes the actual leader and rank."),
            ("Configuration", "Is two the division capacity?", "No. Five is the game limit. Two is the strategic target for a compact Farm division."),
            ("Relationships", "Is every Rival line good?", "Usually yes. Every Rival link can produce additional encounters and options. Farm-Filler links receive the highest value because they also connect the compact reward divisions with the larger reinforcement pool; same-group Rival links remain positive."),
            ("Relationships", "Is Trusted useful?", "Yes. Cross-group Trusted is a setup edge: Trusted members are likely to offer Betray later, converting the edge to Rival."),
            ("Relationships", "When is Neutral good?", "Usually only when its other effects outweigh losing the relationship. Becoming Neutral removes a Rival or Trusted link and therefore reduces the encounter network."),
            ("Relationships", "Why avoid removing all rivalries?", "It destroys accumulated board topology and takes many encounters to rebuild. Release is usually safer."),
            ("Relationships", "Do links guarantee reinforcements?", "No. They affect eligibility and likelihood. Encounter rules, imprisonment, ranks, leaders, Atlas modifiers and random selection still apply."),
            ("Relationships", "How does the plugin identify Rival and Trusted links?", "It reads ServerData.BetrayalMemberRelationships directly. Rival and Trusted are supplied by ExileCore from game memory; the plugin does not infer them from line colours."),
            ("Relationships", "Why are members without relationships left until last?", "When several defeated members are available, connected members are handled first. Leaving an unconnected member for last improves the chance that the remaining encounter option creates a useful new relationship."),
            ("Scoring", "How are actions selected?", "Every parsed effect becomes an Outcome. Outcomes are grouped by Special, Interrogate or Release and summed. The highest total is highlighted."),
            ("Scoring", "Why can Release win?", "Release has a neutral baseline. It wins when changing the board and interrogating are both expected to be harmful."),
            ("Scoring", "How is a correct Farm member valued?", "Its base placement value is 30 plus 10 for each rank. Event-specific effects are added separately."),
            ("Scoring", "Can a good Rival option still be rejected?", "Yes. Rank theft, expelling a desired member, a bad leader change or division-wide damage can outweigh the relationship benefit."),
            ("Execute", "How do Atlas nodes change Execute?", "Execute gives +1 rank. Enduring Legacy adds a 50% chance of one more rank; the plugin scores the expected gain (1.5 ranks) capped at rank 3. Test of Loyalty no longer exists."),
            ("Execute", "Does Execute give safehouse intelligence?", "No. Lethal Extraction no longer exists on the Atlas tree."),
            ("Interrogate", "What does Interrogate cost?", "It grants intelligence but temporarily removes the member from encounters and reinforcements. Without protection it also risks losing rank after completion."),
            ("Interrogate", "How does Unconditional Release change decisions?", "It gives a 30% chance that the interrogated member keeps rank; the model scales the rank-loss penalty by that chance and keeps the temporary absence penalty."),
            ("Interrogate", "Why not always interrogate?", "The member still disappears for three encounters, may lose leadership immediately, and may generate intelligence for a Skip division."),
            ("Safehouses", "Why did the plugin warn that a safehouse is ready?", "A Run division leader reached 100 intelligence (read from the board state, +0x84). The safehouse window also checks whether desired members are placed and ranked."),
            ("Safehouses", "Does completing Catarina reset the board?", "Current game behaviour no longer resets the Immortal Syndicate board, but Catarina Focus remains a secondary and less-tested mode."),
            ("Atlas", "Which Atlas nodes does the plugin use?", "Only Enduring Legacy and Unconditional Release, because they change rank outcomes. Enable them only if allocated. Drop, rarity, reinforcement, leader and bargain nodes do not change Special/Interrogate/Release scoring."),
            ("Debug", "Why did the old DebugMode appear to show nothing?", "It only emitted five-second ExileCore log messages during specific events. The Debug Stats tab now provides persistent live data."),
            ("Debug", "What does Mega Debug do?", "It adds verbose per-member and parser logging to the ExileCore debug log. The persistent table remains available in Debug Stats."),
            ("Debug", "What should I capture for a wrong recommendation?", "Record Action.Id, full English event text, involved members, ranks, divisions, leaders, strategy settings, expected action and highlighted action."),
            ("Overlay", "What do the coloured frames around Syndicate members mean?", "Green means a desired Farm member in a valuable placement (score 30 or higher). Yellow means an acceptable Filler member (score 1 or higher). Red means an unwanted or incorrectly placed member (score 0 or lower). The colours describe configured strategic value, not item rarity or relationship type."),
            ("Overlay", "What does Priority N | Score X mean?", "Priority is the suggested conversation order: members with existing relationships first and relationship-free members last, then placement score. Priority 1 also receives a subtle fill. Encounter-choice buttons are evaluated separately after opening the member's choice panel."),
            ("Interrogate", "What happens when the interrogation queue is full (3/3)?", "Interrogate is treated as unavailable and cannot be recommended. The plugin reads the visible 3/3 prisoner counter before choosing an action, then selects the best Special or Release option instead."),
            ("Compatibility", "Does localization work?", "Not reliably. Several outcomes are parsed from English event text and regular expressions."),
            ("Compatibility", "Why can a game update break parsing?", "ExileAPI object layouts, UI child indices, action IDs and event wording can change between patches."),
            ("Privacy", "Does the plugin send data anywhere?", "No. BetrayalHelper contains no network client and does not depend on WhatsAnAiBridge."),
            ("Safety", "Does the plugin automate input?", "No. It renders recommendations only and never presses encounter buttons."),
            ("Troubleshooting", "The board table is empty. What should I do?", "Open the Syndicate board, verify the plugin is enabled, then check Debug Stats. Live statistics refresh while the board panel is visible."),
            ("Troubleshooting", "No button is highlighted. Why?", "The event may be unsupported, parsing may have failed, or action totals may be tied. Enable Debug and inspect Last decision outcomes."),
        };



        public override bool Initialise()
        {
            Name = "BetrayalHelper";
            NormalizeSettingsNames();
            return true;
        }

        /// <summary>Engine uses "ItThatFled"; UI/settings use "It That Fled". Compare using one form.</summary>
        private static string NormalizeName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "";
            name = name.Trim();
            return name.Replace(" ", "").Equals("ItThatFled", StringComparison.OrdinalIgnoreCase)
                ? "It That Fled"
                : name;
        }

        private void NormalizeSettingsNames()
        {
            try
            {
                foreach (var key in Settings.DivisionMembers.Keys.ToList())
                    Settings.DivisionMembers[key] = Settings.DivisionMembers[key]
                        .Select(NormalizeName).Where(n => n.Length > 0).Distinct().ToList();
            }
            catch { }
        }

        // Rank mechanics from the current Atlas tree (see settings).
        private double ExtraRankChance =>
            Math.Min(1.0, (Settings.EnduringLegacy.Value ? 0.50 : 0.0));

        private double KeepRankChance
        {
            get
            {
                double lose = 1.0;
                if (Settings.UnconditionalRelease.Value) lose *= 0.70;
                                return 1.0 - lose;
            }
        }

        public override void AreaChange(AreaInstance area)
        {
            hasClosedWindow = false;
            _closedOverviewForCurrentPanel = false;
        }

        public override void DrawSettings()
        {
            bool showConfiguration = false;
            if (ImGui.BeginTabBar("##betrayal_helper_tabs"))
            {
                if (ImGui.BeginTabItem("Configuration"))
                {
                    showConfiguration = true;
                    ImGui.EndTabItem();
                }
                if (ImGui.BeginTabItem("Debug Stats"))
                {
                    DrawDebugStatsTab();
                    ImGui.EndTabItem();
                }
                if (ImGui.BeginTabItem("FAQ"))
                {
                    DrawFaqTab();
                    ImGui.EndTabItem();
                }
                ImGui.EndTabBar();
            }

            if (!showConfiguration)
                return;

            base.DrawSettings();

            bool showOverviewConfig = Settings.ShowBoardOverview.Value;
            if (ImGui.Checkbox("Show Betrayal Board Overview Window", ref showOverviewConfig))
                Settings.ShowBoardOverview.Value = showOverviewConfig;

            // ── Farming Mode selector ────────────────────────────────────────
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.PushStyleColor(ImGuiCol.Text, new System.Numerics.Vector4(1f, 0.85f, 0.1f, 1f));
            ImGui.Text("Farming Strategy");
            ImGui.PopStyleColor();
            ImGui.Separator();

            int modeIndex = (int)Settings.FarmingMode;
            ImGui.RadioButton("Safehouse Rewards (5522 classic)", ref modeIndex, 0);
            ImGui.SameLine();
            ImGui.RadioButton("Catarina Focus (run ALL safehouses)", ref modeIndex, 1);
            Settings.FarmingMode = (FarmingMode)modeIndex;

            // Mode explanation
            if (Settings.FarmingMode == FarmingMode.CatarinaFocus)
            {
                ImGui.PushStyleColor(ImGuiCol.Text, new System.Numerics.Vector4(0.4f, 0.8f, 1f, 1f));
                ImGui.TextWrapped("Catarina mode: Run ALL 4 safehouses when ready. Each leader has ~5-10% chance to drop Syndicate Medallion (Catarina access). More safehouses/hour = faster Catarina spawns. Board does NOT reset on kill (since 3.26).");
                ImGui.PopStyleColor();

                // Auto-set all DivisionShouldRun to true
                foreach (var div in new[] { "Transportation", "Fortification", "Research", "Intervention" })
                    Settings.DivisionShouldRun[div] = true;

                ImGui.Spacing();
                ImGui.Text("Preserve configured members (optional):");
                ImGui.TextWrapped("Checked divisions keep their configured members. Placement in unchecked divisions is ignored.");
                foreach (var div in new[] { "Transportation", "Fortification", "Research", "Intervention" })
                {
                    if (!Settings.CatarinaPreserveDivision.ContainsKey(div))
                        Settings.CatarinaPreserveDivision[div] = false;
                    bool preserve = Settings.CatarinaPreserveDivision[div];
                    if (ImGui.Checkbox($"Preserve {div}##catarina_preserve_{div}", ref preserve))
                        Settings.CatarinaPreserveDivision[div] = preserve;
                }
            }
            else
            {
                ImGui.PushStyleColor(ImGuiCol.Text, new System.Numerics.Vector4(0.7f, 0.7f, 0.7f, 1f));
                ImGui.TextWrapped("Classic mode: Farm only selected divisions for rewards. Filler divisions block intel to keep board stable.");
                ImGui.PopStyleColor();
            }

            // ── Per-division Run/Skip checkboxes ─────────────────────────────
            ImGui.Spacing();
            ImGui.Text("Division Run Policy (which safehouses to complete):");
            var divOrder = new[] { "Transportation", "Fortification", "Research", "Intervention" };
            foreach (var div in divOrder)
            {
                if (!Settings.DivisionShouldRun.ContainsKey(div))
                    Settings.DivisionShouldRun[div] = Settings.DivisionRoles.TryGetValue(div, out var r) && r == "Farm";

                bool shouldRun = Settings.DivisionShouldRun[div];
                bool disabled = Settings.FarmingMode == FarmingMode.CatarinaFocus; // locked to Run in Catarina mode

                if (disabled) ImGui.BeginDisabled();
                if (ImGui.Checkbox($"Run {div}##run_{div}", ref shouldRun))
                    Settings.DivisionShouldRun[div] = shouldRun;
                if (disabled) ImGui.EndDisabled();

                ImGui.SameLine();
                var roleStr = Settings.DivisionRoles.TryGetValue(div, out var role) ? role : "?";
                var roleColor = roleStr == "Farm"
                    ? new System.Numerics.Vector4(0.4f, 1f, 0.4f, 1f)
                    : new System.Numerics.Vector4(0.6f, 0.6f, 0.6f, 1f);
                ImGui.PushStyleColor(ImGuiCol.Text, roleColor);
                ImGui.Text($"({roleStr})");
                ImGui.PopStyleColor();
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Text("Division Setup");
            ImGui.Separator();

            var divisionOrder = new[] { "Transportation", "Fortification", "Research", "Intervention" };

            foreach (var div in divisionOrder)
            {
                if (!Settings.DivisionRoles.ContainsKey(div))
                    Settings.DivisionRoles[div] = (div == "Transportation" || div == "Research") ? "Farm" : "Filler";
                if (!Settings.DivisionMembers.ContainsKey(div))
                    Settings.DivisionMembers[div] = new List<string>();
            }

            foreach (var div in divisionOrder)
            {
                bool isFarm = Settings.DivisionRoles[div] == "Farm";
                int target = isFarm ? 2 : 5;
                var members = Settings.DivisionMembers[div];
                int count = members.Count;

                // ── Header row: division name + role radio buttons ──────────────
                var headerColor = isFarm
                    ? new System.Numerics.Vector4(0.4f, 1f, 0.4f, 1f)
                    : new System.Numerics.Vector4(0.8f, 0.8f, 0.8f, 1f);
                ImGui.PushStyleColor(ImGuiCol.Text, headerColor);
                ImGui.Text($"  {div}");
                ImGui.PopStyleColor();
                ImGui.SameLine(160);

                int roleIndex = isFarm ? 0 : 1;
                ImGui.RadioButton($"Farm##{div}", ref roleIndex, 0);
                ImGui.SameLine();
                ImGui.RadioButton($"Filler##{div}", ref roleIndex, 1);
                Settings.DivisionRoles[div] = roleIndex == 0 ? "Farm" : "Filler";

                // ── Progress bar: count / target ─────────────────────────────────
                float progress = target > 0 ? Math.Min((float)count / target, 1f) : 0f;
                var barColor = count == target
                    ? new System.Numerics.Vector4(0.2f, 0.8f, 0.2f, 1f)
                    : count > target
                        ? new System.Numerics.Vector4(1f, 0.4f, 0.1f, 1f)
                        : new System.Numerics.Vector4(0.9f, 0.75f, 0.1f, 1f);
                string barLabel = count == target ? $"{count} / {target}  OK"
                    : count > target ? $"{count} / {target}  too many!"
                    : $"{count} / {target}  need {target - count} more";

                // dark background behind bar so it's always visible
                ImGui.PushStyleColor(ImGuiCol.FrameBg,        new System.Numerics.Vector4(0.12f, 0.12f, 0.12f, 1f));
                ImGui.PushStyleColor(ImGuiCol.PlotHistogram,  barColor);
                ImGui.PushStyleColor(ImGuiCol.Text,           new System.Numerics.Vector4(1f, 1f, 1f, 1f));
                ImGui.ProgressBar(progress, new System.Numerics.Vector2(-1, 18), barLabel);
                ImGui.PopStyleColor(3);

                // ── Member table ─────────────────────────────────────────────────
                int pendingRemove = -1;
                var btnSizeX = new System.Numerics.Vector2(28, 22);

                var tableFlags = ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.RowBg;
                if (ImGui.BeginTable($"##members_{div}", 2, tableFlags))
                {
                    ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch);
                    ImGui.TableSetupColumn("##rm", ImGuiTableColumnFlags.WidthFixed, 32);

                    for (int i = 0; i < members.Count; i++)
                    {
                        ImGui.TableNextRow(ImGuiTableRowFlags.None, 24);

                        ImGui.TableSetColumnIndex(0);
                        var nameColor = isFarm
                            ? new System.Numerics.Vector4(0.6f, 0.9f, 0.6f, 1f)
                            : new System.Numerics.Vector4(0.8f, 0.8f, 0.8f, 1f);
                        ImGui.PushStyleColor(ImGuiCol.Text, nameColor);
                        ImGui.TextUnformatted(members[i]);
                        ImGui.PopStyleColor();

                        ImGui.TableSetColumnIndex(1);
                        ImGui.PushStyleColor(ImGuiCol.Button,        new System.Numerics.Vector4(0.55f, 0.1f, 0.1f, 1f));
                        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new System.Numerics.Vector4(0.85f, 0.2f, 0.2f, 1f));
                        ImGui.PushStyleColor(ImGuiCol.Text,          new System.Numerics.Vector4(1f, 1f, 1f, 1f));
                        if (ImGui.Button($"x##{div}_{i}", btnSizeX))
                            pendingRemove = i;
                        ImGui.PopStyleColor(3);
                    }

                    ImGui.EndTable();
                }

                if (pendingRemove >= 0)
                    members.RemoveAt(pendingRemove);

                // ── Add member combo ─────────────────────────────────────────────
                var assigned = Settings.DivisionMembers.Values.SelectMany(m => m).ToHashSet();
                var available = allMasters.Where(m => !assigned.Contains(m)).OrderBy(m => m).ToArray();
                if (available.Length > 0 && ImGui.BeginCombo($"Add to {div}##{div}_add", "-- add member --"))
                {
                    foreach (var master in available)
                    {
                        if (ImGui.Selectable(master))
                            members.Add(master);
                    }
                    ImGui.EndCombo();
                }

                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();
            }
        }

        private void DrawFaqTab()
        {
            ImGui.TextWrapped("Search the complete strategy and plugin behaviour guide. Results match category, question and answer text.");
            ImGui.SetNextItemWidth(-70);
            ImGui.InputText("##faq_search", ref _faqSearch, 256);
            ImGui.SameLine();
            if (ImGui.Button("Clear"))
                _faqSearch = "";

            string query = _faqSearch.Trim();
            int matches = 0;
            ImGui.Separator();
            ImGui.BeginChild("##faq_results", System.Numerics.Vector2.Zero, ImGuiChildFlags.Borders);
            foreach (var entry in FaqEntries)
            {
                bool match = query.Length == 0
                    || entry.Category.Contains(query, StringComparison.OrdinalIgnoreCase)
                    || entry.Question.Contains(query, StringComparison.OrdinalIgnoreCase)
                    || entry.Answer.Contains(query, StringComparison.OrdinalIgnoreCase);
                if (!match) continue;

                matches++;
                ImGui.PushStyleColor(ImGuiCol.Text, new System.Numerics.Vector4(0.45f, 0.75f, 1f, 1f));
                ImGui.TextUnformatted(entry.Category);
                ImGui.PopStyleColor();
                ImGui.PushTextWrapPos();
                ImGui.TextUnformatted(entry.Question);
                ImGui.PopTextWrapPos();
                ImGui.PushStyleColor(ImGuiCol.Text, new System.Numerics.Vector4(0.78f, 0.78f, 0.78f, 1f));
                ImGui.TextWrapped(entry.Answer);
                ImGui.PopStyleColor();
                ImGui.Separator();
            }
            if (matches == 0)
                ImGui.TextDisabled("No FAQ topics match this search.");
            ImGui.EndChild();
        }

        private void DrawDebugStatsTab()
        {
            ImGui.BeginChild("##debug_stats_full_height", System.Numerics.Vector2.Zero, ImGuiChildFlags.None);
            bool debugEnabled = Settings.DebugMode.Value;
            if (ImGui.Checkbox("Enable debug logging", ref debugEnabled))
                Settings.DebugMode.Value = debugEnabled;
            bool megaDebugEnabled = Settings.MegaDebugMode.Value;
            if (ImGui.Checkbox("Enable mega debug logging", ref megaDebugEnabled))
                Settings.MegaDebugMode.Value = megaDebugEnabled;
            bool showOverviewDebug = Settings.ShowBoardOverview.Value;
            if (ImGui.Checkbox("Show Betrayal Board Overview Window", ref showOverviewDebug))
                Settings.ShowBoardOverview.Value = showOverviewDebug;

            var panel = GameController?.Game?.IngameState?.IngameUi?.SyndicatePanel;
            bool panelVisible = panel?.IsVisible ?? false;
            ImGui.Separator();

            // ── STRATEGIC BOARD GOAL DASHBOARD ─────────────────────────────────────────────
            string modeName = Settings.FarmingMode == FarmingMode.CatarinaFocus ? "Catarina Focus" : "Safehouse Rewards (5-5-2-2)";
            ImGui.PushStyleColor(ImGuiCol.Text, new System.Numerics.Vector4(1f, 0.85f, 0.2f, 1f));
            ImGui.Text($"STRATEGY: {modeName.ToUpper()}");
            ImGui.PopStyleColor();
            ImGui.SameLine();
            ImGui.TextDisabled($"| Syndicate panel: {(panelVisible ? "VISIBLE" : "CLOSED")} | Cached: {ListBetrayalMasters.Count}/14");

            // Calculate strategic KPIs
            int readyFarmCount = ListBetrayalMasters.Count(m => m.GoalStatus == "READY" && m.Score >= 30);
            int needsRankFarmCount = ListBetrayalMasters.Count(m => m.GoalStatus == "NEEDS_RANK" && m.Score >= 30);
            int misplacedCount = ListBetrayalMasters.Count(m => m.GoalStatus == "MISPLACED");
            int outsideFarmCount = ListBetrayalMasters.Count(m => m.GoalStatus == "OUTSIDE" && !string.IsNullOrEmpty(m.TargetDivision) && Settings.DivisionRoles.TryGetValue(m.TargetDivision, out var tr) && tr == "Farm");

            ImGui.Spacing();
            ImGui.TextUnformatted("Goal Progress: ");
            ImGui.SameLine();
            ImGui.TextColored(new System.Numerics.Vector4(0.1f, 1f, 1f, 1f), $"[★ {readyFarmCount} Farm Ready (R3)]");
            ImGui.SameLine();
            ImGui.TextColored(new System.Numerics.Vector4(1f, 0.85f, 0.2f, 1f), $"[▲ {needsRankFarmCount} Need Rank]");
            ImGui.SameLine();
            ImGui.TextColored(new System.Numerics.Vector4(1f, 0.6f, 0.1f, 1f), $"[+ {outsideFarmCount} Farm Outside]");
            ImGui.SameLine();
            ImGui.TextColored(misplacedCount > 0 ? new System.Numerics.Vector4(1f, 0.25f, 0.25f, 1f) : new System.Numerics.Vector4(0.5f, 0.5f, 0.5f, 1f),
                $"[✗ {misplacedCount} Misplaced/Intruders]");

            // Immediate Action Guidance Banner
            ImGui.Spacing();
            if (misplacedCount > 0)
            {
                ImGui.PushStyleColor(ImGuiCol.Text, new System.Numerics.Vector4(1f, 0.35f, 0.35f, 1f));
                ImGui.TextWrapped($"[ACTION PRIORITY: REMOVE INTRUDERS] {misplacedCount} member(s) are misplaced or blocking Farm divisions! Primary action: INTERROGATE intruders down to Rank 0 / Outside.");
                ImGui.PopStyleColor();
            }
            else if (outsideFarmCount > 0)
            {
                ImGui.PushStyleColor(ImGuiCol.Text, new System.Numerics.Vector4(1f, 0.75f, 0.2f, 1f));
                ImGui.TextWrapped($"[ACTION PRIORITY: RECRUITMENT] {outsideFarmCount} Farm target(s) are Outside the syndicate! Accept encounters/bargains to recruit them into the board.");
                ImGui.PopStyleColor();
            }
            else if (needsRankFarmCount > 0)
            {
                ImGui.PushStyleColor(ImGuiCol.Text, new System.Numerics.Vector4(1f, 0.9f, 0.3f, 1f));
                ImGui.TextWrapped($"[ACTION PRIORITY: RANK UP] Farm targets are in correct divisions! EXECUTE them in encounters to reach Rank 3 (Captain).");
                ImGui.PopStyleColor();
            }
            else if (readyFarmCount >= 4)
            {
                ImGui.PushStyleColor(ImGuiCol.Text, new System.Numerics.Vector4(0.2f, 1f, 0.4f, 1f));
                ImGui.TextWrapped($"[BOARD OPTIMAL: READY TO RAID] All Farm targets are Rank 3! Run your Farm safehouses to collect maximum rewards!");
                ImGui.PopStyleColor();
            }
            ImGui.Separator();

            int relationshipRefs = ListBetrayalMasters.Sum(m => m.RelationshipStatuses.Count);
            int rivalRefs = ListBetrayalMasters.Sum(m => m.RelationshipStatuses.Values.Count(x => x == "Rival"));
            int trustedRefs = ListBetrayalMasters.Sum(m => m.RelationshipStatuses.Values.Count(x => x == "Trusted"));
            ImGui.Text($"Relationship topology: {relationshipRefs / 2} unique links ({rivalRefs / 2} Rival, {trustedRefs / 2} Trusted) | Scoring: {_relationshipNormalization}");

            if (ImGui.CollapsingHeader("Relationship topology", ImGuiTreeNodeFlags.None))
            {
                var uniqueRelationships = GetUniqueRelationships().ToList();
                if (ImGui.BeginTable("##debug_relationships", 4,
                    ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY,
                    new System.Numerics.Vector2(0, Math.Min(220f, 28f + uniqueRelationships.Count * 22f))))
                {
                    ImGui.TableSetupColumn("Member A");
                    ImGui.TableSetupColumn("Member B");
                    ImGui.TableSetupColumn("Status");
                    ImGui.TableSetupColumn("Strategic role");
                    ImGui.TableHeadersRow();
                    foreach (var link in uniqueRelationships)
                    {
                        ImGui.TableNextRow();
                        ImGui.TableSetColumnIndex(0); ImGui.TextUnformatted(link.First);
                        ImGui.TableSetColumnIndex(1); ImGui.TextUnformatted(link.Second);
                        ImGui.TableSetColumnIndex(2); ImGui.TextColored(
                            link.Status == "Rival"
                                ? new System.Numerics.Vector4(1f, 0.3f, 0.25f, 1f)
                                : new System.Numerics.Vector4(0.3f, 1f, 0.4f, 1f),
                            link.Status);
                        ImGui.TableSetColumnIndex(3); ImGui.TextUnformatted(GetRelationshipStrategicLabel(link.First, link.Second, link.Status));
                    }
                    ImGui.EndTable();
                }
            }

            foreach (var division in new[] { "Transportation", "Fortification", "Research", "Intervention" })
            {
                int count = ListBetrayalMasters.Count(m => m.Job == division);
                int ranks = ListBetrayalMasters.Where(m => m.Job == division).Sum(m => m.Rank);
                string role = Settings.DivisionRoles.TryGetValue(division, out var configuredRole) ? configuredRole : "?";
                bool run = ShouldRunDivision(division);
                ImGui.Text($"{division}: {count} members, {ranks} ranks, {role}, {(run ? "Run" : "Skip")}");
            }

            ImGui.Separator();
            ImGui.Text($"Current event action: {(_eventAction.Length == 0 ? "none" : _eventAction)}");
            ImGui.Text($"Current event member: {(_masterText.Length == 0 ? "none" : _masterText)}");
            if (_eventText.Length > 0)
                ImGui.TextWrapped($"Event text: {_eventText}");
            ImGui.Text($"Last recommendation: {(_lastRecommendedAction?.ToString() ?? "none")} ({_lastRecommendedScore:+#;-#;0})");
            ImGui.Text($"Interrogation queue at last decision: {(_lastInterrogationQueueFull.HasValue ? (_lastInterrogationQueueFull.Value ? "FULL (3/3)" : "space available") : "not checked")}");
            ImGui.Text($"Detected prisoner counter: {_lastInterrogationCounterText}");

            if (_lastOutcomes.Count > 0 && ImGui.BeginTable("##debug_outcomes", 4, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp))
            {
                ImGui.TableSetupColumn("Action");
                ImGui.TableSetupColumn("Type");
                ImGui.TableSetupColumn("Score");
                ImGui.TableSetupColumn("Description");
                ImGui.TableHeadersRow();
                foreach (var outcome in _lastOutcomes)
                {
                    ImGui.TableNextRow();
                    ImGui.TableSetColumnIndex(0); ImGui.TextUnformatted(outcome.Action.ToString());
                    ImGui.TableSetColumnIndex(1); ImGui.TextUnformatted(outcome.Type.ToString());
                    ImGui.TableSetColumnIndex(2); ImGui.TextUnformatted(outcome.Score.ToString());
                    ImGui.TableSetColumnIndex(3); ImGui.TextWrapped(outcome.Description);
                }
                ImGui.EndTable();
            }

            ImGui.Separator();
            ImGui.TextUnformatted("Syndicate Characters & Board Status");
            float membersTableHeight = Math.Max(260f, ImGui.GetContentRegionAvail().Y - 30f);
            if (ImGui.BeginTable("##debug_members", 9, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY | ImGuiTableFlags.Resizable, new System.Numerics.Vector2(0, membersTableHeight)))
            {
                ImGui.TableSetupColumn("Slot & Pos", ImGuiTableColumnFlags.WidthFixed, 135f);
                ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthFixed, 95f);
                ImGui.TableSetupColumn("Detected Div", ImGuiTableColumnFlags.WidthFixed, 115f);
                ImGui.TableSetupColumn("Target Div", ImGuiTableColumnFlags.WidthFixed, 95f);
                ImGui.TableSetupColumn("Rank", ImGuiTableColumnFlags.WidthFixed, 55f);
                ImGui.TableSetupColumn("Status", ImGuiTableColumnFlags.WidthFixed, 105f);
                ImGui.TableSetupColumn("Goal / Action Needed", ImGuiTableColumnFlags.WidthStretch, 2f);
                ImGui.TableSetupColumn("Score", ImGuiTableColumnFlags.WidthFixed, 45f);
                ImGui.TableSetupColumn("Topology", ImGuiTableColumnFlags.WidthFixed, 75f);
                ImGui.TableHeadersRow();

                foreach (var member in ListBetrayalMasters.OrderBy(m => m.Job == "None" ? "Z_Outside" : m.Job).ThenByDescending(m => m.IsLeader).ThenByDescending(m => m.Rank).ThenBy(m => m.Name))
                {
                    ImGui.TableNextRow();

                    // Col 0: Slot & Pos
                    ImGui.TableSetColumnIndex(0);
                    if (member.SlotIndex >= 0)
                        ImGui.TextUnformatted($"Slot #{member.SlotIndex} ({member.ScreenX:0}, {member.ScreenY:0})");
                    else
                        ImGui.TextDisabled("Not on board");

                    // Col 1: Name
                    ImGui.TableSetColumnIndex(1);
                    ImGui.TextUnformatted(member.Name);

                    // Col 2: Detected Div
                    ImGui.TableSetColumnIndex(2);
                    string divDisplay = member.Job == "None" ? "Outside" : member.Job;
                    if (member.IsLeader)
                        ImGui.TextColored(new System.Numerics.Vector4(1f, 0.85f, 0.2f, 1f), $"{divDisplay} [L]");
                    else
                        ImGui.TextUnformatted(divDisplay);

                    // Col 3: Target Div
                    ImGui.TableSetColumnIndex(3);
                    if (!string.IsNullOrEmpty(member.TargetDivision))
                    {
                        bool isFarm = Settings.DivisionRoles.TryGetValue(member.TargetDivision, out var tr) && tr == "Farm";
                        if (isFarm)
                            ImGui.TextColored(new System.Numerics.Vector4(0.4f, 1f, 0.4f, 1f), $"{member.TargetDivision} [F]");
                        else
                            ImGui.TextDisabled(member.TargetDivision);
                    }
                    else
                    {
                        ImGui.TextDisabled("(any filler)");
                    }

                    // Col 4: Rank
                    ImGui.TableSetColumnIndex(4);
                    if (member.Rank >= 3)
                        ImGui.TextColored(new System.Numerics.Vector4(0.1f, 1f, 1f, 1f), $"R{member.Rank} ★");
                    else if (member.Rank > 0)
                        ImGui.TextUnformatted($"R{member.Rank}");
                    else
                        ImGui.TextDisabled("R0");

                    // Col 5: Status Badge
                    ImGui.TableSetColumnIndex(5);
                    switch (member.GoalStatus)
                    {
                        case "READY":
                            ImGui.TextColored(new System.Numerics.Vector4(0.1f, 1f, 1f, 1f), "[★ READY]");
                            break;
                        case "NEEDS_RANK":
                            ImGui.TextColored(new System.Numerics.Vector4(1f, 0.85f, 0.2f, 1f), "[▲ NEED RANK]");
                            break;
                        case "CORRECT":
                            ImGui.TextColored(new System.Numerics.Vector4(0.2f, 1f, 0.3f, 1f), "[✓ CORRECT]");
                            break;
                        case "MISPLACED":
                            ImGui.TextColored(new System.Numerics.Vector4(1f, 0.25f, 0.25f, 1f), "[✗ MISPLACED]");
                            break;
                        case "OUTSIDE":
                            ImGui.TextColored(new System.Numerics.Vector4(1f, 0.6f, 0.1f, 1f), "[+ OUTSIDE]");
                            break;
                        default:
                            ImGui.TextColored(new System.Numerics.Vector4(0.65f, 0.65f, 0.65f, 1f), "[○ FILLER]");
                            break;
                    }

                    // Col 6: Goal / Action Needed
                    ImGui.TableSetColumnIndex(6);
                    if (member.GoalStatus == "MISPLACED")
                        ImGui.TextColored(new System.Numerics.Vector4(1f, 0.35f, 0.35f, 1f), member.GoalAction);
                    else if (member.GoalStatus == "READY")
                        ImGui.TextColored(new System.Numerics.Vector4(0.1f, 1f, 1f, 1f), member.GoalAction);
                    else if (member.GoalStatus == "NEEDS_RANK")
                        ImGui.TextColored(new System.Numerics.Vector4(1f, 0.9f, 0.3f, 1f), member.GoalAction);
                    else
                        ImGui.TextUnformatted(member.GoalAction);

                    // Col 7: Score
                    ImGui.TableSetColumnIndex(7);
                    if (member.Score >= 30)
                        ImGui.TextColored(new System.Numerics.Vector4(0.2f, 1f, 0.3f, 1f), member.Score.ToString());
                    else if (member.Score < 0)
                        ImGui.TextColored(new System.Numerics.Vector4(1f, 0.25f, 0.25f, 1f), member.Score.ToString());
                    else
                        ImGui.TextDisabled(member.Score.ToString());

                    // Col 8: Topology
                    ImGui.TableSetColumnIndex(8);
                    int memberRivals = member.RelationshipStatuses.Values.Count(x => x == "Rival");
                    int memberTrusted = member.RelationshipStatuses.Values.Count(x => x == "Trusted");
                    ImGui.TextUnformatted($"R{memberRivals} / T{memberTrusted}");
                }
                ImGui.EndTable();
            }

            if (!panelVisible)
                ImGui.TextDisabled("Open the Syndicate board to refresh live member statistics.");
            ImGui.EndChild();
        }

        public override void Render()
        {
            checkIntel();
            scanBetrayal();
            betrayalWindow();
        }



        /// <summary>
        /// Determines if a division's safehouse should be run when intel is full or ready.
        /// In CatarinaFocus mode: always run (every safehouse = chance at Syndicate Medallion).
        /// In SafehouseRewards mode: only Farm divisions are run.
        /// </summary>
        private bool ShouldRunDivision(string division)
        {
            if (Settings.FarmingMode == FarmingMode.CatarinaFocus)
                return true;

            return IsFarmDivision(division);
        }

        /// <summary>
        /// Returns true if this division is a primary farm target (actively push intel).
        /// Used to differentiate priority in UI and scoring.
        /// </summary>
        private bool IsFarmDivision(string division)
        {
            return Settings.DivisionRoles.TryGetValue(division, out var role) && role == "Farm";
        }

        private void UpdateReadySafehouses(Element boardContainer)
        {
            _readySafehouses.Clear();

            var divisionsToCheck = new[] { "Transportation", "Fortification", "Research", "Intervention" };
            var shIndexMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["Intervention"] = 0,
                ["Transportation"] = 1,
                ["Research"] = 2,
                ["Fortification"] = 3
            };

            foreach (var kv in ReadDivisionIntel())
                if (kv.Value >= 100 && ShouldRunDivision(kv.Key))
                    _readySafehouses.Add(kv.Key);

            foreach (var div in divisionsToCheck.Where(_ => false)) // legacy UI/rank signals disabled: real intel is read above
            {
                if (!ShouldRunDivision(div) || _readySafehouses.Contains(div))
                    continue;

                bool isReady = false;

                // Signal 2: In-game board safehouse UI element indicator
                try
                {
                    if (boardContainer != null && shIndexMap.TryGetValue(div, out int shIdx) && boardContainer.Children.Count > shIdx)
                    {
                        var shElem = boardContainer.Children[shIdx];
                        if (shElem != null && shElem.Children.Count > 0)
                        {
                            foreach (var ch in shElem.Children)
                            {
                                if (ch.IsVisible || ch.IsVisibleLocal)
                                {
                                    isReady = true;
                                    break;
                                }
                                string t = ch.Text ?? "";
                                if (t.IndexOf("100", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    t.IndexOf("Raid", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    t.IndexOf("Safehouse", StringComparison.OrdinalIgnoreCase) >= 0)
                                {
                                    isReady = true;
                                    break;
                                }
                            }
                        }
                    }
                }
                catch { }

                // Signal 3: Strategy readiness (All configured targets in this Farm division are present with Rank 3)
                try
                {
                    if (!isReady && Settings.DivisionMembers.TryGetValue(div, out var targets) && targets != null && targets.Count > 0)
                    {
                        var currentDivMembers = ListBetrayalMasters.Where(m => m.Job.Equals(div, StringComparison.OrdinalIgnoreCase)).ToList();
                        bool allTargetsPresentAndRank3 = targets.All(targetName =>
                        {
                            var m = currentDivMembers.FirstOrDefault(x => x.Name.Equals(targetName, StringComparison.OrdinalIgnoreCase));
                            return m != null && m.Rank >= 3;
                        });

                        if (allTargetsPresentAndRank3)
                        {
                            isReady = true;
                        }
                    }
                }
                catch { }

                if (isReady)
                {
                    _readySafehouses.Add(div);
                }
            }
        }

        private bool ShouldPreserveMemberPlacement(string division)
        {
            if (Settings.FarmingMode != FarmingMode.CatarinaFocus)
                return true;

            return Settings.CatarinaPreserveDivision.TryGetValue(division, out var preserve) && preserve;
        }

        private IEnumerable<KeyValuePair<string, List<string>>> GetRequiredMemberDivisions()
        {
            if (Settings.FarmingMode == FarmingMode.CatarinaFocus)
                return Settings.DivisionMembers.Where(kv => ShouldPreserveMemberPlacement(kv.Key));

            return Settings.DivisionMembers
                .Where(kv => Settings.DivisionRoles.TryGetValue(kv.Key, out var role) && role == "Farm");
        }

        /// <summary>
        /// Safehouse intelligence (0-100) per division, read from the board state vector.
        /// Verified live: leader state (first 4 entries) holds intel as int32 at +0x84
        /// (Tora 72 -> 78 after a +6 Intervention intelligence event).
        /// </summary>
        private Dictionary<string, int> ReadDivisionIntel()
        {
            var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            try
            {
                var mem = GameController?.Memory;
                var panel = GameController?.Game?.IngameState?.IngameUi?.SyndicatePanel;
                if (mem == null || panel == null || panel.Address == 0) return result;
                long start = mem.Read<long>(panel.Address + 0x3F8);
                long end = mem.Read<long>(panel.Address + 0x400);
                if (end <= start) return result;
                int count = (int)Math.Min(4, (end - start) / 160);
                for (int i = 0; i < count; i++)
                {
                    long state = start + i * 160;
                    long jobPtr = mem.Read<long>(state + 0x18);
                    long jobNamePtr = jobPtr != 0 ? mem.Read<long>(jobPtr + 0x08) : 0;
                    string job = jobNamePtr != 0 ? mem.ReadStringU(jobNamePtr) : "";
                    int intel = mem.Read<int>(state + 0x84);
                    if (!string.IsNullOrEmpty(job) && job != "None" && intel >= 0 && intel <= 100)
                        result[job] = intel;
                }
            }
            catch { }
            return result;
        }

        private void checkIntel()
        {
            var ingameState = GameController.Game.IngameState;
            var syndicatePanel = ingameState.IngameUi.SyndicatePanel;
            if (syndicatePanel == null || !syndicatePanel.IsVisible) return;
            var warnings = ReadDivisionIntel().Where(kv => kv.Value >= 100).Select(kv => kv.Key).ToList();

            if (GameController.Area.CurrentArea.Area.IsHideout && warnings.Count > 0 && !hasClosedWindow)
            {
                // In CatarinaFocus mode, treat ALL divisions as worth running (for medallion drops).
                // In SafehouseRewards mode, only Farm divisions matter for rewards.
                var farmJobs = GetRequiredMemberDivisions().ToDictionary(kv => kv.Key, kv => kv.Value);

                // Read current ranks of farm members from syndicate board
                var farmMemberInfo = new Dictionary<string, (string job, int rank)>();
                var syndicateStates = syndicatePanel.SyndicateStates;
                if (syndicateStates != null && syndicateStates.Count > 0)
                {
                    for (int i = 0; i < syndicateStates.Count; i++)
                    {
                        var state = syndicateStates[i];
                        string[] parts = state.ToString().Split(',');
                        if (parts.Length < 3) continue;
                        var stateName = parts[0].Trim();
                        var stateJob = parts[2].Trim();
                        var stateRank = state.Rank.RankInt;
                        bool stateIsLeader = i < 4;
                        var effectiveRank = stateRank;
                        if (farmJobs.Values.Any(members => members.Contains(stateName)))
                            farmMemberInfo[stateName] = (stateJob, effectiveRank);
                    }
                }
                else
                {
                    foreach (var m in ListBetrayalMasters)
                    {
                        if (farmJobs.Values.Any(members => members.Contains(m.Name)))
                            farmMemberInfo[m.Name] = (m.Job, m.Rank);
                    }
                }

                // Build safehouse advice per division
                var safeAdvice = new List<(string text, System.Numerics.Vector4 color)>();
                foreach (var warningJob in warnings)
                {
                    bool shouldRun = ShouldRunDivision(warningJob);

                    if (farmJobs.TryGetValue(warningJob, out var targetMembers))
                    {
                        var notReady = new List<string>();
                        var ready = new List<string>();
                        var wrongPlace = new List<string>();
                        foreach (var member in targetMembers)
                        {
                            if (farmMemberInfo.TryGetValue(member, out var info))
                            {
                                if (info.job != warningJob)
                                    wrongPlace.Add($"{member} (currently in {info.job})");
                                else if (info.rank >= 3)
                                    ready.Add($"{member} - Rank {info.rank}");
                                else
                                    notReady.Add($"{member} - Rank {info.rank} / 3");
                            }
                            else
                            {
                                wrongPlace.Add($"{member} (not on board)");
                            }
                        }

                        if (wrongPlace.Count == 0 && notReady.Count == 0)
                            safeAdvice.Add(($"[{warningJob}] >>> GO NOW! {string.Join(", ", ready)} <<<", new System.Numerics.Vector4(0f, 1f, 0f, 1f)));
                        else if (notReady.Count > 0)
                            safeAdvice.Add(($"[{warningJob}] WAIT - need rank 3: {string.Join(", ", notReady)}", new System.Numerics.Vector4(1f, 0.5f, 0f, 1f)));
                        else
                            safeAdvice.Add(($"[{warningJob}] FIX placement: {string.Join(", ", wrongPlace)}", new System.Numerics.Vector4(1f, 1f, 0f, 1f)));
                    }
                    else if (shouldRun)
                    {
                        // Filler division at 100% — opportunistic run (not primary farm target)
                        safeAdvice.Add(($"[{warningJob}] >>> RUN (opportunistic, filler at 100%) <<<", new System.Numerics.Vector4(0.8f, 0.8f, 0.2f, 1f)));
                    }
                    else
                    {
                        safeAdvice.Add(($"[{warningJob}] Skip (Filler division, not running)", new System.Numerics.Vector4(0.6f, 0.6f, 0.6f, 1f)));
                    }
                }

                // Show window when at least one division we should run is ready
                bool anyReady = safeAdvice.Any(a => a.text.Contains("GO NOW!") || a.text.Contains("RUN FOR MEDALLION!") || a.text.Contains("RUN (opportunistic"));
                if (!anyReady) return;

                ImGui.SetNextWindowPos(
                    new System.Numerics.Vector2(Settings.SafehouseWindowX, Settings.SafehouseWindowY),
                    ImGuiCond.Appearing);
                ImGui.SetNextWindowSize(
                    new System.Numerics.Vector2(Settings.SafehouseWindowWidth, Settings.SafehouseWindowHeight),
                    ImGuiCond.Appearing);

                // Link ImGui's open flag directly to your global one
                bool isOpen = !hasClosedWindow;
                string modeLabel = Settings.FarmingMode == FarmingMode.CatarinaFocus
                    ? "Catarina Focus"
                    : "Safehouse Rewards";
                // The text before ### may change, but the stable ID after it makes
                // both strategies share one window, position and size.
                string windowTitle = $"BetrayalHelper - Safehouse Ready###BetrayalHelperSafehousePlanner";

                if (ImGui.Begin(windowTitle, ref isOpen, ImGuiWindowFlags.NoCollapse))
                {
                    var windowPos = ImGui.GetWindowPos();
                    var windowSize = ImGui.GetWindowSize();
                    Settings.SafehouseWindowX = (int)windowPos.X;
                    Settings.SafehouseWindowY = (int)windowPos.Y;
                    Settings.SafehouseWindowWidth = (int)windowSize.X;
                    Settings.SafehouseWindowHeight = (int)windowSize.Y;

                    ImGui.PushStyleColor(ImGuiCol.Text, new System.Numerics.Vector4(0.95f, 0.72f, 0.18f, 1f));
                    ImGui.Text("SAFEHOUSE READY");
                    ImGui.PopStyleColor();
                    ImGui.SameLine();
                    ImGui.TextDisabled($"Strategy: {modeLabel}");

                    ImGui.TextWrapped("Ready divisions: " + string.Join(", ", warnings));

                    if (Settings.FarmingMode == FarmingMode.CatarinaFocus)
                    {
                        ImGui.PushStyleColor(ImGuiCol.Text, new System.Numerics.Vector4(0.4f, 0.8f, 1f, 1f));
                        ImGui.TextWrapped("Run every ready safehouse to pursue Syndicate Medallions and Catarina access.");
                        ImGui.PopStyleColor();
                    }

                    ImGui.Spacing();
                    ImGui.Separator();
                    ImGui.TextDisabled("RECOMMENDATION");
                    foreach (var (text, color) in safeAdvice)
                    {
                        ImGui.PushStyleColor(ImGuiCol.Text, color);
                        ImGui.TextWrapped(text);
                        ImGui.PopStyleColor();
                    }
                    ImGui.Separator();
                    ImGui.Spacing();
                    ImGui.TextDisabled("TARGET MEMBER READINESS");
                    foreach (var kvp in farmJobs)
                    {
                        var divName = kvp.Key;
                        var divMembers = kvp.Value;
                        int ready = 0;
                        var details = new List<string>();
                        foreach (var member in divMembers)
                        {
                            if (farmMemberInfo.TryGetValue(member, out var info))
                            {
                                if (info.job != divName)
                                    details.Add($"{member}: wrong division ({info.job})");
                                else if (info.rank >= 3)
                                { details.Add($"{member}: Rank {info.rank} / 3 - ready"); ready++; }
                                else
                                    details.Add($"{member}: Rank {info.rank} / 3");
                            }
                            else
                                details.Add($"{member}: no board data");
                        }
                        float progress = divMembers.Count > 0 ? (float)ready / divMembers.Count : 0f;
                        string barLabel = $"{divName} - {ready} of {divMembers.Count} ready";
                        ImGui.ProgressBar(progress, new System.Numerics.Vector2(-1, 0), barLabel);
                        ImGui.TextWrapped(string.Join("   |   ", details));
                        ImGui.Spacing();
                    }
                    ImGui.Spacing();

                    if (ImGui.Button("Dismiss until area change"))
                    {
                        hasClosedWindow = true;
                    }
                }
                ImGui.End();

                // If user clicked the titlebar X, also close it
                if (!isOpen)
                {
                    hasClosedWindow = true;
                }
            }



        }
        
        private void scanBetrayal()
        {

            var ingameState = GameController.Game.IngameState;

            if (!ingameState.IngameUi.SyndicatePanel.IsVisible)
            {

            
            List<BetrayalMaster> masters = new List<BetrayalMaster>();


            var entities = GameController.EntityListWrapper.ValidEntitiesByType[EntityType.BetrayalChoice];

            var betrayalLabels = GameController?.Game?.IngameState?.IngameUi?.ItemsOnGroundLabels
                    .Where(x => x != null && x.IsVisible && x.Label != null && x.Label.IsValid &&
                            x.Label.IsVisible && x.ItemOnGround != null &&
                            x.ItemOnGround.Metadata != null &&
                            x.ItemOnGround.Metadata.Equals("Metadata/Terrain/Leagues/Betrayal/Objects/BetrayalMakeChoice", StringComparison.OrdinalIgnoreCase));
                       


            if (entities.Count() > 0)
            {
                updateSyndicateInfo();

                // Collect all visible NPCs with rects and scores, then sort by priority
                var visibleNpcs = new List<(BetrayalMaster master, RectangleF frameRect, RectangleF nameRect)>();
                foreach (var label in betrayalLabels)
                {
                    var nameElement = label.Label.GetChildFromIndices(0, 0);
                    if (nameElement == null) continue;
                    var aux = nameElement.Text;
                    if (string.IsNullOrEmpty(aux)) continue;
                    var master = ListBetrayalMasters.FirstOrDefault(m => m.Name == aux);
                    if (master is not null)
                        visibleNpcs.Add((master, label.Label.GetClientRect(), nameElement.GetClientRect()));
                }

                // Handle already-connected members first and leave relationship-free
                // members until last, improving the remaining member's chance to
                // receive a useful new relationship option.
                visibleNpcs = visibleNpcs
                    .OrderBy(x => x.master.RelationshipStatuses.Count == 0)
                    .ThenByDescending(x => x.master.RelationshipStatuses.Values.Count(status => status == "Rival"))
                    .ThenByDescending(x => x.master.Score)
                    .ThenByDescending(x => x.master.RelationshipStatuses.Count)
                    .ToList();

                for (int i = 0; i < visibleNpcs.Count; i++)
                {
                    var (master, rect, nameRect) = visibleNpcs[i];

                    Color frameColor;
                    int thickness;
                    if (master.Score >= 30)
                    { frameColor = Color.Green; thickness = 6; }
                    else if (master.Score >= 1)
                    { frameColor = Color.Yellow; thickness = 4; }
                    else if ((master.Job == "None" || string.IsNullOrEmpty(master.Job))
                        && !string.IsNullOrEmpty(master.TargetDivision)
                        && Settings.DivisionRoles.TryGetValue(master.TargetDivision, out var recruitRole) && recruitRole == "Farm")
                    { frameColor = Color.Orange; thickness = 4; } // Farm target still outside: recruit
                    else
                    { frameColor = Color.Red; thickness = 4; }

                    // Fill inside for first NPC (= click this one first)
                    if (i == 0)
                    {
                        var fillColor = new Color(frameColor.R, frameColor.G, frameColor.B, (byte)60);
                        Graphics.DrawBox(rect, fillColor);
                    }

                    Graphics.DrawFrame(rect, frameColor, thickness);

                    // Keep the priority label anchored to the actual name element,
                    // not the much larger interaction-label container.
                    var priorityPos = new Vector2N(nameRect.X, Math.Max(0, nameRect.Y - 50));
                    var priorityShadowPos = new Vector2N(priorityPos.X + 2, priorityPos.Y + 2);
                    var overlayDrawList = ImGui.GetForegroundDrawList();
                    uint shadowColor = ImGui.ColorConvertFloat4ToU32(new System.Numerics.Vector4(0f, 0f, 0f, 0.9f));
                    uint priorityColor = ImGui.ColorConvertFloat4ToU32(new System.Numerics.Vector4(
                        frameColor.R / 255f, frameColor.G / 255f, frameColor.B / 255f, 1f));
                    if (Settings.ShowPriorityOrder.Value)
                    unsafe
                    {
                        overlayDrawList.AddText(ImGui.GetFont(), 44f, priorityShadowPos, shadowColor, $"#{i + 1}");
                        overlayDrawList.AddText(ImGui.GetFont(), 44f, priorityPos, priorityColor, $"#{i + 1}");

                        var scorePos = new Vector2N(nameRect.X + 62, Math.Max(0, nameRect.Y - 26));
                        overlayDrawList.AddText(ImGui.GetFont(), 16f, scorePos, priorityColor, $"Score {master.Score}");
                    }
                }
            }
            }


        }
        private void betrayalWindow()
        {
            var ingameState = GameController?.Game?.IngameState;
            var syndicatePanel = ingameState?.IngameUi?.SyndicatePanel;

            if (syndicatePanel?.IsVisible == true)
            {
                DrawSyndicateBoard(syndicatePanel);
            }
            else
            {
                _closedOverviewForCurrentPanel = false;
                // A later encounter may have identical text and member names but
                // a different prisoner count, so invalidate between panel opens.
                _interrogationQueueEventKey = "";
            }
        }
        private void DrawBottomBoardGui()
        {
            if (!Settings.ShowBoardOverview.Value || _closedOverviewForCurrentPanel)
                return;

            var syndicatePanel = GameController?.Game?.IngameState?.IngameUi?.SyndicatePanel;
            if (syndicatePanel == null || !syndicatePanel.IsVisible)
                return;

            bool isOpen = true;
            ImGui.SetNextWindowSize(new Vector2N(920, 420), ImGuiCond.FirstUseEver);

            if (!ImGui.Begin("BetrayalHelper - Board Overview###BetrayalBoardOverview", ref isOpen, ImGuiWindowFlags.None))
            {
                ImGui.End();
                if (!isOpen)
                    _closedOverviewForCurrentPanel = true;
                return;
            }

            if (!isOpen)
            {
                _closedOverviewForCurrentPanel = true;
                ImGui.End();
                return;
            }

            var transportationBetrayalMasters = ListBetrayalMasters.Where(b => b.Job == "Transportation")
                .OrderByDescending(b => b.IsLeader).ThenByDescending(b => b.Rank).ToList();
            var fortificationBetrayalMasters = ListBetrayalMasters.Where(b => b.Job == "Fortification")
                .OrderByDescending(b => b.IsLeader).ThenByDescending(b => b.Rank).ToList();
            var researchBetrayalMasters = ListBetrayalMasters.Where(b => b.Job == "Research")
                .OrderByDescending(b => b.IsLeader).ThenByDescending(b => b.Rank).ToList();
            var interventionBetrayalMasters = ListBetrayalMasters.Where(b => b.Job == "Intervention")
                .OrderByDescending(b => b.IsLeader).ThenByDescending(b => b.Rank).ToList();

            var outsideMasters = ListBetrayalMasters.Where(b => b.Job == "None" || string.IsNullOrEmpty(b.Job))
                .OrderByDescending(b => b.Score).ThenBy(b => b.Name).ToList();

            var interestingMasters = Settings.DivisionMembers
                .Where(kv => Settings.DivisionRoles.TryGetValue(kv.Key, out var r) && r == "Farm")
                .SelectMany(kv => kv.Value)
                .Distinct()
                .ToList();

            var allLists = new List<BetrayalMaster>[] {
                transportationBetrayalMasters,
                fortificationBetrayalMasters,
                researchBetrayalMasters,
                interventionBetrayalMasters
            };

            var headers = new string[] { "Transportation", "Fortification", "Research", "Intervention" };
            var divIntel = ReadDivisionIntel();

            string modeName = Settings.FarmingMode == FarmingMode.CatarinaFocus ? "Catarina Focus" : "Safehouse Rewards";
            ImGui.TextColored(new System.Numerics.Vector4(0.95f, 0.72f, 0.18f, 1f), "BETRAYAL BOARD OVERVIEW");
            ImGui.SameLine();
            ImGui.TextDisabled($"| Mode: {modeName} | On board: {ListBetrayalMasters.Count}/{allMasters.Count}");
            var offBoard = allMasters.Where(n => !ListBetrayalMasters.Any(m => m.Name == n)).ToList();
            if (offBoard.Count > 0)
                ImGui.TextColored(new System.Numerics.Vector4(0.9f, 0.6f, 0.2f, 1f), "Not on board (prison / absent): " + string.Join(", ", offBoard));
            // TextUnformatted: '%' in TextDisabled would be treated as a printf format.
            ImGui.PushStyleColor(ImGuiCol.Text, new System.Numerics.Vector4(0.6f, 0.6f, 0.6f, 1f));
            ImGui.PushTextWrapPos(0f);
            ImGui.TextUnformatted("Atlas: Enduring Legacy " + (Settings.EnduringLegacy.Value ? "ON" : "off")
                + " (extra rank on Execute: " + (int)Math.Round(ExtraRankChance * 100) + " pct) | Unconditional Release "
                + (Settings.UnconditionalRelease.Value ? "ON" : "off") + " (keep rank on Interrogate: " + (int)Math.Round(KeepRankChance * 100) + " pct)");
            ImGui.PopTextWrapPos();
            ImGui.PopStyleColor();
            ImGui.Separator();

            // ── SAFEHOUSE READY ALERT BANNER ──────────────────────────────────────────
            var boardContainer = syndicatePanel?.Children?.Count > 0 ? syndicatePanel.Children[0] : null;
            UpdateReadySafehouses(boardContainer);

            if (_readySafehouses.Count > 0)
            {
                foreach (var readyDiv in _readySafehouses)
                {
                    ImGui.PushStyleColor(ImGuiCol.Button, new System.Numerics.Vector4(0.85f, 0.15f, 0.15f, 1f));
                    ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new System.Numerics.Vector4(0.95f, 0.25f, 0.25f, 1f));
                    ImGui.PushStyleColor(ImGuiCol.ButtonActive, new System.Numerics.Vector4(1f, 0.35f, 0.35f, 1f));
                    ImGui.PushStyleColor(ImGuiCol.Text, new System.Numerics.Vector4(1f, 1f, 1f, 1f));

                    string alertText = $"RUN {readyDiv.ToUpper()} SAFEHOUSE NOW - intel 100";
                    ImGui.Button($"  ***  {alertText}  ***  ###alert_{readyDiv}", new System.Numerics.Vector2(-1, 36));

                    ImGui.PopStyleColor(4);
                    ImGui.Spacing();
                }
            }

            // Main table with 6 dedicated columns: 4 divisions + Outside + Targets
            if (ImGui.BeginTable("MasterTables", 4, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.Resizable))
            {
                ImGui.TableSetupColumn("Transportation", ImGuiTableColumnFlags.WidthStretch, 1.0f);
                ImGui.TableSetupColumn("Fortification", ImGuiTableColumnFlags.WidthStretch, 1.0f);
                ImGui.TableSetupColumn("Research", ImGuiTableColumnFlags.WidthStretch, 1.0f);
                ImGui.TableSetupColumn("Intervention", ImGuiTableColumnFlags.WidthStretch, 1.0f);

                ImGui.TableNextRow();

                // 4 Division columns
                for (int t = 0; t < allLists.Length; t++)
                {
                    ImGui.TableSetColumnIndex(t);

                    bool divIsFarm = Settings.DivisionRoles.TryGetValue(headers[t], out var divRole) && divRole == "Farm";
                    int divTarget = divIsFarm ? 2 : 5;
                    var divLeader = allLists[t].FirstOrDefault(m => m.IsLeader);
                    string divLabel = headers[t];
                    ImGui.PushStyleColor(ImGuiCol.Text, divIsFarm
                        ? new System.Numerics.Vector4(0.4f, 1f, 0.4f, 1f)
                        : new System.Numerics.Vector4(0.7f, 0.7f, 0.7f, 1f));
                    ImGui.TextUnformatted(divLabel);
                    ImGui.PopStyleColor();
                    divIntel.TryGetValue(headers[t], out int intelPct);
                    ImGui.TextDisabled($"{(divIsFarm ? "FARM" : "FILLER")} {allLists[t].Count}/{divTarget}");
                    ImGui.ProgressBar(intelPct / 100f, new System.Numerics.Vector2(-1, ImGui.GetTextLineHeight() + 6f), "Intel " + intelPct);
                    ImGui.Spacing();
                    ImGui.TextDisabled($"Leader: {divLeader?.Name ?? "?"} (highlighted)");
                    ImGui.Separator();

                    if (ImGui.BeginTable($"TableDiv_{t}", 3, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp))
                    {
                        ImGui.TableSetupColumn("R", ImGuiTableColumnFlags.WidthStretch, 0.45f);
                        ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch, 2.0f);
                        ImGui.TableSetupColumn("Goal", ImGuiTableColumnFlags.WidthStretch, 1.1f);
                        ImGui.TableHeadersRow();

                        if (allLists[t].Count == 0)
                        {
                            ImGui.TableNextRow();
                            ImGui.TableSetColumnIndex(0);
                            ImGui.TextDisabled("-");
                            ImGui.TableSetColumnIndex(1);
                            ImGui.TextDisabled("(empty)");
                            ImGui.TableSetColumnIndex(2);
                            ImGui.TextDisabled("-");
                        }
                        else
                        {
                            foreach (var master in allLists[t])
                            {
                                ImGui.TableNextRow();
                                if (master.IsLeader)
                                    ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg0, ImGui.ColorConvertFloat4ToU32(new System.Numerics.Vector4(0.55f, 0.42f, 0.05f, 0.55f)));
                                ImGui.TableSetColumnIndex(0);
                                ImGui.Text($"{master.Rank}");

                                ImGui.TableSetColumnIndex(1);
                                System.Numerics.Vector4 color;
                                if (master.Score >= 30)
                                    color = master.Rank >= 3
                                        ? new System.Numerics.Vector4(0.1f, 1f, 1f, 1f)     // Ready (Cyan)
                                        : new System.Numerics.Vector4(0.2f, 1f, 0.3f, 1f);   // Need Rank (Green)
                                else if (master.Score >= 1)
                                    color = new System.Numerics.Vector4(0.8f, 0.8f, 0.8f, 1f); // Filler (Light Gray)
                                else
                                    color = new System.Numerics.Vector4(1f, 0.3f, 0.3f, 1f);    // Intruder / Misplaced (Red)

                                ImGui.TextColored(color, master.Name);

                                if (ImGui.IsItemHovered())
                                {
                                    string roleInfo = master.IsLeader ? "Leader" : "Member";
                                    string tip = $"{master.Name} ({headers[t]} {roleInfo}, Rank {master.Rank})";
                                    if (!string.IsNullOrEmpty(master.GoalAction))
                                        tip += $"\nStatus: {master.GoalAction}";
                                    if (master.SlotIndex >= 0)
                                        tip += $"\nBoard Slot #{master.SlotIndex} at ({master.ScreenX:0}, {master.ScreenY:0})";
                                    ImGui.SetTooltip(tip);
                                }

                                ImGui.TableSetColumnIndex(2);
                                if (string.IsNullOrEmpty(master.TargetDivision))
                                    ImGui.TextDisabled("-");
                                else if (master.TargetDivision == headers[t])
                                    ImGui.TextColored(new System.Numerics.Vector4(0.3f, 1f, 0.3f, 1f), "OK");
                                else
                                    ImGui.TextColored(new System.Numerics.Vector4(1f, 0.4f, 0.3f, 1f), "> " + master.TargetDivision.Substring(0, 3));
                            }
                        }

                        ImGui.EndTable();
                    }
                }

                ImGui.EndTable();

                // Second row: Outside + Farm Targets, below the divisions
                ImGui.Spacing();
                ImGui.BeginTable("LowerTables", 2, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.Resizable);
                ImGui.TableSetupColumn("Outside", ImGuiTableColumnFlags.WidthStretch, 1.0f);
                ImGui.TableSetupColumn("Targets", ImGuiTableColumnFlags.WidthStretch, 1.0f);
                ImGui.TableNextRow();
                ImGui.TableSetColumnIndex(0);
                ImGui.TextUnformatted($"Outside ({outsideMasters.Count})");
                ImGui.Separator();

                if (ImGui.BeginTable("TableOutside", 2, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
                {
                    ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch);
                    ImGui.TableSetupColumn("Action", ImGuiTableColumnFlags.WidthFixed, 70f);
                    ImGui.TableHeadersRow();

                    if (outsideMasters.Count == 0)
                    {
                        ImGui.TableNextRow();
                        ImGui.TableSetColumnIndex(0);
                        ImGui.TextDisabled("(none)");
                        ImGui.TableSetColumnIndex(1);
                        ImGui.TextDisabled("-");
                    }
                    else
                    {
                        foreach (var outsideMaster in outsideMasters)
                        {
                            ImGui.TableNextRow();
                            ImGui.TableSetColumnIndex(0);

                            bool isFarmTarget = !string.IsNullOrEmpty(outsideMaster.TargetDivision)
                                && Settings.DivisionRoles.TryGetValue(outsideMaster.TargetDivision, out var tr) && tr == "Farm";

                            var color = isFarmTarget
                                ? new System.Numerics.Vector4(1f, 0.6f, 0.1f, 1f)   // Farm target outside (Orange)
                                : new System.Numerics.Vector4(0.7f, 0.7f, 0.7f, 1f); // Neutral filler (Gray)

                            ImGui.TextColored(color, outsideMaster.Name);

                            if (ImGui.IsItemHovered())
                            {
                                string tip = isFarmTarget
                                    ? $"{outsideMaster.Name} (Farm Target for {outsideMaster.TargetDivision})\nAction: Recruit when appears in encounter"
                                    : $"{outsideMaster.Name} (Reserve filler)";
                                ImGui.SetTooltip(tip);
                            }

                            ImGui.TableSetColumnIndex(1);
                            if (isFarmTarget)
                                ImGui.TextColored(color, "Recruit");
                            else
                                ImGui.TextDisabled("Reserve");
                        }
                    }

                    ImGui.EndTable();
                }

                ImGui.TableSetColumnIndex(1);
                ImGui.TextUnformatted("Farm Targets");
                ImGui.Separator();

                if (ImGui.BeginTable("TableTargets", 2, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
                {
                    ImGui.TableSetupColumn("Target", ImGuiTableColumnFlags.WidthStretch, 1.0f);
                    ImGui.TableSetupColumn("Status", ImGuiTableColumnFlags.WidthStretch, 1.2f);
                    ImGui.TableHeadersRow();

                    if (interestingMasters.Count == 0)
                    {
                        ImGui.TableNextRow();
                        ImGui.TableSetColumnIndex(0);
                        ImGui.TextDisabled("(none)");
                        ImGui.TableSetColumnIndex(1);
                        ImGui.TextDisabled("-");
                    }
                    else
                    {
                        foreach (var interestingmaster in interestingMasters)
                        {
                            ImGui.TableNextRow();
                            ImGui.TableSetColumnIndex(0);

                            var master = ListBetrayalMasters.FirstOrDefault(m => m.Name == interestingmaster);
                            var score = master?.Score ?? 0;
                            var job = (master == null || master.Job == "None" || string.IsNullOrEmpty(master.Job)) ? "Outside" : master.Job;
                            var rank = master?.Rank ?? 0;
                            var targetDiv = master?.TargetDivision ?? "";
                            if (string.IsNullOrEmpty(targetDiv))
                            {
                                targetDiv = Settings.DivisionMembers.FirstOrDefault(kv => kv.Value != null && kv.Value.Contains(interestingmaster)).Key ?? "";
                            }

                            System.Numerics.Vector4 color;
                            string statusText;
                            if (score >= 30)
                            {
                                if (rank >= 3)
                                {
                                    color = new System.Numerics.Vector4(0.1f, 1f, 1f, 1f);  // Cyan
                                    statusText = $"{job} R3";
                                }
                                else
                                {
                                    color = new System.Numerics.Vector4(0.2f, 1f, 0.3f, 1f); // Green
                                    statusText = $"{job} R{rank}";
                                }
                            }
                            else if (master != null && master.Job != "None" && !string.IsNullOrEmpty(master.Job))
                            {
                                color = new System.Numerics.Vector4(1f, 0.3f, 0.3f, 1f);     // Red
                                statusText = $"{job} (Wrong)";
                            }
                            else
                            {
                                color = new System.Numerics.Vector4(1f, 0.6f, 0.1f, 1f);     // Orange
                                statusText = "Outside";
                            }

                            ImGui.TextColored(color, interestingmaster);

                            if (ImGui.IsItemHovered())
                            {
                                string tip = $"Target: {targetDiv}\nCurrent: {job} (Rank {rank})\nAction: {master?.GoalAction ?? "Recruit"}";
                                ImGui.SetTooltip(tip);
                            }

                            ImGui.TableSetColumnIndex(1);
                            ImGui.TextColored(color, statusText);
                        }
                    }

                    ImGui.EndTable();
                }

                ImGui.EndTable();
            }

            ImGui.Spacing();
            ImGui.Separator();
            if (ImGui.Button("Close", new Vector2N(80, 24)))
            {
                _closedOverviewForCurrentPanel = true;
            }
            ImGui.SameLine();
            if (ImGui.Button("Refresh Board", new Vector2N(130, 24)))
            {
                updateSyndicateInfo();
            }
            ImGui.SameLine();
            ImGui.TextDisabled($"Refresh re-reads the board | {ListBetrayalMasters.Count} members cached");

            ImGui.End();
        }

        private List<BetrayalMaster> BetrayalEventMembers()
        {
            List<BetrayalMaster> masters = new List<BetrayalMaster>();

            var entities = GameController.EntityListWrapper.ValidEntitiesByType[EntityType.BetrayalChoice];

            foreach (var entity in entities)
            {
                var stateComp = entity.GetComponent<StateMachine>();
                if (stateComp?.States == null) continue;
                var auxmaster = stateComp.States.FirstOrDefault(state => state.Name=="NPCIndex");
                if (auxmaster == null) continue;
                var npcindex = (int)auxmaster.Value;
                if (GameController.Files?.BetrayalTargets?.EntriesList == null) continue;
                if (npcindex < 0 || npcindex >= GameController.Files.BetrayalTargets.EntriesList.Count) continue;
                var aux = GameController.Files.BetrayalTargets.EntriesList[npcindex];
                if (aux == null) continue;
                var aux2 = ListBetrayalMasters.FirstOrDefault(master => master.Name == aux.Name);
                if (aux2 != null) 
                    masters.Add(aux2);
                else
                    masters.Add(new BetrayalMaster(aux.Name, "None", 0, false, new List<string>(), GetScore(aux.Name, "None", 0)));
            }


            return masters;
        }
        private void updateSyndicateInfo()
        {
            var ingameState = GameController?.Game?.IngameState;
            var syndicatePanel = ingameState?.IngameUi?.SyndicatePanel;
            if (syndicatePanel == null) return;

            var relationshipMap = ingameState?.ServerData?.BetrayalMemberRelationships;
            var liveRelationships = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            if (relationshipMap != null)
            {
                try
                {
                    foreach (var relationshipEntry in relationshipMap)
                    {
                        if (relationshipEntry.Key?.Name is not { Length: > 0 } memberName || relationshipEntry.Value == null)
                            continue;

                        var memberLinks = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        foreach (var relation in relationshipEntry.Value)
                        {
                            if (relation.Key?.Name is { Length: > 0 } relatedName)
                                memberLinks[relatedName] = relation.Value.ToString();
                        }
                        liveRelationships[memberName] = memberLinks;
                    }
                }
                catch { }
            }

            var newMasters = new List<BetrayalMaster>();
            var mem = GameController.Memory;

            // Direct Memory Reader for Immortal Syndicate States (offset 0x3F8 / 0x400 in SyndicatePanel)
            try
            {
                long panelAddr = syndicatePanel.Address;
                if (panelAddr != 0 && mem != null)
                {
                    long statesStart = mem.Read<long>(panelAddr + 0x3F8);
                    long statesEnd = mem.Read<long>(panelAddr + 0x400);
                    if (statesEnd > statesStart && (statesEnd - statesStart) % 160 == 0)
                    {
                        int stateCount = (int)((statesEnd - statesStart) / 160);
                        for (int i = 0; i < stateCount; i++)
                        {
                            long stateAddr = statesStart + i * 160;
                            long targetPtr = mem.Read<long>(stateAddr + 0x08);
                            long jobPtr = mem.Read<long>(stateAddr + 0x18);
                            long rankPtr = mem.Read<long>(stateAddr + 0x28);

                            long namePtr = targetPtr != 0 ? mem.Read<long>(targetPtr) : 0;
                            string name = namePtr != 0 ? mem.ReadStringU(namePtr) : "";
                            if (name.Equals("ItThatFled", StringComparison.OrdinalIgnoreCase))
                                name = "It That Fled";

                            long jobNamePtr = jobPtr != 0 ? mem.Read<long>(jobPtr + 0x08) : 0;
                            string job = jobNamePtr != 0 ? mem.ReadStringU(jobNamePtr) : "None";

                            int rank = rankPtr != 0 ? mem.Read<int>(rankPtr + 0x10) : 0;
                            bool isLeader = i < 4;

                            if (string.IsNullOrEmpty(name) || !allMasters.Contains(name))
                                continue;

                            var typedRelationships = liveRelationships.TryGetValue(name, out var savedRelationships)
                                ? new Dictionary<string, string>(savedRelationships, StringComparer.OrdinalIgnoreCase)
                                : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                            if (typedRelationships.Count == 0)
                            {
                                try
                                {
                                    long relStart = mem.Read<long>(stateAddr + 0x50);
                                    long relEnd = mem.Read<long>(stateAddr + 0x58);
                                    if (relEnd > relStart && (relEnd - relStart) % 8 == 0)
                                    {
                                        int relCount = (int)((relEnd - relStart) / 8);
                                        for (int r = 0; r < relCount && r < 20; r++)
                                        {
                                            long otherState = mem.Read<long>(relStart + r * 8);
                                            if (otherState != 0)
                                            {
                                                long otherTarget = mem.Read<long>(otherState + 0x08);
                                                long otherNamePtr = otherTarget != 0 ? mem.Read<long>(otherTarget) : 0;
                                                string otherName = otherNamePtr != 0 ? mem.ReadStringU(otherNamePtr) : "";
                                                if (otherName.Equals("ItThatFled", StringComparison.OrdinalIgnoreCase))
                                                    otherName = "It That Fled";

                                                if (!string.IsNullOrEmpty(otherName) && allMasters.Contains(otherName) && !typedRelationships.ContainsKey(otherName))
                                                {
                                                    typedRelationships[otherName] = "Neutral";
                                                }
                                            }
                                        }
                                    }
                                }
                                catch { }
                            }

                            List<string> stateNames = typedRelationships.Keys.ToList();
                            int score = GetScore(name, job, rank);
                            var betrayalMaster = new BetrayalMaster(name, job, rank, isLeader, stateNames, score)
                            {
                                SlotIndex = i
                            };
                            betrayalMaster.RelationshipStatuses = typedRelationships;
                            UpdateMasterGoalStatus(betrayalMaster);

                            if (Settings.MegaDebugMode)
                                LogMessage(betrayalMaster.ToString(), 5, Color.Red);

                            newMasters.Add(betrayalMaster);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (Settings.DebugMode)
                    LogMessage($"Memory syndicate parse error: {ex.Message}", 5, Color.Red);
            }

            ExileCore.PoEMemory.Element boardContainer = null;

            // Fallback UI-tree parser ONLY if direct memory reading completely failed (0 masters)
            if (newMasters.Count == 0)
            {
                newMasters.Clear();
                try
                {
                    if (ingameState?.IngameUi?.Children != null)
                    {
                        long panelAddr = syndicatePanel.Address;
                        foreach (var uiChild in ingameState.IngameUi.Children)
                        {
                            if (uiChild != null && uiChild.Address == panelAddr && uiChild.Children?.Count > 0)
                            {
                                boardContainer = uiChild.Children[0];
                                break;
                            }
                        }
                    }
                    boardContainer ??= syndicatePanel.Children?.Count > 0 ? syndicatePanel.Children[0] : null;

                    if (boardContainer != null && boardContainer.Children?.Count > 0)
                    {
                        var rawCards = new List<(string Name, float X, float Y, float Width, int Rank, bool InDivision, bool HasLeaderFrame, int SlotIndex)>();
                        var boardRect = boardContainer.GetClientRectCache;
                        float boardLeft = boardRect.X;
                        float boardTop = boardRect.Y;
                        float boardWidth = boardRect.Width;
                        float boardHeight = boardRect.Height;

                        for (int c = 0; c < boardContainer.Children.Count; c++)
                        {
                            var card = boardContainer.Children[c];
                            if (card == null || !card.IsValid) continue;

                            string name = "";
                            try
                            {
                                if (card.Children.Count > 3 && card.Children[3].Children.Count > 0)
                                    name = card.Children[3].Children[0].Text?.Trim() ?? "";
                                if (string.IsNullOrEmpty(name) && card.Children.Count > 3)
                                    name = card.Children[3].Text?.Trim() ?? "";
                            }
                            catch { }

                            // Robust fallback search through children if name wasn't at [3][0]
                            if (string.IsNullOrEmpty(name) || !allMasters.Contains(name))
                            {
                                foreach (var sub in card.Children)
                                {
                                    var t = sub?.Text?.Trim();
                                    if (!string.IsNullOrEmpty(t) && allMasters.Contains(t)) { name = t; break; }
                                    if (sub?.Children != null)
                                    {
                                        foreach (var sub2 in sub.Children)
                                        {
                                            var t2 = sub2?.Text?.Trim();
                                            if (!string.IsNullOrEmpty(t2) && allMasters.Contains(t2)) { name = t2; break; }
                                        }
                                        if (!string.IsNullOrEmpty(name)) break;
                                    }
                                }
                            }

                            if (string.IsNullOrEmpty(name) || name.Equals("Catarina", StringComparison.OrdinalIgnoreCase) || !allMasters.Contains(name))
                                continue;

                            // Local position inside board container
                            float posX = 0, posY = 0, width = 0;
                            try
                            {
                                var pos = card.Position;
                                posX = pos.X;
                                posY = pos.Y;
                                width = card.Width;
                            }
                            catch { }

                            // Screen center position for percentage calculations
                            float screenX = posX, screenY = posY;
                            try
                            {
                                var rect = card.GetClientRectCache;
                                if (rect.Width > 0)
                                {
                                    screenX = rect.Center.X;
                                    screenY = rect.Center.Y;
                                    if (width <= 0) width = rect.Width;
                                }
                            }
                            catch { }

                            // Leaders have a larger frame (width ~324px vs ~283.5px for members) or Child[2] frame
                            bool hasLeaderFrame = (width > 300 && width < 345);
                            try
                            {
                                if (card.Children.Count > 2 && (card.Children[2].IsVisible || card.Children[2].IsVisibleLocal))
                                    hasLeaderFrame = true;
                            }
                            catch { }

                            // Detect rank directly from Child[6..18] stars
                            int rank = 0;
                            try
                            {
                                int maxStarIdx = -1;
                                for (int s = 6; s <= 18 && s < card.Children.Count; s++)
                                {
                                    if (card.Children[s].IsVisible || card.Children[s].IsVisibleLocal)
                                        maxStarIdx = s;
                                }
                                if (maxStarIdx >= 16) rank = 3;
                                else if (maxStarIdx >= 12) rank = 2;
                                else if (maxStarIdx >= 6) rank = 1;
                                else if (card.Children.Count > 5 && card.Children[5].Children.Count > 0)
                                    rank = Math.Clamp(card.Children[5].Children.Count, 1, 3);
                            }
                            catch { }

                            // Division detection: board members have posY < 1750 or screen relative Y < 0.75
                            bool inDivision = false;
                            if (posY > 0 && posY < 1750) inDivision = true;
                            else if (boardHeight > 0 && screenY > boardTop && (screenY - boardTop) / boardHeight < 0.75f) inDivision = true;

                            try
                            {
                                if (card.Children.Count > 4 && (card.Children[4].IsVisible || card.Children[4].IsVisibleLocal)) inDivision = true;
                                if (card.Children.Count > 19 && (card.Children[19].IsVisible || card.Children[19].IsVisibleLocal)) inDivision = true;
                            }
                            catch { }

                            if (rank > 0) inDivision = true;
                            if (posY >= 1750 && rank == 0) inDivision = false;
                            if (inDivision && rank == 0) rank = 1;

                            rawCards.Add((name, posX, posY, width, rank, inDivision, hasLeaderFrame, c));
                        }

                        // Check active encounter event for live precision (NPC name, job, rank)
                        var eventElement = syndicatePanel.EventElement ?? syndicatePanel.BetrayalEventData;
                        string eventNpcName = "";
                        string eventNpcJob = "";
                        int eventNpcRank = -1;
                        if (eventElement != null && (eventElement.IsVisible || eventElement.IsVisibleLocal))
                        {
                            try { eventNpcName = eventElement.Target2?.Name ?? ""; } catch { }
                            try
                            {
                                if (eventElement.Children.Count > 4 && eventElement.Children[4].Children.Count > 6)
                                {
                                    var cardElem = eventElement.Children[4];
                                    if (string.IsNullOrEmpty(eventNpcName))
                                        eventNpcName = cardElem.Children[4]?.Text?.Trim() ?? "";

                                    var textElem = cardElem.Children[6].Children.Count > 0 ? cardElem.Children[6].Children[0] : cardElem.Children[6];
                                    var lines = (textElem?.Text ?? "").Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                                    foreach (var line in lines)
                                    {
                                        var l = line.Trim();
                                        if (l.Equals("Transportation", StringComparison.OrdinalIgnoreCase) ||
                                            l.Equals("Fortification", StringComparison.OrdinalIgnoreCase) ||
                                            l.Equals("Research", StringComparison.OrdinalIgnoreCase) ||
                                            l.Equals("Intervention", StringComparison.OrdinalIgnoreCase))
                                        {
                                            eventNpcJob = l;
                                        }
                                        else if (l.IndexOf("Sergeant", StringComparison.OrdinalIgnoreCase) >= 0) eventNpcRank = 1;
                                        else if (l.IndexOf("Lieutenant", StringComparison.OrdinalIgnoreCase) >= 0) eventNpcRank = 2;
                                        else if (l.IndexOf("Captain", StringComparison.OrdinalIgnoreCase) >= 0) eventNpcRank = 3;
                                    }
                                }
                            }
                            catch { }
                        }

                        var divisionMembers = new Dictionary<string, List<(string Name, float X, float Y, float Width, int Rank, bool InDivision, bool HasLeaderFrame, int SlotIndex)>>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["Transportation"] = new(),
                            ["Fortification"] = new(),
                            ["Research"] = new(),
                            ["Intervention"] = new(),
                            ["None"] = new()
                        };

                        foreach (var card in rawCards)
                        {
                            string div = "None";
                            if (card.InDivision && card.Y < 1750)
                            {
                                // 2x2 Quadrant layout on the Syndicate board:
                                // Left side (X < 2100):
                                //   Top:    Fortification (Y < 1200)
                                //   Bottom: Transportation (Y >= 1200)
                                // Right side (X >= 2100):
                                //   Top-inner: Research (Y < 1150 && X < 3300)
                                //   Outer/bot: Intervention
                                if (card.X < 2100)
                                {
                                    div = card.Y < 1200 ? "Fortification" : "Transportation";
                                }
                                else
                                {
                                    div = (card.Y < 1150 && card.X < 3300) ? "Research" : "Intervention";
                                }
                            }

                            if (!string.IsNullOrEmpty(eventNpcName) && card.Name.Equals(eventNpcName, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(eventNpcJob))
                            {
                                div = eventNpcJob;
                            }

                            divisionMembers[div].Add(card);
                        }

                        foreach (var kvp in divisionMembers)
                        {
                            string divName = kvp.Key;
                            var membersInDiv = kvp.Value;
                            if (membersInDiv.Count == 0) continue;

                            var sorted = membersInDiv.OrderBy(m => m.Y).ToList();
                            string leaderName = "";
                            if (divName != "None")
                            {
                                var leaderCandidate = sorted.FirstOrDefault(m => m.HasLeaderFrame);
                                leaderName = !string.IsNullOrEmpty(leaderCandidate.Name) ? leaderCandidate.Name : sorted[0].Name;
                            }

                            foreach (var m in sorted)
                            {
                                int rank = m.Rank;
                                if (!string.IsNullOrEmpty(eventNpcName) && m.Name.Equals(eventNpcName, StringComparison.OrdinalIgnoreCase) && eventNpcRank >= 0)
                                {
                                    rank = eventNpcRank;
                                }

                                bool isLeader = divName != "None" && m.Name.Equals(leaderName, StringComparison.OrdinalIgnoreCase);

                                var typedRelationships = liveRelationships.TryGetValue(m.Name, out var savedRelationships)
                                    ? new Dictionary<string, string>(savedRelationships, StringComparer.OrdinalIgnoreCase)
                                    : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                                List<string> stateNames = typedRelationships.Keys.ToList();
                                int score = GetScore(m.Name, divName, rank);
                                var betrayalMaster = new BetrayalMaster(m.Name, divName, rank, isLeader, stateNames, score)
                                {
                                    SlotIndex = m.SlotIndex,
                                    ScreenX = m.X,
                                    ScreenY = m.Y,
                                    Width = m.Width
                                };
                                betrayalMaster.RelationshipStatuses = typedRelationships;
                                UpdateMasterGoalStatus(betrayalMaster);

                                if (Settings.MegaDebugMode)
                                    LogMessage(betrayalMaster.ToString(), 5, Color.Red);

                                newMasters.Add(betrayalMaster);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (Settings.DebugMode)
                        LogMessage($"UI syndicate parse error: {ex.Message}", 5, Color.Red);
                }
            }

            if (newMasters.Count > 0)
            {
                ListBetrayalMasters = newMasters;

                _missingMastersFromBoard = ListBetrayalMasters
                    .Where(b => b.Job == "None" || string.IsNullOrEmpty(b.Job))
                    .Select(b => b.Name)
                    .ToList();

                _mastersToBePresent = GetRequiredMemberDivisions()
                    .SelectMany(kv => kv.Value)
                    .Distinct()
                    .ToList();

                UpdateReadySafehouses(boardContainer);
            }
        }

        private void DrawSyndicateBoard(SyndicatePanel syndicatePanel)
        {
            updateSyndicateInfo();
            DrawBottomBoardGui();

            BetrayalEventData? eventData = null;
            try
            {
                eventData = syndicatePanel.EventElement ?? syndicatePanel.BetrayalEventData;
            }
            catch
            {
                eventData = null;
            }

            if (eventData is not null)
            {

                if (eventData.IsVisible)
                {
                    // we got an event

                    // can be betray

                    // can be swap leader

                    // swap

                    _eventAction = eventData.Action?.Id ?? "Unknown";
                    // ExileCore can expose the choice action one frame before its text
                    // element is populated. Keep parser calls safe during that transition.
                    _eventText = eventData.EventText ?? string.Empty;
                    NormalizePreviewedRelationshipState();
                    try { _jobEvent = eventData.CurrentEncounterJob?.Name ?? "Unknown"; }
                    catch { _jobEvent = "Unknown"; }

                    if (string.IsNullOrEmpty(_jobEvent) || _jobEvent == "Unknown")
                    {
                        try
                        {
                            if (eventData.Children.Count > 4 && eventData.Children[4].Children.Count > 6)
                            {
                                var textElem = eventData.Children[4].Children[6].Children.Count > 0 ? eventData.Children[4].Children[6].Children[0] : eventData.Children[4].Children[6];
                                var lines = (textElem?.Text ?? "").Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                                foreach (var line in lines)
                                {
                                    var l = line.Trim();
                                    if (l.Equals("Transportation", StringComparison.OrdinalIgnoreCase) ||
                                        l.Equals("Fortification", StringComparison.OrdinalIgnoreCase) ||
                                        l.Equals("Research", StringComparison.OrdinalIgnoreCase) ||
                                        l.Equals("Intervention", StringComparison.OrdinalIgnoreCase))
                                    {
                                        _jobEvent = l;
                                        break;
                                    }
                                }
                            }
                        }
                        catch { }
                    }
                    _SpecialButton = eventData.SpecialButton?.GetClientRectCache ?? RectangleF.Empty;
                    _ReleaseButton = eventData.ReleaseButton?.GetClientRectCache ?? RectangleF.Empty;
                    _InterrogateButton = eventData.InterrogateButton?.GetClientRectCache ?? RectangleF.Empty;

                    // get the name of the current master
                    string masterName = eventData.Target2?.Name;
                    if (string.IsNullOrEmpty(masterName))
                    {
                        try
                        {
                            var masterPanel = eventData.GetChildAtIndex(4);
                            masterName = masterPanel?.GetChildAtIndex(4)?.Text ?? masterPanel?.Text;
                        }
                        catch {}
                    }
                    _masterText = masterName ?? "Unknown";

                    if (Settings.DebugMode)
                    {
                        LogMessage("event  " + _eventText, 5, Color.Yellow);
                        LogMessage("masterText: " + _masterText, 5, Color.Yellow);
                    }


                    // given the name, get him his current rank and job
                    _eventMaster = ListBetrayalMasters.FirstOrDefault(master => master.Name == _masterText);
                    if (_eventMaster == null)
                    {
                        int fallbackScore = GetScore(_masterText, "None", 0);
                        _eventMaster = new BetrayalMaster(_masterText, "None", 0, false, new List<string>(), fallbackScore);
                        if (Settings.DebugMode)
                            LogMessage($"DrawSyndicateBoard: master '{_masterText}' not found in ListBetrayalMasters — using unallocated fallback (score {fallbackScore})", 5, Color.Yellow);
                    }

                    if ((string.IsNullOrEmpty(_jobEvent) || _jobEvent == "Unknown") && _eventMaster != null && _eventMaster.Job != "None")
                    {
                        _jobEvent = _eventMaster.Job;
                    }

                    // In CatarinaFocus mode: ALL divisions should gain intelligence (run every safehouse)
                    // In SafehouseRewards mode: only Farm divisions gain intel; Filler divisions are blocked
                    if (Settings.FarmingMode == FarmingMode.CatarinaFocus)
                    {
                        _jobsToGainIntelligence = Settings.DivisionRoles.Keys.ToList();
                        _jobsToNOTGainIntelligence = new List<string>(); // nothing blocked
                    }
                    else
                    {
                        _jobsToGainIntelligence = Settings.DivisionRoles
                            .Where(kv => kv.Value == "Farm")
                            .Select(kv => kv.Key)
                            .ToList();

                        _jobsToNOTGainIntelligence = Settings.DivisionRoles
                            .Where(kv => kv.Value == "Filler")
                            .Select(kv => kv.Key)
                            .ToList();
                    }

                    _mastersToBePresent = GetRequiredMemberDivisions()
                        .SelectMany(kv => kv.Value)
                        .Distinct()
                        .ToList();

                    if (Settings.DebugMode)
                        LogMessage("Jobs to gain intelligence: " + string.Join(", ", _jobsToGainIntelligence), 5, Color.Green);

                    if (Settings.DebugMode)
                        LogMessage("Masters to be present: " + string.Join(", ", _mastersToBePresent), 5, Color.Green);



                    // CHECK IF BAD JOBS ARE FULL

                    var countBadMastersInBadJobs = ListBetrayalMasters
                        .Count(bm => _jobsToNOTGainIntelligence.Contains(bm.Job) && bm.Score == 1);

                    if (Settings.DebugMode)
                        LogMessage("countBadMastersInBadJobs " + countBadMastersInBadJobs, 5, Color.Coral);

                    badJobsAreFull = countBadMastersInBadJobs >= 10;

                    // then get where he could be
                    if (Settings.DebugMode)
                        LogMessage("The event master is " + _eventMaster, 5, Color.Yellow);
                    var auxRank = _eventMaster.Rank;
                    if (auxRank == 0)
                        auxRank = 1;
                    if (Settings.DebugMode)
                        LogMessage("The auxrank is " + auxRank.ToString(), 5, Color.Yellow);
                    // event masters

                    _eventMasters = BetrayalEventMembers();

                    foreach (var eventMasterAux in _eventMasters)
                    {
                        if (Settings.DebugMode)
                            LogMessage("Event Masters !!! " + eventMasterAux.ToString(), 5, Color.DarkGoldenrod);
                    }

                    if (Settings.DebugMode)
                        LogMessage("The event is " + _eventAction.ToString(), 5, Color.Yellow);


                    var highestScoringEntry = Settings.DivisionRoles.Keys
                        .Select(job => new MasterWeight(_eventMaster.Name, job, auxRank, GetScore(_eventMaster.Name, job, auxRank)))
                        .Where(mw => mw.Score > _eventMaster.Score)
                        .OrderByDescending(mw => mw.Score)
                        .FirstOrDefault();

                    //  master needs to leave

                    var _eventMasterNeedsToLeaveCurrentJob = false;
                    // Settings are authoritative: Farm division = interesting job
                    bool _isFarmDivision = Settings.DivisionRoles.TryGetValue(_jobEvent, out var _divRole) && _divRole == "Farm";
                    var isJobOfInterest = _isFarmDivision;

                    // Member has interest on this job if they are the Settings-assigned Farm member
                    bool _isSettingsFarmMember = _isFarmDivision
                        && Settings.DivisionMembers.TryGetValue(_jobEvent, out var _farmList)
                        && _farmList.Contains(_eventMaster.Name);
                    var masterHasInterestOnJob = _isSettingsFarmMember;

                    // Members that should be in this job = Settings Farm members for this division
                    var mastersWithThisJobAsInterest = _isFarmDivision && Settings.DivisionMembers.TryGetValue(_jobEvent, out var _farmJobList)
                        ? _farmJobList : new List<string>();
                    int numMastersWithThisJobAsInterest = mastersWithThisJobAsInterest.Count;
                    // Members correctly on this job = those with score > 1 (Farm member in correct div = 30, wrong = -1)
                    var numInterestMastersCurrentlyOnThisJob = ListBetrayalMasters.Count(mw => mw.Job == _jobEvent && mw.Score > 1);
                    var numMastersCurrentlyOnThisJob = ListBetrayalMasters.Count(mw => mw.Job == _jobEvent);
                    //var noSpaceForThisMaster = (numMastersCurrentlyOnThisJob + (numMastersWithThisJobAsInterest - numInterestMastersCurrentlyOnThisJob) - 2) > 0;

                    var noSpaceForThisMaster = numMastersCurrentlyOnThisJob >= 2 || (numMastersCurrentlyOnThisJob - numInterestMastersCurrentlyOnThisJob) >= 2;




                    if (Settings.DebugMode)
                    {
                        LogMessage($"Masters at {_jobEvent}: {numMastersCurrentlyOnThisJob} | masters correctly at this job : {numInterestMastersCurrentlyOnThisJob} | total masters that should be here {numMastersWithThisJobAsInterest} ", 5, Color.Yellow);
                    }

                    // if master has no interest on this job and this is a job where other masters have interest in
                    // and number of masters that should be here 

                    // needs to leave if there is no space for interest masters
                    // 


                    List<Outcome> _eventOutcomes = new List<Outcome>();

                    // Release baseline
                    _eventOutcomes.Add(new Outcome(OutcomeType.Nothing, ActionType.Release, 0, "Release"));

                    // Comprehensive Interrogation Evaluation:
                    if (_eventMaster.Job != "None" && !string.IsNullOrEmpty(_eventMaster.Job))
                    {
                        // 1. General strategic interrogation outcome (intel gain, network absence, Unbreaking scarabs)
                        AddInterrogationStrategyOutcome(_eventOutcomes, _eventMaster);

                        // 2. Goal-oriented placement scoring for Interrogate:
                        if (_eventMaster.Score < 0)
                        {
                            // Misplaced member or intruder in Farm division!
                            // Interrogating is CRITICAL to demote them to Rank 0 / Outside so our farm targets can enter!
                            // Rank loss is what kicks the intruder out, so Unconditional Release lowers the payoff.
                            int intruderInterrogateScore = (int)Math.Round(150 * (1.0 - KeepRankChance));
                            _eventOutcomes.Add(new Outcome(
                                OutcomeType.RemoveBadMemberFromBadJob,
                                ActionType.Interrogate,
                                intruderInterrogateScore,
                                $"{_eventMaster.Name} is MISPLACED / INTRUDER in {_eventMaster.Job} — INTERROGATE to kick out of division!"));
                        }
                        else if (_eventMaster.Score >= 30)
                        {
                            // Valuable target Farm member in correct division!
                            if (Settings.FarmingMode == FarmingMode.CatarinaFocus)
                            {
                                int farmCatarinaScore = _eventMaster.Rank >= 3 ? -20 : -40;
                                _eventOutcomes.Add(new Outcome(
                                    OutcomeType.GainInformationToInterestingJob,
                                    ActionType.Interrogate,
                                    farmCatarinaScore,
                                    $"{_eventMaster.Name} is a valuable Farm member (Rank {_eventMaster.Rank}) — prefer executing to build rank"));
                            }
                            else
                            {
                                // SafehouseRewards: NEVER derank your cash cows!
                                _eventOutcomes.Add(new Outcome(
                                    OutcomeType.GainInformationToInterestingJob,
                                    ActionType.Interrogate,
                                    -150,
                                    $"{_eventMaster.Name} is CORRECTLY PLACED Farm target (Rank {_eventMaster.Rank}) — DO NOT INTERROGATE / DERANK!"));
                            }
                        }
                        else if (_eventMaster.Score >= 1)
                        {
                            // Filler member in Filler division
                            if (Settings.FarmingMode == FarmingMode.CatarinaFocus)
                            {
                                int interrogateRankBonus = _eventMaster.Rank >= 3 ? 50 : _eventMaster.Rank == 2 ? 15 : -20;
                                _eventOutcomes.Add(new Outcome(
                                    OutcomeType.GainInformationToInterestingJob,
                                    ActionType.Interrogate,
                                    interrogateRankBonus,
                                    $"Catarina mode: interrogate Rank {_eventMaster.Rank} → {(interrogateRankBonus > 0 ? "HIGH intel" : "low intel, build rank first")}"));
                            }
                        }
                    }
                    else
                    {
                        // Outside / Free Agent (Rank 0, no job)
                        if (Settings.FarmingMode == FarmingMode.CatarinaFocus)
                        {
                            _eventOutcomes.Add(new Outcome(OutcomeType.GainInformationToInterestingJob, ActionType.Interrogate, -15,
                                $"{_eventMaster.Name} has no job (Rank 0) — minimal intel, prefer execute/assign"));
                        }
                        else
                        {
                            _eventOutcomes.Add(new Outcome(OutcomeType.RemoveBadMemberFromBadJob, ActionType.Interrogate, -50,
                                $"{_eventMaster.Name} has no job / rank 0 — cannot remove from any division"));
                        }
                    }
                    //else
                    if (true)
                    {


 



                        switch (_eventAction)
                        {
                            case "StealIntelligence":

                                // StealIntelligence special: Interrogate here gives RANK UP + intel (not rank down!)
                                // This is the BEST interrogation option — it promotes the member!
                                if (_eventMaster.Score >= 30 && _eventMaster.Job != "None" && _eventMaster.Rank < 3)
                                {
                                    // Farm member, not max rank: Interrogate = rank up + intel = AMAZING
                                    _eventOutcomes.Add(new Outcome(
                                        OutcomeType.PromoteGoodMemberInInterestingJob,
                                        ActionType.Special, 80,
                                        $"{_eventMaster.Name} rank {_eventMaster.Rank}/3 — take Steal Intelligence"));
                                }
                                else if (_eventMaster.Score >= 30 && _eventMaster.Job != "None")
                                {
                                    // Farm member max rank: still gives intel
                                    _eventOutcomes.Add(new Outcome(
                                        OutcomeType.GainInformationToInterestingJob,
                                        ActionType.Special, 60,
                                        $"{_eventMaster.Name} rank {_eventMaster.Rank} (max) — take Steal Intelligence"));
                                }
                                else if (Settings.FarmingMode == FarmingMode.CatarinaFocus && _eventMaster.Job != "None" && _eventMaster.Rank < 3)
                                {
                                    // Catarina mode: filler member, not max rank — Interrogate = rank up + intel!
                                    // This overrides the normal "filler neutral" scoring
                                    _eventOutcomes.Add(new Outcome(
                                        OutcomeType.PromoteBadMemberInBadJob,
                                        ActionType.Special, 60,
                                        $"{_eventMaster.Name} rank {_eventMaster.Rank}/3 — StealIntel: rank up + intel!"));
                                }
                                else if (Settings.FarmingMode == FarmingMode.CatarinaFocus && _eventMaster.Job != "None")
                                {
                                    // Catarina mode: filler member max rank — still gives good intel
                                    _eventOutcomes.Add(new Outcome(
                                        OutcomeType.GainInformationToInterestingJob,
                                        ActionType.Special, 50,
                                        $"{_eventMaster.Name} rank {_eventMaster.Rank} (max) — Interrogate for intel (Catarina)"));
                                }

                                _eventOutcomes.AddRange(StealEventRelationship());

                                break;
                            case "PromoteNPC":


                                _eventOutcomes.AddRange(PromoteNPC());


                                break;
                            case "Execute":

                                // Check if execute has no effect (already max rank)
                                bool executeNoEffect = _eventText.Contains("No effect") || _eventText.Contains("Already max rank");

                                Match matchExecute = Regex.Match(_eventText, @"moves to (\w+)");
                                string jobToMove = matchExecute.Success ? matchExecute.Groups[1].Value : null;
                                var shouldNotMove = false;
                                if (Settings.DebugMode)
                                    LogMessage("Event text is   " + _eventText, 5, Color.Yellow);
                                if (jobToMove is not null)
                                {

                                    var numMastersCurrentlyOnThisMoveJob = ListBetrayalMasters.Count(mw => mw.Job == jobToMove);
                                    bool _isMoveJobFarm = Settings.DivisionRoles.TryGetValue(jobToMove, out var _moveRole) && _moveRole == "Farm";
                                    var mastersWithThisMoveJobAsInterest = _isMoveJobFarm && Settings.DivisionMembers.TryGetValue(jobToMove, out var _moveMembers)
                                        ? _moveMembers : new List<string>();
                                    var numMastersWithThisMoveJobAsInterest = mastersWithThisMoveJobAsInterest.Count;
                                    var numInterestMastersCurrentlyOnThisMoveJob = ListBetrayalMasters.Count(mw => mw.Job == jobToMove && mw.Score > 1);
                                    //var noMoveSpaceForThisMaster = (numMastersCurrentlyOnThisMoveJob + (numMastersWithThisMoveJobAsInterest - numInterestMastersCurrentlyOnThisMoveJob) - 2) > 0;

                                    var noMoveSpaceForThisMaster = numMastersCurrentlyOnThisMoveJob >= 2 || (numMastersCurrentlyOnThisMoveJob - numInterestMastersCurrentlyOnThisMoveJob) >= 2;
                                    /// 2 -2 + 2
                                    if (Settings.DebugMode)
                                    {
                                        LogMessage($"Masters at {jobToMove}: {numMastersCurrentlyOnThisMoveJob} | masters correctly at this job : {numInterestMastersCurrentlyOnThisMoveJob} | total masters that should be here {numMastersWithThisMoveJobAsInterest} ", 5, Color.Yellow);
                                        LogMessage($"{numMastersCurrentlyOnThisMoveJob} | {numMastersWithThisMoveJobAsInterest} | {numInterestMastersCurrentlyOnThisMoveJob} ", 5, Color.Aqua);
                                        LogMessage($" masterHasInterestOnJob: {masterHasInterestOnJob} isJobOfInterest: {isJobOfInterest} noMoveSpaceForThisMaster: {noMoveSpaceForThisMaster} ", 5, Color.Aqua);
                                    }
                                    if (!masterHasInterestOnJob && isJobOfInterest && noMoveSpaceForThisMaster)
                                    {
                                        //Graphics.DrawFrame(_ReleaseButton, Color.Green, 6);
                                        _eventOutcomes.Add(new Outcome(OutcomeType.AddBadMemberToInterestingJob, ActionType.Special, -100, $"{_eventMaster.Name} who is bad, gets moved to interesting job "));

                                        shouldNotMove = true;
                                        if (Settings.DebugMode)
                                        {
                                            LogMessage("Move - This master has no interest on this job and space is needed for others: " + string.Join(", ", mastersWithThisMoveJobAsInterest), 5, Color.Yellow);
                                        }
                                    } else if (masterHasInterestOnJob && isJobOfInterest)
                                    {
                                        shouldNotMove = true;
                                        _eventOutcomes.Add(new Outcome(OutcomeType.AddGoodMemberToInterestingJob, ActionType.Special, 100, $"{_eventMaster.Name} who is GOOD, gets moved to interesting job "));


                                    }
                                }


                                if (shouldNotMove)
                                {
                                    // was already interrogated 
                                }


                                // if he can move to correct safehouse


                                // bad member moving to bad safehouse

                                // good member moving to good safehouse

                                

                                //else if (IsOnTheWrongSafeHouse(highestScoringEntry, _eventMaster, _InterrogateButton, _SpecialButton, _ReleaseButton, jobToMove, _eventText))
                                else if (_eventMaster.Score == 0 && jobToMove != null)// he is unnalocated, moving to a job
                                {
                                    var potentialScore = GetScore(_eventMaster.Name, jobToMove, 1);
                                    if (potentialScore == 1)
                                    {
                                        _eventOutcomes.Add(new Outcome(OutcomeType.AddBadMemberToBadJob, ActionType.Special, 100, $"{_eventMaster.Name} who is bad gets added to bad job "));

                                    }
                                    else if (potentialScore >= 30)
                                    {
                                        _eventOutcomes.Add(new Outcome(OutcomeType.AddGoodMemberToBadJob, ActionType.Special, 100, $"{_eventMaster.Name} who is GOOD gets added to INTERESTING job "));

                                    }
                                    else if (potentialScore < 0)
                                    {
                                        _eventOutcomes.Add(new Outcome(OutcomeType.AddBadMemberToInterestingJob, ActionType.Special, -100, $"{_eventMaster.Name} is not wanted in {jobToMove} "));
                                    }

                                }
                                else

                                 if (_eventMaster.Score >= 30)
                                {
                                    int resultingRank = GetRankAfterExecute(_eventMaster.Rank);
                                    // Expected rank gain: +1, plus the extra-rank chance, capped at rank 3.
                                    double expectedGain = Math.Min(3 - Math.Clamp(_eventMaster.Rank, 0, 3), 1.0 + ExtraRankChance);
                                    int promoteScore;
                                    if (executeNoEffect)
                                        promoteScore = 5;
                                    else
                                        promoteScore = (int)Math.Round(80 * expectedGain);
                                    _eventOutcomes.Add(new Outcome(OutcomeType.PromoteGoodMemberInInterestingJob, ActionType.Special, promoteScore,
                                        $"{_eventMaster.Name} R{_eventMaster.Rank} -> R{resultingRank}{(executeNoEffect ? " (NO EFFECT)" : "")}"));
                                    AddExecuteIntelligenceOutcome(_eventOutcomes, _eventMaster.Job, resultingRank);
                                }
                                else if (_eventMaster.Score == 1)
                                {
                                    if (executeNoEffect)
                                    {
                                        _eventOutcomes.Add(new Outcome(OutcomeType.PromoteBadMemberInBadJob, ActionType.Special, 0, $"{_eventMaster.Name} execute has no effect (max rank)"));
                                    }
                                    else if (isJobOfInterest)
                                    {
                                        _eventOutcomes.Add(new Outcome(OutcomeType.PromoteBadMemberInInterestingJob, ActionType.Special, -150, $"{_eventMaster.Name} is unwanted in Farm division — DO NOT PROMOTE"));
                                    }
                                    else
                                    {
                                        int resultingRank = GetRankAfterExecute(_eventMaster.Rank);
                                        int fillerPromoteScore = Settings.FarmingMode == FarmingMode.CatarinaFocus ? 40 : 15;
                                        _eventOutcomes.Add(new Outcome(OutcomeType.PromoteBadMemberInBadJob, ActionType.Special, fillerPromoteScore,
                                            $"{_eventMaster.Name} filler promote — builds rank for intel / encounters"));
                                        AddExecuteIntelligenceOutcome(_eventOutcomes, _eventMaster.Job, resultingRank);
                                    }
                                }
                                else
                                {
                                    _eventOutcomes.Add(new Outcome(OutcomeType.PromoteGoodMemberInBadJob, ActionType.Special, -150, $"{_eventMaster.Name} misplaced / intruder — NEVER execute!"));
                                }





                                break;
                            case "SwapNPCJob":
                                // example  It That Fled moves to Intervention. Haku moves to Research
                                _eventOutcomes.AddRange(EventSwap(syndicatePanel));
                                break;
                            case "SwapLeader":
                                _eventOutcomes.AddRange(SwapLeaderRelationship());
                                break;
                            case "RemoveNPCFromOrg":
                                _eventOutcomes.AddRange(RemoveNPCFromOrg());
                                break;
                            case "NPCBefriendsAnother":
                                _eventOutcomes.AddRange(EventRelationship());

                                // Bargain events often also include intelligence bonus in the text
                                // e.g. "Korell and Riker become Trusted. +4 Research Intelligence"
                                {
                                    Match intelMatch = Regex.Match(_eventText, @"\+(\d+)\s+(\w+)\s+Intelligence");
                                    if (intelMatch.Success)
                                    {
                                        string intelJob = intelMatch.Groups[2].Value;
                                        if (_jobsToGainIntelligence.Contains(intelJob))
                                        {
                                            int intelScore = Settings.FarmingMode == FarmingMode.CatarinaFocus ? 60 : 40;
                                            _eventOutcomes.Add(new Outcome(OutcomeType.GainInformationToInterestingJob, ActionType.Special, intelScore,
                                                $"+{intelMatch.Groups[1].Value} {intelJob} Intelligence"));
                                        }
                                        else
                                        {
                                            int intelAmount = int.Parse(intelMatch.Groups[1].Value);
                                            _eventOutcomes.Add(new Outcome(OutcomeType.GainInformationToBadJob, ActionType.Special, -intelAmount * 8,
                                                $"Avoid +{intelAmount} {intelJob} Intelligence (Skip/Filler division)"));
                                        }
                                    }
                                }


                                break;
                            case "GainIntelligenceLarge":
                                _eventOutcomes.AddRange(IntelligenceAction());
                                break;
                            case "GainIntelligence":

                                _eventOutcomes.AddRange(IntelligenceAction());
                                break;
                            case "NPCLeavesOrg":
                                //Console.WriteLine("Number is 1");

                                _eventOutcomes.AddRange(NPCLeavesOrg(_SpecialButton, _ReleaseButton, _mastersToBePresent, _eventText, _missingMastersFromBoard, _eventAction));

                                break;
                            case "StealRanks":
                                _eventOutcomes.AddRange(StealRanks());
                                break;
                            case "RemoveAllFromPrison":
                                // Releasing prisoners always gives intel — Special is fine
                                _eventOutcomes.Add(new Outcome(OutcomeType.GainInformationToInterestingJob, ActionType.Special, 20,
                                    "Release prisoners — gain intel"));
                                break;
                            case "DestroyAllItemsInDivision":


                

                                _eventOutcomes.Add(new Outcome(OutcomeType.DestroyEquipment, ActionType.Special, 10, $"Destroy division items"));
                                


                                break;
                            case "RemoveAllRivalries":
                                int allRivalryRemovalScore = ScoreRemovingExistingRivalries(null);
                                _eventOutcomes.Add(new Outcome(OutcomeType.RemoveRivalries, ActionType.Special, allRivalryRemovalScore,
                                    $"Remove all rivalries — topology impact {allRivalryRemovalScore:+#;-#;0}"));
                                

                                break;
                            case "RemoveAllRivalriesInDivision":
                                Match rivalryDivisionMatch = Regex.Match(_eventText, @"(Transportation|Fortification|Research|Intervention)", RegexOptions.IgnoreCase);
                                string rivalryDivision = rivalryDivisionMatch.Success
                                    ? rivalryDivisionMatch.Groups[1].Value
                                    : _eventMaster.Job;
                                int divisionRivalryRemovalScore = ScoreRemovingExistingRivalries(rivalryDivision);
                                _eventOutcomes.Add(new Outcome(OutcomeType.RemoveRivalriesInDivision, ActionType.Special, divisionRivalryRemovalScore,
                                    $"Remove rivalries in {rivalryDivision} — topology impact {divisionRivalryRemovalScore:+#;-#;0}"));
                                break;
                            case "DestroyAllItemsOfRivalDivision":
                                // se o event master for alguém com mais de 20 de score -> do it

                                // betray member 1
                                // betray member 2
                                string DestroyPattern = @"Destroy all Equipment of (Research|Intervention|Transportation|Fortification) members";
                                Match DestroyMatch = Regex.Match(_eventText, DestroyPattern);
                                if (DestroyMatch.Success)
                                {
                                    var capturedDivision = DestroyMatch.Groups[1].Value;
                                    LogMessage($"Captured division: {capturedDivision}", 5, Color.DarkGoldenrod);


                                    // betray member1 is eventMaster

                                    // betray member 2 is the one from the eventMasters that isnt eventmaster

                                    var potentialBetrayedMembers = _eventMasters.Where(master => master.Name != _eventMaster.Name && master.Job == capturedDivision);
                                    // if potentialBetrayedMembers == 1, we're sure it is him
                                    if (potentialBetrayedMembers.Count() == 1)
                                    {
                                        var betrayedMember = potentialBetrayedMembers.FirstOrDefault();
                                        int betrayedMemberBestScore = GetMemberBestPossibleScore(betrayedMember.Name);

                                        if (_eventMaster.Score >= 30 && betrayedMember.Score >= 1) // if both are on correct placement and at least one of them is interesting
                                        {
                                            _eventOutcomes.Add(new Outcome(OutcomeType.RivalBetweenGoodInterestingMember, ActionType.Special, 100, $"(INTERESTING){_eventMaster.Name} {betrayedMember.Name} Become Rivals"));
                                        }
                                        else if (_eventMaster.Score >= 1 && betrayedMember.Score >= 30) // if both are on correct placement and at least one of them is interesting
                                        {
                                            _eventOutcomes.Add(new Outcome(OutcomeType.RivalBetweenGoodInterestingMember, ActionType.Special, 100, $"{_eventMaster.Name} (INTERESTING){betrayedMember.Name} Become Rivals"));
                                        }
                                        else if (_eventMaster.Score >= 30 && betrayedMemberBestScore >= 30) // if one is correct and the other is incorrect but is a Farm member
                                        {
                                            _eventOutcomes.Add(new Outcome(OutcomeType.RivalBetweenGoodInterestingMember, ActionType.Special, 100, $"{_eventMaster.Name} {betrayedMember.Name} Become Rivals and {betrayedMember.Name} gets closer to dream job "));
                                        }
                                    }
                                    else
                                    {


                                    }
                                    // else


                                    

                                }

                                
                                // if both are on correct placement and at least one of them is interesting



                                
                 
                                
                                
                                break;
                            case "DownrankRivalsUprankMyDivision":
                                //Console.WriteLine("Number is 1");

                                // get encounter target _eventMaster
                                // get upwards division

                                // get downwards divison
                                Match matchDownrankRivalsUpgradeMyDivison = Regex.Match(_eventText, @"\+1 rank to (\w+) members\. -1 rank to (\w+) members");

                                if (matchDownrankRivalsUpgradeMyDivison.Success)
                                {
                                    string divisionToGainRank = matchDownrankRivalsUpgradeMyDivison.Groups[1].Value;
                                    string divisionToLoseRank = matchDownrankRivalsUpgradeMyDivison.Groups[2].Value;
                                    if (Settings.DebugMode)
                                    {
                                        LogMessage($"Division to up {divisionToGainRank} division to down {divisionToLoseRank}", 5, Color.Yellow);
                                        LogMessage($"_eventMasterScore {_eventMaster.Score} jobsToGainContainsUpDivision {_jobsToGainIntelligence.Contains(divisionToGainRank)}" +
                                            $"jobsToGainDontContainsDownDivision {!_jobsToGainIntelligence.Contains(divisionToLoseRank)} " +
                                            $"Any master to lose rank that is currently below 2 {ListBetrayalMasters.Any(master => master.Job == divisionToLoseRank && master.Rank < 2)} " +
                                            $"", 5, Color.Yellow);
                                    }
                                    if (_eventMaster.Score >= 30
                                        && _jobsToGainIntelligence.Contains(divisionToGainRank)
                                        && !_jobsToGainIntelligence.Contains(divisionToLoseRank)
                                        && !ListBetrayalMasters.Any(master => master.Job == divisionToLoseRank && master.Rank < 2) // no member will be removed from the division because their rank will be at least 1 after
                                        )
                                    {
                                        //Graphics.DrawFrame(_SpecialButton, Color.Green, 6);

                                        _eventOutcomes.Add(new Outcome(OutcomeType.UprankMyDivisionDownwardsOther, ActionType.Special, 100, $"Uprank my division, downward other"));
                                        _eventOutcomes.Add(new Outcome(OutcomeType.RivalBetweenGoodInterestingMember, ActionType.Special, 10, $" {_eventMaster.Name} is interesting and rivals someone"));
                                    }
                                    else
                                    {
                                        _eventOutcomes.Add(new Outcome(OutcomeType.UprankMyDivisionDownwardsOther, ActionType.Special, -50, $"Uprank my division, downward other"));
                                        //Graphics.DrawFrame(_ReleaseButton, Color.Green, 6);
                                    }
                                }
                                else
                                {
                                    //Graphics.DrawFrame(_ReleaseButton, Color.Green, 6);
                                }



                                // if _eventMaster is person of interest score >= 30 && upwards division = division of interest && downwards divison != division of interest
                                // and all rmembers in downward division are score > 1 i.e. não existe nenhum na downward division com rank < 2







                                break;
                            case "GainItemAnyUnique":
                                _eventOutcomes.Add(new Outcome(OutcomeType.GainUniqueItem, ActionType.Special, 10, $"Gain unique"));
                                break;
                            case "GainItemCurrency":
                                _eventOutcomes.Add(new Outcome(OutcomeType.GainCurrencyItem, ActionType.Special, 10, $"Gain currency"));
                                break;
                            case "GainItemVeiledItem":
                                _eventOutcomes.Add(new Outcome(OutcomeType.GainVeiledItem, ActionType.Special, 0, $"Gain Veiled Item"));
                                break;
                            case "GainItemMap":
                                _eventOutcomes.Add(new Outcome(OutcomeType.GainMap, ActionType.Special, 10, $"Gain Map"));
                                break;
                            case "GainItemScarab":
                                _eventOutcomes.Add(new Outcome(OutcomeType.GainScarab, ActionType.Special, 10, $"Gain Scarab"));
                                break;
                            default:
                                ////Graphics.DrawFrame(_SpecialButton, Color.Green, 6);
                                if (Settings.DebugMode)
                                    LogMessage("Default  ", 5, Color.Red);
                                break;
                        }
                    }

                    foreach(var eventOutcome in _eventOutcomes)
                    {
                        if (Settings.DebugMode)
                        {
                            LogMessage(eventOutcome.ToString(), 5, Color.Aqua);
                        }
                    }

                    PrintHighestScoringActionType(_eventOutcomes);  





                }

            }

        }
        private List<Outcome> NPCLeavesOrg(RectangleF _SpecialButton, RectangleF _ReleaseButton, List<string> mastersToBePresent, string _eventText, List<string> missingMastersFromBoard, string _eventAction)
        {

            string jobToCheck;
            List<Outcome> eventOutcome = new List<Outcome>();
            if (Settings.DebugMode)
                LogMessage("Event is   " + _eventText, 5, Color.Yellow);


            // bool any masters to be present are unnalocated

            bool missingImportantFromBoard = missingMastersFromBoard.Any(name => mastersToBePresent.Contains(name));

            if (Settings.DebugMode)
            {
                LogMessage("Unnalocated names: " + string.Join(", ", missingMastersFromBoard));
                LogMessage("Masters to be present: " + string.Join(", ", mastersToBePresent));
                LogMessage("Are we missing important from board " + missingImportantFromBoard.ToString(), 5, Color.Yellow);

            }
            // bool person removed isnt master to be present



            // get the master that is being removed




            // when it is NPCLeavesOrg

            if (_eventAction == "NPCLeavesOrg")
            {

                Match match = Regex.Match(_eventText, @"(.+?)\s+(?:is\s+removed|leaves|removed)\s+(?:from\s+)?the Immortal Syndicate", RegexOptions.IgnoreCase);

                if (match.Success)
                {
                    var nameToBeRemoved = match.Groups[1].Value; // Extracted word
                    if (Settings.DebugMode)
                        LogMessage("Element to be removed is  " + nameToBeRemoved, 5, Color.Blue);
                        LogMessage("Len of Element to be removed is  " + nameToBeRemoved.Length.ToString(), 5, Color.Blue);

                    // if the master is not important remove them 
                    eventOutcome.Add(new Outcome(OutcomeType.RemoveBadMemberFromOrg, ActionType.Special, -10, $"{nameToBeRemoved} is not an interesting member and is going to be removed "));

                    if (!mastersToBePresent.Contains(nameToBeRemoved) && missingImportantFromBoard)
                    {
                        //Graphics.DrawFrame(_SpecialButton, Color.Green, 6);

                        // if job is interesting
                       // if (_jobsToGainIntelligence.Contains())

                        //else


                        eventOutcome.Add(new Outcome(OutcomeType.BringNewMemberInOrgGoodMemberOutsideForInterestingJob,ActionType.Special, 50, $"{nameToBeRemoved} is not an interesting member and is going to be removed AND WE MIGHT BRING A GOOD IN"));
                    }
                    else
                    {
                        //Graphics.DrawFrame(_ReleaseButton, Color.Green, 6);
                    }

                }
            }

            //foreach (var a in eventOutcome)
            //{
            //    if (Settings.DebugMode)
            //    {
            //        LogMessage("Inside event code " + a.ToString(), 5, Color.Aqua);
            //    }
            //}


            return eventOutcome;


        }
        private List<Outcome> RemoveNPCFromOrg()
        {
            
            string jobToCheck;
            if (Settings.DebugMode)
                LogMessage("Event is   " + _eventText, 5, Color.Yellow);


            // bool any masters to be present are unnalocated

            bool missingImportantFromBoard = _missingMastersFromBoard.Any(name => _mastersToBePresent.Contains(name));
            if (Settings.DebugMode)
            {
                LogMessage("Event is   " + _eventText, 5, Color.AntiqueWhite);
                LogMessage("Unnalocated names: " + string.Join(", ", _missingMastersFromBoard), 5, Color.AntiqueWhite);
                LogMessage("Masters to be present: " + string.Join(", ", _mastersToBePresent), 5, Color.AntiqueWhite);
                LogMessage("Are we missing important from board " + missingImportantFromBoard.ToString(), 5, Color.AntiqueWhite);
            }

            List<Outcome> eventOutcome = new List<Outcome>();



            // when it is NPCLeavesOrg

            if (_eventAction == "RemoveNPCFromOrg")
            {

                Match match = Regex.Match(_eventText, @"(.+?)\s+(?:is\s+removed|leaves|removed)\s+(?:from\s+)?the Immortal Syndicate", RegexOptions.IgnoreCase);

                if (match.Success)
                {
                    var nameToBeRemoved = match.Groups[1].Value; // Extracted word
                    if (Settings.DebugMode)
                        LogMessage("Element to be removed is  " + nameToBeRemoved, 5, Color.Blue);

                   

                    // if the master is not important remove them 

                    eventOutcome.Add(new Outcome(OutcomeType.RemoveBadMemberFromOrg,ActionType.Special, -10, $"{nameToBeRemoved} is not an interesting member and is going to be removed"));
                    if (!_mastersToBePresent.Contains(nameToBeRemoved) && missingImportantFromBoard)
                    {
                        //Graphics.DrawFrame(_SpecialButton, Color.Green, 6);
                        
                        eventOutcome.Add(new Outcome(OutcomeType.BringNewMemberInOrgGoodMemberOutsideForInterestingJob, ActionType.Special, 50, $"{nameToBeRemoved} is not an interesting member and is going to be removed"));
                        
                    }
                    else
                    {
                        //Graphics.DrawFrame(_ReleaseButton, Color.Green, 6);
                    }

                }
            }


            return eventOutcome;

        }

        private List<Outcome> IsOnTheWrongSafeHouse(MasterWeight highestScoringEntry, string jobToMove)
        {


            List<Outcome> eventOutcome = new List<Outcome>();
            Match matchExecute2 = Regex.Match(_eventText, @"\.\s*(.+?)\s+moves to (\w+)");
            var name = "";
            var destination = "";
            bool okToExecute = false;
            if (matchExecute2.Success)
            {
                name = matchExecute2.Groups[1].Value;        // "Cameria" or "It That Fled"
                destination = matchExecute2.Groups[2].Value; // "Intervention"
                                                             //LogMessage("Proposed job" + destination + " for " + name, 5, Color.Yellow);

                if (Settings.DebugMode)
                    LogMessage("parsed name  " + name, 5, Color.Yellow);
                int proposedScore = GetScore(name, GetMemberEffectiveJob(name, "None"), 1);
                if (proposedScore >= (highestScoringEntry?.Score ?? 0))
                {
                    okToExecute = true;
                }

            }


            if (highestScoringEntry != null)
            {
                if (Settings.DebugMode)
                {
                    LogMessage("This master could go to  " + highestScoringEntry.Job + " with a score of " + highestScoringEntry.Score, 5, Color.Yellow);
                }
                if (highestScoringEntry.Score == _eventMaster.Score)
                {
                    //Graphics.DrawFrame(_SpecialButton, Color.Green, 6);
                    eventOutcome.Add(new Outcome(OutcomeType.AddBadMemberToBadJob, ActionType.Special, 100, $"{_eventMaster.Name} who is bad gets moved to bad job "));

                }
                else if (highestScoringEntry.Score > _eventMaster.Score && _eventMaster.Rank == 0 && jobToMove == highestScoringEntry.Job)
                {
                    //Graphics.DrawFrame(_SpecialButton, Color.Green, 6);
                }
                else if (_eventMaster.Rank > 0)
                {
                    //Graphics.DrawFrame(_InterrogateButton, Color.Green, 6);
                }
                else if (okToExecute)
                {
                    //Graphics.DrawFrame(_SpecialButton, Color.Green, 6);
                }
                else
                {


                    //Graphics.DrawFrame(_ReleaseButton, Color.Green, 6);
                }
                //return true;
            }
            //return false;


            return eventOutcome;
        }
        private List<Outcome> IntelligenceAction()
        {
            List<Outcome> eventOutcome = new List<Outcome>();
            Match match = Regex.Match(_eventText, @"\d+\s+(\w+)\s+\w+");
            string jobToCheck;
            if (Settings.DebugMode)
                LogMessage("Event is   " + _eventText, 5, Color.Yellow);
            if (match.Success)
            {
                jobToCheck = match.Groups[1].Value; // Extracted word
                if (Settings.DebugMode)
                    LogMessage("Event Job is   " + jobToCheck, 5, Color.Blue);

                if (_jobsToGainIntelligence.Contains(jobToCheck))
                {
                    // Value scales with the amount actually useful (capped by the room left to 100).
                    int gain = 1;
                    var gainMatch = Regex.Match(_eventText, @"\+?(\d+)");
                    if (gainMatch.Success) int.TryParse(gainMatch.Groups[1].Value, out gain);
                    int current = ReadDivisionIntel().TryGetValue(jobToCheck, out var cur) ? cur : 0;
                    int useful = Math.Clamp(Math.Min(gain, 100 - current), 0, 100);
                    int perPoint = Settings.FarmingMode == FarmingMode.CatarinaFocus ? 9 : 6;
                    eventOutcome.Add(new Outcome(OutcomeType.GainInformationToInterestingJob, ActionType.Special, useful * perPoint,
                        $" Gain +{gain} intelligence to {jobToCheck} ({current}/100, {useful} useful)"));
                }
                else
                {
                    int amount = 1;
                    var amountMatch = Regex.Match(_eventText, @"\+?(\d+)");
                    if (amountMatch.Success)
                        int.TryParse(amountMatch.Groups[1].Value, out amount);
                    int score = -Math.Max(1, amount) * 8;
                    eventOutcome.Add(new Outcome(OutcomeType.GainInformationToBadJob, ActionType.Special, score,
                        $"Avoid +{amount} {jobToCheck} Intelligence (Skip/Filler division)"));

                }


            }
            return eventOutcome;
        }
        /// Returns the member's current job, or their settings-assigned division if Job == "None"
        private string GetMemberEffectiveJob(string memberName, string currentJob)
        {
            if (currentJob != "None") return currentJob;
            foreach (var kv in Settings.DivisionMembers)
            {
                if (kv.Value.Contains(memberName) && ShouldPreserveMemberPlacement(kv.Key))
                    return kv.Key;
            }
            return "None";
        }

        /// Returns the member's effective score for relationship evaluation.
        /// If member has no current job, compute what their score would be in their intended division.
        private int GetMemberEffectiveScore(BetrayalMaster member)
        {
            if (member.Job != "None") return member.Score;
            var intendedJob = GetMemberEffectiveJob(member.Name, "None");
            if (intendedJob == "None") return 0;
            return GetScore(member.Name, intendedJob, 1);
        }

        private List<Outcome> EventRelationship()
        {
            string pattern = @"([A-Za-z]+(?: [A-Za-z]+)*) and ([A-Za-z]+(?: [A-Za-z]+)*) become (\w+)";

            List<Outcome> eventOutcome = new List<Outcome>();
            Match match = Regex.Match(_eventText, pattern);
            if (Settings.DebugMode)
                LogMessage(" Relationship event ", 5, Color.Red);
            if (match.Success)
            {
                string firstName = match.Groups[1].Value;
                string secondName = match.Groups[2].Value;
                string type = match.Groups[3].Value;
                var member1 = ListBetrayalMasters
                   .Where(master => master.Name == firstName)
                   .OrderByDescending(master => master.Score)
                   .FirstOrDefault();

                var member2 = ListBetrayalMasters
                    .Where(master => master.Name == secondName)
                    .OrderByDescending(master => master.Score)
                    .FirstOrDefault();
                if (Settings.DebugMode)
                {
                    LogMessage($"relation type {type}", 5, Color.Yellow);
                    LogMessage("member 1  " + member1, 5, Color.Yellow);
                    LogMessage("member2 " + member2, 5, Color.Yellow);
                }
                if (member1 != null && member2 != null)
                    AddRelationshipOutcome(eventOutcome, type, member1, member2);
            }

            return eventOutcome;
        }

        private List<Outcome> StealEventRelationship()
        {

            string pattern = @"\+(\d+) (\w+) Intelligence\. ([A-Za-z]+(?: [A-Za-z]+)*) and ([A-Za-z]+(?: [A-Za-z]+)*) become Rivals";

            List<Outcome> eventOutcome = new List<Outcome>();
            Match match = Regex.Match(_eventText, pattern);
            if (Settings.DebugMode)
                LogMessage(" Relationship event ", 5, Color.Red);
            if (match.Success)
            {
                int intelligenceAmount = int.TryParse(match.Groups[1].Value, out int parsedIntelligence) ? parsedIntelligence : 0;
                string encounterType = match.Groups[2].Value;
                string member1name = match.Groups[3].Value;
                string member2name = match.Groups[4].Value;
                var member1 = ListBetrayalMasters
                   .Where(master => master.Name == member1name)
                   .OrderByDescending(master => master.Score)
                   .FirstOrDefault();

                var member2 = ListBetrayalMasters
                    .Where(master => master.Name == member2name)
                    .OrderByDescending(master => master.Score)
                    .FirstOrDefault();

                if (Settings.DebugMode)
                {
                    LogMessage("member 1  " + member1, 5, Color.Yellow);
                    LogMessage("member2 " + member2, 5, Color.Yellow);
                    LogMessage("Safehouse intelligence is   " + encounterType, 5, Color.Blue);

                }





                var stealJob1 = member1 != null ? GetMemberEffectiveJob(member1.Name, member1.Job) : "None";
                var stealJob2 = member2 != null ? GetMemberEffectiveJob(member2.Name, member2.Job) : "None";

                if (Settings.DebugMode && member1 != null && member2 != null)
                    LogMessage($"StealRival: {member1.Name}({stealJob1}) + {member2.Name}({stealJob2}), intelligence={intelligenceAmount} {encounterType}", 5, Color.Cyan);

                // Division type is the primary factor; intel direction is secondary bonus/penalty.
                bool stealGoodIntel = _jobsToGainIntelligence.Contains(encounterType);

                if (member1 != null && member2 != null)
                    AddRelationshipOutcome(eventOutcome, "Rivals", member1, member2);

                int intelligenceScore = stealGoodIntel ? intelligenceAmount * 6 : -intelligenceAmount * 2;
                eventOutcome.Add(new Outcome(
                    stealGoodIntel ? OutcomeType.GainInformationToInterestingJob : OutcomeType.GainInformationToBadJob,
                    ActionType.Special,
                    intelligenceScore,
                    $"+{intelligenceAmount} {encounterType} Intelligence{(stealGoodIntel ? "" : " (Filler)")}"));


            }
            else
            {
                if (Settings.DebugMode)
                    LogMessage("Fail to grab regex", 5, Color.Red);
            }

            return eventOutcome;
        }
        private List<Outcome> StealRanks()
        {

            List<Outcome> eventOutcome = new List<Outcome>();
            string pattern = @"\+\d+ rank\(s\) to ([A-Za-z]+(?: [A-Za-z]+)*)\. ([A-Za-z]+(?: [A-Za-z]+)*) loses all ranks\. ([A-Za-z]+(?: [A-Za-z]+)*) and ([A-Za-z]+(?: [A-Za-z]+)*) become Rivals";





            Match match = Regex.Match(_eventText, pattern);
            if (Settings.DebugMode)
                LogMessage(" Relationship event ", 5, Color.Red);
            if (match.Success)
            {
                string gainRankMembername = match.Groups[1].Value;
                string loseRankMembername = match.Groups[2].Value;
                string member1name = match.Groups[3].Value;
                string member2name = match.Groups[4].Value;
                var member1 = ListBetrayalMasters.FirstOrDefault(master => master.Name == member1name);
                var member2 = ListBetrayalMasters.FirstOrDefault(master => master.Name == member2name);

                if (member1 != null && member2 != null)
                    AddRelationshipOutcome(eventOutcome, "Rivals", member1, member2);

                var gainRankMember = ListBetrayalMasters.FirstOrDefault(master => master.Name == gainRankMembername);
                var loseRankMember = ListBetrayalMasters.FirstOrDefault(master => master.Name == loseRankMembername);

                // Find loseRankMember's intended Farm division (if any)
                string loseRankMemberBestJob = null;
                foreach (var kv in Settings.DivisionMembers)
                {
                    if (Settings.DivisionRoles.TryGetValue(kv.Key, out var fr) && fr == "Farm" && kv.Value.Contains(loseRankMember?.Name ?? ""))
                    { loseRankMemberBestJob = kv.Key; break; }
                }

                if (Settings.DebugMode)
                {
                    LogMessage("loseRankMemberBestJob  " + loseRankMemberBestJob, 5, Color.Yellow);
       


                }
                
                if (gainRankMember.Score >= 30 && loseRankMember.Score >= 30 && badJobsAreFull) // they are both interesting jobs with good members but bad jobs are full so its safe
                {
                    eventOutcome.Add(new Outcome(OutcomeType.RivalBetweenGoodInterestingMember, ActionType.Special, 100, $"{member1name} {member2name} become rivals "));
                    eventOutcome.Add(new Outcome(OutcomeType.PromoteGoodMemberInInterestingJob, ActionType.Special, 100, $"{gainRankMember.Name} Gains one rank"));
                } else 


                if (gainRankMember.Score >= 30 && loseRankMember.Score == -1 && loseRankMemberBestJob == gainRankMember.Job)
                {
                    eventOutcome.Add(new Outcome(OutcomeType.RivalBetweenGoodInterestingMember, ActionType.Special, 100, $"{member1name} {member2name} become rivals "));
                    eventOutcome.Add(new Outcome(OutcomeType.RemoveGoodMemberFromBadJob, ActionType.Special, 100, $"{loseRankMember.Name} Loses all ranks from his bad job"));
                    eventOutcome.Add(new Outcome(OutcomeType.PromoteGoodMemberInInterestingJob, ActionType.Special, 100, $"{gainRankMember.Name} Gains one rank"));

                }
                // I only want to do if gain rank score is > 30 and lose score is <1 bestjob for lose rank == gainrank job  

                // gain good and lose bad

                // gain bad and lose good


                // gain good and lose good

                // gain bad and lose bad

                if (Settings.DebugMode)
                {
                    LogMessage("gainRankMember 1  " + gainRankMember, 5, Color.Yellow);
                    LogMessage("loseRankMember " + loseRankMember, 5, Color.Yellow);


                }






                if (((gainRankMember.Score + loseRankMember.Score) > 30) && loseRankMember.Score <= -1) // at least one of them is good, so we want a relationship
                {
                    //Graphics.DrawFrame(_SpecialButton, Color.Green, 6);
                    if (Settings.DebugMode)
                        LogMessage("at least one of them is good, so we want a relationship " + member2, 5, Color.Green);
                }
                else
                // if member is on the wrong place


                {
                    //Graphics.DrawFrame(_ReleaseButton, Color.Green, 6);
                }


            }
            else
            {
                if (Settings.DebugMode)
                    LogMessage("Fail to grab regex", 5, Color.Red);
            }

            return eventOutcome;
        }
        private List<Outcome> PromoteNPC()
        {
            List<Outcome> eventOutcome = new List<Outcome>();
            string pattern = @"([A-Za-z]+(?: [A-Za-z]+)*) moves to ([A-Za-z]+(?: [A-Za-z]+)*)\. ([A-Za-z]+(?: [A-Za-z]+)*) and ([A-Za-z]+(?: [A-Za-z]+)*) become (\w+)";




            Match match = Regex.Match(_eventText, pattern);
            if (Settings.DebugMode)
                LogMessage(" Relationship event ", 5, Color.Red);
            if (match.Success)
            {

                string movingMemberName = match.Groups[1].Value;
                string destination = match.Groups[2].Value;
                string member1name = match.Groups[3].Value;
                string member2name = match.Groups[4].Value;
                string relationshipType = match.Groups[5].Value;
                var member1 = ListBetrayalMasters
                      .Where(master => master.Name == member1name)
                      .OrderByDescending(master => master.Score)
                      .FirstOrDefault();

                var member2 = ListBetrayalMasters
                    .Where(master => master.Name == member2name)
                    .OrderByDescending(master => master.Score)
                    .FirstOrDefault();

                var movingmember = ListBetrayalMasters.FirstOrDefault(master => master.Name == movingMemberName);
                

                int matchingScore = GetScore(movingMemberName, destination, movingmember?.Rank ?? 1);
                var matchingMember = new MasterWeight(movingMemberName, destination, movingmember?.Rank ?? 1, matchingScore);

                





                if (Settings.DebugMode)
                {
                    LogMessage("member 1  " + member1, 5, Color.Yellow);
                    LogMessage("member2 " + member2, 5, Color.Yellow);
                    LogMessage("moving member " + movingmember, 5, Color.Yellow);


                }



                if (matchingMember is not null)
                {

                    if(matchingMember.Score >= 30)
                    {
                        eventOutcome.Add(new Outcome(OutcomeType.AddGoodMemberToInterestingJob, ActionType.Special, 100, $"{matchingMember.Name} Moves to {destination} "));

                    }
                    else if (matchingMember.Score >=1)
                    {
                        eventOutcome.Add(new Outcome(OutcomeType.AddBadMemberToBadJob, ActionType.Special, 50, $"{matchingMember.Name} Moves to {destination} "));

                    }else if (matchingMember.Score == -1)
                    {
                        eventOutcome.Add(new Outcome(OutcomeType.AddBadMemberToInterestingJob, ActionType.Special, -100, $"{matchingMember.Name} Moves to {destination} "));

                    }

                    if (member1 != null && member2 != null)
                        AddRelationshipOutcome(eventOutcome, relationshipType, member1, member2);

                   
                }






            }
            else
            {
                if (Settings.DebugMode)
                    LogMessage("Fail to grab regex", 5, Color.Red);
            }

            return eventOutcome;
        }
        private List<Outcome> SwapLeaderRelationship()
        {

            List<Outcome> eventOutcome = new List<Outcome>();
            string pattern = @"([A-Za-z]+(?: [A-Za-z]+)*) becomes leader of ([A-Za-z]+(?: [A-Za-z]+)*)\. ([A-Za-z]+(?: [A-Za-z]+)*) and ([A-Za-z]+(?: [A-Za-z]+)*) become Rivals";


            Match match = Regex.Match(_eventText, pattern);
            if (Settings.DebugMode)
                LogMessage(" Relationship event ", 5, Color.Red);
            if (match.Success)
            {
                string newLeaderName = match.Groups[1].Value;
                string safehouse = match.Groups[2].Value;
                string member1name = match.Groups[3].Value;
                string member2name = match.Groups[4].Value;
                var member1 = ListBetrayalMasters.FirstOrDefault(master => master.Name == member1name);
                var member2 = ListBetrayalMasters.FirstOrDefault(master => master.Name == member2name);
                var swapScore1 = member1 != null ? GetMemberEffectiveScore(member1) : 0;
                var swapScore2 = member2 != null ? GetMemberEffectiveScore(member2) : 0;

                var newLeader = ListBetrayalMasters.FirstOrDefault(master => master.Name == newLeaderName);

                if (Settings.DebugMode)
                {
                    LogMessage("member 1  " + member1, 5, Color.Yellow);
                    LogMessage("member2 " + member2, 5, Color.Yellow);
                    LogMessage("Safehouse is   " + safehouse, 5, Color.Blue);
                    LogMessage("New leader is  is   " + newLeader, 5, Color.Blue);

                }


                //
                if (member1 != null && member2 != null)
                    AddRelationshipOutcome(eventOutcome, "Rivals", member1, member2);



            }
            else
            {
                if (Settings.DebugMode)
                    LogMessage("Fail to grab regex", 5, Color.Red);
            }
            return eventOutcome;
        }
        private List<Outcome> EventSwap(SyndicatePanel syndicatePanel)
        {
            var eventData = syndicatePanel.EventElement ?? syndicatePanel.BetrayalEventData;
            if (eventData == null) return new List<Outcome>();
            var _eventText = eventData.EventText ?? string.Empty;
            string pattern = @"([A-Za-z]+(?: [A-Za-z]+)*)\s+moves\s+to\s+(\w+)\.\s+([A-Za-z]+(?: [A-Za-z]+)*)\s+moves\s+to\s+(\w+)";
            Match match = Regex.Match(_eventText, pattern);
            List<Outcome> eventOutcome = new List<Outcome>();
            if (match.Success) // swap
            {
                string firstPerson = match.Groups[1].Value.Trim();
                string firstDestination = match.Groups[2].Value.Trim();
                string secondPerson = match.Groups[3].Value.Trim();
                string secondDestination = match.Groups[4].Value.Trim();

                if (Settings.DebugMode)
                {
                    LogMessage($"First Person: {firstPerson} -> {firstDestination}", 5, Color.Blue);
                    LogMessage($"Second Person: {secondPerson} -> {secondDestination}", 5, Color.Blue);
                }

                var firstMaster = ListBetrayalMasters.FirstOrDefault(master => master.Name.Equals(firstPerson, StringComparison.OrdinalIgnoreCase));
                var secondMaster = ListBetrayalMasters.FirstOrDefault(master => master.Name.Equals(secondPerson, StringComparison.OrdinalIgnoreCase));

                int firstOldScore = firstMaster?.Score ?? 0;
                int secondOldScore = secondMaster?.Score ?? 0;

                int firstNewScore = GetScore(firstPerson, firstDestination, firstMaster?.Rank ?? 1);
                int secondNewScore = GetScore(secondPerson, secondDestination, secondMaster?.Rank ?? 1);

                int netGain = (firstNewScore - firstOldScore) + (secondNewScore - secondOldScore);

                if (netGain > 0)
                {
                    eventOutcome.Add(new Outcome(OutcomeType.AddGoodMemberToInterestingJob, ActionType.Special, 80 + netGain,
                        $"Swap: {firstPerson} -> {firstDestination} ({firstNewScore:+#;-#;0}), {secondPerson} -> {secondDestination} ({secondNewScore:+#;-#;0}) (Net +{netGain})"));
                }
                else
                {
                    eventOutcome.Add(new Outcome(OutcomeType.AddBadMemberToInterestingJob, ActionType.Special, -80 + netGain,
                        $"Swap: {firstPerson} -> {firstDestination}, {secondPerson} -> {secondDestination} worsens placement (Net {netGain})"));
                }
            }
            return eventOutcome;
        }



        public int GetScore(string name, string job, int rank)
        {
            if (Settings.FarmingMode == FarmingMode.CatarinaFocus)
            {
                var intendedPreservedDivision = GetRequiredMemberDivisions()
                    .FirstOrDefault(kv => kv.Value.Contains(name)).Key;

                if (!string.IsNullOrEmpty(intendedPreservedDivision))
                    return job == intendedPreservedDivision
                        ? 30 + Math.Clamp(rank, 0, 3) * 10
                        : -1;

                if (!ShouldPreserveMemberPlacement(job))
                    return job == "None" ? 0 : 1;

                bool belongsHere = Settings.DivisionMembers.TryGetValue(job, out var preservedMembers)
                    && preservedMembers.Contains(name);
                return belongsHere ? 30 + Math.Clamp(rank, 0, 3) * 10 : -1;
            }

            // Settings are authoritative: if the job is configured in DivisionRoles, use settings
            if (Settings.DivisionRoles.TryGetValue(job, out var role))
            {
                if (role == "Farm")
                {
                    // Assigned member in their farm division = high value
                    if (Settings.DivisionMembers.TryGetValue(job, out var divMembers) && divMembers.Contains(name))
                        return 30 + Math.Clamp(rank, 0, 3) * 10;
                    // Any other member in a farm division = unwanted
                    return -1;
                }
                else
                {
                    // Filler division: Farm members that wandered here = bad
                    var allFarmMembers = Settings.DivisionMembers
                        .Where(kv => Settings.DivisionRoles.TryGetValue(kv.Key, out var fr) && fr == "Farm")
                        .SelectMany(kv => kv.Value)
                        .ToHashSet();
                    if (allFarmMembers.Contains(name))
                        return -1; // Farm member stuck in Filler = wrong place
                    return 1; // Regular filler member = OK
                }
            }

            return 0; // Unknown job — not configured in Settings
        }

        private void NormalizePreviewedRelationshipState()
        {
            _relationshipNormalization = "live ServerData (no relationship preview)";
            var match = Regex.Match(_eventText,
                @"([A-Za-z]+(?: [A-Za-z]+)*) and ([A-Za-z]+(?: [A-Za-z]+)*) become (Rivals|Trusted|Neutral)",
                RegexOptions.IgnoreCase);
            if (!match.Success)
                return;

            var first = ListBetrayalMasters.FirstOrDefault(x =>
                x.Name.Equals(match.Groups[1].Value.Trim(), StringComparison.OrdinalIgnoreCase));
            var second = ListBetrayalMasters.FirstOrDefault(x =>
                x.Name.Equals(match.Groups[2].Value.Trim(), StringComparison.OrdinalIgnoreCase));
            if (first == null || second == null)
            {
                _relationshipNormalization = "preview detected, member pair not found";
                return;
            }

            string preview = match.Groups[3].Value;
            string? baseline = preview.Equals("Rivals", StringComparison.OrdinalIgnoreCase) ? "Trusted"
                : preview.Equals("Neutral", StringComparison.OrdinalIgnoreCase) ? "Rival"
                : null;

            SetRelationshipState(first, second, baseline);
            SetRelationshipState(second, first, baseline);
            _relationshipNormalization = baseline == null
                ? $"{first.Name} / {second.Name}: preview Trusted, baseline no link"
                : $"{first.Name} / {second.Name}: preview {preview}, baseline assumed {baseline}";

            if (Settings.DebugMode.Value)
                LogMessage($"[BetrayalHelper] Relationship preview normalized: {_relationshipNormalization}", 5, Color.Yellow);
        }

        private static void SetRelationshipState(BetrayalMaster member, BetrayalMaster other, string? status)
        {
            member.RelationshipStatuses.Remove(other.Name);
            member.Relationships.RemoveAll(x => x.Equals(other.Name, StringComparison.OrdinalIgnoreCase));
            if (status == null)
                return;

            member.RelationshipStatuses[other.Name] = status;
            member.Relationships.Add(other.Name);
        }

        private void UpdateMasterGoalStatus(BetrayalMaster master)
        {
            if (master == null) return;

            string targetDiv = "";
            if (Settings.DivisionMembers != null)
            {
                foreach (var kvp in Settings.DivisionMembers)
                {
                    if (kvp.Value != null && kvp.Value.Contains(master.Name))
                    {
                        targetDiv = kvp.Key;
                        break;
                    }
                }
            }
            master.TargetDivision = targetDiv;

            bool isTargetFarm = !string.IsNullOrEmpty(targetDiv)
                && Settings.DivisionRoles.TryGetValue(targetDiv, out var tr) && tr == "Farm";

            bool currentDivIsFarm = Settings.DivisionRoles.TryGetValue(master.Job, out var cr) && cr == "Farm";
            bool currentDivIsFiller = Settings.DivisionRoles.TryGetValue(master.Job, out cr) && cr == "Filler";
            bool isOutside = master.Job == "None" || string.IsNullOrEmpty(master.Job) || master.Job == "Outside";

            if (Settings.FarmingMode == FarmingMode.CatarinaFocus)
            {
                bool isPreserved = Settings.CatarinaPreserveDivision.TryGetValue(master.Job, out var pres) && pres;
                if (isPreserved && !string.IsNullOrEmpty(targetDiv) && master.Job.Equals(targetDiv, StringComparison.OrdinalIgnoreCase))
                {
                    master.IsWellPlaced = true;
                    master.GoalStatus = master.Rank >= 3 ? "READY" : "NEEDS_RANK";
                    master.GoalAction = master.Rank >= 3 ? "READY (Preserved - Run Safehouse)" : $"Rank Up (Preserved R{master.Rank}/3)";
                }
                else
                {
                    master.IsWellPlaced = !isOutside;
                    master.GoalStatus = isOutside ? "OUTSIDE" : (master.Rank >= 3 ? "READY" : "NEEDS_RANK");
                    master.GoalAction = isOutside ? "Recruit to any division" : $"Rank {master.Rank}/3 → Interrogate when high rank for Medallion/Intel";
                }
                return;
            }

            // SafehouseRewards (5-5-2-2) Mode:
            if (isTargetFarm)
            {
                if (master.Job.Equals(targetDiv, StringComparison.OrdinalIgnoreCase))
                {
                    master.IsWellPlaced = true;
                    if (master.Rank >= 3)
                    {
                        master.GoalStatus = "READY";
                        master.GoalAction = master.IsLeader ? "READY! (Leader - Raid Safehouse)" : "READY! (Raid Safehouse)";
                    }
                    else
                    {
                        master.GoalStatus = "NEEDS_RANK";
                        master.GoalAction = $"Rank Up (R{master.Rank}/3 - Execute)";
                    }
                }
                else if (isOutside)
                {
                    master.IsWellPlaced = false;
                    master.GoalStatus = "OUTSIDE";
                    master.GoalAction = $"Recruit into {targetDiv} (Bargain/Execute when appears)";
                }
                else
                {
                    master.IsWellPlaced = false;
                    master.GoalStatus = "MISPLACED";
                    master.GoalAction = $"MISPLACED! Move to {targetDiv} (Interrogate out or Swap)";
                }
            }
            else
            {
                // Not a target farm member
                if (currentDivIsFarm)
                {
                    master.IsWellPlaced = false;
                    master.GoalStatus = "MISPLACED";
                    master.GoalAction = "INTRUDER! Kick out (INTERROGATE to Outside)";
                }
                else if (currentDivIsFiller)
                {
                    master.IsWellPlaced = true;
                    master.GoalStatus = "FILLER";
                    master.GoalAction = "Filler (Build Rivalries / Red Links)";
                }
                else
                {
                    master.IsWellPlaced = true;
                    master.GoalStatus = "OUTSIDE";
                    master.GoalAction = "Leave Outside (Reserve pool)";
                }
            }
        }

        private void AddRelationshipOutcome(List<Outcome> outcomes, string relationshipType, BetrayalMaster first, BetrayalMaster second)
        {
            string firstJob = GetMemberEffectiveJob(first.Name, first.Job);
            string secondJob = GetMemberEffectiveJob(second.Name, second.Job);
            if (Settings.FarmingMode == FarmingMode.CatarinaFocus
                && (!ShouldPreserveMemberPlacement(firstJob) || !ShouldPreserveMemberPlacement(secondJob)))
            {
                int score = relationshipType.Equals("Rivals", StringComparison.OrdinalIgnoreCase) ? 20
                    : relationshipType.Equals("Trusted", StringComparison.OrdinalIgnoreCase) ? 10
                    : 0;
                outcomes.Add(new Outcome(
                    relationshipType.Equals("Rivals", StringComparison.OrdinalIgnoreCase)
                        ? OutcomeType.RivalBetweenGoodInterestingMember
                        : OutcomeType.FriendlyBetweenGoodInterestingMember,
                    ActionType.Special,
                    score,
                    $"{first.Name} + {second.Name}: placement ignored in Catarina Focus"));
                return;
            }

            bool firstFarm = Settings.DivisionRoles.TryGetValue(firstJob, out var firstRole) && firstRole == "Farm";
            bool secondFarm = Settings.DivisionRoles.TryGetValue(secondJob, out var secondRole) && secondRole == "Farm";
            bool firstFiller = Settings.DivisionRoles.TryGetValue(firstJob, out firstRole) && firstRole == "Filler";
            bool secondFiller = Settings.DivisionRoles.TryGetValue(secondJob, out secondRole) && secondRole == "Filler";
            bool crossGroup = (firstFarm && secondFiller) || (firstFiller && secondFarm);
            bool sameGroup = (firstFarm && secondFarm) || (firstFiller && secondFiller);

            if (relationshipType.Equals("Rivals", StringComparison.OrdinalIgnoreCase))
            {
                int newValue = crossGroup ? 140 : sameGroup ? 70 : 30;
                string? currentStatus = GetRelationshipStatusBeforeChoice(first, second);
                int existingValue = currentStatus == null ? 0 : GetExistingRelationshipValue(first.Name, second.Name, currentStatus);
                int score = newValue - existingValue;
                outcomes.Add(new Outcome(
                    OutcomeType.RivalBetweenGoodInterestingMember,
                    ActionType.Special,
                    score,
                    $"{first.Name}({firstJob}) + {second.Name}({secondJob}) become Rivals — {(crossGroup ? "high-value cross-group link" : sameGroup ? "useful same-group link" : "useful link")}{(currentStatus == null ? "" : $", replaces {currentStatus}")}"));
            }
            else if (relationshipType.Equals("Trusted", StringComparison.OrdinalIgnoreCase))
            {
                int newValue = crossGroup ? 65 : sameGroup ? 30 : 10;
                string? currentStatus = GetRelationshipStatusBeforeChoice(first, second);
                int existingValue = currentStatus == null ? 0 : GetExistingRelationshipValue(first.Name, second.Name, currentStatus);
                int score = newValue - existingValue;
                outcomes.Add(new Outcome(
                    OutcomeType.FriendlyBetweenGoodInterestingMember,
                    ActionType.Special,
                    score,
                    $"{first.Name}({firstJob}) + {second.Name}({secondJob}) become Trusted — {(crossGroup ? "cross-group Rival seed" : sameGroup ? "useful same-group link" : "useful link")}"));
            }
            else
            {
                string? currentStatus = GetRelationshipStatusBeforeChoice(first, second);
                int score = currentStatus == null ? 0 : -GetExistingRelationshipValue(first.Name, second.Name, currentStatus);
                outcomes.Add(new Outcome(OutcomeType.NeutralWithGoodMember, ActionType.Special, score,
                    $"{first.Name}({firstJob}) + {second.Name}({secondJob}) become Neutral — {(currentStatus == null ? "no known edge" : $"loses {currentStatus} link")}"));
            }
        }

        private void AddInterrogationStrategyOutcome(List<Outcome> outcomes, BetrayalMaster member)
        {
            double preserveRankChance = KeepRankChance;
            bool shouldRun = ShouldRunDivision(member.Job);

            // Interrogation generates intelligence over three encounters but removes
            // the member from the reinforcement pool for that time.
            int intelligenceValue = shouldRun ? member.Rank * 10 : -member.Rank * 8;
            int expectedRankLossPenalty = member.Score >= 30
                ? -(int)Math.Round(25 * (1.0 - preserveRankChance))
                : 0;
            int rivalLinks = member.RelationshipStatuses.Values.Count(status => status == "Rival");
            int trustedLinks = member.RelationshipStatuses.Values.Count(status => status == "Trusted");
            int networkAbsencePenalty = -Math.Min(40, rivalLinks * 7 + trustedLinks * 4);
            int total = intelligenceValue + expectedRankLossPenalty + networkAbsencePenalty;

            outcomes.Add(new Outcome(
                shouldRun ? OutcomeType.GainInformationToInterestingJob : OutcomeType.GainInformationToBadJob,
                ActionType.Interrogate,
                total,
                $"Interrogate strategy: intel {intelligenceValue:+#;-#;0}, rank-loss risk {expectedRankLossPenalty:+#;-#;0}, network absence {networkAbsencePenalty:+#;-#;0} (R{rivalLinks}/T{trustedLinks}), keep-rank {preserveRankChance:P0}"));
        }

        private IEnumerable<(string First, string Second, string Status)> GetUniqueRelationships()
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var member in ListBetrayalMasters)
            {
                foreach (var relation in member.RelationshipStatuses)
                {
                    string first = string.Compare(member.Name, relation.Key, StringComparison.OrdinalIgnoreCase) <= 0 ? member.Name : relation.Key;
                    string second = first == member.Name ? relation.Key : member.Name;
                    string key = $"{first}\u001f{second}";
                    if (seen.Add(key))
                        yield return (first, second, relation.Value);
                }
            }
        }

        private static string? GetRelationshipStatusBeforeChoice(BetrayalMaster first, BetrayalMaster second)
        {
            string? currentStatus = first.RelationshipStatuses.TryGetValue(second.Name, out var existingStatus)
                ? existingStatus
                : null;

            return currentStatus;
        }

        private string GetRelationshipStrategicLabel(string firstName, string secondName, string status)
        {
            int value = GetExistingRelationshipValue(firstName, secondName, status);
            if (status == "Rival")
                return value >= 100 ? "high-value cross-group Rival" : value > 0 ? "useful Rival" : "unclassified Rival";
            return value >= 60 ? "cross-group Trusted seed" : value > 0 ? "useful Trusted" : "unclassified Trusted";
        }

        private int GetExistingRelationshipValue(string firstName, string secondName, string status)
        {
            var first = ListBetrayalMasters.FirstOrDefault(member => member.Name.Equals(firstName, StringComparison.OrdinalIgnoreCase));
            var second = ListBetrayalMasters.FirstOrDefault(member => member.Name.Equals(secondName, StringComparison.OrdinalIgnoreCase));
            if (first == null || second == null)
                return 0;

            string firstJob = GetMemberEffectiveJob(first.Name, first.Job);
            string secondJob = GetMemberEffectiveJob(second.Name, second.Job);
            string firstRole = Settings.DivisionRoles.TryGetValue(firstJob, out var configuredFirstRole) ? configuredFirstRole : "";
            string secondRole = Settings.DivisionRoles.TryGetValue(secondJob, out var configuredSecondRole) ? configuredSecondRole : "";
            bool crossGroup = (firstRole == "Farm" && secondRole == "Filler") || (firstRole == "Filler" && secondRole == "Farm");
            bool sameGroup = firstRole.Length > 0 && firstRole == secondRole;

            if (status == "Rival")
                return crossGroup ? 140 : sameGroup ? 70 : 30;
            if (status == "Trusted")
                return crossGroup ? 65 : sameGroup ? 30 : 10;
            return 0;
        }

        private int ScoreRemovingExistingRivalries(string? division)
        {
            int retainedTopologyValue = 0;
            foreach (var link in GetUniqueRelationships().Where(link => link.Status == "Rival"))
            {
                if (!string.IsNullOrEmpty(division))
                {
                    var first = ListBetrayalMasters.FirstOrDefault(member => member.Name == link.First);
                    var second = ListBetrayalMasters.FirstOrDefault(member => member.Name == link.Second);
                    if (first?.Job != division && second?.Job != division)
                        continue;
                }
                retainedTopologyValue += GetExistingRelationshipValue(link.First, link.Second, link.Status);
            }

            // Rival links are valuable reinforcement edges regardless of division.
            return -retainedTopologyValue;
        }

        private int GetRankAfterExecute(int currentRank)
        {
            // Execute = +1 rank, plus an expected extra rank from Enduring Legacy / small nodes.
            double expected = Math.Max(0, currentRank) + 1 + ExtraRankChance;
            return (int)Math.Min(3, Math.Round(expected, MidpointRounding.AwayFromZero));
        }

        private void AddExecuteIntelligenceOutcome(List<Outcome> outcomes, string division, int resultingRank)
        {
            // Lethal Extraction is removed in current PoE: no extra safehouse intelligence on Execute
            return;
        }

        /// Returns 30 if this member is a Farm division member, 1 if Filler, 0 if unknown.
        private int GetMemberBestPossibleScore(string name)
        {
            if (Settings.FarmingMode == FarmingMode.CatarinaFocus)
            {
                var preserved = GetRequiredMemberDivisions().FirstOrDefault(kv => kv.Value.Contains(name));
                return string.IsNullOrEmpty(preserved.Key) ? 1 : GetScore(name, preserved.Key, 1);
            }

            foreach (var kv in Settings.DivisionMembers)
            {
                if (Settings.DivisionRoles.TryGetValue(kv.Key, out var r))
                {
                    if (r == "Farm" && kv.Value.Contains(name)) return 30;
                    if (r == "Filler" && kv.Value.Contains(name)) return 1;
                }
            }
            return 0;
        }



        public void PrintHighestScoringActionType(List<Outcome> outcomes)
        {
            _lastOutcomes = outcomes?.ToList() ?? new List<Outcome>();
            _lastRecommendedAction = null;
            _lastRecommendedScore = 0;
            var syndicatePanel = GameController?.Game?.IngameState?.IngameUi?.SyndicatePanel;
            bool interrogationQueueFull = GetInterrogationQueueFullForCurrentEvent(syndicatePanel);
            _lastInterrogationQueueFull = interrogationQueueFull;

            if (outcomes == null || outcomes.Count == 0)
            {
                LogMessage("No outcomes provided." ,5,Color.BlueViolet);
                return;
            }

            // Group by ActionType and calculate total scores
            var topGroup = outcomes
                .GroupBy(o => o.Action)
                .Select(g => new
                {
                    ActionType = g.Key,
                    TotalScore = interrogationQueueFull && g.Key == ActionType.Interrogate
                        ? -1_000_000
                        : g.Sum(o => o.Score),
                    Outcomes = g.ToList()
                })
                .OrderByDescending(g => g.TotalScore)
                .FirstOrDefault();

            if (topGroup == null)
            {
 
                LogMessage("No valid groups found", 5, Color.BlueViolet);
                return;
            }

            _lastRecommendedAction = topGroup.ActionType;
            _lastRecommendedScore = topGroup.TotalScore;

            LogMessage($"ActionType with highest score: {topGroup.ActionType}, Total Score: {topGroup.TotalScore}", 5, Color.BlueViolet);
            LogMessage("Matching Outcomes:", 5, Color.BlueViolet);

            //foreach (var outcome in topGroup.Outcomes)
            for (int i = 0; i < topGroup.Outcomes.Count; i++)
            {
                LogMessage(i + ": " + topGroup.Outcomes[i].ToString(), 5, Color.BlueViolet); // Ensure Outcome overrides ToString()
            }

            RectangleF buttonToHighlight;
            string recommendedText;
            if (topGroup.ActionType == ActionType.Special)
            {
                buttonToHighlight = _SpecialButton;
                recommendedText = $">> {_eventAction.ToUpper()} << ({topGroup.TotalScore} pts)";
            }
            else if (topGroup.ActionType == ActionType.Interrogate)
            {
                buttonToHighlight = _InterrogateButton;
                recommendedText = $">> INTERROGATE << ({topGroup.TotalScore} pts)";
            }
            else
            {
                buttonToHighlight = _ReleaseButton;
                recommendedText = $">> RELEASE << ({topGroup.TotalScore} pts)";
            }

            if (!buttonToHighlight.IsEmpty && buttonToHighlight.Width > 0)
            {
                Graphics.DrawFrame(buttonToHighlight, Color.Green, 6);
                var fill = new Color(0, 255, 0, 45);
                Graphics.DrawBox(buttonToHighlight, fill);

                // The green frame already marks the recommended button; the raw label (action id + points) is debug-only.
                if (!Settings.DebugMode.Value) return;
                var labelPos = new Vector2N(buttonToHighlight.X, Math.Max(0, buttonToHighlight.Y - 26));
                var overlayDrawList = ImGui.GetForegroundDrawList();
                uint textColor = ImGui.ColorConvertFloat4ToU32(new System.Numerics.Vector4(0.2f, 1f, 0.2f, 1f));
                uint shadowColor = ImGui.ColorConvertFloat4ToU32(new System.Numerics.Vector4(0f, 0f, 0f, 0.9f));
                unsafe
                {
                    overlayDrawList.AddText(ImGui.GetFont(), 22f, new Vector2N(labelPos.X + 2, labelPos.Y + 2), shadowColor, recommendedText);
                    overlayDrawList.AddText(ImGui.GetFont(), 22f, labelPos, textColor, recommendedText);
                }
            }
        }

        private bool GetInterrogationQueueFullForCurrentEvent(SyndicatePanel? syndicatePanel)
        {
            string eventKey = $"{_eventAction}|{_masterText}|{_eventText}";
            if (eventKey == _interrogationQueueEventKey)
                return _cachedInterrogationQueueFull;

            _interrogationQueueEventKey = eventKey;
            _cachedInterrogationQueueFull = ReadInterrogationQueueFull(syndicatePanel);
            return _cachedInterrogationQueueFull;
        }

        private bool ReadInterrogationQueueFull(SyndicatePanel? syndicatePanel)
        {
            if (syndicatePanel == null)
            {
                _lastInterrogationCounterText = "Syndicate panel unavailable";
                return false;
            }

            try
            {
                var prisoners = new List<string>();

                var syndicateStates = syndicatePanel.SyndicateStates;
                if (syndicateStates != null && syndicateStates.Count > 0)
                {
                    foreach (var state in syndicateStates)
                    {
                        if (state?.UIElement == null || state.UIElement.ChildCount <= 18)
                            continue;

                        var prisonerMarker = state.UIElement.GetChildAtIndex(18);
                        if (prisonerMarker?.IsVisible == true)
                            prisoners.Add(state.Target?.Name ?? "unknown");
                    }
                }
                else
                {
                    var boardContainer = syndicatePanel.Children?.Count > 0 ? syndicatePanel.Children[0] : null;
                    if (boardContainer != null && boardContainer.Children != null)
                    {
                        for (int c = 9; c < boardContainer.Children.Count && c <= 23; c++)
                        {
                            var card = boardContainer.Children[c];
                            if (card != null && card.IsVisible && card.Position.Y > 1700)
                            {
                                string name = "";
                                try { name = card.Children[3]?.Children[0]?.Text?.Trim() ?? ""; } catch { }
                                if (!string.IsNullOrEmpty(name) && allMasters.Contains(name))
                                    prisoners.Add(name);
                            }
                        }
                    }
                }

                _lastInterrogationCounterText = $"{prisoners.Count}/3 occupied"
                    + (prisoners.Count > 0 ? $" ({string.Join(", ", prisoners)})" : "");
                return prisoners.Count >= 3;
            }
            catch
            {
                _lastInterrogationCounterText = "prisoner state read failed";
                return false;
            }
        }




    }
}
