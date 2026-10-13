using System.Collections.Generic;
using System.Globalization;
using TxTRPG.Application.SlotMachine;
using TxTRPG.Gameplay.Exploration.Events;
using TxTRPG.Gameplay.SlotMachine;

namespace TxTRPG.Application.Exploration.Events
{
    // Every system text of the exploration event path (event screen, node card and Story). Event titles, bodies and
    // labels are asset strings; when they become localization keys, only this class and the display conversion change.
    public static class ExplorationEventStoryFormatter
    {
        public const string Prefix = "[이벤트]";
        public const string ContinueLabel = "계속";
        public const string SlotCheckLabel = "슬롯을 돌린다";
        public const string EndEventLabel = "이벤트 종료";
        public const string CardBadge = "이벤트";
        public const string NodeTypeName = "이벤트";
        public const string UnavailableTitle = "이벤트";
        public const string UnavailableBody = "이벤트를 불러오지 못했습니다";
        public const string NoUsableOption = "선택할 수 있는 행동이 없습니다.";
        public const string SlotUnavailableReason = "슬롯머신을 사용할 수 없음";
        public const string SlotBusy = Prefix + " 다른 행동이 진행 중이라 슬롯을 돌릴 수 없습니다.";
        public const string ActionFailed = Prefix + " 이벤트를 처리하지 못했습니다.";
        public const string LastReelKept = "Reel이 하나뿐이라 제거하지 않았습니다.";

        public static string ConditionUnmet(string reason) => $"조건 미충족: {reason}";
        public static string CardTitle(string title) => string.IsNullOrWhiteSpace(title) ? NodeTypeName : title;
        public static string NodeSelected(string title, string nodeId) => $"[탐험] 이벤트: {CardTitle(title)} 노드를 선택했습니다. ({nodeId})";
        public static string Started(string title) => $"{Prefix} {CardTitle(title)}";
        public static string Ended(string title) => $"{Prefix} {CardTitle(title)}을(를) 마쳤습니다.";
        public static string Status(int current, int maximum) => $"이벤트 진행 중 · 현재 체력 {current}/{maximum}";

        // ---------- Step results ----------

        public static IReadOnlyList<string> Step(ExplorationEventStepResult step, ExplorationEventInputKind? input)
        {
            var lines = new List<string>();
            foreach (var entry in step.Entries)
            {
                switch (entry.Kind)
                {
                    case ExplorationEventStepEntryKind.Input:
                        if (input == ExplorationEventInputKind.Choose) lines.Add($"{Prefix} 선택: {entry.Text}");
                        else if (input == ExplorationEventInputKind.SlotResult) lines.Add($"{Prefix} 판정 결과: {entry.Text}");
                        break;
                    case ExplorationEventStepEntryKind.EffectApplied:
                        if (!string.IsNullOrWhiteSpace(entry.Text)) lines.Add(entry.Text);
                        break;
                    case ExplorationEventStepEntryKind.EffectSkipped:
                    case ExplorationEventStepEntryKind.EffectFailed:
                        lines.Add(EffectSkipped(entry.Text));
                        break;
                    case ExplorationEventStepEntryKind.PageVisitLimit:
                        lines.Add($"{Prefix} 이벤트가 너무 길어져 종료되었습니다.");
                        break;
                    case ExplorationEventStepEntryKind.Defeated:
                        lines.Add($"{Prefix} 체력이 0이 되었습니다.");
                        break;
                }
            }
            return lines;
        }

        public static string EffectSkipped(string reason) => $"{Prefix} 적용할 수 없는 효과를 건너뛰었습니다: {reason}";

        // "[슬롯] 3 + 5×3 + 2 = 20" and the Fortune Critical lines, as in combat.
        public static IReadOnlyList<string> SlotCheck(FortuneCriticalResult fortune)
        {
            var lines = new List<string> { SlotCombatStoryFormatter.Spin(fortune) };
            var summary = SlotCombatStoryFormatter.FortuneSummary(fortune);
            if (summary != null) lines.Add(summary);
            var cap = SlotCombatStoryFormatter.FortuneCap(fortune);
            if (cap != null) lines.Add(cap);
            return lines;
        }

        // ---------- Effects ----------

        public static string HealthChanged(int applied, int current, int maximum) =>
            $"{Prefix} 체력 {Signed(applied)} ({current}/{maximum})";
        public static string HealthRestored(int current, int maximum) => $"{Prefix} 체력을 모두 회복했습니다 ({current}/{maximum})";
        public static string MaximumHealthChanged(int change, int current, int maximum) =>
            $"{Prefix} 최대 체력 {Signed(change)} ({current}/{maximum})";

        public static string ReelNotFound(string selector) => $"{selector}을(를) 찾지 못했습니다.";
        public static string ReelSymbolsAdded(int index, string reelId, int value, int count) =>
            $"{Prefix} {index + 1}번 Reel({reelId})에 {value} 칸 {count}개를 추가했습니다.";
        public static string ReelSymbolNotRemovable(int index, string reelId, int value) =>
            $"{index + 1}번 Reel({reelId})에서 제거할 수 있는 {value} 칸이 없습니다.";
        public static string ReelSymbolsRemoved(int index, string reelId, int value, int removed, int requested) =>
            $"{Prefix} {index + 1}번 Reel({reelId})에서 {value} 칸 {removed}개를 제거했습니다." +
            (removed < requested ? $" (요청 {requested}개 중 마지막 칸 보호로 일부만 제거)" : string.Empty);
        public static string ReelAdded(string reelId, IReadOnlyList<int> values, int reelCount) =>
            $"{Prefix} Reel {reelId}({string.Join(", ", values)})을(를) 추가했습니다. (Reel {reelCount}개)";
        public static string ReelRemoved(int index, string reelId, int reelCount) =>
            $"{Prefix} {index + 1}번 Reel({reelId})을(를) 제거했습니다. (Reel {reelCount}개)";

        public static string FortuneChanceChanged(string target, double previous, double next) =>
            $"{Prefix} {SlotCombatStoryFormatter.FortuneCriticalName} 확률({target}) {Percent(previous)} → {Percent(next)}";
        public static string FortuneCapChanged(int previous, int next) =>
            $"{Prefix} {SlotCombatStoryFormatter.FortuneCriticalName} 최대 배수 ×{previous} → ×{next}";

        public static string UnknownRune(string definitionId) => $"룬 정의 '{definitionId}'이(가) 없습니다.";
        public static string RuneGranted(string name, int count, ExplorationRunePlacement placement, string lineId) => placement switch
        {
            ExplorationRunePlacement.NewLine => $"{Prefix} {name} {count}개를 얻어 새 줄({lineId})에 장착했습니다.",
            ExplorationRunePlacement.StackOnLine => $"{Prefix} {name} {count}개를 얻어 줄 {lineId}에 쌓았습니다.",
            _ => $"{Prefix} {name} {count}개를 얻었습니다. (장착하지 않음)"
        };

        // ---------- Conditions ----------

        public static string HealthRequired(int value) => $"체력 {value} 이상 필요";
        public static string ReelCountAtLeastRequired(int value) => $"Reel {value}개 이상 필요";
        public static string ReelCountAtMostRequired(int value) => $"Reel {value}개 이하 필요";
        public static string RuneRequired(string name, int count) => $"{name} {count}개 필요";

        private static string Signed(int value) => value >= 0 ? $"+{value}" : value.ToString(CultureInfo.InvariantCulture);
        private static string Percent(double chance) => (chance * 100d).ToString("0.##", CultureInfo.InvariantCulture) + "%";
    }
}
