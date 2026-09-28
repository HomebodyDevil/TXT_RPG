using System;
using System.Collections.Generic;
using UnityEngine;

namespace TxTRPG.UI.Dice
{
    [CreateAssetMenu(menuName = "TxT RPG/UI/Die Shape Catalog", fileName = "DieShapeCatalog")]
    public sealed class DieShapeCatalog : ScriptableObject
    {
        [SerializeField] private List<DieShapeDefinition> shapes = new();
        public DieShapeDefinition Find(string definitionId, int faceCount)
        {
            foreach (var shape in shapes)
                if (shape != null && string.Equals(shape.DefinitionId, definitionId, StringComparison.Ordinal) &&
                    shape.IsValidFor(faceCount)) return shape;
            return null;
        }
#if UNITY_EDITOR
        public void ConfigureForEditor(IEnumerable<DieShapeDefinition> definitions) => shapes = new List<DieShapeDefinition>(definitions);
#endif
    }
}
