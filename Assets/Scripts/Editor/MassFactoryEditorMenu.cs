using System.IO;
using MassFactory.Core;
using MassFactory.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MassFactory.EditorTools
{
    /// <summary>One-click project setup: default ScriptableObject assets and the Gameplay scene.</summary>
    public static class MassFactoryEditorMenu
    {
        private const string ConfigFolder = "Assets/ScriptableObjects/Config";
        private const string RelicFolder = "Assets/ScriptableObjects/Relics";
        private const string SceneFolder = "Assets/Scenes";
        private const string ScenePath = SceneFolder + "/Gameplay.unity";

        [MenuItem("Mass Factory/Generate Default Assets")]
        public static void GenerateDefaultAssets()
        {
            Directory.CreateDirectory(ConfigFolder);
            Directory.CreateDirectory(RelicFolder);

            var config = AssetDatabase.LoadAssetAtPath<GameConfigAsset>(ConfigFolder + "/GameConfig.asset");
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<GameConfigAsset>();
                config.ApplyPreset();
                AssetDatabase.CreateAsset(config, ConfigFolder + "/GameConfig.asset");
            }

            var baseline = AssetDatabase.LoadAssetAtPath<GameConfigAsset>(ConfigFolder + "/GameConfig_SpecBaseline.asset");
            if (baseline == null)
            {
                baseline = ScriptableObject.CreateInstance<GameConfigAsset>();
                baseline.preset = GameConfigAsset.Preset.SpecBaseline;
                baseline.ApplyPreset();
                AssetDatabase.CreateAsset(baseline, ConfigFolder + "/GameConfig_SpecBaseline.asset");
            }

            var catalog = AssetDatabase.LoadAssetAtPath<RelicCatalogAsset>(RelicFolder + "/RelicCatalog.asset");
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<RelicCatalogAsset>();
                AssetDatabase.CreateAsset(catalog, RelicFolder + "/RelicCatalog.asset");
            }

            catalog.relics.Clear();
            foreach (var definition in RelicCatalog.CreateDefaultDefinitions())
            {
                string path = RelicFolder + "/" + definition.Id + ".asset";
                var relic = AssetDatabase.LoadAssetAtPath<RelicDefinitionAsset>(path);
                if (relic == null)
                {
                    relic = ScriptableObject.CreateInstance<RelicDefinitionAsset>();
                    relic.CopyFrom(definition);
                    AssetDatabase.CreateAsset(relic, path);
                }
                catalog.relics.Add(relic);
            }
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[MassFactory] Default assets are in Assets/ScriptableObjects.");
        }

        [MenuItem("Mass Factory/Create Gameplay Scene")]
        public static void CreateGameplayScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            GenerateDefaultAssets();
            Directory.CreateDirectory(SceneFolder);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var go = new GameObject("MassFactory");
            var manager = go.AddComponent<GameManager>();
            manager.configAsset = AssetDatabase.LoadAssetAtPath<GameConfigAsset>(ConfigFolder + "/GameConfig.asset");
            manager.relicCatalogAsset = AssetDatabase.LoadAssetAtPath<RelicCatalogAsset>(RelicFolder + "/RelicCatalog.asset");
            EditorSceneManager.SaveScene(scene, ScenePath);

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            Debug.Log("[MassFactory] Created " + ScenePath + " and added it to Build Settings. Press Play.");
        }
    }
}
