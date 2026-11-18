using System.Collections.Generic;
using ExileCore.Shared.Attributes;
using ExileCore.Shared.Interfaces;
using ExileCore.Shared.Nodes;
using SharpDX;

namespace BetrayalHelper
{
    /// <summary>
    /// Farming mode determines overall strategy:
    /// - SafehouseRewards: classic 5522 setup, farm only specific divisions for their rewards
    /// - CatarinaFocus: run ALL safehouses ASAP to maximize Syndicate Medallion drops (Catarina access)
    /// 
    /// Since 3.26: Catarina requires a Syndicate Medallion (5-10% drop from safehouse leaders, buffed in 3.27).
    /// Defeating Catarina does NOT reset the board anymore.
    /// Strategy for Catarina = maximize safehouses completed per hour = more medallion chances.
    /// </summary>
    public enum FarmingMode
    {
        /// <summary>
        /// Classic setup: Farm specific divisions for their rewards, block others.
        /// Uses 5522 layout (2 Farm divisions with 2 members, 2 Filler with 5 members).
        /// Only run Farm safehouses; skip Filler ones.
        /// </summary>
        SafehouseRewards,

        /// <summary>
        /// Catarina-focused: run ALL 4 safehouses as fast as possible.
        /// Each safehouse leader has a chance to drop Syndicate Medallion.
        /// More safehouses/hour = faster Catarina spawns.
        /// Division roles still matter for member placement but ALL safehouses get run.
        /// </summary>
        CatarinaFocus
    }

    public class BetrayalHelperSettings : ISettings
    {
        public ToggleNode DebugMode { get; set; }
        public ToggleNode MegaDebugMode { get; set; }
        public ToggleNode ShowBoardOverview { get; set; }
        // Append board changes, decisions and plan snapshots to ConfigDirectory/history (jsonl + stats.json).
        public ToggleNode RecordHistory { get; set; } = new ToggleNode(true);
        // Show #1/#2/#3 click-order labels above encounter members (frames always stay).
        public ToggleNode ShowPriorityOrder { get; set; } = new ToggleNode(true);
        public ButtonNode ReloadRules { get; set; } = new ButtonNode();
        public ToggleNode Enable { get; set; } = new ToggleNode(false);

        // Current Atlas passive tree (poedb Atlas_passive_skill, Betrayal mastery).
        // Only nodes that change Execute / Interrogate rank outcomes affect decisions.
        // Enduring Legacy: Executed members have 50% chance to gain an additional Rank.
        public ToggleNode EnduringLegacy { get; set; } = new ToggleNode(true);
        // Unconditional Release: Interrogated targets have 30% chance to not lose Rank.
        public ToggleNode UnconditionalRelease { get; set; } = new ToggleNode(true);

        // Persisted geometry of the single safehouse planner window. These are
        // plain values so they are saved with the rest of the plugin settings
        // without adding technical controls to the settings UI.
        [IgnoreMenu]
        public int SafehouseWindowX { get; set; } = 800;
        [IgnoreMenu]
        public int SafehouseWindowY { get; set; } = 500;
        [IgnoreMenu]
        public int SafehouseWindowWidth { get; set; } = 720;
        [IgnoreMenu]
        public int SafehouseWindowHeight { get; set; } = 360;

        /// <summary>
        /// Overall farming strategy. Affects which safehouses to run and scoring logic.
        /// </summary>
        [IgnoreMenu]
        public FarmingMode FarmingMode { get; set; } = FarmingMode.SafehouseRewards;

        /// <summary>
        /// Per-division: should this safehouse be run when intel is full?
        /// In CatarinaFocus mode this is auto-set to Run for all divisions.
        /// In SafehouseRewards mode: Farm=Run, Filler=Skip.
        /// User can override manually.
        /// </summary>
        [IgnoreMenu]
        public Dictionary<string, bool> DivisionShouldRun { get; set; } = new Dictionary<string, bool>
        {
            { "Transportation", true },
            { "Fortification",  true },
            { "Research",       true },
            { "Intervention",   true }
        };

        /// <summary>
        /// Catarina Focus only: keep configured members in selected divisions.
        /// Member placement in unselected divisions is ignored.
        /// </summary>
        [IgnoreMenu]
        public Dictionary<string, bool> CatarinaPreserveDivision { get; set; } = new Dictionary<string, bool>
        {
            { "Transportation", false },
            { "Fortification",  false },
            { "Research",       false },
            { "Intervention",   false }
        };

        // "Farm" or "Filler" per division
        // Farm = divisions you run safehouses for; Filler = board blockers
        [IgnoreMenu]
        public Dictionary<string, string> DivisionRoles { get; set; } = new Dictionary<string, string>
        {
            { "Transportation", "Farm" },
            { "Fortification",  "Farm" },
            { "Research",       "Farm" },
            { "Intervention",   "Farm" }
        };

        // Desired members per division. Ordering has no strategic meaning.
        [IgnoreMenu]
        public Dictionary<string, List<string>> DivisionMembers { get; set; } = new Dictionary<string, List<string>>
        {
            { "Transportation", new List<string> { "It That Fled", "Gravicius" } },
            { "Fortification",  new List<string> { "Riker", "Rin" } },
            { "Research",       new List<string> { "Vorici", "Janus" } },
            { "Intervention",   new List<string> { "Jorgin", "Hillock" } }
        };

        public BetrayalHelperSettings()
        {
            DebugMode = new ToggleNode(false);
            MegaDebugMode = new ToggleNode(false);
            ShowBoardOverview = new ToggleNode(true);
        }
    }
}
