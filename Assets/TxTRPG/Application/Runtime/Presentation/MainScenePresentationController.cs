using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TxTRPG.SceneTransition;
using TxTRPG.UI;

using UnityEngine;

namespace TxTRPG.Application.Presentation
{
    [DisallowMultipleComponent]
    public sealed class MainScenePresentationController : MonoBehaviour, ISceneInitializer
    {
        [SerializeField] private MainSceneDefaultPresentationProfile defaults;
        [SerializeField] private StoryTextPanel storyPanel;
        [SerializeField] private ActionGridPanel actionGridPanel;
        [SerializeField] private EnemyDisplayPanel enemyPanel;
        [SerializeField] private HealthBarPanel healthBarPanel;
        [Header("Explicit preview only")]
        [SerializeField] private bool previewMode;
        [SerializeField] private MainScenePreviewProfile previewProfile;

        private bool ownsStoryFallback;
        private int requestVersion;
        public int InitializationOrder => -850;
        public PresentationDataState StoryState { get; private set; } = PresentationDataState.Loading;
        public PresentationDataState EnemyState { get; private set; } = PresentationDataState.Loading;
        public bool IsPreviewMode => previewMode;

        public async Task InitializeAsync(SceneInitializationContext context, CancellationToken cancellationToken)
        {
            ValidateReferences();
            ApplyBackgroundFallbacks();
            actionGridPanel.InitializeVisibleSlots();
            enemyPanel.Clear();
            EnemyState = PresentationDataState.ReadyEmpty;

            if (previewMode)
            {
                await ApplyPreviewAsync(cancellationToken);
                return;
            }

            ApplyStory(new StoryPresentationResult(PresentationDataState.MissingConfiguration));
            ApplyUnknownHealth();
            await WaitForVisualAssetsAsync(cancellationToken);
        }

        public void ApplyStory(in StoryPresentationResult result)
        {
            var version = ++requestVersion;
            StoryState = result.State;
            switch (result.State)
            {
                case PresentationDataState.Loading:
                    return;
                case PresentationDataState.ReadyWithData:
                    ReplaceStory(result.Messages, version);
                    break;
                case PresentationDataState.ReadyEmpty:
                    ClearOwnedStoryFallback();
                    break;
                case PresentationDataState.MissingConfiguration:
                    ShowStoryFallback(defaults.StoryMissingConfigurationText, version);
                    break;
                case PresentationDataState.Failed:
                    ShowStoryFallback(defaults.StoryLoadFailedText, version);
                    LogFailureOnce("StoryTextPanel", result.ErrorCode);
                    break;
            }
        }

        public void ApplyEnemies(PresentationDataState state, IReadOnlyList<EnemyPresentation> enemies = null, string errorCode = "")
        {
            EnemyState = state;
            if (state == PresentationDataState.ReadyWithData) enemyPanel.SetEnemies(enemies);
            else if (state == PresentationDataState.ReadyEmpty || state == PresentationDataState.MissingConfiguration) enemyPanel.Clear();
            else if (state == PresentationDataState.Failed) LogFailureOnce("EnemyDisplayPanel", errorCode);
        }

        public void Configure(MainSceneDefaultPresentationProfile profile, StoryTextPanel story,
            ActionGridPanel grid, EnemyDisplayPanel enemies, HealthBarPanel health = null,
            bool enablePreview = false, MainScenePreviewProfile preview = null)
        { defaults = profile; storyPanel = story; actionGridPanel = grid; enemyPanel = enemies; healthBarPanel = health; previewMode = enablePreview; previewProfile = preview; }

        public async Task RunPreviewAsync(MainScenePreviewProfile profile, CancellationToken cancellationToken = default)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            var previous = previewProfile;
            previewProfile = profile;
            try { await ApplyPreviewAsync(cancellationToken); }
            finally { previewProfile = previous; }
        }
        private async Task ApplyPreviewAsync(CancellationToken cancellationToken)
        {
            if (previewProfile == null) throw new InvalidOperationException("Preview mode requires a preview profile.");
            if (previewProfile.SimulatedDelay > 0f)
                await Task.Delay(TimeSpan.FromSeconds(previewProfile.SimulatedDelay), cancellationToken);
            switch (previewProfile.Scenario)
            {
                case MainScenePreviewScenario.ReadyEmpty:
                    ApplyStory(new StoryPresentationResult(PresentationDataState.ReadyEmpty));
                    ApplyEnemies(PresentationDataState.ReadyEmpty);
                    break;
                case MainScenePreviewScenario.MissingConfiguration:
                    ApplyStory(new StoryPresentationResult(PresentationDataState.MissingConfiguration));
                    ApplyEnemies(PresentationDataState.MissingConfiguration);
                    break;
                case MainScenePreviewScenario.InitializationFailure:
                    ApplyStory(new StoryPresentationResult(PresentationDataState.Failed, errorCode: "preview-initialization-failure"));
                    ApplyEnemies(PresentationDataState.Failed, errorCode: "preview-initialization-failure");
                    break;
                default:
                    var messages = new List<StoryMessage>();
                    if (previewProfile.StoryData != null)
                        foreach (var entry in previewProfile.StoryData.Entries) messages.Add(entry.ToMessage());
                    ApplyStory(new StoryPresentationResult(messages.Count > 0 ? PresentationDataState.ReadyWithData : PresentationDataState.ReadyEmpty, messages));
                    ApplyEnemies(previewProfile.EnemyData != null && previewProfile.EnemyData.EnemyCount > 0 ? PresentationDataState.ReadyWithData : PresentationDataState.ReadyEmpty,
                        previewProfile.EnemyData?.CreatePresentations());
                    break;
            }
            await WaitForVisualAssetsAsync(cancellationToken);
        }

        private void ReplaceStory(IReadOnlyList<StoryMessage> messages, int version)
        {
            if (version != requestVersion) return;
            storyPanel.Clear(); ownsStoryFallback = false;
            foreach (var message in messages) storyPanel.AddMessage(message);
        }
        private void ShowStoryFallback(string text, int version)
        {
            if (version != requestVersion || string.IsNullOrWhiteSpace(text)) return;
            if (ownsStoryFallback && storyPanel.MessageCount == 1) return;
            storyPanel.Clear(); storyPanel.AddMessage(text); ownsStoryFallback = true;
        }
        private void ClearOwnedStoryFallback()
        {
            if (!ownsStoryFallback) return;
            storyPanel.Clear(); ownsStoryFallback = false;
        }
        private void ApplyUnknownHealth()
        {
            if (healthBarPanel == null) return;
            healthBarPanel.Apply(new CharacterStatusPresentation(default,
                new HealthPresentation(0, 1, defaults.UnknownHealthLabel, defaults.UnknownHealthValue)));
        }
        private void ApplyBackgroundFallbacks()
        {
            if (defaults.StoryBackgroundStyle != null) storyPanel.ApplyBackground(defaults.StoryBackgroundStyle);
            if (defaults.EnemyBackgroundStyle != null) enemyPanel.ApplyBackground(defaults.EnemyBackgroundStyle);
        }
        private async Task WaitForVisualAssetsAsync(CancellationToken token)
        {
            await Task.WhenAll(storyPanel.WhenBackgroundReady, actionGridPanel.WhenAssetsReady, enemyPanel.WhenAssetsReady);
            token.ThrowIfCancellationRequested();
        }
        private void ValidateReferences()
        {
            if (defaults == null || storyPanel == null || actionGridPanel == null || enemyPanel == null)
                throw new InvalidOperationException("MainScenePresentationController references are incomplete.");
        }
        private readonly HashSet<string> loggedFailures = new(StringComparer.Ordinal);
        private void LogFailureOnce(string panel, string errorCode)
        {
            var key = panel + ":" + errorCode;
            if (loggedFailures.Add(key)) Debug.LogWarning($"{panel} presentation failed ({errorCode}). A safe fallback remains visible.", this);
        }
    }
}
