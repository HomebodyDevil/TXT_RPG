using System;
using System.Threading.Tasks;
using TxTRPG.Gameplay.Characters;
using TxTRPG.UI;
using UnityEngine;

namespace TxTRPG.Content.Characters
{
    public sealed class CharacterDisplayPresenter : MonoBehaviour
    {
        [SerializeField] private CharacterDisplayPanel target;

        private readonly CharacterPresentationFactory presentationFactory = new();
        private CharacterRuntimeState state;
        private CharacterContentDefinition content;
        private string temporaryVisualStateId = string.Empty;
        private string appearanceId = string.Empty;
        private string poseId = string.Empty;
        private string expressionId = string.Empty;
        private string animationId = string.Empty;
        private bool mirrored;
        private string lastVisualStateId;
        private bool subscribed;
        private bool hasPresented;

        public string CurrentVisualStateId => lastVisualStateId ?? string.Empty;
        public Task WhenAssetsReady => target != null
            ? target.WhenAssetsReady
            : Task.CompletedTask;

        public void SetTarget(CharacterDisplayPanel configuredTarget)
        {
            if (target == configuredTarget)
            {
                return;
            }
            Unbind();
            target = configuredTarget;
        }

        public void Bind(
            CharacterRuntimeState runtimeState,
            CharacterContentDefinition contentDefinition)
        {
            if (runtimeState == null)
            {
                throw new ArgumentNullException(nameof(runtimeState));
            }
            if (contentDefinition == null)
            {
                throw new ArgumentNullException(nameof(contentDefinition));
            }
            if (!string.Equals(
                    runtimeState.CharacterDefinitionId,
                    contentDefinition.DefinitionId,
                    StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Runtime state and content definition IDs must match.",
                    nameof(contentDefinition));
            }

            Unsubscribe();
            state = runtimeState;
            content = contentDefinition;
            target?.SetAppearanceDefinitions(
                new[] { contentDefinition.AppearanceDefinition });
            lastVisualStateId = null;
            hasPresented = false;
            Subscribe();
            Refresh(true);
        }

        public void SetPresentationOptions(
            string configuredAppearanceId = "",
            string configuredPoseId = "",
            string configuredExpressionId = "",
            string configuredAnimationId = "",
            bool shouldMirror = false)
        {
            appearanceId = configuredAppearanceId?.Trim() ?? string.Empty;
            poseId = configuredPoseId?.Trim() ?? string.Empty;
            expressionId = configuredExpressionId?.Trim() ?? string.Empty;
            animationId = configuredAnimationId?.Trim() ?? string.Empty;
            mirrored = shouldMirror;
            Refresh(true);
        }

        public void SetTemporaryVisualState(string visualStateId)
        {
            temporaryVisualStateId = visualStateId?.Trim() ?? string.Empty;
            Refresh(false);
        }

        public void ClearTemporaryVisualState()
        {
            SetTemporaryVisualState(string.Empty);
        }

        public void Unbind()
        {
            Unsubscribe();
            state = null;
            content = null;
            lastVisualStateId = null;
            hasPresented = false;
        }

        private void OnEnable()
        {
            Subscribe();
            Refresh(false);
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void OnDestroy()
        {
            Unbind();
        }

        private void Subscribe()
        {
            if (subscribed || !isActiveAndEnabled || state == null)
            {
                return;
            }
            state.Health.Changed += OnHealthChanged;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed || state == null)
            {
                subscribed = false;
                return;
            }
            state.Health.Changed -= OnHealthChanged;
            subscribed = false;
        }

        private void OnHealthChanged(HealthChangeResult _)
        {
            Refresh(false);
        }

        private void Refresh(bool force)
        {
            if (target == null || state == null || content == null)
            {
                return;
            }

            var presentation = presentationFactory.Create(
                state,
                content.VisualStatePolicy,
                temporaryVisualStateId,
                appearanceId,
                poseId,
                expressionId,
                animationId,
                mirrored);
            if (!force && hasPresented &&
                string.Equals(
                    lastVisualStateId,
                    presentation.VisualStateId,
                    StringComparison.Ordinal))
            {
                return;
            }

            lastVisualStateId = presentation.VisualStateId;
            if (hasPresented)
            {
                target.UpdateCharacter(presentation);
            }
            else
            {
                target.ShowCharacterImmediately(presentation);
                hasPresented = true;
            }
        }
    }
}
