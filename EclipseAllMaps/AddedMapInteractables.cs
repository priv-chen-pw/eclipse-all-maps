using System.Collections.Generic;
using System.Linq;
using RoR2;
using RoR2.ExpansionManagement;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Networking;

namespace EclipseAllMaps
{
    /// <summary>
    /// Gives Void Locus a stage 5 set of interactables.
    ///
    /// Its own list has only Void Cradles, Void Potentials and Stalks: void items paid for in health, and
    /// nothing to spend gold on. It gets Sky Meadow's list instead, blended with its DLC lists exactly as on
    /// Sky Meadow, with Void Locus's own list blended in the way the game blends a DLC list into a stage. Its
    /// one category keeps its weight, the same as every stage's Chests category, so void interactables stay
    /// about as common as chests.
    ///
    /// On every visit, unlike the monsters: the map gets a stage 5's credits either way, and its vanilla list
    /// would spend all of them on void items.
    /// </summary>
    internal static class AddedMapInteractables
    {
        private const string VoidLocus = "voidstage";
        private const string SkyMeadowInteractablesAddress = "RoR2/Base/skymeadow/dpSkyMeadowInteractables.asset";
        private const string VoidLocusInteractablesAddress = "RoR2/DLC1/voidstage/dccsVoidStageInteractables.asset";

        internal static void Init()
        {
            // RebuildCards rolls the interactables right after the monsters, limiting their DLC content to the
            // DLCs the monsters drew from. This replaces that pick afterwards, under the same limit.
            On.RoR2.ClassicStageInfo.RebuildCards += (orig, self, forcedMonsterCategory, forcedInteractableCategory) =>
            {
                orig(self, forcedMonsterCategory, forcedInteractableCategory);

                if (forcedInteractableCategory || !NetworkServer.active || AddedMaps.InPlay()?.SceneName != VoidLocus)
                {
                    return;
                }

                DirectorCardCategorySelection interactables = VoidLocusInteractables(self);
                if (interactables)
                {
                    self.interactableCategories = interactables;
                }
            };
        }

        /// <summary>The blended selection, or null (keeping Void Locus's own) if either list is missing.</summary>
        private static DirectorCardCategorySelection VoidLocusInteractables(ClassicStageInfo stageInfo)
        {
            DccsPool skyMeadow = Addressables.LoadAssetAsync<DccsPool>(SkyMeadowInteractablesAddress).WaitForCompletion();
            DirectorCardCategorySelection voidLocus = Addressables.LoadAssetAsync<DirectorCardCategorySelection>(VoidLocusInteractablesAddress).WaitForCompletion();
            if (!skyMeadow || skyMeadow.poolCategories.Length == 0 || !voidLocus)
            {
                Log.Warning("Sky Meadow's or Void Locus's interactable list is missing; Void Locus keeps its own interactables.");
                return null;
            }

            // The same DLC limit vanilla passes. GetBlendedDCCS adds to the list, so it gets a copy.
            DirectorCardCategorySelection monsters = stageInfo.modifiableMonsterCategories;
            List<ExpansionDef> expansions = monsters && monsters.expansionsInEffect?.Count > 0
                ? new List<ExpansionDef>(monsters.expansionsInEffect)
                : null;

            var rng = new Xoroshiro128Plus(stageInfo.seedServer);
            DirectorCardCategorySelection skyMeadowInteractables = DCCSBlender.GetBlendedDCCS(
                skyMeadow.poolCategories[0], ref rng, stageInfo, stageInfo.contentInteractableSourceMixLimit, expansions);
            DirectorCardCategorySelection interactables = DCCSBlender.BlendSpecificDCCS(
                new List<DirectorCardCategorySelection> { skyMeadowInteractables, voidLocus });
            Object.Destroy(skyMeadowInteractables);

            Log.Info($"Void Locus interactables: {Describe(interactables)}");
            return interactables;
        }

        private static string Describe(DirectorCardCategorySelection interactables)
        {
            float totalWeight = interactables.categories.Sum(category => category.selectionWeight);
            return string.Join("; ", interactables.categories.Select(category =>
                $"{category.name} {category.selectionWeight / totalWeight:P0} ({string.Join(", ", category.cards.Select(card => card.GetSpawnCard()?.name.Replace("isc", "")))})"));
        }
    }
}
