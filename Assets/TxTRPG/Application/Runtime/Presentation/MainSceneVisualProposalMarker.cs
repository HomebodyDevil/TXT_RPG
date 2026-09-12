using UnityEngine;

namespace TxTRPG.Application.Presentation
{
    [DisallowMultipleComponent]
    public sealed class MainSceneVisualProposalMarker : MonoBehaviour
    {
        [SerializeField] private int schemaVersion = 1;
        [SerializeField] private string sourceScenePath = "Assets/Scenes/TMP_MainScene.unity";

        public int SchemaVersion => schemaVersion;
        public string SourceScenePath => sourceScenePath;

#if UNITY_EDITOR
        public void ConfigureForEditor(int version, string sourcePath)
        {
            schemaVersion = Mathf.Max(1, version);
            sourceScenePath = sourcePath ?? string.Empty;
        }
#endif
    }
}
