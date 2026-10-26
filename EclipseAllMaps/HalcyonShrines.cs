using System.Linq;
using RoR2;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Networking;

namespace EclipseAllMaps
{
    /// <summary>
    /// Lets the Halcyon Shrine spawn on every stage like any other interactable, and tones it down from stage 4.
    ///
    /// Vanilla files it under "Storm Stuff" in each map's Seekers of the Storm interactable list: the Tier 1
    /// shrine on stage 1 maps (40 credits, weight 7 in a weight 3 category), the normal one later (50 credits,
    /// weight 1 in a weight 1.5 category). That list only joins a stage when the blend rolls it among the
    /// map's DLC lists, and Conduit Canyon and Void Locus have none. Here a stage's interactables without a
    /// Halcyon Shrine get its stage's one, with those weights. Its green portal is closed with the others in
    /// <see cref="AlternatePathPortals"/>.
    ///
    /// Its fight scales with the run twice: the gold it drains already scales with difficulty (like chest
    /// prices), and the Halcyonite it spawns once full gets +60% health and +25% damage per 100 gold of it, up
    /// to 15 times. That cap is reached from stage 3 on (x10 health, x4.75 damage, on top of its level), while
    /// the reward stays one item per player. Its three monster waves while it fills (Stone Golems, 140 credits
    /// times the difficulty each) grow with the difficulty too. From stage 4 the waves get half the credits
    /// and the Halcyonite half the gold bonus.
    /// </summary>
    internal static class HalcyonShrines
    {
        private const string Tier1ShrineAddress = "RoR2/DLC2/iscShrineHalcyoniteTier1.asset";
        private const string ShrineAddress = "RoR2/DLC2/iscShrineHalcyonite.asset";
        private const string CategoryName = "Storm Stuff";

        /// <summary>Stages cleared by the time stage 4 starts.</summary>
        private const int Stage4ClearCount = 3;

        /// <summary>Vanilla's cap on the Halcyonite's gold bonus, in 100s of gold.</summary>
        private const int MaxGoldLevel = 15;

        private static DirectorCard tier1Shrine;
        private static DirectorCard shrine;

        internal static void Init()
        {
            // Awake sets monsterCredit to 140 times the difficulty; each wave gets that much.
            On.RoR2.HalcyoniteShrineInteractable.Awake += (orig, self) =>
            {
                orig(self);

                if (NetworkServer.active && IsWeakened())
                {
                    self.monsterCredit *= 0.5f;
                }
            };

            // Called once the shrine is full, with the gold drained / 100 as the Halcyonite's bonus level.
            On.RoR2.CombatDirector.HalcyoniteShrineActivation += (orig, self, monsterCredit, chosenDirectorCard, difficultyLevel, shrineTransform) =>
            {
                if (IsWeakened())
                {
                    int weakened = Mathf.Min(difficultyLevel, MaxGoldLevel) / 2;
                    Log.Info($"Halcyon Shrine: Halcyonite gold bonus {difficultyLevel} -> {weakened}.");
                    difficultyLevel = weakened;
                }

                orig(self, monsterCredit, chosenDirectorCard, difficultyLevel, shrineTransform);
            };

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

        private static bool IsWeakened() =>
            EclipseStagePool.IsActive(Run.instance) && Run.instance.stageClearCount >= Stage4ClearCount;

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
