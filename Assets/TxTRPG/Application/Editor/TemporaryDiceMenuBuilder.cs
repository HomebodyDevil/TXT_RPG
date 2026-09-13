using System;
using System.Linq;
using TMPro;
using TxTRPG.Application.Combat;
using TxTRPG.Application.Dice;
using TxTRPG.Application.Players;
using TxTRPG.UI;
using TxTRPG.UI.Windows;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TxTRPG.Application.Editor
{
    public static class TemporaryDiceMenuBuilder
    {
        public const string ConfigurationPath = "Assets/TxTRPG/Application/Configuration/TemporaryDiceConfiguration.asset";
        public const string CombatConfigurationPath = "Assets/TxTRPG/Application/Configuration/TemporaryCombatConfiguration.asset";
        public const string HealthBarPrefabPath = "Assets/TxTRPG/UI/Prefabs/HealthBarPanel.prefab";
        public const string AppRootPrefabPath = "Assets/TxTRPG/SceneTransition/Prefabs/AppRoot.prefab";
        public const string ScenePath = "Assets/Scenes/TMP_MainScene.unity";
        public const string ButtonName = "TemporaryDiceRoll";
        public const string EnemyHealthName = "TemporaryEnemyHealthBar";
        public const string NextActionName = "TemporaryEnemyNextAction";

        [MenuItem("Tools/TxT RPG/Application/Temporary/Apply Player Dice Roll Menu")]
        public static void Apply()
        {
            var diceConfiguration = CreateOrUpdateConfiguration();
            var combatConfiguration = CreateOrUpdateCombatConfiguration();
            ApplyAppRoot(diceConfiguration);
            ApplyMainScene(combatConfiguration);
            AssetDatabase.SaveAssets();
            Debug.Log("Temporary player action combat was applied. This operation is safe to rerun.");
        }

        public static TemporaryDiceConfiguration CreateOrUpdateConfiguration()
        {
            EnsureFolder("Assets/TxTRPG/Application/Configuration");
            var asset = AssetDatabase.LoadAssetAtPath<TemporaryDiceConfiguration>(ConfigurationPath);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<TemporaryDiceConfiguration>();
                AssetDatabase.CreateAsset(asset, ConfigurationPath);
                asset.ConfigureForEditor(true, new[] { Create("temporary.d4", "D4", 4), Create("temporary.d6", "D6", 6), Create("temporary.d8", "D8", 8) });
                EditorUtility.SetDirty(asset);
            }
            else if (asset.TryUpgradeKnownDefaultsForEditor()) EditorUtility.SetDirty(asset);
            else if (asset.Dice.Any(definition => definition != null && definition.HasLegacyFaces))
                throw new InvalidOperationException("The existing temporary dice configuration contains custom legacy numeric faces. Configure each effect explicitly; no effect was guessed.");
            return asset;
        }

        public static TemporaryCombatConfiguration CreateOrUpdateCombatConfiguration()
        {
            EnsureFolder("Assets/TxTRPG/Application/Configuration");
            var asset = AssetDatabase.LoadAssetAtPath<TemporaryCombatConfiguration>(CombatConfigurationPath);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<TemporaryCombatConfiguration>();
            asset.ConfigureForEditor(true, "테스트 적", "demo-slime", "temporary.enemy.1", 40, 5, 3);
            AssetDatabase.CreateAsset(asset, CombatConfigurationPath);
            return asset;
        }

        private static TemporaryDieDefinition Create(string id, string name, int faces)
        {
            var result = new TemporaryDieDefinition();
            result.ConfigureForEditor(id, name, faces);
            return result;
        }

        private static void ApplyAppRoot(TemporaryDiceConfiguration configuration)
        {
            var root = PrefabUtility.LoadPrefabContents(AppRootPrefabPath);
            try
            {
                var host = root.GetComponentInChildren<PlayerSessionHost>(true) ?? throw new InvalidOperationException("AppRoot has no PlayerSessionHost.");
                var serialized = new SerializedObject(host);
                serialized.FindProperty("temporaryDiceConfiguration").objectReferenceValue = configuration;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, AppRootPrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void ApplyMainScene(TemporaryCombatConfiguration combatConfiguration)
        {
            var scene = SceneManager.GetSceneByPath(ScenePath);
            var opened = !scene.IsValid() || !scene.isLoaded;
            if (!opened && scene.isDirty) throw new InvalidOperationException("Save or revert unsaved TMP_MainScene changes before applying temporary combat.");
            if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                var components = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Component>(true)).ToArray();
                var menu = components.OfType<GameMenuPanel>().Single();
                var story = components.OfType<StoryTextPanel>().Single();
                var enemyPanel = components.OfType<EnemyDisplayPanel>().Single();
                var playerHealth = components.OfType<HealthBarPanel>().First(panel => !panel.transform.IsChildOf(enemyPanel.transform));
                DisableEnemyDemoLoader(enemyPanel);

                var enemyHealth = EnsureEnemyHealthBar(enemyPanel);
                var nextAction = EnsureNextActionText(enemyPanel);
                var controller = menu.GetComponent<TemporaryDiceRollMenuController>() ?? menu.gameObject.AddComponent<TemporaryDiceRollMenuController>();
                controller.ConfigureForEditor(menu, story, combatConfiguration, enemyPanel, playerHealth, enemyHealth, nextAction);
                menu.SetCommandHandlerForEditor(controller);

                var content = menu.Content ?? throw new InvalidOperationException("GameMenuPanel has no Content reference.");
                var existing = content.Find(ButtonName);
                var view = existing != null ? existing.GetComponent<GameMenuButtonView>() : CreateButton(content);
                if (view == null) throw new InvalidOperationException("Temporary action button has no GameMenuButtonView.");
                var label = view.GetComponentInChildren<TMP_Text>(true);
                if (label != null) label.text = "행동";
                var binding = new GameMenuButtonBinding();
                binding.ConfigureCommandForEditor(TemporaryDiceRollMenuController.RollAllCommandId, view.Button, true, view);
                menu.AddOrReplaceCommandForEditor(binding);

                EditorUtility.SetDirty(menu);
                EditorUtility.SetDirty(controller);
                EditorUtility.SetDirty(enemyPanel);
                if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new InvalidOperationException("Could not save TMP_MainScene.");
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        }

        private static void DisableEnemyDemoLoader(EnemyDisplayPanel enemyPanel)
        {
            var startup = enemyPanel.GetComponentInParent<PanelStartupController>(true);
            if (startup == null) return;
            var serialized = new SerializedObject(startup);
            var loader = serialized.FindProperty("initialDataLoader");
            if (loader != null && loader.objectReferenceValue is EnemyDisplayPanelDemoLoader)
            {
                loader.objectReferenceValue = null;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(startup);
            }
        }

        private static HealthBarPanel EnsureEnemyHealthBar(EnemyDisplayPanel enemyPanel)
        {
            var existing = enemyPanel.transform.Find(EnemyHealthName);
            if (existing != null) return existing.GetComponent<HealthBarPanel>();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HealthBarPrefabPath) ?? throw new InvalidOperationException("HealthBarPanel prefab is missing.");
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, enemyPanel.transform);
            instance.name = EnemyHealthName;
            var rect = (RectTransform)instance.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -12f);
            rect.sizeDelta = new Vector2(360f, 56f);
            return instance.GetComponent<HealthBarPanel>();
        }

        private static TMP_Text EnsureNextActionText(EnemyDisplayPanel enemyPanel)
        {
            var existing = enemyPanel.transform.Find(NextActionName);
            if (existing != null) return existing.GetComponent<TMP_Text>();
            var rect = CreateRect(NextActionName, enemyPanel.transform);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -72f);
            rect.sizeDelta = new Vector2(360f, 34f);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.text = "다음 적 행동: 공격 5";
            text.fontSize = 20f;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            return text;
        }

        private static GameMenuButtonView CreateButton(Transform parent)
        {
            var root = CreateRect(ButtonName, parent); root.sizeDelta = new Vector2(150, 48);
            var visual = CreateRect("VisualRoot", root); Stretch(visual);
            var bgRect = CreateRect("Background", visual); Stretch(bgRect); var bg = bgRect.gameObject.AddComponent<Image>(); bg.color = new Color(.2f,.24f,.32f,1);
            var iconRect = CreateRect("Icon", visual); Stretch(iconRect); var icon = iconRect.gameObject.AddComponent<Image>(); icon.raycastTarget = false; icon.gameObject.SetActive(false);
            var labelRect = CreateRect("Label", visual); Stretch(labelRect, 8, 6, 8, 6); var label = labelRect.gameObject.AddComponent<TextMeshProUGUI>(); label.text = "행동"; label.fontSize = 16; label.alignment = TextAlignmentOptions.Center; label.textWrappingMode = TextWrappingModes.NoWrap; label.overflowMode = TextOverflowModes.Ellipsis; label.raycastTarget = false;
            var borderRect = CreateRect("Border", visual); Stretch(borderRect); var border = borderRect.gameObject.AddComponent<Image>(); border.color = new Color(.65f,.72f,.9f,.35f); border.raycastTarget = false;
            var effect = CreateRect("EffectOverlay", visual); Stretch(effect);
            var button = root.gameObject.AddComponent<Button>(); button.targetGraphic = bg;
            var view = root.gameObject.AddComponent<GameMenuButtonView>(); view.ConfigureForEditor(button, visual, bg, icon, label, border, effect); return view;
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static void Stretch(RectTransform rect, float left = 0, float bottom = 0, float right = 0, float top = 0)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom); rect.offsetMax = new Vector2(-right, -top);
        }

        private static void EnsureFolder(string path)
        {
            var parts = path.Split('/'); var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
