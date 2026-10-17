using System.Collections.Generic;
using RoR2;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace EclipseAllMaps
{
    /// <summary>
    /// A boss vanilla only ever spawns alone, from a script: the Alloy Hunter (summoned once on Conduit Canyon)
    /// and the Alloy Worship Unit (Siren's Call's bonus boss). Bought with a teleporter's boss credits like an
    /// ordinary boss they would come four or five at a time late in a run, which their kits are not made for.
    ///
    /// As a teleporter boss one comes per boss share instead: one, plus one per Shrine of the Mountain (and per
    /// hex stack, which adds boss credits the same way). Each spawns from a copy of its card that costs one share
    /// of the credits, so the director spawns exactly that many and pays out a share's gold and experience for
    /// each, and each gets the health the share would have bought: share / usual cost times its own. The fight
    /// keeps the total health of any boss bought with the same credits, in fewer bodies.
    /// </summary>
    internal sealed class SoloBoss
    {
        private static readonly List<SoloBoss> all = new List<SoloBoss>();

        internal readonly string Name;

        private readonly string cardAddress;
        private readonly int? usualCostOverride;

        /// <summary>A copy of the boss's card, ours to change: its cost is set for each fight.</summary>
        private CharacterSpawnCard card;

        private DirectorCard directorCard;
        private int usualCost;
        private float healthMultiplier = 1f;

        /// <param name="usualCost">The credits one is worth, if not its card's cost.</param>
        internal SoloBoss(string name, string cardAddress, int? usualCost = null)
        {
            Name = name;
            this.cardAddress = cardAddress;
            usualCostOverride = usualCost;
            all.Add(this);
        }

        internal static void Init()
        {
            // Invoked right after the spawning director's own callback, which gives elite and multiplayer health.
            SpawnCard.onSpawnedServerGlobal += result =>
            {
                if (!result.success || !result.spawnedInstance || result.spawnRequest == null)
                {
                    return;
                }

                foreach (SoloBoss boss in all)
                {
                    if (boss.card && result.spawnRequest.spawnCard == boss.card)
                    {
                        boss.BoostHealth(result.spawnedInstance);
                        return;
                    }
                }
            };
        }

        /// <summary>
        /// Makes this the teleporter's next boss, one per boss share. False (changing nothing) without its DLC.
        /// Call before <c>SetNextSpawnAsBoss</c> runs, once the boss credits are in.
        /// </summary>
        internal bool OverrideNextBoss(CombatDirector bossDirector, TeleporterInteraction teleporter)
        {
            if (!Load())
            {
                return false;
            }

            float credits = bossDirector.monsterCredit;
            int shares = 1 + teleporter.shrineBonusStacks + teleporter.hexBonusStacks;
            int share = Mathf.FloorToInt(credits / shares);

            // A share, or its usual cost if shares are smaller (then fewer come), but never more than all the
            // credits, so at least one always spawns.
            card.directorCreditCost = Mathf.Max(1, Mathf.Min(Mathf.Max(share, usualCost), Mathf.FloorToInt(credits)));
            healthMultiplier = Mathf.Max(1f, card.directorCreditCost / (float)usualCost);

            bossDirector.OverrideNextBossCard(directorCard, singularSpawn: false);
            Log.Info($"Teleporter boss: {Name}, {Mathf.Max(1, Mathf.FloorToInt(credits / card.directorCreditCost))} of them with x{healthMultiplier:0.00} health ({credits:0} credits, {shares} share(s)).");
            return true;
        }

        private bool Load()
        {
            if (!card)
            {
                CharacterSpawnCard original = Addressables.LoadAssetAsync<CharacterSpawnCard>(cardAddress).WaitForCompletion();
                if (!original)
                {
                    Log.Warning($"{cardAddress} is missing; no {Name} as a teleporter boss.");
                    return false;
                }

                card = Object.Instantiate(original);
                card.name = original.name;
                usualCost = Mathf.Max(1, usualCostOverride ?? original.directorCreditCost);
                directorCard = new DirectorCard { spawnCard = card, selectionWeight = 1 };
            }

            // False without its DLC.
            return directorCard.IsAvailable();
        }

        private void BoostHealth(GameObject masterObject)
        {
            if (healthMultiplier <= 1f || !masterObject.TryGetComponent(out CharacterMaster master) || !master.inventory)
            {
                return;
            }

            // BoostHp adds 10% health per stack, so this multiplies with the stacks the director gave.
            int stacks = master.inventory.GetItemCountPermanent(RoR2Content.Items.BoostHp);
            master.inventory.GiveItemPermanent(RoR2Content.Items.BoostHp, Mathf.RoundToInt((10 + stacks) * (healthMultiplier - 1f)));
        }
    }
}
