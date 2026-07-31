# BetrayalHelper

BetrayalHelper is an [ExileCore](https://github.com/exApiTools/ExileApi) plugin for Path of Exile 1. It reads the visible Immortal Syndicate board, evaluates encounter choices, and highlights the action that best supports a configurable safehouse-farming strategy.

The plugin is designed primarily for a classic **5-5-2-2 board**: two compact reward divisions and two large filler divisions, connected through carefully selected Trusted and Rival relationships.

> [!IMPORTANT]
> BetrayalHelper is a decision-support overlay. It does not click buttons, automate encounters, or modify game memory.

## Features

- Reads members, divisions, ranks, leaders, relationships, safehouse intelligence, and the current encounter choice.
- Highlights the recommended `Special`, `Interrogate`, or `Release` button.
- Configurable Farm/Filler role for every division.
- Configurable Run/Skip policy independent of member placement.
- Ordered member lists; the first member is treated as the preferred leader.
- Rank-aware Execute scoring.
- Optional Atlas-passive modelling for Test of Loyalty and Lethal Extraction.
- Betrayal Scarab of Unbreaking support from 0 to 2 scarabs.
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
7. Enable Test of Loyalty or Lethal Extraction only if allocated on your Atlas tree.
8. Set Unbreaking Scarabs to the number inserted into the current map.

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
| Rival | Farm ↔ Farm | Negative |
| Trusted | Farm ↔ Farm | Negative |
| Rival/Trusted | Filler ↔ Filler | Negative |
| Neutral | Removes Farm ↔ Filler edge | Strong negative |
| Neutral | Removes same-group edge | Positive cleanup |

Why: the useful cross-group edge lets a filler member join a valuable member's encounter. The filler can then be interrogated while the compact reward division stays intact. Same-group edges consume reinforcement opportunities and can pull valuable members into the wrong encounter.

A Rival edge is therefore **not automatically good**. The side effect of the Betray option—rank loss, division-wide rank changes, removal from the organization, leadership changes, or equipment destruction—is scored separately and may outweigh the relationship bonus.

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

- `TestOfLoyalty`: Execute is modelled as +2 ranks, capped at rank 3.
- `LethalExtraction`: Execute adds `2 × resulting rank` intelligence to the member's current division.

Only passives that change encounter decisions are exposed. Drop-only passives are intentionally absent.

### Unbreaking Scarabs

`UnbreakingScarabs` accepts 0, 1, or 2:

- 0: normal rank-loss risk after interrogation.
- 1: 50% expected rank preservation.
- 2: 100% expected rank preservation.

Even at two scarabs, interrogation temporarily removes the member from encounters and reinforcement selection. Highly connected members retain an absence penalty.

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
- expected rank preservation from Unbreaking scarabs,
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
- Relationship storage currently records connected member names but not every underlying relationship flag.
- Lethal Extraction is currently assumed to use the resulting post-Execute rank.
- Scarab count is configured manually per mapping setup.
- Intelligence value is not yet capped precisely by the remaining amount to 100 in every outcome parser.
- No automated test suite currently simulates every localized encounter string.

## Documentation

- [Full FAQ](docs/FAQ.md)
- [Changelog](CHANGELOG.md)
- [Contributing](CONTRIBUTING.md)
- [Security](SECURITY.md)

## License

[MIT](LICENSE)

This project is not affiliated with or endorsed by Grinding Gear Games or the ExileCore project.
