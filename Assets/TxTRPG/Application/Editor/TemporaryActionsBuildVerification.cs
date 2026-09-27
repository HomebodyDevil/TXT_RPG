using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Player;
using UnityEngine;
namespace TxTRPG.Application.Editor
{
    public static class TemporaryActionsBuildVerification
    {
        [MenuItem("Tools/TxT RPG/UI/Actions/Temporary/Compile Windows And Android Scripts")]
        public static void Compile()
        {
            if (EditorApplication.isPlaying || EditorApplication.isCompiling) throw new InvalidOperationException("Leave Play Mode and wait for compilation.");
            foreach (var target in new[] { BuildTarget.StandaloneWindows64, BuildTarget.Android })
            {
                var folder=Path.Combine(Path.GetTempPath(),"TxTRPG-Actions-SingleRow-Scripts",target.ToString());
                Directory.CreateDirectory(folder);
                var settings=new ScriptCompilationSettings { target=target, group=BuildPipeline.GetBuildTargetGroup(target), options=ScriptCompilationOptions.None };
                var result=PlayerBuildInterface.CompilePlayerScripts(settings,folder);
                if(result.assemblies==null || result.assemblies.Count==0) throw new InvalidOperationException("Player script compilation failed: "+target);
                Debug.Log("ACTIONS_PLAYER_SCRIPTS: PASS "+target+" assemblies="+result.assemblies.Count+" output="+folder);
            }
        }
    }
}
