using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TxTRPG.UI.Runes;
using TxTRPG.UI.Tooltips;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using Object = UnityEngine.Object;

namespace TxTRPG.UI.Tests
{
    public sealed class TooltipPresenterTests
    {
        private const string ViewPrefabPath = "Assets/TxTRPG/UI/Prefabs/Tooltips/TooltipView.prefab";
        private const string CardPrefabPath = "Assets/TxTRPG/UI/Prefabs/Runes/RuneGridCard.prefab";

        private sealed class FakeAnchor : ITooltipAnchor
        {
            public RectTransform Rect;
            public bool Valid = true;
            public bool Over = true;

            public bool IsAnchorValid => Valid;
            public bool IsPointerOver => Over;

            public bool TryGetAnchorRect(RectTransform space, out Rect rect)
            {
                var corners = new Vector3[4];
                Rect.GetWorldCorners(corners);
                Vector2 min = space.InverseTransformPoint(corners[0]);
                Vector2 max = space.InverseTransformPoint(corners[2]);
                rect = UnityEngine.Rect.MinMaxRect(min.x, min.y, max.x, max.y);
                return true;
            }
        }

        private readonly List<Object> created = new();
        private GameObject canvas;
        private RectTransform frame;
        private TooltipPresenter presenter;
        private TooltipKeywordCatalog keywords;
        private FakeAnchor anchor;

        private T Track<T>(T item) where T : Object { created.Add(item); return item; }

        [SetUp]
        public void SetUp()
        {
            canvas = Track(new GameObject("TooltipCanvas", typeof(Canvas)));
            frame = new GameObject("ScreenFrame", typeof(RectTransform)).GetComponent<RectTransform>();
            frame.SetParent(canvas.transform, false);
            frame.anchorMin = frame.anchorMax = frame.pivot = Vector2.zero;
            frame.sizeDelta = new Vector2(1920f, 1080f);
            var layer = new GameObject("TooltipLayer", typeof(RectTransform)).GetComponent<RectTransform>();
            layer.SetParent(frame, false);
            layer.anchorMin = Vector2.zero;
            layer.anchorMax = Vector2.one;
            layer.offsetMin = layer.offsetMax = Vector2.zero;

            keywords = Track(ScriptableObject.CreateInstance<TooltipKeywordCatalog>());
            var chain = new[] { "kw.a", "kw.b", "kw.c", "kw.d" };
            var definitions = new List<TooltipKeywordDefinition>();
            for (var i = 0; i < chain.Length; i++)
            {
                var keyword = Track(ScriptableObject.CreateInstance<TooltipKeywordDefinition>());
                var next = i + 1 < chain.Length ? chain[i + 1] : "kw.a";
                keyword.Configure(chain[i], chain[i].ToUpperInvariant(), null, new[] { TooltipBlock.Text($"다음 {{kw:{next}}} 끝") });
                definitions.Add(keyword);
            }
            keywords.SetKeywords(definitions);

            var prefab = AssetDatabase.LoadAssetAtPath<TooltipView>(ViewPrefabPath);
            Assert.That(prefab, Is.Not.Null, ViewPrefabPath);
            presenter = layer.gameObject.AddComponent<TooltipPresenter>();
            presenter.ConfigureForEditor(prefab, keywords, frame);
            presenter.SetTimingForEditor(0f, .1f, .08f, 3);

            anchor = new FakeAnchor { Rect = NewRect("Card", new Vector2(100f, 600f), new Vector2(120f, 120f)) };
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var item in created) if (item != null) Object.DestroyImmediate(item);
            created.Clear();
        }

        private RectTransform NewRect(string name, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(frame, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private static IReadOnlyList<TooltipBlock> Blocks(string text = "본문 {kw:kw.a}") => new[] { TooltipBlock.Title("제목"), TooltipBlock.Text(text) };

        private Vector2 TopLeftInFrame(TooltipView view)
        {
            var corners = new Vector3[4];
            view.Rect.GetWorldCorners(corners);
            return frame.InverseTransformPoint(corners[1]);
        }

        // Hovers the first keyword of the tooltip at `depth` and lets the keyword delay pass.
        private void HoverKeyword(int depth, ref float time)
        {
            var detector = presenter.GetView(depth).ActiveTextBlocks[0];
            var link = detector.Text.textInfo.linkCount > 0 ? detector.Text.textInfo.linkInfo[0].GetLinkID() : FirstLink(detector);
            detector.SetPointerLinkForTests(link);
            presenter.Tick(time += .05f);
            presenter.Tick(time += .06f);
        }

        private static string FirstLink(TooltipKeywordLinkDetector detector)
        {
            detector.Text.ForceMeshUpdate();
            return detector.Text.textInfo.linkInfo[0].GetLinkID();
        }

        [Test]
        public void ShowRoot_PlacesTheBoxOnTheAnchorTopRight()
        {
            Assert.That(presenter.ShowRoot(anchor, Blocks()), Is.True);
            var view = presenter.GetView(0);
            Assert.That(presenter.OpenCount, Is.EqualTo(1));
            Assert.That(view.gameObject.activeSelf, Is.True);
            Assert.That(TopLeftInFrame(view).x, Is.EqualTo(220f).Within(.01f));
            Assert.That(TopLeftInFrame(view).y, Is.EqualTo(720f).Within(.01f));
            Assert.That(view.Rect.rect.width, Is.InRange(TooltipView.DefaultMinimumWidth, TooltipView.DefaultMaximumWidth));
            Assert.That(view.Rect.rect.height, Is.GreaterThan(0f));
            Assert.That(view.Background.raycastTarget, Is.True);
        }

        [Test]
        public void Hide_WaitsForTheGraceAndTheBoxKeepsTheTooltip()
        {
            presenter.ShowRoot(anchor, Blocks());
            anchor.Over = false;
            presenter.Tick(1f);
            presenter.Tick(1.05f);
            Assert.That(presenter.OpenCount, Is.EqualTo(1), "inside the grace time");
            presenter.GetView(0).SetPointerOverForTests(true);
            presenter.Tick(1.2f);
            Assert.That(presenter.OpenCount, Is.EqualTo(1), "the pointer moved onto the box");
            presenter.GetView(0).SetPointerOverForTests(false);
            presenter.Tick(1.3f);
            presenter.Tick(1.39f);
            Assert.That(presenter.OpenCount, Is.Zero, "outside the chain after the grace time");
        }

        [Test]
        public void KeywordChain_StopsAtTheMaximumDepthAndNeverRepeatsAKeyword()
        {
            presenter.ShowRoot(anchor, Blocks());
            var time = 0f;
            presenter.Tick(time);
            for (var depth = 0; depth < 3; depth++) HoverKeyword(depth, ref time);
            Assert.That(presenter.OpenCount, Is.EqualTo(4), "root + 3 keyword tooltips");
            Assert.That(Enumerable.Range(1, 3).Select(presenter.GetKeywordId), Is.EqualTo(new[] { "kw.a", "kw.b", "kw.c" }));
            HoverKeyword(3, ref time);
            Assert.That(presenter.OpenCount, Is.EqualTo(4), "the maximum depth is 3");

            presenter.HideAll();
            presenter.SetTimingForEditor(0f, .1f, .08f, 10);
            presenter.ShowRoot(anchor, Blocks());
            for (var depth = 0; depth < 4; depth++) HoverKeyword(depth, ref time);
            Assert.That(presenter.OpenCount, Is.EqualTo(5), "kw.a → kw.b → kw.c → kw.d");
            HoverKeyword(4, ref time);
            Assert.That(presenter.OpenCount, Is.EqualTo(5), "kw.d links kw.a, which is already open");
        }

        [Test]
        public void KeywordTooltip_NeedsTheDelayAndClosesWithItsParent()
        {
            presenter.Tick(10f);
            presenter.ShowRoot(anchor, Blocks());
            var detector = presenter.GetView(0).ActiveTextBlocks[0];
            detector.SetPointerLinkForTests(FirstLink(detector));
            presenter.Tick(10.05f);
            Assert.That(presenter.OpenCount, Is.EqualTo(1), "before the keyword delay");
            presenter.Tick(10.11f);
            Assert.That(presenter.OpenCount, Is.EqualTo(2));
            var keywordView = presenter.GetView(1);
            Assert.That(TopLeftInFrame(keywordView).x, Is.GreaterThan(TopLeftInFrame(presenter.GetView(0)).x), "next to the keyword");
            anchor.Valid = false;
            presenter.Tick(10.12f);
            Assert.That(presenter.OpenCount, Is.Zero, "an invalid anchor closes the whole chain at once");
            Assert.That(keywordView.gameObject.activeSelf, Is.False);
        }

        [Test]
        public void MovedAnchor_ClosesAtOnce()
        {
            presenter.ShowRoot(anchor, Blocks());
            presenter.Tick(0f);
            anchor.Rect.anchoredPosition += new Vector2(0f, 30f);
            presenter.Tick(.01f);
            Assert.That(presenter.OpenCount, Is.Zero);
        }

        [Test]
        public void TallContent_IsLimitedToTheFrame()
        {
            var text = string.Join("\n", Enumerable.Range(0, 120).Select(i => $"줄 {i}"));
            UnityEngine.TestTools.LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("clipped"));
            presenter.ShowRoot(anchor, Blocks(text));
            var view = presenter.GetView(0);
            Assert.That(view.Rect.rect.height, Is.EqualTo(1080f - 2f * TooltipPresenter.DefaultFrameMargin).Within(.01f));
            Assert.That(TopLeftInFrame(view).y, Is.EqualTo(1080f - TooltipPresenter.DefaultFrameMargin).Within(.01f));
        }

        [Test]
        public void Touch_IsIgnoredAndTheCardFallbackIsShown()
        {
            var eventSystem = Track(new GameObject("EventSystem", typeof(EventSystem))).GetComponent<EventSystem>();
            var touch = new ExtendedPointerEventData(eventSystem) { pointerType = UIPointerType.Touch };
            var mouse = new ExtendedPointerEventData(eventSystem) { pointerType = UIPointerType.MouseOrPen };
            Assert.That(TooltipPresenter.IsTouch(touch), Is.True);
            Assert.That(TooltipPresenter.IsTouch(mouse), Is.False);

            var cardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CardPrefabPath);
            var card = ((GameObject)PrefabUtility.InstantiatePrefab(cardPrefab, frame)).GetComponent<RuneGridCardView>();
            var trigger = card.TooltipTrigger;
            Assert.That(trigger, Is.Not.Null);
            Assert.That(card.CardArea.Find("HitArea"), Is.Not.Null);
            Assert.That(card.BadgeArea.Find("HitArea"), Is.Not.Null);
            var catalog = Track(ScriptableObject.CreateInstance<RuneTooltipCatalog>());
            trigger.Bind(presenter, catalog, new RuneGridEntryDisplay("line.1", "rune.none", "이름 룬", 1));
            trigger.OnPointerEnter(touch);
            Assert.That(presenter.OpenCount, Is.Zero, "touch opens nothing");
            trigger.OnPointerEnter(mouse);
            Assert.That(presenter.OpenCount, Is.EqualTo(1));
            Assert.That(trigger.CreateBlocks().Select(b => b.Content), Is.EqualTo(new[] { "이름 룬", TooltipView.MissingDescription }));

            trigger.Bind(presenter, catalog, new RuneGridEntryDisplay("line.1", "rune.none", "이름 룬", 5));
            Assert.That(presenter.OpenCount, Is.EqualTo(1), "a weight change keeps the tooltip");
            trigger.Bind(presenter, catalog, new RuneGridEntryDisplay("line.2", "rune.none", "이름 룬", 5));
            Assert.That(presenter.OpenCount, Is.Zero, "another line closes it");
        }
    }
}
