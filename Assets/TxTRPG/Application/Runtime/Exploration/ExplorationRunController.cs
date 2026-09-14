using System;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using TxTRPG.Application.Dice;
using TxTRPG.Application.Players;
using TxTRPG.Gameplay.Characters;
using TxTRPG.Gameplay.Exploration;
using TxTRPG.SceneTransition;
using TxTRPG.UI;
using TxTRPG.UI.Exploration;
using UnityEngine.Events;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TxTRPG.Application.Exploration
{
    [DisallowMultipleComponent]
    public sealed class ExplorationRunController : MonoBehaviour, ISceneInitializer
    {
        [SerializeField] private ExplorationRunConfiguration configuration;
        [SerializeField] private TemporaryDiceRollMenuController combatController;
        [SerializeField] private StoryTextPanel storyPanel;
        [SerializeField] private GameObject panel;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private ExplorationNodeChoiceCardList choiceCardList;
        [SerializeField] private Button[] choiceButtons = Array.Empty<Button>();
        [SerializeField] private TMP_Text[] choiceLabels = Array.Empty<TMP_Text>();
        [SerializeField] private Button continueButton;

        private ExplorationRunState run;
        private bool initialized;
        private bool processing;
        private UnityAction[] legacyChoiceListeners = Array.Empty<UnityAction>();

        public int InitializationOrder => -790;
        public ExplorationRunState Run => run;

        public async Task InitializeAsync(SceneInitializationContext context, CancellationToken cancellationToken)
        {
            initialized = false; Unbind();
            var host = PlayerSessionHost.Instance ?? throw new InvalidOperationException("Exploration requires PlayerSessionHost.");
            await host.EnsureInitializedAsync(cancellationToken); cancellationToken.ThrowIfCancellationRequested();
            if (configuration == null || combatController == null || storyPanel == null) throw new InvalidOperationException("Exploration references are incomplete.");
            if (choiceCardList == null) EnsureChoiceCapacity(configuration.ChoiceCount);
            var created = false;
            run = host.GetOrCreateTemporaryExploration(() =>
            {
                created = true;
                var source = host.Session.CurrentPlayer.ActiveCharacter.Health;
                var explorationHealth = new HealthState(source.Maximum, source.Current);
                return new ExplorationRunState(Guid.NewGuid().ToString("N"), explorationHealth,
                    new ExplorationNodeGenerator(configuration.ChoiceCount, configuration.CreateWeights(), new SystemExplorationRandomSource()));
            });
            combatController.CombatFinished += OnCombatFinished;
            BindLegacyChoices();
            continueButton.onClick.AddListener(ContinuePlaceholder);
            initialized = true;
            if (created) storyPanel.AddMessage("[탐험] 새로운 탐험을 시작했습니다. 다음 노드를 선택하세요.");
            RefreshRunPresentation();
        }

        private void EnsureChoiceCapacity(int count)
        {
            if (count < 1) throw new InvalidOperationException("Exploration Choice Count must be positive.");
            if (choiceButtons.Length == 0 || choiceLabels.Length == 0 || choiceButtons[0] == null) throw new InvalidOperationException("Exploration requires a choice button template.");
            if (choiceButtons.Length >= count && choiceLabels.Length >= count) return;
            var buttons = new Button[count]; var labels = new TMP_Text[count];
            Array.Copy(choiceButtons, buttons, choiceButtons.Length); Array.Copy(choiceLabels, labels, choiceLabels.Length);
            for (var i = choiceButtons.Length; i < count; i++)
            {
                buttons[i] = Instantiate(choiceButtons[0], choiceButtons[0].transform.parent);
                buttons[i].name = $"Choice{i + 1}";
                labels[i] = buttons[i].GetComponentInChildren<TMP_Text>(true);
            }
            choiceButtons = buttons; choiceLabels = labels;
        }
        private void SelectChoice(ExplorationNodeChoiceRequest request)
        {
            if (!initialized || run == null || !string.Equals(request.RunId, run.RunId, StringComparison.Ordinal)) return;
            SelectChoice(request.ChoiceSetId, request.NodeId);
        }

        private void SelectChoice(int index)
        {
            if (!initialized || processing || run.Phase != ExplorationPhase.AwaitingChoice) return;
            var choices = run.CurrentChoices;
            if (index < 0 || index >= choices.Count) return;
            SelectChoice(run.CurrentChoiceSetId, choices[index].Id);
        }

        private void SelectChoice(string choiceSetId, string nodeId)
        {
            if (!initialized || processing || run.Phase != ExplorationPhase.AwaitingChoice) return;
            processing = true;
            try
            {
                SetChoicesInteractable(false);
                var node = run.Select(choiceSetId, nodeId);
                storyPanel.AddMessage($"[탐험] {DisplayName(node.TypeId)} 노드를 선택했습니다. ({node.Id})");
                if (node.TypeId == ExplorationNodeTypeIds.Combat)
                {
                    panel.SetActive(false);
                    combatController.BeginCombat(run.PlayerHealth);
                }
                else if (node.TypeId == ExplorationNodeTypeIds.RecoveryUpgrade)
                {
                    ShowPlaceholder();
                }
                else
                {
                    run.FailActive(node.Id, "HandlerNotRegistered");
                    statusText.text = $"처리기가 등록되지 않은 노드입니다: {node.TypeId}";
                    storyPanel.AddMessage($"[탐험] 노드 처리기를 찾지 못해 탐험이 중단되었습니다: {node.TypeId}");
                }
            }
            catch (Exception exception) { Debug.LogError($"Exploration choice failed: {exception.Message}", this); if (run.Phase == ExplorationPhase.AwaitingChoice) SetChoicesInteractable(true); }
            finally { processing = false; }
        }

        private void ContinuePlaceholder()
        {
            if (!initialized || processing || run.Phase != ExplorationPhase.ResolvingNode) return;
            var nodeId = run.ActiveNodeId;
            var node = FindNode(nodeId);
            if (node == null || node.TypeId != ExplorationNodeTypeIds.RecoveryUpgrade) return;
            processing = true;
            try
            {
                run.CompleteActive(nodeId, "PlaceholderAcknowledged");
                storyPanel.AddMessage("[탐험] 회복 및 강화 노드를 효과 없이 완료했습니다.");
                ShowChoices();
            }
            finally { processing = false; }
        }

        private void OnCombatFinished(bool victory)
        {
            if (!initialized || run.Phase != ExplorationPhase.ResolvingNode) return;
            var node = FindNode(run.ActiveNodeId);
            if (node == null || node.TypeId != ExplorationNodeTypeIds.Combat) return;
            if (victory)
            {
                run.CompleteActive(node.Id, "CombatVictory");
                storyPanel.AddMessage("[탐험] 전투 노드를 완료했습니다. 다음 노드를 선택하세요.");
                combatController.ClearCombat(); ShowChoices();
            }
            else
            {
                run.FailActive(node.Id, "CombatDefeat");
                storyPanel.AddMessage("[탐험] 전투 패배로 탐험이 종료되었습니다.");
                panel.SetActive(true); statusText.text = "탐험 종료: 패배"; SetChoiceButtons(false); continueButton.gameObject.SetActive(false);
            }
        }

        private void RefreshRunPresentation()
        {
            if (run.Phase == ExplorationPhase.AwaitingChoice) { ShowChoices(); return; }
            if (run.Phase == ExplorationPhase.Failed)
            {
                panel.SetActive(true); statusText.text = "탐험 종료: 패배"; SetChoiceButtons(false); continueButton.gameObject.SetActive(false); return;
            }
            var active = FindNode(run.ActiveNodeId);
            if (active != null && active.TypeId == ExplorationNodeTypeIds.RecoveryUpgrade) ShowPlaceholder();
            else panel.SetActive(false);
        }
        private void ShowChoices()
        {
            panel.SetActive(true); statusText.text = $"다음 노드 선택 · 현재 체력 {run.PlayerHealth.Current}/{run.PlayerHealth.Maximum}"; continueButton.gameObject.SetActive(false);
            var choices = run.CurrentChoices;
            for (var i = 0; i < choiceButtons.Length; i++)
            {
                var visible = i < choices.Count; choiceButtons[i].gameObject.SetActive(visible); choiceButtons[i].interactable = visible;
                if (visible) choiceLabels[i].text = $"{i + 1}. {DisplayName(choices[i].TypeId)}";
            }
            if (choiceCardList != null)
            {
                var data = new ExplorationNodeChoiceCardData[choices.Count];
                for (var i = 0; i < choices.Count; i++) data[i] = CreateCardData(choices[i]);
                choiceCardList.gameObject.SetActive(true); choiceCardList.Show(data, SelectChoice); SetLegacyChoicesVisible(false);
            }
            else if (choices.Count > 0 && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(choiceButtons[0].gameObject);
        }

        private ExplorationNodeChoiceCardData CreateCardData(ExplorationNodeRecord node)
        {
            var request = new ExplorationNodeChoiceRequest(run.RunId, run.CurrentChoiceSetId, node.Id);
            if (node.TypeId == ExplorationNodeTypeIds.Combat) return new ExplorationNodeChoiceCardData(request, "전투", "적과 전투를 시작합니다.", string.Empty, null, true);
            if (node.TypeId == ExplorationNodeTypeIds.RecoveryUpgrade) return new ExplorationNodeChoiceCardData(request, "회복 및 강화", "향후 회복 또는 강화 효과가 제공됩니다.", "미구현", null, true);
            return new ExplorationNodeChoiceCardData(request, node.TypeId, "처리기가 등록되지 않은 노드입니다.", "지원 안 함", null, true);
        }

        private void ShowPlaceholder()
        {
            panel.SetActive(true); statusText.text = "회복 및 강화: 미구현. 이번에는 효과가 적용되지 않습니다."; SetChoiceButtons(false); continueButton.gameObject.SetActive(true); continueButton.interactable = true;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(continueButton.gameObject);
        }

        private void SetChoiceButtons(bool value) { if (choiceCardList != null) { if (!value) choiceCardList.Hide(); choiceCardList.gameObject.SetActive(value); } SetLegacyChoicesVisible(value && choiceCardList == null); }
        private void SetLegacyChoicesVisible(bool value) { foreach (var button in choiceButtons) if (button != null) button.gameObject.SetActive(value); }
        private void SetChoicesInteractable(bool value) { if (choiceCardList != null) foreach (var card in choiceCardList.Cards) if (card != null) card.Button.interactable = value; foreach (var button in choiceButtons) if (button != null) button.interactable = value; }
        private ExplorationNodeRecord FindNode(string id) { foreach (var node in run.Nodes) if (node.Id == id) return node; return null; }
        private static string DisplayName(string typeId) => typeId == ExplorationNodeTypeIds.Combat ? "전투" : typeId == ExplorationNodeTypeIds.RecoveryUpgrade ? "회복 및 강화 (미구현)" : typeId;

        private void Unbind()
        {
            if (combatController != null) combatController.CombatFinished -= OnCombatFinished;
            for (var i = 0; i < choiceButtons.Length && i < legacyChoiceListeners.Length; i++) if (choiceButtons[i] != null && legacyChoiceListeners[i] != null) choiceButtons[i].onClick.RemoveListener(legacyChoiceListeners[i]);
            legacyChoiceListeners = Array.Empty<UnityAction>();
            if (choiceCardList != null) choiceCardList.Hide();
            if (continueButton != null) continueButton.onClick.RemoveListener(ContinuePlaceholder);
        }
        private void BindLegacyChoices()
        {
            if (choiceCardList != null) return;
            legacyChoiceListeners = new UnityAction[choiceButtons.Length];
            for (var i = 0; i < choiceButtons.Length; i++) { var index = i; legacyChoiceListeners[i] = () => SelectChoice(index); choiceButtons[i].onClick.AddListener(legacyChoiceListeners[i]); }
        }
        private void OnDestroy() => Unbind();

#if UNITY_EDITOR
        public void ConfigureForEditor(ExplorationRunConfiguration targetConfiguration, TemporaryDiceRollMenuController targetCombat, StoryTextPanel targetStory, GameObject targetPanel, TMP_Text targetStatus, Button[] buttons, TMP_Text[] labels, Button targetContinue)
        { configuration = targetConfiguration; combatController = targetCombat; storyPanel = targetStory; panel = targetPanel; statusText = targetStatus; choiceButtons = buttons ?? Array.Empty<Button>(); choiceLabels = labels ?? Array.Empty<TMP_Text>(); continueButton = targetContinue; }
        public void ConfigureCardsForEditor(ExplorationNodeChoiceCardList targetCardList) => choiceCardList = targetCardList;
#endif

        private sealed class SystemExplorationRandomSource : IExplorationRandomSource
        {
            private readonly System.Random random = new();
            public int Next(int exclusiveMaximum) => random.Next(exclusiveMaximum);
        }
    }
}
