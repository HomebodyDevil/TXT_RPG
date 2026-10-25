using System;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using TxTRPG.Application.Runes;
using TxTRPG.UI;
using TxTRPG.UI.Runes;
using TxTRPG.UI.Shapes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TxTRPG.Application.Editor
{
    // TEMPORARY application menu (candidate-main-scene-rune-grid). Creates, only when missing: the temporary attack/heal
    // rune Sprites, TemporaryRuneIconCatalog.asset, RuneGridCard.prefab and RuneGridPanel.prefab, then places one RuneGrid
    // instance (with FlexibleLayoutItem, TemporaryRuneGridBinder and the catalog as an instance override) under
    // Candidate_MainScene Canvas/MainArea/ContentLayer/LeftArea/ContentLayer. Existing PNGs, assets and Prefabs are never
    // overwritten and an existing RuneGrid leaves the Scene untouched, so rerunning is safe. It stops without changes in
    // Play Mode, while compiling, in Prefab Stage, with unsaved open Scenes or when LeftArea/ContentLayer has another child.
    // Saves only what it created and Candidate_MainScene. Not part of the registered Prefab rebuild batch (one-time
    // application; both Prefabs are developer-owned afterwards). Retire it once the user has applied and verified the grid.
    public static class TemporaryRuneGridApply
    {
        public const string MenuPath = "Tools/TxT RPG/Application/Temporary/Apply Candidate Rune Grid";
        public const string ScenePath = "Assets/Scenes/Candidate_MainScene.unity";
        public const string IconFolder = "Assets/TxTRPG/UI/Icons/TemporaryRunes";
        public const string HitAreaName = "HitArea";
        public const string AttackIconPath = IconFolder + "/attack.png";
        public const string HealIconPath = IconFolder + "/heal.png";
        public const string CatalogPath = "Assets/TxTRPG/UI/Styles/Runes/TemporaryRuneIconCatalog.asset";
        public const string CardPrefabPath = "Assets/TxTRPG/UI/Prefabs/Runes/RuneGridCard.prefab";
        public const string PanelPrefabPath = "Assets/TxTRPG/UI/Prefabs/Runes/RuneGridPanel.prefab";
        public const string LeftContentPath = "MainArea/ContentLayer/LeftArea/ContentLayer";
        public const string InstanceName = "RuneGrid";
        public const float ScrollbarWidth = 10f;

        public static readonly Color CardColor = new Color32(0x1C, 0x27, 0x45, 0xFF);
        public static readonly Color BadgeColor = new Color32(0xC6, 0x28, 0x28, 0xFF);
        private static readonly Color AttackIconColor = new Color32(0xF5, 0x9E, 0x4B, 0xFF);
        private static readonly Color HealIconColor = new Color32(0x5C, 0xD6, 0x8A, 0xFF);

        [MenuItem(MenuPath)]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogError("RUNE_GRID_APPLY: exit Play Mode first. Nothing was changed."); return; }
            if (EditorApplication.isCompiling) { Debug.LogError("RUNE_GRID_APPLY: wait for script compilation to finish. Nothing was changed."); return; }
            if (PrefabStageUtility.GetCurrentPrefabStage() != null) { Debug.LogError("RUNE_GRID_APPLY: save and close Prefab Stage first. Nothing was changed."); return; }
            if (Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty))
            {
                Debug.LogError("RUNE_GRID_APPLY: save or revert the unsaved open Scenes first. Nothing was changed.");
                return;
            }
            var summary = new StringBuilder("RUNE_GRID_APPLY:\n");
            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                var leftContent = FindLeftContent(scene);
                if (leftContent == null) { Debug.LogError($"RUNE_GRID_APPLY: {ScenePath} has no Canvas/{LeftContentPath}. Nothing was changed."); return; }
                var existing = leftContent.Find(InstanceName);
                if (existing == null && leftContent.childCount > 0)
                {
                    Debug.LogError($"RUNE_GRID_APPLY: {LeftContentPath} already has other children ({string.Join(", ", leftContent.Cast<Transform>().Select(t => t.name))}). Nothing was changed; report this to the developer.");
                    return;
                }

                var attack = EnsureIcon(TemporaryRuneConfiguration.AttackRuneId, AttackIconPath, summary);
                var heal = EnsureIcon(TemporaryRuneConfiguration.HealRuneId, HealIconPath, summary);
                var catalog = EnsureCatalog(attack, heal, summary);
                var card = EnsureCardPrefab(summary);
                var panel = EnsurePanelPrefab(card, summary);
                if (!ValidatePanelPrefab(panel, out var error)) { Debug.LogError($"{summary} invalid {PanelPrefabPath}: {error}. Delete it and rerun to recreate it."); return; }

                if (existing != null)
                    summary.AppendLine($" kept existing {InstanceName} in {ScenePath}; Scene unchanged");
                else
                {
                    Place(leftContent, panel, catalog);
                    EditorSceneManager.MarkSceneDirty(scene);
                    if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException($"Could not save {ScenePath}.");
                    summary.AppendLine($" placed {InstanceName} under {LeftContentPath} and saved {ScenePath}");
                }
                AssetDatabase.SaveAssets();
            }
            catch (Exception exception)
            {
                Debug.LogError($"{summary} FAILED: {exception}");
                return;
            }
            finally
            {
                if (setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
            }
            summary.AppendLine(" validation: passed. Safe to rerun.");
            Debug.Log(summary.ToString());
        }

        public static Transform FindLeftContent(Scene scene)
        {
            var canvas = scene.GetRootGameObjects().FirstOrDefault(root => root.name == "Canvas");
            return canvas != null ? canvas.transform.Find(LeftContentPath) : null;
        }

        public static bool ValidatePanelPrefab(GameObject prefab, out string error)
        {
            var panel = prefab != null ? prefab.GetComponent<RuneGridPanel>() : null;
            if (panel == null) { error = "no RuneGridPanel on the root"; return false; }
            if (!panel.IsValid(out var missing)) { error = $"missing {missing}"; return false; }
            if (panel.ScrollRect.horizontal || !panel.ScrollRect.vertical) { error = "ScrollRect must scroll vertically only"; return false; }
            if (panel.Content.childCount > 0) { error = "Content must not store card instances"; return false; }
            var cardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CardPrefabPath);
            var card = cardPrefab != null ? cardPrefab.GetComponent<RuneGridCardView>() : null;
            if (card == null || !card.IsValid(out missing)) { error = "RuneGridCard.prefab is missing references"; return false; }
            // The transparent HitArea images (rune tooltip trigger) are the only input targets of a card.
            var raycast = cardPrefab.GetComponentsInChildren<Graphic>(true).FirstOrDefault(graphic => graphic.raycastTarget && graphic.name != HitAreaName);
            if (raycast != null) { error = $"card graphic '{raycast.name}' has raycastTarget on"; return false; }
            error = string.Empty;
            return true;
        }

        // ---------- Icons and catalog ----------

        private static Sprite EnsureIcon(string definitionId, string path, StringBuilder summary)
        {
            if (!File.Exists(path))
            {
                EnsureFolder(IconFolder);
                var attack = definitionId == TemporaryRuneConfiguration.AttackRuneId;
                WriteIcon(path, attack ? AttackIconColor : HealIconColor, attack ? (Func<float, float, bool>)Sword : Cross);
                summary.AppendLine($" created {path} (temporary {definitionId} icon)");
            }
            else summary.AppendLine($" kept {path}");
            return AssetDatabase.LoadAssetAtPath<Sprite>(path) ?? throw new InvalidOperationException($"{path} is not imported as a Sprite.");
        }

        private static RuneIconCatalog EnsureCatalog(Sprite attack, Sprite heal, StringBuilder summary)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<RuneIconCatalog>(CatalogPath);
            if (catalog != null) { summary.AppendLine($" kept {CatalogPath}"); return catalog; }
            EnsureFolder(Path.GetDirectoryName(CatalogPath).Replace('\\', '/'));
            catalog = ScriptableObject.CreateInstance<RuneIconCatalog>();
            catalog.SetEntries(new[]
            {
                new RuneIconCatalog.Entry(TemporaryRuneConfiguration.AttackRuneId, attack),
                new RuneIconCatalog.Entry(TemporaryRuneConfiguration.HealRuneId, heal)
            });
            AssetDatabase.CreateAsset(catalog, CatalogPath);
            summary.AppendLine($" created {CatalogPath} (temporary: rune.attack, rune.heal)");
            return catalog;
        }

        // 128×128 white-on-transparent coverage with 4×4 supersampling, tinted once; same import settings as the menu icons.
        internal static void WriteIcon(string path, Color color, Func<float, float, bool> contains)
        {
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
                                if (contains(x + (sx + .5f) / samples - 64f, y + (sy + .5f) / samples - 64f)) covered++;
                        var c = (Color32)color;
                        pixels[y * size + x] = new Color32(c.r, c.g, c.b, (byte)Mathf.RoundToInt(255f * covered / (samples * samples)));
                    }
                texture.SetPixels32(pixels);
                texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally { Object.DestroyImmediate(texture); }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 128f; importer.alphaIsTransparency = true; importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed; importer.maxTextureSize = 128;
            importer.filterMode = FilterMode.Bilinear; importer.wrapMode = TextureWrapMode.Clamp;
            importer.npotScale = TextureImporterNPOTScale.None; importer.isReadable = false;
            importer.SaveAndReimport();
        }

        // Diagonal sword (tip at the top right): blade with a point, cross guard, grip and pommel.
        private static bool Sword(float x, float y)
        {
            const float root2 = 1.41421356f;
            var u = (x + y) / root2;
            var v = Mathf.Abs(x - y) / root2;
            if (u >= -18f && u <= 44f && v <= 8f) return true;
            if (u > 44f && u <= 58f && v <= 8f * (58f - u) / 14f) return true;
            if (u >= -25f && u < -18f && v <= 23f) return true;
            if (u >= -46f && u < -25f && v <= 5f) return true;
            return (u + 50f) * (u + 50f) + v * v <= 49f;
        }

        // Plus-shaped healing cross.
        private static bool Cross(float x, float y) => RoundedBox(x, y, 46f, 15f, 5f) || RoundedBox(x, y, 15f, 46f, 5f);

        private static bool RoundedBox(float x, float y, float halfWidth, float halfHeight, float radius)
        {
            var dx = Mathf.Max(Mathf.Abs(x) - (halfWidth - radius), 0f);
            var dy = Mathf.Max(Mathf.Abs(y) - (halfHeight - radius), 0f);
            return Mathf.Abs(x) <= halfWidth && Mathf.Abs(y) <= halfHeight && dx * dx + dy * dy <= radius * radius;
        }

        // ---------- Prefabs ----------

        private static RuneGridCardView EnsureCardPrefab(StringBuilder summary)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(CardPrefabPath);
            if (asset != null) { summary.AppendLine($" kept {CardPrefabPath}"); return asset.GetComponent<RuneGridCardView>(); }
            EnsureFolder(Path.GetDirectoryName(CardPrefabPath).Replace('\\', '/'));
            var preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = NewUi("RuneGridCard", null);
                SceneManager.MoveGameObjectToScene(root.gameObject, preview);
                root.sizeDelta = new Vector2(100f, 100f);
                var view = root.gameObject.AddComponent<RuneGridCardView>();
                var visual = Stretch(NewUi("VisualRoot", root));
                var cardArea = NewUi("CardArea", visual);
                var background = Stretch(NewUi("CardBackground", cardArea)).gameObject.AddComponent<UiShapeGraphic>();
                background.Configure(UiShape.RoundedRectangle, 10f, UiShapeDrawMode.Fill, 0f);
                background.color = CardColor;
                background.raycastTarget = false;
                var icon = Inset(NewUi("Icon", cardArea), .14f).gameObject.AddComponent<Image>();
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                icon.gameObject.SetActive(false);
                var label = Text(Inset(NewUi("FallbackLabel", cardArea), .08f), 10f, 22f);
                label.textWrappingMode = TextWrappingModes.Normal;
                label.overflowMode = TextOverflowModes.Ellipsis;
                label.gameObject.SetActive(false);
                var badgeArea = NewUi("BadgeArea", visual);
                var badge = Stretch(NewUi("BadgeBackground", badgeArea)).gameObject.AddComponent<UiShapeGraphic>();
                badge.Configure(UiShape.RoundedRectangle, 6f, UiShapeDrawMode.Fill, 0f);
                badge.color = BadgeColor;
                badge.raycastTarget = false;
                var count = Text(Inset(NewUi("CountText", badgeArea), .08f), 6f, 28f);
                count.fontStyle = FontStyles.Bold;
                count.textWrappingMode = TextWrappingModes.NoWrap;
                count.overflowMode = TextOverflowModes.Overflow;
                Set(view, "visualRoot", visual);
                Set(view, "cardArea", cardArea);
                Set(view, "badgeArea", badgeArea);
                Set(view, "icon", icon);
                Set(view, "fallbackLabel", label);
                Set(view, "countText", count);
                view.ApplyProportions(RuneGridLayoutSettings.DefaultBadgeSizeRatio, RuneGridLayoutSettings.DefaultBadgeOverhangRatio);
                var saved = PrefabUtility.SaveAsPrefabAsset(root.gameObject, CardPrefabPath, out var success);
                if (!success) throw new InvalidOperationException($"Could not save {CardPrefabPath}.");
                summary.AppendLine($" created {CardPrefabPath}");
                return saved.GetComponent<RuneGridCardView>();
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
        }

        private static GameObject EnsurePanelPrefab(RuneGridCardView card, StringBuilder summary)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(PanelPrefabPath);
            if (asset != null) { summary.AppendLine($" kept {PanelPrefabPath}"); return asset; }
            var preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = Stretch(NewUi("RuneGridPanel", null));
                SceneManager.MoveGameObjectToScene(root.gameObject, preview);
                var panel = root.gameObject.AddComponent<RuneGridPanel>();
                var scrollView = Stretch(NewUi("Scroll View", root));
                var scroll = scrollView.gameObject.AddComponent<ScrollRect>();

                // The scrollbar strip is reserved outside the Viewport, so showing or hiding the scrollbar never changes
                // the Viewport width or the column count.
                var viewport = Stretch(NewUi("Viewport", scrollView));
                viewport.offsetMax = new Vector2(-ScrollbarWidth, 0f);
                viewport.gameObject.AddComponent<RectMask2D>();
                var hit = viewport.gameObject.AddComponent<Image>();
                hit.color = new Color(0f, 0f, 0f, 0f);
                hit.raycastTarget = true;

                var content = NewUi("Content", viewport);
                content.anchorMin = new Vector2(0f, 1f);
                content.anchorMax = new Vector2(1f, 1f);
                content.pivot = new Vector2(0f, 1f);
                content.offsetMin = content.offsetMax = Vector2.zero;
                var grid = content.gameObject.AddComponent<RuneGridLayoutGroup>();
                var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
                fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                var bar = NewUi("Scrollbar Vertical", scrollView);
                bar.anchorMin = new Vector2(1f, 0f);
                bar.anchorMax = Vector2.one;
                bar.pivot = new Vector2(1f, 1f);
                bar.offsetMin = new Vector2(-ScrollbarWidth, 0f);
                bar.offsetMax = Vector2.zero;
                var barImage = bar.gameObject.AddComponent<Image>();
                barImage.color = new Color(1f, 1f, 1f, .08f);
                var scrollbar = bar.gameObject.AddComponent<Scrollbar>();
                var area = Stretch(NewUi("Sliding Area", bar));
                var handle = Stretch(NewUi("Handle", area));
                var handleImage = handle.gameObject.AddComponent<Image>();
                handleImage.color = new Color(1f, 1f, 1f, .45f);
                scrollbar.handleRect = handle;
                scrollbar.targetGraphic = handleImage;
                scrollbar.direction = Scrollbar.Direction.BottomToTop;
                scrollbar.navigation = new Navigation { mode = Navigation.Mode.None };

                scroll.content = content;
                scroll.viewport = viewport;
                scroll.horizontal = false;
                scroll.vertical = true;
                scroll.movementType = ScrollRect.MovementType.Clamped;
                scroll.scrollSensitivity = 24f;
                scroll.verticalScrollbar = scrollbar;
                scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
                scroll.verticalScrollbarSpacing = 0f;

                var message = Text(Inset(NewUi("Message", root), .06f), 12f, 24f);
                message.textWrappingMode = TextWrappingModes.Normal;
                message.overflowMode = TextOverflowModes.Ellipsis;
                message.color = new Color(1f, 1f, 1f, .75f);
                message.gameObject.SetActive(false);

                Set(panel, "scrollRect", scroll);
                Set(panel, "viewport", viewport);
                Set(panel, "content", content);
                Set(panel, "layoutGroup", grid);
                Set(panel, "cardPrefab", card);
                Set(panel, "messageText", message);
                var saved = PrefabUtility.SaveAsPrefabAsset(root.gameObject, PanelPrefabPath, out var success);
                if (!success) throw new InvalidOperationException($"Could not save {PanelPrefabPath}.");
                summary.AppendLine($" created {PanelPrefabPath}");
                return saved;
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
        }

        // ---------- Scene ----------

        private static void Place(Transform leftContent, GameObject panelPrefab, RuneIconCatalog catalog)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(panelPrefab, leftContent);
            instance.name = InstanceName;
            var rect = (RectTransform)instance.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            instance.AddComponent<FlexibleLayoutItem>().Configure(FlexibleLayoutSizeMode.Weighted, 1f, 100f, 0f, 0f);
            var panel = instance.GetComponent<RuneGridPanel>();
            instance.AddComponent<TemporaryRuneGridBinder>().Configure(panel);
            Set(panel, "iconCatalog", catalog);
            PrefabUtility.RecordPrefabInstancePropertyModifications(panel);
        }

        // ---------- Helpers ----------

        private static RectTransform NewUi(string name, Transform parent)
        {
            var gameObject = new GameObject(name, typeof(RectTransform)) { layer = LayerMask.NameToLayer("UI") };
            var rect = (RectTransform)gameObject.transform;
            if (parent != null) rect.SetParent(parent, false);
            return rect;
        }

        private static RectTransform Stretch(RectTransform rect) => Inset(rect, 0f);

        private static RectTransform Inset(RectTransform rect, float ratio)
        {
            rect.anchorMin = new Vector2(ratio, ratio);
            rect.anchorMax = new Vector2(1f - ratio, 1f - ratio);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        private static TextMeshProUGUI Text(RectTransform rect, float minimumSize, float maximumSize)
        {
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            if (TMP_Settings.defaultFontAsset != null) text.font = TMP_Settings.defaultFontAsset;
            text.alignment = TextAlignmentOptions.Center;
            text.enableAutoSizing = true;
            text.fontSizeMin = minimumSize;
            text.fontSizeMax = maximumSize;
            text.color = Color.white;
            text.raycastTarget = false;
            text.text = string.Empty;
            return text;
        }

        private static void Set(Object target, string field, Object value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(field) ?? throw new InvalidOperationException($"{target.GetType().Name} has no field '{field}'.");
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            var parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
    }
}
