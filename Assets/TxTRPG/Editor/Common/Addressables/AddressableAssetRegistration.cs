using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace TxTRPG.Editor.Common.Addressables
{
    public sealed class AddressableRegistrationReceipt : IDisposable
    {
        private readonly AddressableAssetSettings settings;
        private readonly string guid;
        private readonly bool created;
        private readonly AddressableAssetGroup createdGroup;
        private bool completed;

        internal AddressableRegistrationReceipt(
            AddressableAssetSettings settings,
            string guid,
            bool created,
            AddressableAssetGroup createdGroup = null)
        {
            this.settings = settings;
            this.guid = guid;
            this.created = created;
            this.createdGroup = createdGroup;
        }

        public void Commit() => completed = true;

        public void Dispose()
        {
            if (completed || !created || settings == null)
            {
                return;
            }

            settings.RemoveAssetEntry(guid, false);
            if (createdGroup != null && createdGroup.entries.Count == 0)
            {
                settings.RemoveGroup(createdGroup);
            }
            settings.SetDirty(
                AddressableAssetSettings.ModificationEvent.EntryRemoved,
                null,
                true,
                true);
            completed = true;
        }
    }

    public static class AddressableAssetRegistration
    {
        public static AddressableRegistrationReceipt Register(
            string assetPath,
            string address,
            string groupName,
            bool allowUpdateExisting = false)
        {
            ValidateArguments(assetPath, address, groupName);
            var guid = AssetDatabase.AssetPathToGUID(assetPath);
            if (string.IsNullOrEmpty(guid))
            {
                throw new InvalidOperationException($"Asset was not found at '{assetPath}'.");
            }

            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            var existing = settings.FindAssetEntry(guid);
            if (existing != null && !allowUpdateExisting)
            {
                if (!string.Equals(existing.address, address, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"Asset '{assetPath}' is already registered as '{existing.address}'.");
                }
                return new AddressableRegistrationReceipt(settings, guid, false);
            }
            if (TryFindAddress(address, out var duplicate))
            {
                if (!string.Equals(duplicate.guid, guid, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Address '{address}' is already used by asset GUID '{duplicate.guid}'.");
                }
            }

            var group = settings.FindGroup(groupName);
            var createdGroup = group == null;
            group ??= settings.CreateGroup(
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
            settings.SetDirty(
                AddressableAssetSettings.ModificationEvent.EntryModified,
                entry,
                true,
                true);
            return new AddressableRegistrationReceipt(
                settings,
                guid,
                existing == null,
                createdGroup ? group : null);
        }

        public static AddressableRegistrationReceipt RegisterSprite(
            Sprite sprite,
            string address,
            string groupName,
            out string runtimeAddress)
        {
            if (sprite == null)
            {
                throw new ArgumentNullException(nameof(sprite));
            }
            var receipt = Register(AssetDatabase.GetAssetPath(sprite), address, groupName);
            runtimeAddress = $"{address}[{sprite.name}]";
            return receipt;
        }

        public static bool TryFindAddress(
            string address,
            out AddressableAssetEntry entry)
        {
            entry = null;
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null || string.IsNullOrWhiteSpace(address))
            {
                return false;
            }

            foreach (var group in settings.groups)
            {
                if (group == null)
                {
                    continue;
                }
                foreach (var candidate in group.entries)
                {
                    if (string.Equals(
                        candidate.address,
                        address,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        entry = candidate;
                        return true;
                    }
                }
            }
            return false;
        }

        public static bool TryFindAsset(
            string assetPath,
            out AddressableAssetEntry entry)
        {
            entry = null;
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            var guid = AssetDatabase.AssetPathToGUID(assetPath);
            if (settings == null || string.IsNullOrEmpty(guid))
            {
                return false;
            }
            entry = settings.FindAssetEntry(guid);
            return entry != null;
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

            var addresses = new Dictionary<string, AddressableAssetEntry>(
                StringComparer.OrdinalIgnoreCase);
            foreach (var group in settings.groups)
            {
                if (group == null || group.entries.Count == 0)
                {
                    continue;
                }
                if (group.GetSchema<BundledAssetGroupSchema>() == null)
                {
                    errors.Add($"Group '{group.Name}' is missing BundledAssetGroupSchema.");
                }
                if (group.GetSchema<ContentUpdateGroupSchema>() == null)
                {
                    errors.Add($"Group '{group.Name}' is missing ContentUpdateGroupSchema.");
                }

                foreach (var entry in group.entries)
                {
                    if (string.IsNullOrWhiteSpace(entry.address))
                    {
                        errors.Add($"Entry '{entry.guid}' in group '{group.Name}' has an empty address.");
                    }
                    else if (!addresses.TryAdd(entry.address, entry))
                    {
                        errors.Add($"Address '{entry.address}' is duplicated.");
                    }
                    if (string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(entry.guid)))
                    {
                        errors.Add($"Address '{entry.address}' points to missing asset GUID '{entry.guid}'.");
                    }
                }
            }
            return errors;
        }

        private static void ValidateArguments(
            string assetPath,
            string address,
            string groupName)
        {
            if (string.IsNullOrWhiteSpace(assetPath) ||
                string.IsNullOrWhiteSpace(address) ||
                string.IsNullOrWhiteSpace(groupName))
            {
                throw new ArgumentException("Asset path, address and group name are required.");
            }
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
    }
}
