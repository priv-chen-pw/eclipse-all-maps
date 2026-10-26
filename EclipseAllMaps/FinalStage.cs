using System.Collections.Generic;
using System.Linq;
using RoR2;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Networking;

namespace EclipseAllMaps
{
    /// <summary>
    /// What comes after stage 5.
    ///
    /// - Random final stage: a stage 5's Primordial Teleporter (Sky Meadow's, Helminth Hatchery's, Void Locus's)
    ///   exits to Commencement while aligned with the moon. As it spawns its exit is pointed at one of the final
    ///   stages instead: Commencement (Mithrix), the Planetarium (Voidling), Prime Meridian (False Son),
    ///   Solutional Haunt (Solus Wing) or Soulbound Threshold (Anima Harvester). Shift Destination still loops,
    ///   to the run's next stage. Whenever the teleporter aligns with the moon chat hints at the pick, the way the
    ///   Bazaar's seers do, and each final stage comes once before any comes back: one reached as the final stage
    ///   is marked with a run event flag, and the marks are cleared when every final stage has one.
    /// - End or loop: each final boss leaves a win and a way on to stage 1, side by side. Commencement's escape and
    ///   the Planetarium's exit end the run, so they get a Destination Portal to stage 1. Solutional Haunt's
    ///   Primordial Teleporter loops, and Prime Meridian's and Soulbound Threshold's portals lead to the next stage,
    ///   so they get an altar that ends the run (EndAltar): Solutional Haunt always (vanilla has no ending after
    ///   Solus Wing), the other two in Eclipse, which hides the Shrine of Rebirth and turns off the sacrifice ending.
    /// - Weaker Soulbound Threshold: as the final stage its monsters (challenge waves, the champions the
    ///   Harvester feeds on) have 60% health and both of the Anima Harvester's phases 80%.
    /// - Shrine of Descent keeps the teleporter: its 666% charge no longer shuts the teleporter or opens the
    ///   Ritual Portal to Soulbound Threshold, now a final stage.
    ///
    /// Secret routes stay: the frog's portal to the Planetarium, Solus Wing's Mainline Portal towards Solus Heart,
    /// and Soulbound Threshold's Veil of Memory to the Echo of Providence.
    /// </summary>
    internal static class FinalStage
    {
        private const string Commencement = "moon2";
        private const string SoulboundThreshold = "champrush";

        /// <summary>
        /// The final stages, in their checkboxes' order, each with the dream chat hints at it with, like a Bazaar
        /// seer's: Mithrix the King of Nothing, Voidling in its Planetarium, False Son the heir of the storm, Solus
        /// Wing's wings and eye, the Anima Harvester setting the scales right.
        /// </summary>
        private static readonly (string scene, string omen)[] FinalStages =
        {
            (Commencement, "You dream of a king, ruling over nothing."),
            ("voidraid", "You dream of stars, drowning in the dark."),
            ("meridian", "You dream of thunder, and a forsaken heir."),
            ("solutionalhaunt", "You dream of steel wings, and an unblinking eye."),
            (SoulboundThreshold, "You dream of scales, tipped askew."),
        };

        /// <summary>Prime Meridian's portal on after False Son; it leads to the run's next stage.</summary>
        private const string LoopPortalAddress = "RoR2/DLC2/iscDestinationPortal.asset";

        /// <summary>How far from a vanilla exit the mod's own one goes: clear of it, but beside it.</summary>
        private const float BesideMin = 10f;
        private const float BesideMax = 18f;

        /// <summary>Both of its phases' bodies, ReaperBossBody and ReaperBossMalachiteBody.</summary>
        private const string AnimaHarvesterBody = "ReaperBoss";

        /// <summary>Health in percent on Soulbound Threshold as the final stage, as stacks of PercentMaxHealth.</summary>
        private const int AnimaHarvesterHealthPercent = 80;
        private const int SoulboundThresholdMonsterHealthPercent = 60;

        private static SpawnCard loopPortalCard;

        /// <summary>The exit this stage's end altar begins, if it has one, and the ending it gives.</summary>
        private static SceneExitController endExit;
        private static GameEndingDef endExitEnding;

        /// <summary>The final stage reached last, which starts no new round of them. Only read once this run has
        /// reached every final stage, so after this run has set it.</summary>
        private static string lastVisited;

        internal static void Init()
        {
            On.RoR2.TeleporterInteraction.Start += (orig, self) =>
            {
                orig(self);

                SceneExitController exit = self.sceneExitController;
                if (NetworkServer.active && EclipseStagePool.IsActive(Run.instance)
                    && exit && exit.destinationScene && exit.destinationScene.cachedName == Commencement && OnStageFive())
                {
                    PickFinalStage(exit);
                }
            };

            // At the stage's start and after each Shift Destination back, right after "aligns with the moon".
            On.EntityStates.LunarTeleporter.IdleToActive.OnExit += (orig, self) =>
            {
                orig(self);

                SceneDef destination = self.teleporterInteraction ? self.teleporterInteraction.sceneExitController.destinationScene : null;
                if (NetworkServer.active && destination && EclipseStagePool.IsActive(Run.instance) && OnStageFive())
                {
                    foreach ((string scene, string omen) in FinalStages)
                    {
                        if (scene == destination.cachedName)
                        {
                            Chat.SendBroadcastChat(new Chat.SimpleChatMessage { baseToken = $"<style=cWorldEvent>{omen}</style>" });
                        }
                    }
                }
            };

            Stage.onServerStageBegin += stage => MarkVisited();

            // Mithrix is down: the escape begins and the frog shows up in the arena.
            On.RoR2.EscapeSequenceController.BeginEscapeSequence += (orig, self) =>
            {
                orig(self);

                if (NetworkServer.active && EclipseStagePool.IsActive(Run.instance))
                {
                    Transform frog = self.transform.Find("EscapeSequenceObjects/FrogSpawner");
                    OpenLoopPortal((frog ? frog : self.transform).position);
                }
            };

            // Voidling is down; the portal this opens ends the run.
            On.RoR2.VoidRaidGauntletController.SpawnOutroPortal += (orig, self) =>
            {
                orig(self);

                if (NetworkServer.active && EclipseStagePool.IsActive(Run.instance) && self.currentDonut?.returnPoint)
                {
                    OpenLoopPortal(ExitPosition(exit => exit.destinationScene && exit.destinationScene.cachedName == "voidoutro",
                        self.currentDonut.returnPoint.position));
                }
            };

            // False Son is down. In Eclipse the Shrine of Rebirth stays hidden and only its portal on opens.
            On.EntityStates.ShrineRebirth.RevealRebirthShriine.OnEnter += (orig, self) =>
            {
                orig(self);

                if (NetworkServer.active && self.isEclipse && EclipseStagePool.IsActive(Run.instance) && IsFinalStage())
                {
                    // Its Destination Portal spawns at portalObject.
                    GameObject portalSpot = self._shrineController.portalObject;
                    OpenEndAltar((portalSpot ? portalSpot.transform : self.transform).position, DLC2Content.GameEndings.RebirthEndingDef,
                        "The storm falls silent..");
                }
            };

            // The Anima Harvester is down and the ritual complete: the Mortal Portal to the next stage opens. Eclipse
            // turns off the sacrifice ending, the same test as here.
            On.RoR2.ChampRushMissionController.CompleteReaperFight += (orig, self) =>
            {
                orig(self);

                if (NetworkServer.active && self.reaperSpawned && Run.instance.selectedDifficulty >= DifficultyIndex.Eclipse1
                    && EclipseStagePool.IsActive(Run.instance) && IsFinalStage())
                {
                    OpenEndAltar(ExitPosition(exit => exit.name.StartsWith("ChampRushExitPortal"), self.transform.position),
                        DLC4Content.GameEndings.CRSacrificeEndingGood, "The scales are balanced..");
                }
            };

            // Solus Wing is down and has crashed: its eye opens the Mainline Portal. Its arena also has the treasure
            // room's Primordial Teleporter, but no ending; the Alloyed Collective's is Solus Heart's offering.
            On.EntityStates.SolusWing.PostFight.SpawnEyePortal.OnEnter += (orig, self) =>
            {
                orig(self);

                if (NetworkServer.active && EclipseStagePool.IsActive(Run.instance) && IsFinalStage())
                {
                    OpenEndAltar((self.exitPortal ? self.exitPortal.transform : self.transform).position, DLC3Content.GameEndings.DecompileEnding,
                        "Connection terminated..");
                }
            };

            // The run ends as soon as a player uses the altar, and its exit still teleports everyone out: the Rebirth
            // ending waits for that before fading out. Once the game is over the exit loads no stage.
            On.RoR2.SceneExitController.Begin += (orig, self) =>
            {
                if (NetworkServer.active && self == endExit && endExitEnding && self.exitState == SceneExitController.ExitState.Idle)
                {
                    Log.Info($"The end altar ends the run: {endExitEnding.cachedName}.");
                    Run.instance.BeginGameOver(endExitEnding);
                }

                orig(self);
            };

            // Every monster of the stage, however it spawns. PercentMaxHealth sets max health to its stacks in
            // percent, on top of everything else, the Harvester's boss scaling included. A body that comes back (a
            // revive) keeps its master's inventory, and with it the cut.
            CharacterBody.onBodyStartGlobal += body =>
            {
                if (NetworkServer.active && body.teamComponent.teamIndex == TeamIndex.Monster && body.inventory
                    && EclipseStagePool.IsActive(Run.instance)
                    && SceneCatalog.mostRecentSceneDef.cachedName == SoulboundThreshold && IsFinalStage()
                    && body.inventory.GetItemCountPermanent(RoR2Content.Items.PercentMaxHealth) == 0)
                {
                    bool harvester = body.name.StartsWith(AnimaHarvesterBody);
                    body.inventory.GiveItemPermanent(RoR2Content.Items.PercentMaxHealth,
                        harvester ? AnimaHarvesterHealthPercent : SoulboundThresholdMonsterHealthPercent);
                    if (harvester)
                    {
                        Log.Info($"Soulbound Threshold: {body.name} at {AnimaHarvesterHealthPercent}% health, its monsters at {SoulboundThresholdMonsterHealthPercent}%.");
                    }
                }
            };

            // The major catastrophe overcharges the teleporter to 666%; leaving that state opens the Ritual Portal.
            On.EntityStates.TeleporterCatastrophe.DLC4.Teleporter.OverchargingState.OnExit += (orig, self) =>
            {
                if (NetworkServer.active && EclipseStagePool.IsActive(Run.instance))
                {
                    self.portalSpawnCard = null;
                    Log.Info("Shrine of Descent: no Ritual Portal to Soulbound Threshold.");
                }

                orig(self);
            };

            // Then comes DLC4's charged state, which outside Conduit Canyon shuts the teleporter and places a
            // Reincarnation zone instead. The plain charged state it extends takes its place, with its karma event.
            // Swapped on the server, so clients without the mod get the plain state too.
            On.RoR2.EntityStateMachine.SetNextState += (orig, self, newNextState) =>
            {
                if (newNextState is EntityStates.TeleporterCatastrophe.DLC4.Teleporter.ChargedState && NetworkServer.active
                    && EclipseStagePool.IsActive(Run.instance))
                {
                    Util.KarmaEventTrigger("SurviveMajorCatastrophe");
                    newNextState = new TeleporterInteraction.ChargedState();
                    Log.Info("Shrine of Descent: the teleporter stays usable.");
                }

                orig(self, newNextState);
            };
        }

        /// <summary>A stage 5 of the rotation: stageOrder 5, or Void Locus in play, which is filed under 99.</summary>
        internal static bool OnStageFive() =>
            SceneCatalog.mostRecentSceneDef.stageOrder == 5 || AddedMaps.InPlay()?.SceneName == "voidstage";

        /// <summary>
        /// Whether the run came here from stage 5: leaving a stage 5 begins a loop, and no stage has been cleared
        /// in it since. The same maps reached mid-run (Prime Meridian down the Colossus path, Soulbound Threshold
        /// through a Shrine of Descent, Solutional Haunt down the Alloyed path) come after a cleared stage.
        /// </summary>
        private static bool IsFinalStage() => Run.instance.stageClearCount > 0 && Run.instance.stageClearCountInCurrentLoop == 0;

        /// <summary>
        /// Points <paramref name="exit"/> at one of the final stages switched on, available to this run and not
        /// reached yet, each as likely. Once all have been, they all count as new again, bar the last one. With
        /// none switched on, it keeps Commencement.
        /// </summary>
        private static void PickFinalStage(SceneExitController exit)
        {
            Run run = Run.instance;
            var choices = new List<SceneDef>();
            foreach ((string scene, _) in FinalStages)
            {
                SceneDef sceneDef = SceneCatalog.FindSceneDef(scene);

                // CanPickStage is false when the map's DLC is off for this run.
                if (sceneDef && run.CanPickStage(sceneDef) && StageMaps.IsEnabled(sceneDef))
                {
                    choices.Add(sceneDef);
                }
            }

            if (choices.Count == 0)
            {
                Log.Warning("Every final stage is switched off, so the Primordial Teleporter leads to Commencement.");
                return;
            }

            List<SceneDef> fresh = choices.FindAll(choice => !run.GetEventFlag(VisitedFlag(choice.cachedName)));
            if (fresh.Count == 0)
            {
                choices.ForEach(choice => run.ResetEventFlag(VisitedFlag(choice.cachedName)));
                fresh = choices.Count > 1 ? choices.FindAll(choice => choice.cachedName != lastVisited) : choices;
            }

            exit.destinationScene = fresh[run.nextStageRng.RangeInt(0, fresh.Count)];
            Log.Info($"{SceneCatalog.mostRecentSceneDef.cachedName}: the Primordial Teleporter leads to {exit.destinationScene.cachedName} "
                + $"(one of {string.Join(", ", fresh.Select(choice => choice.cachedName))}).");
        }

        /// <summary>Marks the final stage the run has just reached as the final stage, for the next picks to skip.</summary>
        private static void MarkVisited()
        {
            string scene = SceneCatalog.mostRecentSceneDef ? SceneCatalog.mostRecentSceneDef.cachedName : null;
            if (EclipseStagePool.IsActive(Run.instance) && IsFinalStage() && FinalStages.Any(finalStage => finalStage.scene == scene))
            {
                Run.instance.SetEventFlag(VisitedFlag(scene));
                lastVisited = scene;
            }
        }

        /// <summary>A run event flag: set for the run, and not among those reset on a new loop.</summary>
        private static string VisitedFlag(string scene) => "EclipseAllMaps.FinalStageReached." + scene;

        /// <summary>Where a vanilla exit matching <paramref name="match"/> stands, or <paramref name="fallback"/>.</summary>
        private static Vector3 ExitPosition(System.Func<SceneExitController, bool> match, Vector3 fallback)
        {
            SceneExitController exit = InstanceTracker.GetInstancesList<SceneExitController>().FirstOrDefault(match);
            return exit ? exit.transform.position : fallback;
        }

        /// <summary>Opens a Destination Portal to the next loop's stage 1 beside <paramref name="position"/>.</summary>
        private static void OpenLoopPortal(Vector3 position)
        {
            // Coming from stage 5 the next stage is a stage 1 already, but not by every route (Commencement
            // through Solutional Haunt's teleporter, the Planetarium through Void Locus's signals).
            AddedMapProgression.PickStageOne(Run.instance);

            loopPortalCard ??= Addressables.LoadAssetAsync<SpawnCard>(LoopPortalAddress).WaitForCompletion();
            GameObject portal = SpawnBeside(loopPortalCard, position);
            if (!portal || !portal.TryGetComponent(out SceneExitController exit))
            {
                Log.Warning("Could not place the Destination Portal to stage 1.");
                return;
            }

            // As when looping straight from stage 5 (and leaving the final stages that are hidden realms), so the
            // next loop's stages keep their numbers within the loop.
            exit.doNotIncrementStageNumber = true;

            Chat.SendBroadcastChat(new Chat.SimpleChatMessage { baseToken = "<style=cWorldEvent>A Destination Portal opens: it leads on to the next loop.</style>" });
            Log.Info($"{SceneCatalog.mostRecentSceneDef.cachedName}: a Destination Portal to {Run.instance.nextStageScene?.cachedName}.");
        }

        /// <summary>
        /// Raises the end altar beside <paramref name="position"/>, which ends the run with <paramref name="ending"/>,
        /// announced in the game's own world-event style after <paramref name="aftermath"/>, a line on the fallen boss.
        /// </summary>
        private static void OpenEndAltar(Vector3 position, GameEndingDef ending, string aftermath)
        {
            GameObject altar = SpawnBeside(EndAltar.SpawnCard, position);
            if (!ending || !altar)
            {
                Log.Warning("Could not place the altar that ends the run.");
                return;
            }

            endExit = EndAltar.SetUp(altar);
            endExitEnding = ending;
            Chat.SendBroadcastChat(new Chat.SimpleChatMessage
            {
                baseToken = $"<style=cWorldEvent>{aftermath} An altar rises.. touch it, and let your journey end.</style>",
            });
            Log.Info($"{SceneCatalog.mostRecentSceneDef.cachedName}: an altar that ends the run ({ending.cachedName}).");
        }

        /// <summary>
        /// Places <paramref name="card"/>'s object beside <paramref name="position"/>, where a vanilla exit stands: far
        /// enough not to overlap it, or further off if nothing nearer is free.
        /// </summary>
        private static GameObject SpawnBeside(SpawnCard card, Vector3 position)
        {
            if (!card)
            {
                return null;
            }

            foreach (float maxDistance in new[] { BesideMax, 40f })
            {
                var placement = new DirectorPlacementRule
                {
                    placementMode = DirectorPlacementRule.PlacementMode.Approximate,
                    minDistance = BesideMin,
                    maxDistance = maxDistance,
                    position = position,
                };
                GameObject spawned = DirectorCore.instance.TrySpawnObject(new DirectorSpawnRequest(card, placement, new Xoroshiro128Plus(Run.instance.stageRng.nextUlong)));
                if (spawned)
                {
                    return spawned;
                }
            }

            return null;
        }
    }
}
