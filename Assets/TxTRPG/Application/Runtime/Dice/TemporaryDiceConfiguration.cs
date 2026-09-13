using System;
using System.Collections.Generic;
using UnityEngine;

namespace TxTRPG.Application.Dice
{
    [Serializable]
    public sealed class TemporaryDieDefinition
    {
        [SerializeField] private string id = string.Empty;
        [SerializeField] private string displayName = string.Empty;
        [SerializeField] private int[] faceValues = Array.Empty<int>();
        [SerializeField] private int minimumValue = 1;
        [SerializeField] private int maximumValue = 6;
        public string Id => id?.Trim() ?? string.Empty;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? Id : displayName.Trim();
        public IReadOnlyList<int> FaceValues => faceValues;
        public int MinimumValue => minimumValue;
        public int MaximumValue => maximumValue;
#if UNITY_EDITOR
        public void ConfigureForEditor(string identifier, string name, int faces)
        { id = identifier; displayName = name; minimumValue = 1; maximumValue = faces; faceValues = new int[faces]; for (var i = 0; i < faces; i++) faceValues[i] = i + 1; }
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
        { enabledForSession = enabled; dice = new List<TemporaryDieDefinition>(definitions); }
#endif
    }
}