using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TxTRPG.Application.Items;
using TxTRPG.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using TxTRPG.UI.Exploration;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TxTRPG.Application.Editor
{
    // Temporary visual verification only. Restores the Game View selection and removes its custom sizes.
    public static class TemporaryActionsGridVerification
    {
        private static readonly (string name, int width, int height)[] sizes = {
            ("desktop",1920,1080), ("phone-portrait",390,844), ("phone-landscape",844,390),
            ("tablet",1024,768), ("ultrawide",2560,1080), ("phone-safe-area",390,844) };
        private static object group;
        private static EditorWindow view;
        private static PropertyInfo selectedIndex;
        private static int previousIndex, customIndex, sizeIndex, phase;
        private static double next;
        private static string prefix;
        private static readonly List<Measurement> measurements = new();
        [Serializable] private sealed class Report { public List<Measurement> screens; }
        [Serializable] private sealed class Measurement
        {
            public string screen, path; public int width, height, slots, columns;
            public float canvasScale, match, reservedInset, requiredHeight, headerReservation, horizontalPosition;
            public bool singleRow, headerVisible; public int centerThreshold; public string fittingAlignment;
            public Vector2 referenceResolution, viewport, parentSize, cell, spacing, picture;
        }
        [MenuItem("Tools/TxT RPG/UI/Actions/Temporary/Apply Balanced To Main Scene")]
        public static void Apply()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (EditorApplication.isPlaying || EditorApplication.isCompiling || scene.path != "Assets/Scenes/TMP_MainScene.unity" || scene.isDirty)
                throw new InvalidOperationException("Open clean TMP_MainScene in Edit Mode after compilation. Save your edits first.");
            var panel = Object.FindObjectsByType<QuickItemGridPresenter>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single().GetComponent<ActionGridPanel>();
            var character = panel.GetComponentInParent<FlexibleLayoutPanel>();
            var actionsItem = panel.GetComponent<FlexibleLayoutItem>();
            var characterItem = character.GetComponent<FlexibleLayoutItem>();
            Undo.RecordObjects(new Object[]{panel,actionsItem,characterItem}, "Apply Actions presentation");
            panel.SetDisplayMode(ActionGridDisplayMode.Balanced);
            actionsItem.Configure(actionsItem.SizeMode, actionsItem.Weight, actionsItem.FixedSize, 184, actionsItem.MaximumSize);
            characterItem.Configure(characterItem.SizeMode, characterItem.Weight, characterItem.FixedSize, 190, characterItem.MaximumSize);
            foreach(var item in new Object[]{panel,actionsItem,characterItem}) { EditorUtility.SetDirty(item); PrefabUtility.RecordPrefabInstancePropertyModifications(item); }
            EditorSceneManager.MarkSceneDirty(scene);
            if(!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Scene save failed.");
            Debug.Log("ACTIONS_APPLY: PASS Balanced; Actions minimum height 184; Character minimum width 190. Only TMP_MainScene saved.");
        }
        private static CanvasGroup choiceOverlay;
        private static bool addedOverlayGroup;
        private static float previousAlpha;
        private static ActionGridPanel measuredPanel;
        private static ActionGridDisplayMode savedMode;
        private static FlexibleLayoutItem actionsAllocation, characterAllocation;
        private static float savedActionsMinimum, savedCharacterMinimum;
        private static ActionGridEntry[] savedEntries;
        private static int savedCapacity, savedSelection;
        private static Sprite verificationSprite;
        private static RectTransform mainRect;
        private static Vector2 savedOffsetMin, savedOffsetMax;
        [MenuItem("Tools/TxT RPG/UI/Actions/Temporary/Inspect Allocation")]
        public static void InspectAllocation()
        {
            var panel = Object.FindObjectsByType<QuickItemGridPresenter>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single().GetComponent<ActionGridPanel>();
            for (var t = panel.transform; t != null; t = t.parent)
            {
                Debug.Log("ACTIONS_PARENT: " + GetPath(t) + " rect=" + ((RectTransform)t).rect.size);
                if (t.TryGetComponent<FlexibleLayoutItem>(out var item)) Debug.Log("ACTIONS_ITEM: " + JsonUtility.ToJson(item));
                if (t.TryGetComponent<FlexibleLayoutPanel>(out var layout)) Debug.Log("ACTIONS_FLEX: " + JsonUtility.ToJson(layout));
            }
        }
        [MenuItem("Tools/TxT RPG/UI/Actions/Temporary/Capture Before")]
        public static void Before() => Start("before");
        [MenuItem("Tools/TxT RPG/UI/Actions/Temporary/Capture After")]
        public static void After() => Start("after");
        [MenuItem("Tools/TxT RPG/UI/Actions/Temporary/Capture Cell Details")]
        public static void Details() => Start("details");
        [MenuItem("Tools/TxT RPG/UI/Actions/Temporary/Capture Single Row Before")]
        public static void SingleRowBefore() => Start("single-row-before");
        [MenuItem("Tools/TxT RPG/UI/Actions/Temporary/Capture Single Row After")]
        public static void SingleRowAfter() => Start("single-row-after");
        private static void Start(string label)
        {
            if (!EditorApplication.isPlaying || group != null) throw new InvalidOperationException("Enter AppScene Play Mode and wait for the previous capture.");
            var controller = Object.FindObjectsByType<TxTRPG.Application.Exploration.ExplorationRunController>(FindObjectsSortMode.None).Single();
            var overlayRoot = (GameObject)new SerializedObject(controller).FindProperty("panel").objectReferenceValue;
            choiceOverlay = overlayRoot.GetComponent<CanvasGroup>(); addedOverlayGroup = choiceOverlay == null;
            if(addedOverlayGroup) choiceOverlay = overlayRoot.AddComponent<CanvasGroup>();
            previousAlpha = choiceOverlay.alpha; choiceOverlay.alpha = 0;
            measuredPanel = Object.FindObjectsByType<QuickItemGridPresenter>(FindObjectsSortMode.None).Single().GetComponent<ActionGridPanel>();
            savedMode = measuredPanel.DisplayMode;
            if(label=="details")
            {
                var presenter=measuredPanel.GetComponent<QuickItemGridPresenter>();
                savedEntries=presenter.GetEntries().ToArray(); savedCapacity=measuredPanel.Capacity; savedSelection=measuredPanel.SelectedIndex;
                verificationSprite=Sprite.Create(Texture2D.whiteTexture,new Rect(0,0,Texture2D.whiteTexture.width,Texture2D.whiteTexture.height),Vector2.one*.5f);
                measuredPanel.SetEntries(Enumerable.Range(0,savedCapacity).Select(i=>new ActionGridEntry("visual-check-"+i,ActionGridEntryKind.Item,verificationSprite,"Verification",quantity:99,cooldownNormalized:.4f,shortcutLabel:(i+1).ToString())).ToArray(),savedCapacity);
                measuredPanel.Select(0,false);
            }
            actionsAllocation = measuredPanel.GetComponent<FlexibleLayoutItem>();
            characterAllocation = measuredPanel.GetComponentInParent<FlexibleLayoutPanel>().GetComponent<FlexibleLayoutItem>();
            savedActionsMinimum = actionsAllocation.MinimumSize; savedCharacterMinimum = characterAllocation.MinimumSize;
            mainRect = (RectTransform)characterAllocation.transform.parent.GetComponentInParent<FlexibleLayoutPanel>().transform;
            savedOffsetMin=mainRect.offsetMin; savedOffsetMax=mainRect.offsetMax;
            if(label == "before")
            {
                measuredPanel.SetDisplayMode(ActionGridDisplayMode.Manual);
                SetMinimum(actionsAllocation,0); SetMinimum(characterAllocation,0);
            }
            var assembly = typeof(EditorWindow).Assembly;
            var viewType = assembly.GetType("UnityEditor.GameView");
            view = EditorWindow.GetWindow(viewType); selectedIndex = viewType.GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            previousIndex = (int)selectedIndex.GetValue(view);
            var sizesType = assembly.GetType("UnityEditor.GameViewSizes");
            var singleton = typeof(ScriptableSingleton<>).MakeGenericType(sizesType).GetProperty("instance").GetValue(null);
            var groupEnum = assembly.GetType("UnityEditor.GameViewSizeGroupType");
            group = sizesType.GetMethod("GetGroup").Invoke(singleton, new[] { Enum.Parse(groupEnum, "Standalone") });
            prefix = label; sizeIndex = 0; phase = 0; next = EditorApplication.timeSinceStartup + 1; measurements.Clear();
            EditorApplication.update += Tick; EditorApplication.playModeStateChanged += PlayChanged;
        }
        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup < next) return;
            try
            {
                if (phase == 0)
                {
                    var entry = sizes[sizeIndex]; var assembly = typeof(EditorWindow).Assembly;
                    if(entry.name=="phone-safe-area") { mainRect.offsetMin=new Vector2(24,34); mainRect.offsetMax=new Vector2(-24,-44); }
                    var type = assembly.GetType("UnityEditor.GameViewSize"); var kind = assembly.GetType("UnityEditor.GameViewSizeType");
                    var size = Activator.CreateInstance(type, new object[] { Enum.Parse(kind,"FixedResolution"), entry.width, entry.height, "Actions verification" });
                    customIndex = (int)group.GetType().GetMethod("GetCustomCount").Invoke(group,null);
                    var index = (int)group.GetType().GetMethod("GetBuiltinCount").Invoke(group,null) + customIndex;
                    group.GetType().GetMethod("AddCustomSize").Invoke(group,new[] { size }); selectedIndex.SetValue(view,index); view.Repaint();
                    phase = 1; next = EditorApplication.timeSinceStartup + 1.5; return;
                }
                if (phase == 1)
                {
                    var entry = sizes[sizeIndex];
                    if (Screen.width != entry.width || Screen.height != entry.height) throw new InvalidOperationException($"Expected {entry.width}x{entry.height}, got {Screen.width}x{Screen.height}.");
                    var presenter = Object.FindObjectsByType<QuickItemGridPresenter>(FindObjectsSortMode.None).Single();
                    var panel = presenter.GetComponent<ActionGridPanel>(); var grid = panel.ScrollRect.content.GetComponent<GridLayoutGroup>();
                    Canvas.ForceUpdateCanvases(); LayoutRebuilder.ForceRebuildLayoutImmediate(panel.ScrollRect.content);
                    var cell = panel.GetComponentsInChildren<ActionGridCell>().FirstOrDefault();
                    var canvas = panel.GetComponentInParent<Canvas>(); var scaler = canvas.GetComponent<CanvasScaler>();
                    var controller = panel.ScrollRect.GetComponent<ConfigurableScrollbarController>();
                    var m = new Measurement { screen=entry.name, width=Screen.width,height=Screen.height,path=GetPath(panel.transform),
                        slots=panel.VisibleCellCount, columns=panel.CurrentColumns,canvasScale=canvas.scaleFactor,referenceResolution=scaler.referenceResolution,match=scaler.matchWidthOrHeight,
                        viewport=panel.ScrollRect.viewport.rect.size,parentSize=((RectTransform)panel.transform).rect.size,cell=grid.cellSize,spacing=grid.spacing,
                        picture=cell!=null?((RectTransform)cell.transform.Find("ContentRoot/EmptySlot")).rect.size:Vector2.zero,
                        singleRow=panel.IsSingleRow, headerVisible=panel.SurfaceLayout!=null?panel.SurfaceLayout.HeaderVisible:panel.transform.Find("Header").gameObject.activeSelf,
                        headerReservation=panel.SurfaceLayout!=null?panel.SurfaceLayout.HeaderReservation:0, centerThreshold=panel.GetDisplaySettings().singleRow.centerThreshold, fittingAlignment=panel.GetDisplaySettings().singleRow.alignment.ToString(), horizontalPosition=panel.ScrollRect.horizontalNormalizedPosition,
                        reservedInset=panel.IsSingleRow?Mathf.Max(panel.ScrollRect.viewport.offsetMin.x,-panel.ScrollRect.viewport.offsetMax.x):(controller!=null?controller.ReservedInset:0),requiredHeight=ActionGridPanel.CalculateRequiredGridHeight(panel.VisibleCellCount,panel.CurrentColumns,grid.cellSize.y,grid.spacing.y,grid.padding) };
                    measurements.Add(m); Debug.Log("ACTIONS_MEASURE: "+JsonUtility.ToJson(m));
                    Directory.CreateDirectory("Assets/Screenshots"); ScreenCapture.CaptureScreenshot($"Assets/Screenshots/actions-{prefix}-{entry.name}.png");
                    phase=2; next=EditorApplication.timeSinceStartup+.5; return;
                }
                RemoveSize(); sizeIndex++;
                if(sizeIndex<sizes.Length) { phase=0; next=EditorApplication.timeSinceStartup+.2; return; }
                Directory.CreateDirectory("DOCS/development/verification");
                File.WriteAllText($"DOCS/development/verification/actions-grid-{prefix}.json",JsonUtility.ToJson(new Report { screens=new List<Measurement>(measurements) },true));
                Finish(); Debug.Log("ACTIONS_CAPTURE: PASS "+prefix);
            }
            catch(Exception e) { Finish(); Debug.LogError("ACTIONS_CAPTURE: FAIL "+e); }
        }
        private static void SetMinimum(FlexibleLayoutItem item, float minimum) => item.Configure(item.SizeMode,item.Weight,item.FixedSize,minimum,item.MaximumSize);
        private static string GetPath(Transform t) => t.parent==null?t.name:GetPath(t.parent)+"/"+t.name;
        private static void RemoveSize() { if(mainRect!=null) { mainRect.offsetMin=savedOffsetMin; mainRect.offsetMax=savedOffsetMax; } selectedIndex.SetValue(view,previousIndex); group.GetType().GetMethod("RemoveCustomSize").Invoke(group,new object[]{customIndex}); phase=0; }
        private static void PlayChanged(PlayModeStateChange state) { if(state==PlayModeStateChange.ExitingPlayMode) Finish(); }
        private static void Finish()
        {
            if(choiceOverlay!=null) { choiceOverlay.alpha=previousAlpha; if(addedOverlayGroup) Object.DestroyImmediate(choiceOverlay); } choiceOverlay=null;
            if(measuredPanel!=null && savedEntries!=null) { measuredPanel.SetEntries(savedEntries,savedCapacity); if(savedSelection>=0) measuredPanel.Select(savedSelection,false); savedEntries=null; }
            if(verificationSprite!=null) { Object.DestroyImmediate(verificationSprite); verificationSprite=null; }
            if(measuredPanel!=null) { measuredPanel.SetDisplayMode(savedMode); SetMinimum(actionsAllocation,savedActionsMinimum); SetMinimum(characterAllocation,savedCharacterMinimum); measuredPanel=null; }
            EditorApplication.update-=Tick; EditorApplication.playModeStateChanged-=PlayChanged;
            if(group!=null && phase!=0) RemoveSize(); group=null;
        }
    }
}
