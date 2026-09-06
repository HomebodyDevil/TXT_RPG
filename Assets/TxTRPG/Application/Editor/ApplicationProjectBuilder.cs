using System;
using System.Linq;
using TxTRPG.Application.Configuration;
using TxTRPG.Application.Players;
using TxTRPG.Content.Characters;
using TxTRPG.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace TxTRPG.Application.Editor
{
    public static class ApplicationProjectBuilder
    {
        public const string ConfigurationFolder =
            "Assets/TxTRPG/Application/Configuration";
        public const string DefaultNewGameProfilePath =
            ConfigurationFolder + "/DefaultNewGameProfile.asset";
        public const string CharacterCatalogPath =
            "Assets/TxTRPG/Content/Characters/CharacterContentCatalog.asset";
        public const string DefaultCharacterContentPath =
            "Assets/TxTRPG/Content/Characters/DefaultCharacter/DefaultCharacterContent.asset";
        public const string CharacterPanelPrefabPath =
            "Assets/TxTRPG/UI/Prefabs/CharacterDisplayPanel.prefab";

        public static NewGameProfile CreateOrUpdateNewGameProfile()
        {
            EnsureFolder(ConfigurationFolder);
            var catalog = LoadRequired<CharacterContentCatalog>(CharacterCatalogPath);
            var initialCharacter = LoadRequired<CharacterContentDefinition>(
                DefaultCharacterContentPath);
            var profile = AssetDatabase.LoadAssetAtPath<NewGameProfile>(
                DefaultNewGameProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<NewGameProfile>();
                AssetDatabase.CreateAsset(profile, DefaultNewGameProfilePath);
            }

            profile.ConfigureForEditor(catalog, initialCharacter);
            profile.ValidateOrThrow();
            EditorUtility.SetDirty(profile);
            return profile;
        }

        public static void ConfigureInitialContentScene(string scenePath)
        {
            var normalizedPath = scenePath?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalizedPath))
            {
                throw new ArgumentException("An initial content scene path is required.");
            }

            var previousActiveScene = SceneManager.GetActiveScene();
            var scene = SceneManager.GetSceneByPath(normalizedPath);
            var openedForConfiguration = !scene.IsValid() || !scene.isLoaded;
            if (openedForConfiguration)
            {
                scene = EditorSceneManager.OpenScene(
                    normalizedPath,
                    OpenSceneMode.Additive);
            }
            else if (scene.isDirty)
            {
                throw new InvalidOperationException(
                    $"Save '{normalizedPath}' before rebuilding the application configuration.");
            }

            try
            {
                var profile = CreateOrUpdateNewGameProfile();
                var catalog = profile.CharacterCatalog;
                var roots = scene.GetRootGameObjects();
                var binder = roots
                    .SelectMany(root => root.GetComponentsInChildren<ActiveCharacterDisplayBinder>(true))
                    .FirstOrDefault();
                if (binder == null)
                {
                    var demoLoader = roots
                        .SelectMany(root => root.GetComponentsInChildren<CharacterDisplayPanelDemoLoader>(true))
                        .FirstOrDefault();
                    if (demoLoader == null)
                    {
                        throw new InvalidOperationException(
                            $"No character display panel was found in '{normalizedPath}'.");
                    }
                    binder = ReplaceDemoPanel(demoLoader, catalog);
                }
                else
                {
                    ConfigureRuntimePanel(binder.gameObject, catalog);
                }

                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene, normalizedPath))
                {
                    throw new InvalidOperationException(
                        $"Could not save configured content scene '{normalizedPath}'.");
                }
            }
            finally
            {
                if (openedForConfiguration)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
                if (previousActiveScene.IsValid() && previousActiveScene.isLoaded)
                {
                    EditorSceneManager.SetActiveScene(previousActiveScene);
                }
            }
        }

        private static ActiveCharacterDisplayBinder ReplaceDemoPanel(
            CharacterDisplayPanelDemoLoader demoLoader,
            CharacterContentCatalog catalog)
        {
            var source = demoLoader.gameObject;
            var parent = source.transform.parent;
            var siblingIndex = source.transform.GetSiblingIndex();
            var sourceRect = source.GetComponent<RectTransform>();
            var sourceLayoutItem = source.GetComponent<FlexibleLayoutItem>();
            var panelPrefab = LoadRequired<GameObject>(CharacterPanelPrefabPath);
            var replacement = (GameObject)PrefabUtility.InstantiatePrefab(
                panelPrefab,
                parent);
            replacement.name = source.name;
            replacement.transform.SetSiblingIndex(siblingIndex);

            CopyRectTransform(sourceRect, replacement.GetComponent<RectTransform>());
            if (sourceLayoutItem != null)
            {
                var replacementLayout = replacement.GetComponent<FlexibleLayoutItem>() ??
                    replacement.AddComponent<FlexibleLayoutItem>();
                EditorUtility.CopySerialized(sourceLayoutItem, replacementLayout);
            }

            Object.DestroyImmediate(source);
            return ConfigureRuntimePanel(replacement, catalog);
        }

        private static ActiveCharacterDisplayBinder ConfigureRuntimePanel(
            GameObject panelObject,
            CharacterContentCatalog catalog)
        {
            var panel = panelObject.GetComponent<CharacterDisplayPanel>() ??
                throw new InvalidOperationException(
                    $"'{panelObject.name}' has no CharacterDisplayPanel.");
            foreach (var loader in panelObject.GetComponents<CharacterDisplayPanelDemoLoader>())
            {
                Object.DestroyImmediate(loader);
            }
            foreach (var startup in panelObject.GetComponents<PanelStartupController>())
            {
                Object.DestroyImmediate(startup);
            }
            foreach (var reveal in panelObject.GetComponents<FadePanelRevealTransition>())
            {
                Object.DestroyImmediate(reveal);
            }

            var presenter = panelObject.GetComponent<CharacterDisplayPresenter>() ??
                panelObject.AddComponent<CharacterDisplayPresenter>();
            presenter.SetTarget(panel);
            RemoveLegacyGeneratedStatusPanel(panelObject.transform);
            var binder = panelObject.GetComponent<ActiveCharacterDisplayBinder>() ??
                panelObject.AddComponent<ActiveCharacterDisplayBinder>();
            binder.Configure(presenter, catalog);
            return binder;
        }

        private static void RemoveLegacyGeneratedStatusPanel(Transform panelRoot)
        {
            var legacyStatus = panelRoot.Find("CharacterStatusPanel");
            if (legacyStatus == null ||
                legacyStatus.Find("AttackPowerText") == null ||
                legacyStatus.GetComponent<CharacterStatusPanel>() == null)
            {
                return;
            }

            Object.DestroyImmediate(legacyStatus.gameObject);
        }

        private static void CopyRectTransform(RectTransform source, RectTransform target)
        {
            if (source == null || target == null)
            {
                return;
            }
            target.anchorMin = source.anchorMin;
            target.anchorMax = source.anchorMax;
            target.pivot = source.pivot;
            target.anchoredPosition = source.anchoredPosition;
            target.sizeDelta = source.sizeDelta;
            target.localRotation = source.localRotation;
            target.localScale = source.localScale;
        }

        private static T LoadRequired<T>(string path) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                throw new InvalidOperationException(
                    $"Required asset was not found at '{path}'.");
            }
            return asset;
        }

        private static void EnsureFolder(string path)
        {
            var segments = path.Split('/');
            var current = segments[0];
            for (var index = 1; index < segments.Length; index++)
            {
                var next = current + "/" + segments[index];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, segments[index]);
                }
                current = next;
            }
        }
    }
}
