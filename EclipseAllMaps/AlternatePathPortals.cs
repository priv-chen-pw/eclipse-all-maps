using RoR2;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Networking;

namespace EclipseAllMaps
{
    /// <summary>
    /// Keeps the run on the stage rotation by closing the portals down the two DLC paths, and moves their
    /// endings to Commencement's frog, the way vanilla reaches the Planetarium.
    ///
    /// Colossus path (green portals, <c>iscColossusPortal</c>): every vanilla teleporter carries one for Reformed
    /// Altar and Treeborn Colony (Golden Dieback), a Halcyon Shrine opens one on stages 1-3 of the other maps once
    /// its gold is in, the Bazaar's geode listener opens one when its three geodes break, and Gilded Coast's exit
    /// opens one when the next stage is 2-4. Each leads to the path's map for the next stage number, from tier 4
    /// to Prime Meridian.
    ///
    /// Alloyed path: solving a map's access codes (<c>SolusFight</c>) or Siren's Call's event arms the
    /// teleporter's portal to Solutional Haunt, lighting an orb on it. On a run's third stage the access codes
    /// arm a portal to Conduit Canyon instead, which is a stage 4 of the rotation, and after Solus Wing one to
    /// Computational Exchange, a hidden realm that returns to the rotation.
    ///
    /// After Mithrix, petting the frog 10 times opens vanilla's purple portal to the Planetarium (Voidling);
    /// here it also opens a green one to Prime Meridian (False Son) and one to Neural Sanctum (Solus Heart).
    /// </summary>
    internal static class AlternatePathPortals
    {
        private const string PrimeMeridian = "meridian";
        private const string NeuralSanctum = "solusweb";
        private const string ConduitCanyon = "conduitcanyon";

        private const string ColossusPortalCard = "iscColossusPortal";
        private const string ColossusPortalAddress = "RoR2/DLC2/iscColossusPortal.asset";

        private const string SolutionalHauntPortalCard = "iscHardwareProgPortal_Haunt";
        private const string ConduitCanyonPortalCard = "iscHardwareProgPortal";

        /// <summary>The portal Computational Exchange's Solus vendor opens to Neural Sanctum.</summary>
        private const string NeuralSanctumPortalAddress = "RoR2/DLC3/SolusWebPortal.prefab";

        /// <summary>Set once False Son is beaten; vanilla opens no more Colossus portals after it.</summary>
        private const string FalseSonBeatenFlag = "FalseSonBossComplete";

        private const string SolusHeartBeatenFlag = "SolusHeartBeaten";

        /// <summary>The green portal's card with Neural Sanctum's portal as its prefab, to place it the same way.</summary>
        private static InteractableSpawnCard neuralSanctumPortalCard;

        internal static void Init()
        {
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

            // Gilded Coast's exit spawns its green portal itself, next to the gold one, when this says the next
            // stage is 2-4.
            On.EntityStates.Missions.Goldshores.Exit.IsValidStormTier += (orig, self) =>
            {
                if (ModSettings.NoColossusPortals.Value && EclipseStagePool.IsActive(Run.instance))
                {
                    Log.Info("Gilded Coast: no green Colossus portal.");
                    return false;
                }

                return orig(self);
            };

            // Behind PortalSpawner.WillSpawn, which the access codes and Siren's Call set to arm the teleporter's
            // portal; it also lights the orb and registers the portal with the teleporter. Server side only: on a
            // client it is the hook of the synced value.
            On.RoR2.PortalSpawner.OnWillSpawnUpdated += (orig, self, newValue) =>
            {
                if (newValue && NetworkServer.active && IsClosedAlloyedPortal(self))
                {
                    newValue = false;
                    Log.Info($"{SceneCatalog.mostRecentSceneDef.cachedName}: no portal down the Alloyed path ({self.PortalSpawnCard.name}).");
                }

                orig(self, newValue);
            };

            // Commencement's frog shows up for the escape after Mithrix; at maxPets it opens the void portal.
            On.RoR2.FrogController.Pet += (orig, self, interactor) =>
            {
                orig(self, interactor);

                if (NetworkServer.active && self.petCount == self.maxPets && EclipseStagePool.IsActive(Run.instance))
                {
                    Vector3 position = self.transform.position;
                    if (ModSettings.FrogPortalToPrimeMeridian.Value)
                    {
                        OpenPrimeMeridianPortal(position);
                    }

                    if (ModSettings.FrogPortalToNeuralSanctum.Value)
                    {
                        OpenNeuralSanctumPortal(position);
                    }
                }
            };
        }

        /// <summary>
        /// Whether a green Colossus portal spawner is one this mod closes: the teleporter's on an added map in
        /// play (the only maps it opens on), and any other (Halcyon Shrine, the Bazaar's geodes) wherever the mod
        /// is on.
        /// </summary>
        private static bool IsClosedColossusPortal(PortalSpawner spawner, out string source)
        {
            if (spawner.GetComponentInParent<TeleporterInteraction>())
            {
                source = "teleporter";
                return AddedMaps.InPlay() != null;
            }

            source = spawner.GetComponentInParent<HalcyoniteShrineInteractable>() ? "Halcyon Shrine" : spawner.name;
            return EclipseStagePool.IsActive(Run.instance);
        }

        /// <summary>
        /// Whether a portal spawner leads off the rotation down the Alloyed path: Solutional Haunt's always, and
        /// Conduit Canyon's when Conduit Canyon is switched off, since then it is vanilla's and leads on there.
        /// </summary>
        private static bool IsClosedAlloyedPortal(PortalSpawner spawner)
        {
            if (!ModSettings.NoSolutionalHauntPortals.Value || !spawner.PortalSpawnCard || !EclipseStagePool.IsActive(Run.instance))
            {
                return false;
            }

            switch (spawner.PortalSpawnCard.name)
            {
                case SolutionalHauntPortalCard:
                    return true;
                case ConduitCanyonPortalCard:
                    return !StageMaps.IsEnabled(ConduitCanyon);
                default:
                    return false;
            }
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

            GameObject portal = SpawnPortal(Addressables.LoadAssetAsync<SpawnCard>(ColossusPortalAddress).WaitForCompletion(), position);
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
        /// Opens vanilla's portal to Neural Sanctum (it leads nowhere else) near <paramref name="position"/>,
        /// unless the run lacks DLC3 or has beaten Solus Heart already.
        /// </summary>
        private static void OpenNeuralSanctumPortal(Vector3 position)
        {
            Run run = Run.instance;
            SceneDef neuralSanctum = SceneCatalog.FindSceneDef(NeuralSanctum);
            if (!neuralSanctum || !run.CanPickStage(neuralSanctum) || run.GetEventFlag(SolusHeartBeatenFlag))
            {
                return;
            }

            if (!neuralSanctumPortalCard)
            {
                var colossusPortal = Addressables.LoadAssetAsync<InteractableSpawnCard>(ColossusPortalAddress).WaitForCompletion();
                GameObject prefab = Addressables.LoadAssetAsync<GameObject>(NeuralSanctumPortalAddress).WaitForCompletion();
                if (!colossusPortal || !prefab)
                {
                    Log.Warning("The portal to Neural Sanctum is missing.");
                    return;
                }

                neuralSanctumPortalCard = Object.Instantiate(colossusPortal);
                neuralSanctumPortalCard.name = "iscNeuralSanctumPortal";
                neuralSanctumPortalCard.prefab = prefab;
            }

            if (!SpawnPortal(neuralSanctumPortalCard, position))
            {
                Log.Warning("Could not place the portal to Neural Sanctum.");
                return;
            }

            Chat.SendBroadcastChat(new Chat.SimpleChatMessage { baseToken = "PORTAL_SOLUSSHOP_OPEN" });
            Log.Info("Commencement: the frog opened a portal to Neural Sanctum.");
        }

        /// <summary>Places a portal 10-40 m from <paramref name="position"/>, like the frog's void portal.</summary>
        private static GameObject SpawnPortal(SpawnCard portalCard, Vector3 position)
        {
            if (!portalCard)
            {
                return null;
            }

            var placement = new DirectorPlacementRule
            {
                placementMode = DirectorPlacementRule.PlacementMode.Approximate,
                minDistance = 10f,
                maxDistance = 40f,
                position = position,
            };
            return DirectorCore.instance.TrySpawnObject(new DirectorSpawnRequest(portalCard, placement, new Xoroshiro128Plus(Run.instance.stageRng.nextUlong)));
        }
    }
}
