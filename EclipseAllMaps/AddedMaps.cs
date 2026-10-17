using RoR2;

namespace EclipseAllMaps
{
    /// <summary>
    /// The maps vanilla never rolls from its stage pools (<c>sgStage1</c>-<c>5</c> and
    /// <c>loopSgStage1</c>-<c>5</c>) and only reaches through a special portal, and the stage each joins.
    /// Their checkboxes are in <see cref="StageMaps"/> with every other map's.
    ///
    /// Damp Caverns (<c>fungal</c>) and Tenebrous Quagmire (<c>mire</c>) are not listed: vanilla already
    /// rolls both on stage 3, in the looped pool too.
    /// </summary>
    internal static class AddedMaps
    {
        internal static readonly AddedMap[] All =
        {
            // Reformed Altar, normally on the Colossus path.
            new AddedMap("lemuriantemple"),

            // Treeborn Colony, normally on the Colossus path. After looping it becomes Golden Dieback
            // (habitatfall), the same swap vanilla makes on that path.
            new AddedMap("habitat"),

            // Conduit Canyon, normally behind the DLC3 portal. Its teleporter normally leads on down the
            // Alloyed path; AddedMapProgression points it at the next stage.
            new AddedMap("conduitcanyon"),

            // Void Locus files itself under stageOrder 99 with the hidden realms, so it needs an explicit
            // stage. It has no teleporter of its own, only Deep Void Signals leading to the Planetarium;
            // AddedMapProgression swaps those for a Primordial Teleporter, so it ends a loop like Sky Meadow.
            new AddedMap("voidstage", stage: 5),
        };

        /// <summary>
        /// The added map <paramref name="scene"/> is, counting a looped variant (Golden Dieback for Treeborn
        /// Colony) as its map, or null for any other scene.
        /// </summary>
        internal static AddedMap Of(SceneDef scene)
        {
            if (!scene)
            {
                return null;
            }

            foreach (AddedMap addedMap in All)
            {
                SceneDef sceneDef = SceneCatalog.FindSceneDef(addedMap.SceneName);
                if (sceneDef && (scene == sceneDef || scene == sceneDef.loopedSceneDef))
                {
                    return addedMap;
                }
            }

            return null;
        }

        /// <summary>
        /// The added map being played right now, if this is an Eclipse run with the mod and that map switched
        /// on; null otherwise.
        /// </summary>
        internal static AddedMap InPlay()
        {
            AddedMap addedMap = Of(SceneCatalog.mostRecentSceneDef);
            return addedMap != null && addedMap.Enabled && EclipseStagePool.IsActive(Run.instance) ? addedMap : null;
        }
    }

    internal sealed class AddedMap
    {
        internal readonly string SceneName;

        private readonly int? stage;

        internal AddedMap(string sceneName, int? stage = null)
        {
            SceneName = sceneName;
            this.stage = stage;
        }

        /// <summary>Its checkbox, under its stage in <see cref="StageMaps"/>.</summary>
        internal bool Enabled => StageMaps.IsEnabled(SceneName);

        /// <summary>
        /// The stage (1-5) this map joins. Normally the map's own <c>stageOrder</c>, which also matches the
        /// stage its teleporter leads on from: each one's <c>destinationsGroup</c> is the next stage's pool.
        /// </summary>
        internal int StageOf(SceneDef sceneDef) => stage ?? sceneDef.stageOrder;
    }
}
