# Changelog

All notable changes to BetrayalHelper are documented here. The format follows Keep a Changelog and uses semantic versioning from the first public release.

## [Unreleased]

### Added
- Real safehouse intelligence: leader state `+0x84` (verified 72 -> 78 after +6 event). Per-division Intel bars in Board Overview; banner and safehouse window trigger at 100 for Run divisions.
- `ShowPriorityOrder` setting; orange frame for Farm targets still outside the board.
- `tools/intel_snap.py` (in BetrayalHelperv2/tools) to snapshot/diff board memory.

### Changed (scoring)
- Intelligence is now wanted wherever the safehouse should be run (Run policy), not only in Farm divisions. Previously +4 Research Intelligence was scored as -32 for a Filler division and Release won even with Research set to Run. Applies to Gain/Steal Intelligence and intel-bearing relationship/rank events.
- Reaching 100 intel with an event adds a +25 completion bonus.
- Gain Intelligence events on Run divisions now score 6 points per useful intel point (9 in Catarina Focus), capped by room left to 100 (was a flat 40/60 regardless of amount).
- Execute promote score uses expected rank gain (1 + extra-rank chance, capped at R3) instead of a rounded rank jump.
- Interrogate payoff for kicking an intruder out is scaled by (1 - keep-rank chance).

### Removed
- All UI-tree / screen-position fallbacks (card coordinates, `Position.Y > 1700` prisoner check, `SyndicateStates` wrapper, safehouse UI-element signal). Game memory is the only data source; if the board memory cannot be read, a red "MEMORY READ FAILED" window appears and recommendations are blocked.
- Prisoner count (3/3 queue) is currently not read (no verified offset); Interrogate stays selectable until the offset is mapped.

### Fixed
- Farm division capacity in Execute/swap "no space" checks follows the configured member count instead of a hardcoded 2; null-guard for missing label list; debug header shows real roster size.
- `ShouldRunDivision` ignored the per-division Run/Skip setting and returned the Farm role; Run is authoritative again (this is what made Release win over +4 Research Intelligence).
- Board Overview layout: divisions in one row, Outside and Farm Targets below; leader shown by row highlight; all UI text in English.
- Raw action label (e.g. NPCBEFRIENDSANOTHER) over the recommended button is now debug-only.
- "ItThatFled" in saved settings never matched "It That Fled" from the board; names are now normalized on load.
- Removed fake safehouse-ready signals (`state+0x78` is not intelligence; rank-based readiness).
- Board Overview: division headers show FARM/FILLER, member count vs target and leader; per-member "Cel" column (OK / ->target division); list of members missing from the board.

### Changed
- Atlas model updated to the current tree (poedb Atlas_passive_skill, Betrayal mastery). Only rank nodes affect decisions: Enduring Legacy (+50% extra rank on Execute), Unconditional Release (30% keep rank on Interrogate), and their small nodes (+10% / +5%, 2 copies each). Removed Unbreaking scarab, small-node and obsolete Test of Loyalty / Lethal Extraction settings. Other nodes do not change the Special/Interrogate/Release choice.

## [1.4.0] - 2026-11-16

### Added
- **Direct Engine Memory Architecture (PoE 3.30)**:
  - Replaced heuristic on-screen card coordinate parsing with direct memory extraction of the internal `std::vector<BetrayalSyndicateState>` located at `SyndicatePanel + 0x3F8` (start) and `+0x400` (end).
  - Stride size of 160 bytes (`0xA0`) mapped cleanly across all 14 active Syndicate members.
  - Per-member internal state decoding:
    - `state + 0x08`: Target pointer $\rightarrow$ UTF-16 Member Name.
    - `state + 0x18`: Job pointer $\rightarrow$ UTF-16 Division Name (`Transportation`, `Fortification`, `Research`, `Intervention`, `None`).
    - `state + 0x28`: Rank pointer $\rightarrow$ 32-bit Integer Rank (`0` = Outside, `1` = Sergeant, `2` = Lieutenant, `3` = Captain).
    - `state + 0x78`: Intelligence value (0–100%) for division leaders.
  - Automatic string normalization: maps internal engine identifier `"ItThatFled"` to standard name `"It That Fled"`.
- **Accurate Division Leader & Safehouse Intel Resolution**:
  - Identified the first 4 array elements (`index < 4`) in the memory vector as the definitive division leaders, bypassing brittle UI card frame measurements.
  - Direct reading of live safehouse intelligence points (`state + 0x78`) from memory.
  - Triggered ready safehouse status immediately when a division leader hits 100% intelligence.
- **Board Overview RAM Extraction Header**:
  - Updated Board Overview status header to display `| Pamięć RAM: 14/14`, providing immediate confirmation of 100% memory-based parsing fidelity without UI fallback guessing.
- **Ready Safehouse Raid Banner**:
  - Visual notification banner prompting the player to execute safehouse raids when target divisions reach full intelligence.

### Fixed & Changed
- **Eliminated Faulty Coordinate/Visual Heuristics**:
  - **Problem**: In PoE 3.30, the Immortal Syndicate board acts as a dynamic force-directed physics graph. Cards reposition arbitrarily based on active trust/rivalry webs (e.g. Transportation members drifting into the left/Fortification quadrant, or leaders shifting towards the center). Furthermore, ExileCore 3.30's `syndicatePanel.SyndicateStates` wrapper returned an empty list (`Count == 0`) due to an outdated internal offset. Previous spatial clustering wrongly misclassified legitimate division members as intruders.
  - **Solution**: Bypassed the broken high-level ExileCore property and read the underlying memory vector directly using `GameController.Memory.Read<long>()`. Screen-coordinate fallback is strictly retained as a failsafe only if memory parsing returns 0 members.
- **Security & AV Anti-False-Positive Hardening**:
  - All memory reads are strictly Read-Only via ExileApi's standard `GameController.Memory.Read<T>()` API (zero `WriteProcessMemory`, zero DLL injection, zero kernel hooks).
  - Completely eliminated temporary standalone test `.exe` compilations that previously triggered Windows Defender "Sample Submission" popups. All plugin code compiles strictly as an in-process `.dll` library.
- **Relationship State Snapshotting**:
  - Snapshot committed relationships before opening a decision; ExileCore's live map can already contain the previewed Rival/Trusted result while the choice is displayed.
  - Added safe mid-choice fallback treating an offered `become Rivals` result as a conversion from Trusted when no pre-choice snapshot exists.
  - Read exact Rival and Trusted topology from `ServerData.BetrayalMemberRelationships` instead of treating division membership as relationship data.

## [1.3.0] - 2026-11-15

### Fixed
- **Syndicate Board Member Parsing**: Fixed full Immortal Syndicate board recognition for PoE 3.30 / ExileCore. Replaced fixed slot indexing (`c in 9..23`) and faulty parent visibility checks (`!card.IsVisible`) with robust card enumeration across all board children, resolving member name from `card.Children[3].Children[0].Text`.
- **Division & Leader Placement**: Implemented division badge detection (`card.Children[4].IsVisibleLocal`) and column spatial clustering to accurately place members into Transportation, Fortification, Research, Intervention, and Outside/Free Agents. Accurately detected leaders by card frame width (324px vs 283.5px).
- **Removed Obsolete Atlas Passives**: Disabled "Test of Loyalty" and "Lethal Extraction" dependencies since these mechanics no longer exist on the current Path of Exile Atlas tree. Execute now reliably models +1 rank gain.

## [1.2.0] - 2026-11-14

### Added
- PoE 3.30 compatibility updates for Syndicate board element indexing and member detection.
- Fast cached syndicate member ranking and relationship resolution.

### Fixed
- Fixed syndicate member card visibility and rank parsing when opening the Betrayal investigation board.
- Ensured overlay decisions and safehouse alerts correctly track current area state.

## [1.1.0] - 2026-07-31

### Added

- Persistent `Debug Stats` settings tab with live board, relationship, division, member, event, outcome, and recommendation diagnostics.
- Searchable in-HUD `FAQ` tab covering configuration, scoring, relationships, encounters, Atlas passives, scarabs, compatibility, and troubleshooting.
- Persistent position and size for the safehouse notification window.
- Coloured member-priority frames with large ordered priority labels and placement scores.

### Fixed

- Debug mode no longer relies exclusively on short-lived ExileCore log messages to expose decision statistics.
- Safehouse Rewards and Catarina Focus now use one stable window instead of separate ImGui window identities.
- Replaced internal rank abbreviations and ambiguous missing-data markers with readable labels.
- Interrogate is no longer recommended while all three prisoner cards are occupied.
- Removed the obsolete on-board division-score diagnostics overlay.
- Cached prisoner-state detection per encounter to avoid frame-time spikes.

### Planned

- Replace more text parsers with stable ExileAPI fields where available.
- Cap marginal intelligence scoring by the exact amount remaining to 100.
- Add replayable parser and decision test fixtures.
- Investigate automatic map-device scarab detection.

## [1.0.0] - 2026-07-31

### Added

- First public release under the `BetrayalHelper` name.
- Public installation and strategy documentation.
- Full FAQ covering board layout, dependencies, scoring, relationships, Atlas passives, scarabs, troubleshooting, privacy, and compatibility.
- 5-5-2-2 relationship model favoring Farm-to-Filler links.
- Conditional Rival, Trusted, and Neutral scoring based on relationship topology.
- Configurable 0–2 Betrayal Scarabs of Unbreaking.
- Rank-aware Execute and Interrogate scoring.
- Test of Loyalty and Lethal Extraction decision modelling.
- Farm/Filler roles, independent Run/Skip policy, and ordered preferred leaders.

### Changed

- Renamed folder, project, assembly, namespace, plugin class, settings class, and HUD display name from the private `BetrayalHelperV2`/`mt_BetrayalHelper` naming to `BetrayalHelper`.
- Same-group Rival and Trusted relationships are no longer treated as universally beneficial.
- Removing desired cross-group rivalries is heavily penalized.

### Removed

- Non-functional toggles for drop-only Atlas passives.
- Machine-specific paths, local build output, and historical HUD error logs from the public package.

### Known limitations

- Some event parsing depends on English UI text.
- CatarinaFocus is less tested than SafehouseRewards.
- Lethal Extraction currently assumes the resulting post-Execute rank.
- Unbreaking scarab count is configured manually.
