using System.Linq;
using System.Threading;
using NUnit.Framework;
using TxTRPG.UI.SlotMachine;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace TxTRPG.UI.Tests
{
    public sealed class SlotMachinePanelTests
    {
        private const string PanelPrefabPath = "Assets/TxTRPG/UI/Prefabs/SlotMachine/SlotMachinePanel.prefab";
        private static readonly int[] OneToSix = { 1, 2, 3, 4, 5, 6 };
        private GameObject canvas;
        private SlotMachinePanel panel;

        [SetUp]
        public void SetUp()
        {
            canvas = new GameObject("SlotCanvas", typeof(Canvas));
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PanelPrefabPath);
            Assert.That(prefab, Is.Not.Null, $"Missing prefab: {PanelPrefabPath}");
            panel = ((GameObject)PrefabUtility.InstantiatePrefab(prefab, canvas.transform)).GetComponent<SlotMachinePanel>();
            Arrange(new Vector2(968f, 413f));
            // Edit Mode time does not advance, so the presentation is made instantaneous; the end state is what is tested.
            panel.SetSpinSettingsForEditor(SlotReelSpinSettings.Instant);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(canvas);

        private static SlotReelDisplay[] Reels(int count) =>
            Enumerable.Range(1, count).Select(i => new SlotReelDisplay($"reel.{i}", OneToSix)).ToArray();

        private static SlotSummaryDisplay Summary(string total, params (string Effect, string Value)[] lines) =>
            new("합계", total, "룬", "쌓임 → 분배", lines.Select(l => new SlotRuneLineDisplay(l.Effect, l.Value)).ToArray(),
                "장착된 룬이 없습니다", "외 {0}줄");

        private static SlotSpinDisplay Spin(params int[] stops) => new(
            stops.Select((stop, i) => new SlotReelStopDisplay($"reel.{i + 1}", stop, OneToSix)).ToArray(),
            stops.Sum(stop => OneToSix[stop]),
            Summary("…", ("공격", "×1"), ("회복", "×1")),
            Summary(stops.Sum(stop => OneToSix[stop]).ToString(), ("공격", "×1 → 5"), ("회복", "×1 → 5")));

        [Test]
        public void Prefab_HasTheReelMachineAndSummaryWithoutInputTargets()
        {
            Assert.That(panel.ReelCount, Is.EqualTo(3));
            Assert.That(panel.ActiveDividerCount, Is.EqualTo(2), "one divider between neighbouring reels");
            Assert.That(panel.PaylineFrameRect, Is.Not.Null);
            Assert.That(panel.PaylineFrameRect.GetComponent<Graphic>(), Is.Null, "PaylineFrame is a layout-only root");
            Assert.That(panel.PaylineFrameRect.Find("ProceduralBorder").GetComponent<SlotPaylineFrameGraphic>(), Is.Not.Null);
            Assert.That(panel.PaylineFrameRect.Find("SkinImage").GetComponent<Image>().enabled, Is.False, "the payline image is off without a skin");
            Assert.That(panel.SkinCatalog, Is.Not.Null, "SlotMachinePanelSkinCatalog is assigned");
            Assert.That(panel.transform.Find("Content/ReelMachine/ReelArea"), Is.Not.Null);
            Assert.That(panel.transform.Find("Content/SummaryArea/TotalBox/TotalLabel"), Is.Not.Null);
            Assert.That(panel.transform.Find("Content/SummaryArea/RuneTableArea/RuneTable/Rows"), Is.Not.Null);
            Assert.That(panel.transform.Find("Content/Summary"), Is.Null, "the old summary was replaced");
            // TMP creates SubMeshUI children at runtime for fallback-font glyphs; they are not authored in the Prefab.
            Assert.That(panel.GetComponentsInChildren<Graphic>(true).Where(g => g.raycastTarget && !(g is TMPro.TMP_SubMeshUI)), Is.Empty,
                "the panel takes no input");
            Assert.That(panel.GetComponent<CanvasGroup>(), Is.Not.Null);
            Assert.That(panel.SpinProfileForEditor, Is.Not.Null, "DefaultSlotReelSpinProfile is assigned");
            var reel = (SlotReelView)panel.ReelViews[0];
            Assert.That(reel.VisibleCellCount, Is.EqualTo(5));
            Assert.That(reel.transform.Find("Window/Strip/SymbolCell_6/VisualRoot/Afterimage_3"), Is.Not.Null);
            Assert.That(reel.transform.Find("Window/ShadeOverlay"), Is.Not.Null);
        }

        [Test]
        public void ReelCountChanges_AddAndRemoveReelsAndDividers()
        {
            panel.SetReels(Reels(5));
            Assert.That(panel.ReelCount, Is.EqualTo(5));
            Assert.That(panel.ActiveDividerCount, Is.EqualTo(4));
            Assert.That(panel.ReelViews.Select(v => v.ReelId), Is.EqualTo(new[] { "reel.1", "reel.2", "reel.3", "reel.4", "reel.5" }));
            Assert.That(panel.ReelViews.All(v => v.PaylineValue == 1), Is.True, "the first cell is on the payline before any spin");
            var area = panel.ReelAreaForEditor;
            var order = Enumerable.Range(0, area.childCount).Select(i => area.GetChild(i)).Where(t => t.gameObject.activeSelf)
                .Select(t => t.GetComponent<SlotReelViewBase>() != null ? "R" : "D");
            Assert.That(string.Concat(order), Is.EqualTo("RDRDRDRDR"), "dividers sit only between reels");
            panel.SetReels(Reels(1));
            Assert.That(panel.ReelCount, Is.EqualTo(1));
            Assert.That(panel.ActiveDividerCount, Is.EqualTo(0));
            panel.SetReels(Reels(2));
            Assert.That(panel.ActiveDividerCount, Is.EqualTo(1), "the kept divider template is reused");
            Assert.That(panel.GetComponentsInChildren<SlotReelViewBase>(true).Length, Is.EqualTo(2));
        }

        [Test]
        public void PaylineFrame_SpansTheReelWindowsAtThePaylineCell()
        {
            panel.SetReels(Reels(4));
            Arrange(new Vector2(968f, 413f));
            panel.UpdatePaylineFrameForEditor();
            var frame = WorldRect(panel.PaylineFrameRect);
            var first = WorldRect(((SlotReelView)panel.ReelViews[0]).WindowForEditor);
            var last = WorldRect(((SlotReelView)panel.ReelViews[3]).WindowForEditor);
            Assert.That(frame.xMin, Is.LessThanOrEqualTo(first.xMin + .01f));
            Assert.That(frame.xMax, Is.GreaterThanOrEqualTo(last.xMax - .01f));
            Assert.That(frame.center.y, Is.EqualTo(first.center.y).Within(.5f));
            Assert.That(frame.height, Is.EqualTo(first.height / 5f).Within(.5f), "the root covers exactly the payline cell");
            var border = WorldRect(panel.PaylineVisibleRect);
            Assert.That(panel.PaylineBorderShown, Is.True);
            Assert.That(border.height, Is.GreaterThan(frame.height).And.LessThan(frame.height + 20f), "the border is drawn outside the cell");
            Assert.That(border.center.y, Is.EqualTo(frame.center.y).Within(.5f));
        }

        [Test]
        public void PlaySpin_EndsOnTheResultWithTwoNeighboursEachSide()
        {
            panel.SetReels(Reels(3));
            panel.SetSummary(Summary("—", ("공격", "×1"), ("회복", "×1")));
            panel.SetSpinRandomSeedForEditor(7);
            var task = panel.PlaySpinAsync(Spin(2, 0, 5), false, CancellationToken.None);
            Assert.That(task.IsCompleted, Is.True);
            Assert.That(task.Result, Is.True);
            AssertResult(new[] { 3, 1, 6 });
            Assert.That(panel.TotalLabelText, Is.EqualTo("합계"));
            Assert.That(panel.TotalValueText, Is.EqualTo("10"));
            Assert.That(panel.RuneLineTexts, Is.EqualTo(new[] { ("공격", "×1 → 5"), ("회복", "×1 → 5") }));
            Assert.That(panel.IsSpinning, Is.False);
            AssertEffectsCleared();
        }

        [Test]
        public void ReduceMotionAndStopAtResult_EndInTheSameState()
        {
            panel.SetReels(Reels(3));
            var task = panel.PlaySpinAsync(Spin(4, 1, 3), true, CancellationToken.None);
            Assert.That(task.IsCompleted && task.Result, Is.True);
            AssertResult(new[] { 5, 2, 4 });
            panel.StopAtResult(Spin(4, 1, 3));
            AssertResult(new[] { 5, 2, 4 });
            Assert.That(panel.TotalValueText, Is.EqualTo("11"));
        }

        [Test]
        public void MotionEffects_AreClearedWhenPutAtRest()
        {
            panel.SetReels(Reels(3));
            var settings = SlotReelSpinSettings.Default;
            var reel = (SlotReelView)panel.ReelViews[0];
            reel.ApplyMotionEffectForEditor(settings, 1f);
            Assert.That(reel.AnyAfterimageVisible, Is.True);
            Assert.That(reel.MaximumVisualScaleY, Is.EqualTo(settings.MaximumVerticalStretch).Within(1e-4f));
            panel.StopAtResult(Spin(1, 1, 1));
            AssertEffectsCleared();
            // OnDisable does not run in Edit Mode; clearing on disable is checked by the Play Mode validation menu.
            reel.ApplyMotionEffectForEditor(settings, 1f);
            reel.ShowStop(new SlotReelDisplay("reel.1", OneToSix), 0);
            Assert.That(reel.AnyAfterimageVisible, Is.False, "an interrupted spin is put at rest without effects");
        }

        [Test]
        public void SummaryStates_ShowTheTotalAndRuneRows()
        {
            panel.SetSummary(Summary("—", ("공격", "×1"), ("회복", "×1")));
            Assert.That(panel.TotalValueText, Is.EqualTo("—"));
            Assert.That(panel.RuneColumnTitleText, Is.EqualTo("룬"));
            Assert.That(panel.ValueColumnTitleText, Is.EqualTo("쌓임 → 분배"));
            Assert.That(panel.RuneLineTexts, Is.EqualTo(new[] { ("공격", "×1"), ("회복", "×1") }));
            panel.SetSummary(Summary("…", ("공격", "×1"), ("회복", "×1")));
            Assert.That(panel.TotalValueText, Is.EqualTo("…"));
            panel.SetSummary(Summary("—"));
            Assert.That(panel.RuneLineTexts, Is.EqualTo(new[] { ("장착된 룬이 없습니다", string.Empty) }), "no rune lines");
            var many = Enumerable.Range(1, 40).Select(i => ("공격", $"×{i}")).ToArray();
            panel.SetSummary(Summary("—", many));
            var rows = panel.RuneLineTexts;
            Assert.That(rows.Count, Is.LessThan(40));
            Assert.That(rows.Last().Effect, Is.EqualTo($"외 {40 - (rows.Count - 1)}줄"), "the last row counts the hidden lines");
            Assert.That(rows.Take(rows.Count - 1).Select(r => r.Value), Is.EqualTo(many.Take(rows.Count - 1).Select(m => m.Item2)), "leading lines are shown");
            panel.SetSummary(Summary("—", ("회복", "×2")));
            Assert.That(panel.RuneLineTexts, Is.EqualTo(new[] { ("회복", "×2") }), "rows are reused and surplus rows hidden");
        }

        [Test]
        public void ConfigurationError_HidesTheContent()
        {
            panel.ShowError("슬롯머신 설정이 올바르지 않습니다.");
            Assert.That(panel.StatusText, Is.EqualTo("슬롯머신 설정이 올바르지 않습니다."));
            Assert.That(panel.transform.Find("Content").gameObject.activeSelf, Is.False, "reels are hidden on a configuration error");
            panel.ClearError();
            Assert.That(panel.StatusText, Is.Empty);
            Assert.That(panel.transform.Find("Content").gameObject.activeSelf, Is.True);
        }

        [Test]
        public void ContentLayout_SwitchesArrangementWithHysteresis()
        {
            var layout = panel.GetComponentInChildren<SlotMachineContentLayout>(true);
            var rect = (RectTransform)panel.transform;
            Arrange(new Vector2(968f, 413f));
            Assert.That(layout.Arrangement, Is.EqualTo(SlotMachineContentArrangement.SideBySide));
            var machine = layout.ReelMachineForEditor.rect;
            Assert.That(machine.width / (rect.rect.width - 24f), Is.InRange(.6f, .67f), "reel machine about 62% (normalized) of the width");
            Arrange(new Vector2(332f, 274f));
            Assert.That(layout.Arrangement, Is.EqualTo(SlotMachineContentArrangement.Stacked), "ratio 1.21 stacks");
            Assert.That(layout.ReelMachineForEditor.anchoredPosition.y, Is.GreaterThan(layout.SummaryAreaForEditor.anchoredPosition.y), "reel machine on top");
            Arrange(new Vector2(1.32f * 300f, 300f));
            Assert.That(layout.Arrangement, Is.EqualTo(SlotMachineContentArrangement.Stacked), "inside the hysteresis band it stays stacked");
            Arrange(new Vector2(1.36f * 300f, 300f));
            Assert.That(layout.Arrangement, Is.EqualTo(SlotMachineContentArrangement.SideBySide));
            Arrange(new Vector2(1.28f * 300f, 300f));
            Assert.That(layout.Arrangement, Is.EqualTo(SlotMachineContentArrangement.SideBySide), "inside the band it stays side by side");
            Arrange(new Vector2(1.24f * 300f, 300f));
            Assert.That(layout.Arrangement, Is.EqualTo(SlotMachineContentArrangement.Stacked));
        }

        [Test]
        public void ConfigurationKeptForUnchangedReels_AfterAResult()
        {
            panel.SetReels(Reels(3));
            panel.StopAtResult(Spin(2, 3, 4));
            var changed = Reels(4);
            changed[1] = new SlotReelDisplay("reel.2", new[] { 9, 9 });
            panel.SetReels(changed);
            Assert.That(panel.ReelViews.Select(v => v.PaylineValue), Is.EqualTo(new[] { 3, 9, 5, 1 }),
                "unchanged reels keep their result, a changed or new reel shows its first cell");
        }

        // Edit Mode has no canvas update loop: the panel root has no layout controller, so Content and the reel area
        // (below the layout-only ReelMachine) are rebuilt explicitly.
        private void Arrange(Vector2 size)
        {
            ((RectTransform)panel.transform).sizeDelta = size;
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)panel.transform.Find("Content"));
            LayoutRebuilder.ForceRebuildLayoutImmediate(panel.ReelAreaForEditor);
            panel.RuneTableForEditor.RefreshForEditor();
        }

        private void AssertResult(int[] payline)
        {
            Assert.That(panel.ReelViews.Select(v => v.PaylineValue), Is.EqualTo(payline));
            foreach (var view in panel.ReelViews.Cast<SlotReelView>())
            {
                var stop = view.StopIndex;
                for (var offset = -2; offset <= 2; offset++)
                {
                    var expected = OneToSix[(stop + offset + 6) % 6];
                    Assert.That(view.ValueAt(offset), Is.EqualTo(expected), $"strip neighbour at {offset}");
                    Assert.That(view.TextAt(offset), Is.EqualTo(expected.ToString()), $"text at {offset}");
                }
            }
        }

        private void AssertEffectsCleared()
        {
            foreach (var view in panel.ReelViews.Cast<SlotReelView>())
            {
                Assert.That(view.AnyAfterimageVisible, Is.False, $"{view.name} has no afterimage");
                Assert.That(view.MaximumVisualScaleY, Is.EqualTo(1f), $"{view.name} VisualRoot scale is 1");
            }
        }

        private static Rect WorldRect(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }
    }
}
