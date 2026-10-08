using System;
using System.Collections.Generic;
using UnityEngine;

namespace TxTRPG.UI.Dice
{
    [CreateAssetMenu(menuName = "TxT RPG/UI/Die Shape", fileName = "DieShape")]
    public sealed class DieShapeDefinition : ScriptableObject
    {
        // Developer-owned: identity, model choice, aliases and reading rule text.
        [SerializeField] private string definitionId;
        [SerializeField, Tooltip("Other die definition IDs that use this same model and face layout.")]
        private string[] additionalDefinitionIds = Array.Empty<string>();
        [SerializeField] private GameObject modelPrefab;
        [SerializeField] private string readingRule;
        // Generator-owned for generated models: per-face data in face-index order (never inferred from mesh order).
        [SerializeField, Tooltip("Rest pose per face: that face points up and its label reads away from the camera.")]
        private Vector3[] facePoseEuler = Array.Empty<Vector3>();
        [SerializeField] private Vector3[] faceNormals = Array.Empty<Vector3>();
        [SerializeField] private Vector3[] faceReadingUps = Array.Empty<Vector3>();
        [SerializeField, Tooltip("Convex collision proxy; must match the visual polyhedron.")] private Mesh colliderMesh;
        [SerializeField, Min(0f), Tooltip("Centre-to-face distance; the centre height of a die resting on a face.")]
        private float restHeight;
        [SerializeField, Min(0f), Tooltip("Centre-to-farthest-vertex distance.")] private float boundingRadius;

        [NonSerialized] private bool validated;
        [NonSerialized] private string validationError;
        [NonSerialized] private Quaternion[] symmetries;

        public string DefinitionId => definitionId?.Trim() ?? string.Empty;
        public IReadOnlyList<string> AdditionalDefinitionIds => additionalDefinitionIds ?? Array.Empty<string>();
        public GameObject ModelPrefab => modelPrefab;
        public int FaceCount => facePoseEuler?.Length ?? 0;
        public string ReadingRule => readingRule ?? string.Empty;
        public IReadOnlyList<Vector3> FaceNormals => faceNormals ?? Array.Empty<Vector3>();
        public IReadOnlyList<Vector3> FaceReadingUps => faceReadingUps ?? Array.Empty<Vector3>();
        public Mesh ColliderMesh => colliderMesh;
        public float RestHeight => restHeight;
        public float BoundingRadius => boundingRadius;

        public bool Matches(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return false;
            id = id.Trim();
            if (string.Equals(DefinitionId, id, StringComparison.Ordinal)) return true;
            foreach (var alias in AdditionalDefinitionIds)
                if (string.Equals(alias?.Trim(), id, StringComparison.Ordinal)) return true;
            return false;
        }

        public bool IsValidFor(int faceCount) => modelPrefab != null && faceCount > 0 && FaceCount == faceCount &&
            modelPrefab.GetComponent<DieModelView>() != null && IsPresentable;

        // A presentable shape always rests with one face parallel to the floor and can show any face by symmetry.
        public bool IsPresentable { get { Validate(); return string.IsNullOrEmpty(validationError); } }
        public string ValidationError { get { Validate(); return validationError; } }

        public IReadOnlyList<Quaternion> Symmetries { get { Validate(); return symmetries; } }

        public Quaternion PoseFor(int faceIndex)
        {
            if ((uint)faceIndex >= (uint)FaceCount) throw new ArgumentOutOfRangeException(nameof(faceIndex));
            return Quaternion.Euler(facePoseEuler[faceIndex]);
        }

        private void Validate()
        {
            if (validated) return;
            validated = true;
            symmetries = Array.Empty<Quaternion>();
            if (FaceCount == 0 || FaceNormals.Count != FaceCount)
                validationError = "Face normals are missing or do not match the face count.";
            else if (colliderMesh == null) validationError = "The collision proxy mesh is missing.";
            else if (restHeight <= 0f || boundingRadius < restHeight) validationError = "Rest height or bounding radius is invalid.";
            else if (DieFaceGeometry.TryValidate(FaceNormals, FaceReadingUps, out var error))
            {
                validationError = string.Empty;
                symmetries = DieFaceGeometry.ComputeRotationSymmetries(FaceNormals);
            }
            else validationError = error;
        }

        private void OnEnable() => validated = false;
        private void OnValidate() => validated = false;

#if UNITY_EDITOR
        public void ConfigureForEditor(string id, GameObject prefab, Vector3[] poses, string rule)
        { definitionId = id; modelPrefab = prefab; facePoseEuler = (Vector3[])poses.Clone(); readingRule = rule; validated = false; }

        public void ConfigureGeneratedDataForEditor(Vector3[] poses, Vector3[] normals, Vector3[] readingUps, Mesh collider,
            float centerToFace, float centerToVertex)
        {
            facePoseEuler = (Vector3[])poses.Clone();
            faceNormals = (Vector3[])normals.Clone();
            faceReadingUps = (Vector3[])readingUps.Clone();
            colliderMesh = collider;
            restHeight = centerToFace;
            boundingRadius = centerToVertex;
            validated = false;
        }

        public bool EnsureAdditionalDefinitionIdForEditor(string id)
        {
            if (Matches(id)) return false;
            var list = new List<string>(AdditionalDefinitionIds) { id };
            additionalDefinitionIds = list.ToArray();
            validated = false;
            return true;
        }
#endif
    }
}
