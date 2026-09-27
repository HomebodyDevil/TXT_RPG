using System;
using System.IO;
using System.Linq;
using TxTRPG.Application.Dice;
using TxTRPG.UI.Windows;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TxTRPG.Application.Editor
{
    // Editor-only PNG assets and a selective migration, not a generated-Prefab batch task.
    public static class TemporaryMenuIconUtility
    {
        public const string Folder = "Assets/TxTRPG/UI/Icons/TemporaryMenu";
        public static readonly string[] Keys = { "system", "inventory", "status", "action" };
        public static string PathFor(string key) => $"{Folder}/{key}.png";

        public static Sprite GetDefault(string key)
        {
            if (!Keys.Contains(key)) throw new ArgumentException("Unknown menu icon key.", nameof(key));
            var path = PathFor(key);
            return AssetDatabase.LoadAssetAtPath<Sprite>(path) ?? throw new InvalidOperationException($"Menu icon is not imported as a Sprite: {path}");
        }

        public static bool ApplyDefault(GameMenuButtonView view, string key)
        {
            if (view == null || view.Icon == null || view.Label == null) throw new InvalidOperationException("Menu button Icon/Label references are incomplete.");
            var existing = view.Icon.sprite;
            var legacyBag = key == "inventory" && AssetDatabase.GetAssetPath(existing) == QuickItemsUiProjectBuilder.BagIconPath;
            if (existing != null && !legacyBag) return false; // Includes existing defaults with developer tint/padding changes.
            view.SetIcon(GetDefault(key));
            view.ConfigureDisplay(GameMenuButtonDisplayMode.ImageOnly, 8f, Color.white);
            EditorUtility.SetDirty(view); EditorUtility.SetDirty(view.Icon);
            foreach (var target in new UnityEngine.Object[] { view, view.Icon, view.Icon.rectTransform, view.Icon.gameObject, view.Label.gameObject })
                if (PrefabUtility.IsPartOfPrefabInstance(target)) PrefabUtility.RecordPrefabInstancePropertyModifications(target);
            return true;
        }

        public static bool ApplyToMenu(GameMenuPanel menu)
        {
            var changed = false;
            foreach (var binding in menu.Buttons)
            {
                var key = binding.ActionKind == GameMenuButtonActionKind.Command
                    ? binding.CommandId == TemporaryDiceRollMenuController.RollAllCommandId ? "action" : null
                    : binding.PageId is "system" or "inventory" or "status" ? binding.PageId : null;
                if (key != null) changed |= ApplyDefault(binding.View, key);
            }
            return changed;
        }

        [MenuItem("Tools/TxT RPG/Application/Temporary/Apply Menu Icons Only")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before applying menu icons.");
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null) throw new InvalidOperationException("Save and close Prefab Stage before applying menu icons.");
            var scene = SceneManager.GetSceneByPath(QuickItemsUiProjectBuilder.ScenePath);
            if (scene.IsValid() && scene.isLoaded && scene.isDirty) throw new InvalidOperationException("Save the existing TMP_MainScene edits before applying menu icons.");
            foreach (var key in Keys)
            {
                if (!File.Exists(PathFor(key))) CreateIcon(key, PathFor(key));
                GetDefault(key);
            }
            foreach (var path in new[] { QuickItemsUiProjectBuilder.GameMenuPrefabPath, QuickItemsUiProjectBuilder.GameMenuScreenPrefabPath })
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var changed = false;
                    foreach (var menu in root.GetComponentsInChildren<GameMenuPanel>(true)) changed |= ApplyToMenu(menu);
                    if (changed) PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            var opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(QuickItemsUiProjectBuilder.ScenePath, OpenSceneMode.Additive);
            try
            {
                var menu = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<GameMenuPanel>(true)).Single();
                if (ApplyToMenu(menu))
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save TMP_MainScene icons.");
                }
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
            Debug.Log("MENU_ICONS: Applied four temporary icons; custom Sprites and command/page bindings preserved.");
        }

        private static void CreateIcon(string key, string path)
        {
            EnsureFolder(Folder);
            const int size = 128, samples = 4;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            try
            {
                var pixels = new Color32[size * size];
                for (var y = 0; y < size; y++)
                    for (var x = 0; x < size; x++)
                    {
                        var covered = 0;
                        for (var sy = 0; sy < samples; sy++)
                            for (var sx = 0; sx < samples; sx++)
                                if (Contains(key, x + (sx + .5f) / samples - 64f, y + (sy + .5f) / samples - 64f)) covered++;
                        pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(255f * covered / (samples * samples)));
                    }
                texture.SetPixels32(pixels); texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 128f; importer.alphaIsTransparency = true; importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed; importer.maxTextureSize = 128;
            importer.filterMode = FilterMode.Bilinear; importer.wrapMode = TextureWrapMode.Clamp;
            importer.npotScale = TextureImporterNPOTScale.None; importer.isReadable = false;
            importer.SaveAndReimport();
        }

        private static bool Contains(string key, float x, float y)
        {
            var radius = Mathf.Sqrt(x * x + y * y);
            switch (key)
            {
                case "system":
                    var angle = Mathf.Atan2(y, x);
                    var tooth = Mathf.Abs(Mathf.DeltaAngle(angle * Mathf.Rad2Deg, Mathf.Round(angle * Mathf.Rad2Deg / 45f) * 45f)) < 12f;
                    return radius >= 15f && (radius <= 34f || radius <= 46f && tooth);
                case "inventory":
                    var body = RoundedBox(x, y + 8f, 38f, 32f, 7f);
                    var handle = RoundedBox(x, y - 27f, 21f, 18f, 9f) && !RoundedBox(x, y - 27f, 12f, 9f, 3f);
                    var pocket = RoundedBox(x, y + 12f, 20f, 13f, 4f) && !RoundedBox(x, y + 12f, 13f, 6f, 1f);
                    return (body || handle) && !pocket;
                case "status":
                    var head = x * x + (y - 23f) * (y - 23f) <= 21f * 21f;
                    var shoulders = y <= -6f && y >= -43f && x * x / (44f * 44f) + (y + 40f) * (y + 40f) / (38f * 38f) <= 1f;
                    return head || shoulders;
                case "action":
                    if (!RoundedBox(x, y, 43f, 43f, 11f)) return false;
                    return !Circle(x, y, 0, 0, 7f) && !Circle(x, y, -22, -22, 7f) && !Circle(x, y, 22, 22, 7f) && !Circle(x, y, -22, 22, 7f) && !Circle(x, y, 22, -22, 7f);
                default: return false;
            }
        }
        private static bool Circle(float x, float y, float cx, float cy, float r) => (x - cx) * (x - cx) + (y - cy) * (y - cy) <= r * r;
        private static bool RoundedBox(float x, float y, float halfWidth, float halfHeight, float radius)
        {
            var dx = Mathf.Max(Mathf.Abs(x) - (halfWidth - radius), 0f);
            var dy = Mathf.Max(Mathf.Abs(y) - (halfHeight - radius), 0f);
            return Mathf.Abs(x) <= halfWidth && Mathf.Abs(y) <= halfHeight && dx * dx + dy * dy <= radius * radius;
        }
        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            var parent = Path.GetDirectoryName(folder).Replace('\\', '/'); EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
    }
}
