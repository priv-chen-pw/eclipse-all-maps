using RoR2;
using UnityEngine.AddressableAssets;
using UnityEngine.Networking;

namespace EclipseAllMaps
{
    /// <summary>
    /// Makes the added maps end the way their stage's other maps do: charge the teleporter, then move on.
    ///
    /// - Void Locus spawns no Deep Void Signals, so there is no void fog (only a charging signal raises it)
    ///   and no portal to the Planetarium. The scene director places a Primordial Teleporter instead, the
    ///   one Sky Meadow and Helminth Hatchery use: it starts aligned with the moon, leading to the final stage
    ///   (see <see cref="FinalStage"/>), and "Shift Destination" sends the run on to stage 1 instead. Leaving
    ///   it begins a loop, as leaving any stage 5 does.
    /// - Conduit Canyon's teleporter, placed in the scene, is wired to Solutional Haunt (the Alloyed path).
    ///   It goes to the run's next stage instead, picked from the stage 5 pool like any stage 4 map's.
    ///
    /// Each has its own setting, and applies only where the mod does and that map is switched on. The green
    /// Colossus portals Reformed Altar and Treeborn Colony open are closed in <see cref="AlternatePathPortals"/>.
    /// </summary>
    internal static class AddedMapProgression
    {
        private const string VoidLocus = "voidstage";
        private const string ConduitCanyon = "conduitcanyon";

        /// <summary>
        /// The stage 5 map whose destinations Void Locus, which has none, borrows for the Primordial
        /// Teleporter's planet alignment (and the final stages for their portals on): stage 1, or the looped
        /// stage 1 pool after a loop.
        /// </summary>
        private const string VoidLocusLeadsOnLike = "skymeadow";

        private const string PrimordialTeleporterAddress = "RoR2/Base/Teleporters/iscLunarTeleporter.asset";

        private static SpawnCard primordialTeleporter;

        internal static void Init()
        {
            On.RoR2.VoidStageMissionController.Start += (orig, self) =>
            {
                if (NetworkServer.active && VoidLocusGetsTeleporter())
                {
                    self.batteryCount = 0;
                }

                orig(self);
            };

            On.RoR2.SceneDirector.Start += (orig, self) =>
            {
                if (NetworkServer.active && VoidLocusGetsTeleporter())
                {
                    primordialTeleporter ??= Addressables.LoadAssetAsync<SpawnCard>(PrimordialTeleporterAddress).WaitForCompletion();
                    self.teleporterSpawnCard = primordialTeleporter;
                    Log.Info("Void Locus: no Deep Void Signals; placing a Primordial Teleporter.");
                }

                orig(self);
            };

            // The teleporter picks the next stage from the current map's destinations as soon as it spawns.
            On.RoR2.Run.PickNextStageSceneFromCurrentSceneDestinations += (orig, self) =>
            {
                if (VoidLocusGetsTeleporter())
                {
                    PickStageOne(self);
                }
                else
                {
                    orig(self);
                }
            };

            // Vanilla begins a loop when the run leaves a stageOrder 5 map; Void Locus is filed under 99. Without
            // it the loop count, the stage count within the loop and the loop's event flags never move on.
            On.RoR2.Run.ShouldBeginLoop += (orig, self, sceneDef) => orig(self, sceneDef) || VoidLocusGetsTeleporter();

            On.RoR2.TeleporterInteraction.Start += (orig, self) =>
            {
                orig(self);

                if (NetworkServer.active && ModSettings.ConduitCanyonToStage5.Value && IsChanged(ConduitCanyon))
                {
                    self.sceneExitController.useRunNextStageScene = true;
                    Log.Info($"Conduit Canyon: the teleporter leads to {Run.instance.nextStageScene?.cachedName} instead of the Alloyed path.");
                }
            };
        }

        /// <summary>
        /// Picks the run's next stage the way a stage 5 teleporter does: from Sky Meadow's destinations, stage 1,
        /// or the looped stage 1 pool after a loop.
        /// </summary>
        internal static void PickStageOne(Run run)
        {
            var choices = new WeightedSelection<SceneDef>();
            SceneCatalog.FindSceneDef(VoidLocusLeadsOnLike).AddDestinationsToWeightedSelection(choices, run.CanPickStage);
            run.PickNextStageScene(choices);
        }

        /// <summary>
        /// Whether the current scene is <paramref name="sceneName"/>, in a run the mod is on for, with that
        /// map switched on.
        /// </summary>
        private static bool IsChanged(string sceneName) =>
            AddedMaps.InPlay() != null && SceneCatalog.mostRecentSceneDef.cachedName == sceneName;

        /// <summary>
        /// Whether this is Void Locus getting a Primordial Teleporter instead of its signals. Its teleporter,
        /// battery count and borrowed destinations all go by this.
        /// </summary>
        private static bool VoidLocusGetsTeleporter() => ModSettings.VoidLocusTeleporter.Value && IsChanged(VoidLocus);
    }
}
