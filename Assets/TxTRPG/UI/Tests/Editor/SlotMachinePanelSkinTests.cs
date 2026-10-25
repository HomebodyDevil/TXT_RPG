using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using NUnit.Framework;
using TMPro;
using TxTRPG.UI.SlotMachine;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TxTRPG.UI.Tests
{
    // Skins are applied to a temporary instance of the stored SlotMachinePanel.prefab; skins, catalogs and sprites are
    // created in memory and destroyed afterwards. Nothing is saved.
    public sealed class SlotMachinePanelSkinTests
    {
        private const string PanelPrefabPath = "Assets/TxTRPG/UI/Prefabs/SlotMachine/SlotMachinePanel.prefab";
        private static readonly int[] OneToSix = { 1, 2, 3, 4, 5, 6 };
        private static readonly string[] ImageEntries =
        {
            "paylineFrame", "panelBackground", "reelMachineFrame", "reelMachineEdge", "reelBackground", "reelShadeOverlay",
            "reelDivider", "summaryBackground", "totalBoxBackground", "runeTableBackground", "runeRowBackground"
        };

        private readonly List<Object> created = new();
        private GameObject canvas;
        private SlotMachinePanel panel;

        [SetUp]
        public void SetUp()
        {
            canvas = new GameObject("SlotSkinCanvas", typeof(Canvas));
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PanelPrefabPath);
            Assert.That(prefab, Is.Not.Null, $"Missing prefab: {PanelPrefabPath}");
            panel = ((GameObject)PrefabUtility.InstantiatePrefab(prefab, canvas.transform)).GetComponent<SlotMachinePanel>();
            panel.SetSpinSettingsForEditor(SlotReelSpinSettings.Instant);
            panel.SetReels(Reels(3));
            panel.SetSummary(Summary(2));
            Arrange();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(canvas);
            foreach (var item in created) if (item != null) Object.DestroyImmediate(item);
            created.Clear();
        }

        [Test]
        public void NoSkinAndEmptySkin_ShowTheCodeBorderAndRecordedDefaults()
        {
            AssertDefaults("before any skin");
            Assert.That(panel.ActiveSkin, Is.Null);
            Assert.That(panel.ActiveSkinId, Is.Empty);
            panel.SetSkin(Skin("test.empty"));
            Assert.That(panel.ActiveSkinId, Is.EqualTo("test.empty"));
            AssertDefaults("empty skin");
        }

        [Test]
        public void HiddenTotalLabel_StillGetsTheSkinAndRecordsDefaults()
        {
            var label = panel.PanelTextTargetsForEditor[0];
            Assert.That(label.Text.name, Is.EqualTo("TotalLabel"));
            Assert.That(label.Text.gameObject.activeSelf, Is.False, "the empty summary label hides TotalLabel");
            Assert.That(label.HasDefaults, Is.True);
            var dark = new Color(.8f, .2f, .1f, 1f);
            panel.SetSkin(Skin("test.dark", s => SetColor(s, "darkSurfaceTextColor", dark)));
            Assert.That(label.Text.color, Is.EqualTo(dark), "the inactive label is recolored with the total value");
            Assert.That(panel.PanelTextTargetsForEditor[1].Text.color, Is.EqualTo(dark));
            Assert.That(label.Record(label.Text, true), Is.True, "defaults can be recorded while the label is inactive");
            panel.SetSkin(null);
            Assert.That(label.Text.color, Is.EqualTo(label.DefaultColor));
        }

        [Test]
        public void PaylineSprite_ShowsTheImageExpandedByTheOutset()
        {
            var sprite = Sprite();
            panel.SetSkin(Skin("test.image", s =>
            {
                s.FindProperty("paylineFrame.sprite").objectReferenceValue = sprite;
                s.FindProperty("paylineOutset.left").floatValue = 6f;
                s.FindProperty("paylineOutset.right").floatValue = 7f;
                s.FindProperty("paylineOutset.top").floatValue = 8f;
                s.FindProperty("paylineOutset.bottom").floatValue = 9f;
            }));
            panel.UpdatePaylineFrameForEditor();
            var image = panel.PaylineImageForEditor;
            Assert.That(panel.PaylineImageShown, Is.True);
            Assert.That(panel.PaylineBorderShown, Is.False, "never both payline displays");
            Assert.That(image.gameObject.activeSelf && panel.PaylineBorderForEditor.gameObject.activeSelf, Is.True, "GameObjects are kept");
            Assert.That(image.raycastTarget, Is.False);
            Assert.That(image.sprite, Is.SameAs(sprite));
            Assert.That(image.color, Is.EqualTo(Color.white), "a sprite without a color override keeps its own colors");
            var root = panel.PaylineFrameRect.rect;
            var shown = image.rectTransform;
            Assert.That(shown.rect.width, Is.EqualTo(root.width + 13f).Within(.01f));
            Assert.That(shown.rect.height, Is.EqualTo(root.height + 17f).Within(.01f));
            Assert.That(shown.offsetMin, Is.EqualTo(new Vector2(-6f, -9f)));
            Assert.That(shown.offsetMax, Is.EqualTo(new Vector2(7f, 8f)));
            Assert.That(panel.PaylineVisibleRect, Is.SameAs(shown));
        }

        [Test]
        public void PaylineColorOnly_RecolorsTheCodeBorder()
        {
            var color = new Color(1f, .2f, .1f, 1f);
            panel.SetSkin(Skin("test.color", s => SetColor(s, "paylineFrame", color)));
            Assert.That(panel.PaylineBorderShown, Is.True);
            Assert.That(panel.PaylineImageShown, Is.False);
            Assert.That(panel.PaylineBorderForEditor.color, Is.EqualTo(color));
            panel.SetSkin(null);
            Assert.That(panel.PaylineBorderForEditor.color, Is.EqualTo(panel.PaylineBorderSkinForEditor.DefaultColor));
        }

        [Test]
        public void SkinSwitches_EmptyEntriesReturnToDefaultsNotToThePreviousSkin()
        {
            var full = FullSkin("test.full");
            panel.SetSkin(full.Skin);
            var background = panel.PanelSkinTargetsForEditor[0].Graphic;
            var machineFrame = (Image)panel.PanelSkinTargetsForEditor[1].Graphic;
            Assert.That(machineFrame.sprite, Is.SameAs(full.Sprite));
            Assert.That(machineFrame.color, Is.EqualTo(full.Color));
            Assert.That(panel.PaylineImageShown, Is.True);

            var partialColor = new Color(.1f, .3f, .9f, 1f);
            panel.SetSkin(Skin("test.partial", s => SetColor(s, "panelBackground", partialColor)));
            Assert.That(background.color, Is.EqualTo(partialColor));
            var frameTarget = panel.PanelSkinTargetsForEditor[1];
            Assert.That(machineFrame.sprite == frameTarget.DefaultSprite, Is.True, "an empty entry shows the default, not the previous skin");
            Assert.That(machineFrame.color, Is.EqualTo(frameTarget.DefaultColor));
            Assert.That(panel.PaylineImageShown, Is.False);

            panel.SetSkin(full.Skin);
            panel.SetSkin(null);
            AssertDefaults("after null");
        }

        [Test]
        public void AddedReelsDividersAndRows_GetTheActiveSkin()
        {
            var full = FullSkin("test.full");
            panel.SetSkin(full.Skin);
            panel.SetReels(Reels(5));
            panel.SetSummary(Summary(6));
            Arrange();
            Assert.That(panel.ReelCount, Is.EqualTo(5));
            foreach (var view in panel.ReelViews.Cast<SlotReelView>())
            {
                Assert.That(((Image)view.FrameSkinForEditor.Graphic).sprite, Is.SameAs(full.Sprite), $"{view.name} frame");
                Assert.That(view.ShadeSkinForEditor.Graphic.color, Is.EqualTo(full.Color), $"{view.name} shade");
            }
            foreach (var divider in panel.ReelDividersForEditor)
                Assert.That(divider.GetComponent<Image>().sprite, Is.SameAs(full.Sprite), $"{divider.name}");
            var rows = panel.RuneTableForEditor.RowsForEditor.Where(r => r != null && r.gameObject.activeSelf).ToList();
            Assert.That(rows.Count, Is.GreaterThan(2));
            foreach (var row in rows)
            {
                Assert.That(((Image)row.BackgroundSkinForEditor.Graphic).sprite, Is.SameAs(full.Sprite), $"{row.name} background");
                Assert.That(row.EffectTextSkinForEditor.Text.color, Is.EqualTo(full.TextColor), $"{row.name} text");
            }
            Assert.That(panel.PaylineImageShown, Is.True);
            panel.UpdatePaylineFrameForEditor();
            var frame = WorldRect(panel.PaylineFrameRect);
            var first = WorldRect(((SlotReelView)panel.ReelViews[0]).WindowForEditor);
            var last = WorldRect(((SlotReelView)panel.ReelViews[4]).WindowForEditor);
            Assert.That(frame.xMin, Is.EqualTo(first.xMin).Within(.5f));
            Assert.That(frame.xMax, Is.EqualTo(last.xMax).Within(.5f), "the image payline spans all five reels");
        }

        [Test]
        public void SkinDuringMotion_KeepsTheResultAndMultipliesTextAlpha()
        {
            var reelText = new Color(1f, .5f, 0f, .5f);
            var skin = Skin("test.text", s => SetColor(s, "reelTextColor", reelText));
            var view = (SlotReelView)panel.ReelViews[0];
            var settings = SlotReelSpinSettings.Default;
            view.ApplyMotionEffectForEditor(settings, 1f);
            panel.SetSkin(skin);
            var center = view.VisibleCellCount / 2 + 1;
            var texts = view.CellTextsForEditor;
            var expectedCenter = reelText.a * settings.CruiseTextAlpha;
            Assert.That(texts[center].color.r, Is.EqualTo(1f).Within(1e-4f));
            Assert.That(texts[center].color.g, Is.EqualTo(.5f).Within(1e-4f));
            Assert.That(texts[center].color.a, Is.EqualTo(expectedCenter).Within(1e-3f), "cell alpha × skin alpha");
            var afterimage = view.transform.Find($"Window/Strip/SymbolCell_{center}/VisualRoot/Afterimage_1").GetComponent<TMP_Text>();
            Assert.That(afterimage.enabled, Is.True);
            Assert.That(afterimage.color.a, Is.EqualTo(reelText.a * settings.AfterimageAlpha).Within(1e-3f), "afterimage alpha × skin alpha");

            var task = panel.PlaySpinAsync(Spin(2, 0, 5), false, CancellationToken.None);
            Assert.That(task.IsCompleted && task.Result, Is.True);
            Assert.That(panel.ReelViews.Select(v => v.PaylineValue), Is.EqualTo(new[] { 3, 1, 6 }));
            Assert.That(view.AnyAfterimageVisible, Is.False);
            Assert.That(texts[center].color.a, Is.EqualTo(reelText.a).Within(1e-3f), "payline cell at rest");
            Assert.That(texts[center + 1].color.a, Is.EqualTo(reelText.a * .8f).Within(1e-3f), "neighbour cell alpha × skin alpha");
            panel.SetSkin(null);
            Assert.That(texts[center].color, Is.EqualTo(new Color(1f, 1f, 1f, 1f)), "the default text color is restored");
        }

        [Test]
        public void Catalog_RejectsInvalidEntriesAndTrySetSkinKeepsTheCurrentSkin()
        {
            var a = Skin("test.a");
            var b = Skin("test.b");
            var catalog = Catalog(new[] { a, b }, "test.b");
            Assert.That(catalog.Validate(out _), Is.True);
            Assert.That(catalog.SelectedSkin, Is.SameAs(b));
            Assert.That(catalog.TryGet(" test.a ", out var found) && found == a, Is.True, "IDs are trimmed");
            Assert.That(Catalog(new[] { a, Skin("  ") }, "test.a").Validate(out var reason), Is.False, reason);
            Assert.That(reason, Does.Contain("no ID"));
            Assert.That(Catalog(new[] { a, Skin("test.a") }, "test.a").Validate(out reason), Is.False);
            Assert.That(reason, Does.Contain("more than once"));
            Assert.That(Catalog(new[] { a }, "test.missing").Validate(out reason), Is.False);
            Assert.That(reason, Does.Contain("not registered"));
            Assert.That(Catalog(new SlotMachinePanelSkin[] { a, null }, "test.a").Validate(out reason), Is.False);
            Assert.That(reason, Does.Contain("empty"));

            SetCatalog(catalog);
            panel.ApplyCatalogSelectionForEditor();
            Assert.That(panel.ActiveSkin, Is.SameAs(b), "the developer selection is applied");
            Assert.That(panel.TrySetSkin("test.missing", out reason), Is.False);
            Assert.That(reason, Is.Not.Empty);
            Assert.That(panel.ActiveSkin, Is.SameAs(b), "a failed lookup keeps the current skin");
            Assert.That(panel.TrySetSkin("test.a", out _), Is.True);
            Assert.That(panel.ActiveSkinId, Is.EqualTo("test.a"));
        }

        [Test]
        public void StoredCatalog_SelectsTheDefaultSkin()
        {
            Assert.That(panel.SkinCatalog, Is.Not.Null);
            Assert.That(panel.SkinCatalog.Validate(out var reason), Is.True, reason);
            panel.ApplyCatalogSelectionForEditor();
            Assert.That(panel.ActiveSkinId, Is.EqualTo("slot-skin.default"));
            AssertDefaults("default skin");
            Assert.That(panel.TrySetSkin("slot-skin.sample-image-payline", out reason), Is.True, reason);
            Assert.That(panel.PaylineImageShown, Is.True);
        }

        [Test]
        public void MissingCatalog_WarnsOnceAndKeepsTheDefaultLook()
        {
            SetCatalog(null);
            var pattern = new Regex("no SlotMachinePanelSkinCatalog is assigned");
            var warnings = 0;
            void Count(string message, string stack, LogType type) { if (type == LogType.Warning && pattern.IsMatch(message)) warnings++; }
            LogAssert.Expect(LogType.Warning, pattern);
            UnityEngine.Application.logMessageReceived += Count;
            try
            {
                panel.ApplyCatalogSelectionForEditor();
                panel.ApplyCatalogSelectionForEditor();
            }
            finally { UnityEngine.Application.logMessageReceived -= Count; }
            Assert.That(warnings, Is.EqualTo(1), "one warning per panel instance");
            Assert.That(panel.ActiveSkin, Is.Null);
            AssertDefaults("no catalog");
        }

        [Test]
        public void SkinChanged_IsRaisedOnlyForAnActualChange()
        {
            var count = 0;
            panel.SkinChanged += _ => count++;
            var skin = Skin("test.a");
            panel.SetSkin(null);
            Assert.That(count, Is.EqualTo(0), "null to null is no change");
            panel.SetSkin(skin);
            panel.SetSkin(skin);
            Assert.That(count, Is.EqualTo(1));
            panel.SetSkin(null);
            Assert.That(count, Is.EqualTo(2));
        }

        // ---------- Helpers ----------

        private void AssertDefaults(string label)
        {
            Assert.That(panel.PaylineBorderShown, Is.True, $"{label}: code border shown");
            Assert.That(panel.PaylineImageShown, Is.False, $"{label}: payline image hidden");
            foreach (var target in panel.PanelSkinTargetsForEditor) AssertDefault(target, target.Graphic, label);
            foreach (var divider in panel.ReelDividersForEditor)
                AssertDefault(panel.DividerSkinForEditor, divider.GetComponent<Graphic>(), label);
            foreach (var text in panel.PanelTextTargetsForEditor)
            {
                Assert.That(text.HasDefaults, Is.True, $"{label}: {text.Text?.name} defaults recorded");
                Assert.That(text.Text.color, Is.EqualTo(text.DefaultColor), $"{label}: {text.Text.name} color");
            }
            Assert.That(panel.PaylineBorderForEditor.color, Is.EqualTo(panel.PaylineBorderSkinForEditor.DefaultColor), $"{label}: border color");
            foreach (var view in panel.ReelViews.Cast<SlotReelView>())
            {
                AssertDefault(view.FrameSkinForEditor, view.FrameSkinForEditor.Graphic, label);
                AssertDefault(view.ShadeSkinForEditor, view.ShadeSkinForEditor.Graphic, label);
            }
            var table = panel.RuneTableForEditor;
            AssertDefault(table.BackgroundSkinForEditor, table.BackgroundSkinForEditor.Graphic, label);
            foreach (var row in table.RowsForEditor.Where(r => r != null))
            {
                AssertDefault(row.BackgroundSkinForEditor, row.BackgroundSkinForEditor.Graphic, label);
                Assert.That(row.EffectTextSkinForEditor.Text.color, Is.EqualTo(row.EffectTextSkinForEditor.DefaultColor), $"{label}: {row.name} text");
            }
        }

        private static void AssertDefault(SlotSkinGraphicTarget target, Graphic graphic, string label)
        {
            Assert.That(target.HasDefaults && graphic != null, Is.True, $"{label}: {graphic?.name} defaults recorded");
            Assert.That(graphic.color, Is.EqualTo(target.DefaultColor), $"{label}: {graphic.name} color");
            if (graphic is Image image)
            {
                // Unity == (an unassigned serialized reference is a "null" object, not C# null).
                Assert.That(image.sprite == target.DefaultSprite, Is.True, $"{label}: {graphic.name} sprite ({image.sprite} vs {target.DefaultSprite})");
                Assert.That(image.type, Is.EqualTo(target.DefaultType), $"{label}: {graphic.name} type");
            }
        }

        private (SlotMachinePanelSkin Skin, Sprite Sprite, Color Color, Color TextColor) FullSkin(string id)
        {
            var sprite = Sprite();
            var color = new Color(.8f, .2f, .6f, 1f);
            var textColor = new Color(.05f, .1f, .2f, 1f);
            var skin = Skin(id, s =>
            {
                foreach (var entry in ImageEntries)
                {
                    s.FindProperty($"{entry}.sprite").objectReferenceValue = sprite;
                    SetColor(s, entry, color);
                }
                foreach (var entry in new[] { "reelTextColor", "darkSurfaceTextColor", "lightSurfaceTextColor" }) SetColor(s, entry, textColor);
            });
            return (skin, sprite, color, textColor);
        }

        private SlotMachinePanelSkin Skin(string id, Action<SerializedObject> configure = null)
        {
            var skin = ScriptableObject.CreateInstance<SlotMachinePanelSkin>();
            created.Add(skin);
            var serialized = new SerializedObject(skin);
            serialized.FindProperty("skinId").stringValue = id;
            configure?.Invoke(serialized);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return skin;
        }

        private static void SetColor(SerializedObject skin, string entry, Color color)
        {
            skin.FindProperty($"{entry}.overrideColor").boolValue = true;
            skin.FindProperty($"{entry}.color").colorValue = color;
        }

        private SlotMachinePanelSkinCatalog Catalog(IEnumerable<SlotMachinePanelSkin> skins, string selected)
        {
            var catalog = ScriptableObject.CreateInstance<SlotMachinePanelSkinCatalog>();
            created.Add(catalog);
            catalog.ConfigureForEditor(skins, selected);
            return catalog;
        }

        private void SetCatalog(SlotMachinePanelSkinCatalog catalog)
        {
            var serialized = new SerializedObject(panel);
            serialized.FindProperty("skinCatalog").objectReferenceValue = catalog;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        // 8 px sprite with a 2 px 9-slice border, so Sliced drawing has no border warning.
        private Sprite Sprite()
        {
            var texture = new Texture2D(8, 8, TextureFormat.RGBA32, false);
            created.Add(texture);
            var sprite = UnityEngine.Sprite.Create(texture, new Rect(0f, 0f, 8f, 8f), new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect,
                new Vector4(2f, 2f, 2f, 2f));
            created.Add(sprite);
            return sprite;
        }

        private static SlotReelDisplay[] Reels(int count) =>
            Enumerable.Range(1, count).Select(i => new SlotReelDisplay($"reel.{i}", OneToSix)).ToArray();

        private static SlotSummaryDisplay Summary(int lines) =>
            new(string.Empty, "—", "룬", "쌓임 → 분배", Enumerable.Range(1, lines).Select(i => new SlotRuneLineDisplay("공격", $"×{i}")).ToArray(),
                "장착된 룬이 없습니다", "외 {0}줄");

        private static SlotSpinDisplay Spin(params int[] stops) => new(
            stops.Select((stop, i) => new SlotReelStopDisplay($"reel.{i + 1}", stop, OneToSix)).ToArray(),
            stops.Sum(stop => OneToSix[stop]),
            Summary(2),
            Summary(2));

        private void Arrange()
        {
            ((RectTransform)panel.transform).sizeDelta = new Vector2(968f, 413f);
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)panel.transform.Find("Content"));
            LayoutRebuilder.ForceRebuildLayoutImmediate(panel.ReelAreaForEditor);
            panel.RuneTableForEditor.RefreshForEditor();
        }

        private static Rect WorldRect(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }
    }
}
