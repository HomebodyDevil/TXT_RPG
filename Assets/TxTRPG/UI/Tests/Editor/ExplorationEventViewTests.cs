using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TMPro;
using TxTRPG.UI.Exploration;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TxTRPG.UI.Tests
{
    public sealed class ExplorationEventViewTests
    {
        private readonly List<Object> created = new();
        private ExplorationEventView view;
        private ExplorationEventChoiceView template;

        [SetUp]
        public void SetUp()
        {
            var canvas = Track(new GameObject("Canvas", typeof(Canvas)));
            var root = Rect("EventView", canvas.transform);
            root.sizeDelta = new Vector2(600f, 500f);
            var visual = Rect("VisualRoot", root);
            var title = Text("Title", visual);
            var bodyScroll = Rect("BodyScroll", visual).gameObject.AddComponent<ScrollRect>();
            var body = Text("Body", bodyScroll.transform);
            var choicesScroll = Rect("ChoicesScroll", visual).gameObject.AddComponent<ScrollRect>();
            var layout = choicesScroll.gameObject.AddComponent<LayoutElement>();
            var content = Rect("Content", choicesScroll.transform);
            content.gameObject.AddComponent<VerticalLayoutGroup>();
            var choice = Rect("ChoiceTemplate", content);
            var hit = choice.gameObject.AddComponent<Image>();
            var choiceVisual = Rect("VisualRoot", choice);
            var background = choiceVisual.gameObject.AddComponent<Image>();
            background.raycastTarget = false;
            var button = choice.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            template = choice.gameObject.AddComponent<ExplorationEventChoiceView>();
            var reason = Text("Reason", choice);
            reason.gameObject.SetActive(false);
            template.ConfigureForEditor(button, choiceVisual, Text("Label", choice), reason);
            Assert.That(hit.raycastTarget, Is.True);
            view = root.gameObject.AddComponent<ExplorationEventView>();
            view.ConfigureForEditor(visual, title, bodyScroll, body, choicesScroll, layout, content, template);
            view.Hide();
        }

        [TearDown]
        public void TearDown()
        {
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            foreach (var item in created) if (item != null) Object.DestroyImmediate(item);
            created.Clear();
        }

        [Test]
        public void Show_ReusesTheTemplateAndHidesSurplusChoices()
        {
            view.Show(Page(3), null);
            var first = view.Choices.ToArray();
            Assert.That(first.Length, Is.EqualTo(3));
            Assert.That(template.gameObject.activeSelf, Is.False, "the template itself is never shown");
            view.Show(Page(1), null);
            Assert.That(view.Choices.Count, Is.EqualTo(3), "instances are reused");
            Assert.That(view.Choices.Count(choice => choice.gameObject.activeSelf), Is.EqualTo(1));
            view.Show(Page(4), null);
            Assert.That(view.Choices.Take(3), Is.EqualTo(first));
            Assert.That(view.Choices.Count(choice => choice.gameObject.activeSelf), Is.EqualTo(4));
            Assert.That(view.TitleText, Is.EqualTo("Title"));
            Assert.That(view.BodyText, Is.EqualTo("Body"));
        }

        [Test]
        public void DisabledChoice_ShowsItsReasonAndCannotBeUsed()
        {
            var picked = new List<int>();
            view.Show(new ExplorationEventPageDisplay("t", "b", new[]
            {
                new ExplorationEventChoiceDisplay("open", true, "ignored"),
                new ExplorationEventChoiceDisplay("locked", false, "조건 미충족: 체력 6 이상 필요")
            }), picked.Add);
            Assert.That(view.Choices[0].ReasonText, Is.Empty, "an enabled choice shows no reason");
            Assert.That(view.Choices[1].ReasonText, Is.EqualTo("조건 미충족: 체력 6 이상 필요"));
            Assert.That(view.Choices[1].Button.interactable, Is.False);
            view.RequestForEditor(1);
            Assert.That(picked, Is.Empty);
        }

        [Test]
        public void OneChoiceLocksEveryChoiceUntilTheNextPage()
        {
            var picked = new List<int>();
            view.Show(Page(2), picked.Add);
            view.Choices[1].Button.onClick.Invoke();
            view.Choices[0].Button.onClick.Invoke();
            view.Choices[1].Button.onClick.Invoke();
            Assert.That(picked, Is.EqualTo(new[] { 1 }));
            Assert.That(view.IsLocked, Is.True);
            Assert.That(view.Choices.All(choice => !choice.Button.interactable), Is.True);
            view.Show(Page(2), picked.Add);
            Assert.That(view.IsLocked, Is.False);
            view.Choices[0].Button.onClick.Invoke();
            Assert.That(picked, Is.EqualTo(new[] { 1, 0 }));
        }

        [Test]
        public void BusyPage_StartsLocked()
        {
            var picked = new List<int>();
            view.Show(new ExplorationEventPageDisplay("t", "b", new[] { new ExplorationEventChoiceDisplay("spin", true, "") }, true), picked.Add);
            view.Choices[0].Button.onClick.Invoke();
            Assert.That(picked, Is.Empty);
            Assert.That(view.Choices[0].Button.interactable, Is.False);
        }

        [Test]
        public void Show_DoesNotSelectAChoice()
        {
            var previous = EventSystem.current;
            var events = Track(new GameObject("EventSystem", typeof(ExplorationTestEventSystem))).GetComponent<EventSystem>();
            try
            {
                EventSystem.current = events;
                view.Show(Page(2), null);
                Assert.That(events.currentSelectedGameObject, Is.Null);
                events.SetSelectedGameObject(view.Choices[0].Button.gameObject);
                view.Show(Page(2), null);
                Assert.That(events.currentSelectedGameObject, Is.Null, "a new page releases the focus the view owned");
            }
            finally { if (previous != null) EventSystem.current = previous; }
        }

        [Test]
        public void Hide_DeactivatesAndStaleCallbacksAreIgnored()
        {
            var picked = new List<int>();
            view.Show(Page(1), picked.Add);
            var stale = view.Choices[0].Button;
            view.Hide();
            Assert.That(view.gameObject.activeSelf, Is.False);
            stale.onClick.Invoke();
            Assert.That(picked, Is.Empty);
        }

        [Test]
        public void TextAndDecorationsDoNotBlockRaycasts()
        {
            view.Show(Page(2), null);
            foreach (var choice in view.Choices.Where(c => c.gameObject.activeSelf))
            {
                Assert.That(choice.VisualRoot.GetComponent<Graphic>().raycastTarget, Is.False);
                Assert.That(choice.GetComponentsInChildren<TMP_Text>(true).All(text => !text.raycastTarget), Is.True);
            }
        }

        private static ExplorationEventPageDisplay Page(int count) =>
            new("Title", "Body", Enumerable.Range(0, count).Select(i => new ExplorationEventChoiceDisplay($"Choice {i}", true, string.Empty)).ToArray());

        private T Track<T>(T item) where T : Object { created.Add(item); return item; }

        private static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static TextMeshProUGUI Text(string name, Transform parent)
        {
            var text = Rect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
            text.raycastTarget = false;
            return text;
        }
    }
}
