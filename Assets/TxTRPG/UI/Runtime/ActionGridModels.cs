using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace TxTRPG.UI
{
    public enum ActionGridEntryKind
    {
        Item,
        Skill,
        Equipment,
        QuestItem
    }

    public enum ActionGridLayoutMode
    {
        FixedColumns,
        AdaptiveCellSize
    }

    public enum ActionGridHorizontalAlignment
    {
        Left,
        Center,
        Right
    }

    public enum ActionGridVerticalPlacement
    {
        Top,
        CenterWhenContentFits
    }

    public enum ActionGridPopulationMode
    {
        EntriesOnly,
        FillCapacityWithEmptySlots
    }

    public enum GridActivationBehavior
    {
        SelectOnly,
        OpenContextMenu,
        ExecuteDefaultAction
    }

    public enum ActionGridPackingMode { CompactForward, PreserveSlots }
    public enum CapacityReductionPolicy { RejectIfOccupied, MoveOverflow, RemoveOverflow }
    public enum ScrollbarVisibilityMode { Hidden, Auto, Always }
    public enum ScrollbarSpaceMode
    {
        ReserveAlways,
        ReserveWhenVisible,
        Overlay,
        ReserveSymmetricallyAlways
    }
    public enum ScrollbarSide { Left, Right }
    public enum ScrollbarHandleSizeMode { ContentRatio, FixedNormalized, FixedPixels, MinimumPixels }

    public readonly struct CapacityChangeResult
    {
        public CapacityChangeResult(bool succeeded, int previousCapacity, int capacity, int overflowCount)
        {
            Succeeded = succeeded;
            PreviousCapacity = previousCapacity;
            Capacity = capacity;
            OverflowCount = overflowCount;
        }

        public bool Succeeded { get; }
        public int PreviousCapacity { get; }
        public int Capacity { get; }
        public int OverflowCount { get; }
    }

    public readonly struct ActionGridEntry
    {
        public ActionGridEntry(
            string id,
            ActionGridEntryKind kind,
            Sprite icon,
            string displayName,
            string description = "",
            int quantity = 0,
            bool isEnabled = true,
            float cooldownNormalized = 0f,
            string shortcutLabel = "",
            string iconAssetId = "")
        {
            Id = id ?? string.Empty;
            Kind = kind;
            Icon = icon;
            DisplayName = displayName ?? string.Empty;
            Description = description ?? string.Empty;
            Quantity = Mathf.Max(0, quantity);
            IsEnabled = isEnabled;
            CooldownNormalized = Mathf.Clamp01(cooldownNormalized);
            ShortcutLabel = shortcutLabel ?? string.Empty;
            IconAssetId = iconAssetId ?? string.Empty;
        }

        public string Id { get; }
        public ActionGridEntryKind Kind { get; }
        public Sprite Icon { get; }
        public string DisplayName { get; }
        public string Description { get; }
        public int Quantity { get; }
        public bool IsEnabled { get; }
        public float CooldownNormalized { get; }
        public string ShortcutLabel { get; }
        public string IconAssetId { get; }
        public string EntryInstanceId => Id;

        public ActionGridEntry WithIcon(Sprite icon)
        {
            return new ActionGridEntry(
                Id,
                Kind,
                icon,
                DisplayName,
                Description,
                Quantity,
                IsEnabled,
                CooldownNormalized,
                ShortcutLabel,
                IconAssetId);
        }
    }

    public readonly struct ActionMenuOption
    {
        public ActionMenuOption(
            string commandId,
            string label,
            bool isEnabled = true,
            string disabledReason = "")
        {
            CommandId = commandId ?? string.Empty;
            Label = label ?? string.Empty;
            IsEnabled = isEnabled;
            DisabledReason = disabledReason ?? string.Empty;
        }

        public string CommandId { get; }
        public string Label { get; }
        public bool IsEnabled { get; }
        public string DisabledReason { get; }
    }

    public readonly struct ActionCommandResult
    {
        public ActionCommandResult(bool succeeded, string message = "")
        {
            Succeeded = succeeded;
            Message = message ?? string.Empty;
        }

        public bool Succeeded { get; }
        public string Message { get; }
    }

    public interface IActionGridEntryProvider
    {
        System.Collections.Generic.IReadOnlyList<ActionGridEntry> GetEntries();
    }

    public interface IActionMenuProvider
    {
        System.Collections.Generic.IReadOnlyList<ActionMenuOption> GetOptions(string entryId);
    }

    public interface IActionCommandExecutor
    {
        Task<ActionCommandResult> ExecuteAsync(
            string entryId,
            string commandId,
            CancellationToken cancellationToken);
    }
}
