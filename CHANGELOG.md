# Changelog

All notable changes to BetrayalHelper are documented here. The format follows Keep a Changelog and uses semantic versioning from the first public release.

## [Unreleased]

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
