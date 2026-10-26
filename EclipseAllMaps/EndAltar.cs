using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using RoR2;
using RoR2.Navigation;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace EclipseAllMaps
{
    /// <summary>
    /// The altar that ends the run after a final boss.
    ///
    /// On the network it is a Shrine of Order, so clients without the mod can still see and use it: the server
    /// renames it and makes it free through PurchaseInteraction's sync vars, and adds a SceneExitController that
    /// using it begins (FinalStage turns that exit into the run's end, and everyone teleports out as through a
    /// portal). Every machine with the mod knows the altar by its synced name and dresses it: the shrine's
    /// renderers and colliders off, the mod's own stone model on (Assets/EndAltar.obj, made by
    /// tools/make_altar.py: Sky Meadow's rock with the Eclipse emblem on each side), and over it the beam a Wake's
    /// Soul Conduit gets when the Wake succeeds.
    /// </summary>
    internal static class EndAltar
    {
        internal const string Name = "Altar of the Eclipse";
        private const string Context = "Let your journey end";

        private const string ShrineCardAddress = "RoR2/Base/ShrineRestack/iscShrineRestack.asset";
        private const string StoneAddress = "RoR2/Base/skymeadow/matSMRock.mat";
        private const string SoulConduitAddress = "RoR2/DLC4/WakeSacrifice/SacrificeMount.prefab";

        /// <summary>Emission over the emblem's own colours, so its corona glows and its dark moon stays dark.</summary>
        private static readonly Color EmblemGlow = new Color(1.2f, 1.2f, 1.2f);

        /// <summary>Longer than any stage, in seconds; a finite one, since the particle's age is divided by it.</summary>
        private const float BeamLifetime = 1e6f;

        private static InteractableSpawnCard spawnCard;
        private static Mesh mesh;
        private static Material[] materials;
        private static GameObject successBeam;

        /// <summary>The Shrine of Order's card, placeable on any ground node: a shrine's may not stand where shrines
        /// can't spawn, which takes in most boss arenas. Upright, so the beam falls straight onto it.</summary>
        internal static InteractableSpawnCard SpawnCard
        {
            get
            {
                if (!spawnCard)
                {
                    spawnCard = UnityEngine.Object.Instantiate(Addressables.LoadAssetAsync<InteractableSpawnCard>(ShrineCardAddress).WaitForCompletion());
                    spawnCard.forbiddenFlags = NodeFlags.None;
                    spawnCard.hullSize = HullClassification.Human;
                    spawnCard.orientToFloor = false;
                }

                return spawnCard;
            }
        }

        internal static void Init()
        {
            // A client learns the name in the sync var update after the spawn, or at once when joining later.
            On.RoR2.PurchaseInteraction.OnDeserialize += (orig, self, reader, initialState) =>
            {
                orig(self, reader, initialState);

                if (self.displayNameToken == Name)
                {
                    Dress(self);
                }
            };
        }

        /// <summary>Turns a Shrine of Order spawned on the server into the altar; returns the exit it begins.</summary>
        internal static SceneExitController SetUp(GameObject shrineObject)
        {
            var shrine = shrineObject.GetComponent<PurchaseInteraction>();
            shrine.NetworkdisplayNameToken = Name;
            shrine.NetworkcontextToken = Context;
            shrine.NetworkcostType = CostTypeIndex.None;
            shrine.Networkcost = 0;

            // A new event drops the shrine's own listeners, which would stack a Shrine of Order. They are on this
            // obsolete event, as in every vanilla prefab.
            var exit = shrineObject.AddComponent<SceneExitController>();
#pragma warning disable CS0618
            shrine.onPurchase = new PurchaseEvent();
            shrine.onPurchase.AddListener(_ =>
            {
                shrine.SetAvailable(false);
                exit.Begin();
            });
#pragma warning restore CS0618

            Dress(shrine);
            return exit;
        }

        private static void Dress(PurchaseInteraction shrine)
        {
            if (shrine.transform.Find(Name))
            {
                return;
            }

            LoadAssets();
            foreach (Renderer renderer in shrine.GetComponentsInChildren<Renderer>(true))
            {
                renderer.enabled = false;
            }

            foreach (Collider collider in shrine.GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = false;
            }

            // World: solid, and in the interactor's raycast mask.
            var model = new GameObject(Name) { layer = LayerIndex.world.intVal };
            model.transform.SetParent(shrine.transform, false);
            model.AddComponent<MeshFilter>().sharedMesh = mesh;
            var modelRenderer = model.AddComponent<MeshRenderer>();
            modelRenderer.sharedMaterials = materials;
            model.AddComponent<MeshCollider>().sharedMesh = mesh;
            model.AddComponent<EntityLocator>().entity = shrine.gameObject;

            // In a Wake the beam, one particle, lives 30 s and its motes stop after 30 s, the Wake being over; here
            // they stay. Without its fade over its lifetime the beam is at full strength from its first moment.
            GameObject beam = UnityEngine.Object.Instantiate(successBeam, model.transform, false);
            ParticleSystem pillar = beam.transform.Find("Beam").GetComponent<ParticleSystem>();
            ParticleSystem.MainModule pillarMain = pillar.main;
            pillarMain.startLifetime = BeamLifetime;
            ParticleSystem.ColorOverLifetimeModule pillarFade = pillar.colorOverLifetime;
            pillarFade.enabled = false;
            ParticleSystem.MainModule motes = beam.transform.Find("Beam/Motes").GetComponent<ParticleSystem>().main;
            motes.loop = true;
            beam.SetActive(true);

            shrine.GetComponent<Highlight>().targetRenderer = modelRenderer;
            shrine.GetComponent<GenericDisplayNameProvider>().displayToken = Name;
        }

        private static void LoadAssets()
        {
            if (mesh)
            {
                return;
            }

            Material stone = Addressables.LoadAssetAsync<Material>(StoneAddress).WaitForCompletion();
            var emblemTexture = new Texture2D(2, 2) { wrapMode = TextureWrapMode.Clamp };
            emblemTexture.LoadImage(ReadResource("EndAltarEmblem.png"));
            var emblem = new Material(stone) { mainTexture = emblemTexture };
            emblem.SetTexture("_NormalTex", null);
            emblem.SetTexture("_EmTex", emblemTexture);
            emblem.SetColor("_EmColor", EmblemGlow);

            var materialNames = new List<string>();
            using (var reader = new StreamReader(new MemoryStream(ReadResource("EndAltar.obj"))))
            {
                mesh = LoadObj(reader, materialNames);
            }

            materials = materialNames.ConvertAll(name => name.StartsWith("emblem") ? emblem : stone).ToArray();

            // Inactive in the prefab until a Wake succeeds; its copy is switched on.
            GameObject soulConduit = Addressables.LoadAssetAsync<GameObject>(SoulConduitAddress).WaitForCompletion();
            successBeam = soulConduit.transform.Find("SuccessIndicator").gameObject;
        }

        private static byte[] ReadResource(string name)
        {
            using Stream stream = typeof(EndAltar).Assembly.GetManifestResourceStream(name);
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            return memory.ToArray();
        }

        /// <summary>
        /// Reads a Wavefront OBJ into a mesh with a submesh per material (<paramref name="materialNames"/>, in order),
        /// x mirrored into Unity's left-handed axes as Unity's own importer does.
        /// shortcut: every face corner needs a uv and a normal (v/vt/vn); a model exported without them won't load.
        /// </summary>
        private static Mesh LoadObj(TextReader reader, List<string> materialNames)
        {
            var positions = new List<Vector3>();
            var uvs = new List<Vector2>();
            var normals = new List<Vector3>();
            var vertices = new List<Vector3>();
            var vertexUvs = new List<Vector2>();
            var vertexNormals = new List<Vector3>();
            var corners = new Dictionary<string, int>();
            var submeshes = new List<List<int>>();

            static float Number(string text) => float.Parse(text, CultureInfo.InvariantCulture);

            int Corner(string corner)
            {
                if (!corners.TryGetValue(corner, out int index))
                {
                    string[] ids = corner.Split('/');
                    index = corners[corner] = vertices.Count;
                    vertices.Add(positions[int.Parse(ids[0]) - 1]);
                    vertexUvs.Add(uvs[int.Parse(ids[1]) - 1]);
                    vertexNormals.Add(normals[int.Parse(ids[2]) - 1]);
                }

                return index;
            }

            string line;
            while ((line = reader.ReadLine()) != null)
            {
                string[] parts = line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                switch (parts.Length > 0 ? parts[0] : null)
                {
                    case "v":
                        positions.Add(new Vector3(-Number(parts[1]), Number(parts[2]), Number(parts[3])));
                        break;
                    case "vt":
                        uvs.Add(new Vector2(Number(parts[1]), Number(parts[2])));
                        break;
                    case "vn":
                        normals.Add(new Vector3(-Number(parts[1]), Number(parts[2]), Number(parts[3])));
                        break;
                    case "usemtl":
                        materialNames.Add(parts[1]);
                        submeshes.Add(new List<int>());
                        break;
                    case "f":
                        if (submeshes.Count == 0)
                        {
                            materialNames.Add("stone");
                            submeshes.Add(new List<int>());
                        }

                        // A fan over the polygon, wound backwards: mirroring x turned the faces inside out.
                        List<int> triangles = submeshes[submeshes.Count - 1];
                        for (int i = 3; i < parts.Length; i++)
                        {
                            triangles.Add(Corner(parts[1]));
                            triangles.Add(Corner(parts[i]));
                            triangles.Add(Corner(parts[i - 1]));
                        }

                        break;
                }
            }

            var mesh = new Mesh { name = "EndAltar" };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, vertexUvs);
            mesh.SetNormals(vertexNormals);
            mesh.subMeshCount = submeshes.Count;
            for (int i = 0; i < submeshes.Count; i++)
            {
                mesh.SetTriangles(submeshes[i], i);
            }

            mesh.RecalculateTangents();
            return mesh;
        }
    }
}
