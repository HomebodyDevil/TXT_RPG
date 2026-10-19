using System.Linq;
using TxTRPG.Gameplay.Encounters;

namespace TxTRPG.Application.Encounters
{
    // "{element names} : {kind}", for example "룬 : 전투" or "룬, 유물 : 휴식". Elements follow the catalog order; an id
    // missing from the catalog is shown as the id itself (after the catalog elements, in result order).
    public static class EncounterResultFormatter
    {
        public const string NoElementsMessage = "선택된 요소가 없습니다";
        public const string ChanceFailedMessage = "이벤트 없음 (발생 확률 판정 실패)";
        public const string NoKindMessage = "종류 가중치가 모두 0입니다";

        public static string KindName(EncounterKind kind) => kind switch
        {
            EncounterKind.Combat => "전투",
            EncounterKind.Event => "이벤트",
            EncounterKind.Rest => "휴식",
            _ => kind.ToString()
        };

        public static string Format(EncounterRollResult result, EncounterElementCatalog catalog)
        {
            if (result == null) return string.Empty;
            switch (result.Outcome)
            {
                case EncounterRollOutcome.NoElementsSelected: return NoElementsMessage;
                case EncounterRollOutcome.ChanceFailed: return ChanceFailedMessage;
                case EncounterRollOutcome.NoKindAvailable: return NoKindMessage;
            }
            var ordered = result.ElementIds
                .Select((id, order) => (id, order, index: catalog != null ? catalog.IndexOf(id) : -1))
                .OrderBy(item => item.index < 0 ? int.MaxValue : item.index)
                .ThenBy(item => item.order)
                .Select(item => DisplayName(item.id, catalog));
            return $"{string.Join(", ", ordered)} : {KindName(result.Kind)}";
        }

        private static string DisplayName(string id, EncounterElementCatalog catalog) =>
            catalog != null && catalog.TryGetDisplayName(id, out var name) ? name : id;
    }
}
