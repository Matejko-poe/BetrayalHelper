# BetrayalHelper - technical reference (data map)

Purpose: for every piece of data the plugin uses, say **where it comes from, how it is read, where it is used, what the Settings > System tab shows for it, how it breaks and how to repair it**. After a game patch, start here instead of reading 3800 lines of code.

Rules that apply to everything below:

- **Memory only.** Data comes from game memory (read-only `GameController.Memory.Read<T>`) or from ExileCore wrappers that read memory. No card positions, no screen coordinates, no UI-tree layout guessing. UI rectangles are used only to *draw* highlights.
- If a read fails, the plugin must say so (System tab, red `MEMORY READ FAILED` window) and stop recommending. It must not guess.
- Names: the engine writes `ItThatFled`; everything inside the plugin uses `It That Fled` (`NormalizeName`).

Symbols: `M` = raw memory offset, `W` = ExileCore wrapper, `E` = entity/file wrapper, `U` = UI element (draw only), `S` = plugin settings.

## 0. Quick triage after a patch or a wrong recommendation

1. Open plugin settings > **System**. Every row is one data ID from section 1. Fix FAIL rows top to bottom (a failed upstream row makes later ones meaningless).
2. Rows OK but recommendation odd: set `DebugMode`; HUD log (`D:\nigus\3.30\Logs\Info*.log`) prints every outcome group with its component scores (`Group Special: total ... [score: description; ...]`). Then see section 2 for the formula.
3. To locate a moved offset use the before/after snapshot method (section 4). Typical fix is changing one constant (1-2 bytes) in the code location given in the catalog.

## 1. Data catalog

Offsets are relative to the 160-byte (`0xA0`) member state unless stated. "Code" names the function that owns the read; grep for the offset (e.g. `0x84`) to find every use.

| ID | Data | Source | Read in (code) | Used by | System row | Status |
|---|---|---|---|---|---|---|
| M-PANEL | Syndicate panel object address | `GameController.Game.IngameState.IngameUi.SyndicatePanel.Address` (W, ExileCore) | `updateSyndicateInfo`, `ReadDivisionIntel`, `ReadPrisonersFromMemory`, `RunSystemChecks` | everything below | Syndicate panel | verified |
| M-VEC | Member state vector: start `panel+0x3F8`, end `panel+0x400`, entry size 160 | M, int64 x2 | same four functions | M-NAME..M-PRISON | State vector (panel+0x3F8) | verified (15 entries, 14 before Janus appeared) |
| M-NAME | Member name | `state+0x08` -> ptr P; `*(P)` -> UTF-16 string ptr -> `ReadStringU` | `updateSyndicateInfo` | all scoring, overview, overlay | Member names | verified |
| M-JOB | Division of the member | `state+0x18` -> ptr J; `*(J+0x08)` -> UTF-16 string: `Transportation`/`Fortification`/`Research`/`Intervention`/`None` (`None` = outside) | `updateSyndicateInfo`, `ReadDivisionIntel` | scoring (`GetScore`), overview columns | Divisions (job) | verified |
| M-RANK | Rank 0-3 | `state+0x28` -> ptr R; `int32 *(R+0x10)` is **inverted**: record 1 = Captain (rank 3), 2 = Lieutenant (2), 3 = Sergeant (1), 0 = outside. `DecodeRank` maps it; verified on 8 cards by stars/title | `updateSyndicateInfo` | scoring, overview, Execute | Ranks | verified |
| M-LEADER | Division leader flag | index in vector `< 4` (first four entries are the leaders, one per division) | `updateSyndicateInfo` (`isLeader = i < 4`), `ReadDivisionIntel` | overview highlight, intel | Leaders (first 4 entries) | verified on live data (assumption: ordering); breaks if leaders stop being first |
| M-INTEL | Safehouse intelligence 0-100 of the leader's division | `int32 state+0x84`, only for the first 4 entries | `ReadDivisionIntel` | ready banner, safehouse window (`checkIntel`), intel scoring, overview bars | Safehouse intel (leader+0x84) | verified (Tora 72 -> 78 after +6) |
| M-PRISON | Turns left in prison (1-3, 0 = free) | `int32 state+0x94` | `ReadPrisonersFromMemory` | Interrogate blocked at 3/3 (`ReadInterrogationQueueFull`) | Prisoners (state+0x94) | verified with 0, 1, 2 prisoners |
| M-PRISON2 | Same turns value / division receiving intel | `state+0x74` (= turns), `state+0x90` (1, 4 seen) | - | unused, noted for future | - | observed only |
| W-REL | Typed relationships Rival/Trusted between members | `IngameState.ServerData.BetrayalMemberRelationships` (W): `Dictionary<member, Dictionary<member, Rival/Trusted>>` | `updateSyndicateInfo` (builds `liveRelationships`, caches last non-empty in `_lastRelationships`) | relationship scoring (`AddRelationshipOutcome`, `GetExistingRelationshipValue`), network penalty in Interrogate | Relationships (ServerData) | verified (Aisling-Elreon none -> Trusted); wrapper is sometimes empty, cache covers it |
| W-EVENT | Open choice window: action id, text, member | `panel.EventElement ?? panel.BetrayalEventData` (W): `.IsVisible`, `.Action.Id`, `.EventText`, `.Target2.Name`; `.CurrentEncounterJob.Name` (often throws, caught -> `Unknown`) | `DrawSyndicateBoard` | all outcome parsers (`switch (_eventAction)`) | Encounter event (N/A when closed) | verified (`GainIntelligenceLarge` / `+4 Research Intelligence` / Gravicius) |
| U-BTN | Rectangles of Special/Interrogate/Release buttons | `eventData.SpecialButton/InterrogateButton/ReleaseButton.GetClientRectCache` (U) | `DrawSyndicateBoard` | **drawing only**: green frame + fill on the recommended button | - | draw only; if wrong the frame is misplaced, data unaffected |
| E-CHOICE | Members shown in the current map encounter (overlay frames #1/#2/#3) | `EntityListWrapper.ValidEntitiesByType[EntityType.BetrayalChoice]` + `StateMachine` state `NPCIndex` -> `GameController.Files.BetrayalTargets.EntriesList[idx].Name` | `BetrayalEventMembers` | in-window logic (who else is present) | - | works (not covered by System tab yet) |
| U-LABEL | On-ground labels of the encounter members (overlay frames/priority) | `IngameUi.ItemsOnGroundLabels` with metadata `Metadata/Terrain/Leagues/Betrayal/Objects/BetrayalMakeChoice`; name text via `Label.GetChildFromIndices(0, 0)`, rect via `GetClientRect()` | `scanBetrayal` | frames + `#N` labels over encounter members | - | draw + name match; if broken, frames vanish |
| A-AREA | Hideout check for the safehouse window | `GameController.Area.CurrentArea.Area.IsHideout` (W) | `checkIntel` | safehouse window only in hideout | - | works |
| S-CFG | Farm/Filler roles, member lists, Run/Skip, Atlas toggles | plugin settings JSON `D:\nigus\3.30\config\global\BetrayalHelper_settings.json` | `Settings.*` | everything in section 2 | Configured members | works |

Rejected / do not use:

| Offset | Why |
|---|---|
| `state+0x78` | was assumed to be intelligence; holds leader flag/garbage |
| `state+0x50` (vector of one state pointer, leaders only) | leader -> member link (yellow line on the board), NOT a relationship |
| `state+0x38` (pairs) | per-member UI/upgrade data, not relationships |
| `state+0x7C`, `+0x80` | card screen position (floats); must not be used |
| `panel.SyndicateStates`, `panel.SyndicateLeadersData` (W) | ExileCore 3.30 returns empty / address 0 |
| card position, `Position.Y > 1700`, line colours | forbidden heuristics |

## 2. How values are computed

Everything the player sees is the sum of `Outcome` objects grouped by action type; the highest group total gets the green frame. `PrintHighestScoringActionType` logs each group.

### 2.1 Placement score (`GetScore(name, job, rank)`)

| Situation (SafehouseRewards) | Score |
|---|---|
| member listed for a Farm division and standing in it | `30 + 10 x rank` |
| any other member in a Farm division | `-1` (intruder) |
| a member listed for Farm standing in a Filler division | `-1` |
| ordinary member in a Filler division | `1` |
| outside / unknown division | `0` |

Thresholds used everywhere: `>= 30` good Farm member, `>= 1` filler, `0` outside, `< 0` misplaced.

### 2.2 Run policy (`ShouldRunDivision`)

- `CatarinaFocus`: always run.
- `SafehouseRewards`: only Farm divisions can run (Filler never); the Run tick can only switch a Farm division off.
- Run-wanted divisions (`_jobsToGainIntelligence`) get full intel value; others get a small positive value (intel is free, only the safehouse run is skipped).

### 2.3 Safehouse ready (`UpdateReadySafehouses`)

Division is ready when `ReadDivisionIntel()[division] >= 100` **and** `ShouldRunDivision(division)`. Source: M-INTEL. Nothing else (no rank or UI signals) can trigger it.

### 2.4 Intelligence events (`IntelligenceAction`, text `+N <Division> Intelligence`)

- Division is run-wanted: `useful = min(N, 100 - currentIntel)`; score `useful x 6` (`x 9` in CatarinaFocus), `+25` if the event reaches 100.
- Division not run-wanted (Filler): intel is free, `+1` per useful point (beats Release, never beats a real option). Same formula for the `+N Division Intelligence` part of Bargain events (`BuildIntelOutcome`).

### 2.5 Execute (case `Execute`)

- Good Farm member (`score >= 30`): `80 x expectedGain`, `expectedGain = min(3 - rank, 1 + extraRankChance)`; `extraRankChance = 0.5` with `EnduringLegacy`. "No effect" text -> `5`.
- Filler member: `15` (`40` in Catarina), but `-150` if it would be promoted inside a Farm division.
- Misplaced member: `-150`.
- Move-to-division events add `+100` (good member into its Farm division), `-100` (bad member into Farm division when no space), `+100` (unallocated member into suitable job). Capacity = number of configured members of that Farm division.

### 2.6 Interrogate (`AddInterrogationStrategyOutcome` + placement outcomes)

- Intel value: `rank x 10` if the division runs, else `-rank x 8`.
- Rank loss: `-25 x (1 - keepRankChance)` for a good Farm member; `keepRankChance = 0.30` with `UnconditionalRelease`.
- Absence from network: `-min(40, 7 x Rivals + 4 x Trusted)`.
- Placement: intruder (`score < 0`) `+150 x (1 - keepRankChance)`; correctly placed Farm member `-150`; outside member `-50`.
- Prison full (3/3, M-PRISON) -> Interrogate group set to `-1,000,000` (never recommended).

### 2.7 Relationships (`AddRelationshipOutcome`, `GetExistingRelationshipValue`)

Value of a link depends on the roles of both members' divisions:

| Link | Farm<->Filler | same group | other |
|---|---|---|---|
| Rival | 140 | 70 | 30 |
| Trusted | 65 | 30 | 10 |

New link score = `newValue - value of the link it replaces`; becoming Neutral = `-(value of the removed link)`. Removing all rivalries = minus the sum of existing Rival values.

### 2.6b Release

Fixed `0`. Wins when both other groups are `<= 0`.

### 2.8 Overlay frames and order (`scanBetrayal`)

- Frame colour: green `score >= 30`, yellow `score >= 1`, orange = Farm target still outside, red otherwise.
- Order `#1..#N`: members with relationships first, then more Rivals, then higher score, then more relationships.

### 2.9 Atlas nodes

Source: poedb Atlas passive tree, Betrayal mastery. Only two nodes change decisions: Enduring Legacy (`+50%` extra rank on Execute) and Unconditional Release (`30%` keep rank on Interrogate). The other nodes (Head of the Snake, Hidden Flank, Hard Bargain, Coercive Interrogation, drop/rarity/reinforcement/bargain small nodes) are intentionally not modelled. Test of Loyalty and Lethal Extraction no longer exist.

### 2.10 History and statistics (`HistoryLogger`, `RecordBoardHistory`, `BuildPlanSnapshot`)

Folder: `D:
igus.30\config\BetrayalHelper\history\` (HUD `ConfigDirectory`), toggle `RecordHistory`. Append-only JSON lines:

| File | Content |
|---|---|
| `board.jsonl` | `baseline` of the whole board, then every change: `job`, `rank`, `leader`, `prison`, `intel`, `relation_added/removed`, `member_appeared/disappeared`; each has `eventId` when it happens within 15 s of an encounter choice (so a choice can be joined to its effect) |
| `decisions.jsonl` | one `decision` per event (action id, text, member, job, rank, placement score, every outcome group with its components, recommendation, settings incl. effective Run per division and Atlas toggles, plan snapshot) and an `event_closed` marker |
| `plan.jsonl` | plan-vs-reality snapshots (written after changes): per Farm division placed/target and rank sum/max, filler count, intel per division, prisoners, relation groups (Rival/Trusted cross-group vs same-group), members off board |
| `stats.json` | running counters: `decisions.total`, `decisions.recommended.<Action>`, `decisions.event.<EventId>.recommended.<Action>`, `changes.<field>`, `events.closed`, `areas`, `sessions` |

The chosen button is not read directly; it is inferred from the board changes that follow the event (same `eventId`). Compare `recommended` with the resulting changes to measure how often the advice is followed and what it yields.

## 3. Failure -> likely cause -> repair

| Symptom | System row | Likely cause | Repair |
|---|---|---|---|
| red `MEMORY READ FAILED`, all rows FAIL after "State vector" | State vector | `panel+0x3F8/0x400` moved or entry size changed | find the new vector: scan the panel (`memory.read` of `panel+0x300..0x600`) for two heap pointers whose difference is `n x 160`; update the two constants (`0x3F8`, `0x400`) and the `160` stride in the four reading functions |
| names unresolved | Member names | `+0x08` or the inner name pointer moved | the state is 20 qwords; dump one state, find the qword whose target holds a pointer to a UTF-16 member name; change `+0x08` |
| divisions all `None` or unknown names | Divisions | `+0x18` / job name at `+0x08` moved | same method; job object contains a pointer to a UTF-16 division name |
| ranks outside 0-3 | Ranks | `+0x28` / `+0x10` moved | rank object: find int32 equal to the visible rank (Vagan 3, Tora 1 ...) |
| intel values outside 0-100 or wrong | Safehouse intel | `+0x84` moved | `intel_snap.py snap before`, gain intel (+N), `snap after`, `diff`: the leader field that changes by exactly N |
| prisoners wrong | Prisoners | `+0x94` moved | imprison one member, snapshots before/after: the field that becomes 3 for that member only |
| relationship row FAIL/WARN, relation scoring blank | Relationships | ExileCore `ServerData` wrapper empty/stale | WARN = cached snapshot in use (fine); FAIL = never read: open the board/an encounter. If it stays empty after patch, wait for ExileCore update or find the map via a before/after relation change (`tools/sd_snap.py`) |
| recommendations never appear in an encounter | Encounter event | `EventElement` wrapper or `Action.Id` changed | `bridge_client.py eval "...SyndicatePanel.EventElement.Action.Id"`; check `IsVisible`, `EventText`, `Target2.Name` |
| green frame misplaced | - (U-BTN) | button element rect changed | draw-only; check `eventData.SpecialButton.GetClientRectCache` |
| frames over encounter members missing | - (U-LABEL/E-CHOICE) | label metadata path or child indices changed | `Metadata/Terrain/Leagues/Betrayal/Objects/BetrayalMakeChoice`; name element `GetChildFromIndices(0,0)` |

## 4. Tools

All read-only. Token is never printed; the client reads it from the bridge folder.

```text
BetrayalHelperv2/tools/bridge-kit/bridge_client.py   status | eval "<expr>" | describe "<expr>" | collect | raw
BetrayalHelperv2/tools/intel_snap.py                 snap <name> | diff <a> <b>      (board state vector + objects it points to)
BetrayalHelperv2/tools/sd_snap.py                    snap <name> | diff <a> <b>      (ServerData struct and heap blocks)
bridge memory.layout {"path": "..."}                 struct layout with real bytes and validity checks
```

Procedure for a moved offset: (1) board open, `snap before`; (2) change exactly one thing in game; (3) `snap after`; (4) compare **by member name** (the vector can be reallocated and reordered; leaders are always the first four); (5) confirm with a second data point (1 and 2 prisoners; two intel steps) before trusting it.

Build and deploy: `.\manage.ps1 deploy BetrayalHelper`, **then** `.\manage.ps1 build BetrayalHelper` (build compiles the HUD copy, so deploy first). HUD recompiles on start; log `D:\nigus\3.30\Logs\Info*.log`, marker line `BetrayalHelper build: ...` confirms the new code runs.

## 5. Code map

| Function | Responsibility |
|---|---|
| `Initialise`, `NormalizeName`, `NormalizeSettingsNames` | name normalization of saved settings |
| `RunSystemChecks`, `DrawSystemTab` | System tab (every row = a catalog ID) |
| `updateSyndicateInfo` | reads M-VEC/M-NAME/M-JOB/M-RANK + W-REL into `ListBetrayalMasters`; sets `_memoryReadOk` |
| `ReadDivisionIntel`, `UpdateReadySafehouses`, `checkIntel` | M-INTEL, ready banner, safehouse window |
| `ReadPrisonersFromMemory`, `ReadInterrogationQueueFull` | M-PRISON |
| `DrawSyndicateBoard` | reads W-EVENT, runs the `switch (_eventAction)` parsers, calls `PrintHighestScoringActionType` |
| `GetScore`, `UpdateMasterGoalStatus`, `ShouldRunDivision` | placement score, goals, Run policy |
| `IntelligenceAction`, `AddInterrogationStrategyOutcome`, `GetRankAfterExecute`, `AddRelationshipOutcome` | section 2 formulas |
| `scanBetrayal` | overlay frames and `#N` order over encounter members |
| `DrawBottomBoardGui` | Board Overview window |
| `PrintHighestScoringActionType` | sums outcome groups, logs them, draws the button frame |

Related: [MEMORY_OFFSETS.md](MEMORY_OFFSETS.md) (offset history and rejected ones), [FAQ.md](FAQ.md) (player-facing), `CHANGELOG.md` (what changed when).
