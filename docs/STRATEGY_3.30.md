# Strategy notes for 3.30 and the recommended default setup

Source: r/pathofexile thread "3.30 Betrayal Cheatsheet + Customizer Website [INFO PRE-LAUNCH]" (post by Ayeleth_, 99 comments, saved as an `.mhtml` file in the plugin folder, read on 2026-11-18). The post quotes the 3.30 patch notes; the member tables come from a commenter reading `betrayaltargets` / `betrayaltraitorrewards` game data (marked "needs confirmation" by the author). Treat everything here as a community source, not verified by the plugin. The author later published a final 3.30 version at `pathofexile.elrincondelexiliado.com/syndicate` (not read).

## What changed in 3.30 (from the post)

- Betrayal scarabs are removed (the plugin already has no scarab settings).
- Catarina always drops an Allflame Ember of Propagation, Kulemak or the Ethereal. Every other Allflame is a specific member's safehouse reward.
- Intervention: all members reward a trapped stash with a specific class of Unique items (higher rank = more and rarer items).
- Non-Intervention safehouses that gave scarab chests now give chests with a specific Allflame Ember (Elreon in Transportation too).
- Rin: Research = trapped stash of Threads; Fortification = chests of Threads; Intervention = Unique maps ("Swiping Unique Maps").
- Riker Fortification = chests of Ancient Orbs. Vorici Research = trapped stashes with quality Skill Gems that have a Soul Core applied.
- Veiled items are removed from non-Betrayal content. New atlas tree (the post's commenters: no intelligence for Execute, 6% chance of a Syndicate anomaly on Interrogate).

## Rewards per member (3.30 data as quoted in the thread)

| Division | Members and reward |
|---|---|
| Transportation (Allflame Embers) | Elreon Divine, Korell Cloister, Leo Multitude (+300% maps), Jorgin Resplendence, Vagan Wildwood, Cameria Toads; It That Fled = foulborn chest + Mageblood card jackpot (jewels, shields, Romira's); Gravicius = low-yield, fun |
| Fortification | Janus Allflame of the Gilded (gold), Haku Allflame of Flesh; Rin Threads; Riker Ancient Orbs |
| Research | Janus (Cadiro shop, no timer), Riker (Ancient Orb spam on an item class), Vorici (Soul Core skill gems), Tora (random gem + GCPs + Facetor's Lens + Vaal); the rest are timed crafting (essences etc.) |
| Intervention (Unique class) | Janus rings, It That Fled amulets, Jorgin belts, Rin maps, Hillock body armours, Leo helmets, Aisling gloves, Korell boots, Guff shields, Cameria axes, Haku maces, Vagan swords, Vorici daggers/claws, Tora bows/quivers, Elreon staves, Gravicius sceptres/wands, Riker mixed |

## Mechanics confirmed in the comments

- 2-2-5-5 in any permutation (2255, 2552, 5522) is valid; the two "2" divisions can be any two.
- Research and Intervention always appear in maps (two of the three safehouse types); Transportation and Fortification appear about 50/50.
- A division whose intel bar is 100% does not appear in maps while you do not run it. Filling a bar without running it blocks that division (the plugin's old Farm/Filler logic).
- A commenter reports members getting shuffled after each safehouse run; another says their 2-5-5-2 still works. Unresolved.
- Several commenters think rewards got worse and "just rush Catarina" is the viable play; others still see Allflames, veiled Exalts, fractured veiled items and Intervention uniques as worth it.

## Decision for the plugin (user choice, 2026-11-18)

"Run everything, optimise currency from rewards." Consequences implemented:

1. **Run policy:** the per-division Run setting is authoritative and defaults to all four divisions on (no blocking). Intel into a run division scores 6 per useful point (see TECHNICAL_REFERENCE 2.4).
2. **Roles:** all four divisions are placement targets ("Farm" role) with two preferred members each; any other member in a division counts as unwanted (can be removed by Interrogate/Execute logic).
3. **Recommended default members** (button "Apply recommended 3.30 setup", also the code defaults):

| Division | Members | Why |
|---|---|---|
| Transportation | It That Fled, Gravicius | thread: ITF is the top candidate (foulborn chest, Mageblood card, jewels), pair with Gravicius for the gamble |
| Research | Vorici, Janus | thread: "in research it's definitely Vorici and one of Janus/Riker (prob Janus)": Soul Core gems and Cadiro's shop |
| Fortification | Riker, Rin | confirmed rewards: Ancient Orbs (Riker) and Threads (Rin); Janus (gold) and Haku (Allflame of Flesh, called trash in trade) were passed over |
| Intervention | Jorgin, Hillock | Unique belts (Mageblood jackpot chance, named in the thread) and Unique body armours; weakest data, first candidates to change once prices are known |

The picks for Fortification and Intervention are the least supported by the thread; adjust in the settings after seeing real prices (member lists are free to edit). Possible swaps: Intervention Aisling/Korell for cheaper classes, Fortification Janus for gold in SSF.

## Open questions

- Which Allflame Embers are actually valuable (decides who stays in Transportation).
- Threads and Intervention unique drop rates (decides Rin and the Intervention pair).
- Whether safehouse runs shuffle members (decides how much placement is worth).
- Plugin side: the history files (`config/BetrayalHelper/history`) will show how often each recommendation is followed and what changes on the board.
