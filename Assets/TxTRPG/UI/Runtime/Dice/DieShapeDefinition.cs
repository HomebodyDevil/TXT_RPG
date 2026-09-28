using System;
using UnityEngine;

namespace TxTRPG.UI.Dice
{
    [CreateAssetMenu(menuName = "TxT RPG/UI/Die Shape", fileName = "DieShape")]
    public sealed class DieShapeDefinition : ScriptableObject
    {
        [SerializeField] private string definitionId;
        [SerializeField] private GameObject modelPrefab;
        [SerializeField] private Vector3[] facePoseEuler = Array.Empty<Vector3>();
        [SerializeField] private string readingRule;
        public string DefinitionId => definitionId?.Trim() ?? string.Empty;
        public GameObject ModelPrefab => modelPrefab;
        public int FaceCount => facePoseEuler?.Length ?? 0;
        public string ReadingRule => readingRule ?? string.Empty;
        public bool IsValidFor(int faceCount) => modelPrefab != null && faceCount > 0 && FaceCount == faceCount &&
            modelPrefab.GetComponent<DieModelView>() != null;
        public Quaternion PoseFor(int faceIndex)
        {
            if ((uint)faceIndex >= (uint)FaceCount) throw new ArgumentOutOfRangeException(nameof(faceIndex));
            return Quaternion.Euler(facePoseEuler[faceIndex]);
        }
#if UNITY_EDITOR
        public void ConfigureForEditor(string id, GameObject prefab, Vector3[] poses, string rule)
        { definitionId = id; modelPrefab = prefab; facePoseEuler = (Vector3[])poses.Clone(); readingRule = rule; }
#endif
    }

}
