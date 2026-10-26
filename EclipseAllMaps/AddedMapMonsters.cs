using System.Linq;
using RoR2;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Networking;

namespace EclipseAllMaps
{
    /// <summary>
    /// Balances which monsters Void Locus spawns on the first loop.
    ///
    /// Its own pool is the Void Invasion family event every stage 4-5 map can roll: Void Devastator (800
    /// credits), Void Jailer (450) as the only miniboss, and Void Reaver (300) and Void Barnacle (50) as the
    /// basic monsters. A vanilla stage 5 has basics at 10-60 credits and minibosses at 40-200, and files the
    /// Void Reaver as a miniboss it holds back until after the first loop. That pool suits the map where
    /// vanilla puts it, since its portal needs 6 cleared stages, but not stage 5 of the first loop.
    ///
    /// Until then it gets the stage 5 pool instead: Sky Meadow's, blended with its DLC lists exactly as on
    /// Sky Meadow, and each category split evenly between those monsters and Void Locus's own (the Void
    /// Reaver as a miniboss; the Void Jailer comes back with the vanilla pool after a loop).
    ///
    /// Conduit Canyon needs nothing: its pool already matches the stage 4 maps' (same categories, similar
    /// costs, its Overloading Worm, Solus Amalgamator and Scavenger held back until after the first loop).
    /// </summary>
    internal static class AddedMapMonsters
    {
        private const string VoidLocus = "voidstage";

        /// <summary>Cleared stages vanilla needs before the teleporter can open a portal to Void Locus.</summary>
        private const int VanillaVoidLocusMinStagesCleared = 6;

        private const string SkyMeadowMonstersAddress = "RoR2/Base/skymeadow/dpSkyMeadowMonsters.asset";

        /// <summary>
        /// Void Locus's own monsters on the first loop, in the category vanilla stage 5 maps file each under,
        /// with their vanilla card settings.
        /// </summary>
        private static readonly (string category, string address, bool preventOverhead)[] VoidMonsters =
        {
            ("Champions", "RoR2/DLC1/VoidMegaCrab/cscVoidMegaCrab.asset", false),
            ("Minibosses", "RoR2/Base/Nullifier/cscNullifier.asset", true),
            ("Basic Monsters", "RoR2/DLC1/VoidBarnacle/cscVoidBarnacle.asset", true),
        };

        internal static void Init()
        {
            // ClassicStageInfo.Start rolls the stage's monsters here, from the scene's own pool unless a
            // selection is forced; artifacts (Kin, Dissonance, Honor) still apply to a forced one.
            On.RoR2.ClassicStageInfo.RebuildCards += (orig, self, forcedMonsterCategory, forcedInteractableCategory) =>
            {
                DirectorCardCategorySelection monsters = !forcedMonsterCategory && NetworkServer.active && IsFirstLoopVoidLocus()
                    ? FirstLoopVoidLocusMonsters(self)
                    : null;
                if (!monsters)
                {
                    orig(self, forcedMonsterCategory, forcedInteractableCategory);
                    return;
                }

                // RebuildCards uses a copy, so this one is ours to destroy.
                try
                {
                    orig(self, monsters, forcedInteractableCategory);
                }
                finally
                {
                    Object.Destroy(monsters);
                }
            };
        }

        private static bool IsFirstLoopVoidLocus() =>
            AddedMaps.InPlay() != null
            && SceneCatalog.mostRecentSceneDef.cachedName == VoidLocus
            && Run.instance.stageClearCount < VanillaVoidLocusMinStagesCleared;

        /// <summary>The first-loop selection, or null (keeping the vanilla pool) if Sky Meadow's is missing.</summary>
        private static DirectorCardCategorySelection FirstLoopVoidLocusMonsters(ClassicStageInfo stageInfo)
        {
            DccsPool skyMeadow = Addressables.LoadAssetAsync<DccsPool>(SkyMeadowMonstersAddress).WaitForCompletion();
            DccsPool.Category standard = skyMeadow ? skyMeadow.poolCategories.FirstOrDefault(category => category.name == "Standard") : null;
            if (standard == null)
            {
                Log.Warning($"{SkyMeadowMonstersAddress} has no Standard category; Void Locus keeps its own monsters.");
                return null;
            }

            var rng = new Xoroshiro128Plus(stageInfo.seedServer);
            DirectorCardCategorySelection monsters = DCCSBlender.GetBlendedDCCS(standard, ref rng, stageInfo, stageInfo.contentSourceMixLimit);

            // Sky Meadow keeps a Void Reaver card of its own for after the first loop; the one below replaces it.
            monsters.RemoveCardsThatFailFilter(card => card.GetSpawnCard()?.name != "cscNullifier");

            foreach ((string category, string address, bool preventOverhead) in VoidMonsters)
            {
                int index = monsters.FindCategoryIndexByName(category);
                if (index < 0)
                {
                    continue;
                }

                // As likely as all of Sky Meadow's monsters in the category together.
                float skyMeadowWeight = monsters.SumAllWeightsInCategory(monsters.categories[index]);
                monsters.AddCard(index, new DirectorCard
                {
                    spawnCard = Addressables.LoadAssetAsync<SpawnCard>(address).WaitForCompletion(),
                    selectionWeight = Mathf.Max(1, Mathf.RoundToInt(skyMeadowWeight)),
                    preventOverhead = preventOverhead,
                });
            }

            Log.Info($"Void Locus monsters (first loop): {Describe(monsters)}");
            return monsters;
        }

        private static string Describe(DirectorCardCategorySelection monsters) =>
            string.Join("; ", monsters.categories.Select(category =>
                $"{category.name}: " + string.Join(", ", category.cards
                    .Where(card => card.IsAvailable())
                    .Select(card => $"{card.GetSpawnCard().name.Replace("csc", "")} x{card.selectionWeight}"))));
    }
}
