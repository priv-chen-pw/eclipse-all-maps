using RoR2;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Networking;

namespace EclipseAllMaps
{
    /// <summary>
    /// Makes the added maps end the way their stage's other maps do: charge the teleporter, then move on.
    ///
    /// - Void Locus spawns no Deep Void Signals, so there is no void fog (only a charging signal raises it)
    ///   and no portal to the Planetarium. The scene director places a Primordial Teleporter instead, the
    ///   one Sky Meadow and Helminth Hatchery use: it starts aligned with the moon, leading to Commencement
    ///   (Mithrix), and "Shift Destination" sends the run on to stage 1 instead.
    /// - Conduit Canyon's teleporter, placed in the scene, is wired to Solutional Haunt (the Alloyed path).
    ///   It goes to the run's next stage instead, picked from the stage 5 pool like any stage 4 map's.
    /// - Reformed Altar and Treeborn Colony (Golden Dieback) open no green Colossus portal. Every teleporter
    ///   carries one for those maps (100%), leading on down the Colossus path to the next of them and Prime
    ///   Meridian, which would take the run off the stage rotation.
    /// - Nor does a Halcyon Shrine, on any map: once its gold is in, it opens one on stages 1-3 of the other
    ///   maps. Its gold portal (Gilded Coast) stays.
    ///
    /// Each has its own setting, and applies only where the mod does and (for an added map) that map is
    /// switched on. The Colossus path's end, Prime Meridian and False Son, stays reachable the way the
    /// Planetarium is: petting Commencement's frog 10 times after Mithrix opens a green portal there next to
    /// the void one.
    /// </summary>
    internal static class AddedMapProgression
    {
        private const string VoidLocus = "voidstage";
        private const string ConduitCanyon = "conduitcanyon";
        private const string PrimeMeridian = "meridian";

        /// <summary>The green Colossus portal (<c>PortalColossus</c>), which vanilla teleporters carry.</summary>
        private const string ColossusPortalCard = "iscColossusPortal";
        private const string ColossusPortalAddress = "RoR2/DLC2/iscColossusPortal.asset";

        /// <summary>Set once False Son is beaten; vanilla opens no more Colossus portals after it.</summary>
        private const string FalseSonBeatenFlag = "FalseSonBossComplete";

        /// <summary>
        /// The stage 5 map whose destinations Void Locus, which has none, borrows for the Primordial
        /// Teleporter's planet alignment: stage 1, or the looped stage 1 pool after a loop.
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
                if (!VoidLocusGetsTeleporter())
                {
                    orig(self);
                    return;
                }

                var choices = new WeightedSelection<SceneDef>();
                SceneCatalog.FindSceneDef(VoidLocusLeadsOnLike).AddDestinationsToWeightedSelection(choices, self.CanPickStage);
                self.PickNextStageScene(choices);
            };

            On.RoR2.TeleporterInteraction.Start += (orig, self) =>
            {
                orig(self);

                if (NetworkServer.active && ModSettings.ConduitCanyonToStage5.Value && IsChanged(ConduitCanyon))
                {
                    self.sceneExitController.useRunNextStageScene = true;
                    Log.Info($"Conduit Canyon: the teleporter leads to {Run.instance.nextStageScene?.cachedName} instead of the Alloyed path.");
                }
            };

            // Start rolls the portal against spawnChance, and turns on its preview orb if it will spawn.
            On.RoR2.PortalSpawner.Start += (orig, self) =>
            {
                if (NetworkServer.active && self.portalSpawnCard && self.portalSpawnCard.name == ColossusPortalCard
                    && ModSettings.NoColossusPortals.Value && IsClosedColossusPortal(self, out string source))
                {
                    self.spawnChance = -1f;
                    Log.Info($"{SceneCatalog.mostRecentSceneDef.cachedName}: no green Colossus portal from the {source}.");
                }

                orig(self);
            };

            // Commencement's frog shows up for the escape after Mithrix; at maxPets it opens the void portal.
            On.RoR2.FrogController.Pet += (orig, self, interactor) =>
            {
                orig(self, interactor);

                if (NetworkServer.active && self.petCount == self.maxPets && ModSettings.FrogPortalToPrimeMeridian.Value && EclipseStagePool.IsActive(Run.instance))
                {
                    OpenPrimeMeridianPortal(self.transform.position);
                }
            };
        }

        /// <summary>
        /// Opens a green Colossus portal to Prime Meridian near <paramref name="position"/>, unless the run
        /// lacks DLC2 or has beaten False Son already.
        /// </summary>
        private static void OpenPrimeMeridianPortal(Vector3 position)
        {
            Run run = Run.instance;
            SceneDef primeMeridian = SceneCatalog.FindSceneDef(PrimeMeridian);
            if (!primeMeridian || !run.CanPickStage(primeMeridian) || run.GetEventFlag(FalseSonBeatenFlag))
            {
                return;
            }

            SpawnCard portalCard = Addressables.LoadAssetAsync<SpawnCard>(ColossusPortalAddress).WaitForCompletion();
            var placement = new DirectorPlacementRule
            {
                placementMode = DirectorPlacementRule.PlacementMode.Approximate,
                minDistance = 10f,
                maxDistance = 40f,
                position = position,
            };
            GameObject portal = portalCard
                ? DirectorCore.instance.TrySpawnObject(new DirectorSpawnRequest(portalCard, placement, new Xoroshiro128Plus(run.stageRng.nextUlong)))
                : null;
            if (!portal)
            {
                Log.Warning("Could not place the portal to Prime Meridian.");
                return;
            }

            // It would get there anyway (no tier matches Commencement, so it falls back to its own
            // destination), but not by design; this pins it.
            if (portal.TryGetComponent(out SceneExitController exit))
            {
                exit.isAlternatePath = false;
                exit.useRunNextStageScene = false;
                exit.destinationScene = primeMeridian;
            }

            Chat.SendBroadcastChat(new Chat.SimpleChatMessage { baseToken = "PORTAL_STORM_OPEN" });
            Log.Info("Commencement: the frog opened a portal to Prime Meridian.");
        }

        /// <summary>
        /// Whether a green Colossus portal spawner is one this mod closes: a Halcyon Shrine's (stages 1-3 of
        /// other maps, once its gold is in) anywhere the mod is on, or the teleporter's on an added map.
        /// </summary>
        private static bool IsClosedColossusPortal(PortalSpawner spawner, out string source)
        {
            if (spawner.GetComponentInParent<HalcyoniteShrineInteractable>())
            {
                source = "Halcyon Shrine";
                return EclipseStagePool.IsActive(Run.instance);
            }

            source = "teleporter";
            return AddedMaps.InPlay() != null;
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
