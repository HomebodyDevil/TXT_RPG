using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TxTRPG.UI;
using TxTRPG.UI.Exploration;
using TxTRPG.UI.Windows;
using TxTRPG.Application.Exploration;
namespace TxTRPG.Application.Editor
{
    // Temporary targeted integration; never regenerates the surrounding screen.
    public static class TemporaryExplorationTreeIntegration
    {
        public const string TargetPath = "Canvas/MainScreenViewport/Main_FlexibleLayoutPanel/ContentLayer/NodeTreePanel";
        [MenuItem("Tools/TxT RPG/UI/Exploration/Temporary/Snapshot Tree Target")]
        public static void Snapshot()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Use Edit Mode.");
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if(scene.path!="Assets/Scenes/TMP_MainScene.unity")throw new InvalidOperationException("Keep the intended TMP_MainScene open.");
            var target=FindMain(scene).Find("ContentLayer/NodeTreePanel");
            if(target==null)throw new InvalidOperationException("The user-authored target is missing; no panel was created.");
            var folder=Path.Combine(Path.GetTempPath(),"TxTRPG-TreePanel");Directory.CreateDirectory(folder);
            var path=Path.Combine(folder,"BeforeIntegration.unity");
            if(!EditorSceneManager.SaveScene(scene,path,true))throw new InvalidOperationException("Snapshot failed.");
            File.WriteAllText(Path.Combine(folder,"Target.json"),Newtonsoft.Json.JsonConvert.SerializeObject(new {path=TargetPath,sibling=target.GetSiblingIndex(),components=target.GetComponents<Component>().Select(c=>new{type=c.GetType().FullName,serialized=EditorJsonUtility.ToJson(c,true)}).ToArray()},Newtonsoft.Json.Formatting.Indented));
            Debug.Log("TREE_TARGET_SNAPSHOT: "+folder);
        }

        [MenuItem("Tools/TxT RPG/UI/Exploration/Temporary/Snapshot Applied Tree")]
        public static void SnapshotApplied()
        {
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var path=Path.Combine(Path.GetTempPath(),"TxTRPG-TreePanel/AfterIntegration.unity");
            if(!EditorSceneManager.SaveScene(scene,path,true))throw new InvalidOperationException("Snapshot failed.");
            Debug.Log("TREE_APPLIED_SNAPSHOT: "+path);
        }
        [MenuItem("Tools/TxT RPG/UI/Exploration/Temporary/Commit Reviewed Tree")]
        public static void CommitReviewed()
        {
            if(EditorApplication.isPlaying||EditorApplication.isCompiling)throw new InvalidOperationException("Use Edit Mode after compilation.");
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if(scene.path!="Assets/Scenes/TMP_MainScene.unity")throw new InvalidOperationException("Unexpected active Scene.");
            var folder=Path.Combine(Path.GetTempPath(),"TxTRPG-TreePanel");
            var latest=Path.Combine(folder,"ReviewedCurrent.unity");
            if(!EditorSceneManager.SaveScene(scene,latest,true))throw new InvalidOperationException("Snapshot failed.");
            if(File.ReadAllText(latest)!=File.ReadAllText(Path.Combine(folder,"AfterIntegration.unity")))throw new InvalidOperationException("Scene changed after review; inspect a fresh snapshot before saving.");
            if(!EditorSceneManager.SaveScene(scene))throw new InvalidOperationException("Save failed.");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            Debug.Log("TREE_COMMIT_REVIEWED: saved reviewed changes and closed the Scene for serialization verification.");
        }
        [MenuItem("Tools/TxT RPG/UI/Exploration/Temporary/Verify Tree Runtime")]
        public static void VerifyRuntime()=>TemporaryActionsScrollVerification.Start("TxTRPG.UI.Tests.ExplorationTreePlayModeTests");
        public const string NodePath="Assets/TxTRPG/UI/Prefabs/ExplorationTreeNode.prefab";
        public const string EdgePath="Assets/TxTRPG/UI/Prefabs/ExplorationTreeEdge.prefab";
        public const string StylePath="Assets/TxTRPG/UI/Styles/ExplorationTreeDefaultStyle.asset";
        // Task: temporary.exploration-tree-integration; category: one-time Scene integration;
        // inputs: TargetPath in TMP_MainScene, existing TemporaryMenu icons and TMP default font.
        // outputs: NodePath, EdgePath, StylePath and selected Scene references. Default selection: false.
        // Excluded from production Prefab batches: preserves developer-owned assets and mutates a selected Scene.
        [MenuItem("Tools/TxT RPG/UI/Exploration/Temporary/Apply Tree Panel")]
        public static void Apply()
        {
            if(EditorApplication.isPlaying||EditorApplication.isCompiling)throw new InvalidOperationException("Use Edit Mode after compilation.");
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if(scene.path!="Assets/Scenes/TMP_MainScene.unity")throw new InvalidOperationException("Keep the intended TMP_MainScene open.");
            var target=FindMain(scene).Find("ContentLayer/NodeTreePanel");
            if(target==null)throw new InvalidOperationException("The user-authored target is missing; no panel was created.");
            var flexible=target.GetComponent<FlexibleLayoutPanel>();
            if(flexible==null||flexible.ContentRoot==target)throw new InvalidOperationException("Target content-layer references are incomplete.");
            if(target.GetComponent<ExplorationNodeTreePanel>()!=null){Validate();Debug.Log("TREE_APPLY: Already applied; authored values preserved.");return;}
            var content=flexible.ContentRoot;
            if(content.childCount!=0)throw new InvalidOperationException("Target ContentLayer is not empty; preserve existing children.");
            var components=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Component>(true)).ToArray();
            var service=components.OfType<GameWindowService>().Single();var controller=components.OfType<ExplorationRunController>().Single();
            EnsureAssets();
            var panel=Undo.AddComponent<ExplorationNodeTreePanel>(target.gameObject);
            var body=Rect("TreeBody",content);body.gameObject.AddComponent<FlexibleLayoutItem>();
            var header=Rect("Header",body);header.anchorMin=new Vector2(0,1);header.anchorMax=Vector2.one;header.pivot=new Vector2(.5f,1);header.sizeDelta=new Vector2(0,48);
            var browse=Button("Browse",header,"기록 탐색");var br=(RectTransform)browse.transform;br.anchorMin=Vector2.zero;br.anchorMax=new Vector2(.68f,1);br.offsetMin=Vector2.zero;br.offsetMax=new Vector2(-4,0);
            var latest=Button("Latest",header,"최신");var lr=(RectTransform)latest.transform;lr.anchorMin=new Vector2(.68f,0);lr.anchorMax=Vector2.one;lr.offsetMin=new Vector2(4,0);lr.offsetMax=Vector2.zero;
            var scrollRoot=Rect("TreeScroll",body);Stretch(scrollRoot,0,0,0,56);
            var scroll=scrollRoot.gameObject.AddComponent<ScrollRect>();scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=35;
            var viewport=Rect("Viewport",scrollRoot);Stretch(viewport);viewport.gameObject.AddComponent<RectMask2D>();var hit=viewport.gameObject.AddComponent<Image>();hit.color=new Color(.055f,.07f,.10f,.94f);
            var treeContent=Rect("Content",viewport);treeContent.anchorMin=treeContent.anchorMax=new Vector2(0,1);treeContent.pivot=new Vector2(0,1);treeContent.anchoredPosition=Vector2.zero;
            var edges=Rect("EdgesLayer",treeContent);Stretch(edges);edges.pivot=new Vector2(0,1);
            var nodes=Rect("NodesLayer",treeContent);Stretch(nodes);nodes.pivot=new Vector2(0,1);
            scroll.viewport=viewport;scroll.content=treeContent;scroll.horizontal=true;scroll.vertical=true;
            var navigator=viewport.gameObject.AddComponent<ExplorationTreeNavigation>();navigator.targetGraphic=hit;navigator.Configure(panel,browse,service);
            navigator.navigation=new Navigation{mode=Navigation.Mode.None};
            var noticeRect=Rect("Notice",body);noticeRect.anchorMin=new Vector2(0,0);noticeRect.anchorMax=new Vector2(1,0);noticeRect.pivot=new Vector2(.5f,0);noticeRect.sizeDelta=new Vector2(0,60);
            var notice=Text(noticeRect,string.Empty,14);notice.gameObject.SetActive(false);
            panel.Configure(scroll,nodes,edges,AssetDatabase.LoadAssetAtPath<ExplorationTreeNodeView>(NodePath),AssetDatabase.LoadAssetAtPath<ExplorationTreeEdgeView>(EdgePath),AssetDatabase.LoadAssetAtPath<ExplorationTreeStyle>(StylePath),browse,latest,navigator,notice,service);
            Undo.RecordObject(controller,"Connect exploration history");controller.ConfigureTreeForEditor(panel);EditorUtility.SetDirty(controller);
            foreach(var component in target.GetComponentsInChildren<Component>(true))
            {
                if(component==null)continue;EditorUtility.SetDirty(component);
                if(PrefabUtility.IsPartOfPrefabInstance(component))PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            }
            if(PrefabUtility.IsPartOfPrefabInstance(controller))PrefabUtility.RecordPrefabInstancePropertyModifications(controller);
            EditorSceneManager.MarkSceneDirty(scene);
            Validate();
            Debug.Log("TREE_APPLY: Targeted integration complete; Scene remains unsaved for diff review.");
        }
        [MenuItem("Tools/TxT RPG/UI/Exploration/Temporary/Constrain Choices To Center")]
        public static void ConstrainChoices()
        {
            if(EditorApplication.isPlaying||EditorApplication.isCompiling)throw new InvalidOperationException("Use Edit Mode after compilation.");
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if(scene.path!="Assets/Scenes/TMP_MainScene.unity")throw new InvalidOperationException("Open TMP_MainScene.");
            var controller=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<ExplorationRunController>(true)).Single();
            var overlay=(GameObject)new SerializedObject(controller).FindProperty("panel").objectReferenceValue;
            var center=FindMain(scene).Find("ContentLayer/Text_FlexibleLayoutPanel").GetComponent<FlexibleLayoutPanel>();
            Undo.SetTransformParent(overlay.transform,center.ContentRoot,"Constrain choices to center");
            var ignored=overlay.GetComponent<LayoutElement>()??Undo.AddComponent<LayoutElement>(overlay);Undo.RecordObject(ignored,"Exclude overlay from allocation");ignored.ignoreLayout=true;
            var rect=(RectTransform)overlay.transform;Undo.RecordObject(rect,"Fit choices to center");Stretch(rect);rect.localScale=Vector3.one;rect.SetAsLastSibling();
            EditorUtility.SetDirty(rect);EditorUtility.SetDirty(ignored);EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("TREE_CHOICES: Center-only overlay applied; Scene remains unsaved for review.");
        }
        private static Transform FindMain(UnityEngine.SceneManagement.Scene scene)
        {
            var canvas=scene.GetRootGameObjects().Single(r=>r.name=="Canvas").transform;
            return canvas.Find("Main_FlexibleLayoutPanel")??canvas.Find("MainScreenViewport/Main_FlexibleLayoutPanel")??throw new InvalidOperationException("The authored main panel is missing.");
        }
        [MenuItem("Tools/TxT RPG/UI/Exploration/Temporary/Apply Narrow Screen Scroll")]
        public static void ApplyNarrowScreen()
        {
            if(EditorApplication.isPlaying||EditorApplication.isCompiling)throw new InvalidOperationException("Use Edit Mode after compilation.");
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();if(scene.path!="Assets/Scenes/TMP_MainScene.unity")throw new InvalidOperationException("Open TMP_MainScene.");
            var main=FindMain(scene).GetComponent<FlexibleLayoutPanel>();
            if(main.GetComponentInParent<ResponsiveMainScreen>() is ResponsiveMainScreen existing)
            {
                var menu=main.GetComponentInChildren<GameMenuPanel>(true);var config=new SerializedObject(existing);config.FindProperty("menu").objectReferenceValue=menu;config.FindProperty("menuAllocation").objectReferenceValue=menu.GetComponent<FlexibleLayoutItem>();config.ApplyModifiedProperties();EditorSceneManager.MarkSceneDirty(scene);Debug.Log("TREE_NARROW: Existing wrapper references completed.");return;
            }
            var rect=(RectTransform)main.transform;var parent=rect.parent;var sibling=rect.GetSiblingIndex();
            var oldMin=rect.anchorMin;var oldMax=rect.anchorMax;var oldPivot=rect.pivot;var oldPosition=rect.anchoredPosition;var oldSize=rect.sizeDelta;
            var viewport=Rect("MainScreenViewport",parent);Undo.RegisterCreatedObjectUndo(viewport.gameObject,"Add responsive page viewport");Stretch(viewport);viewport.SetSiblingIndex(sibling);viewport.gameObject.AddComponent<RectMask2D>();viewport.gameObject.AddComponent<Image>().color=Color.clear;
            var scroll=viewport.gameObject.AddComponent<ScrollRect>();scroll.viewport=viewport;scroll.content=rect;scroll.horizontal=scroll.vertical=false;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=40;
            Undo.SetTransformParent(rect,viewport,"Wrap existing main panel");rect.anchorMin=oldMin;rect.anchorMax=oldMax;rect.pivot=oldPivot;rect.anchoredPosition=oldPosition;rect.sizeDelta=oldSize;
            var items=new[]{"NodeTreePanel","Text_FlexibleLayoutPanel","Character_FlexibleLayoutPanel"}.Select(name=>main.ContentRoot.Find(name).GetComponent<FlexibleLayoutItem>()).ToArray();
            var cards=main.GetComponentInChildren<ExplorationNodeChoiceCardList>(true);var cardScroll=(ScrollRect)new SerializedObject(cards).FindProperty("scrollRect").objectReferenceValue;
            var targetMenu=main.GetComponentInChildren<GameMenuPanel>(true);
            viewport.gameObject.AddComponent<ResponsiveMainScreen>().Configure(scroll,main,items,cards,cardScroll,targetMenu,targetMenu.GetComponent<FlexibleLayoutItem>());
            foreach(var inner in main.GetComponentsInChildren<ScrollRect>(true))
            {
                var bridge=Undo.AddComponent<NestedScrollRectBridge>(inner.gameObject);bridge.Configure(inner,scroll);EditorUtility.SetDirty(bridge);
                if(PrefabUtility.IsPartOfPrefabInstance(bridge))PrefabUtility.RecordPrefabInstancePropertyModifications(bridge);
            }
            EditorUtility.SetDirty(rect);if(PrefabUtility.IsPartOfPrefabInstance(rect))PrefabUtility.RecordPrefabInstancePropertyModifications(rect);
            EditorSceneManager.MarkSceneDirty(scene);Debug.Log("TREE_NARROW: Responsive wrapper and explicit scroll bridges applied; review before saving.");
        }
        private static void EnsureAssets()
        {
            if(AssetDatabase.LoadAssetAtPath<ExplorationTreeStyle>(StylePath)==null)
            {
                var style=ScriptableObject.CreateInstance<ExplorationTreeStyle>();
                style.fallbackIcon=TemporaryMenuIconUtility.GetDefault("status");
                style.types=new[]{new ExplorationTreeStyle.TypeImage{typeId="combat",displayName="전투",sprite=TemporaryMenuIconUtility.GetDefault("action")},new ExplorationTreeStyle.TypeImage{typeId="recovery-upgrade",displayName="회복·강화",sprite=TemporaryMenuIconUtility.GetDefault("system")}};
                AssetDatabase.CreateAsset(style,StylePath);
            }
            if(AssetDatabase.LoadAssetAtPath<GameObject>(NodePath)==null)
            {
                var root=Rect("ExplorationTreeNode",null);root.anchorMin=root.anchorMax=new Vector2(0,1);root.sizeDelta=new Vector2(92,124);
                try
                {
                    var visual=Rect("VisualRoot",root);Stretch(visual);var alpha=visual.gameObject.AddComponent<CanvasGroup>();alpha.blocksRaycasts=false;
                    var background=visual.gameObject.AddComponent<Image>();background.color=new Color(.18f,.25f,.32f,1);background.raycastTarget=false;
                    var imageRect=Rect("Icon",visual);imageRect.anchorMin=new Vector2(.2f,.54f);imageRect.anchorMax=new Vector2(.8f,.96f);imageRect.offsetMin=imageRect.offsetMax=Vector2.zero;
                    var icon=imageRect.gameObject.AddComponent<Image>();icon.preserveAspect=true;icon.sprite=TemporaryMenuIconUtility.GetDefault("status");icon.raycastTarget=false;
                    var titleRect=Rect("Title",visual);Stretch(titleRect,3,0,3,0);var title=Text(titleRect,"노드",16);
                    var statusRect=Rect("Status",visual);statusRect.anchorMin=new Vector2(0,.01f);statusRect.anchorMax=new Vector2(1,.22f);statusRect.offsetMin=new Vector2(2,0);statusRect.offsetMax=new Vector2(-2,0);var status=Text(statusRect,"현재 후보",14);status.fontStyle=FontStyles.Bold;
                    var effectRect=Rect("EffectOverlay",visual);Stretch(effectRect);var effect=effectRect.gameObject.AddComponent<Image>();effect.color=Color.clear;effect.raycastTarget=false;
                    var view=root.gameObject.AddComponent<ExplorationTreeNodeView>();view.Configure(visual,alpha,icon,background,effect,title,status);
                    PrefabUtility.SaveAsPrefabAsset(root.gameObject,NodePath);
                }
                finally{UnityEngine.Object.DestroyImmediate(root.gameObject);}
            }
            if(AssetDatabase.LoadAssetAtPath<GameObject>(EdgePath)==null)
            {
                var root=Rect("ExplorationTreeEdge",null);root.anchorMin=root.anchorMax=new Vector2(0,1);
                try
                {
                    var visual=Rect("VisualRoot",root);Stretch(visual);var line=visual.gameObject.AddComponent<Image>();line.raycastTarget=false;
                    var overlay=Rect("EffectOverlay",visual);Stretch(overlay);var effect=overlay.gameObject.AddComponent<Image>();effect.color=Color.clear;effect.raycastTarget=false;
                    root.gameObject.AddComponent<ExplorationTreeEdgeView>().Configure(visual,line,effect);PrefabUtility.SaveAsPrefabAsset(root.gameObject,EdgePath);
                }
                finally{UnityEngine.Object.DestroyImmediate(root.gameObject);}
            }
            AssetDatabase.SaveAssetIfDirty(AssetDatabase.LoadAssetAtPath<ExplorationTreeStyle>(StylePath));
        }
        [MenuItem("Tools/TxT RPG/UI/Exploration/Temporary/Validate Tree Panel")]
        public static void Validate()
        {
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var panel=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<ExplorationNodeTreePanel>(true)).Single();
            if(panel.Scroll==null||panel.Style==null||panel.BrowseButton==null||panel.Navigation==null||panel.Style.fallbackIcon==null)throw new InvalidOperationException("Tree references incomplete.");
            foreach(var path in new[]{NodePath,EdgePath})
            {
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(prefab==null||prefab.transform.Find("VisualRoot/EffectOverlay")==null)throw new InvalidOperationException("Invalid tree prefab: "+path);
                if(prefab.GetComponentsInChildren<Graphic>(true).Any(g=>g.raycastTarget))throw new InvalidOperationException("Decorations must not block input.");
            }
            Debug.Log("TREE_VALIDATE: saved assets and Scene references are complete.");
        }
        private static RectTransform Rect(string name,Transform parent){var go=new GameObject(name,typeof(RectTransform));if(parent!=null)go.transform.SetParent(parent,false);return (RectTransform)go.transform;}
        private static void Stretch(RectTransform rect,float left=0,float bottom=0,float right=0,float top=0){rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=new Vector2(left,bottom);rect.offsetMax=new Vector2(-right,-top);}
        private static TMP_Text Text(RectTransform rect,string value,float size){var text=rect.gameObject.AddComponent<TextMeshProUGUI>();text.font=TMP_Settings.defaultFontAsset;text.text=value;text.fontSize=size;text.alignment=TextAlignmentOptions.Center;text.textWrappingMode=TextWrappingModes.Normal;text.overflowMode=TextOverflowModes.Ellipsis;text.raycastTarget=false;return text;}
        private static Button Button(string name,Transform parent,string label){var rect=Rect(name,parent);var image=rect.gameObject.AddComponent<Image>();image.color=new Color(.15f,.23f,.32f,1);var button=rect.gameObject.AddComponent<Button>();button.targetGraphic=image;var text=Rect("Label",rect);Stretch(text,4,2,4,2);Text(text,label,16);return button;}
    }
}
