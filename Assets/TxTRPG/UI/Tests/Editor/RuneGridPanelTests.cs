using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using TxTRPG.UI.Runes;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace TxTRPG.UI.Tests
{
    public sealed class RuneGridPanelTests
    {
        private const string PanelPrefabPath = "Assets/TxTRPG/UI/Prefabs/Runes/RuneGridPanel.prefab";
        private GameObject canvas;
        private RuneGridPanel panel;
        private RuneIconCatalog catalog;
        private Texture2D texture;
        private Sprite attackIcon;

        [SetUp]
        public void SetUp()
        {
            canvas = new GameObject("RuneGridCanvas", typeof(Canvas));
            var host = new GameObject("LeftArea", typeof(RectTransform)).GetComponent<RectTransform>();
            host.SetParent(canvas.transform, false);
            host.sizeDelta = new Vector2(300f, 400f);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PanelPrefabPath);
            Assert.That(prefab, Is.Not.Null, $"Missing prefab: {PanelPrefabPath}");
            panel = ((GameObject)PrefabUtility.InstantiatePrefab(prefab, host)).GetComponent<RuneGridPanel>();
            texture = new Texture2D(4, 4);
            attackIcon = Sprite.Create(texture, new Rect(0, 0, 4, 4), Vector2.one * .5f);
            catalog = ScriptableObject.CreateInstance<RuneIconCatalog>();
            catalog.SetEntries(new[] { new RuneIconCatalog.Entry("rune.attack", attackIcon) });
            panel.SetIconCatalog(catalog);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(canvas);
            Object.DestroyImmediate(attackIcon);
            Object.DestroyImmediate(texture);
            Object.DestroyImmediate(catalog);
        }

        private static RuneGridEntryDisplay Attack(string key, int count) => new(key, "rune.attack", "공격 룬", count);
        private static RuneGridEntryDisplay Heal(string key, int count) => new(key, "rune.heal", "회복 룬", count);

        [Test]
        public void Prefab_IsVerticalOnlyAndStoresNoCards()
        {
            Assert.That(panel.IsValid(out var missing), Is.True, missing);
            Assert.That(panel.ScrollRect.horizontal, Is.False);
            Assert.That(panel.ScrollRect.vertical, Is.True);
            Assert.That(panel.ScrollRect.movementType, Is.EqualTo(ScrollRect.MovementType.Clamped));
            Assert.That(panel.ScrollRect.verticalScrollbarVisibility, Is.EqualTo(ScrollRect.ScrollbarVisibility.AutoHide));
            Assert.That(panel.Content.childCount, Is.Zero);
            Assert.That(panel.IconCatalog, Is.SameAs(catalog));
            Assert.That(panel.Viewport.GetComponent<RectMask2D>(), Is.Not.Null);
            Assert.That(panel.Viewport.GetComponent<Graphic>().raycastTarget, Is.True, "the viewport receives scroll input");
        }

        [Test]
        public void SetEntries_ReusesCardsAndDeactivatesExtras()
        {
            panel.SetEntries(new[] { Attack("a", 1), Heal("b", 1), Attack("c", 2) });
            Assert.That(panel.Cards.Count, Is.EqualTo(3));
            var created = panel.Cards.ToArray();

            panel.SetEntries(new[] { Attack("a", 1) });
            Assert.That(panel.ActiveCardCount, Is.EqualTo(1));
            Assert.That(panel.Cards.Count, Is.EqualTo(3), "extra cards are kept in the pool");
            Assert.That(panel.Cards.Count(card => card.gameObject.activeSelf), Is.EqualTo(1));

            panel.SetEntries(new[] { Attack("a", 1), Heal("b", 1), Attack("c", 2) });
            panel.SetEntries(new[] { Attack("a", 1), Heal("b", 1), Attack("c", 2) });
            Assert.That(panel.Cards, Is.EqualTo(created), "repeating the same data instantiates nothing");
            Assert.That(panel.Content.childCount, Is.EqualTo(3));
        }

        [Test]
        public void Badge_ShowsTheWeightUpTo99()
        {
            panel.SetEntries(new[] { Attack("a", 1), Attack("b", 99), Attack("c", 100), Attack("d", 250) });
            Assert.That(panel.Cards.Take(4).Select(card => card.CountText.text), Is.EqualTo(new[] { "1", "99", "99+", "99+" }));
            Assert.That(new RuneGridEntryDisplay(null, null, null, -3).StackCount, Is.Zero);
        }

        [Test]
        public void Card_ShowsTheIconOrTheNameFallback_AndClearsOnReuse()
        {
            panel.SetEntries(new[] { Attack("a", 1), Heal("b", 1) });
            var first = panel.Cards[0];
            var second = panel.Cards[1];
            Assert.That(first.HasIcon, Is.True);
            Assert.That(first.Icon.sprite, Is.SameAs(attackIcon));
            Assert.That(first.FallbackLabel.gameObject.activeSelf, Is.False);
            Assert.That(second.HasIcon, Is.False);
            Assert.That(second.FallbackLabel.gameObject.activeSelf, Is.True);
            Assert.That(second.FallbackLabel.text, Is.EqualTo("회복 룬"));

            panel.SetEntries(new[] { Heal("b", 2) });
            Assert.That(first.Icon.sprite, Is.Null, "a reused card keeps no previous Sprite");
            Assert.That(first.FallbackLabel.text, Is.EqualTo("회복 룬"));
            Assert.That(second.Icon.sprite, Is.Null);
            Assert.That(second.FallbackLabel.text, Is.Empty);
            Assert.That(second.CountText.text, Is.Empty);
        }

        [Test]
        public void MissingOrInvalidCatalog_ShowsEveryCardAsFallback()
        {
            panel.SetIconCatalog(null);
            panel.SetEntries(new[] { Attack("a", 1) });
            Assert.That(panel.Cards[0].HasIcon, Is.False);
            Assert.That(panel.Cards[0].FallbackLabel.text, Is.EqualTo("공격 룬"));

            catalog.SetEntries(new[] { new RuneIconCatalog.Entry("rune.attack", attackIcon), new RuneIconCatalog.Entry("rune.attack", attackIcon) });
            panel.SetIconCatalog(catalog);
            Assert.That(panel.Cards[0].HasIcon, Is.False, "a catalog that fails Validate is not used");
        }

        [Test]
        public void ShowMessage_HidesEveryCard_AndSetEntriesHidesTheMessage()
        {
            panel.SetEntries(new[] { Attack("a", 1), Heal("b", 1) });
            panel.ShowMessage("장착된 룬이 없습니다");
            Assert.That(panel.IsShowingMessage, Is.True);
            Assert.That(panel.MessageText.gameObject.activeSelf, Is.True);
            Assert.That(panel.MessageText.text, Is.EqualTo("장착된 룬이 없습니다"));
            Assert.That(panel.ActiveCardCount, Is.Zero);
            Assert.That(panel.Cards.Any(card => card.gameObject.activeSelf), Is.False);

            panel.SetEntries(new[] { Attack("a", 1) });
            Assert.That(panel.MessageText.gameObject.activeSelf, Is.False);
            Assert.That(panel.ActiveCardCount, Is.EqualTo(1));
        }

        [Test]
        public void CardGraphics_AreNotInputTargets()
        {
            panel.SetEntries(new[] { Attack("a", 1), Heal("b", 1) });
            var raycast = panel.Content.GetComponentsInChildren<Graphic>(true).Where(graphic => graphic.raycastTarget).Select(graphic => graphic.name);
            Assert.That(raycast, Is.Empty);
            Assert.That(panel.MessageText.raycastTarget, Is.False);
        }

        [Test]
        public void Layout_PlacesBadgeAtTheCellCornerWithoutHorizontalOverflow()
        {
            panel.SetEntries(Enumerable.Range(0, 12).Select(i => Attack($"line.{i}", 1)).ToArray());
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(panel.Content);
            var layout = panel.LayoutGroup.CurrentLayout;
            Assert.That(layout.Columns, Is.GreaterThanOrEqualTo(1));
            Assert.That(panel.Content.rect.width, Is.LessThanOrEqualTo(panel.Viewport.rect.width + .01f));
            Assert.That(layout.UsedWidth, Is.LessThanOrEqualTo(panel.LayoutGroup.AvailableWidth + .01f));
            Assert.That(panel.Content.rect.height, Is.EqualTo(layout.ContentHeight).Within(.01f));

            var card = panel.Cards[0];
            var cell = (RectTransform)card.transform;
            Assert.That(cell.rect.width, Is.EqualTo(layout.CellSize).Within(.01f));
            Assert.That(card.CardArea.rect.width, Is.EqualTo(layout.CardSize).Within(.01f));
            Assert.That(card.BadgeArea.rect.width, Is.EqualTo(layout.BadgeSize).Within(.01f));
            Assert.That(card.BadgeArea.GetSiblingIndex(), Is.GreaterThan(card.CardArea.GetSiblingIndex()), "the badge draws above the card");
            Assert.That(card.BadgeArea.anchorMin.x, Is.Zero);
            Assert.That(card.BadgeArea.anchorMax.y, Is.EqualTo(1f));
        }

        [Test]
        public void MissingReference_LogsOnceAndDoesNotThrow()
        {
            var bare = new GameObject("BarePanel", typeof(RectTransform)).AddComponent<RuneGridPanel>();
            bare.transform.SetParent(canvas.transform, false);
            LogAssert.Expect(LogType.Error, new Regex("RuneGridPanel is missing"));
            Assert.DoesNotThrow(() => bare.SetEntries(new[] { Attack("a", 1) }));
            Assert.DoesNotThrow(() => bare.ShowMessage("x"));
            LogAssert.NoUnexpectedReceived();
        }
    }
}
