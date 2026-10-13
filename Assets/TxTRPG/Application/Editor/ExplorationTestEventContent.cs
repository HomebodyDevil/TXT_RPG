using System;
using TxTRPG.Application.Exploration.Events;
using TxTRPG.Application.Runes;
using TxTRPG.Gameplay.Exploration.Events;

namespace TxTRPG.Application.Editor
{
    // Initial content of the five temporary test events (DOCS/instructions/exploration-event-nodes.md 9.2). Used only when
    // an asset is created; existing assets are developer-owned and never rewritten. Texts and numbers are temporary values.
    public static class ExplorationTestEventContent
    {
        private const string End = ExplorationEventTargets.End;

        public static void Configure(ExplorationEventDefinition definition, string eventId)
        {
            switch (eventId)
            {
                case "event.test.signpost": Signpost(definition); break;
                case "event.test.shrine": Shrine(definition); break;
                case "event.test.slot-trial": SlotTrial(definition); break;
                case "event.test.workshop": Workshop(definition); break;
                case "event.test.trap": Trap(definition); break;
                default: throw new ArgumentException($"Unknown test event '{eventId}'.", nameof(eventId));
            }
        }

        private static ExplorationEventBranchDefinition Go(string next, params IExplorationEventEffect[] effects) => new(next, effects);
        private static ExplorationEventContinueDefinition Continue(string next, params IExplorationEventEffect[] effects) =>
            new(ExplorationEventStoryFormatter.ContinueLabel, Go(next, effects));
        private static ExplorationEventPageDefinition Page(string id, string title, string body, ExplorationEventInteractionDefinition interaction,
            params IExplorationEventEffect[] onEnter) => new(id, title, body, interaction, onEnter);

        // Text only: two pages and Continue.
        private static void Signpost(ExplorationEventDefinition definition) =>
            definition.ConfigureForEditor("event.test.signpost", "낡은 표지판", "갈림길에 오래된 표지판이 서 있다.", "start", new[]
            {
                Page("start", string.Empty, "이끼 낀 표지판에 희미한 글씨가 남아 있다.\n\n\"이 길 끝에서 운명이 굴러간다.\"", Continue("back")),
                Page("back", "표지판 뒷면", "뒷면에는 누군가 급히 새긴 글씨가 있다.\n\n\"돌아가지 마라.\"\n\n특별한 일은 일어나지 않았다.", Continue(End))
            });

        // Choices, a condition, health, maximum health and Fortune Critical.
        private static void Shrine(ExplorationEventDefinition definition) =>
            definition.ConfigureForEditor("event.test.shrine", "수상한 제단", "붉게 물든 제단이 무언가를 원한다.", "start", new[]
            {
                Page("start", string.Empty, "길가에 낡은 제단이 있다. 제단 위의 그릇이 희미하게 빛난다.",
                    new ExplorationEventChoiceDefinition(
                        new ExplorationEventChoiceOptionDefinition("기도한다", Go("prayed", new ChangeMaximumHealthEffect(5))),
                        new ExplorationEventChoiceOptionDefinition("피를 바친다", Go("offered", new ChangeHealthEffect(-5),
                            new ChangeFortuneChanceEffect(ExplorationFortuneTarget.DefaultReel, ExplorationValueChangeMode.Add, .02f)), new HealthAtLeastCondition(6)),
                        new ExplorationEventChoiceOptionDefinition("지나친다", Go(End)))),
                Page("prayed", string.Empty, "따뜻한 기운이 몸을 감싼다. 더 오래 버틸 수 있을 것 같다.", Continue(End)),
                Page("offered", string.Empty, "그릇에 피가 스며들자 Reel이 어디선가 덜컥 움직이는 소리가 들린다.", Continue(End))
            });

        // Slot ranges with the Fortune Critical final total and a reel symbol change.
        private static void SlotTrial(ExplorationEventDefinition definition) =>
            definition.ConfigureForEditor("event.test.slot-trial", "운명의 슬롯", "낡은 슬롯머신이 결과에 따라 대가나 보상을 준다.", "start", new[]
            {
                Page("start", string.Empty, "먼지 쌓인 슬롯머신이 멈춰 있다. 손잡이에는 이렇게 적혀 있다.\n\n6 이하: 대가\n7~12: 아무 일도 없음\n13 이상: 보상",
                    new ExplorationEventSlotCheckDefinition(ExplorationEventStoryFormatter.SlotCheckLabel, true, Go("middle"),
                        new ExplorationEventSlotOutcomeDefinition(null, 6, Go("low", new ChangeHealthEffect(-4))),
                        new ExplorationEventSlotOutcomeDefinition(7, 12, Go("middle")),
                        new ExplorationEventSlotOutcomeDefinition(13, null, Go("high", new ChangeHealthEffect(6),
                            new AddReelSymbolEffect(new ExplorationReelSelector(ExplorationReelSelectorMode.First), 7))))),
                Page("low", "운명의 슬롯: 대가", "기계가 날카로운 소리를 내며 손을 문다.", Continue(End)),
                Page("middle", "운명의 슬롯: 무승부", "기계가 조용히 멈췄다. 아무 일도 일어나지 않았다.", Continue(End)),
                Page("high", "운명의 슬롯: 보상", "동전이 쏟아지고, 첫 번째 Reel에 반짝이는 7이 새겨졌다.", Continue(End))
            });

        // Reel count, symbol removal and rune grant.
        private static void Workshop(ExplorationEventDefinition definition) =>
            definition.ConfigureForEditor("event.test.workshop", "떠돌이 세공사", "세공사가 슬롯머신이나 룬을 손봐 준다고 한다.", "start", new[]
            {
                Page("start", string.Empty, "떠돌이 세공사가 공구를 펼친다. \"하나만 골라.\"",
                    new ExplorationEventChoiceDefinition(
                        new ExplorationEventChoiceOptionDefinition("Reel을 하나 더 단다", Go(End, new AddReelEffect("reel.event", 1, 2, 3, 4, 5, 6))),
                        new ExplorationEventChoiceOptionDefinition("첫 Reel의 1을 깎아낸다", Go(End,
                            new RemoveReelSymbolEffect(new ExplorationReelSelector(ExplorationReelSelectorMode.First), 1))),
                        new ExplorationEventChoiceOptionDefinition("공격 룬을 받는다", Go(End,
                            new GrantRuneEffect(TemporaryRuneConfiguration.AttackRuneId, 1, ExplorationRunePlacement.NewLine)))))
            });

        // onEnter damage; at 6 health or less the exploration fails.
        private static void Trap(ExplorationEventDefinition definition) =>
            definition.ConfigureForEditor("event.test.trap", "무너지는 천장", "머리 위에서 돌가루가 떨어진다.", "start", new[]
            {
                Page("start", string.Empty, "천장이 무너져 돌덩이가 쏟아졌다!", Continue(End), new ChangeHealthEffect(-6))
            });
    }
}
