# Eclipse All Maps — development notes

How the mod is built and how it works. The player-facing description is in the
[README](../README.md).

## Environment this is built against

| Thing | Version |
| --- | --- |
| Game | Unity 2021.3.45f2, DLC1-4 installed (same build as KarmaClimb) |
| `RiskOfRain2.GameLibs` | `1.5.0-r.1` |
| `MMHOOK.RoR2` | `2026.10.8` |
| BepInEx | 5.4.21 |
| Risk Of Options | 2.8.6: a Thunderstore dependency, but only a soft dependency in code |
| Dev profile | r2modman profile `EclipseMapsRun` |

## Building

```
dotnet build EclipseAllMaps/EclipseAllMaps.csproj
```

The build drops `EclipseAllMaps.dll` into
`%APPDATA%\r2modmanPlus-local\RiskOfRain2\profiles\EclipseMapsRun\BepInEx\plugins\EclipseAllMaps`.
The copy is skipped if the profile folder is missing. The profile needs BepInExPack, HookGenPatcher
and Risk Of Options installed through r2modman.

`dotnet build EclipseAllMaps.sln -c Release` also writes `artifacts/EclipseAllMaps-<version>.zip`, the
Thunderstore package. It fails if `Version` in the csproj, `version_number` in the manifest and
`PluginVersion` in the code disagree. `Thunderstore/icon.png` (256×256) is the four added maps in a 2×2
grid, stages 2-5 in reading order, cropped from the Risk of Rain 2 Wiki's 480×288 screenshots, under an
eclipse ring.

## How stages are picked

Read from the installed game's code and Addressables data (SceneDefs and SceneCollections):

- Stage 1 comes from `Run.startingSceneGroup` (`sgStage1`) in `Run.Start`.
- Every later stage is picked as soon as the current map's teleporter spawns
  (`TeleporterInteraction.Start` → `Run.PickNextStageSceneFromCurrentSceneDestinations`), from the
  current map's `destinationsGroup` (`sgStageN`), or its `loopedDestinationsGroup` (`loopSgStageN`) once
  `stageClearCount >= 4`.
- Both go through `Run.PickNextStageScene(WeightedSelection<SceneDef>)`. That is the one hook:
  [EclipseStagePool.cs](../EclipseAllMaps/EclipseStagePool.cs) adds the maps for the stage being
  picked, only when `Run.instance is EclipseRun`. The SceneCollection assets are never edited (and note
  that vanilla `SceneCollection.SetSceneEntries` copies nothing: its loop runs `i < 0`).

The vanilla pools, as shipped:

| Stage | `sgStageN` (weight) |
| --- | --- |
| 1 | blackbeach, blackbeach2, golemplains, golemplains2 (0.5 each); snowyforest, lakes, village (1); lakesnight, villagenight (0) |
| 2 | goolake, foggyswamp, ancientloft, nest |
| 3 | frozenwall, wispgraveyard, sulfurpools, ironalluvium, mire, fungal |
| 4 | dampcavesimple, shipgraveyard, rootjungle, repurposedcrater, sanctum |
| 5 | skymeadow, helminthroost |

The looped pools swap in the night or looped variants (lakesnight, villagenight, ironalluvium2).

## The added maps

| Map | Scene | `stageOrder` | Joins | DLC | Notes |
| --- | --- | --- | --- | --- | --- |
| Reformed Altar | `lemuriantemple` | 2 | 2 | 2 | Destinations `sgStage3` |
| Treeborn Colony | `habitat` | 3 | 3 | 2 | `loopedSceneDef` = `habitatfall` (Golden Dieback); destinations `sgStage4` |
| Conduit Canyon | `conduitcanyon` | 4 | 4 | 3 | `filterOutOfBazaar`; destinations `sgStage5`, but its scene-placed teleporter exits to `solutionalhaunt` |
| Void Locus | `voidstage` | 99 | 5 | 1 | No teleporter, no destinations: exits only to `voidraid` (Planetarium) through Deep Void Signals |
| Damp Caverns | `fungal` | 3 | — | 4 | Already in `sgStage3` and `loopSgStage3` |
| Tenebrous Quagmire | `mire` | 3 | — | 4 | Already in `sgStage3` and `loopSgStage3` |

The list lives in [AddedMaps.cs](../EclipseAllMaps/AddedMaps.cs). A map joins its own `stageOrder`
unless it gives an explicit stage, so a game patch that moves a map moves it here too.

Details worth knowing:

- **Settings.** All bound in [ModSettings.cs](../EclipseAllMaps/ModSettings.cs), which also holds the Risk
  Of Options labels and menu order (a checkbox per bool; `AddedMapChance` is a 0.1-5 step slider from its
  `AcceptableValueRange`). Sections: General (`AddAllMapsToEclipse`, `AlsoInNormalRuns`, `AddedMapChance`,
  `BalanceCredits`), Stage 1-5, Void Locus, Conduit Canyon, Colossus Path, Bosses, Debug. Each feature
  class checks its own entry next to `EclipseStagePool.IsActive` / `AddedMaps.InPlay`, which go through
  `ModSettings.AppliesTo`: an `EclipseRun`, or with `AlsoInNormalRuns` a plain `Run` (the classic mode;
  Simulacrum and Prismatic Trials are subclasses and never count). `AddAllMapsToEclipse` switches the mod. Every map in the vanilla pools plus
  the added ones has a checkbox under `Stage 1`-`Stage 5`, keyed by scene name (BepInEx keys can't hold
  the apostrophes in "Siren's Call"), with "Name (scene)" as its Risk Of Options label; the list is
  [StageMaps.cs](../EclipseAllMaps/StageMaps.cs), and a map missing from it stays on. `PickNextStageScene`
  drops switched-off maps from the pool and the Bazaar seers skip them; if nothing rollable is left
  (weight 0 entries count as nothing) the stage keeps its vanilla pool. An added map switched off also
  gets none of its changes. Golden Dieback has no checkbox of its own: it is Treeborn Colony after a
  loop. All are read at pick time, so a change applies from the next pick. Risk Of Options shows them under Settings → Mod
  Options. The manifest lists it so r2modman installs it, but
  [RiskOfOptionsCompat.cs](../EclipseAllMaps/RiskOfOptionsCompat.cs) only touches it when present, so a
  manual install without it still loads.
- **Credits.** [AddedMapCredits.cs](../EclipseAllMaps/AddedMapCredits.cs) sets an added map's
  `ClassicStageInfo` credits to its stage's median just before `SceneDirector.Start` reads them (see
  the table below). Only in Eclipse with the mod and that map switched on; Golden Dieback counts as
  Treeborn Colony.
- **Progression.** [AddedMapProgression.cs](../EclipseAllMaps/AddedMapProgression.cs) makes both odd
  exits normal (only in Eclipse with the mod and that map switched on):
  - Void Locus: `VoidStageMissionController.batteryCount` is set to 0 before its `Start`, so no Deep
    Void Signals spawn. That also means no void fog (only `DeepVoidPortalBattery.Charging` requests it)
    and no Planetarium portal. `SceneDirector.teleporterSpawnCard` is set to `iscLunarTeleporter`
    (address `RoR2/Base/Teleporters/iscLunarTeleporter.asset`), the Primordial Teleporter Sky Meadow
    and Helminth Hatchery use; the ground graph has 261 `TeleporterOK` nodes to place it on. Its prongs
    start `IdleToActive` → `Active` (aligned with the moon: `useRunNextStageScene = false`, exit to
    `moon2`); "Shift Destination" goes `Idle` (`useRunNextStageScene = true`). Void Locus has no
    destinations, so `PickNextStageSceneFromCurrentSceneDestinations` uses Sky Meadow's
    (`sgStage1`, or `loopSgStage1` after a loop). The boss comes from the stage's champions (see
    Monsters below). With none at all, as under Void Locus's rare Lunar family event after a loop, the
    teleporter just finishes once charged (`BossGroup` only enables when a member joins).
  - Conduit Canyon: its teleporter is placed in the scene with `useRunNextStageScene = false` and
    `destinationScene = solutionalhaunt`. After `TeleporterInteraction.Start` (which picks the next
    stage) `useRunNextStageScene` is set to true, so it goes to the stage 5 pick.
- **Weights.** Added maps get weight 1, like every stage 2-5 entry. The `OnlyAddedMaps` debug option
  clears the rest of the pool instead.
- **Looping.** Treeborn Colony becomes Golden Dieback under vanilla's own test from
  `SceneExitController.GetLoopedStageOrDefault` (`loopClearCount > 0 && stageClearCount > 5`).
- **DLC.** A map whose expansion is off for the run is skipped (`Run.CanPickStage`).
- **Eclipse's event flags.** `EclipseRun.Start` sets `NoVoidStage`, which only bans the teleporter's
  void portal (`PortalSpawner.bannedEventFlag`). Conduit Canyon still sets `HardwarePortalTaken` on
  load (`ConduitCanyonController.Awake`), which only stops Computational Exchange offering the
  Alloyed-path portal later.
- **Bazaar seers.** `BazaarController.SetUpSeerStations` offers every map sharing
  `nextStageScene.stageOrder`. With Void Locus (99) up next that would include the Planetarium and
  A Moment, Whole, so for that one call each added map's `stageOrder` is set to the stage it joins and
  then restored. Only maps switched on are refiled (so a switched-off Void Locus is never a stage 5 seer
  option), plus whichever added map is already the next stage, even if its checkbox went off since.
  A seer sets the exit's `destinationScene` directly, bypassing `PickNextStageScene`, so with
  `OnlyAddedMaps` the same call also sets `filterOutOfBazaar` on every other map of that stage and
  empties `seerSceneOverrides` (the Bazaar's chance-based extra seer scenes). Seers then show an added
  map, or are disabled as in vanilla when none qualifies (Conduit Canyon is filtered out of the
  Bazaar anyway).
- **Other exits.** The Bazaar, Gilded Coast and other hidden realms return to `Run.nextStageScene`,
  so they keep the pick. The green Colossus-path portal (`PortalColossus`, `iscColossusPortal`) is an
  `isAlternatePath` exit with tier destinations lemuriantemple / habitat / meridian; see Colossus path
  below for where it still opens.

## Colossus path

Every vanilla teleporter (`Teleporter1`, `LunarTeleporter Variant`) has a `PortalSpawner` for
`iscColossusPortal` with `spawnChance` 1 and `validStages` habitat, lemuriantemple, habitatfall (banned by
`FalseSonBossComplete`), so Reformed Altar and Treeborn Colony always open it, leading to the next path
map and then Prime Meridian. Gilded Coast's exit and the Bazaar's geode listener can open one too (its
tier destination follows the next stage's number); those are left alone.

[AddedMapProgression.cs](../EclipseAllMaps/AddedMapProgression.cs) sets that spawner's `spawnChance` to
-1 before its `Start` (which rolls it and lights the preview orb) on the added maps in play. To keep
Prime Meridian reachable it mirrors the void route: Commencement (`moon2`) spawns `FrogInteractable`
at `EscapeSequenceObjects/FrogSpawner` once Mithrix falls, and its `FrogController` opens an
`iscDeepVoidPortal` (Planetarium) on the 10th pet (`maxPets`). Hooking `FrogController.Pet`, the 10th
pet also spawns `iscColossusPortal` within 10-40 m of the frog, pinned to `destinationScene = meridian`
(it would land there anyway: from stageOrder 6 no tier matches and it falls back to `destinationScene`).
Not without DLC2, nor once False Son is beaten. After False Son, Prime Meridian's Shrine of Rebirth
offers the Rebirth ending (`RebirthEndingDef`, `isWin` 1, so it counts for Eclipse) or a portal on to
Helminth Hatchery; `PM DestinationPortal` goes to `Run.nextStageScene`.

## Alloy Hunter

Conduit Canyon's teleporter has a `VultureFightOverride` (`triggerOnStart`): it forces the teleporter
boss to a Solus Control Unit (`ForcedBossFight` = `cscRoboBallBoss`, singular, scaled), and at 75% health
summons `VultureHunterMaster`, the Alloy Hunter, which kills it and takes its place. Its own boss deck
(`dccsConduitcanyonBosses`: Solus Amalgamator) never gets used. [AlloyHunterBoss.cs](../EclipseAllMaps/AlloyHunterBoss.cs)
clears `triggerOnStart` on Conduit Canyon in play, so that deck picks one boss. From stage 4
(`stageClearCount` >= 3) on any map, `CombatDirector.SetNextSpawnAsBoss` on the teleporter's boss
director first rolls the Alloy Hunter (`RoR2/DLC3/VultureHunter/cscVultureHunter.asset`: 800 credits, air,
allowed as boss) at 1 in (bosses + 1), counting bosses as vanilla does (available champions not
`forbiddenAsBoss`), and makes it the boss card with `OverrideNextBossCard`. Needs DLC3
(`DirectorCard.IsAvailable`).

Balance, checked against the other bosses (body stats; damage coefficients from the entity state
configurations):

| Boss | Cost | Health (+/level) | Armor | Health × (1 + armor/100) | Damage | Hardest hits |
| --- | --- | --- | --- | --- | --- | --- |
| Solus Control Unit | 800 | 2500 (+750) | 20 | 3000 | 15 | eye blast 1× |
| Magma Worm | 800 | 2400 (+720) | 15 | 2760 | 10 | |
| Imp Overlord | 800 | 2800 (+840) | 20 | 3360 | 16 | blink 4×, ground pound 2× |
| Void Devastator | 800 | 2800 (+840) | 20 | 3360 | 12 | |
| Solus Amalgamator | 800 | 3000 (+900) | 20 | 3600 | 15 | flamethrower turret 4.2×, shock armor 4×, artillery 3× |
| Alloy Hunter | 800 | 3600 (+1080) | 0 | 3600 | 15 | bombing run 5×, calldown 3×, bombing laser 2.2× |
| Grandparent | 1150 | 3625 (+1088) | 20 | 4350 | 26 | |

It matches the Solus Amalgamator, with a slightly harder single hit. Its body starts in
`GenericCharacterMain` (the murder entrance is only set by the override), so a director spawn is a plain
boss. Vanilla's Conduit Canyon summons it with `ScaleDifficultyAsBoss()` (BoostHp worth +difficulty/2.5
of its health, √players, plus BoostDamage); through the boss director it is an ordinary 800-credit boss
(not `singularSpawn`), so as a pool boss it is weaker than vanilla's Conduit Canyon one. Reformed Altar and Treeborn Colony were already seer options in vanilla; Conduit
  Canyon is filtered out of the Bazaar.
- **`run_scene_override`** (cheat convar) keeps forcing the first stage.
- **Multiplayer.** Picking is server-only and clients load the scene by name, so only the host needs the
  mod. No `NetworkCompatibility` is declared.

## Director credits

`SceneDirector.Start` spends `ClassicStageInfo.sceneDirectorInteractibleCredits` × (0.5 + 0.5 × players)
(plus `bonusInteractibleCreditObjects` whose object is active) on interactables, and
`sceneDirectorMonsterCredits` × difficulty coefficient on starting monsters. Nothing else changes them per
map (`ClassicStageInfoVariation` can swap them, but none of these scenes has one; Sacrifice's ×0.5 is
global). Base values as shipped (interactable / monster), read from each scene's `ClassicStageInfo`:

| Stage | Vanilla maps | Median | Added map: vanilla → now |
| --- | --- | --- | --- |
| 1 | blackbeach, blackbeach2, golemplains, golemplains2 220 / 100; snowyforest 300 / 110; lakes 280 / 120; lakesnight, village 280 / 100; villagenight 310 / 100 | 280 / 100 | — |
| 2 | goolake 220 / 100; foggyswamp, ancientloft, nest 280 / 100 | 280 / 100 | lemuriantemple 300 / 120 → 280 / 100 |
| 3 | frozenwall, wispgraveyard, sulfurpools, ironalluvium, ironalluvium2 280 / 100; fungal 310 / 100; mire 340 / 100 | 280 / 100 | habitat, habitatfall 280 / 100 (unchanged) |
| 4 | dampcavesimple (+160 while `CaveStateOn` is active), shipgraveyard, rootjungle, repurposedcrater, sanctum 400 / 230 | 400 / 230 | conduitcanyon 400 / 220 → 400 / 230 |
| 5 | skymeadow 520 / 230; helminthroost 570 / 230 | 545 / 230 | voidstage 220 / 230 → 545 / 230 |

The medians are hard-coded in `AddedMapCredits.MedianCredits`; a game patch that changes the vanilla
values needs them re-read.

## Monsters

Each map's monster pool (`ClassicStageInfo.monsterDccsPool`, a `DccsPool` of DCCSs) as shipped, with
spawn card costs; a vanilla teleporter's void portal needs `minStagesCleared` 6, so vanilla never shows
Void Locus before stage 7.

| Category | Stage 4 maps (cost) | Conduit Canyon | Stage 5: Sky Meadow / Helminth Hatchery | Void Locus |
| --- | --- | --- | --- | --- |
| Champions | 600-800, Overloading Worm 4000 | Imp Overlord 800, Wandering Vagrant 600; Overloading Worm, Solus Amalgamator after 5 stages | 800-1150, Overloading Worm 4000 | Void Devastator 800 |
| Minibosses | 40-200; Void Reaver 300 after 4 stages | Clay Templar 125, `IronHauler` 200, `DefectiveUnit` 40, Parent 100 | 40-200; Void Reaver 300 after 5 stages (Sky Meadow) | Void Jailer 450 |
| Basic | 8-40 | `WorkerUnit` 11, `ExtractorUnit` 25, `Tanker` 18, Alloy Vulture 28 | 10-60 | Void Reaver 300, Void Barnacle 50 |
| Special | Scavenger after 5 stages | Scavenger after 5 stages | Scavenger after 5 stages | — |

Void Locus's pool (`dccsVoidStageMonsters`) is the same as `dccsVoidFamily`, the Void Invasion family
event any stage 4-5 map rolls 2% of the time. Conduit Canyon is in line with stage 4 and is left alone.

[AddedMapMonsters.cs](../EclipseAllMaps/AddedMapMonsters.cs) hooks `ClassicStageInfo.RebuildCards`
(called from its `Start`) and, on Void Locus while `stageClearCount < 6`, passes a forced monster
selection: Sky Meadow's Standard category (`RoR2/Base/skymeadow/dpSkyMeadowMonsters.asset`) blended by
`DCCSBlender.GetBlendedDCCS` exactly as on Sky Meadow (so its DLC1-3 lists join when those are on), minus
its gated Void Reaver card, plus the Void Devastator (Champions), Void Reaver (Minibosses) and Void
Barnacle (Basic Monsters), each weighted as much as Sky Meadow's whole category. Artifacts still apply
to a forced selection. From 6 cleared stages (Void Locus on a later loop) the vanilla pool is used.

## Stage 4 bonus

Every vanilla stage 4 map hands out one guaranteed extra, placed in the scene rather than by the
director:

| Map | Bonus |
| --- | --- |
| Abyssal Depths (`dampcavesimple`) | Legendary Chest (`GoldChest`) in "Newt Statues and Preplaced Chests" |
| Sundered Grove (`rootjungle`) | Legendary Chest at 1 of 5 spots ("GROUP: Large Treasure Chests") |
| Repurposed Crater (`repurposedcrater`) | Legendary Chest at 1 of 3 spots ("GROUP_ GoldChest") |
| Siren's Call (`shipgraveyard`) | Bonus boss: `SuperRoboBallEncounter` (Alloy Worship Unit) |
| Lurid Basilica (`sanctum`) | Bonus boss: `SanctumBossEncounter` |
| Conduit Canyon (`conduitcanyon`) | Legendary Chest in a secret container, behind a Sentry Terminal |

Conduit Canyon needs nothing added. `Director/PowerKeySpawner` (`PowerOrbKeySpawner`) drops a Sentry Key
(`PowerPyramid` item) from every Collective elite killed, and `ConduitCanyonElitePromoterDisabler` keeps
promoting spawns to Collective elites, while unfinished Sentry Terminals (`PowerPedestal`) outnumber the
keys players hold. The teleporter's 3 terminals ("GROUP : Boss and Portal") unlock it (`set_locked`
false); "GROUP: Secrets" has two `UESLootContainer1`s with one terminal each, behind a `MinorShieldWall`:
one holds a Legendary Chest (`dtGoldChest`, 400 gold scaled), the other an equipment triple shop. Since
keys keep dropping until every terminal is filled, the chest is always reachable.

## Interactables

`ClassicStageInfo.interactableDccsPool`, blended in `RebuildCards` right after the monsters (its DLC
lists limited to the DLCs the monsters drew from, `expansionsInEffect`). Reformed Altar, Treeborn
Colony, Golden Dieback and Conduit Canyon have ordinary stage lists (Chests 45, Barrels 10, Shrines 7-10,
Drones 3-14, Misc 7, Rare 0.4, Duplicator 8, plus small DLC categories), like Sky Meadow, Helminth
Hatchery or Abandoned Aqueduct, and are left alone.

Void Locus's list (`dccsVoidStageInteractables`) is one category, `VoidStuff` (weight 45): Void Cradle
(`VoidChest`, 25), Void Potential (`VoidTriple`, 40), Stalk (`VoidCoinBarrel`, 15). All are paid in
health, so nothing there costs gold, and with stage 5 credits (545) it would place about 2.5 times its
usual number of them. [AddedMapInteractables.cs](../EclipseAllMaps/AddedMapInteractables.cs), after
`RebuildCards`, replaces `interactableCategories` on every Void Locus visit with Sky Meadow's list
(`RoR2/Base/skymeadow/dpSkyMeadowInteractables.asset`, blended by `DCCSBlender.GetBlendedDCCS` under the
same DLC limit vanilla passes) combined with Void Locus's own list by `DCCSBlender.BlendSpecificDCCS`.
That keeps every category at its own weight, so `VoidStuff` (45) is as likely as Chests (45): about a
third of the category picks, out of roughly 145 total weight.
- An Eclipse-menu toggle. The switches are config entries, shown as checkboxes by Risk Of Options.
