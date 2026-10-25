using System;
using System.Linq;
using System.Reflection;
using System.Text;
using TxTRPG.UI;
using TxTRPG.UI.Runes;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TxTRPG.Application.Editor
{
    // TEMPORARY Play Mode measurement (candidate-main-scene-initial-content-and-layout). Enter Play Mode through
    // AppScene so Candidate_MainScene is the content scene, then run the menu. It switches the Game View through several
    // fixed resolutions (each added as a temporary Standalone custom size and removed again by its total index), logs the
    // Left/Middle/Right area rects with the CANDIDATE_LAYOUT prefix and captures screenshots under Temp/. Nothing is saved.
    public static class TemporaryCandidateLayoutProbe
    {
        public const string MenuPath =
            "Tools/TxT RPG/Application/Temporary/Measure Candidate Main Scene Layout (Play Mode)";

        private const string ScenePath = "Assets/Scenes/Candidate_MainScene.unity";
        private static readonly (string Name, int Width, int Height)[] Sizes =
        {
            ("desktop-1920x1080", 1920, 1080), ("ultrawide-2560x1080", 2560, 1080), ("desktop-1280x720", 1280, 720),
            ("phone-portrait-1080x1920", 1080, 1920), ("phone-portrait-750x1334", 750, 1334),
            ("narrow-640x1136", 640, 1136), ("narrow-landscape-700x400", 700, 400)
        };

        private static EditorWindow view;
        private static PropertyInfo selectedIndex;
        private static object group;
        private static int previousIndex, customCountBefore, sizeIndex, phase;
        private static double next;
        private static StringBuilder report;

        [MenuItem(MenuPath, false, 3001)]
        public static void Measure()
        {
            if (!EditorApplication.isPlaying || group != null)
            {
                Debug.LogError("CANDIDATE_LAYOUT: FAIL enter Play Mode through AppScene and wait for any previous measurement.");
                return;
            }
            if (!SceneManager.GetSceneByPath(ScenePath).isLoaded)
            {
                Debug.LogError("CANDIDATE_LAYOUT: FAIL Candidate_MainScene is not loaded.");
                return;
            }
            var assembly = typeof(EditorWindow).Assembly;
            var viewType = assembly.GetType("UnityEditor.GameView");
            view = EditorWindow.GetWindow(viewType);
            selectedIndex = viewType.GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            previousIndex = (int)selectedIndex.GetValue(view);
            var sizesType = assembly.GetType("UnityEditor.GameViewSizes");
            var singleton = typeof(ScriptableSingleton<>).MakeGenericType(sizesType).GetProperty("instance").GetValue(null);
            group = sizesType.GetMethod("GetGroup").Invoke(singleton, new[] { Enum.Parse(assembly.GetType("UnityEditor.GameViewSizeGroupType"), "Standalone") });
            customCountBefore = CustomCount();
            report = new StringBuilder();
            sizeIndex = 0; phase = 0; next = EditorApplication.timeSinceStartup + 0.5;
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += PlayChanged;
        }

        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup < next) return;
            try
            {
                var entry = Sizes[sizeIndex];
                if (phase == 0)
                {
                    var assembly = typeof(EditorWindow).Assembly;
                    var size = Activator.CreateInstance(assembly.GetType("UnityEditor.GameViewSize"), new object[]
                        { Enum.Parse(assembly.GetType("UnityEditor.GameViewSizeType"), "FixedResolution"), entry.Width, entry.Height, "Candidate layout probe" });
                    group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { size });
                    selectedIndex.SetValue(view, BuiltinCount() + CustomCount() - 1);
                    view.Repaint();
                    phase = 1; next = EditorApplication.timeSinceStartup + 1.5;
                    return;
                }
                if (phase == 1)
                {
                    if (Screen.width != entry.Width || Screen.height != entry.Height)
                        throw new InvalidOperationException($"Expected {entry.Width}x{entry.Height}, got {Screen.width}x{Screen.height}.");
                    Canvas.ForceUpdateCanvases();
                    var line = MeasureScene(entry.Name);
                    report.AppendLine(line);
                    Debug.Log("CANDIDATE_LAYOUT: " + line);
                    ScreenCapture.CaptureScreenshot($"Temp/candidate-layout-{entry.Name}.png");
                    phase = 2; next = EditorApplication.timeSinceStartup + 0.5;
                    return;
                }
                RemoveProbeSize();
                sizeIndex++;
                if (sizeIndex < Sizes.Length) { phase = 0; next = EditorApplication.timeSinceStartup + 0.2; return; }
                var summary = report.ToString();
                Finish();
                Debug.Log("CANDIDATE_LAYOUT: PASS\n" + summary);
            }
            catch (Exception exception)
            {
                Finish();
                Debug.LogError("CANDIDATE_LAYOUT: FAIL " + exception);
            }
        }

        private static string MeasureScene(string name)
        {
            var scene = SceneManager.GetSceneByPath(ScenePath);
            var canvas = scene.GetRootGameObjects().Single(item => item.name == "Canvas").GetComponent<Canvas>();
            var main = (RectTransform)canvas.transform.Find("MainArea");
            var layout = main.Find("ContentLayer").GetComponent<FlexibleContentLayoutGroup>();
            var builder = new StringBuilder();
            builder.Append($"{name} screen={Screen.width}x{Screen.height} scale={canvas.scaleFactor:0.###} main={main.rect.width:0.#}x{main.rect.height:0.#} axis={layout.CurrentAxis}");
            foreach (var areaName in new[] { "LeftArea", "MiddleArea", "RightArea" })
            {
                var area = (RectTransform)main.Find("ContentLayer/" + areaName);
                var corners = new Vector3[4];
                area.GetWorldCorners(corners);
                var min = main.InverseTransformPoint(corners[0]);
                builder.Append($" | {areaName} x={min.x:0.#} y={min.y:0.#} w={area.rect.width:0.#} h={area.rect.height:0.#}");
            }
            var runeGrid = main.GetComponentInChildren<RuneGridPanel>(true);
            if (runeGrid != null && runeGrid.LayoutGroup != null)
            {
                var grid = runeGrid.LayoutGroup.CurrentLayout;
                var contentWidth = runeGrid.Content.rect.width;
                var viewportWidth = runeGrid.Viewport.rect.width;
                builder.Append($" | RuneGrid cols={grid.Columns} card={grid.CardSize:0.#} cell={grid.CellSize:0.#} used={grid.UsedWidth:0.#}/{runeGrid.LayoutGroup.AvailableWidth:0.#}" +
                    $" content={contentWidth:0.#}x{runeGrid.Content.rect.height:0.#} viewport={viewportWidth:0.#}x{runeGrid.Viewport.rect.height:0.#}" +
                    $" horizontalOverflow={(contentWidth > viewportWidth + .01f || grid.UsedWidth > runeGrid.LayoutGroup.AvailableWidth + .01f ? "YES" : "no")}");
            }
            return builder.ToString();
        }

        private static int BuiltinCount() => (int)group.GetType().GetMethod("GetBuiltinCount").Invoke(group, null);
        private static int CustomCount() => (int)group.GetType().GetMethod("GetCustomCount").Invoke(group, null);

        // RemoveCustomSize takes the total index (built-in sizes first); the probe size is always the last custom entry.
        private static void RemoveProbeSize()
        {
            selectedIndex.SetValue(view, previousIndex);
            if (CustomCount() > customCountBefore)
                group.GetType().GetMethod("RemoveCustomSize").Invoke(group, new object[] { BuiltinCount() + CustomCount() - 1 });
            phase = 0;
        }

        private static void PlayChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode) Finish();
        }

        private static void Finish()
        {
            EditorApplication.update -= Tick;
            EditorApplication.playModeStateChanged -= PlayChanged;
            if (group != null && CustomCount() > customCountBefore) RemoveProbeSize();
            if (view != null && selectedIndex != null) selectedIndex.SetValue(view, previousIndex);
            group = null;
        }
    }
}
