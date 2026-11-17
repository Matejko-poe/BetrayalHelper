# Memory offsets used by BetrayalHelper

All reads are read-only (`GameController.Memory.Read<T>`). The plugin uses memory only: no UI-position or wrapper fallbacks. The **System** tab in the plugin settings shows which of these reads currently work.

Verified live on 2026-11-17 with the WhatsAnAiBridge (`tools/intel_snap.py` before/after snapshots).

## Board state vector

`SyndicatePanel + 0x3F8` = start, `+0x400` = end of a `std::vector` of 160-byte (`0xA0`) member states. Entry count is currently 15 (14 until Janus was discovered). The first 4 entries are the division leaders, one per division.

| Offset in state | Type | Meaning | Status |
|---|---|---|---|
| `+0x08` | ptr | target object; `*ptr` = UTF-16 name pointer (`ItThatFled` is normalized to `It That Fled`) | verified |
| `+0x18` | ptr | job object; `+0x08` = UTF-16 division name (`Transportation`, `Fortification`, `Research`, `Intervention`, `None`) | verified |
| `+0x28` | ptr | rank object; int32 at `+0x10` = rank 0-3 | verified |
| `+0x84` | int32 | safehouse intelligence 0-100, meaningful for the 4 leaders (Tora 72 -> 78 after a +6 event) | verified |
| `+0x94` | int32 | imprisonment turns left (1-3), 0 when free. Gravicius 3, Guff 3, others 0 | verified with 1 and 2 prisoners (turn countdown not yet observed) |
| `+0x74` | int32 | same value as `+0x94` for prisoners | observed, unused |
| `+0x90` | int32 | differs per prisoner (Gravicius 1, Guff 4), probably the division receiving intel | observed, unused |
| `+0x78` | - | NOT intelligence (old assumption); do not use | rejected |
| `+0x7C`, `+0x80` | float | UI position of the card; must not be used for logic | unused |

## Relationships

`IngameState.ServerData.BetrayalMemberRelationships` (ExileCore wrapper) returns typed Rival/Trusted per member and is verified live (Aisling-Elreon: none -> Trusted, symmetric). It is sometimes empty (observed 0, later 12 members), so the plugin caches the last non-empty snapshot. State vectors `+0x38` and `+0x50` are NOT relationships (`+0x50` = leader-to-member link, the yellow lines on the board). Raw offset of the map was not needed.

## Other sources

- Rival/Trusted topology: `IngameState.ServerData.BetrayalMemberRelationships` (ExileCore wrapper, works).
- Encounter choice: `SyndicatePanel.EventElement` (`Action.Id`, `EventText`, `Target2.Name`, button rects for highlight drawing only).
- Broken in ExileCore 3.30 (return empty / stale): `SyndicatePanel.SyndicateStates`, `SyndicatePanel.SyndicateLeadersData`.

## How to find a new offset

1. Open the board, `python tools/intel_snap.py snap before`.
2. Change exactly one thing in game (gain intel, imprison a member).
3. `python tools/intel_snap.py snap after`, then diff the two JSON snapshots per member (compare by name, the vector can be reallocated and reordered).
4. Verify with a second data point before trusting an offset.
