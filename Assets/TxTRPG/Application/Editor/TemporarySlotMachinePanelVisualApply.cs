using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using TxTRPG.Application.SlotMachine;
using TxTRPG.UI.SlotMachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TxTRPG.Application.Editor
{
    // TEMPORARY application menu (slot-machine-panel-visual-and-spin-motion). Partially edits the developer-owned
    // SlotReel.prefab and SlotMachinePanel.prefab into the reel machine + summary area layout, creates the gradient
    // sprites and DefaultSlotReelSpinProfile.asset when missing, and removes the TMP_MainScene overrides that conflict
    // with the new hierarchy (unused overrides of removed objects and the old layout-driven ReelArea rect).
    // Never regenerates a Prefab: missing objects are added, the properties named in the instruction document are set,
    // and a Prefab is saved only when something changed, so rerunning is safe. Old generated objects (PaylineOverlay,
    // Summary) are removed only when they still hold exactly their generated children and components; otherwise they
    // are deactivated and reported. Excluded from the registered Prefab rebuild batch because it also edits a Scene and
    // the Prefabs are developer-owned. Retire it once every working copy has been migrated.
    public static class TemporarySlotMachinePanelVisualApply
    {
        public const string MenuPath = "Tools/TxT RPG/Application/Temporary/Apply Slot Machine Panel Visual";
        public const string StyleFolder = "Assets/TxTRPG/UI/Styles/SlotMachine";
        public const string ProfilePath = StyleFolder + "/DefaultSlotReelSpinProfile.asset";
        public const string CylinderSpritePath = StyleFolder + "/SlotReelCylinderGradient.png";
        public const string ShadeSpritePath = StyleFolder + "/SlotReelEdgeShade.png";
        public const int VisibleCells = 5;
        public const int AfterimagesPerCell = SlotReelSpinSettings.MaximumAfterimageCount;
        private const float ContentTopInset = 44f;

        private static readonly Color PanelGray = Hex(0x75, 0x75, 0x75);
        private static readonly Color FrameBlack = Hex(0x0A, 0x0A, 0x0A);
        private static readonly Color ReelGray = Hex(0x80, 0x80, 0x80);
        private static readonly Color DividerLight = Hex(0xDC, 0xDC, 0xDC);
        private static readonly Color PaylineGreen = Hex(0xA3, 0xF2, 0x13);
        private static readonly Color SummaryLight = Hex(0xD9, 0xD9, 0xD9);
        private static readonly Color ShadeColor = new(0f, 0f, 0f, .75f);

        [MenuItem(MenuPath)]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before applying the slot machine panel visual.");
            if (EditorApplication.isCompiling) throw new InvalidOperationException("Wait for script compilation to finish.");
            if (PrefabStageUtility.GetCurrentPrefabStage() != null) throw new InvalidOperationException("Save and close Prefab Stage first.");
            var scene = SceneManager.GetSceneByPath(TemporarySlotMachineIntegration.ScenePath);
            var opened = !scene.IsValid() || !scene.isLoaded;
            if (!opened && scene.isDirty) throw new InvalidOperationException("Save or revert unsaved TMP_MainScene changes before applying the slot machine panel visual.");
            if (SceneManager.sceneCount > 0 && Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty))
                throw new InvalidOperationException("Save or revert the unsaved open Scenes before applying the slot machine panel visual.");
            var summary = new StringBuilder("SLOT_MACHINE_PANEL_VISUAL_APPLY:\n");
            // Prefabs are edited with an empty Scene open, so open instances (which may lag behind the Prefab) do not
            // run canvas updates during the save. The open Scenes are all saved, so restoring them loses nothing.
            var setup = EditorSceneManager.GetSceneManagerSetup();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            try { ApplyAssetsAndPrefabs(summary); }
            finally { if (setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup); }
            scene = SceneManager.GetSceneByPath(TemporarySlotMachineIntegration.ScenePath);
            opened = !scene.IsValid() || !scene.isLoaded;
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

        // Used by Apply Slot Machine Combat after it creates a missing Prefab skeleton.
        public static void ApplyAssetsAndPrefabs(StringBuilder summary)
        {
            EnsureFolder(StyleFolder);
            var cylinder = EnsureGradientSprite(CylinderSpritePath, CylinderPixel, summary);
            var shade = EnsureGradientSprite(ShadeSpritePath, ShadePixel, summary);
            var profile = EnsureProfile(summary);
            EditPrefab(TemporarySlotMachineIntegration.ReelPrefabPath, root => ApplyReel(root, cylinder, shade), summary);
            var reelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TemporarySlotMachineIntegration.ReelPrefabPath).GetComponent<SlotReelView>();
            EditPrefab(TemporarySlotMachineIntegration.PanelPrefabPath, root => ApplyPanel(root, reelPrefab, cylinder, profile, summary), summary);
        }

        // ---------- Assets ----------

        // Vertical cylinder shading: black at the top and bottom edges, white over the middle. Tinted by Image.color.
        private static Color CylinderPixel(float y)
        {
            var g = Mathf.SmoothStep(0f, 1f, Mathf.Min(y, 1f - y) / .45f);
            return new Color(g, g, g, 1f);
        }

        // Edge shade: opaque black at the top and bottom, transparent from 30% inward. Its strength is the Image alpha.
        private static Color ShadePixel(float y) =>
            new(0f, 0f, 0f, 1f - Mathf.SmoothStep(0f, 1f, Mathf.Min(y, 1f - y) / .3f));

        private static Sprite EnsureGradientSprite(string path, Func<float, Color> pixel, StringBuilder summary)
        {
            if (!File.Exists(path))
            {
                var texture = new Texture2D(4, 256, TextureFormat.RGBA32, false);
                for (var y = 0; y < texture.height; y++)
                {
                    var color = pixel(y / (texture.height - 1f));
                    for (var x = 0; x < texture.width; x++) texture.SetPixel(x, y, color);
                }
                File.WriteAllBytes(path, texture.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                summary.AppendLine($" created {path}");
            }
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer.textureType != TextureImporterType.Sprite || importer.mipmapEnabled || importer.wrapMode != TextureWrapMode.Clamp ||
                importer.textureCompression != TextureImporterCompression.Uncompressed || importer.filterMode != FilterMode.Bilinear)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
                summary.AppendLine($" set sprite import settings of {path}");
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path) ?? throw new InvalidOperationException($"{path} is not a Sprite.");
        }

        private static SlotReelSpinProfile EnsureProfile(StringBuilder summary)
        {
            var profile = AssetDatabase.LoadAssetAtPath<SlotReelSpinProfile>(ProfilePath);
            if (profile != null) return profile;
            profile = ScriptableObject.CreateInstance<SlotReelSpinProfile>();
            AssetDatabase.CreateAsset(profile, ProfilePath);
            AssetDatabase.SaveAssets();
            summary.AppendLine($" created {ProfilePath} with the default values");
            return profile;
        }

        // Loads the Prefab, applies the edit and saves only when the serialized state changed.
        private static void EditPrefab(string path, Action<GameObject> edit, StringBuilder summary)
        {
            // Loading a Prefab silently adds a missing CanvasRenderer in memory, so only the stored file shows it.
            var missingRenderer = StoredGraphicLacksRenderer(path);
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var before = Snapshot(root);
                edit(root);
                foreach (var graphic in root.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
                foreach (var text in root.GetComponentsInChildren<TMP_Text>(true)) TemporarySlotMachineIntegration.SyncTextState(text);
                if (!missingRenderer && Snapshot(root) == before) { summary.AppendLine($" {path} already matches; not saved"); return; }
                PrefabUtility.SaveAsPrefabAsset(root, path, out var saved);
                if (!saved) throw new InvalidOperationException($"Could not save {path}.");
                summary.AppendLine($" updated {path}");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        // True when a stored GameObject has a Graphic script but no CanvasRenderer (class ID 222).
        private static bool StoredGraphicLacksRenderer(string path)
        {
            if (!File.Exists(path)) return false;
            var documents = File.ReadAllText(path).Split(new[] { "--- !u!" }, StringSplitOptions.RemoveEmptyEntries);
            var classes = new Dictionary<string, string>();
            var scripts = new Dictionary<string, string>();
            foreach (var document in documents)
            {
                var header = System.Text.RegularExpressions.Regex.Match(document, @"^(\d+) &(-?\d+)");
                if (!header.Success) continue;
                classes[header.Groups[2].Value] = header.Groups[1].Value;
                var script = System.Text.RegularExpressions.Regex.Match(document, @"m_Script: \{fileID: \d+, guid: ([0-9a-f]+)");
                if (script.Success) scripts[header.Groups[2].Value] = script.Groups[1].Value;
            }
            foreach (var document in documents.Where(d => d.StartsWith("1 &")))
            {
                var ids = System.Text.RegularExpressions.Regex.Matches(document, @"component: \{fileID: (-?\d+)\}")
                    .Cast<System.Text.RegularExpressions.Match>().Select(m => m.Groups[1].Value).ToArray();
                var hasGraphic = ids.Any(id => scripts.TryGetValue(id, out var guid) &&
                    typeof(Graphic).IsAssignableFrom(AssetDatabase.LoadAssetAtPath<MonoScript>(AssetDatabase.GUIDToAssetPath(guid))?.GetClass()));
                if (hasGraphic && !ids.Any(id => classes.TryGetValue(id, out var type) && type == "222")) return true;
            }
            return false;
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

        // ---------- SlotReel.prefab ----------

        private static void ApplyReel(GameObject root, Sprite cylinder, Sprite shade)
        {
            var rect = (RectTransform)root.transform;
            if (root.GetComponent<LayoutElement>() == null)
            {
                var element = root.AddComponent<LayoutElement>();
                element.minWidth = 40f; element.flexibleWidth = 1f; element.flexibleHeight = 1f;
            }
            var view = root.GetComponent<SlotReelView>() ?? root.AddComponent<SlotReelView>();
            var frame = Child("Frame", rect, 0);
            Stretch(frame);
            SetImage(frame, cylinder, ReelGray);
            var window = Child("Window", rect, 1);
            SetRect(window, new Vector2(0f, .08f), new Vector2(1f, .92f));
            if (window.GetComponent<RectMask2D>() == null) window.gameObject.AddComponent<RectMask2D>();
            var strip = Child("Strip", window, 0);
            Stretch(strip);
            var font = FontOf(root);
            var cellCount = VisibleCells + 2;
            var cells = new RectTransform[cellCount];
            var roots = new RectTransform[cellCount];
            var texts = new TMP_Text[cellCount];
            var icons = new Image[cellCount];
            var afterimages = new SlotCellAfterimages[cellCount];
            for (var i = 0; i < cellCount; i++)
            {
                cells[i] = Child($"SymbolCell_{i}", strip, i);
                var visual = Child("VisualRoot", cells[i], 0);
                Stretch(visual);
                var iconRect = Child("Icon", visual, 0);
                var created = iconRect.GetComponent<Image>() == null;
                if (created) Stretch(iconRect, 4f, 4f, 4f, 4f);
                icons[i] = iconRect.GetComponent<Image>() ?? iconRect.gameObject.AddComponent<Image>();
                if (created) { icons[i].preserveAspect = true; icons[i].enabled = false; }
                // Afterimages render behind the value text: Icon, Afterimage_3, Afterimage_2, Afterimage_1, Value.
                var images = new TMP_Text[AfterimagesPerCell];
                for (var j = AfterimagesPerCell; j >= 1; j--)
                {
                    var afterimage = Child($"Afterimage_{j}", visual, 1 + AfterimagesPerCell - j);
                    var text = afterimage.GetComponent<TMP_Text>();
                    if (text == null)
                    {
                        Stretch(afterimage);
                        text = CreateText(afterimage, "1", font, TextAlignmentOptions.Center, new Color(1f, 1f, 1f, 0f));
                        text.enabled = false;
                    }
                    images[j - 1] = text;
                }
                afterimages[i] = new SlotCellAfterimages(images);
                var value = Child("Value", visual, 1 + AfterimagesPerCell);
                var valueText = value.GetComponent<TMP_Text>();
                if (valueText == null)
                {
                    Stretch(value);
                    valueText = CreateText(value, "1", font, TextAlignmentOptions.Center, Color.white);
                }
                valueText.color = Color.white;
                valueText.textWrappingMode = TextWrappingModes.NoWrap;
                valueText.overflowMode = TextOverflowModes.Overflow;
                texts[i] = valueText;
                roots[i] = visual;
            }
            var overlay = Child("ShadeOverlay", window, 1);
            Stretch(overlay);
            SetImage(overlay, shade, ShadeColor);
            var effect = Child("EffectRoot", rect, 2);
            Stretch(effect);
            view.ConfigureForEditor(window, strip, VisibleCells, cells, roots, texts, icons, afterimages);
            SetFloatArray(view, "distanceAlpha", 1f, .8f, .4f);
            SetFloatArray(view, "distanceFontScale", 1f, 1f, 1f);
        }

        // ---------- SlotMachinePanel.prefab ----------

        private static void ApplyPanel(GameObject root, SlotReelView reelPrefab, Sprite cylinder, SlotReelSpinProfile profile, StringBuilder summary)
        {
            var rect = (RectTransform)root.transform;
            var panel = root.GetComponent<SlotMachinePanel>() ?? throw new InvalidOperationException("SlotMachinePanel.prefab has no SlotMachinePanel.");
            var font = FontOf(root);
            var background = Child("Background", rect, 0);
            SetImage(background, null, PanelGray);

            // Content: SlotMachineContentLayout replaces the old VerticalLayoutGroup.
            var content = Child("Content", rect, 1);
            Stretch(content);
            var vertical = content.GetComponent<VerticalLayoutGroup>();
            if (vertical != null) UnityEngine.Object.DestroyImmediate(vertical);
            var layout = content.GetComponent<SlotMachineContentLayout>() ?? content.gameObject.AddComponent<SlotMachineContentLayout>();
            // The top inset leaves room for the boundary action button, which may cover the panel's top center.
            layout.padding = new RectOffset(12, 12, (int)ContentTopInset, 8);

            // Reel machine: black frame with bright side bands, reels with dividers, payline frame overlay.
            var machine = Child("ReelMachine", content, 0);
            var frame = Child("Frame", machine, 0);
            Stretch(frame);
            SetImage(frame, null, FrameBlack);
            var leftEdge = Child("LeftEdge", frame, 0);
            SetRect(leftEdge, Vector2.zero, new Vector2(.027f, 1f));
            SetImage(leftEdge, cylinder, DividerLight);
            var rightEdge = Child("RightEdge", frame, 1);
            SetRect(rightEdge, new Vector2(.973f, 0f), Vector2.one);
            SetImage(rightEdge, cylinder, DividerLight);

            var reelArea = FindDeep(rect, "ReelArea") ?? Child("ReelArea", machine, 1);
            if (reelArea.parent != machine) reelArea.SetParent(machine, false);
            reelArea.SetSiblingIndex(1);
            SetRect(reelArea, new Vector2(.027f, .04f), new Vector2(.973f, .96f));
            var oldElement = reelArea.GetComponent<LayoutElement>();
            if (oldElement != null) UnityEngine.Object.DestroyImmediate(oldElement);
            var oldHorizontal = reelArea.GetComponent<HorizontalLayoutGroup>();
            if (oldHorizontal != null && !(oldHorizontal is SlotReelAreaLayout)) UnityEngine.Object.DestroyImmediate(oldHorizontal);
            var areaLayout = reelArea.GetComponent<SlotReelAreaLayout>() ?? reelArea.gameObject.AddComponent<SlotReelAreaLayout>();
            areaLayout.padding = new RectOffset();
            areaLayout.spacing = 0f;
            areaLayout.childAlignment = TextAnchor.MiddleCenter;
            areaLayout.childControlWidth = areaLayout.childControlHeight = true;
            areaLayout.childForceExpandWidth = false;
            areaLayout.childForceExpandHeight = true;
            RemoveIfPristine(reelArea.Find("PaylineOverlay"), new Dictionary<string, Type[]>
            {
                [string.Empty] = new[] { typeof(RectTransform), typeof(LayoutElement) },
                ["TopLine"] = new[] { typeof(RectTransform), typeof(CanvasRenderer), typeof(Image) },
                ["BottomLine"] = new[] { typeof(RectTransform), typeof(CanvasRenderer), typeof(Image) },
                ["LeftMarker"] = new[] { typeof(RectTransform), typeof(CanvasRenderer), typeof(Image) },
                ["RightMarker"] = new[] { typeof(RectTransform), typeof(CanvasRenderer), typeof(Image) }
            }, summary);

            var views = reelArea.GetComponentsInChildren<SlotReelViewBase>(true).Where(v => v.transform.parent == reelArea).ToList();
            while (views.Count < 3)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(reelPrefab.gameObject, reelArea);
                instance.name = $"SlotReel_{views.Count + 1}";
                views.Add(instance.GetComponent<SlotReelViewBase>());
            }
            var dividers = new List<RectTransform>();
            for (var i = 0; i < views.Count - 1; i++)
            {
                var divider = Child($"ReelDivider_{i + 1}", reelArea, -1);
                SetImage(divider, cylinder, DividerLight);
                var element = divider.GetComponent<LayoutElement>() ?? divider.gameObject.AddComponent<LayoutElement>();
                element.minWidth = 4f; element.flexibleWidth = .16f; element.flexibleHeight = 1f;
                dividers.Add(divider);
            }
            for (var i = 0; i < views.Count; i++)
            {
                views[i].transform.SetSiblingIndex(i * 2);
                if (i < dividers.Count) dividers[i].SetSiblingIndex(i * 2 + 1);
            }

            var paylineRect = Child("PaylineFrame", machine, 2);
            // After Apply Slot Machine Panel Skin the border lives on PaylineFrame/ProceduralBorder (layout-only root).
            var paylineHost = paylineRect.Find("ProceduralBorder") as RectTransform ?? paylineRect;
            if (paylineHost.GetComponent<CanvasRenderer>() == null) paylineHost.gameObject.AddComponent<CanvasRenderer>();
            var payline = paylineHost.GetComponent<SlotPaylineFrameGraphic>() ?? paylineHost.gameObject.AddComponent<SlotPaylineFrameGraphic>();
            payline.color = PaylineGreen;
            payline.ConfigureForEditor(4f, 2f);
            var ignore = paylineRect.GetComponent<LayoutElement>() ?? paylineRect.gameObject.AddComponent<LayoutElement>();
            ignore.ignoreLayout = true;

            // Summary area: light background, total box, rune line table with column titles.
            var summaryArea = Child("SummaryArea", content, 1);
            var summaryBackground = Child("Background", summaryArea, 0);
            Stretch(summaryBackground);
            SetImage(summaryBackground, null, SummaryLight);
            var totalBox = Child("TotalBox", summaryArea, 1);
            var totalBackground = Child("Background", totalBox, 0);
            Stretch(totalBackground);
            SetImage(totalBackground, null, PanelGray);
            // The box shows the value only: TotalLabel stays (empty and inactive) for the display contract, and the value
            // takes the whole text area, centered.
            var totalLabel = EnsureText(totalBox, "TotalLabel", 1, new Vector2(.05f, 0f), new Vector2(.5f, 1f), SlotCombatStoryFormatter.TotalLabel, font, Color.white, .55f, 14f, 72f);
            totalLabel.gameObject.SetActive(!string.IsNullOrEmpty(SlotCombatStoryFormatter.TotalLabel));
            var totalValue = EnsureText(totalBox, "TotalValue", 2, new Vector2(.05f, 0f), new Vector2(.95f, 1f), SlotCombatStoryFormatter.IdleTotalValue, font, Color.white, .55f, 14f, 72f);
            totalValue.alignment = TextAlignmentOptions.Center;

            var tableArea = Child("RuneTableArea", summaryArea, 2);
            var table = Child("RuneTable", tableArea, 0);
            SetRect(table, Vector2.zero, new Vector2(1f, .935f));
            var tableBackground = Child("Background", table, 0);
            Stretch(tableBackground);
            SetImage(tableBackground, null, PanelGray);
            var rows = Child("Rows", table, 1);
            SetRect(rows, new Vector2(.046f, .044f), new Vector2(.954f, .948f));
            var tableView = rows.GetComponent<SlotRuneTableView>() ?? rows.gameObject.AddComponent<SlotRuneTableView>();
            var template = EnsureRowTemplate(rows, font);
            tableView.ConfigureForEditor(template, Array.Empty<SlotRuneLineRow>());
            var header = Child("RuneTableHeader", tableArea, 1);
            SetRect(header, new Vector2(0f, .89f), Vector2.one);
            var runeTitle = EnsureText(header, "RuneColumnTitle", 0, Vector2.zero, new Vector2(.48f, 1f), SlotCombatStoryFormatter.RuneColumnTitle, font, Color.black, .8f, 10f, 36f);
            var valueTitle = EnsureText(header, "ValueColumnTitle", 1, new Vector2(.48f, 0f), Vector2.one, SlotCombatStoryFormatter.ValueColumnTitle, font, Color.black, .8f, 10f, 36f);

            RemoveIfPristine(content.Find("Summary"), new Dictionary<string, Type[]>
            {
                [string.Empty] = new[] { typeof(RectTransform), typeof(VerticalLayoutGroup) },
                ["TotalText"] = new[] { typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI), typeof(LayoutElement) },
                ["AllocationText"] = new[] { typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI), typeof(LayoutElement) }
            }, summary);

            layout.ConfigureForEditor(machine, summaryArea, totalBox, tableArea);
            var status = Child("StatusText", rect, 2);
            var statusText = status.GetComponent<TMP_Text>();
            if (statusText == null)
            {
                Stretch(status, 16f, 16f, 16f, 16f);
                statusText = CreateText(status, string.Empty, font, TextAlignmentOptions.Center, Color.white);
                statusText.fontSize = 16f;
                status.gameObject.SetActive(false);
            }
            Stretch(Child("EffectRoot", rect, 3));
            panel.ConfigureForEditor(reelArea, areaLayout, reelPrefab, views, dividers, payline, content.gameObject,
                totalLabel, totalValue, runeTitle, valueTitle, tableView, statusText, profile);
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
        }

        private static SlotRuneLineRow EnsureRowTemplate(RectTransform rows, TMP_FontAsset font)
        {
            var templateRect = Child("RuneLineRowTemplate", rows, 0);
            templateRect.anchorMin = new Vector2(0f, 1f);
            templateRect.anchorMax = Vector2.one;
            templateRect.pivot = new Vector2(.5f, 1f);
            if (templateRect.sizeDelta.y <= 0f) templateRect.sizeDelta = new Vector2(0f, 40f);
            var row = templateRect.GetComponent<SlotRuneLineRow>() ?? templateRect.gameObject.AddComponent<SlotRuneLineRow>();
            var background = Child("Background", templateRect, 0);
            Stretch(background);
            var image = SetImage(background, null, SummaryLight);
            var effect = EnsureText(templateRect, "EffectText", 1, Vector2.zero, new Vector2(.42f, 1f), "공격", font, Color.black, .6f, 10f, 48f);
            var value = EnsureText(templateRect, "ValueText", 2, new Vector2(.42f, 0f), Vector2.one, "×1", font, Color.black, .6f, 10f, 48f);
            var message = EnsureText(templateRect, "MessageText", 3, new Vector2(.04f, 0f), new Vector2(.96f, 1f), string.Empty, font, Color.black, .5f, 10f, 40f);
            // Unused row texts are emptied at runtime, never deactivated.
            foreach (var text in new[] { effect, value, message })
                if (!text.gameObject.activeSelf) text.gameObject.SetActive(true);
            row.ConfigureForEditor(image, effect, value, message);
            if (templateRect.gameObject.activeSelf) templateRect.gameObject.SetActive(false);
            return row;
        }

        private static TMP_Text EnsureText(RectTransform parent, string name, int index, Vector2 anchorMin, Vector2 anchorMax, string placeholder,
            TMP_FontAsset font, Color color, float ratio, float minimum, float maximum)
        {
            var rect = Child(name, parent, index);
            SetRect(rect, anchorMin, anchorMax);
            var text = rect.GetComponent<TMP_Text>() ?? CreateText(rect, placeholder, font, TextAlignmentOptions.Center, color);
            text.color = color;
            // The Korean fallback atlas is generated at the variable font's thin default weight; bold keeps the summary
            // legible on the gray boxes.
            text.fontStyle = FontStyles.Bold;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
            var scaled = rect.GetComponent<SlotHeightScaledText>() ?? rect.gameObject.AddComponent<SlotHeightScaledText>();
            scaled.ConfigureForEditor(ratio, minimum, maximum);
            return text;
        }

        // ---------- Scene ----------

        private static void CleanSceneOverrides(Scene scene, StringBuilder summary)
        {
            var panel = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<SlotMachinePanel>(true)).SingleOrDefault();
            if (panel == null) { summary.AppendLine(" TMP_MainScene has no SlotMachinePanel; run Apply Slot Machine Combat first"); return; }
            var instanceRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(panel.gameObject);
            if (instanceRoot == null) { summary.AppendLine(" the TMP_MainScene SlotMachinePanel is not a Prefab instance; no overrides to clean"); return; }
            var before = PrefabUtility.GetPropertyModifications(instanceRoot)?.Length ?? 0;
            // Overrides of objects removed from the Prefab (Summary, TotalText, AllocationText, PaylineOverlay).
            PrefabUtility.RemoveUnusedOverrides(new[] { instanceRoot }, InteractionMode.AutomatedAction);
            // The old VerticalLayoutGroup drove the ReelArea rect and the Scene recorded it; the new anchors must win.
            var reelArea = panel.ReelAreaForEditor;
            if (reelArea != null && PrefabUtility.GetPropertyModifications(instanceRoot)
                    .Any(m => m.target == PrefabUtility.GetCorrespondingObjectFromSource(reelArea)))
                PrefabUtility.RevertObjectOverride(reelArea, InteractionMode.AutomatedAction);
            var after = PrefabUtility.GetPropertyModifications(instanceRoot)?.Length ?? 0;
            if (after != before)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                summary.AppendLine($" TMP_MainScene: removed {before - after} SlotMachinePanel overrides of removed objects and the old ReelArea rect; other overrides kept");
            }
            else summary.AppendLine(" TMP_MainScene: no conflicting SlotMachinePanel overrides");
        }

        public static void Validate()
        {
            var reel = AssetDatabase.LoadAssetAtPath<GameObject>(TemporarySlotMachineIntegration.ReelPrefabPath);
            var view = reel.GetComponent<SlotReelView>();
            Require(view.VisibleCellCount == VisibleCells, "SlotReel shows five cells");
            Require(reel.transform.Find($"Window/Strip/SymbolCell_{VisibleCells + 1}/VisualRoot/Afterimage_1") != null, "SlotReel cells have afterimages");
            Require(reel.transform.Find("Window/ShadeOverlay") != null, "SlotReel has a ShadeOverlay");
            var panelRoot = AssetDatabase.LoadAssetAtPath<GameObject>(TemporarySlotMachineIntegration.PanelPrefabPath);
            var panel = panelRoot.GetComponent<SlotMachinePanel>();
            Require(panel.SpinProfileForEditor == AssetDatabase.LoadAssetAtPath<SlotReelSpinProfile>(ProfilePath), "SlotMachinePanel references DefaultSlotReelSpinProfile");
            Require(panel.ReelCount == 3 && panel.ActiveDividerCount == 2, "three reels and two dividers");
            Require(panel.PaylineFrameRect != null && panel.RuneTableForEditor != null, "payline frame and rune table are connected");
            Require(panelRoot.transform.Find("Content/ReelMachine/ReelArea") != null && panelRoot.transform.Find("Content/SummaryArea/TotalBox/TotalValue") != null,
                "the reel machine and summary hierarchy exists");
            Require(panelRoot.GetComponentsInChildren<Graphic>(true).All(g => !g.raycastTarget), "no graphic takes input");
        }

        // ---------- Helpers ----------

        private static void RemoveIfPristine(Transform target, Dictionary<string, Type[]> generated, StringBuilder summary)
        {
            if (target == null) return;
            var name = target.name;
            var pristine = Matches(target, generated[string.Empty]) && target.childCount == generated.Count - 1;
            if (pristine)
                foreach (Transform child in target)
                    pristine &= generated.TryGetValue(child.name, out var types) && child.childCount == 0 && Matches(child, types);
            if (pristine)
            {
                UnityEngine.Object.DestroyImmediate(target.gameObject);
                summary.AppendLine($" removed the generated {name}");
                return;
            }
            if (target.gameObject.activeSelf) target.gameObject.SetActive(false);
            Debug.LogWarning($"SLOT_MACHINE_PANEL_VISUAL_APPLY: {name} has children or components added after generation; it was deactivated, not removed. Review it in SlotMachinePanel.prefab.");
            summary.AppendLine($" deactivated {name} (it has added children or components)");
        }

        private static bool Matches(Transform target, Type[] types)
        {
            var components = target.GetComponents<Component>();
            return components.Length == types.Length && components.All(c => c != null && types.Contains(c.GetType()));
        }

        // Finds or creates a direct child; index >= 0 sets its sibling position, -1 keeps it (new children go last).
        private static RectTransform Child(string name, Transform parent, int index)
        {
            var existing = parent.Find(name) as RectTransform;
            if (existing == null)
            {
                existing = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
                existing.SetParent(parent, false);
            }
            if (index >= 0 && existing.GetSiblingIndex() != Mathf.Min(index, parent.childCount - 1))
                existing.SetSiblingIndex(index);
            return existing;
        }

        private static RectTransform FindDeep(Transform root, string name) =>
            root.GetComponentsInChildren<RectTransform>(true).FirstOrDefault(t => t.name == name && t != root);

        private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax)
        {
            rect.anchorMin = anchorMin; rect.anchorMax = anchorMax; rect.pivot = new Vector2(.5f, .5f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static void Stretch(RectTransform rect, float left = 0f, float top = 0f, float right = 0f, float bottom = 0f)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.pivot = new Vector2(.5f, .5f);
            rect.offsetMin = new Vector2(left, bottom); rect.offsetMax = new Vector2(-right, -top);
        }

        private static Image SetImage(RectTransform rect, Sprite sprite, Color color)
        {
            var image = rect.GetComponent<Image>() ?? rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.type = Image.Type.Simple;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static TMP_Text CreateText(RectTransform rect, string value, TMP_FontAsset font, TextAlignmentOptions alignment, Color color)
        {
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) text.font = font;
            text.text = value;
            text.fontSize = 24f;
            text.enableAutoSizing = false;
            text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.color = color;
            text.raycastTarget = false;
            return text;
        }

        private static TMP_FontAsset FontOf(GameObject root) =>
            root.GetComponentsInChildren<TMP_Text>(true).Select(t => t.font).FirstOrDefault(f => f != null) ?? TMP_Settings.defaultFontAsset;

        private static void SetFloatArray(UnityEngine.Object target, string property, params float[] values)
        {
            var serialized = new SerializedObject(target);
            var array = serialized.FindProperty(property) ?? throw new InvalidOperationException($"Field '{property}' is missing.");
            array.arraySize = values.Length;
            for (var i = 0; i < values.Length; i++) array.GetArrayElementAtIndex(i).floatValue = values[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path)!.Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        private static Color Hex(int r, int g, int b) => new Color32((byte)r, (byte)g, (byte)b, 255);

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException($"Slot machine panel visual check failed: {message}.");
        }
    }
}
