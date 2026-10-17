using RoR2;
using UnityEngine.AddressableAssets;
using UnityEngine.Networking;

namespace EclipseAllMaps
{
    /// <summary>
    /// Picks the teleporter boss where the mod changes it.
    ///
    /// - Conduit Canyon's teleporter event is scripted by its <c>VultureFightOverride</c>: the boss is always a
    ///   Solus Control Unit, and at 75% health an Alloy Hunter kills it and takes over. With either setting below
    ///   the script is off and the stage has one boss fight.
    /// - Robot bosses: Conduit Canyon's boss is a Solus Control Unit, Alloy Worship Unit, Solus Amalgamator or
    ///   Alloy Hunter, each as likely. The first and third are ordinary 800-credit bosses on stage 4 maps
    ///   already; the other two come as <see cref="SoloBoss"/>es.
    /// - Alloy Hunter as a normal boss: from stage 4 on any map's teleporter can roll the Alloy Hunter (as a
    ///   <see cref="SoloBoss"/>), as likely as any one of the stage's own bosses.
    /// </summary>
    internal static class TeleporterBosses
    {
        private const string ConduitCanyon = "conduitcanyon";

        /// <summary>Stages cleared by the time stage 4 starts.</summary>
        private const int Stage4ClearCount = 3;

        private const string SolusControlUnitAddress = "RoR2/Base/RoboBallBoss/cscRoboBallBoss.asset";
        private const string SolusAmalgamatorAddress = "RoR2/DLC3/SolusAmalgamator/cscSolusAmalgamator.asset";

        private static readonly SoloBoss AlloyHunter = new SoloBoss("Alloy Hunter", "RoR2/DLC3/VultureHunter/cscVultureHunter.asset");

        // Its card costs 0, as only Siren's Call's script spawns it. It is worth a Solus Control Unit's 800: the
        // same body with 30 armor instead of 20, so between that and the Solus Amalgamator in effective health.
        private static readonly SoloBoss AlloyWorshipUnit = new SoloBoss("Alloy Worship Unit", "RoR2/Base/RoboBallBoss/cscSuperRoboBallBoss.asset", usualCost: 800);

        private static DirectorCard solusControlUnit;
        private static DirectorCard solusAmalgamator;

        internal static void Init()
        {
            SoloBoss.Init();

            On.RoR2.VultureFightOverride.Start += (orig, self) =>
            {
                if (NetworkServer.active && OnConduitCanyon() && (ModSettings.AlloyHunterBoss.Value || ModSettings.RobotBosses.Value))
                {
                    self.triggerOnStart = false;

                    // Its boss director starts with 800 credits, which the scripted single scaled boss never
                    // needed; every other teleporter's starts with none. Kept, they would buy extra bosses.
                    TeleporterInteraction teleporter = self.GetComponent<TeleporterInteraction>();
                    if (teleporter && teleporter.bossDirector)
                    {
                        teleporter.bossDirector.monsterCredit = 0f;
                    }

                    Log.Info("Conduit Canyon: no scripted Solus Control Unit and Alloy Hunter fight.");
                }

                orig(self);
            };

            // The teleporter calls this once its charge starts and the boss credits are in, to roll the boss.
            On.RoR2.CombatDirector.SetNextSpawnAsBoss += (orig, self) =>
            {
                TeleporterInteraction teleporter = TeleporterInteraction.instance;
                // Artifact of Kin keeps the whole stage, its boss included, to one monster type.
                if (NetworkServer.active && teleporter && self == teleporter.bossDirector && !self.HasOverrideBoss()
                    && EclipseStagePool.IsActive(Run.instance)
                    && !(RunArtifactManager.instance && RunArtifactManager.instance.IsArtifactEnabled(RoR2Content.Artifacts.SingleMonsterType)))
                {
                    PickBoss(self, teleporter);
                }

                orig(self);
            };
        }

        private static void PickBoss(CombatDirector bossDirector, TeleporterInteraction teleporter)
        {
            if (ModSettings.RobotBosses.Value && OnConduitCanyon())
            {
                PickRobotBoss(bossDirector, teleporter);
            }
            else if (ModSettings.AlloyHunterBoss.Value && Run.instance.stageClearCount >= Stage4ClearCount)
            {
                RollAlloyHunter(bossDirector, teleporter);
            }
        }

        /// <summary>
        /// One of the four robot bosses, each as likely. If the pick is unavailable, Conduit Canyon's own boss
        /// deck (the Solus Amalgamator) rolls as usual.
        /// </summary>
        private static void PickRobotBoss(CombatDirector bossDirector, TeleporterInteraction teleporter)
        {
            solusControlUnit ??= NewCard(SolusControlUnitAddress);
            solusAmalgamator ??= NewCard(SolusAmalgamatorAddress);

            switch (bossDirector.rng.RangeInt(0, 4))
            {
                case 0:
                    OverrideWith(bossDirector, solusControlUnit, "Solus Control Unit");
                    break;
                case 1:
                    AlloyWorshipUnit.OverrideNextBoss(bossDirector, teleporter);
                    break;
                case 2:
                    OverrideWith(bossDirector, solusAmalgamator, "Solus Amalgamator");
                    break;
                default:
                    AlloyHunter.OverrideNextBoss(bossDirector, teleporter);
                    break;
            }
        }

        /// <summary>
        /// Makes the Alloy Hunter the next boss with the odds of one more boss on the stage: 1 in (the
        /// stage's bosses + 1). Not on a stage without bosses (such as Void Locus's Lunar family event), where it
        /// would be certain.
        /// </summary>
        private static void RollAlloyHunter(CombatDirector bossDirector, TeleporterInteraction teleporter)
        {
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

            if (bosses > 0 && bossDirector.rng.nextNormalizedFloat * (bosses + 1) < 1f)
            {
                AlloyHunter.OverrideNextBoss(bossDirector, teleporter);
            }
        }

        /// <summary>An ordinary boss: as many as the credits buy, elite when they stretch to it, as vanilla.</summary>
        private static void OverrideWith(CombatDirector bossDirector, DirectorCard card, string name)
        {
            if (card.IsAvailable())
            {
                bossDirector.OverrideNextBossCard(card, singularSpawn: false);
                Log.Info($"Teleporter boss: {name}.");
            }
        }

        private static DirectorCard NewCard(string address) => new DirectorCard
        {
            spawnCard = Addressables.LoadAssetAsync<SpawnCard>(address).WaitForCompletion(),
            selectionWeight = 1,
        };

        private static bool OnConduitCanyon() => AddedMaps.InPlay()?.SceneName == ConduitCanyon;
    }
}
