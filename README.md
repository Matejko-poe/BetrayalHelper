# BetrayalHelper

BetrayalHelper is an [ExileCore](https://github.com/exApiTools/ExileApi) plugin for Path of Exile 1. It reads the visible Immortal Syndicate board, evaluates encounter choices, and highlights the action that best supports a configurable safehouse-farming strategy.

This project is a maintained and substantially extended fork of [myrahz/BetrayalHelperRevamped](https://github.com/myrahz/BetrayalHelperRevamped). Credit for the original plugin and its core Betrayal-helper implementation belongs to its original author and contributors.

The plugin is designed primarily for a classic **5-5-2-2 board**: two compact reward divisions and two large filler divisions, connected through carefully selected Trusted and Rival relationships.

> [!IMPORTANT]
> BetrayalHelper is a decision-support overlay. It does not click buttons, automate encounters, or modify game memory.

## Features

- Reads members, divisions, ranks, leaders, typed Rival/Trusted relationships, safehouse intelligence, and the current encounter choice.
- Highlights the recommended `Special`, `Interrogate`, or `Release` button.
- Configurable Farm/Filler role for every division.
- Configurable Run/Skip policy independent of member placement.
- Board Overview: per-division members, ranks, highlighted leader, goal marker, live safehouse intel bars, and a list of members missing from the board.
- Safehouse alert when a Run division reaches 100 intelligence (read from memory).
- Rank-aware Execute scoring.
- Atlas modelling for the current tree: Enduring Legacy and Unconditional Release.
- Relationship scoring optimized for a bipartite 5-5-2-2 network.
- Debug and verbose-debug output for unsupported or changed encounter text.

## Requirements

- Path of Exile 1
- A current ExileCore/ExileAPI installation
- Windows
- .NET 10 runtime compatible with the current ExileCore build

The project references assemblies from three directories above the plugin folder, matching the standard layout:

```text
ExileCore root/
├── ExileCore.dll
├── GameOffsets.dll
├── ImGui.NET.dll
├── Newtonsoft.Json.dll
├── SharpDX.Mathematics.dll
└── Plugins/
    └── Source/
        └── BetrayalHelper/
```

## Installation

### Git

From `Plugins/Source`:

```powershell
git clone https://github.com/Matejko-poe/BetrayalHelper.git
```

### Manual

1. Download the repository as ZIP.
2. Extract it as `Plugins/Source/BetrayalHelper`.
3. Start or reload ExileCore.
4. Enable `BetrayalHelper` in the plugin menu.

ExileCore compiles source plugins when it starts. Build output belongs in `bin/` and `obj/` and is intentionally excluded from the repository.

## Quick configuration

1. Select `Safehouse Rewards`.
2. Mark the two reward divisions as `Farm` and the two storage divisions as `Filler`.
3. Put two desired members in each Farm division.
4. Put five remaining members in each Filler division.
5. Order each list so the preferred leader is first.
6. Set Run/Skip separately for every safehouse.
7. Enable Enduring Legacy and Unconditional Release only if allocated on your Atlas tree.

The defaults are examples, not universal economic recommendations. Safehouse rewards change between leagues; configure members for your own goals.

## The 5-5-2-2 relationship model

The desired relationship graph is bipartite:

```text
members in 2-person Farm divisions  <──>  members in 5-person Filler divisions
```

Relationship value depends on both endpoints:

| Relationship | Placement | Strategic value |
|---|---|---:|
| Rival | Farm ↔ Filler | High positive |
| Trusted | Farm ↔ Filler | Positive; seed for a later Betray/Rival |
| Rival | Same group | Positive |
| Trusted | Same group | Positive; lower than Rival |
| Neutral | Removes Rival/Trusted edge | Negative |

Why: every relationship expands the reinforcement and decision network. Cross-group edges receive an additional premium because they connect compact reward divisions with the larger filler pool, but same-group links remain useful.

A Rival edge is positive by itself. The side effect of the Betray option—rank loss, division-wide rank changes, removal from the organization, leadership changes, equipment destruction, or unwanted intelligence—is scored separately and may still outweigh the relationship bonus.

## Settings

### Farming strategy

- `SafehouseRewards`: optimize selected safehouses and preserve the 5-5-2-2 board.
- `CatarinaFocus`: legacy/secondary mode that runs every safehouse. SafehouseRewards receives most testing.

### Division Role

- `Farm`: compact division containing deliberately selected reward members.
- `Filler`: large division used for board control and safe interrogation targets.

### Run policy

Run/Skip answers a different question from Farm/Filler: should intelligence for this safehouse be accelerated and should the safehouse be completed at 100 intelligence?

### Atlas passives

Source: poedb Atlas passive tree, Betrayal mastery. Test of Loyalty and Lethal Extraction no longer exist.

- `EnduringLegacy`: Executed members have 50% chance to gain an additional rank. Modelled as expected rank (rounded) after Execute.
- `UnconditionalRelease`: Interrogated targets have 30% chance to not lose rank. Scales the rank-loss penalty of Interrogate.

Other Betrayal nodes (Head of the Snake, Hidden Flank, Hard Bargain, Coercive Interrogation, drop and rarity nodes, small rank nodes) do not change the Special/Interrogate/Release choice and are not modelled. Enable only the nodes you actually allocated.

### Display

- `ShowBoardOverview`: Board Overview window.
- `ShowPriorityOrder`: #1/#2/#3 labels above encounter members (order: members with relationships first, more Rivals, higher score). Frames always show. The raw action label above the recommended button appears only in `DebugMode`.

### Safehouse intelligence

Intel (0-100) is read per division leader from the board state vector at `state + 0x84` (first four entries of the vector at `SyndicatePanel + 0x3F8`). Verified live: +6 intel changed 72 to 78. A Run division at 100 shows a banner and opens the safehouse window.

## How scoring works

Each visible choice generates one or more `Outcome` entries. Outcomes are grouped by action and summed. The highest total highlights its button.

Major components include:

- member placement,
- member rank,
- preferred division and leader,
- safehouse Run/Skip policy,
- immediate and future intelligence,
- relationship topology,
- temporary interrogation absence,
- expected rank preservation from Unconditional Release,
- destructive Betray side effects,
- board capacity and missing desired members.

Correctly placed Farm members use a rank-aware base value:

```text
30 + 10 × rank
```

The numeric weights are strategic heuristics, not currency prices. See [FAQ](docs/FAQ.md) for edge cases and limitations.

## Supported encounter outcomes

The plugin recognizes common ExileAPI action IDs including:

- Execute
- Steal Intelligence
- Gain Intelligence / Gain Intelligence Large
- Promote NPC
- Swap NPC Job
- Swap Leader
- Steal Ranks
- Remove NPC / NPC Leaves Organization
- Befriend / become Trusted / become Rivals / become Neutral
- release prisoners
- destroy Syndicate equipment
- remove rivalries
- bargain for currency, maps, unique items, scarabs, or veiled items

Some outcomes are parsed from English UI text. Game updates or localization changes can require parser updates.

## Debugging

The plugin settings contain a persistent `Debug Stats` tab. It shows the live board state, relationship counts, division summaries, current encounter, last parsed outcomes, last recommendation, and a per-member scoring table. This remains visible while the settings window is open, unlike timed HUD log messages.

Enable `DebugMode` to log:

- action ID,
- event text,
- selected member,
- parsed outcomes,
- relationship classification,
- final action score.

Enable `MegaDebugMode` for full member-state output. When reporting an incorrect recommendation, include the information requested in the [bug report template](.github/ISSUE_TEMPLATE/bug_report.yml).

The settings also contain a searchable `FAQ` tab. Search matches category names, questions, and answer text; leaving the field empty displays the complete built-in guide.

## Build

The easiest validation is to place the repository in `Plugins/Source/BetrayalHelper` and let ExileCore compile it.

Manual build from that location:

```powershell
dotnet build .\BetrayalHelper.csproj --no-restore
```

## Known limitations

- SafehouseRewards is better tested than CatarinaFocus.
- Some UI fields are accessed through child indices and string representations supplied by ExileAPI.
- Relationship types are read directly from `ServerData.BetrayalMemberRelationships`; no screen-colour detection is required.
- Atlas nodes are toggled manually; the plugin does not read the Atlas tree.
- Member names: the engine uses `ItThatFled`; the plugin normalizes it to `It That Fled`.
- Intelligence is capped by the remaining amount to 100 for Gain Intelligence events; other parsers (Steal Intelligence, Execute) still use fixed weights.
- No automated test suite currently simulates every localized encounter string.

## Documentation

- [Full FAQ](docs/FAQ.md)
- [Changelog](CHANGELOG.md)
- [Contributing](CONTRIBUTING.md)
- [Security](SECURITY.md)

## License

[MIT](LICENSE)

This project is not affiliated with or endorsed by Grinding Gear Games or the ExileCore project.
