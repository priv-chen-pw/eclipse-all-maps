using System.Linq;
using RoR2;
using UnityEngine.AddressableAssets;

namespace EclipseAllMaps
{
    /// <summary>
    /// Lets the Halcyon Shrine spawn on every stage like any other interactable.
    ///
    /// Vanilla files it under "Storm Stuff" in each map's Seekers of the Storm interactable list: the Tier 1
    /// shrine on stage 1 maps (40 credits, weight 7 in a weight 3 category), the normal one later (50 credits,
    /// weight 1 in a weight 1.5 category). That list only joins a stage when the blend rolls it among the
    /// map's DLC lists, and Conduit Canyon and Void Locus have none. Here a stage's interactables without a
    /// Halcyon Shrine get its stage's one, with those weights. Its green portal is closed with the others in
    /// <see cref="AddedMapProgression"/>.
    /// </summary>
    internal static class HalcyonShrines
    {
        private const string Tier1ShrineAddress = "RoR2/DLC2/iscShrineHalcyoniteTier1.asset";
        private const string ShrineAddress = "RoR2/DLC2/iscShrineHalcyonite.asset";
        private const string CategoryName = "Storm Stuff";

        private static DirectorCard tier1Shrine;
        private static DirectorCard shrine;

        internal static void Init()
        {
            // Called with a copy of the stage's interactables, just before they are rolled.
            SceneDirector.onGenerateInteractableCardSelection += (sceneDirector, interactables) =>
            {
                if (!ModSettings.HalcyonShrineEveryStage.Value || !EclipseStagePool.IsActive(Run.instance))
                {
                    return;
                }

                int stage = CurrentStage();
                if (stage < 1 || stage > 5 || interactables.categories.Any(category => category.cards.Any(IsHalcyonShrine)))
                {
                    return;
                }

                bool firstStage = stage == 1;
                DirectorCard card = firstStage
                    ? tier1Shrine ??= NewCard(Tier1ShrineAddress, 7)
                    : shrine ??= NewCard(ShrineAddress, 1);

                // False without Seekers of the Storm.
                if (!card.IsAvailable())
                {
                    return;
                }

                int category = interactables.FindCategoryIndexByName(CategoryName);
                if (category < 0)
                {
                    category = interactables.AddCategory(CategoryName, firstStage ? 3f : 1.5f);
                }

                interactables.AddCard(category, card);
                Log.Info($"{SceneCatalog.mostRecentSceneDef.cachedName}: a Halcyon Shrine can spawn.");
            };
        }

        private static DirectorCard NewCard(string address, int weight) => new DirectorCard
        {
            spawnCard = Addressables.LoadAssetAsync<SpawnCard>(address).WaitForCompletion(),
            selectionWeight = weight,
        };

        private static bool IsHalcyonShrine(DirectorCard card) =>
            card.GetSpawnCard() is SpawnCard spawnCard && spawnCard.name.StartsWith("iscShrineHalcyonite");

        /// <summary>The stage (1-5) the current map is on: an added map's own, or its stageOrder.</summary>
        private static int CurrentStage()
        {
            SceneDef scene = SceneCatalog.mostRecentSceneDef;
            AddedMap addedMap = AddedMaps.Of(scene);
            if (addedMap != null)
            {
                return addedMap.StageOf(SceneCatalog.FindSceneDef(addedMap.SceneName));
            }

            return scene ? scene.stageOrder : 0;
        }
    }
}
