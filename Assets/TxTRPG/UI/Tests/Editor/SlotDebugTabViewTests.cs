using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TxTRPG.UI.SlotMachine;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace TxTRPG.UI.Tests
{
    // Reads the saved EncounterDebugPanel.prefab only and uses its SlotPage.
    public sealed class SlotDebugTabViewTests
    {
        private const string PrefabPath = "Assets/TxTRPG/UI/Prefabs/Encounters/EncounterDebugPanel.prefab";
        private GameObject canvas;
        private RectTransform host;
        private RectTransform panelRoot;
        private SlotDebugTabView view;
        private readonly List<int> countChanges = new();
        private readonly List<(string, float)> reelChances = new();
        private readonly List<float> totalChances = new();
        private readonly List<string> actions = new();

        [SetUp]
        public void SetUp()
        {
            canvas = new GameObject("SlotDebugCanvas", typeof(Canvas));
            // Same parent size as Candidate_MainScene's OverlayLayer (the 1920x1080 screen frame).
            host = new GameObject("OverlayLayer", typeof(RectTransform)).GetComponent<RectTransform>();
            host.SetParent(canvas.transform, false);
            host.anchorMin = host.anchorMax = new Vector2(.5f, .5f);
            host.sizeDelta = new Vector2(1920f, 1080f);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.That(prefab, Is.Not.Null, $"Missing {PrefabPath}");
            var panel = (GameObject)PrefabUtility.InstantiatePrefab(prefab, host);
            panelRoot = (RectTransform)panel.transform;
            view = panel.GetComponentInChildren<SlotDebugTabView>(true);
            Assert.That(view, Is.Not.Null, "the panel has a slot page");
            // Selected like a user would: the encounter page is hidden and the slot page shown.
            var tabs = panel.GetComponent<TxTRPG.UI.DebugPanels.DebugPanelTabGroup>();
            tabs.Select(1);
            Assert.That(view.gameObject.activeInHierarchy && !tabs.Tabs[0].Page.activeSelf, Is.True);
            view.ReelCountChangeRequested += countChanges.Add;
            view.ReelChanceChanged += (id, chance) => reelChances.Add((id, chance));
            view.TotalChanceChanged += totalChances.Add;
            view.SpinRequested += () => actions.Add("spin");
            view.ShowRequested += () => actions.Add("show");
            view.HideRequested += () => actions.Add("hide");
            view.ResetRequested += () => actions.Add("reset");
        }

        [TearDown]
        public void TearDown()
        {
            countChanges.Clear();
            reelChances.Clear();
            totalChances.Clear();
            actions.Clear();
            Object.DestroyImmediate(canvas);
        }

        private static SlotDebugReelEntry[] Entries(params float[] chances) =>
            chances.Select((chance, i) => new SlotDebugReelEntry($"reel.{i + 1}", $"Reel {i + 1}", chance)).ToArray();

        private SlotDebugReelRow[] Rows() => Enumerable.Range(0, view.ActiveRowCount).Select(view.GetRow).ToArray();

        [Test]
        public void SavedPrefab_IsComplete()
        {
            Assert.That(view.IsValid(out var missing), Is.True, missing);
            Assert.That(view.ReelRowTemplate.gameObject.activeSelf, Is.False, "the row template is inactive");
            Assert.That(view.BoundsRoot, Is.EqualTo(panelRoot), "the page grows with the panel root");
            Assert.That(view.BottomMargin, Is.EqualTo(16f), "same margin as the panel's top-right placement");
            var scroll = view.GetComponent<ScrollRect>();
            Assert.That(scroll != null && scroll.vertical && !scroll.horizontal, Is.True, "vertical scroll only");
            Assert.That(scroll.movementType, Is.EqualTo(ScrollRect.MovementType.Clamped));
            foreach (var button in new[] { view.MinusButton, view.PlusButton, view.SpinButton, view.ResetButton, view.ShowButton, view.HideButton })
                Assert.That(LayoutUtility.GetMinHeight((RectTransform)button.transform), Is.GreaterThanOrEqualTo(48f), button.name);
            Assert.That(view.TotalSlider.wholeNumbers && view.TotalSlider.maxValue == 100f && view.TotalSlider.minValue == 0f, Is.True);
        }

        [Test]
        public void SetReels_DoesNotNotify_AndReusesRows()
        {
            view.SetReels(Entries(.05f, .5f, 1f), true, true);
            Assert.That(reelChances, Is.Empty);
            var first = Rows();
            Assert.That(first.Length, Is.EqualTo(3));
            Assert.That(first.Select(row => row.Value.text), Is.EqualTo(new[] { "5%", "50%", "100%" }));
            Assert.That(first.Select(row => row.Label.text), Is.EqualTo(new[] { "Reel 1", "Reel 2", "Reel 3" }));
            Assert.That(view.CountText.text, Is.EqualTo("3"));

            view.SetReels(Entries(.1f, .2f, .3f), true, true);
            Assert.That(Rows(), Is.EqualTo(first), "the same IDs keep the rows");
            Assert.That(first[2].Slider.value, Is.EqualTo(30f));

            view.SetReels(Entries(.1f, .2f), true, true);
            Assert.That(view.ActiveRowCount, Is.EqualTo(2));
            Assert.That(first[2].gameObject.activeSelf, Is.False, "the surplus row is deactivated");

            view.SetReels(Entries(.1f, .2f, .3f, .4f), true, true);
            Assert.That(Rows().Take(3), Is.EqualTo(first), "rows are reused before new ones are made");
            Assert.That(view.CountText.text, Is.EqualTo("4"));
            Assert.That(reelChances, Is.Empty);
        }

        [Test]
        public void Sliders_RaiseChancesAsFractions()
        {
            view.SetReels(Entries(.05f, .05f), true, true);
            view.SetTotalChance(.03f);
            Assert.That(view.TotalValue.text, Is.EqualTo("3%"));
            view.GetRow(1).Slider.value = 37f;
            view.TotalSlider.value = 80f;
            Assert.That(reelChances.Count, Is.EqualTo(1));
            Assert.That(reelChances[0].Item1, Is.EqualTo("reel.2"));
            Assert.That(reelChances[0].Item2, Is.EqualTo(.37f).Within(1e-5f));
            Assert.That(view.GetRow(1).Value.text, Is.EqualTo("37%"));
            Assert.That(totalChances.Single(), Is.EqualTo(.8f).Within(1e-5f));
            Assert.That(view.TotalValue.text, Is.EqualTo("80%"));
        }

        [Test]
        public void NonWholePercent_IsShownRoundedWithoutNotifying()
        {
            view.SetReels(Entries(.025f), false, true);
            Assert.That(view.GetRow(0).Slider.value, Is.EqualTo(3f).Or.EqualTo(2f));
            Assert.That(reelChances, Is.Empty);
        }

        [Test]
        public void MinusAndPlus_FollowTheLimits()
        {
            view.SetReels(Entries(.05f), false, true);
            Assert.That(view.MinusButton.interactable, Is.False);
            Assert.That(view.PlusButton.interactable, Is.True);
            view.PlusButton.onClick.Invoke();
            view.MinusButton.onClick.Invoke();
            Assert.That(countChanges, Is.EqualTo(new[] { 1 }), "a disabled − raises nothing");

            view.SetReels(Entries(.05f, .05f), true, false);
            Assert.That(view.PlusButton.interactable, Is.False);
            view.MinusButton.onClick.Invoke();
            Assert.That(countChanges, Is.EqualTo(new[] { 1, -1 }));
        }

        [Test]
        public void Spinning_DisablesSpinCountAndReset_ButNotSlidersOrMoves()
        {
            view.SetReels(Entries(.05f, .05f), true, true);
            view.SetShown(true);
            view.SetSpinning(true);
            Assert.That(view.SpinButton.interactable || view.MinusButton.interactable || view.PlusButton.interactable || view.ResetButton.interactable,
                Is.False);
            Assert.That(view.TotalSlider.interactable && view.GetRow(0).Slider.interactable, Is.True);
            Assert.That(view.HideButton.interactable, Is.True);
            view.SpinButton.onClick.Invoke();
            Assert.That(actions, Is.Empty);
            view.SetSpinning(false);
            Assert.That(view.SpinButton.interactable && view.ResetButton.interactable, Is.True);
            view.SpinButton.onClick.Invoke();
            view.ResetButton.onClick.Invoke();
            Assert.That(actions, Is.EqualTo(new[] { "spin", "reset" }));
        }

        [Test]
        public void ShowAndHide_FollowTheShownState()
        {
            view.SetShown(true);
            Assert.That(view.ShowButton.interactable, Is.False);
            Assert.That(view.HideButton.interactable, Is.True);
            view.HideButton.onClick.Invoke();
            view.SetShown(false);
            Assert.That(view.ShowButton.interactable, Is.True);
            Assert.That(view.HideButton.interactable, Is.False);
            view.ShowButton.onClick.Invoke();
            Assert.That(actions, Is.EqualTo(new[] { "hide", "show" }));
        }

        [Test]
        public void WithoutSession_OnlyMovesAreUsable_AndResultIsShown()
        {
            view.SetReels(Entries(.05f, .05f), true, true);
            view.SetShown(true);
            view.SetSessionAvailable(false);
            Assert.That(view.SpinButton.interactable || view.ResetButton.interactable || view.MinusButton.interactable ||
                        view.PlusButton.interactable || view.TotalSlider.interactable, Is.False);
            Assert.That(view.HideButton.interactable, Is.True);
            view.ShowResult("3, 5×2, 2 → 합계 ×3 = 45");
            Assert.That(view.ResultText.text, Is.EqualTo("3, 5×2, 2 → 합계 ×3 = 45"));
        }

        private float PanelBottomMargin()
        {
            var panel = BoundsIn(host, panelRoot);
            return panel.yMin - host.rect.yMin;
        }

        private static Rect BoundsIn(RectTransform space, RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            var min = space.InverseTransformPoint(corners[0]);
            var max = space.InverseTransformPoint(corners[2]);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        [Test]
        public void TallParent_ShowsEveryControlWithoutScrolling()
        {
            view.SetReels(Entries(Enumerable.Repeat(.05f, 10).ToArray()), true, false);
            LayoutRebuilder.ForceRebuildLayoutImmediate(panelRoot);
            var content = view.GetComponent<ScrollRect>().content;
            var preferred = LayoutUtility.GetPreferredHeight(content);
            Assert.That(view.PageLayout.preferredHeight, Is.EqualTo(preferred).Within(.5f), "the whole content is shown");
            Assert.That(preferred, Is.GreaterThan(520f), "more than the previous fixed cap");
            Assert.That(PanelBottomMargin(), Is.GreaterThanOrEqualTo(16f - .5f), "the panel stays inside its parent");
        }

        [Test]
        public void ShortParent_LimitsThePageToTheSpaceBelow_AndScrolls()
        {
            view.SetReels(Entries(Enumerable.Repeat(.05f, 10).ToArray()), true, false);
            host.sizeDelta = new Vector2(1920f, 600f);
            view.RefreshPageHeight();
            LayoutRebuilder.ForceRebuildLayoutImmediate(panelRoot);
            var content = view.GetComponent<ScrollRect>().content;
            Assert.That(view.PageLayout.preferredHeight, Is.LessThan(LayoutUtility.GetPreferredHeight(content) - 1f), "the page scrolls");
            Assert.That(PanelBottomMargin(), Is.EqualTo(16f).Within(.5f), "the panel ends 16 above the parent's bottom");

            host.sizeDelta = new Vector2(1920f, 150f);
            view.RefreshPageHeight();
            Assert.That(view.PageLayout.preferredHeight, Is.EqualTo(SlotDebugTabView.DefaultMinimumPageHeight).Within(.01f), "never shorter than the minimum");
        }

        [Test]
        public void Navigation_FollowsRows()
        {
            view.SetReels(Entries(Enumerable.Repeat(.05f, 10).ToArray()), true, false);
            var rows = Rows();
            Assert.That(view.MinusButton.navigation.selectOnDown, Is.EqualTo(rows[0].Slider));
            Assert.That(rows[0].Slider.navigation.selectOnDown, Is.EqualTo(rows[1].Slider));
            Assert.That(rows[9].Slider.navigation.selectOnDown, Is.EqualTo(view.TotalSlider));
            Assert.That(view.TotalSlider.navigation.selectOnDown, Is.EqualTo(view.SpinButton));
            Assert.That(view.SpinButton.navigation.selectOnRight, Is.EqualTo(view.ResetButton));
            Assert.That(view.SpinButton.navigation.selectOnDown, Is.EqualTo(view.ShowButton));
        }
    }
}
