using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using TxTRPG.Content.Characters;
using TxTRPG.Gameplay.Characters;
using TxTRPG.Gameplay.Players;
using TxTRPG.SceneTransition;
using TxTRPG.UI;
using UnityEngine;

namespace TxTRPG.Application.Players
{
    [DisallowMultipleComponent]
    public sealed class ActiveCharacterStatusBinder : MonoBehaviour,
        ISceneInitializer
    {
        [SerializeField] private CharacterStatusPanel statusPanel;
        [SerializeField] private CharacterContentCatalog characterCatalog;

        private IPlayerSession session;
        private PlayerState player;
        private CharacterRuntimeState character;
        private ICharacterNameLocalizer nameLocalizer;
        private IHealthTextFormatter healthTextFormatter;

        public int InitializationOrder => -900;

        public async Task InitializeAsync(
            SceneInitializationContext context,
            CancellationToken cancellationToken)
        {
            if (statusPanel == null)
            {
                return;
            }

            var host = PlayerSessionHost.Instance;
            if (host == null)
            {
                throw new InvalidOperationException(
                    "ActiveCharacterStatusBinder requires PlayerSessionHost in AppScene.");
            }

            await host.EnsureInitializedAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            Initialize(host.Session);
        }

        public void Initialize(IPlayerSession playerSession)
        {
            if (statusPanel == null)
            {
                return;
            }

            session = playerSession ??
                throw new ArgumentNullException(nameof(playerSession));
            if (!session.IsReady)
            {
                throw new InvalidOperationException(
                    "The player session must be ready before status UI is bound.");
            }
            BindPlayer(session.CurrentPlayer);
        }

        public void Configure(
            CharacterStatusPanel configuredStatusPanel,
            CharacterContentCatalog catalog)
        {
            statusPanel = configuredStatusPanel;
            characterCatalog = catalog;
        }

        public void SetNameLocalizer(ICharacterNameLocalizer localizer)
        {
            nameLocalizer = localizer;
            RefreshStatus();
        }

        public void SetHealthTextFormatter(IHealthTextFormatter formatter)
        {
            healthTextFormatter = formatter;
            RefreshStatus();
        }

        private void BindPlayer(PlayerState nextPlayer)
        {
            Unsubscribe();
            player = nextPlayer ?? throw new ArgumentNullException(nameof(nextPlayer));
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
                    "ActiveCharacterStatusBinder requires a character catalog.");
            }

            if (character != null)
            {
                character.Health.Changed -= OnHealthChanged;
            }
            character = nextCharacter;
            character.Health.Changed += OnHealthChanged;
            RefreshStatus();
        }

        private void OnActiveCharacterChanged(
            CharacterRuntimeState previous,
            CharacterRuntimeState next)
        {
            BindCharacter(next);
        }

        private void OnHealthChanged(HealthChangeResult result)
        {
            RefreshStatus();
        }

        private void RefreshStatus()
        {
            if (statusPanel == null || character == null || characterCatalog == null)
            {
                return;
            }

            var content = characterCatalog.GetRequired(character.CharacterDefinitionId);
            var displayName = string.Empty;
            if (nameLocalizer != null)
            {
                nameLocalizer.TryLocalize(
                    content.DisplayNameLocalizationKey,
                    out displayName);
            }

            var current = character.Health.Current;
            var maximum = character.Health.Maximum;
            var label = healthTextFormatter?.HealthLabel ?? string.Empty;
            var valueText = healthTextFormatter?.FormatValue(current, maximum) ??
                string.Format(
                    CultureInfo.CurrentCulture,
                    "{0:N0} / {1:N0}",
                    current,
                    maximum);
            var name = new CharacterNamePresentation(displayName);
            var health = new HealthPresentation(
                current,
                maximum,
                label,
                valueText);
            statusPanel.Apply(new CharacterStatusPresentation(name, health));
        }

        private void OnEnable()
        {
            if (statusPanel != null && session != null && session.IsReady && player == null)
            {
                BindPlayer(session.CurrentPlayer);
            }
        }

        private void OnDisable()
        {
            Unsubscribe();
            statusPanel?.Clear();
        }

        private void Unsubscribe()
        {
            if (character != null)
            {
                character.Health.Changed -= OnHealthChanged;
                character = null;
            }
            if (player != null)
            {
                player.ActiveCharacterChanged -= OnActiveCharacterChanged;
                player = null;
            }
        }
    }

    public interface ICharacterNameLocalizer
    {
        bool TryLocalize(string localizationKey, out string localizedValue);
    }

    public interface IHealthTextFormatter
    {
        string HealthLabel { get; }
        string FormatValue(int current, int maximum);
    }
}
