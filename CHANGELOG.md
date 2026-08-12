# Changelog

All notable changes to BetrayalHelper are documented here. The format follows Keep a Changelog and uses semantic versioning from the first public release.

## [Unreleased]

- Remove the obsolete preferred-leader marker, member ordering controls, and related scoring documentation. Configured division members are now clearly presented as an unordered set.
- Fix Skip/Filler intelligence scoring so unwanted safehouse intelligence is negative instead of receiving a small bonus.
- Add optional per-division member preservation in Catarina Focus; unchecked divisions ignore member placement while all safehouses remain enabled.
- Normalize the relationship named by the current choice instead of caching topology between conversations; ExileCore's live map can already contain the previewed result.
- Show the exact preview normalization used by scoring in `Debug Stats` and the debug log.
- Treat same-group Rival and Trusted relationships as useful instead of applying topology penalties.
- Score a Trusted-to-Rival conversion by its incremental improvement, avoiding double-counting the existing link.
- Scale Betray intelligence value by the actual amount instead of applying fixed `+100` or `-50` values.
- Score becoming Neutral as the loss of the exact current Rival or Trusted edge.
- Guard against a transient null encounter text while ExileCore is populating the Betrayal choice panel.
- Read exact Rival and Trusted topology from `ServerData.BetrayalMemberRelationships` instead of incorrectly treating division membership as relationship data.
- Prioritize already-connected defeated members and leave members without relationships until last.
- Show exact Rival and Trusted link counts in Debug Stats.
- Add a scrollable Debug Stats topology table with every unique pair, its exact status, and its strategic classification.
- Weight Rival links more strongly than Trusted links when evaluating the temporary network loss from Interrogate.
- Score removal of all rivalries from the actual current topology instead of using a fixed penalty.
- Document the direct game-memory relationship source in the HUD FAQ and project documentation.

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
