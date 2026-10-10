using System;
using System.Collections.Generic;
using TxTRPG.Gameplay;
using TxTRPG.Gameplay.SlotMachine;
using UnityEngine;
using SlotMachineModel = TxTRPG.Gameplay.SlotMachine.SlotMachine;

namespace TxTRPG.Application.SlotMachine
{
    [Serializable]
    public sealed class TemporarySlotReelDefinition
    {
        [SerializeField] private string id = string.Empty;
        [SerializeField, Tooltip("Reel cells in order. Repeating a value makes it more likely.")]
        private int[] values = Array.Empty<int>();

        public TemporarySlotReelDefinition() { }

        public TemporarySlotReelDefinition(string reelId, params int[] reelValues)
        {
            id = reelId;
            values = reelValues ?? Array.Empty<int>();
        }

        public string Id => id?.Trim() ?? string.Empty;
        public IReadOnlyList<int> Values => values ?? Array.Empty<int>();
    }

    // Authoring source of the session slot machine. The asset is copied into TemporarySlotMachineState at startup and is
    // never changed at runtime.
    [CreateAssetMenu(menuName = "TxT RPG/Temporary Slot Machine Configuration", fileName = "TemporarySlotMachineConfiguration")]
    public sealed class TemporarySlotMachineConfiguration : ScriptableObject
    {
        [SerializeField] private List<TemporarySlotReelDefinition> reels = CreateDefaultReels();

        public IReadOnlyList<TemporarySlotReelDefinition> Reels => reels;

        public static List<TemporarySlotReelDefinition> CreateDefaultReels() => new()
        {
            new TemporarySlotReelDefinition("reel.1", 1, 2, 3, 4, 5, 6),
            new TemporarySlotReelDefinition("reel.2", 1, 2, 3, 4, 5, 6),
            new TemporarySlotReelDefinition("reel.3", 1, 2, 3, 4, 5, 6)
        };

        public bool TryCreateMachine(out SlotMachineModel machine, out string reason)
        {
            machine = null;
            if (reels == null || reels.Count == 0) { reason = "The slot machine configuration has no reels."; return false; }
            try
            {
                var created = new List<SlotReel>(reels.Count);
                for (var i = 0; i < reels.Count; i++)
                {
                    var definition = reels[i] ?? throw new ArgumentException($"Reel {i + 1} is empty.");
                    created.Add(SlotReel.FromValues(definition.Id, definition.Values));
                }
                machine = new SlotMachineModel(created);
                reason = string.Empty;
                return true;
            }
            catch (ArgumentException exception)
            {
                reason = exception.Message;
                return false;
            }
        }

#if UNITY_EDITOR
        public void ConfigureForEditor(IEnumerable<TemporarySlotReelDefinition> definitions) =>
            reels = new List<TemporarySlotReelDefinition>(definitions ?? Array.Empty<TemporarySlotReelDefinition>());
#endif
    }

    // Session-lifetime slot machine. Change the reels through Machine (TryAddReel, TryRemoveReel, TryReplaceReel,
    // TrySetSymbol, TryAddSymbol, TryReplaceStrip, ...); every change is validated atomically and raises Changed.
    public sealed class TemporarySlotMachineState
    {
        public TemporarySlotMachineState(SlotMachineModel machine)
        {
            Machine = machine ?? throw new ArgumentNullException(nameof(machine));
            Machine.Changed += () => Changed?.Invoke();
        }

        public SlotMachineModel Machine { get; }
        public int ReelCount => Machine.ReelCount;
        public event Action Changed;

        public SlotSpinResult Spin(IRandomIndexProvider random) => Machine.Spin(random);

        public static bool TryCreate(TemporarySlotMachineConfiguration configuration, out TemporarySlotMachineState state, out string reason)
        {
            state = null;
            if (configuration == null) { reason = "No slot machine configuration is assigned."; return false; }
            if (!configuration.TryCreateMachine(out var machine, out reason)) return false;
            state = new TemporarySlotMachineState(machine);
            return true;
        }
    }
}
