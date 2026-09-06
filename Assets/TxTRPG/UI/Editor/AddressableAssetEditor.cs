using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using TxTRPG.Editor.Common.Addressables;
using TxTRPG.Editor.Common.Menu;

namespace TxTRPG.UI.Editor
{
    public static class AddressableAssetEditor
    {
        [MenuItem(
            TxTRPGEditorMenuPaths.Addressables + "Register UI Assets",
            false,
            TxTRPGEditorMenuPriorities.Register)]
        public static void RegisterUiAssets()
        {
            Register("Assets/TxTRPG/UI/Prefabs/StoryTextPanel.prefab", "ui/prefabs/story-text-panel", "Gameplay_Common");
            Register("Assets/TxTRPG/UI/Prefabs/StoryMessageItem.prefab", "ui/prefabs/story-message-item", "Gameplay_Common");
            Register("Assets/TxTRPG/UI/Prefabs/CharacterDisplayPanel.prefab", "ui/prefabs/character-display-panel", "Gameplay_Common");
            Register("Assets/TxTRPG/UI/Prefabs/EnemyDisplayPanel.prefab", "ui/prefabs/enemy-display-panel", "Gameplay_Common");
            Register("Assets/TxTRPG/UI/Prefabs/ActionGridPanel.prefab", "ui/prefabs/action-grid-panel", "Gameplay_Common");
            Register("Assets/TxTRPG/UI/Prefabs/ActionGridCell.prefab", "ui/prefabs/action-grid-cell", "Gameplay_Common");
            Register("Assets/TxTRPG/UI/Prefabs/ActionContextMenu.prefab", "ui/prefabs/action-context-menu", "Gameplay_Common");
            Register("Assets/TxTRPG/UI/Prefabs/FlexibleLayoutPanel.prefab", "ui/prefabs/flexible-layout-panel", "Gameplay_Common");
            Register("Assets/TxTRPG/UI/Styles/StoryTextPanelDefaultBackgroundStyle.asset", "ui/styles/story-text-panel-default-background", "SharedUI");
            Register("Assets/TxTRPG/UI/DEMO/FlexibleLayoutPanel/FlexibleLayoutBackgroundDemoStyle.asset", "ui/backgrounds/flexible-demo-style", "SharedUI");
            Register("Assets/TxTRPG/UI/DEMO/CharacterDisplayPanel/CharacterDisplayBackgroundDemoStyle.asset", "ui/backgrounds/character-demo-style", "SharedUI");
            Register("Assets/TxTRPG/UI/DEMO/EnemyDisplayPanel/EnemyDisplayBackgroundDemoStyle.asset", "ui/backgrounds/enemy-demo-style", "SharedUI");
            AssetDatabase.SaveAssets();
            Debug.Log("TxT RPG UI assets registered with Addressables.");
        }

        [MenuItem(
            TxTRPGEditorMenuPaths.Addressables + "Build Player Content",
            false,
            TxTRPGEditorMenuPriorities.Build)]
        public static void BuildPlayerContent()
        {
            ThrowIfInvalid();
            AddressableAssetSettings.BuildPlayerContent(out var result);
            if (!string.IsNullOrEmpty(result.Error))
            {
                throw new InvalidOperationException($"Addressables build failed: {result.Error}");
            }

            Debug.Log($"Addressables player content built at {result.OutputPath}.");
        }

        [MenuItem(
            TxTRPGEditorMenuPaths.Addressables + "Validate Settings",
            false,
            TxTRPGEditorMenuPriorities.Validate)]
        public static void ValidateSettingsMenu()
        {
            ThrowIfInvalid();
            Debug.Log("TxT RPG Addressables settings validation succeeded.");
        }

        public static string Register(string assetPath, string address, string groupName)
        {
            using var receipt = AddressableAssetRegistration.Register(
                assetPath,
                address,
                groupName,
                allowUpdateExisting: true);
            receipt.Commit();
            return address;
        }

        public static IReadOnlyList<string> ValidateSettings()
        {
            return AddressableAssetRegistration.ValidateSettings();
        }

        private static void ThrowIfInvalid()
        {
            var errors = ValidateSettings();
            if (errors.Count > 0)
            {
                throw new InvalidOperationException(
                    "Addressables settings validation failed:\n- " + string.Join("\n- ", errors));
            }
        }

        public static string RegisterSprite(Sprite sprite, string address, string groupName)
        {
            if (sprite == null)
            {
                return string.Empty;
            }

            using var receipt = AddressableAssetRegistration.RegisterSprite(
                sprite,
                address,
                groupName,
                out var runtimeAddress);
            receipt.Commit();
            return runtimeAddress;
        }
    }
}
