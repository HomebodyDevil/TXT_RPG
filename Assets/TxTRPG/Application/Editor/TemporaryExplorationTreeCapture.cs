using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using TxTRPG.UI.Exploration;
using TxTRPG.UI;
namespace TxTRPG.Application.Editor
{
    // Temporary runtime observation only; restores resolution and safe-area simulation.
    public static class TemporaryExplorationTreeCapture
    {
        private static readonly (string name,int width,int height)[] Sizes={ ("desktop",1920,1080),("phone-portrait",390,844),("phone-landscape",844,390),("tablet",1024,768),("ultrawide",2560,1080),("phone-safe-area",390,844)};
        private static object group; private static EditorWindow view; private static PropertyInfo selected;
        private static int previous,index,custom=-1,phase; private static double next;
        private static RectTransform main; private static Vector2 min,max;
        private static readonly List<string> report=new();
        [MenuItem("Tools/TxT RPG/UI/Exploration/Temporary/Capture Tree Screens")]
        public static void Start()
        {
            if(!EditorApplication.isPlaying||group!=null)throw new InvalidOperationException("Enter Play Mode and wait for the previous capture.");
            var responsive=UnityEngine.Object.FindObjectsByType<ResponsiveMainScreen>(FindObjectsSortMode.None).SingleOrDefault();main=responsive!=null?responsive.PageScroll.viewport:(RectTransform)GameObject.Find("Canvas/Main_FlexibleLayoutPanel").transform;min=main.offsetMin;max=main.offsetMax;
            var assembly=typeof(EditorWindow).Assembly;var type=assembly.GetType("UnityEditor.GameView");
            view=EditorWindow.GetWindow(type);selected=type.GetProperty("selectedSizeIndex",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);previous=(int)selected.GetValue(view);
            var sizes=assembly.GetType("UnityEditor.GameViewSizes");var singleton=typeof(ScriptableSingleton<>).MakeGenericType(sizes).GetProperty("instance").GetValue(null);
            group=sizes.GetMethod("GetGroup").Invoke(singleton,new[]{Enum.Parse(assembly.GetType("UnityEditor.GameViewSizeGroupType"),"Standalone")});
            index=phase=0;report.Clear();next=EditorApplication.timeSinceStartup+1;EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=Changed;
        }
        private static void Tick()
        {
            if(EditorApplication.timeSinceStartup<next)return;
            try
            {
                var size=Sizes[index];
                if(phase==0)
                {
                    if(size.name=="phone-safe-area"){main.offsetMin=new Vector2(24,34);main.offsetMax=new Vector2(-24,-44);}
                    var assembly=typeof(EditorWindow).Assembly;
                    var value=Activator.CreateInstance(assembly.GetType("UnityEditor.GameViewSize"),new object[]{Enum.Parse(assembly.GetType("UnityEditor.GameViewSizeType"),"FixedResolution"),size.width,size.height,"Tree verification"});
                    custom=(int)group.GetType().GetMethod("GetCustomCount").Invoke(group,null);
                    var selection=custom+(int)group.GetType().GetMethod("GetBuiltinCount").Invoke(group,null);
                    group.GetType().GetMethod("AddCustomSize").Invoke(group,new[]{value});selected.SetValue(view,selection);view.Repaint();phase=1;next=EditorApplication.timeSinceStartup+1.5;return;
                }
                if(phase==1)
                {
                    if(Screen.width!=size.width||Screen.height!=size.height)throw new InvalidOperationException("Resolution did not apply.");
                    var tree=UnityEngine.Object.FindObjectsByType<ExplorationNodeTreePanel>(FindObjectsSortMode.None).Single();
                    var r=tree.Scroll.viewport.rect;var line=$"{size.name}: {Screen.width}x{Screen.height}; viewport={r.size}; nodes={tree.VisibleNodes.Count}; content={tree.Scroll.content.rect.size}";
                    report.Add(line);Debug.Log("TREE_SCREEN: "+line);Directory.CreateDirectory("Assets/Screenshots");ScreenCapture.CaptureScreenshot($"Assets/Screenshots/exploration-tree-{size.name}.png");phase=2;next=EditorApplication.timeSinceStartup+.6;return;
                }
                Remove();index++;
                if(index<Sizes.Length){phase=0;next=EditorApplication.timeSinceStartup+.2;return;}
                Directory.CreateDirectory("DOCS/development/verification");File.WriteAllLines("DOCS/development/verification/exploration-tree-screens.txt",report);Finish();Debug.Log("TREE_CAPTURE: PASS");
            }
            catch(Exception e){Finish();Debug.LogError("TREE_CAPTURE: FAIL "+e);}
        }
        private static void Remove(){if(main!=null){main.offsetMin=min;main.offsetMax=max;}if(custom>=0){selected.SetValue(view,previous);group.GetType().GetMethod("RemoveCustomSize").Invoke(group,new object[]{custom});custom=-1;}}
        private static void Changed(PlayModeStateChange state){if(state==PlayModeStateChange.ExitingPlayMode)Finish();}
        private static void Finish(){Remove();EditorApplication.update-=Tick;EditorApplication.playModeStateChanged-=Changed;group=null;}
    }
}
