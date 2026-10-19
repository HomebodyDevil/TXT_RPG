using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using TMPro;
using TxTRPG.Application.Encounters;
using TxTRPG.Gameplay.Encounters;
using TxTRPG.UI.Encounters;
using TxTRPG.UI.ScreenFrame;
using TxTRPG.UI.SlotMachine;
using TxTRPG.UI.Windows;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TxTRPG.Application.Editor
{
    // TEMPORARY Play Mode verification (DOCS/instructions/screen-frame-letterbox-resolution-policy.md). Enter Play Mode
    // through AppScene so Candidate_MainScene is the content scene, then run a menu:
    // - "Capture Candidate Layout 1920x1080": works before and after the screen-frame application; logs the screen rects
    //   of MainArea's areas, SlotReelDock and EncounterDebugPanel, text and control sizes, and captures the Game View;
    //   then regenerates every TMP mesh and captures again ("-text-refreshed"). The two captures match when
    //   ScreenFrameController regenerated text after the canvas scale change.
    // - "Compile Player Scripts (Windows, Android)": compiles player scripts for installed targets (no build).
    // - "Verify Candidate Screen Frame": sweeps 1920x1080, 1280x720, 2560x1080, 2400x1080, 1024x768, 2048x1536, 1080x1920
    //   and 2400x1080 with a simulated notch safe area (injected through ScreenFrameController.SetInput), checks the frame,
    //   letterbox bars, dock and debug panel, that the same size does not recalculate, opens the result modal at 2560x1080
    //   and closes it by a pointer click on the left letterbox, and swaps in a textured letterbox presentation at 1024x768.
    // Game View sizes are added temporarily and removed by total index; the previous size, presentation, input and modal
    // state are restored. Logs use SCREEN_FRAME_VERIFY; captures and the JSON report go to the folder in
    // TXTRPG_VERIFY_OUTPUT (default %TEMP%/TxTRPG-ScreenFrameVerification). Nothing is saved; safe to rerun. Retire it with
    // TemporaryScreenFrameApply.
    public static class TemporaryScreenFrameVerification
    {
        public const string BaselineMenuPath = "Tools/TxT RPG/Application/Temporary/Capture Candidate Layout 1920x1080";
        public const string VerifyMenuPath = "Tools/TxT RPG/Application/Temporary/Verify Candidate Screen Frame";
        public const string CompileMenuPath = "Tools/TxT RPG/Application/Temporary/Compile Player Scripts (Windows, Android)";
        private const string Log = "SCREEN_FRAME_VERIFY: ";

        private sealed class Entry
        {
            public string name; public int width; public int height; public Rect? safe; public bool modal; public bool swap; public bool measureText;
        }

        private sealed class FakeInput : IScreenFrameInput
        {
            public Rect safe;
            public Vector2 ScreenSize => new Vector2(Screen.width, Screen.height);
            public Rect SafeArea => safe;
        }

        private static readonly Entry[] Sweep =
        {
            new Entry { name = "1920x1080", width = 1920, height = 1080, measureText = true },
            new Entry { name = "1280x720", width = 1280, height = 720 },
            new Entry { name = "2560x1080", width = 2560, height = 1080, modal = true },
            new Entry { name = "2400x1080", width = 2400, height = 1080 },
            new Entry { name = "1024x768", width = 1024, height = 768, swap = true },
            new Entry { name = "2048x1536", width = 2048, height = 1536 },
            new Entry { name = "1080x1920", width = 1080, height = 1920 },
            // 20:9 phone in landscape: 132 px cutout on the left, 63 px gesture area at the bottom.
            new Entry { name = "2400x1080-notch", width = 2400, height = 1080, safe = new Rect(132f, 63f, 2268f, 1017f) },
        };

        private static Entry[] entries;
        private static int index;
        private static int phase;
        private static double next;
        private static int recalculationsAtMeasure;
        private static bool baselineOnly;
        private static bool passed;
        private static readonly StringBuilder report = new StringBuilder();
        private static EditorWindow view;
        private static PropertyInfo selectedIndex;
        private static object group;
        private static int previousIndex;
        private static int customIndex = -1;
        private static Texture2D checker;

        [MenuItem(BaselineMenuPath, false, 3031)]
        public static void CaptureBaseline() => Start(new[] { new Entry { name = "1920x1080", width = 1920, height = 1080, measureText = true } }, true);

        [MenuItem(VerifyMenuPath, false, 3032)]
        public static void Verify() => Start(Sweep, false);

        // Compiles player scripts (no build, no asset changes) into a temporary folder for each target whose module is
        // installed. Compiler errors appear in the Console; the summary uses the SCREEN_FRAME_VERIFY prefix.
        [MenuItem(CompileMenuPath, false, 3033)]
        public static void CompilePlayerScripts()
        {
            foreach (var (target, group) in new[] { (BuildTarget.StandaloneWindows64, BuildTargetGroup.Standalone), (BuildTarget.Android, BuildTargetGroup.Android) })
            {
                if (!BuildPipeline.IsBuildTargetSupported(group, target)) { Debug.LogWarning(Log + $"compile {target}: SKIPPED (module not installed)"); continue; }
                var output = Path.Combine(Path.GetTempPath(), "TxTRPG-PlayerScripts-" + target);
                var settings = new UnityEditor.Build.Player.ScriptCompilationSettings { target = target, group = group };
                var result = UnityEditor.Build.Player.PlayerBuildInterface.CompilePlayerScripts(settings, output);
                var ok = result.assemblies != null && result.assemblies.Contains("TxTRPG.UI.dll");
                if (ok) Debug.Log(Log + $"compile {target}: PASS {result.assemblies.Count} assemblies");
                else Debug.LogError(Log + $"compile {target}: FAIL ({result.assemblies?.Count ?? 0} assemblies, TxTRPG.UI.dll missing; see Console errors)");
            }
        }

        private static string OutputFolder
        {
            get
            {
                var folder = Environment.GetEnvironmentVariable("TXTRPG_VERIFY_OUTPUT");
                return string.IsNullOrEmpty(folder) ? Path.Combine(Path.GetTempPath(), "TxTRPG-ScreenFrameVerification") : folder;
            }
        }

        private static void Start(Entry[] list, bool baseline)
        {
            if (!EditorApplication.isPlaying) { Debug.LogError(Log + "FAIL enter Play Mode through AppScene first."); return; }
            if (!SceneManager.GetSceneByPath(TemporaryScreenFrameApply.ScenePath).isLoaded) { Debug.LogError(Log + "FAIL Candidate_MainScene is not loaded."); return; }
            if (entries != null) { Debug.LogError(Log + "FAIL a verification is already running."); return; }
            Directory.CreateDirectory(OutputFolder);
            var assembly = typeof(EditorWindow).Assembly;
            var viewType = assembly.GetType("UnityEditor.GameView");
            view = EditorWindow.GetWindow(viewType);
            selectedIndex = viewType.GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            previousIndex = (int)selectedIndex.GetValue(view);
            var sizesType = assembly.GetType("UnityEditor.GameViewSizes");
            var singleton = typeof(ScriptableSingleton<>).MakeGenericType(sizesType).GetProperty("instance").GetValue(null);
            group = sizesType.GetMethod("GetGroup").Invoke(singleton, new[] { Enum.Parse(assembly.GetType("UnityEditor.GameViewSizeGroupType"), "Standalone") });
            Debug.Log(Log + "Standalone custom Game View sizes before: " + (int)group.GetType().GetMethod("GetCustomCount").Invoke(group, null));
            entries = list; baselineOnly = baseline; index = 0; phase = 0; passed = true; report.Clear();
            next = EditorApplication.timeSinceStartup + .2;
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += PlayChanged;
        }

        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup < next) return;
            try
            {
                var entry = entries[index];
                var controller = FindInScene<ScreenFrameController>();
                switch (phase)
                {
                    case 0:
                        SelectSize(entry);
                        phase = 1; next = EditorApplication.timeSinceStartup + 1.5; return;
                    case 1:
                        if (Screen.width != entry.width || Screen.height != entry.height)
                            throw new InvalidOperationException($"expected {entry.width}x{entry.height}, Game View is {Screen.width}x{Screen.height}");
                        if (entry.safe.HasValue && controller != null)
                        {
                            controller.SetInput(new FakeInput { safe = entry.safe.Value });
                            phase = 2; next = EditorApplication.timeSinceStartup + .5; return;
                        }
                        goto case 2;
                    case 2:
                        Measure(entry, controller);
                        Capture(entry.name);
                        recalculationsAtMeasure = controller != null ? controller.RecalculationCount : 0;
                        phase = baselineOnly ? 7 : 3; next = EditorApplication.timeSinceStartup + 1; return;
                    case 7:
                        // Diagnostic: regenerate every TMP mesh at the current canvas scale and capture again.
                        foreach (var text in Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None)) text.ForceMeshUpdate(true, true);
                        Capture(entry.name + "-text-refreshed");
                        phase = 3; next = EditorApplication.timeSinceStartup + 1; return;
                    case 3:
                        if (controller != null)
                            Check(entry.name, controller.RecalculationCount == recalculationsAtMeasure,
                                $"no recalculation while the size stays the same ({recalculationsAtMeasure} -> {controller.RecalculationCount})");
                        if (entry.modal && controller != null) { OpenModal(entry); phase = 4; next = EditorApplication.timeSinceStartup + .7; return; }
                        if (entry.swap && controller != null) { SwapPresentation(controller); phase = 5; next = EditorApplication.timeSinceStartup + .5; return; }
                        goto case 9;
                    case 4:
                        CheckModal(entry, controller);
                        goto case 9;
                    case 5:
                        Capture(entry.name + "-textured-letterbox");
                        phase = 6; next = EditorApplication.timeSinceStartup + .5; return;
                    case 6:
                        RestorePresentation(controller);
                        goto case 9;
                    case 9:
                        if (controller != null) controller.SetInput(null);
                        RemoveSize();
                        index++;
                        if (index < entries.Length) { phase = 0; next = EditorApplication.timeSinceStartup + .3; return; }
                        File.WriteAllText(Path.Combine(OutputFolder, baselineOnly ? "baseline.txt" : "report.txt"), report.ToString());
                        Finish();
                        if (passed) Debug.Log(Log + "PASS " + (baselineOnly ? "baseline captured" : "all sizes") + " -> " + OutputFolder);
                        else Debug.LogError(Log + "FAIL see the checks above -> " + OutputFolder);
                        return;
                }
            }
            catch (Exception exception)
            {
                Finish();
                Debug.LogError(Log + "FAIL " + exception);
            }
        }

        private static void Measure(Entry entry, ScreenFrameController controller)
        {
            var canvas = controller != null ? controller.GetComponent<Canvas>() : FindInScene<Canvas>();
            Write($"== {entry.name} screen {Screen.width}x{Screen.height} safe {(entry.safe ?? Screen.safeArea)} canvasScale {canvas.scaleFactor:0.####}");
            var dock = FindInScene<SlotReelDock>();
            var debug = FindInScene<EncounterDebugPanel>();
            if (controller == null)
            {
                foreach (var name in new[] { "LeftArea", "MiddleArea", "RightArea" })
                {
                    var area = FindByName(name);
                    if (area != null) Write($"  {name} screen {ScreenRect(area)} size {area.rect.size}");
                }
                if (dock != null) Write($"  SlotReelDock screen {ScreenRect((RectTransform)dock.transform)} size {((RectTransform)dock.transform).rect.size}");
                if (debug != null) Write($"  EncounterDebugPanel screen {ScreenRect((RectTransform)debug.transform)}");
                if (entry.measureText) MeasureText(canvas.transform);
                return;
            }

            var layout = controller.Current;
            var frame = ScreenRect(controller.FrameRoot);
            Write($"  layout {layout}");
            Write($"  frame screen {frame} (canvas {layout.FrameRect})");
            var expected = ScreenFrameMath.Calculate(new Vector2(Screen.width, Screen.height), entry.safe ?? Screen.safeArea, new Vector2(1920f, 1080f));
            Check(entry.name, Mathf.Abs(canvas.scaleFactor - expected.ScaleFactor) < .001f, $"canvas scale {canvas.scaleFactor:0.####} = {expected.ScaleFactor:0.####}");
            Check(entry.name, Near(frame.size, new Vector2(1920f, 1080f) * expected.ScaleFactor, 1f), $"frame pixel size {frame.size}");
            var safe = entry.safe ?? new Rect(0f, 0f, Screen.width, Screen.height);
            Check(entry.name, Inside(frame, safe, 1f), $"frame inside safe area {safe}");
            Check(entry.name, Mathf.Abs(frame.center.x - safe.center.x) < 1f && Mathf.Abs(frame.center.y - safe.center.y) < 1f, "frame centered in safe area");
            Check(entry.name, controller.FrameRoot.rect.size == new Vector2(1920f, 1080f), $"frame canvas size {controller.FrameRoot.rect.size}");

            foreach (var name in new[] { "LeftArea", "MiddleArea", "RightArea" })
            {
                var area = FindByName(name);
                if (area != null) Write($"  {name} screen {ScreenRect(area)} size {area.rect.size}");
            }
            if (dock != null)
            {
                var dockRect = ScreenRect((RectTransform)dock.transform);
                Write($"  SlotReelDock screen {dockRect} size {((RectTransform)dock.transform).rect.size} anchored {((RectTransform)dock.transform).anchoredPosition}");
                Check(entry.name, Inside(dockRect, frame, 1f), "SlotReelDock inside frame");
            }
            if (debug != null)
            {
                var debugRect = ScreenRect((RectTransform)debug.transform);
                Write($"  EncounterDebugPanel screen {debugRect}");
                Check(entry.name, Inside(debugRect, frame, 1f), "EncounterDebugPanel inside frame");
            }

            var bars = controller.Letterbox != null ? controller.Letterbox.Presentation as LetterboxBarsPresentation : null;
            Check(entry.name, bars != null, "letterbox uses LetterboxBarsPresentation");
            if (bars != null)
            {
                var screenArea = Screen.width * (float)Screen.height;
                var covered = frame.width * frame.height;
                foreach (var (barName, rect, image, area) in new[]
                         {
                             ("Top", bars.Top, bars.TopImage, layout.TopBar), ("Bottom", bars.Bottom, bars.BottomImage, layout.BottomBar),
                             ("Left", bars.Left, bars.LeftImage, layout.LeftBar), ("Right", bars.Right, bars.RightImage, layout.RightBar),
                         })
                {
                    var barRect = ScreenRect(rect);
                    var visible = area.width > 0.01f && area.height > 0.01f;
                    Write($"  bar {barName} canvas {area.size} screen {barRect} image {image.enabled}");
                    Check(entry.name, image.enabled == visible, $"bar {barName} image enabled = {visible}");
                    Check(entry.name, !image.raycastTarget, $"bar {barName} not a raycast target");
                    if (visible) covered += barRect.width * barRect.height;
                }
                Check(entry.name, Mathf.Abs(covered - screenArea) < screenArea * .002f, $"frame + bars cover the screen ({covered:0} / {screenArea:0})");
            }
            if (entry.measureText) MeasureText(controller.FrameRoot);
        }

        // Font sizes and interactive control sizes in canvas units (the frame is always 1920x1080 units).
        private static void MeasureText(Transform root)
        {
            var texts = root.GetComponentsInChildren<TMP_Text>(false).Where(text => text.isActiveAndEnabled && !string.IsNullOrEmpty(text.text)).ToArray();
            foreach (var bucket in texts.GroupBy(text => Mathf.Round(text.fontSize * 10f) / 10f).OrderBy(group => group.Key))
                Write($"  text {bucket.Key:0.#} units x{bucket.Count()}: {string.Join(", ", bucket.Take(6).Select(text => text.name + "(" + Shorten(text.text) + ")"))}");
            foreach (var selectable in root.GetComponentsInChildren<Selectable>(false).Where(item => item.isActiveAndEnabled).OrderBy(item => ((RectTransform)item.transform).rect.height))
            {
                var rect = (RectTransform)selectable.transform;
                Write($"  control {selectable.GetType().Name} {HierarchyPath(selectable.transform)} {rect.rect.width:0.#}x{rect.rect.height:0.#} units");
            }
        }

        private static void OpenModal(Entry entry)
        {
            var roll = FindInScene<EncounterRollController>();
            if (roll == null || !roll.IsReady || !roll.HasResultModal) { Check(entry.name, false, "EncounterRollController with a result modal"); return; }
            if (roll.ResultWindow.IsOpen) roll.ResultWindow.Close();
            roll.Selection.Clear();
            roll.Selection.SetSelected(TemporaryEncounterTogglesApply.RuneId, true);
            roll.SetSettings(new EncounterRollSettings(1f, .5f, 1f, 0f, 0f));
            var result = roll.RollNow();
            Check(entry.name, result?.Outcome == EncounterRollOutcome.Occurred && roll.ResultWindow.IsOpen, $"modal opened ({result?.Outcome})");
        }

        private static void CheckModal(Entry entry, ScreenFrameController controller)
        {
            var roll = FindInScene<EncounterRollController>();
            var host = roll.ResultWindow.GetComponentInChildren<ModalWindowHost>(true);
            try
            {
                var frame = ScreenRect(controller.FrameRoot);
                var backdrop = ScreenRect((RectTransform)host.transform);
                var window = ScreenRect(host.WindowRect);
                var frameArea = host.transform.Find("FrameArea");
                var aligned = frameArea != null ? frameArea.GetComponent<ScreenFrameAlignedRect>() : null;
                Write($"  modal backdrop {backdrop} window {window} frameArea {(frameArea != null ? ScreenRect((RectTransform)frameArea).ToString() : "missing")} controller {(aligned != null && aligned.Controller == controller)}");
                Check(entry.name, Near(backdrop.size, new Vector2(Screen.width, Screen.height), 1f), "modal backdrop covers the whole screen");
                Check(entry.name, aligned != null && aligned.Controller == controller, "FrameArea references the Scene ScreenFrameController");
                Check(entry.name, frameArea != null && Near(ScreenRect((RectTransform)frameArea).size, frame.size, 1f) && Near(ScreenRect((RectTransform)frameArea).position, frame.position, 1f), "FrameArea equals the frame");
                Check(entry.name, Inside(window, frame, 1f), "modal window inside frame");
                Check(entry.name, Mathf.Abs(window.center.x - frame.center.x) < 1f && Mathf.Abs(window.center.y - frame.center.y) < 1f, "modal window centered in frame");
                Capture(entry.name + "-modal");

                // Pointer press and click on the left letterbox (outside the frame).
                var point = new Vector2(frame.xMin * .5f, frame.center.y);
                var eventSystem = EventSystem.current;
                var data = new PointerEventData(eventSystem) { position = point, button = PointerEventData.InputButton.Left, pointerId = -1 };
                var hits = new List<RaycastResult>();
                eventSystem.RaycastAll(data, hits);
                var top = hits.Count > 0 ? hits[0] : default;
                Write($"  letterbox click at {point}: top hit {(top.gameObject != null ? HierarchyPath(top.gameObject.transform) : "none")}");
                Check(entry.name, top.gameObject == host.gameObject, "the backdrop (not the letterbox) receives the letterbox click");
                data.pointerCurrentRaycast = top; data.pointerPressRaycast = top; data.eligibleForClick = true;
                data.pointerPress = top.gameObject; data.rawPointerPress = top.gameObject;
                if (top.gameObject != null)
                {
                    ExecuteEvents.Execute(top.gameObject, data, ExecuteEvents.pointerDownHandler);
                    ExecuteEvents.Execute(top.gameObject, data, ExecuteEvents.pointerUpHandler);
                    ExecuteEvents.Execute(top.gameObject, data, ExecuteEvents.pointerClickHandler);
                }
                Check(entry.name, !roll.ResultWindow.IsOpen, "letterbox click closes the modal");
            }
            finally
            {
                if (roll.ResultWindow.IsOpen) roll.ResultWindow.Close();
                roll.ResetSettingsToConfiguration();
                roll.Selection.Clear();
            }
        }

        private static GameObject swapTemplate;

        private static void SwapPresentation(ScreenFrameController controller)
        {
            checker = new Texture2D(8, 8) { filterMode = FilterMode.Point };
            for (var y = 0; y < 8; y++)
                for (var x = 0; x < 8; x++)
                    checker.SetPixel(x, y, (x + y) % 2 == 0 ? new Color(.85f, .2f, .5f) : new Color(.15f, .2f, .6f));
            checker.Apply();
            var sprite = Sprite.Create(checker, new Rect(0, 0, 8, 8), new Vector2(.5f, .5f), 100f);
            var bars = LetterboxBarsPresentation.Create(null, Color.white);
            bars.gameObject.name = "TemporaryTexturedLetterbox";
            foreach (var image in new[] { bars.TopImage, bars.BottomImage, bars.LeftImage, bars.RightImage })
            {
                image.sprite = sprite; image.type = Image.Type.Tiled; image.raycastTarget = true;
            }
            bars.gameObject.SetActive(false);
            swapTemplate = bars.gameObject;
            var before = controller.Letterbox.PresentationInstance;
            controller.Letterbox.SetPresentation(swapTemplate);
            var current = controller.Letterbox.Presentation as LetterboxBarsPresentation;
            current.gameObject.SetActive(true);
            Check("swap", before == null || !before.activeSelf, "previous presentation removed");
            Check("swap", current != null && current.TopImage.sprite == sprite && current.TopImage.enabled, "textured presentation received the last layout");
            Check("swap", controller.Letterbox.PresentationInstance.GetComponentsInChildren<Graphic>(true).All(graphic => !graphic.raycastTarget), "textured presentation graphics are not raycast targets");
        }

        private static void RestorePresentation(ScreenFrameController controller)
        {
            controller.Letterbox.SetPresentation(controller.Letterbox.Profile != null ? controller.Letterbox.Profile.LetterboxPresentation : null);
            Check("swap", !controller.Letterbox.IsUsingFallback, "default presentation restored");
            if (swapTemplate != null) Object.Destroy(swapTemplate);
            if (checker != null) Object.Destroy(checker);
            swapTemplate = null; checker = null;
        }

        private static void SelectSize(Entry entry)
        {
            var assembly = typeof(EditorWindow).Assembly;
            var size = Activator.CreateInstance(assembly.GetType("UnityEditor.GameViewSize"),
                Enum.Parse(assembly.GetType("UnityEditor.GameViewSizeType"), "FixedResolution"), entry.width, entry.height, "Screen frame verification");
            customIndex = (int)group.GetType().GetMethod("GetCustomCount").Invoke(group, null);
            var total = (int)group.GetType().GetMethod("GetBuiltinCount").Invoke(group, null) + customIndex;
            group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { size });
            selectedIndex.SetValue(view, total);
            view.Repaint();
        }

        private static void RemoveSize()
        {
            if (customIndex < 0) return;
            selectedIndex.SetValue(view, previousIndex);
            // RemoveCustomSize takes the total index (built-in sizes first), not the custom-list index.
            var builtin = (int)group.GetType().GetMethod("GetBuiltinCount").Invoke(group, null);
            group.GetType().GetMethod("RemoveCustomSize").Invoke(group, new object[] { builtin + customIndex });
            customIndex = -1;
            view.Repaint();
        }

        private static void PlayChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode) Finish();
        }

        private static void Finish()
        {
            EditorApplication.update -= Tick;
            EditorApplication.playModeStateChanged -= PlayChanged;
            try { RemoveSize(); } catch (Exception exception) { Debug.LogError(Log + "could not remove the temporary Game View size: " + exception.Message); }
            if (group != null) Debug.Log(Log + "Standalone custom Game View sizes after: " + (int)group.GetType().GetMethod("GetCustomCount").Invoke(group, null));
            var controller = FindInScene<ScreenFrameController>();
            if (controller != null) controller.SetInput(null);
            entries = null;
        }

        private static void Capture(string name) => ScreenCapture.CaptureScreenshot(Path.Combine(OutputFolder, (baselineOnly ? "baseline-" : "frame-") + name + ".png"));

        private static void Check(string scope, bool ok, string detail)
        {
            passed &= ok;
            Write($"  [{(ok ? "PASS" : "FAIL")}] {detail}");
            if (ok) Debug.Log(Log + $"{scope} PASS {detail}");
            else Debug.LogError(Log + $"{scope} FAIL {detail}");
        }

        private static void Write(string line)
        {
            report.AppendLine(line);
            Debug.Log(Log + line.Trim());
        }

        private static T FindInScene<T>() where T : Component
        {
            var scene = SceneManager.GetSceneByPath(TemporaryScreenFrameApply.ScenePath);
            return scene.isLoaded ? scene.GetRootGameObjects().Select(root => root.GetComponentInChildren<T>(true)).FirstOrDefault(found => found != null) : null;
        }

        private static RectTransform FindByName(string name)
        {
            var canvas = FindInScene<Canvas>();
            return canvas == null ? null : canvas.GetComponentsInChildren<RectTransform>(true).FirstOrDefault(rect => rect.name == name);
        }

        private static readonly Vector3[] Corners = new Vector3[4];

        private static Rect ScreenRect(RectTransform rect)
        {
            rect.GetWorldCorners(Corners);
            return Rect.MinMaxRect(Corners[0].x, Corners[0].y, Corners[2].x, Corners[2].y);
        }

        private static bool Inside(Rect inner, Rect outer, float tolerance) =>
            inner.xMin >= outer.xMin - tolerance && inner.yMin >= outer.yMin - tolerance && inner.xMax <= outer.xMax + tolerance && inner.yMax <= outer.yMax + tolerance;

        private static bool Near(Vector2 a, Vector2 b, float tolerance) => Mathf.Abs(a.x - b.x) <= tolerance && Mathf.Abs(a.y - b.y) <= tolerance;

        private static string HierarchyPath(Transform transform) => transform.parent == null ? transform.name : HierarchyPath(transform.parent) + "/" + transform.name;

        private static string Shorten(string text)
        {
            text = text.Replace("\n", " ");
            return text.Length > 12 ? text.Substring(0, 12) + "…" : text;
        }
    }
}
