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
  `BalanceCredits`), Stage 1-5, Void Locus, Conduit Canyon, Colossus Path, Alloyed Path, Bosses, Debug. Each feature
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
- **Other exits.** The Bazaar, Gilded Coast, Computational Exchange and other hidden realms return to
  `Run.nextStageScene`, so they keep the pick. The portals down the two DLC paths are closed; see
  Colossus path and Alloyed path below.
- **`run_scene_override`** (cheat convar) keeps forcing the first stage.
- **Multiplayer.** Picking is server-only and clients load the scene by name, so only the host needs the
  mod. Every hook acts on the server only. No `NetworkCompatibility` is declared.

## Colossus path

Every vanilla teleporter (`Teleporter1`, `LunarTeleporter Variant`) has a `PortalSpawner` for
`iscColossusPortal` with `spawnChance` 1 and `validStages` habitat, lemuriantemple, habitatfall (banned by
`FalseSonBossComplete`), so Reformed Altar and Treeborn Colony always open it. The portal
(`PortalColossus`) is an `isAlternatePath` exit whose destination follows the next stage's number: tier 2
lemuriantemple, 3 habitat, 4 meridian, otherwise its own `destinationScene` (meridian). The other sources,
from a scan of every scene:

- the Halcyon Shrine's own spawner (below);
- the Bazaar's `Mini Geodes/Geode Listener`, fired by `MiniGeodeTracker.Listening` once three geodes break
  (banned by `StormPortalTaken`);
- Gilded Coast's `EntityStates.Missions.Goldshores.Exit`, which spawns `iscColossusPortal` itself next to
  the gold exit when its private `IsValidStormTier` holds (DLC2, next stage on tier 2-4);
- Solutional Haunt's treasure-room teleporter, which carries the teleporter spawner but is never on a
  valid stage.

[AlternatePathPortals.cs](../EclipseAllMaps/AlternatePathPortals.cs) (Colossus Path / No green portals)
sets a Colossus spawner's `spawnChance` to -1 before `PortalSpawner.Start` (which rolls it and lights the
preview orb): a teleporter's only on the added maps in play, any other wherever the mod is on. Hooking
`Exit.IsValidStormTier` to return false closes Gilded Coast's.

To keep the paths' endings reachable the mod mirrors the void route. Commencement (`moon2`) spawns
`FrogInteractable` at `EscapeSequenceObjects/FrogSpawner` once Mithrix falls, and `FrogController.Pet`
fires its `PortalSpawner` (`iscDeepVoidPortal`, the Planetarium) once `petCount >= maxPets` (10). On the
pet that reaches it (`petCount == maxPets`, so once) the mod also spawns, 10-40 m from the frog:

- `iscColossusPortal` pinned to `destinationScene = meridian` (it would land there anyway: from stageOrder
  6 no tier matches). Not without DLC2, nor once False Son is beaten. After False Son, Prime Meridian's
  Shrine of Rebirth offers the Rebirth ending (`RebirthEndingDef`, `isWin` 1, so it counts for Eclipse)
  or a portal on to Helminth Hatchery; `PM DestinationPortal` goes to `Run.nextStageScene`.
- Neural Sanctum's portal; see Alloyed path.

## Halcyon Shrine

The Halcyon Shrine (`ShrineHalcyonite`, `HalcyoniteShrineInteractable`) carries `PortalSpawner`s fired by
`AttemptSpawnPortal` when its gold is in: `iscColossusPortal` on stage tiers 1-3 except the SotS maps
(banned by `StormPortalTaken`), and `iscGoldshoresPortal` on tiers 4-5, on the SotS maps' tiers 1-3, or
after the path was taken. The green one is closed with the others above. Vanilla lists the shrine under
"Storm Stuff" in every stage 1-5 map's DLC2 interactable list (Conduit Canyon and Void Locus have none):
stage 1 maps `iscShrineHalcyoniteTier1` (40 credits, weight 7, category 3; Disturbed Impact 15 / 9), later
maps `iscShrineHalcyonite` (50 credits, weight 1, category 1.5; Reformed Altar and Helminth Hatchery 10),
max 1 per stage. That DLC2 list only joins when the blender rolls it within
`contentInteractableSourceMixLimit`, so many stages lack it.
[HalcyonShrines.cs](../EclipseAllMaps/HalcyonShrines.cs) subscribes to
`SceneDirector.onGenerateInteractableCardSelection` (called with a copy of the stage's
`interactableCategories` right before the roll) and, when no Halcyon card is in it, adds the stage's one
with those weights to "Storm Stuff" (creating the category if needed). Needs DLC2
(`DirectorCard.IsAvailable`).

Its fight, from the prefab:

- Gold: 75 / 150 / 300 for the three tiers, through `Run.GetDifficultyScaledCost` (× difficulty^1.25, like
  chest prices), set in `Awake`.
- Monster waves: entering the no, low and mid quality states (`tierChangeMonsterCreditReduction` 0) each
  spends `monsterCredit` (140 × `difficultyCoefficient`, set in `Awake`) through `SpendAllCreditsOnMapSpawns`
  on its "ActivationWave" director: `dccsShrineHalcyoniteActivationMonsterWave` (Stone Golem, 40 credits),
  `eliteBias` 2.5, so elite golems (240 each) only while 600+ credits are left, 0.6 gold.
- Halcyonite: at max quality `DrainConditionMet` calls `CombatDirector.HalcyoniteShrineActivation` on its
  "Halcyonite" director with `cscHalcyonite` (200 credits) and level `goldDrained / 100`. The spawn
  (`isHalcyonShrineSpawn`) gets BoostHp for +60% health and BoostDamage for +25% damage per level (above 15
  the level becomes 15 + level / 200, so 15 in practice) and the Aurelionite affix; no elite tier (its ×6
  elite cost never fits the 200 credits).
- Reward: one item per player, a choice of 3 from `dtShrineHalcyoniteTier3` (white 0.65, green 0.3, red
  0.05), plus the portal.

Since the gold already scales with difficulty, the level cap is reached from stage 3 (full is about 1770
gold at difficulty 4): ×10 health and ×4.75 damage on top of its level, for the same reward as on stage 1
(level 3: ×2.8 / ×1.75). Solo in Eclipse at stage 4, 20 minutes (difficulty 6.1, monster level 16): each
wave is 860 credits, two elite and nine normal Stone Golems; the Halcyonite has about 49,500 health,
against 15,400 for an Imp Overlord. With Colossus Path / Weaker Halcyon Shrines from stage 4
(`stageClearCount` >= 3) the shrine's `monsterCredit` is halved after `Awake` (430: ten normal golems a
wave) and `HalcyoniteShrineActivation` gets min(level, 15) / 2, at most 7: ×5.2 health (about 25,700)
and ×2.75 damage.

## Alloyed path

The Alloyed Collective's path is Conduit Canyon (stage 4) → Solutional Haunt (`solutionalhaunt`, Solus
Wing; its treasure room's Primordial Teleporter exits to `moon2`) → Commencement. Its ending is Solus
Heart in Neural Sanctum (`solusweb`, "Hidden Realm: Neural Sanctum"), which vanilla reaches from
Computational Exchange: handing the Solus vendor a Master Core (`SolusVendorShrineController.HandleGivenItem`)
runs `SolusWebPortalSequence` and spawns `SolusWebPortal`. Accepting Solus Heart's offering
(`SolusHeartFinaleSequence.OfferingAccepted`) ends the run with `DecompileEnding` (`isWin` 1); killing it
opens an exit to `Run.nextStageScene` (and, outside Eclipse, a void portal). `SolusWebMissionController`
needs nothing from the quest (only the `NoSolusHeartOffer` flag, which nothing sets, skips the offering),
and the scene has no event-flag responses.

Its portals off the rotation come from the access codes. `AccessCodesMissionController` (on Pretender's
Precipice, Iron Alluvium / Iron Auroras and Repurposed Crater, and in the "Access Codes Portals" prefab)
places code nodes on points of interest; solved in order while the teleporter is idle, they fire
`SolusFight.TriggerServer`. That forces a companion boss (`cscSolusAmalgamator` on `companionBoss`), adds
a boss reward (`TeleporterInteraction.ActivateAccessCodes`), and arms one of three `PortalSpawner`s
(`spawnChance` 0, `registerSelfToPortalRemotelyOnActivation`), lighting the teleporter's
`CEPortalIndicator`:

- `iscSolusShopPortal` (Computational Exchange) after `SolusWingBeaten`;
- else `iscHardwareProgPortal` (Conduit Canyon) when `stageClearCountInCurrentLoop == 2`;
- else `iscHardwareProgPortal_Haunt` (Solutional Haunt).

`SolusFight.TriggerSirensCallPortal` (Siren's Call) arms Computational Exchange or Solutional Haunt the
same way. Each goes through the `PortalSpawner.WillSpawn` setter into `OnWillSpawnUpdated` (MMHOOK has no
hooks for property setters). With Alloyed Path / No Solutional Haunt portals,
[AlternatePathPortals.cs](../EclipseAllMaps/AlternatePathPortals.cs) passes false there for Solutional
Haunt's, and for Conduit Canyon's when Conduit Canyon is switched off (on it is a stage 4 of the rotation).
The access-code fight and its reward stay. Computational Exchange returns to `Run.nextStageScene`
(`CompExchangePortal`, `useRunNextStageScene`), and its `SpawnHardwarePortal` has no position set in the
scene, so it never spawns.

With Alloyed Path / Frog portal to Neural Sanctum, the frog's 10th pet also spawns `SolusWebPortal`
(`RoR2/DLC3/SolusWebPortal.prefab`, "Decrypted Portal", exit to `solusweb`) on a copy of
`iscColossusPortal` for its placement rules. Not without DLC3 (`CanPickStage(solusweb)`), nor once
`SolusHeartBeaten`.

## Teleporter bosses

Conduit Canyon's teleporter has a `VultureFightOverride` (`triggerOnStart`): it forces the teleporter
boss to a Solus Control Unit (`ForcedBossFight` = `cscRoboBallBoss`, singular and scaled unless a Shrine
of the Mountain is active), and at 75% health summons `VultureHunterMaster`, the Alloy Hunter, once
(`singularKiller`), with `ScaleDifficultyAsBoss()` (health × (1 + difficulty / 2.5) × √players, damage ×
(1 + difficulty / 30)); it kills the unit and takes its place. Its boss director carries its own deck
(`dccsConduitcanyonBosses`: Solus Amalgamator, weight 3) and starts with 800 credits; every other
teleporter's starts with 0.

[TeleporterBosses.cs](../EclipseAllMaps/TeleporterBosses.cs) clears `triggerOnStart` on Conduit Canyon in
play when Conduit Canyon / Robot bosses or Bosses / `AlloyHunterFromStageFour` (the key kept from when it
only meant the boss pool) is on, and zeroes those 800 credits, so the fight is bought like any stage 4
boss. It hooks `CombatDirector.SetNextSpawnAsBoss` on the teleporter's boss director, which the charging
state calls with the boss credits in: 600 × √difficulty × (1 + Mountain stacks + hex stacks). Unless a
boss is already forced, it picks:

- Robot bosses, on Conduit Canyon: Solus Control Unit, Alloy Worship Unit, Solus Amalgamator or Alloy
  Hunter, `rng.RangeInt(0, 4)`. The unit and the Amalgamator go in as ordinary boss cards
  (`OverrideNextBossCard`, not singular): as many as the credits buy, elite when they stretch to it.
- Alloy Hunter as a normal boss, from stage 4 (`stageClearCount` >= 3) on any map, Conduit Canyon too
  with Robot bosses off: the Alloy Hunter at 1 in (bosses + 1), counting bosses as vanilla does
  (available champions not `forbiddenAsBoss`). Not on a stage without any, such as Void Locus's Lunar
  family event, where it would be certain.

Neither under the Artifact of Kin, which keeps a stage to one monster type. Both need DLC3.

Robot bosses on stage 4 (body stats; damage coefficients from the entity state configurations):

| Boss | Cost | Health (+/level) | Armor | Health × (1 + armor/100) | Damage | Vanilla on stage 4 |
| --- | --- | --- | --- | --- | --- | --- |
| Solus Control Unit | 800 | 2500 (+750) | 20 | 3000 | 15 | Siren's Call, Repurposed Crater, Solus family event |
| Alloy Worship Unit | 0 (counted 800) | 2500 (+750) | 30 | 3250 | 15 | Siren's Call bonus boss, single and scaled |
| Solus Amalgamator | 800 | 3000 (+900) | 20 | 3600 | 15 | Conduit Canyon's deck, Solus family event |
| Alloy Hunter | 800 | 3600 (+1080) | 0 | 3600 | 15 | Conduit Canyon, single and scaled |
| Imp Overlord (for scale) | 800 | 2800 (+840) | 20 | 3360 | 16 | most stage 4 maps |

Hardest hits: Solus Control Unit eye blast 1×; Imp Overlord blink 4×, ground pound 2×; Solus Amalgamator
flamethrower turret 4.2×, shock armor 4×, artillery 3×; Alloy Hunter bombing run 5×, calldown 3×, bombing
laser 2.2×. All four sit within 20% of each other in effective health, around the other stage 4 bosses.
Left out: Xi Construct (not `Mechanical`, not of the Solus line) and the scripted arena bosses Solus Wing
and Solus Heart (`cscSolusWing` costs 0, its Solus Wing body has 1000 base armor).

### One at a time

Vanilla never buys the Alloy Hunter or the Alloy Worship Unit with credits: the Hunter is only Conduit
Canyon's single summon (`dccsVultureFamily` holds it but is in no stage pool), the Alloy Worship Unit only
Siren's Call's `SuperRoboBallEncounter` (`grantUniqueBonusScaling`, so `ScaleDifficultyAsBoss`), and
`cscSuperRoboBallBoss` costs 0. Bought as an 800-credit boss, the Alloy Hunter came four or five at a time
late in a run: stage 5 at 35 minutes in Eclipse is difficulty ≈ 11, about 2000 boss credits, 4000 with a
Shrine of the Mountain, and five hunters' bombing runs (5×) at once.

[SoloBoss.cs](../EclipseAllMaps/SoloBoss.cs) spawns them from a copy of the card that costs one share:
credits / (1 + Mountain stacks + hex stacks), never below the usual cost (800; the Alloy Worship Unit
counts as the Solus Control Unit whose body it shares) or above all the credits. The director then spawns
one per share (at most 6 a wave, elite only from 6 shares, as for any card) and pays a share's gold and
experience for each. On `SpawnCard.onSpawnedServerGlobal`, which runs after the director's own elite and
multiplayer BoostHp, each gets BoostHp for health × share / usual cost, multiplied with those. At that
35-minute stage 5: one hunter with ×2.5 health, or two with a Mountain shrine, where vanilla's scaled
Conduit Canyon one has ×5.4. A fight keeps the total health its credits buy, in fewer bodies, with the
card's own damage. The Alloy Hunter's body starts in `GenericCharacterMain` (the murder entrance is only
set by the override), so a director spawn is a plain boss.

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
