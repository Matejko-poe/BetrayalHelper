# BetrayalHelper FAQ

## Contents

- [General](#general)
- [Board layout](#board-layout)
- [Relationships](#relationships)
- [Decisions and scoring](#decisions-and-scoring)
- [Atlas passives](#atlas-passives)
- [Installation and compatibility](#installation-and-compatibility)
- [Troubleshooting](#troubleshooting)
- [Privacy and automation](#privacy-and-automation)

## General

### What does BetrayalHelper do?

It reads the visible Immortal Syndicate board and encounter-choice UI, assigns strategic scores to the available actions, and draws a green frame around the highest-scoring button.

### Does it play the encounter for me?

No. It does not move the character, attack, click a choice, open a safehouse, or modify the board. It is an informational overlay.

### Which strategy is the main target?

SafehouseRewards with a 5-5-2-2 board. CatarinaFocus exists but is secondary and receives less testing.

### Does it know current market prices?

No. Member selection is configured by the user. The plugin optimizes the board structure around those choices rather than claiming which reward is most profitable this week.

### Why are the default members not necessarily optimal?

League patches and trade prices change reward value. Defaults are a usable example and a migration aid, not financial advice.

## Board layout

### What does 5-5-2-2 mean?

Two divisions contain five members each and act as Filler divisions. Two divisions contain two selected members each and act as Farm divisions.

### Is two the game's capacity for a Farm division?

No. A division can contain up to five members. Two is the strategic target for a compact reward division.

### Why keep Farm divisions small?

A compact division concentrates encounters and safehouse rewards around selected members and makes it easier to use members from the filler divisions as interrogation targets.

### Why fill the other divisions to five?

They store the remaining board members, reduce unwanted placement options, and provide many candidates for cross-group relationships and interrogation.

### What is the difference between Farm/Filler and Run/Skip?

Farm/Filler controls desired member placement. Run/Skip controls whether intelligence and completion of that safehouse are desirable. They are intentionally independent.

### Why is the first member in a division special?

The ordered list treats index zero as the preferred leader. Moving a different member to leadership may therefore receive a lower score.

### Does the plugin enforce the layout?

No. It recommends choices that move the observed board toward the configured layout.

## Relationships

### How does the plugin know whether a line is Rival or Trusted?

It reads `ServerData.BetrayalMemberRelationships` directly from ExileCore. The plugin snapshots the committed topology before a decision card opens because the live data can already contain the previewed result while an option is displayed. Line colours are not inferred from pixels.

### Why are members without relationships left until last?

When multiple defeated members are available, the overlay prioritizes members that already have connections. This leaves an unconnected member until last, improving the opportunity for the remaining choice to create a useful new relationship.

### Where can I inspect the relationships detected by the plugin?

Open `Debug Stats` and expand `Relationship topology`. It lists every unique member pair, its exact Rival or Trusted status, and its strategic value for the configured Farm/Filler layout.

### Are all red Rival lines good?

Usually yes. Every Rival link can generate additional encounters and decisions. Farm-to-Filler Rival links receive the highest score because they connect the compact reward divisions with the larger filler pool, but same-group Rival links remain positive.

### Are green Trusted lines bad?

Not necessarily. A cross-group Trusted relationship is useful because two Trusted members are likely to receive a Betray option later, which can convert the relationship to Rival.

### Why prefer Rival over Trusted for a finished cross-group edge?

Rivals can appear to oppose or betray each other and tend to produce the encounter network used by the strategy. Trusted is treated as a construction stage, not a failure.

### When should I accept a same-group Rival relationship?

Normally accept it unless another side effect is worse. A same-group Rival still strengthens the encounter network; it simply receives a lower bonus than a Farm-to-Filler Rival.

### Why can Neutral be a good result?

Neutral normally loses value because it removes an existing Rival or Trusted edge. It is worth accepting only when another effect of the action compensates for that loss.

### Why is “remove all rivalries” heavily penalized?

It destroys accumulated board structure and usually requires many encounters to rebuild. Release is normally safer unless the board is intentionally being reset.

### Do relationships guarantee reinforcements?

No. They influence eligibility and likelihood; encounter type, ranks, leaders, board state, Atlas modifiers, and random selection still matter.

### Why did a linked member not appear?

They may be imprisoned, outside the active board, selected for another encounter, blocked by encounter rules, or simply not chosen by the random reinforcement roll.

### Why does interrogating a connected member carry an absence penalty?

An interrogated member cannot appear or reinforce during the interrogation period. Removing a highly connected member temporarily reduces the useful network.

## Decisions and scoring

### What are Special, Interrogate, and Release?

`Special` is the context-dependent right-side action such as Execute, Betray, Bargain, swap, or intelligence gain. `Interrogate` imprisons the member. `Release` leaves the board unchanged.

### Why can Release be recommended?

Release has a neutral baseline. It wins when both changing the board and interrogating the member have negative expected value.

### How is rank valued?

A correctly placed Farm member receives `30 + 10 × rank` before event-specific effects. Rank also affects Execute and Interrogate evaluation.

### Why might Execute in a Skip division be bad?

It can grant unwanted safehouse intelligence and can also strengthen an incorrectly placed member.

### Why might Execute a rank-1 Farm member be excellent?

With Enduring Legacy (50% extra rank), the plugin models the expected rank, which can count as reaching rank 3 sooner.

### Why is Execute at rank 3 sometimes neutral?

If the UI reports no rank effect, the promotion component is zero. Other effects such as relationship consequences can still matter.

### Why is Interrogate not automatically good with Unconditional Release?

The member is still absent for three encounters and cannot reinforce. Interrogation can also generate intelligence for a division marked Skip or immediately remove a leader from leadership.

### Does the plugin understand every Betray side effect?

It covers the common action IDs and text patterns, but PoE patches can add or reword outcomes. Enable debug logging when an outcome is missing.

### Are scores comparable to chaos or divine orbs?

No. They are internal strategic weights used to rank choices shown at the same encounter.

### Can several effects be counted for one button?

Yes. A Betray option may create a relationship, change ranks, move leadership, and grant intelligence. These are represented as separate outcomes and summed.

### Can a good Rival edge be rejected?

Yes. Removing a desired member, destroying critical placement, or severely damaging ranks can outweigh the relationship benefit.

### Does intelligence stop being valuable at 100?

The safehouse warning respects readiness, but not every parser perfectly caps marginal intelligence by the exact remaining amount. This is a known area for improvement.

## Atlas passives and scarabs

### Which Atlas passives have settings?

Only Test of Loyalty and Lethal Extraction, because they directly change the value of encounter choices.

### Why is Effective Leadership not a setting?

It changes leader appearance frequency and drops, but the old toggle did not alter a single calculation. A non-functional setting was more misleading than useful, so it was removed.

### Why are veiled-item and equipment passives absent?

They affect combat difficulty or drops rather than the board transition caused by Special, Interrogate, or Release. The plugin does not currently perform economic drop valuation.

### How is Test of Loyalty modelled?

Execute gains two ranks instead of one, capped at rank 3.

### How is Lethal Extraction modelled?

Execute grants intelligence equal to twice the resulting rank. Intelligence for Run divisions is positive; intelligence for Skip divisions is negative.

### Is the Lethal Extraction ordering confirmed?

The current assumption is that the post-Execute rank is used. If live testing proves the game uses the pre-Execute rank, this formula must be changed.

### What does Betrayal Scarab of Unbreaking do?

Each scarab gives interrogated targets in the area a 50% chance not to lose rank when interrogation completes. The map-device limit is two.

### Do two Unbreaking scarabs guarantee rank preservation?

The plugin models the modifiers additively as 100%. Report live evidence if the game behaves differently.

### Can the plugin detect inserted scarabs automatically?

Not currently. Set `UnbreakingScarabs` to 0, 1, or 2 for the mapping setup being used.

### What about Betrayal Scarab of Reinforcements?

It increases reinforcement frequency but does not change which relationship topology is desirable. It makes a well-built network trigger more often, so it does not currently require a separate choice-weight setting.

## Installation and compatibility

### Where should the plugin be installed?

`ExileCore/Plugins/Source/BetrayalHelper`.

### Why does a standalone build fail to locate DLLs?

The project uses ExileCore's standard relative references. Build it from the expected `Plugins/Source/BetrayalHelper` location or adjust the reference paths locally.

### Can I rename the folder?

The recommended folder name is `BetrayalHelper`. Other names may work, but documentation and troubleshooting assume the standard name.

### Does it work with localized clients?

Not reliably. Several event parsers expect English text. Action IDs cover many cases, but text-dependent regexes may fail on localized UI.

### Does it work after every PoE or ExileCore update?

No guarantee. UI child indices, action IDs, event strings, and ExileAPI memory objects can change.

### Are binary releases required?

No. ExileCore normally compiles the source plugin on startup.

## Troubleshooting

### The plugin does not appear in ExileCore

Confirm the folder and `.csproj` name are `BetrayalHelper`, check HUD compile logs, and verify the current ExileCore build targets a compatible .NET version.

### The plugin compiles but shows nothing

Enable it, open the Syndicate board or encounter-choice UI, and verify the game is using an English client. The overlay is context-sensitive.

### DebugMode is enabled, but I cannot see statistics

Open the plugin settings and select `Debug Stats`. DebugMode still writes timed diagnostic messages to the ExileCore log, while this tab provides the persistent live view: board visibility, cached members, relationship references, division summaries, current event, parsed outcomes, recommendation, and member scores.

### Where is the built-in documentation?

Open the plugin settings and select `FAQ`. Enter any phrase to filter by category, question, or answer; clear the search field to display the full operating guide.

### The wrong button is highlighted

Enable DebugMode, capture the full event text and board, and open a bug report. Do not choose an action until the text is recorded.

### No button is highlighted

The event may be unsupported, parsing may have failed, or all actions may have tied scores. Debug logs should show the action ID and generated outcomes.

### What do the coloured frames around defeated Syndicate members mean?

- Green: desired Farm member in a valuable placement, with a score of at least 30.
- Yellow: acceptable Filler member, with a score of at least 1.
- Red: unwanted or incorrectly placed member, with a score of 0 or lower.

These colours represent configured strategic value. They do not indicate item rarity, friendship, or rivalry.

### What does `Priority N | Score X` mean?

Priority is the suggested conversation order: speak to Priority 1 first, followed by 2 and 3. Score is the member's current placement value used to establish that order. The first member also receives a subtle coloured fill. After opening a member's encounter panel, the plugin evaluates the available action buttons separately.

### What happens when all three interrogation slots are occupied?

When all three prisoner cards are occupied, Interrogate is treated as unavailable regardless of its calculated strategic value. The plugin reads the prisoner marker on each Syndicate member state, excludes Interrogate, and chooses the best available Special or Release action instead. The detected queue state and prisoner names are shown in `Debug Stats` beside the last decision.

### My saved settings disappeared after upgrading from BetrayalHelperV2

The public release uses a new plugin, assembly, namespace, and folder name. Old configuration keys may not migrate automatically. Reconfigure the division layout once.

### Why are there nullable warnings during build?

Some ExileAPI objects are only non-null while specific panels are visible. The plugin has runtime guards in core paths, but additional nullable cleanup remains welcome.

### What should a useful bug report include?

- plugin commit/version,
- PoE and ExileCore versions,
- action ID and complete English event text,
- board screenshot,
- Farm/Filler and Run/Skip configuration,
- Atlas passive and scarab settings,
- expected action and highlighted action,
- relevant debug log lines.

## Privacy and automation

### Does BetrayalHelper send data anywhere?

No network code is included in this repository.

### Does it include WhatsAnAiBridge?

No. Bridge tooling may be used by developers for local diagnostics, but it is not a dependency and is not distributed with BetrayalHelper.

### Does it contain private game data or authentication tokens?

No. Build artifacts, local error logs, bridge files, tokens, and machine-specific paths are excluded from the public repository.

### Is this allowed by Grinding Gear Games?

Users are responsible for following the current game rules and third-party tool policy. BetrayalHelper only displays information and does not automate input, but this project makes no legal or policy guarantee.
