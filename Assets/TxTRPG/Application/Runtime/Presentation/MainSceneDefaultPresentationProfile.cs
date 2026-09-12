using System;
using System.Collections.Generic;
using TxTRPG.UI;
using UnityEngine;

namespace TxTRPG.Application.Presentation
{
    public enum PresentationDataState
    {
        Loading,
        ReadyWithData,
        ReadyEmpty,
        MissingConfiguration,
        Failed
    }

    [CreateAssetMenu(menuName = "TxT RPG/Application/Main Scene Default Presentation Profile", fileName = "MainSceneDefaultPresentationProfile")]
    public sealed class MainSceneDefaultPresentationProfile : ScriptableObject
    {
        [Header("Deployment fallbacks")]
        [SerializeField, TextArea(2, 4)] private string storyMissingConfigurationText = "The story is not available yet.";
        [SerializeField, TextArea(2, 4)] private string storyLoadFailedText = "The story could not be loaded.";
        [SerializeField] private string unknownHealthLabel = "HP";
        [SerializeField] private string unknownHealthValue = "Unknown";
        [SerializeField] private PanelBackgroundStyle storyBackgroundStyle;
        [SerializeField] private PanelBackgroundStyle enemyBackgroundStyle;

        public string StoryMissingConfigurationText => storyMissingConfigurationText ?? string.Empty;
        public string StoryLoadFailedText => storyLoadFailedText ?? string.Empty;
        public string UnknownHealthLabel => unknownHealthLabel ?? string.Empty;
        public string UnknownHealthValue => unknownHealthValue ?? string.Empty;
        public PanelBackgroundStyle StoryBackgroundStyle => storyBackgroundStyle;
        public PanelBackgroundStyle EnemyBackgroundStyle => enemyBackgroundStyle;

#if UNITY_EDITOR
        public void ConfigureForEditor(string missingStory, string failedStory, string healthLabel, string healthValue,
            PanelBackgroundStyle storyStyle, PanelBackgroundStyle enemyStyle)
        {
            storyMissingConfigurationText = missingStory ?? string.Empty;
            storyLoadFailedText = failedStory ?? string.Empty;
            unknownHealthLabel = healthLabel ?? string.Empty;
            unknownHealthValue = healthValue ?? string.Empty;
            storyBackgroundStyle = storyStyle;
            enemyBackgroundStyle = enemyStyle;
        }
#endif
    }

    public readonly struct StoryPresentationResult
    {
        public StoryPresentationResult(PresentationDataState state, IReadOnlyList<StoryMessage> messages = null, string errorCode = "")
        { State = state; Messages = messages ?? Array.Empty<StoryMessage>(); ErrorCode = errorCode ?? string.Empty; }
        public PresentationDataState State { get; }
        public IReadOnlyList<StoryMessage> Messages { get; }
        public string ErrorCode { get; }
    }
}
