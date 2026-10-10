using System.Linq;
using System.Threading;
using NUnit.Framework;
using TxTRPG.UI.SlotMachine;
using UnityEditor;
using UnityEngine;

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
            ((RectTransform)panel.transform).sizeDelta = new Vector2(360f, 280f);
            // Edit Mode time does not advance, so the presentation is made instantaneous; the end state is what is tested.
            panel.SetTimingForEditor(0f, 0f, 0f, 0f);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(canvas);

        private static SlotReelDisplay[] Reels(int count) =>
            Enumerable.Range(1, count).Select(i => new SlotReelDisplay($"reel.{i}", OneToSix)).ToArray();

        private static SlotSpinDisplay Spin(params int[] stops) => new(
            stops.Select((stop, i) => new SlotReelStopDisplay($"reel.{i + 1}", stop, OneToSix)).ToArray(),
            stops.Sum(stop => OneToSix[stop]), "회전 중…", $"합계 {stops.Sum(stop => OneToSix[stop])}", "공격 ×1 → 5 · 회복 ×1 → 5");

        [Test]
        public void Prefab_ShowsThreeReelsWithoutInputTargets()
        {
            Assert.That(panel.ReelCount, Is.EqualTo(3));
            Assert.That(panel.GetComponentsInChildren<UnityEngine.UI.Graphic>(true).Where(g => g.raycastTarget), Is.Empty, "the panel takes no input");
            Assert.That(panel.GetComponent<CanvasGroup>(), Is.Not.Null);
        }

        [Test]
        public void ReelCountChanges_AddAndRemoveReelViews()
        {
            panel.SetReels(Reels(5));
            Assert.That(panel.ReelCount, Is.EqualTo(5));
            Assert.That(panel.ReelViews.Select(v => v.ReelId), Is.EqualTo(new[] { "reel.1", "reel.2", "reel.3", "reel.4", "reel.5" }));
            Assert.That(panel.ReelViews.All(v => v.PaylineValue == 1), Is.True, "the first cell is on the payline before any spin");
            panel.SetReels(Reels(2));
            Assert.That(panel.ReelCount, Is.EqualTo(2));
            Assert.That(panel.GetComponentsInChildren<SlotReelViewBase>(true).Length, Is.EqualTo(2));
        }

        [Test]
        public void PlaySpin_EndsOnTheResultWithStripNeighbours()
        {
            panel.SetReels(Reels(3));
            panel.SetTotalText("합계 —");
            var task = panel.PlaySpinAsync(Spin(2, 0, 5), false, CancellationToken.None);
            Assert.That(task.IsCompleted, Is.True);
            Assert.That(task.Result, Is.True);
            AssertResult(new[] { 3, 1, 6 });
            Assert.That(panel.TotalText, Is.EqualTo("합계 10"));
            Assert.That(panel.AllocationText, Is.EqualTo("공격 ×1 → 5 · 회복 ×1 → 5"));
            Assert.That(panel.IsSpinning, Is.False);
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
            Assert.That(panel.TotalText, Is.EqualTo("합계 11"));
        }

        [Test]
        public void StatesAndErrors_ChangeTheTexts()
        {
            panel.SetTotalText("합계 —");
            panel.SetAllocationText("장착된 룬이 없습니다");
            Assert.That(panel.TotalText, Is.EqualTo("합계 —"));
            Assert.That(panel.AllocationText, Is.EqualTo("장착된 룬이 없습니다"));
            panel.ShowError("슬롯머신 설정이 올바르지 않습니다.");
            Assert.That(panel.StatusText, Is.EqualTo("슬롯머신 설정이 올바르지 않습니다."));
            Assert.That(panel.transform.Find("Content").gameObject.activeSelf, Is.False, "reels are hidden on a configuration error");
            panel.ClearError();
            Assert.That(panel.StatusText, Is.Empty);
            Assert.That(panel.transform.Find("Content").gameObject.activeSelf, Is.True);
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

        private void AssertResult(int[] payline)
        {
            Assert.That(panel.ReelViews.Select(v => v.PaylineValue), Is.EqualTo(payline));
            foreach (var view in panel.ReelViews.Cast<SlotReelView>())
            {
                var stop = view.StopIndex;
                Assert.That(view.ValueAt(-1), Is.EqualTo(OneToSix[(stop + 5) % 6]), "the cell above is the previous strip value");
                Assert.That(view.ValueAt(1), Is.EqualTo(OneToSix[(stop + 1) % 6]), "the cell below is the next strip value");
                Assert.That(view.TextAt(0), Is.EqualTo(OneToSix[stop].ToString()));
                Assert.That(view.TextAt(-1), Is.EqualTo(OneToSix[(stop + 5) % 6].ToString()));
            }
        }
    }
}
