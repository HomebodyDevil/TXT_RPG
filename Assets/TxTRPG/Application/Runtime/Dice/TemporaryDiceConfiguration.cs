using System;
using System.Collections.Generic;
using TxTRPG.Gameplay.Dice;
using UnityEngine;
using UnityEngine.Serialization;

namespace TxTRPG.Application.Dice
{
    [Serializable]
    public sealed class TemporaryDieFaceDefinition
    {
        [SerializeField] private DiceEffectKind effectKind = DiceEffectKind.Attack;
        [SerializeField] private int amount = 1;

        public TemporaryDieFaceDefinition() { }

        public DiceEffectKind EffectKind => effectKind;
        public int Amount => amount;
        public DiceFace ToFace() => new DiceFace(effectKind, amount);

#if UNITY_EDITOR
        public TemporaryDieFaceDefinition(DiceEffectKind kind, int value)
        {
            effectKind = kind;
            amount = value;
        }
#endif
    }

    [Serializable]
    public sealed class TemporaryDieDefinition
    {
        [SerializeField] private string id = string.Empty;
        [SerializeField] private string displayName = string.Empty;
        [SerializeField] private TemporaryDieFaceDefinition[] faces = Array.Empty<TemporaryDieFaceDefinition>();
        [FormerlySerializedAs("faceValues")]
        [SerializeField, HideInInspector] private int[] legacyFaceValues = Array.Empty<int>();
        [SerializeField] private int minimumValue = 1;
        [SerializeField] private int maximumValue = 6;

        public string Id => id?.Trim() ?? string.Empty;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? Id : displayName.Trim();
        public IReadOnlyList<TemporaryDieFaceDefinition> Faces => faces;
        public int MinimumValue => minimumValue;
        public int MaximumValue => maximumValue;
        public bool HasLegacyFaces => (faces == null || faces.Length == 0) && legacyFaceValues != null && legacyFaceValues.Length > 0;

        public DiceFace[] CreateRuntimeFaces()
        {
            if (faces == null || faces.Length == 0)
                throw new InvalidOperationException($"Temporary die '{Id}' has no effect faces. Upgrade known defaults or configure each face explicitly.");
            var result = new DiceFace[faces.Length];
            for (var i = 0; i < faces.Length; i++)
            {
                if (faces[i] == null) throw new InvalidOperationException($"Temporary die '{Id}' face {i + 1} is null.");
                result[i] = faces[i].ToFace();
            }
            return result;
        }

#if UNITY_EDITOR
        public void ConfigureForEditor(string identifier, string name, int faceCount)
        {
            id = identifier;
            displayName = name;
            minimumValue = 1;
            maximumValue = faceCount;
            faces = CreateAlternatingFaces(faceCount);
            legacyFaceValues = Array.Empty<int>();
        }

        public bool IsKnownLegacyDefaultForEditor(string expectedId, int expectedFaceCount)
        {
            if (!string.Equals(Id, expectedId, StringComparison.Ordinal) || faces != null && faces.Length > 0)
                return false;
            if (legacyFaceValues == null || legacyFaceValues.Length != expectedFaceCount ||
                minimumValue != 1 || maximumValue != expectedFaceCount)
                return false;
            for (var i = 0; i < legacyFaceValues.Length; i++)
                if (legacyFaceValues[i] != i + 1) return false;
            return true;
        }

        public void UpgradeKnownDefaultForEditor(int expectedFaceCount)
        {
            faces = CreateAlternatingFaces(expectedFaceCount);
            legacyFaceValues = Array.Empty<int>();
        }

        private static TemporaryDieFaceDefinition[] CreateAlternatingFaces(int count)
        {
            var result = new TemporaryDieFaceDefinition[count];
            for (var i = 0; i < count; i++)
                result[i] = new TemporaryDieFaceDefinition(i % 2 == 0 ? DiceEffectKind.Attack : DiceEffectKind.Heal, i + 1);
            return result;
        }
#endif
    }

    [CreateAssetMenu(menuName = "TxT RPG/Temporary Dice Configuration", fileName = "TemporaryDiceConfiguration")]
    public sealed class TemporaryDiceConfiguration : ScriptableObject
    {
        [SerializeField] private bool enabledForSession;
        [SerializeField] private List<TemporaryDieDefinition> dice = new();

        public bool EnabledForSession => enabledForSession;
        public IReadOnlyList<TemporaryDieDefinition> Dice => dice;

#if UNITY_EDITOR
        public void ConfigureForEditor(bool enabled, IEnumerable<TemporaryDieDefinition> definitions)
        {
            enabledForSession = enabled;
            dice = new List<TemporaryDieDefinition>(definitions);
        }

        public bool TryUpgradeKnownDefaultsForEditor()
        {
            if (!enabledForSession || dice == null || dice.Count != 3) return false;
            var expectedIds = new[] { "temporary.d4", "temporary.d6", "temporary.d8" };
            var expectedCounts = new[] { 4, 6, 8 };
            for (var i = 0; i < dice.Count; i++)
            {
                if (dice[i] == null ||
                    !dice[i].IsKnownLegacyDefaultForEditor(expectedIds[i], expectedCounts[i]))
                    return false;
            }
            for (var i = 0; i < dice.Count; i++)
                dice[i].UpgradeKnownDefaultForEditor(expectedCounts[i]);
            return true;
        }
#endif
    }
}
