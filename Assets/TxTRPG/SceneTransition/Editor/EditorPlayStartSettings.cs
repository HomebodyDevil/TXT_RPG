using UnityEditor;
using UnityEngine;

namespace TxTRPG.SceneTransition.Editor
{
    [FilePath("ProjectSettings/TxTRPGEditorPlaySettings.asset", FilePathAttribute.Location.ProjectFolder)]
    public sealed class EditorPlayStartSettings : ScriptableSingleton<EditorPlayStartSettings>
    {
        [SerializeField] private bool startThroughAppScene = true;
        [SerializeField] private bool ownsStartScene;
        [SerializeField] private string previousStartSceneGuid = string.Empty;

        public bool StartThroughAppScene => startThroughAppScene;
        public bool OwnsStartScene => ownsStartScene;
        public string PreviousStartSceneGuid => previousStartSceneGuid ?? string.Empty;

        internal void Configure(bool enabled, bool owns, string previousGuid)
        {
            startThroughAppScene = enabled;
            ownsStartScene = owns;
            previousStartSceneGuid = previousGuid ?? string.Empty;
            Save(true);
        }
    }
}
