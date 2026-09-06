using System;
using System.Threading;
using System.Threading.Tasks;
using TxTRPG.Content.Characters;
using TxTRPG.Gameplay.Characters;
using TxTRPG.Gameplay.Players;
using TxTRPG.SceneTransition;
using TxTRPG.UI;
using UnityEngine;
using UnityEngine.UI;

namespace TxTRPG.Application.Players
{
    [DisallowMultipleComponent]
    public sealed class ActiveCharacterDisplayBinder : MonoBehaviour,
        ISceneInitializer,
        ISceneReadySource
    {
        [SerializeField] private CharacterDisplayPresenter displayPresenter;
        [SerializeField] private CharacterContentCatalog characterCatalog;

        private readonly TaskCompletionSource<bool> readiness =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private IPlayerSession session;
        private PlayerState player;

        public int InitializationOrder => -1000;
        public Task WhenReady => readiness.Task;

        public async Task InitializeAsync(
            SceneInitializationContext context,
            CancellationToken cancellationToken)
        {
            try
            {
                var host = PlayerSessionHost.Instance;
                if (host == null)
                {
                    throw new InvalidOperationException(
                        "ActiveCharacterDisplayBinder requires PlayerSessionHost in AppScene.");
                }

                await host.EnsureInitializedAsync(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                Initialize(host.Session);
                await displayPresenter.WhenAssetsReady;
                cancellationToken.ThrowIfCancellationRequested();
                Canvas.ForceUpdateCanvases();
                if (transform is RectTransform rectTransform)
                {
                    LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
                }
                readiness.TrySetResult(true);
            }
            catch (OperationCanceledException)
            {
                readiness.TrySetCanceled();
                throw;
            }
            catch (Exception exception)
            {
                readiness.TrySetException(exception);
                throw;
            }
        }

        public void Initialize(IPlayerSession playerSession)
        {
            session = playerSession ??
                throw new ArgumentNullException(nameof(playerSession));
            if (!session.IsReady)
            {
                throw new InvalidOperationException(
                    "The player session must be ready before the display is bound.");
            }
            BindPlayer(session.CurrentPlayer);
        }

        public void Configure(
            CharacterDisplayPresenter presenter,
            CharacterContentCatalog catalog)
        {
            displayPresenter = presenter;
            characterCatalog = catalog;
        }

        private void BindPlayer(PlayerState nextPlayer)
        {
            Unsubscribe();
            player = nextPlayer ??
                throw new ArgumentNullException(nameof(nextPlayer));
            player.ActiveCharacterChanged += OnActiveCharacterChanged;
            BindCharacter(player.ActiveCharacter);
        }

        private void BindCharacter(CharacterRuntimeState nextCharacter)
        {
            if (nextCharacter == null)
            {
                throw new ArgumentNullException(nameof(nextCharacter));
            }
            if (characterCatalog == null)
            {
                throw new InvalidOperationException(
                    "ActiveCharacterDisplayBinder requires a character catalog.");
            }
            if (displayPresenter == null)
            {
                throw new InvalidOperationException(
                    "ActiveCharacterDisplayBinder requires a display presenter.");
            }

            var content = characterCatalog.GetRequired(
                nextCharacter.CharacterDefinitionId);
            displayPresenter.Bind(nextCharacter, content);
        }

        private void OnActiveCharacterChanged(
            CharacterRuntimeState previous,
            CharacterRuntimeState next)
        {
            BindCharacter(next);
        }

        private void OnEnable()
        {
            if (session != null && session.IsReady && player == null)
            {
                BindPlayer(session.CurrentPlayer);
            }
        }

        private void OnDisable()
        {
            Unsubscribe();
            displayPresenter?.Unbind();
        }

        private void Unsubscribe()
        {
            if (player != null)
            {
                player.ActiveCharacterChanged -= OnActiveCharacterChanged;
                player = null;
            }
        }
    }

}
