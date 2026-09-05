using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace TxTRPG.UI.Editor
{
    public static class AddressableAssetEditor
    {
        [MenuItem("Tools/TxT RPG/Addressables/Register UI Assets")]
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

        [MenuItem("Tools/TxT RPG/Addressables/Build Player Content")]
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

        [MenuItem("Tools/TxT RPG/Addressables/Validate Settings")]
        public static void ValidateSettingsMenu()
        {
            ThrowIfInvalid();
            Debug.Log("TxT RPG Addressables settings validation succeeded.");
        }

        public static string Register(string assetPath, string address, string groupName)
        {
            if (string.IsNullOrWhiteSpace(assetPath) || string.IsNullOrWhiteSpace(address))
            {
                throw new ArgumentException("Asset path and address are required.");
            }

            var guid = AssetDatabase.AssetPathToGUID(assetPath);
            if (string.IsNullOrEmpty(guid))
            {
                throw new InvalidOperationException($"Asset was not found at '{assetPath}'.");
            }

            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            var group = settings.FindGroup(groupName) ?? settings.CreateGroup(
                groupName,
                false,
                false,
                false,
                null,
                typeof(BundledAssetGroupSchema),
                typeof(ContentUpdateGroupSchema));
            ConfigureLocalGroup(group);
            var entry = settings.CreateOrMoveEntry(guid, group, false, false);
            entry.address = address;
            entry.SetLabel(groupName, false, true, false);
            settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryModified, entry, true, true);
            return address;
        }

        public static IReadOnlyList<string> ValidateSettings()
        {
            var errors = new List<string>();
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                errors.Add("Addressables settings do not exist.");
                return errors;
            }

            var addresses = new Dictionary<string, AddressableAssetEntry>(StringComparer.OrdinalIgnoreCase);
            foreach (var group in settings.groups)
            {
                if (group == null || group.entries.Count == 0) continue;
                var bundled = group.GetSchema<BundledAssetGroupSchema>();
                var updates = group.GetSchema<ContentUpdateGroupSchema>();
                if (bundled == null) errors.Add($"Group '{group.Name}' is missing BundledAssetGroupSchema.");
                if (updates == null) errors.Add($"Group '{group.Name}' is missing ContentUpdateGroupSchema.");

                foreach (var entry in group.entries)
                {
                    if (string.IsNullOrWhiteSpace(entry.address))
                    {
                        errors.Add($"Entry '{entry.guid}' in group '{group.Name}' has an empty address.");
                        continue;
                    }
                    if (addresses.TryGetValue(entry.address, out var duplicate))
                    {
                        errors.Add(
                            $"Address '{entry.address}' is duplicated by '{duplicate.guid}' and '{entry.guid}' " +
                            "(address comparison is case-insensitive).");
                    }
                    else
                    {
                        addresses.Add(entry.address, entry);
                    }
                    if (string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(entry.guid)))
                    {
                        errors.Add($"Address '{entry.address}' points to missing asset GUID '{entry.guid}'.");
                    }
                }
            }

            return errors;
        }

        private static void ConfigureLocalGroup(AddressableAssetGroup group)
        {
            var bundled = group.GetSchema<BundledAssetGroupSchema>() ??
                group.AddSchema<BundledAssetGroupSchema>();
            bundled.IncludeInBuild = true;
            bundled.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackTogether;
            bundled.Compression = BundledAssetGroupSchema.BundleCompressionMode.LZ4;
            var updates = group.GetSchema<ContentUpdateGroupSchema>() ??
                group.AddSchema<ContentUpdateGroupSchema>();
            updates.StaticContent = true;
            EditorUtility.SetDirty(group);
            EditorUtility.SetDirty(bundled);
            EditorUtility.SetDirty(updates);
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

            var baseAddress = Register(AssetDatabase.GetAssetPath(sprite), address, groupName);
            return $"{baseAddress}[{sprite.name}]";
        }
    }
}
