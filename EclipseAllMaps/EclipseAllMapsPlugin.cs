using BepInEx;

namespace EclipseAllMaps
{
    /// <summary>
    /// Adds the maps vanilla only reaches through a special portal (Reformed Altar, Treeborn Colony,
    /// Conduit Canyon and Void Locus) to the normal stage rotation of Eclipse runs.
    /// </summary>
    // Only the host picks stages, and clients load whatever scene the server changes to, so the mod works
    // on the host alone and declares no network compatibility.
    [BepInDependency(RiskOfOptionsCompat.PluginGuid, BepInDependency.DependencyFlags.SoftDependency)]
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class EclipseAllMapsPlugin : BaseUnityPlugin
    {
        public const string PluginAuthor = "chen-ror2";
        public const string PluginName = "EclipseAllMaps";
        public const string PluginGuid = PluginAuthor + "." + PluginName;
        public const string PluginVersion = "1.0.0";

        public void Awake()
        {
            Log.Init(Logger);

            ModSettings.Bind(Config);

            EclipseStagePool.Init();
            AddedMapCredits.Init();
            AddedMapProgression.Init();
            AddedMapMonsters.Init();
            AddedMapInteractables.Init();
            AlloyHunterBoss.Init();

            if (RiskOfOptionsCompat.IsInstalled)
            {
                RiskOfOptionsCompat.AddOptions(ModSettings.Menu);
            }

            Log.Info($"{PluginName} {PluginVersion} ready.");
        }
    }
}
