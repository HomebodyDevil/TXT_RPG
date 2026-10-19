using NUnit.Framework;
using TMPro;
using TxTRPG.UI.ScreenFrame;
using UnityEngine;
using UnityEngine.TestTools;

namespace TxTRPG.UI.Tests
{
    public sealed class ScreenFrameControllerTests
    {
        private sealed class FixedInput : IScreenFrameInput
        {
            public Vector2 ScreenSize { get; set; }
            public Rect SafeArea { get; set; }
        }

        private GameObject canvasObject;
        private ScreenFrameController controller;
        private RectTransform frame;
        private ScreenFrameProfile profile;
        private FixedInput input;

        [SetUp]
        public void SetUp()
        {
            // Inactive while wiring so OnEnable reads the fixed input instead of the Game View.
            canvasObject = new GameObject("ScreenFrameCanvas", typeof(RectTransform), typeof(Canvas));
            canvasObject.SetActive(false);
            frame = new GameObject("ScreenFrame", typeof(RectTransform)).GetComponent<RectTransform>();
            frame.SetParent(canvasObject.transform, false);
            controller = canvasObject.AddComponent<ScreenFrameController>();
            input = new FixedInput { ScreenSize = new Vector2(2560f, 1080f), SafeArea = new Rect(0f, 0f, 2560f, 1080f) };
            controller.SetInput(input);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(canvasObject);
            if (profile != null) Object.DestroyImmediate(profile);
        }

        [Test]
        public void Refresh_PlacesFrameRootOnComputedFrame()
        {
            controller.Configure(null, frame, null);
            canvasObject.SetActive(true);
            Assert.That(controller.Current.IsValid);
            Assert.That(frame.anchorMin, Is.EqualTo(Vector2.zero));
            Assert.That(frame.anchorMax, Is.EqualTo(Vector2.zero));
            Assert.That(frame.pivot, Is.EqualTo(Vector2.zero));
            Assert.That(frame.anchoredPosition, Is.EqualTo(new Vector2(320f, 0f)));
            Assert.That(frame.sizeDelta, Is.EqualTo(new Vector2(1920f, 1080f)));
        }

        [Test]
        public void SameInput_DoesNotRaiseLayoutChangedAgain()
        {
            controller.Configure(null, frame, null);
            canvasObject.SetActive(true);
            var raised = 0;
            controller.LayoutChanged += _ => raised++;
            Assert.That(controller.RefreshIfChanged(), Is.False);
            Assert.That(controller.RefreshIfChanged(), Is.False);
            Assert.That(raised, Is.Zero);

            input.ScreenSize = new Vector2(1024f, 768f);
            input.SafeArea = new Rect(0f, 0f, 1024f, 768f);
            Assert.That(controller.RefreshIfChanged(), Is.True);
            Assert.That(raised, Is.EqualTo(1));
            Assert.That(frame.anchoredPosition.y, Is.EqualTo(180f).Within(.01f));
        }

        [Test]
        public void ZeroScreen_KeepsLastValidLayout()
        {
            controller.Configure(null, frame, null);
            canvasObject.SetActive(true);
            var before = controller.Current;
            input.ScreenSize = Vector2.zero;
            Assert.That(controller.RefreshIfChanged(), Is.False);
            Assert.That(controller.Current.FrameRect, Is.EqualTo(before.FrameRect));
            Assert.That(frame.anchoredPosition, Is.EqualTo(new Vector2(320f, 0f)));
        }

        [Test]
        public void MissingProfile_UsesDefaultsWithoutException()
        {
            var scaler = canvasObject.AddComponent<ScreenFrameCanvasScaler>();
            controller.Configure(scaler, frame, null);
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("no ScreenFrameProfile"));
            Assert.DoesNotThrow(() => canvasObject.SetActive(true));
            Assert.That(scaler.FrameReferenceResolution, Is.EqualTo(new Vector2(1920f, 1080f)));
            Assert.That(controller.Current.FrameRect.size, Is.EqualTo(new Vector2(1920f, 1080f)));
        }

        [Test]
        public void ProfileWithoutSafeArea_IgnoresSafeArea()
        {
            profile = ScriptableObject.CreateInstance<ScreenFrameProfile>();
            profile.Configure(new Vector2(1920f, 1080f), false, null, Color.black);
            var scaler = canvasObject.AddComponent<ScreenFrameCanvasScaler>();
            scaler.SetProfile(profile);
            controller.Configure(scaler, frame, null);
            input.SafeArea = new Rect(400f, 0f, 2160f, 1080f);
            canvasObject.SetActive(true);
            Assert.That(frame.anchoredPosition.x, Is.EqualTo(320f).Within(.01f));
        }

        [Test]
        public void ScalerScale_FollowsControllerLayout()
        {
            profile = ScriptableObject.CreateInstance<ScreenFrameProfile>();
            profile.Configure(new Vector2(1920f, 1080f), true, null, Color.black);
            var scaler = canvasObject.AddComponent<ScreenFrameCanvasScaler>();
            scaler.SetProfile(profile);
            controller.Configure(scaler, frame, null);
            input.ScreenSize = new Vector2(1024f, 768f);
            input.SafeArea = new Rect(0f, 0f, 1024f, 768f);
            canvasObject.SetActive(true);
            // The Canvas applies the scale only while it renders; Play Mode verification checks Canvas.scaleFactor.
            Assert.That(scaler.HasFrameScale);
            Assert.That(scaler.FrameScale, Is.EqualTo(1024f / 1920f).Within(.0001f));

            input.ScreenSize = new Vector2(3840f, 2160f);
            input.SafeArea = new Rect(0f, 0f, 3840f, 2160f);
            controller.RefreshIfChanged();
            Assert.That(scaler.FrameScale, Is.EqualTo(2f).Within(.0001f));

            canvasObject.SetActive(false);
            Assert.That(scaler.HasFrameScale, Is.False, "a disabled controller stops driving the scale");
        }

        [Test]
        public void ScaleChange_MarksAllTextForRegeneration()
        {
            controller.Configure(null, frame, null);
            controller.SetTextRegenerationInEditModeForTests(true);
            var active = CreateText("Active", true);
            var inactive = CreateText("Inactive", false);
            canvasObject.SetActive(true);
            Assert.That(controller.TextRegenerationCount, Is.EqualTo(1), "the first layout regenerates text once");

            active.havePropertiesChanged = false;
            inactive.havePropertiesChanged = false;
            // 2560x1080 -> 2400x1080 keeps scale 1: no regeneration.
            input.ScreenSize = new Vector2(2400f, 1080f);
            input.SafeArea = new Rect(0f, 0f, 2400f, 1080f);
            Assert.That(controller.RefreshIfChanged(), Is.True);
            Assert.That(controller.TextRegenerationCount, Is.EqualTo(1));
            Assert.That(active.havePropertiesChanged, Is.False);

            // 1280x720 -> scale 0.667: every text, active or not, is marked.
            input.ScreenSize = new Vector2(1280f, 720f);
            input.SafeArea = new Rect(0f, 0f, 1280f, 720f);
            Assert.That(controller.RefreshIfChanged(), Is.True);
            Assert.That(controller.TextRegenerationCount, Is.EqualTo(2));
            Assert.That(active.havePropertiesChanged, Is.True);
            Assert.That(inactive.havePropertiesChanged, Is.True);
        }

        [Test]
        public void EditMode_DoesNotMarkTextByDefault()
        {
            controller.Configure(null, frame, null);
            CreateText("Active", true);
            canvasObject.SetActive(true);
            input.ScreenSize = new Vector2(1280f, 720f);
            input.SafeArea = new Rect(0f, 0f, 1280f, 720f);
            controller.RefreshIfChanged();
            Assert.That(controller.TextRegenerationCount, Is.Zero, "marking text dirty in Edit Mode would mark the Scene as changed");
        }

        private TextMeshProUGUI CreateText(string name, bool active)
        {
            var text = new GameObject(name, typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            text.transform.SetParent(frame, false);
            text.text = "글자";
            text.gameObject.SetActive(active);
            return text;
        }

        [Test]
        public void SafeArea_MovesFrameInsideIt()
        {
            controller.Configure(null, frame, null);
            input.SafeArea = new Rect(400f, 0f, 2160f, 1080f);
            canvasObject.SetActive(true);
            // Safe 400..2560 (2160 wide): frame centered at 1480 -> x 520.
            Assert.That(frame.anchoredPosition.x, Is.EqualTo(520f).Within(.01f));
        }

        [Test]
        public void AlignedRect_FollowsFrameAsCanvasFractions()
        {
            controller.Configure(null, frame, null);
            var aligned = new GameObject("FrameArea", typeof(RectTransform)).AddComponent<ScreenFrameAlignedRect>();
            aligned.transform.SetParent(canvasObject.transform, false);
            canvasObject.SetActive(true);
            // OnEnable does not run in Edit Mode for this component; SetController binds an active instance.
            aligned.SetController(controller);
            var rect = (RectTransform)aligned.transform;
            Assert.That(rect.anchorMin.x, Is.EqualTo(320f / 2560f).Within(.0001f));
            Assert.That(rect.anchorMax.x, Is.EqualTo(2240f / 2560f).Within(.0001f));
            Assert.That(rect.anchorMin.y, Is.EqualTo(0f).Within(.0001f));
            Assert.That(rect.anchorMax.y, Is.EqualTo(1f).Within(.0001f));
            Assert.That(rect.sizeDelta, Is.EqualTo(Vector2.zero));

            input.ScreenSize = new Vector2(1024f, 768f);
            input.SafeArea = new Rect(0f, 0f, 1024f, 768f);
            controller.RefreshIfChanged();
            Assert.That(rect.anchorMin, Is.EqualTo(new Vector2(0f, 180f / 1440f)));
            Assert.That(rect.anchorMax.y, Is.EqualTo(1260f / 1440f).Within(.0001f));
        }

        [Test]
        public void AlignedRect_WithoutController_StretchesOverParent()
        {
            var aligned = new GameObject("FrameArea", typeof(RectTransform)).AddComponent<ScreenFrameAlignedRect>();
            aligned.transform.SetParent(canvasObject.transform, false);
            aligned.Apply(ScreenFrameLayout.Invalid);
            var rect = (RectTransform)aligned.transform;
            Assert.That(rect.anchorMin, Is.EqualTo(Vector2.zero));
            Assert.That(rect.anchorMax, Is.EqualTo(Vector2.one));
        }
    }
}
