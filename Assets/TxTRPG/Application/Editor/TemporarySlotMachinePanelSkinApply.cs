using System;
using System.IO;
using System.Linq;
using System.Text;
using TxTRPG.UI.SlotMachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TxTRPG.Application.Editor
{
    // TEMPORARY application menu (slot-machine-panel-skin). Creates the skin assets when missing (catalog, default skin,
    // sample image-payline skin and its 9-slice sample sprite), turns SlotMachinePanel.prefab's PaylineFrame into a
    // layout-only root with ProceduralBorder (the moved SlotPaylineFrameGraphic) and SkinImage children, records the
    // default look of every skin target in SlotReel.prefab and SlotMachinePanel.prefab once, and connects the catalog.
    // Partial edits only: existing defaults, skins and catalog contents are never overwritten and a Prefab is saved only
    // when something changed, so rerunning is safe. TMP_MainScene is saved only when it holds overrides of the moved
    // payline graphic. Excluded from the registered Prefab rebuild batch for the same reasons as Apply Slot Machine
    // Panel Visual. Retire it once every working copy has been migrated.
    public static class TemporarySlotMachinePanelSkinApply
    {
        public const string MenuPath = "Tools/TxT RPG/Application/Temporary/Apply Slot Machine Panel Skin";
        public const string SkinFolder = TemporarySlotMachinePanelVisualApply.StyleFolder + "/Skins";
        public const string CatalogPath = SkinFolder + "/SlotMachinePanelSkinCatalog.asset";
        public const string DefaultSkinPath = SkinFolder + "/DefaultSlotMachinePanelSkin.asset";
        public const string SampleSkinPath = SkinFolder + "/SampleImagePaylineSkin.asset";
        public const string SampleSpritePath = SkinFolder + "/SlotPaylineFrameSample.png";
        public const string DefaultSkinId = "slot-skin.default";
        public const string SampleSkinId = "slot-skin.sample-image-payline";

        // Sample sprite: 32 px, transparent margin and glow outside a 5 px outlined gold band whose inner edge is 10 px
        // from the image edge. The 12 px 9-slice border keeps the band and glow unscaled.
        private const int SampleSize = 32;
        private const int SampleBorder = 12;
        private const float SampleOutset = 10f;

        [MenuItem(MenuPath)]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before applying the slot machine panel skin.");
            if (EditorApplication.isCompiling) throw new InvalidOperationException("Wait for script compilation to finish.");
            if (PrefabStageUtility.GetCurrentPrefabStage() != null) throw new InvalidOperationException("Save and close Prefab Stage first.");
            if (Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty))
                throw new InvalidOperationException("Save or revert the unsaved open Scenes before applying the slot machine panel skin.");
            var summary = new StringBuilder("SLOT_MACHINE_PANEL_SKIN_APPLY:\n");
            // Prefabs are edited with an empty Scene open (see Apply Slot Machine Panel Visual). Every open Scene is saved.
            var setup = EditorSceneManager.GetSceneManagerSetup();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            try { ApplyAssetsAndPrefabs(summary); }
            finally { if (setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup); }
            var scene = SceneManager.GetSceneByPath(TemporarySlotMachineIntegration.ScenePath);
            var opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(TemporarySlotMachineIntegration.ScenePath, OpenSceneMode.Additive);
            try
            {
                CleanSceneOverrides(scene, summary);
                if (scene.isDirty && !EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save TMP_MainScene.");
                Validate();
                summary.AppendLine(" validation: passed. Safe to rerun.");
                Debug.Log(summary.ToString());
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        }

        public static void ApplyAssetsAndPrefabs(StringBuilder summary)
        {
            EnsureFolder(SkinFolder);
            var sample = EnsureSampleSprite(summary);
            var defaultSkin = EnsureSkin(DefaultSkinPath, DefaultSkinId, "기본", null, summary);
            var sampleSkin = EnsureSkin(SampleSkinPath, SampleSkinId, "예시 이미지 Payline", sample, summary);
            var catalog = EnsureCatalog(defaultSkin, sampleSkin, summary);
            EditPrefab(TemporarySlotMachineIntegration.ReelPrefabPath, ApplyReel, summary);
            EditPrefab(TemporarySlotMachineIntegration.PanelPrefabPath, root => ApplyPanel(root, catalog, summary), summary);
        }

        // ---------- Assets ----------

        private static Sprite EnsureSampleSprite(StringBuilder summary)
        {
            if (!File.Exists(SampleSpritePath))
            {
                var texture = new Texture2D(SampleSize, SampleSize, TextureFormat.RGBA32, false);
                for (var y = 0; y < SampleSize; y++)
                for (var x = 0; x < SampleSize; x++)
                    texture.SetPixel(x, y, SamplePixel(Mathf.Min(Mathf.Min(x, y), Mathf.Min(SampleSize - 1 - x, SampleSize - 1 - y))));
                File.WriteAllBytes(SampleSpritePath, texture.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(SampleSpritePath, ImportAssetOptions.ForceSynchronousImport);
                summary.AppendLine($" created {SampleSpritePath}");
            }
            var importer = (TextureImporter)AssetImporter.GetAtPath(SampleSpritePath);
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            var border = new Vector4(SampleBorder, SampleBorder, SampleBorder, SampleBorder);
            if (importer.textureType != TextureImporterType.Sprite || importer.mipmapEnabled || importer.wrapMode != TextureWrapMode.Clamp ||
                importer.textureCompression != TextureImporterCompression.Uncompressed || settings.spriteMeshType != SpriteMeshType.FullRect ||
                importer.spriteBorder != border)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
                importer.spriteBorder = border;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
                summary.AppendLine($" set sprite import settings of {SampleSpritePath} (Full Rect, 9-slice border {SampleBorder})");
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(SampleSpritePath) ?? throw new InvalidOperationException($"{SampleSpritePath} is not a Sprite.");
        }

        // d = distance in pixels from the nearest image edge.
        private static Color SamplePixel(int d)
        {
            var gold = new Color(1f, .835f, .29f);
            var outline = new Color(.23f, .16f, 0f, 1f);
            if (d < 2) return Color.clear;
            if (d < 5) return new Color(gold.r, gold.g, gold.b, .12f + .12f * (d - 2));
            if (d == 5 || d == 9) return outline;
            if (d == 6) return new Color(1f, .95f, .69f, 1f);
            if (d < 9) return new Color(gold.r, gold.g, gold.b, 1f);
            return Color.clear;
        }

        private static SlotMachinePanelSkin EnsureSkin(string path, string id, string displayName, Sprite payline, StringBuilder summary)
        {
            var skin = AssetDatabase.LoadAssetAtPath<SlotMachinePanelSkin>(path);
            if (skin != null) return skin;
            skin = ScriptableObject.CreateInstance<SlotMachinePanelSkin>();
            var serialized = new SerializedObject(skin);
            serialized.FindProperty("skinId").stringValue = id;
            serialized.FindProperty("displayName").stringValue = displayName;
            if (payline != null)
            {
                serialized.FindProperty("paylineFrame.sprite").objectReferenceValue = payline;
                serialized.FindProperty("paylineFrame.drawMode").enumValueIndex = (int)SlotSkinDrawMode.Sliced;
                foreach (var side in new[] { "left", "right", "top", "bottom" })
                    serialized.FindProperty($"paylineOutset.{side}").floatValue = SampleOutset;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(skin, path);
            AssetDatabase.SaveAssets();
            summary.AppendLine($" created {path} ({id})");
            return skin;
        }

        private static SlotMachinePanelSkinCatalog EnsureCatalog(SlotMachinePanelSkin defaultSkin, SlotMachinePanelSkin sampleSkin, StringBuilder summary)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<SlotMachinePanelSkinCatalog>(CatalogPath);
            if (catalog != null) return catalog;
            catalog = ScriptableObject.CreateInstance<SlotMachinePanelSkinCatalog>();
            catalog.ConfigureForEditor(new[] { defaultSkin, sampleSkin }, DefaultSkinId);
            AssetDatabase.CreateAsset(catalog, CatalogPath);
            AssetDatabase.SaveAssets();
            summary.AppendLine($" created {CatalogPath} (selected {DefaultSkinId})");
            return catalog;
        }

        // ---------- Prefabs ----------

        private static void EditPrefab(string path, Action<GameObject> edit, StringBuilder summary)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var before = Snapshot(root);
                edit(root);
                if (Snapshot(root) == before) { summary.AppendLine($" {path} already matches; not saved"); return; }
                PrefabUtility.SaveAsPrefabAsset(root, path, out var saved);
                if (!saved) throw new InvalidOperationException($"Could not save {path}.");
                summary.AppendLine($" updated {path}");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static string Snapshot(GameObject root)
        {
            var builder = new StringBuilder();
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            {
                builder.Append(transform.GetSiblingIndex()).Append('/').Append(transform.name).Append(transform.gameObject.activeSelf).Append('\n');
                foreach (var component in transform.GetComponents<Component>())
                    if (component != null) builder.Append(component.GetType().Name).Append(EditorJsonUtility.ToJson(component)).Append('\n');
            }
            return builder.ToString();
        }

        private static void ApplyReel(GameObject root)
        {
            var view = root.GetComponent<SlotReelView>() ?? throw new InvalidOperationException("SlotReel.prefab has no SlotReelView.");
            view.ConfigureSkinForEditor(GraphicAt(root.transform, "Frame"), GraphicAt(root.transform, "Window/ShadeOverlay"), false);
        }

        private static void ApplyPanel(GameObject root, SlotMachinePanelSkinCatalog catalog, StringBuilder summary)
        {
            var panel = root.GetComponent<SlotMachinePanel>() ?? throw new InvalidOperationException("SlotMachinePanel.prefab has no SlotMachinePanel.");
            var payline = root.transform.Find("Content/ReelMachine/PaylineFrame") as RectTransform
                ?? throw new InvalidOperationException("SlotMachinePanel.prefab has no Content/ReelMachine/PaylineFrame; run Apply Slot Machine Panel Visual first.");

            // ProceduralBorder: the SlotPaylineFrameGraphic moved from the root with its values (color, thickness).
            var oldBorder = payline.GetComponent<SlotPaylineFrameGraphic>();
            var borderRect = Child("ProceduralBorder", payline, 0);
            var border = borderRect.GetComponent<SlotPaylineFrameGraphic>();
            if (border == null)
            {
                if (borderRect.GetComponent<CanvasRenderer>() == null) borderRect.gameObject.AddComponent<CanvasRenderer>();
                border = borderRect.gameObject.AddComponent<SlotPaylineFrameGraphic>();
                if (oldBorder != null && ComponentUtility.CopyComponent(oldBorder)) ComponentUtility.PasteComponentValues(border);
                else
                {
                    border.color = new Color32(0xA3, 0xF2, 0x13, 0xFF);
                    border.ConfigureForEditor(4f, 2f);
                }
                Stretch(borderRect, -4f);
                summary.AppendLine(oldBorder != null ? " moved the payline border to PaylineFrame/ProceduralBorder" : " created PaylineFrame/ProceduralBorder");
            }
            if (oldBorder != null)
            {
                UnityEngine.Object.DestroyImmediate(oldBorder);
                var renderer = payline.GetComponent<CanvasRenderer>();
                if (renderer != null) UnityEngine.Object.DestroyImmediate(renderer);
            }
            border.raycastTarget = false;
            if (!border.enabled) border.enabled = true;
            var ignore = payline.GetComponent<LayoutElement>() ?? payline.gameObject.AddComponent<LayoutElement>();
            ignore.ignoreLayout = true;

            // SkinImage: disabled until a skin with a payline sprite is applied.
            var imageRect = Child("SkinImage", payline, 1);
            var image = imageRect.GetComponent<Image>();
            if (image == null)
            {
                image = imageRect.gameObject.AddComponent<Image>();
                image.type = Image.Type.Sliced;
                image.color = Color.white;
                image.enabled = false;
                Stretch(imageRect, 0f);
                summary.AppendLine(" created PaylineFrame/SkinImage");
            }
            image.raycastTarget = false;

            var content = root.transform;
            panel.ConfigureSkinForEditor(catalog, payline, border, image,
                GraphicAt(content, "Background"),
                GraphicAt(content, "Content/ReelMachine/Frame"),
                GraphicAt(content, "Content/ReelMachine/Frame/LeftEdge"),
                GraphicAt(content, "Content/ReelMachine/Frame/RightEdge"),
                GraphicAt(content, "Content/SummaryArea/Background"),
                GraphicAt(content, "Content/SummaryArea/TotalBox/Background"),
                false);
            var table = panel.RuneTableForEditor ?? throw new InvalidOperationException("SlotMachinePanel has no rune table.");
            table.ConfigureSkinForEditor(GraphicAt(content, "Content/SummaryArea/RuneTableArea/RuneTable/Background"), false);
            // Nested SlotReel instances take their defaults from SlotReel.prefab (saved above); this only fills gaps.
            foreach (var view in panel.ReelViews.OfType<SlotReelView>())
                view.ConfigureSkinForEditor(GraphicAt(view.transform, "Frame"), GraphicAt(view.transform, "Window/ShadeOverlay"), false);
        }

        // ---------- Scene ----------

        // Only overrides of the moved payline graphic (or of the PaylineFrame CanvasRenderer) conflict with the new
        // hierarchy; every other TMP_MainScene override is kept.
        private static void CleanSceneOverrides(Scene scene, StringBuilder summary)
        {
            var panel = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<SlotMachinePanel>(true)).SingleOrDefault();
            if (panel == null) { summary.AppendLine(" TMP_MainScene has no SlotMachinePanel"); return; }
            var instanceRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(panel.gameObject);
            if (instanceRoot == null) { summary.AppendLine(" the TMP_MainScene SlotMachinePanel is not a Prefab instance"); return; }
            var modifications = PrefabUtility.GetPropertyModifications(instanceRoot) ?? Array.Empty<PropertyModification>();
            var kept = modifications.Where(m => !IsPaylineLeftover(m)).ToArray();
            if (kept.Length == modifications.Length) { summary.AppendLine(" TMP_MainScene: no overrides conflict with the payline hierarchy; not saved"); return; }
            PrefabUtility.SetPropertyModifications(instanceRoot, kept);
            EditorSceneManager.MarkSceneDirty(scene);
            summary.AppendLine($" TMP_MainScene: removed {modifications.Length - kept.Length} overrides of the moved payline graphic; other overrides kept");
        }

        // An override whose target object no longer exists in the Prefab and that names a SlotPaylineFrameGraphic or
        // Graphic field (the graphic and CanvasRenderer removed from the PaylineFrame root).
        private static bool IsPaylineLeftover(PropertyModification modification) =>
            modification.target == null && (modification.propertyPath == "thickness" || modification.propertyPath == "minimumPixelThickness" ||
                modification.propertyPath.StartsWith("m_Color", StringComparison.Ordinal) || modification.propertyPath == "m_RaycastTarget");

        public static void Validate()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<SlotMachinePanelSkinCatalog>(CatalogPath);
            Require(catalog != null && catalog.Validate(out _), "the skin catalog exists and is valid");
            Require(catalog.TryGet(DefaultSkinId, out _) && catalog.TryGet(SampleSkinId, out var sample) && sample.HasPaylineImage, "default and sample skins are registered");
            Require(AssetDatabase.LoadAssetAtPath<Sprite>(SampleSpritePath).border != Vector4.zero, "the sample payline sprite has a 9-slice border");
            var reel = AssetDatabase.LoadAssetAtPath<GameObject>(TemporarySlotMachineIntegration.ReelPrefabPath).GetComponent<SlotReelView>();
            Require(reel.FrameSkinForEditor.HasDefaults && reel.ShadeSkinForEditor.HasDefaults, "SlotReel skin defaults are recorded");
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(TemporarySlotMachineIntegration.PanelPrefabPath);
            var panel = root.GetComponent<SlotMachinePanel>();
            Require(panel.SkinCatalog == catalog, "SlotMachinePanel references the skin catalog");
            var payline = root.transform.Find("Content/ReelMachine/PaylineFrame");
            Require(payline.GetComponent<Graphic>() == null && panel.PaylineFrameRect == payline, "PaylineFrame is a layout-only root");
            Require(panel.PaylineBorderForEditor != null && panel.PaylineBorderForEditor.transform.parent == payline && panel.PaylineBorderForEditor.enabled,
                "the code-drawn border is PaylineFrame/ProceduralBorder");
            Require(panel.PaylineImageForEditor != null && panel.PaylineImageForEditor.transform.parent == payline && !panel.PaylineImageForEditor.enabled,
                "PaylineFrame/SkinImage exists and is disabled");
            Require(panel.PanelSkinTargetsForEditor.All(t => t.HasDefaults) && panel.PanelTextTargetsForEditor.All(t => t.HasDefaults) &&
                panel.PaylineBorderSkinForEditor.HasDefaults, "panel skin defaults are recorded");
            var table = panel.RuneTableForEditor;
            Require(table.BackgroundSkinForEditor.HasDefaults && table.RowTemplateForEditor.BackgroundSkinForEditor.HasDefaults &&
                table.RowTemplateForEditor.EffectTextSkinForEditor.HasDefaults, "rune table skin defaults are recorded");
            Require(root.GetComponentsInChildren<Graphic>(true).All(g => !g.raycastTarget), "no graphic takes input");
        }

        // ---------- Helpers ----------

        private static Graphic GraphicAt(Transform root, string path)
        {
            var target = root.Find(path) ?? throw new InvalidOperationException($"'{path}' is missing under {root.name}.");
            return target.GetComponent<Graphic>() ?? throw new InvalidOperationException($"'{path}' under {root.name} has no Graphic.");
        }

        private static RectTransform Child(string name, Transform parent, int index)
        {
            var existing = parent.Find(name) as RectTransform;
            if (existing == null)
            {
                existing = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
                existing.SetParent(parent, false);
            }
            if (existing.GetSiblingIndex() != Mathf.Min(index, parent.childCount - 1)) existing.SetSiblingIndex(index);
            return existing;
        }

        // Stretches over the parent with the same inset on every side (negative extends outward).
        private static void Stretch(RectTransform rect, float inset)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.pivot = new Vector2(.5f, .5f);
            rect.offsetMin = new Vector2(inset, inset); rect.offsetMax = new Vector2(-inset, -inset);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path)!.Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException($"Slot machine panel skin check failed: {message}.");
        }
    }
}
