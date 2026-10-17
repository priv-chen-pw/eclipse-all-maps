using RoR2;
using UnityEngine.Networking;

namespace EclipseAllMaps
{
    /// <summary>
    /// Gives each added map the scene director credits of the stage it joins, so it spawns about as many
    /// interactables (chests, shrines, drones...) and starting monsters as the maps it stands in for.
    ///
    /// The scene director reads both from the scene's <c>ClassicStageInfo</c> in <c>SceneDirector.Start</c>,
    /// then scales them by player count and difficulty the same way on every map, so setting the base values
    /// on that component just before is enough. The component belongs to the loaded scene, so nothing needs
    /// restoring afterwards.
    /// </summary>
    internal static class AddedMapCredits
    {
        internal static void Init()
        {
            On.RoR2.SceneDirector.Start += (orig, self) =>
            {
                if (NetworkServer.active)
                {
                    Rebalance();
                }

                orig(self);
            };
        }

        /// <summary>
        /// The median base credits (interactable, monster) of the vanilla maps on <paramref name="stage"/>,
        /// over sgStageN and loopSgStageN, as read from each scene's ClassicStageInfo. Null for a stage no map
        /// joins.
        /// </summary>
        private static (int interactable, int monster)? MedianCredits(int stage) => stage switch
        {
            // goolake 220; foggyswamp, ancientloft, nest 280. Monsters 100 on all.
            2 => (280, 100),
            // frozenwall, wispgraveyard, sulfurpools, ironalluvium, ironalluvium2 280; fungal 310; mire 340.
            // Monsters 100 on all.
            3 => (280, 100),
            // dampcavesimple, shipgraveyard, rootjungle, repurposedcrater, sanctum 400. Monsters 230 on all.
            4 => (400, 230),
            // skymeadow 520, helminthroost 570. Monsters 230 on both.
            5 => (545, 230),
            _ => null,
        };

        private static void Rebalance()
        {
            AddedMap addedMap = AddedMaps.InPlay();
            ClassicStageInfo stageInfo = SceneInfo.instance ? SceneInfo.instance.GetComponent<ClassicStageInfo>() : null;
            if (addedMap == null || !stageInfo || !ModSettings.BalanceCredits.Value)
            {
                return;
            }

            int stage = addedMap.StageOf(SceneCatalog.FindSceneDef(addedMap.SceneName));
            (int interactable, int monster)? credits = MedianCredits(stage);
            if (credits == null)
            {
                return;
            }

            (int interactable, int monster) = credits.Value;
            Log.Info($"{SceneCatalog.mostRecentSceneDef.cachedName} as stage {stage}: interactable credits {stageInfo.sceneDirectorInteractibleCredits} -> {interactable}, monster credits {stageInfo.sceneDirectorMonsterCredits} -> {monster}.");
            stageInfo.sceneDirectorInteractibleCredits = interactable;
            stageInfo.sceneDirectorMonsterCredits = monster;
        }
    }
}
