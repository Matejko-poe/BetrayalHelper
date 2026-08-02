using ExileCore;
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

        private static readonly (string Category, string Question, string Answer)[] FaqEntries =
        {
            ("Overview", "What does BetrayalHelper do?", "It reads the visible Immortal Syndicate board and encounter choice, scores Special, Interrogate and Release, then highlights the highest-scoring button. It never clicks for you."),
            ("Overview", "What is the main strategy?", "Safehouse Rewards uses a 5-5-2-2 board: two compact Farm divisions with two desired members and two Filler divisions with five members."),
            ("Configuration", "What is the difference between Farm/Filler and Run/Skip?", "Farm/Filler describes desired member placement. Run/Skip independently describes whether intelligence and completion of that safehouse are desirable."),
            ("Configuration", "Why is the first member marked [L]?", "The first configured member is the preferred leader. Use the arrow buttons to change leader priority."),
            ("Configuration", "Is two the division capacity?", "No. Five is the game limit. Two is the strategic target for a compact Farm division."),
            ("Relationships", "Is every Rival line good?", "No. Rival between Farm and Filler is valuable. Farm-Farm and Filler-Filler links can waste reinforcement opportunities and are normally penalized."),
            ("Relationships", "Is Trusted useful?", "Yes. Cross-group Trusted is a setup edge: Trusted members are likely to offer Betray later, converting the edge to Rival."),
            ("Relationships", "When is Neutral good?", "Neutral is good when it removes an unwanted same-group edge. It is bad when it removes a desired Farm-Filler edge."),
            ("Relationships", "Why avoid removing all rivalries?", "It destroys accumulated board topology and takes many encounters to rebuild. Release is usually safer."),
            ("Relationships", "Do links guarantee reinforcements?", "No. They affect eligibility and likelihood. Encounter rules, imprisonment, ranks, leaders, Atlas modifiers and random selection still apply."),
            ("Relationships", "How does the plugin identify Rival and Trusted links?", "It reads ServerData.BetrayalMemberRelationships directly. Rival and Trusted are supplied by ExileCore from game memory; the plugin does not infer them from line colours."),
            ("Relationships", "Why are members without relationships left until last?", "When several defeated members are available, connected members are handled first. Leaving an unconnected member for last improves the chance that the remaining encounter option creates a useful new relationship."),
            ("Scoring", "How are actions selected?", "Every parsed effect becomes an Outcome. Outcomes are grouped by Special, Interrogate or Release and summed. The highest total is highlighted."),
            ("Scoring", "Why can Release win?", "Release has a neutral baseline. It wins when changing the board and interrogating are both expected to be harmful."),
            ("Scoring", "How is a correct Farm member valued?", "Its base placement value is 30 plus 10 for each rank. Event-specific effects are added separately."),
            ("Scoring", "Can a good Rival option still be rejected?", "Yes. Rank theft, expelling a desired member, a bad leader change or division-wide damage can outweigh the relationship benefit."),
            ("Execute", "What does Test of Loyalty change?", "When enabled, Execute is modelled as gaining two ranks instead of one, capped at rank 3."),
            ("Execute", "What does Lethal Extraction change?", "Execute adds intelligence equal to twice the resulting rank. Run divisions value it positively; Skip divisions value it negatively."),
            ("Interrogate", "What does Interrogate cost?", "It grants intelligence but temporarily removes the member from encounters and reinforcements. Without protection it also risks losing rank after completion."),
            ("Interrogate", "How do Unbreaking scarabs change decisions?", "Set 0, 1 or 2 scarabs. The model uses 0%, 50% or 100% expected rank preservation, while retaining the temporary absence penalty."),
            ("Interrogate", "Why not always interrogate with two scarabs?", "The member still disappears for three encounters, may lose leadership immediately, and may generate intelligence for a Skip division."),
            ("Safehouses", "Why did the plugin warn that a safehouse is ready?", "A division reached 100 intelligence. The recommendation also checks whether its desired members are placed and ranked."),
            ("Safehouses", "Does completing Catarina reset the board?", "Current game behaviour no longer resets the Immortal Syndicate board, but Catarina Focus remains a secondary and less-tested mode."),
            ("Atlas", "Why are drop-only Atlas passives missing?", "Settings are exposed only when they change a board decision. Extra veiled drops or equipment do not currently alter Special, Interrogate or Release scoring."),
            ("Debug", "Why did the old DebugMode appear to show nothing?", "It only emitted five-second ExileCore log messages during specific events. The Debug Stats tab now provides persistent live data."),
            ("Debug", "What does Mega Debug do?", "It adds verbose per-member and parser logging to the ExileCore debug log. The persistent table remains available in Debug Stats."),
            ("Debug", "What should I capture for a wrong recommendation?", "Record Action.Id, full English event text, involved members, ranks, divisions, leaders, strategy settings, scarab count, expected action and highlighted action."),
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
            return true;
        }

        public override void AreaChange(AreaInstance area)
        {
            hasClosedWindow = false;
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
                int swapA = -1, swapB = -1;

                // button size — tall enough to click comfortably
                var btnSize = new System.Numerics.Vector2(34, 22);
                var btnSizeX = new System.Numerics.Vector2(28, 22);

                var tableFlags = ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.RowBg;
                if (ImGui.BeginTable($"##members_{div}", 5, tableFlags))
                {
                    ImGui.TableSetupColumn("##role", ImGuiTableColumnFlags.WidthFixed, 30);   // [L] / #N
                    ImGui.TableSetupColumn("Name",   ImGuiTableColumnFlags.WidthStretch);     // name
                    ImGui.TableSetupColumn("##up",   ImGuiTableColumnFlags.WidthFixed, 38);   // ^
                    ImGui.TableSetupColumn("##dn",   ImGuiTableColumnFlags.WidthFixed, 38);   // v
                    ImGui.TableSetupColumn("##rm",   ImGuiTableColumnFlags.WidthFixed, 32);   // x

                    for (int i = 0; i < members.Count; i++)
                    {
                        ImGui.TableNextRow(ImGuiTableRowFlags.None, 24);

                        // Col 0 — role badge
                        ImGui.TableSetColumnIndex(0);
                        if (i == 0)
                        {
                            ImGui.PushStyleColor(ImGuiCol.Text, new System.Numerics.Vector4(1f, 0.85f, 0.1f, 1f));
                            ImGui.TextUnformatted("[L]");
                            ImGui.PopStyleColor();
                        }
                        else
                        {
                            ImGui.PushStyleColor(ImGuiCol.Text, new System.Numerics.Vector4(0.55f, 0.55f, 0.55f, 1f));
                            ImGui.TextUnformatted($" {i + 1}.");
                            ImGui.PopStyleColor();
                        }

                        // Col 1 — name
                        ImGui.TableSetColumnIndex(1);
                        System.Numerics.Vector4 nameColor;
                        if (isFarm)
                            nameColor = i == 0
                                ? new System.Numerics.Vector4(0.3f, 1f, 0.3f, 1f)
                                : new System.Numerics.Vector4(0.6f, 0.9f, 0.6f, 1f);
                        else
                            nameColor = new System.Numerics.Vector4(0.8f, 0.8f, 0.8f, 1f);
                        ImGui.PushStyleColor(ImGuiCol.Text, nameColor);
                        ImGui.TextUnformatted(members[i]);
                        ImGui.PopStyleColor();

                        // Col 2 — Up button
                        ImGui.TableSetColumnIndex(2);
                        if (i > 0)
                        {
                            if (ImGui.Button($"^##{div}_{i}", btnSize))
                            { swapA = i; swapB = i - 1; }
                        }

                        // Col 3 — Dn button
                        ImGui.TableSetColumnIndex(3);
                        if (i < members.Count - 1)
                        {
                            if (ImGui.Button($"v##{div}_{i}", btnSize))
                            { swapA = i; swapB = i + 1; }
                        }

                        // Col 4 — Remove button (red tint)
                        ImGui.TableSetColumnIndex(4);
                        ImGui.PushStyleColor(ImGuiCol.Button,        new System.Numerics.Vector4(0.55f, 0.1f, 0.1f, 1f));
                        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new System.Numerics.Vector4(0.85f, 0.2f, 0.2f, 1f));
                        ImGui.PushStyleColor(ImGuiCol.Text,          new System.Numerics.Vector4(1f, 1f, 1f, 1f));
                        if (ImGui.Button($"x##{div}_{i}", btnSizeX))
                            pendingRemove = i;
                        ImGui.PopStyleColor(3);
                    }

                    ImGui.EndTable();
                }

                if (swapA >= 0 && swapB >= 0)
                {
                    var tmp = members[swapA];
                    members[swapA] = members[swapB];
                    members[swapB] = tmp;
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
            ImGui.BeginChild("##faq_results", System.Numerics.Vector2.Zero, ImGuiChildFlags.Border);
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

            var panel = GameController?.Game?.IngameState?.IngameUi?.SyndicatePanel;
            bool panelVisible = panel?.IsVisible ?? false;
            ImGui.Separator();
            ImGui.Text($"Syndicate panel: {(panelVisible ? "VISIBLE" : "not visible")}");
            ImGui.Text($"Cached members: {ListBetrayalMasters.Count} / 14");
            int relationshipRefs = ListBetrayalMasters.Sum(m => m.RelationshipStatuses.Count);
            int rivalRefs = ListBetrayalMasters.Sum(m => m.RelationshipStatuses.Values.Count(x => x == "Rival"));
            int trustedRefs = ListBetrayalMasters.Sum(m => m.RelationshipStatuses.Values.Count(x => x == "Trusted"));
            ImGui.Text($"Relationship references: {relationshipRefs} ({relationshipRefs / 2} unique: {rivalRefs / 2} Rival, {trustedRefs / 2} Trusted)");
            ImGui.Text($"Missing board members: {(_missingMastersFromBoard == null ? "unknown" : string.Join(", ", _missingMastersFromBoard))}");

            if (ImGui.CollapsingHeader("Relationship topology", ImGuiTreeNodeFlags.DefaultOpen))
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
            ImGui.TextUnformatted("Members");
            float membersTableHeight = Math.Max(120f, ImGui.GetContentRegionAvail().Y - ImGui.GetTextLineHeightWithSpacing());
            if (ImGui.BeginTable("##debug_members", 8, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY, new System.Numerics.Vector2(0, membersTableHeight)))
            {
                ImGui.TableSetupColumn("Name");
                ImGui.TableSetupColumn("Division");
                ImGui.TableSetupColumn("Rank");
                ImGui.TableSetupColumn("Leader");
                ImGui.TableSetupColumn("Score");
                ImGui.TableSetupColumn("Links");
                ImGui.TableSetupColumn("Types");
                ImGui.TableSetupColumn("Configured role");
                ImGui.TableHeadersRow();
                foreach (var member in ListBetrayalMasters.OrderBy(m => m.Job).ThenByDescending(m => m.IsLeader).ThenBy(m => m.Name))
                {
                    string role = Settings.DivisionRoles.TryGetValue(member.Job, out var configuredRole) ? configuredRole : "Unassigned";
                    ImGui.TableNextRow();
                    ImGui.TableSetColumnIndex(0); ImGui.TextUnformatted(member.Name);
                    ImGui.TableSetColumnIndex(1); ImGui.TextUnformatted(member.Job);
                    ImGui.TableSetColumnIndex(2); ImGui.TextUnformatted(member.Rank.ToString());
                    ImGui.TableSetColumnIndex(3); ImGui.TextUnformatted(member.IsLeader ? "yes" : "");
                    ImGui.TableSetColumnIndex(4); ImGui.TextUnformatted(member.Score.ToString());
                    ImGui.TableSetColumnIndex(5); ImGui.TextUnformatted((member.Relationships?.Count ?? 0).ToString());
                    int memberRivals = member.RelationshipStatuses.Values.Count(x => x == "Rival");
                    int memberTrusted = member.RelationshipStatuses.Values.Count(x => x == "Trusted");
                    ImGui.TableSetColumnIndex(6); ImGui.TextUnformatted($"R{memberRivals} / T{memberTrusted}");
                    ImGui.TableSetColumnIndex(7); ImGui.TextUnformatted(role);
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
        /// Determines if a division's safehouse should be run when intel is full.
        /// In CatarinaFocus mode: always run (every safehouse = chance at Syndicate Medallion).
        /// In SafehouseRewards mode: Farm divisions are priority, but Filler divisions are also
        /// allowed to run when intel is full (just lower priority — don't block board for them).
        /// </summary>
        private bool ShouldRunDivision(string division)
        {
            if (Settings.FarmingMode == FarmingMode.CatarinaFocus)
                return true;

            return Settings.DivisionShouldRun.TryGetValue(division, out var shouldRun)
                ? shouldRun
                : IsFarmDivision(division);
        }

        /// <summary>
        /// Returns true if this division is a primary farm target (actively push intel).
        /// Used to differentiate priority in UI and scoring.
        /// </summary>
        private bool IsFarmDivision(string division)
        {
            return Settings.DivisionRoles.TryGetValue(division, out var role) && role == "Farm";
        }

        private void checkIntel()
        {
            var ingameState = GameController.Game.IngameState;
            var syndicatePanel = ingameState.IngameUi.SyndicatePanel;
            if (syndicatePanel == null || !syndicatePanel.IsVisible) return;
            if (syndicatePanel.SyndicateLeadersData == null) return;
            var leadersData = syndicatePanel.SyndicateLeadersData.Leaders;
            var warnings = new List<string>();

            if (leadersData is not null && leadersData.Count() ==4)
            {
                foreach (var leaderdata in leadersData)
                {
                    if (leaderdata is not null && leaderdata.Job is not null)
                    {
                        var intel = leaderdata.Intel;
                        var job = leaderdata.Job.Name;

                        if (intel >= 100)
                        {

                            warnings.Add(job);
                        }
                    }


                }
            }

            if (GameController.Area.CurrentArea.Area.IsHideout && warnings.Count > 0 && !hasClosedWindow)
            {
                // In CatarinaFocus mode, treat ALL divisions as worth running (for medallion drops).
                // In SafehouseRewards mode, only Farm divisions matter for rewards.
                var farmJobs = Settings.DivisionRoles
                    .Where(kv => kv.Value == "Farm")
                    .ToDictionary(
                        kv => kv.Key,
                        kv => Settings.DivisionMembers.TryGetValue(kv.Key, out var m) ? m : new List<string>()
                    );

                // Read current ranks of farm members from syndicate board
                var farmMemberInfo = new Dictionary<string, (string job, int rank)>();
                var syndicateStates = syndicatePanel.SyndicateStates;
                if (syndicateStates != null)
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
                    overlayDrawList.AddText(ImGui.GetFont(), 44f, priorityShadowPos, shadowColor, $"#{i + 1}");
                    overlayDrawList.AddText(ImGui.GetFont(), 44f, priorityPos, priorityColor, $"#{i + 1}");

                    var scorePos = new Vector2N(nameRect.X + 62, Math.Max(0, nameRect.Y - 26));
                    overlayDrawList.AddText(ImGui.GetFont(), 16f, scorePos, priorityColor, $"Score {master.Score}");
                }
            }
            }


        }
        private void betrayalWindow()
        {
            var ingameState = GameController.Game.IngameState;

            if (ingameState.IngameUi.SyndicatePanel.IsVisible)
            {
                DrawSyndicateBoard(ingameState.IngameUi.SyndicatePanel);
            }
            else
            {
                // A later encounter may have identical text and member names but
                // a different prisoner count, so invalidate between panel opens.
                _interrogationQueueEventKey = "";
            }
        }
        private void DrawBottomBoardGui()
        {




            var transportationBetrayalMasters = ListBetrayalMasters.Where(b => b.Job == "Transportation")
                .OrderByDescending(b => b.IsLeader).ThenBy(b => b.Rank).ToList();
            var fortificationBetrayalMasters = ListBetrayalMasters.Where(b => b.Job == "Fortification")
                .OrderByDescending(b => b.IsLeader).ThenBy(b => b.Rank).ToList();
            var researchBetrayalMasters = ListBetrayalMasters.Where(b => b.Job == "Research")
                .OrderByDescending(b => b.IsLeader).ThenBy(b => b.Rank).ToList();
            var interventionBetrayalMasters = ListBetrayalMasters.Where(b => b.Job == "Intervention")
                .OrderByDescending(b => b.IsLeader).ThenBy(b => b.Rank).ToList();
            var unnalocatedBetrayalMasters = ListBetrayalMasters.Where(b => b.Job == "None")
                .OrderByDescending(b => b.IsLeader).ThenBy(b => b.Rank).ToList();



            List<string> missingNames = allMasters.Except(ListBetrayalMasters.Select(b => b.Name)).ToList();

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




            // Start main table with one column per list
            ImGui.BeginTable("MasterTables", allLists.Length, ImGuiTableFlags.Borders);

            for (int t = 0; t < allLists.Length; t++)
            {
                ImGui.TableNextColumn();

                // Header for this list — green for Farm, gray for Filler
                bool divIsFarm = Settings.DivisionRoles.TryGetValue(headers[t], out var divRole) && divRole == "Farm";
                string divLabel = divIsFarm ? $"{headers[t]} [F]" : $"{headers[t]} [B]";
                ImGui.PushStyleColor(ImGuiCol.Text, divIsFarm
                    ? new System.Numerics.Vector4(0.4f, 1f, 0.4f, 1f)
                    : new System.Numerics.Vector4(0.7f, 0.7f, 0.7f, 1f));
                ImGui.Text(divLabel);
                ImGui.PopStyleColor();
                ImGui.Separator();

                // Child table for this list
                if (ImGui.BeginTable($"Table{t}", 2, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
                {
                    ImGui.TableSetupColumn("Rank");
                    ImGui.TableSetupColumn("Name");
                    ImGui.TableHeadersRow();

                    foreach (var master in allLists[t])
                    {
                        ImGui.TableNextRow();
                        ImGui.TableNextColumn();
                        ImGui.Text(master.Rank.ToString());

                        ImGui.TableNextColumn();

                        System.Numerics.Vector4 color;
                        if (master.Score >= 30)
                            color = new System.Numerics.Vector4(0f, 1f, 0f, 1f);       // Bright green: Farm member, correct position
                        else if (master.Score >= 1)
                            color = new System.Numerics.Vector4(0.7f, 0.7f, 0.7f, 1f); // Gray: Filler member, OK
                        else
                            color = new System.Numerics.Vector4(1f, 0.2f, 0.2f, 1f);   // Red: wrong member for this division

                        ImGui.TextColored(color, master.Name);
                    }

                    ImGui.EndTable();
                }



            }
            // for unnalocated
            ImGui.TableNextColumn();

            // Header for this list
            ImGui.Text("Outside");
            ImGui.Separator();

            // Child table for this list
            if (ImGui.BeginTable($"Table", 2, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
            {
                ImGui.TableSetupColumn("Name");
                ImGui.TableSetupColumn("Action");
                ImGui.TableHeadersRow();

                foreach (var missingmaster in missingNames)
                {
                    ImGui.TableNextRow();

                    ImGui.TableNextColumn();
                    // find if that master should be on the board

                    var auxText = "";
                    var color = new System.Numerics.Vector4(0f, 1f, 0f, 1f); // Green default
                    if (_mastersToBePresent.Contains(missingmaster))
                    {
                        color = new System.Numerics.Vector4(1f, 0f, 0f, 1f); // Red for master that should be on main board
                        auxText = "Bring in";
                    }


                    ImGui.TextColored(color, missingmaster);
                    ImGui.TableNextColumn();
                    ImGui.TextColored(color, auxText);

                }

                ImGui.EndTable();
            }

            // for interesting members
            ImGui.TableNextColumn();

            // Header for this list
            ImGui.Text("Interesting");
            ImGui.Separator();

            // Child table for this list
            if (ImGui.BeginTable($"Table", 2, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
            {
                ImGui.TableSetupColumn("Name");
                ImGui.TableSetupColumn("Job");
                ImGui.TableHeadersRow();

                foreach (var interestingmaster in interestingMasters)
                {
                    ImGui.TableNextRow();

                    ImGui.TableNextColumn();
                    // find if that master should be on the board



                    var interestingMasterAux = ListBetrayalMasters.FirstOrDefault(master => master.Name == interestingmaster);
                    var interestingMasterScore = interestingMasterAux?.Score ?? 0;
                    var interestingMasterJob = interestingMasterAux?.Job ?? "Outside!";
                    //var interestingMasterScore =  0;
                    //  var interestingMasterJob = "Job";

                    var color = new System.Numerics.Vector4(0f, 1f, 0f, 1f); // Green default
                    if (interestingMasterScore < 30)
                    {
                        color = new System.Numerics.Vector4(1f, 0f, 0f, 1f); // Red for master that should be on main board

                    }


                    ImGui.TextColored(color, interestingmaster);
                    ImGui.TableNextColumn();
                    ImGui.TextColored(color, interestingMasterJob);

                }

                ImGui.EndTable();
            }

            ImGui.EndTable();
            ImGui.Button("Close");

        }

        private List<BetrayalMaster> BetrayalEventMembers()
        {
            List<BetrayalMaster> masters = new List<BetrayalMaster>();

            var entities = GameController.EntityListWrapper.ValidEntitiesByType[EntityType.BetrayalChoice];

            foreach (var entity in entities)
            {
                var auxmaster = entity.GetComponent<StateMachine>().States.FirstOrDefault(state => state.Name=="NPCIndex");
                var npcindex = (int)auxmaster.Value;
                var aux = GameController.Files.BetrayalTargets.EntriesList[npcindex];
                var aux2 = ListBetrayalMasters.FirstOrDefault(master => master.Name == aux.Name);
                if (aux2 != null) 
                    masters.Add(aux2);
            }


            return masters;
        }
        private void updateSyndicateInfo()
        {
            var ingameState = GameController.Game.IngameState;
            var syndicatePanel = ingameState.IngameUi.SyndicatePanel;
            var relationshipMap = ingameState.ServerData.BetrayalMemberRelationships;

            ListBetrayalMasters.Clear();

            //foreach (var singleState in syndicateStates)

            // check if betrayal event is on

            var syndicateStates = syndicatePanel.SyndicateStates;

            for (int i = 0; i < syndicateStates.Count; i++)
            {
                var singleState = syndicateStates[i];
                var masterTest = singleState.UIElement.GetChildFromIndices(3, 0);
                var rank = singleState.Rank.RankInt;



                string[] parts = singleState.ToString().Split(',');
                var name = parts[0].Trim();
                var jobDescription = parts[1].Trim();
                var job = parts[2].Trim();
                

                var typedRelationships = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                var relationshipEntry = relationshipMap.FirstOrDefault(entry =>
                    entry.Key != null && entry.Key.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                if (relationshipEntry.Key != null && relationshipEntry.Value != null)
                {
                    foreach (var relation in relationshipEntry.Value)
                    {
                        if (relation.Key?.Name is { Length: > 0 } relatedName)
                            typedRelationships[relatedName] = relation.Value.ToString();
                    }
                }
                List<string> stateNames = typedRelationships.Keys.ToList();

                bool isLeader = i < 4;
                int score = GetScore(name, job, rank);
                BetrayalMaster betrayalMaster = new BetrayalMaster(name, job, rank, isLeader, stateNames, score);
                betrayalMaster.RelationshipStatuses = typedRelationships;

                if (Settings.MegaDebugMode)
                    LogMessage(betrayalMaster.ToString(), 5, Color.Red);
                ListBetrayalMasters.Add(betrayalMaster);


            }


            List<string> missingMastersFromBoard = allMasters.Except(ListBetrayalMasters.Select(b => b.Name)).ToList();

            _missingMastersFromBoard = allMasters
              .Except(ListBetrayalMasters.Select(b => b.Name))
              .ToList();

            _mastersToBePresent = Settings.DivisionMembers
                .Where(kv => Settings.DivisionRoles.TryGetValue(kv.Key, out var r) && r == "Farm")
                .SelectMany(kv => kv.Value)
                .Distinct()
                .ToList();

        }

        private void DrawSyndicateBoard(SyndicatePanel syndicatePanel)
        {

            updateSyndicateInfo();
            //DrawBottomBoard();
            DrawBottomBoardGui();


            if (syndicatePanel.BetrayalEventData is not null)
            {

                if (syndicatePanel.BetrayalEventData.IsVisible)
                {
                    // we got an event

                    // can be betray

                    // can be swap leader

                    // swap





                    _eventAction = syndicatePanel.BetrayalEventData.Action.Id;
                    _eventText = syndicatePanel.BetrayalEventData.EventText;
                    try { _jobEvent = syndicatePanel.BetrayalEventData.CurrentEncounterJob.Name; }
                    catch { _jobEvent = "Unknown"; }
                    _SpecialButton = syndicatePanel.BetrayalEventData.SpecialButton.GetClientRectCache;
                    _ReleaseButton = syndicatePanel.BetrayalEventData.ReleaseButton.GetClientRectCache;
                    _InterrogateButton = syndicatePanel.BetrayalEventData.InterrogateButton.GetClientRectCache;

                    // children



                    // 4

                    // get the name of the current

                    var masterPanel = syndicatePanel.BetrayalEventData.GetChildAtIndex(4);
                    _masterText = masterPanel.GetChildAtIndex(4).Text;

                    if (Settings.DebugMode)
                    {
                        LogMessage("event  " + _eventText, 5, Color.Yellow);
                        LogMessage("masterText: " + _masterText, 5, Color.Yellow);
                    }


                    // given the name, get him his current rank and job
                    _eventMaster = ListBetrayalMasters.FirstOrDefault(master => master.Name == _masterText);
                    if (_eventMaster == null)
                    {
                        if (Settings.DebugMode)
                            LogMessage($"DrawSyndicateBoard: master '{_masterText}' not found in ListBetrayalMasters — skipping event", 5, Color.Red);
                        return;
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

                    _mastersToBePresent = Settings.DivisionMembers
                        .Where(kv => Settings.DivisionRoles.TryGetValue(kv.Key, out var r) && r == "Farm")
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

                    // handle interrogation outcomes


                    _eventOutcomes.Add(new Outcome(OutcomeType.Nothing, ActionType.Release, 0, $"Release"));

                    if (!masterHasInterestOnJob && isJobOfInterest && noSpaceForThisMaster && _jobEvent == _eventMaster.Job && _eventMaster.Job != "None")

                    {
                        _eventOutcomes.Add(new Outcome(OutcomeType.RemoveBadMemberFromInterestingJobAndNeedSpace, ActionType.Interrogate, 50, $"{_eventMaster.Name} is going to get demoted "));
                                              

                        //Graphics.DrawFrame(_InterrogateButton, Color.Green, 6);
                        if (Settings.DebugMode)
                        {
                            LogMessage("This master has no interest on this job and space is needed for others: " + string.Join(", ", mastersWithThisJobAsInterest), 5, Color.Yellow);
                        }
                    }

                    if (_eventMaster.Job != "None")
                        AddInterrogationStrategyOutcome(_eventOutcomes, _eventMaster);
                    else if (_eventMaster.Job != "None")
                    {
                        if (_eventMaster.Score >= 30)
                        {
                            // Farm member in correct position — neutral base (StealIntelligence adds +80/+60 bonus)
                            // In Catarina mode: interrogating a high-rank Farm member is BAD (loses rank on someone important!)
                            int farmInterrogateScore = 0;
                            if (Settings.FarmingMode == FarmingMode.CatarinaFocus && _eventMaster.Rank >= 2)
                                farmInterrogateScore = -40; // Don't derank your good members!
                            _eventOutcomes.Add(new Outcome(OutcomeType.GainInformationToInterestingJob, ActionType.Interrogate, farmInterrogateScore,
                                $"{_eventMaster.Name} farm member in correct position (rank {_eventMaster.Rank})"));
                        }
                        else if (_eventMaster.Score >= 1)
                        {
                            // Filler member in Filler division — neutral (gives intel, no harm)
                            _eventOutcomes.Add(new Outcome(OutcomeType.GainInformationToInterestingJob, ActionType.Interrogate, 0,
                                $"{_eventMaster.Name} filler member, neutral to interrogate"));
                        }
                        else
                        {
                            // Wrong placement (score=-1) — interrogate to clear the position
                            _eventOutcomes.Add(new Outcome(OutcomeType.RemoveBadMemberFromBadJob, ActionType.Interrogate, 50,
                                $"{_eventMaster.Name} wrong position — Interrogate to remove"));
                        }

                        // In Catarina mode: interrogation gives intel proportional to member rank
                        // Rank 3 = ~6 intel/turn × 3 turns = 18 total (GREAT!)
                        // Rank 1 = ~1-2 intel/turn × 3 turns = 3-6 total (WASTE - better to Execute first!)
                        // So: only recommend Interrogate on high rank members
                        if (Settings.FarmingMode == FarmingMode.CatarinaFocus && _eventMaster.Score < 30)
                        {
                            int interrogateRankBonus;
                            if (_eventMaster.Rank >= 3)
                                interrogateRankBonus = 50; // High rank = lots of intel, worth it!
                            else if (_eventMaster.Rank == 2)
                                interrogateRankBonus = 10; // Mediocre — Execute would be better to build rank first
                            else
                                interrogateRankBonus = -20; // Low rank = waste — Execute to build rank, interrogate later!
                            _eventOutcomes.Add(new Outcome(OutcomeType.GainInformationToInterestingJob, ActionType.Interrogate, interrogateRankBonus,
                                $"Catarina mode: interrogate rank {_eventMaster.Rank} → {(interrogateRankBonus > 0 ? "GOOD intel" : "low intel, build rank first!")}"));
                        }
                    }
                    else
                    {
                        // Member has no job (rank 0) — interrogation gives minimal intel
                        if (Settings.FarmingMode == FarmingMode.CatarinaFocus)
                        {
                            // Rank 0 interrogation = barely any intel — not worth it, Execute to assign + rank up
                            _eventOutcomes.Add(new Outcome(OutcomeType.GainInformationToInterestingJob, ActionType.Interrogate, -10,
                                $"{_eventMaster.Name} no job, rank 0 — interrogate gives almost no intel"));
                        }
                        else
                        {
                            _eventOutcomes.Add(new Outcome(OutcomeType.RemoveBadMemberFromBadJob, ActionType.Interrogate, -50,
                                $"{_eventMaster.Name} he has no job so there is no point interrogating him"));
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

                                }
                                else

                                 if (_eventMaster.Score >= 30)
                                {
                                    int resultingRank = GetRankAfterExecute(_eventMaster.Rank);
                                    int gainedRanks = Math.Max(0, resultingRank - _eventMaster.Rank);
                                    int promoteScore;
                                    if (executeNoEffect)
                                        promoteScore = 0;
                                    else
                                        promoteScore = 25 * gainedRanks;
                                    _eventOutcomes.Add(new Outcome(OutcomeType.PromoteGoodMemberInInterestingJob, ActionType.Special, promoteScore,
                                        $"{_eventMaster.Name} R{_eventMaster.Rank} -> R{resultingRank}{(executeNoEffect ? " (NO EFFECT)" : "")}"));
                                    AddExecuteIntelligenceOutcome(_eventOutcomes, _eventMaster.Job, resultingRank);


                                    //Graphics.DrawFrame(_SpecialButton, Color.Green, 6);
                                }

                                else if (_eventMaster.Score == 1)
                                {
                                    //Graphics.DrawFrame(_ReleaseButton, Color.Yellow, 6);
                                    if (executeNoEffect)
                                    {
                                        _eventOutcomes.Add(new Outcome(OutcomeType.PromoteBadMemberInBadJob, ActionType.Special, 0, $"{_eventMaster.Name} execute has no effect (max rank)"));
                                    }
                                    else if (isJobOfInterest)
                                    {
                                        _eventOutcomes.Add(new Outcome(OutcomeType.PromoteBadMemberInInterestingJob, ActionType.Special, -100, $"{_eventMaster.Name} who is bad and is on interesting job gets promoted "));

                                    }
                                    else
                                    {
                                        // filler on filler division — promoting builds rank for future interrogation intel
                                        // In Catarina mode: higher rank = more intel from future interrogation (rank 3 = 6/turn!)
                                        // Plus Lethal Extraction gives immediate intel = 2×rank
                                        int resultingRank = GetRankAfterExecute(_eventMaster.Rank);
                                        int fillerPromoteScore = Settings.FarmingMode == FarmingMode.CatarinaFocus ? 35 : 5;
                                        _eventOutcomes.Add(new Outcome(OutcomeType.PromoteBadMemberInBadJob, ActionType.Special, fillerPromoteScore,
                                            $"{_eventMaster.Name} filler promote — builds rank for future intel (Catarina: Lethal Extraction + future interrogation)"));
                                        AddExecuteIntelligenceOutcome(_eventOutcomes, _eventMaster.Job, resultingRank);
                                    }
                                    

                                }
                                else


                                {
                                    _eventOutcomes.Add(new Outcome(OutcomeType.PromoteGoodMemberInBadJob, ActionType.Special, -100, $"{_eventMaster.Name} who is good and is on bad job gets promoted "));

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
                                            _eventOutcomes.Add(new Outcome(OutcomeType.GainInformationToBadJob, ActionType.Special, 5,
                                                $"+{intelMatch.Groups[1].Value} {intelJob} Intelligence (Filler)"));
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

                Match match = Regex.Match(_eventText, @"(.+?)\s+removed from the Immortal Syndicate");

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

                Match match = Regex.Match(_eventText, @"(.+?)\s+removed from the Immortal Syndicate");

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
                    // In Catarina mode: intel to ANY division is highly valuable (more safehouses = more medallions)
                    int intelScore = Settings.FarmingMode == FarmingMode.CatarinaFocus ? 60 : 40;
                    eventOutcome.Add(new Outcome(OutcomeType.GainInformationToInterestingJob, ActionType.Special, intelScore, $" Gain intelligence to {jobToCheck} "));
                }
                else
                {
                    //Graphics.DrawFrame(_ReleaseButton, Color.Green, 6);
                    // Filler intel — low priority: filling filler safehouse = fewer filler encounters in maps
                    eventOutcome.Add(new Outcome(OutcomeType.GainInformationToBadJob, ActionType.Special, 5, $" Gain intelligence to {jobToCheck} (Filler — low priority)"));

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
                if (kv.Value.Contains(memberName))
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

            string pattern = @"\+\d+ (\w+) Intelligence\. ([A-Za-z]+(?: [A-Za-z]+)*) and ([A-Za-z]+(?: [A-Za-z]+)*) become Rivals";

            List<Outcome> eventOutcome = new List<Outcome>();
            Match match = Regex.Match(_eventText, pattern);
            if (Settings.DebugMode)
                LogMessage(" Relationship event ", 5, Color.Red);
            if (match.Success)
            {
                string encounterType = match.Groups[1].Value;
                string member1name = match.Groups[2].Value;
                string member2name = match.Groups[3].Value; ;
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





                // Division type check using effective jobs (handles Job="None" members)
                var stealFarmSet = Settings.DivisionRoles.Where(kv => kv.Value == "Farm").Select(kv => kv.Key).ToHashSet();
                var stealBlockSet = Settings.DivisionRoles.Where(kv => kv.Value == "Filler").Select(kv => kv.Key).ToHashSet();

                var stealJob1 = member1 != null ? GetMemberEffectiveJob(member1.Name, member1.Job) : "None";
                var stealJob2 = member2 != null ? GetMemberEffectiveJob(member2.Name, member2.Job) : "None";
                var stealScore1 = member1 != null ? GetMemberEffectiveScore(member1) : 0;
                var stealScore2 = member2 != null ? GetMemberEffectiveScore(member2) : 0;

                bool stealSameType = (stealFarmSet.Contains(stealJob1) && stealFarmSet.Contains(stealJob2))
                                  || (stealBlockSet.Contains(stealJob1) && stealBlockSet.Contains(stealJob2));
                bool stealCrossType = (stealFarmSet.Contains(stealJob1) && stealBlockSet.Contains(stealJob2))
                                   || (stealBlockSet.Contains(stealJob1) && stealFarmSet.Contains(stealJob2));

                if (Settings.DebugMode && member1 != null && member2 != null)
                    LogMessage($"StealRival: {member1.Name}({stealJob1}) + {member2.Name}({stealJob2}) sameType={stealSameType} crossType={stealCrossType}", 5, Color.Cyan);

                // Division type is the primary factor; intel direction is secondary bonus/penalty.
                bool stealGoodIntel = _jobsToGainIntelligence.Contains(encounterType);

                if (member1 != null && member2 != null)
                    AddRelationshipOutcome(eventOutcome, "Rivals", member1, member2);

                if (stealCrossType)
                {
                    // Farm+Filler Rivals: always desirable — lots of cross-division encounters
                    if (stealGoodIntel)
                    {
                        eventOutcome.Add(new Outcome(OutcomeType.GainInformationToInterestingJob, ActionType.Special, 100,
                            $"Gain intelligence to {encounterType}"));
                    }
                    else
                    {
                        eventOutcome.Add(new Outcome(OutcomeType.GainInformationToBadJob, ActionType.Special, -30,
                            $"Gain intelligence to {encounterType} (not a Farm division)"));
                    }
                }
                else if (stealSameType)
                {
                    // Same-type rivalry is still useful; only score the intelligence side effect here.
                    if (stealGoodIntel)
                    {
                        eventOutcome.Add(new Outcome(OutcomeType.GainInformationToInterestingJob, ActionType.Special, 100,
                            $"Gain intelligence to {encounterType}"));
                    }
                    else
                    {
                        eventOutcome.Add(new Outcome(OutcomeType.GainInformationToBadJob, ActionType.Special, -50,
                            $"Gain intelligence to {encounterType} (not a Farm division)"));
                    }
                }
                else
                {
                    // Unknown divisions — score-based fallback
                    if ((stealScore1 + stealScore2) > 20)
                    {
                        if (stealGoodIntel)
                            eventOutcome.Add(new Outcome(OutcomeType.GainInformationToInterestingJob, ActionType.Special, 100,
                                $"Gain intelligence to {encounterType}"));
                        else
                            eventOutcome.Add(new Outcome(OutcomeType.GainInformationToBadJob, ActionType.Special, -50,
                                $"Gain intelligence to {encounterType}"));
                    }
                }


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
            var _eventText = syndicatePanel.BetrayalEventData.EventText;
            string pattern = @"([A-Za-z]+(?: [A-Za-z]+)*)\s+moves\s+to\s+(\w+)\.\s+([A-Za-z]+(?: [A-Za-z]+)*)\s+moves\s+to\s+(\w+)";
            Match match = Regex.Match(_eventText, pattern);
            List<Outcome> eventOutcome = new List<Outcome>();
            if (match.Success) // swap
            {
                string firstPerson = match.Groups[1].Value;
                string firstDestination = match.Groups[2].Value;
                string secondPerson = match.Groups[3].Value;
                string secondDestination = match.Groups[4].Value;
                if (Settings.DebugMode)
                {
                    LogMessage($"First Person: {firstPerson}", 5, Color.Blue);
                    LogMessage($"First Destination: {firstDestination}", 5, Color.Blue);
                    LogMessage($"Second Person: {secondDestination}", 5, Color.Blue);
                    LogMessage($"Second Destination: {secondPerson}", 5, Color.Blue);
                }
                List<string> _eventMasters = new List<string>();
                _eventMasters.Add(firstPerson);
                _eventMasters.Add(secondPerson);

                //foreach (var _eventMaster in _eventMasters)
                //{
                //    LogMessage($"event masters: {_eventMaster}", 5, Color.Purple);
                //}

                var filteredMasters = ListBetrayalMasters.Where(master => _eventMasters.Contains(master.Name)).ToList();

                //foreach(var filteredMaster in filteredMasters)
                //{
                //    LogMessage($"masters: {filteredMaster}", 5, Color.Blue);
                //}


                List<BetrayalMaster> NewMasters = new List<BetrayalMaster>();
                NewMasters.Add(
                new BetrayalMaster(filteredMasters[0].Name, filteredMasters[1].Job, filteredMasters[0].Rank, filteredMasters[0].IsLeader, filteredMasters[0].Relationships, GetScore(filteredMasters[0].Name, filteredMasters[1].Job, filteredMasters[0].IsLeader ? filteredMasters[0].Rank + 1 : filteredMasters[0].Rank))
                );
                NewMasters.Add(
                new BetrayalMaster(filteredMasters[1].Name, filteredMasters[0].Job, filteredMasters[1].Rank, filteredMasters[1].IsLeader, filteredMasters[1].Relationships, GetScore(filteredMasters[1].Name, filteredMasters[0].Job, filteredMasters[1].IsLeader ? filteredMasters[1].Rank + 1 : filteredMasters[1].Rank))
                );








                int oldScore = filteredMasters.Sum(master => master.Score);
                int newScore = NewMasters.Sum(master => master.Score);
                var _SpecialButton = syndicatePanel.BetrayalEventData.SpecialButton.GetClientRectCache;
                var _ReleaseButton = syndicatePanel.BetrayalEventData.ReleaseButton.GetClientRectCache;
  

                    //Graphics.DrawFrame(_SpecialButton, Color.Green, 6);
                    // good swap
                    

                    // the ones that leave
                    foreach (var memberFiltered in filteredMasters)
                    {
                        if (Settings.DebugMode)
                            LogMessage(memberFiltered.ToString(), 5, Color.Red);

                        if(memberFiltered.Score >= 30)
                        {
                            eventOutcome.Add(new Outcome(OutcomeType.RemoveGoodMemberFromInterestingJob, ActionType.Special, -100, $"{memberFiltered.Name} who is interesting and on the correct job gets MOVED OUT "));
                        }
                        else  if (memberFiltered.Score >= 1)
                        {
                            eventOutcome.Add(new Outcome(OutcomeType.RemoveBadMemberFromBadJob, ActionType.Special, 0, $"{memberFiltered.Name} who is interesting and on the correct job gets MOVED OUT "));
                        }
                        else 
                        {
                            eventOutcome.Add(new Outcome(OutcomeType.RemoveBadMemberFromInterestingJob, ActionType.Special, 50, $"{memberFiltered.Name} who is interesting and on the correct job gets MOVED OUT "));
                        }

                    }
                    // the ones that join
                    foreach (var newMember in NewMasters)
                    {
                        if (Settings.DebugMode)
                            LogMessage(newMember.ToString(), 5, Color.Green);

                        if (newMember.Score >= 30)
                        {
                            eventOutcome.Add(new Outcome(OutcomeType.AddGoodMemberToInterestingJob, ActionType.Special, 100, $"{newMember.Name} who is interesting and on the correct job gets MOVED OUT "));
                        }
                        else if (newMember.Score >= 1)
                        {
                            eventOutcome.Add(new Outcome(OutcomeType.AddBadMemberToBadJob, ActionType.Special, 0, $"{newMember.Name} who is interesting and on the correct job gets MOVED OUT "));
                        }
                        else
                        {
                            eventOutcome.Add(new Outcome(OutcomeType.AddBadMemberToInterestingJob, ActionType.Special, -60, $"{newMember.Name} who is interesting and on the correct job gets MOVED OUT "));
                        }
                    }
                
               





            }
            return eventOutcome;
        }



        public int GetScore(string name, string job, int rank)
        {
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

        private void AddRelationshipOutcome(List<Outcome> outcomes, string relationshipType, BetrayalMaster first, BetrayalMaster second)
        {
            string firstJob = GetMemberEffectiveJob(first.Name, first.Job);
            string secondJob = GetMemberEffectiveJob(second.Name, second.Job);
            bool firstFarm = Settings.DivisionRoles.TryGetValue(firstJob, out var firstRole) && firstRole == "Farm";
            bool secondFarm = Settings.DivisionRoles.TryGetValue(secondJob, out var secondRole) && secondRole == "Farm";
            bool firstFiller = Settings.DivisionRoles.TryGetValue(firstJob, out firstRole) && firstRole == "Filler";
            bool secondFiller = Settings.DivisionRoles.TryGetValue(secondJob, out secondRole) && secondRole == "Filler";
            bool crossGroup = (firstFarm && secondFiller) || (firstFiller && secondFarm);
            bool sameGroup = (firstFarm && secondFarm) || (firstFiller && secondFiller);

            if (relationshipType.Equals("Rivals", StringComparison.OrdinalIgnoreCase))
            {
                int score = crossGroup ? 140 : sameGroup ? -80 : 20;
                outcomes.Add(new Outcome(
                    crossGroup ? OutcomeType.RivalBetweenGoodInterestingMember : OutcomeType.RivalBetweenBadMembers,
                    ActionType.Special,
                    score,
                    $"{first.Name}({firstJob}) + {second.Name}({secondJob}) become Rivals — {(crossGroup ? "desired 2↔5 link" : sameGroup ? "undesired same-group link" : "unclassified link")}"));
            }
            else if (relationshipType.Equals("Trusted", StringComparison.OrdinalIgnoreCase))
            {
                int score = crossGroup ? 65 : sameGroup ? -45 : 10;
                outcomes.Add(new Outcome(
                    crossGroup ? OutcomeType.FriendlyBetweenGoodInterestingMember : OutcomeType.FriendlyBetweenBadMembers,
                    ActionType.Special,
                    score,
                    $"{first.Name}({firstJob}) + {second.Name}({secondJob}) become Trusted — {(crossGroup ? "seed for desired 2↔5 Rival" : "wrong reinforcement group")}"));
            }
            else
            {
                int score = crossGroup ? -140 : sameGroup ? 70 : 0;
                outcomes.Add(new Outcome(OutcomeType.NeutralWithGoodMember, ActionType.Special, score,
                    $"{first.Name}({firstJob}) + {second.Name}({secondJob}) become Neutral — {(crossGroup ? "desired edge lost" : sameGroup ? "bad same-group edge removed" : "unclassified")}"));
            }
        }

        private void AddInterrogationStrategyOutcome(List<Outcome> outcomes, BetrayalMaster member)
        {
            int scarabCount = Math.Clamp(Settings.UnbreakingScarabs.Value, 0, 2);
            double preserveRankChance = scarabCount * 0.5;
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
                $"Interrogate strategy: intel {intelligenceValue:+#;-#;0}, rank-loss risk {expectedRankLossPenalty:+#;-#;0}, network absence {networkAbsencePenalty:+#;-#;0} (R{rivalLinks}/T{trustedLinks}), Unbreaking {scarabCount}/2"));
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

        private string GetRelationshipStrategicLabel(string firstName, string secondName, string status)
        {
            int value = GetExistingRelationshipValue(firstName, secondName, status);
            if (status == "Rival")
                return value > 0 ? "desired cross-group Rival" : value < 0 ? "undesired same-group Rival" : "unclassified Rival";
            return value > 0 ? "useful cross-group Trusted seed" : value < 0 ? "undesired same-group Trusted" : "unclassified Trusted";
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
                return crossGroup ? 140 : sameGroup ? -80 : 20;
            if (status == "Trusted")
                return crossGroup ? 65 : sameGroup ? -45 : 10;
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

            // Removing a valuable edge is harmful; removing a bad same-group edge is useful.
            return -retainedTopologyValue;
        }

        private int GetRankAfterExecute(int currentRank)
        {
            int ranksGained = Settings.TestOfLoyalty ? 2 : 1;
            return Math.Min(3, Math.Max(0, currentRank) + ranksGained);
        }

        private void AddExecuteIntelligenceOutcome(List<Outcome> outcomes, string division, int resultingRank)
        {
            if (!Settings.LethalExtraction || string.IsNullOrEmpty(division) || division == "None")
                return;

            int intelligence = 2 * resultingRank;
            bool shouldRun = ShouldRunDivision(division);
            int score = shouldRun ? intelligence * 6 : -intelligence * 8;
            outcomes.Add(new Outcome(
                shouldRun ? OutcomeType.GainInformationToInterestingJob : OutcomeType.GainInformationToBadJob,
                ActionType.Special,
                score,
                $"Lethal Extraction: +{intelligence} {division} Intelligence"));
        }

        /// Returns 30 if this member is a Farm division member, 1 if Filler, 0 if unknown.
        private int GetMemberBestPossibleScore(string name)
        {
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

            if (topGroup.ActionType == ActionType.Special)
            {
                Graphics.DrawFrame(_SpecialButton, Color.Green, 6);
            }
            else if (topGroup.ActionType == ActionType.Interrogate)
            {
                Graphics.DrawFrame(_InterrogateButton, Color.Green, 6);
            }
            else
            {
                Graphics.DrawFrame(_ReleaseButton, Color.Green, 6);
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
            if (syndicatePanel?.SyndicateStates == null)
            {
                _lastInterrogationCounterText = "Syndicate states unavailable";
                return false;
            }

            try
            {
                // In the current Betrayal board layout, child 18 of a member's
                // UI card is visible only while that member occupies one of the
                // three interrogation cells at the bottom of the board.
                var prisoners = new List<string>();
                foreach (var state in syndicatePanel.SyndicateStates)
                {
                    if (state?.UIElement == null || state.UIElement.ChildCount <= 18)
                        continue;

                    var prisonerMarker = state.UIElement.GetChildAtIndex(18);
                    if (prisonerMarker?.IsVisible == true)
                        prisoners.Add(state.Target?.Name ?? "unknown");
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
