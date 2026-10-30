using RoR2;
using UnityEngine.Networking;

namespace EclipseAllMaps
{
    /// <summary>
    /// Keeps the run on the stage rotation by closing the portals down the two DLC paths. Their final bosses
    /// are among the final stages instead (see <see cref="FinalStage"/>).
    ///
    /// Colossus path (green portals, <c>iscColossusPortal</c>): every vanilla teleporter carries one for Reformed
    /// Altar and Treeborn Colony (Golden Dieback), a Halcyon Shrine opens one on stages 1-3 of the other maps once
    /// its gold is in, the Bazaar's geode listener opens one when its three geodes break, and Gilded Coast's exit
    /// opens one when the next stage is 2-4. Each leads to the path's map for the next stage number, from tier 4
    /// to Prime Meridian.
    ///
    /// Alloyed path: no access nodes spawn, so their codes never start the fight that arms a portal. Siren's
    /// Call's event still arms the teleporter's portal to Solutional Haunt, lighting an orb on it, which stays
    /// shut; after Solus Wing it arms one to Computational Exchange instead, a hidden realm that returns to the
    /// rotation.
    /// </summary>
    internal static class AlternatePathPortals
    {
        private const string ColossusPortalCard = "iscColossusPortal";

        private const string SolutionalHauntPortalCard = "iscHardwareProgPortal_Haunt";

        internal static void Init()
        {
            // Places and switches on the access nodes; as with the Alloyed Collective off, they stay off instead.
            On.RoR2.AccessCodesMissionController.OnStartServer += (orig, self) =>
            {
                if (!EclipseStagePool.IsActive(Run.instance))
                {
                    orig(self);
                    return;
                }

                foreach (AccessCodesNodeData data in self.nodes)
                {
                    data.node.SetActive(false);
                }

                Log.Info($"{SceneCatalog.mostRecentSceneDef.cachedName}: no access nodes.");
            };

            // Start rolls the portal against spawnChance, and turns on its preview orb if it will spawn.
            On.RoR2.PortalSpawner.Start += (orig, self) =>
            {
                if (NetworkServer.active && self.portalSpawnCard && self.portalSpawnCard.name == ColossusPortalCard
                    && IsClosedColossusPortal(self, out string source))
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
                if (EclipseStagePool.IsActive(Run.instance))
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

        /// <summary>Whether a portal spawner leads off the rotation down the Alloyed path: Solutional Haunt's.</summary>
        private static bool IsClosedAlloyedPortal(PortalSpawner spawner) =>
            spawner.PortalSpawnCard && spawner.PortalSpawnCard.name == SolutionalHauntPortalCard
            && EclipseStagePool.IsActive(Run.instance);
    }
}
