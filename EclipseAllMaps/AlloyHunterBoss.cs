using RoR2;
using UnityEngine.AddressableAssets;
using UnityEngine.Networking;

namespace EclipseAllMaps
{
    /// <summary>
    /// Conduit Canyon's teleporter event is scripted by its <c>VultureFightOverride</c>: the boss is always a
    /// Solus Control Unit, and at 75% health an Alloy Hunter kills it and takes over, so the stage has two
    /// boss fights. Here the script is off, so Conduit Canyon has one boss like any stage, and the Alloy
    /// Hunter joins the boss pool of every teleporter from stage 4 on instead, as likely as any one of the
    /// stage's own bosses.
    /// </summary>
    internal static class AlloyHunterBoss
    {
        private const string ConduitCanyon = "conduitcanyon";
        private const string AlloyHunterCardAddress = "RoR2/DLC3/VultureHunter/cscVultureHunter.asset";

        /// <summary>Stages cleared by the time stage 4 starts.</summary>
        private const int Stage4ClearCount = 3;

        private static DirectorCard alloyHunter;

        internal static void Init()
        {
            On.RoR2.VultureFightOverride.Start += (orig, self) =>
            {
                if (NetworkServer.active && ModSettings.ConduitCanyonOneBoss.Value && AddedMaps.InPlay()?.SceneName == ConduitCanyon)
                {
                    self.triggerOnStart = false;
                    Log.Info("Conduit Canyon: no scripted Solus Control Unit and Alloy Hunter fight.");
                }

                orig(self);
            };

            // The teleporter calls this once its charge starts, to roll the boss.
            On.RoR2.CombatDirector.SetNextSpawnAsBoss += (orig, self) =>
            {
                if (NetworkServer.active
                    && TeleporterInteraction.instance && self == TeleporterInteraction.instance.bossDirector
                    && !self.HasOverrideBoss()
                    && EclipseStagePool.IsActive(Run.instance)
                    && Run.instance.stageClearCount >= Stage4ClearCount
                    && ModSettings.AlloyHunterBoss.Value)
                {
                    RollAlloyHunter(self);
                }

                orig(self);
            };
        }

        /// <summary>
        /// Makes the Alloy Hunter the next boss with the odds of one more boss on the stage: 1 in (the
        /// stage's bosses + 1).
        /// </summary>
        private static void RollAlloyHunter(CombatDirector bossDirector)
        {
            alloyHunter ??= new DirectorCard
            {
                spawnCard = Addressables.LoadAssetAsync<SpawnCard>(AlloyHunterCardAddress).WaitForCompletion(),
                selectionWeight = 1,
            };

            // False without DLC3.
            if (!alloyHunter.IsAvailable())
            {
                return;
            }

            // Counted the way SetNextSpawnAsBoss picks: available champions not barred from boss duty.
            int bosses = 0;
            WeightedSelection<DirectorCard> cards = bossDirector.finalMonsterCardsSelection;
            for (int i = 0; cards != null && i < cards.Count; i++)
            {
                DirectorCard card = cards.GetChoice(i).value;
                SpawnCard spawnCard = card.GetSpawnCard();
                bool isChampion = spawnCard.prefab.GetComponent<CharacterMaster>().bodyPrefab.GetComponent<CharacterBody>().isChampion;
                bool forbiddenAsBoss = (spawnCard as CharacterSpawnCard)?.forbiddenAsBoss ?? false;
                if (isChampion && !forbiddenAsBoss && card.IsAvailable())
                {
                    bosses++;
                }
            }

            if (bossDirector.rng.nextNormalizedFloat * (bosses + 1) < 1f)
            {
                bossDirector.OverrideNextBossCard(alloyHunter, singularSpawn: false);
                Log.Info($"Teleporter boss: Alloy Hunter (1 in {bosses + 1}).");
            }
        }
    }
}
