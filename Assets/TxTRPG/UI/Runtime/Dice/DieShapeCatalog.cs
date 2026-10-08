using System;
using System.Collections.Generic;
using UnityEngine;

namespace TxTRPG.UI.Dice
{
    [CreateAssetMenu(menuName = "TxT RPG/UI/Die Shape Catalog", fileName = "DieShapeCatalog")]
    public sealed class DieShapeCatalog : ScriptableObject
    {
        [SerializeField] private List<DieShapeDefinition> shapes = new();
        public IReadOnlyList<DieShapeDefinition> Shapes => shapes;

        // Shapes that fail the flat-upward-face or symmetry checks are never returned, so the panel reports
        // "3D 형태 없음" and the text result path instead of disguising the die as another shape.
        public DieShapeDefinition Find(string definitionId, int faceCount)
        {
            foreach (var shape in shapes)
                if (shape != null && shape.Matches(definitionId) && shape.IsValidFor(faceCount)) return shape;
            return null;
        }

        public bool TryValidate(out List<string> diagnostics)
        {
            diagnostics = new List<string>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var shape in shapes)
            {
                if (shape == null) { diagnostics.Add("The catalog contains a missing shape reference."); continue; }
                if (!shape.IsPresentable) diagnostics.Add($"{shape.name}: {shape.ValidationError}");
                if (shape.ModelPrefab == null || shape.ModelPrefab.GetComponent<DieModelView>() == null)
                    diagnostics.Add($"{shape.name}: the model Prefab or DieModelView is missing.");
                if (!ids.Add(shape.DefinitionId)) diagnostics.Add($"{shape.name}: duplicate definition ID '{shape.DefinitionId}'.");
                foreach (var alias in shape.AdditionalDefinitionIds)
                    if (!string.IsNullOrWhiteSpace(alias) && !ids.Add(alias.Trim()))
                        diagnostics.Add($"{shape.name}: duplicate definition ID '{alias.Trim()}'.");
            }
            return diagnostics.Count == 0;
        }

#if UNITY_EDITOR
        public void ConfigureForEditor(IEnumerable<DieShapeDefinition> definitions) => shapes = new List<DieShapeDefinition>(definitions);
#endif
    }
}
