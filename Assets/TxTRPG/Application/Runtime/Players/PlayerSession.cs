using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TxTRPG.Application.Configuration;
using TxTRPG.Application.Persistence;
using TxTRPG.Gameplay.Characters;
using TxTRPG.Gameplay.Players;

namespace TxTRPG.Application.Players
{
    public interface ICharacterInstanceIdGenerator
    {
        string Create();
    }

    public sealed class GuidCharacterInstanceIdGenerator :
        ICharacterInstanceIdGenerator
    {
        public string Create() => Guid.NewGuid().ToString("N");
    }

    public interface IPlayerSession
    {
        bool IsReady { get; }
        PlayerState CurrentPlayer { get; }
        Task WhenReady { get; }

        event Action<PlayerState> Ready;

        Task InitializeAsync(CancellationToken cancellationToken);
        Task SaveAsync(CancellationToken cancellationToken);
    }

    public sealed class PlayerSession : IPlayerSession
    {
        private readonly object initializationGate = new();
        private readonly NewGameProfile newGameProfile;
        private readonly IPlayerSaveRepository saveRepository;
        private readonly ICharacterInstanceIdGenerator instanceIdGenerator;
        private Task initializationTask;
        private PlayerState currentPlayer;

        public PlayerSession(
            NewGameProfile profile,
            IPlayerSaveRepository repository,
            ICharacterInstanceIdGenerator idGenerator)
        {
            newGameProfile = profile ??
                throw new ArgumentNullException(nameof(profile));
            saveRepository = repository ??
                throw new ArgumentNullException(nameof(repository));
            instanceIdGenerator = idGenerator ??
                throw new ArgumentNullException(nameof(idGenerator));
        }

        public bool IsReady { get; private set; }
        public PlayerState CurrentPlayer => IsReady
            ? currentPlayer
            : throw new InvalidOperationException("Player session is not ready.");
        public Task WhenReady => InitializeAsync(CancellationToken.None);

        public event Action<PlayerState> Ready;

        public Task InitializeAsync(CancellationToken cancellationToken)
        {
            lock (initializationGate)
            {
                if (IsReady)
                {
                    return Task.CompletedTask;
                }
                if (initializationTask == null ||
                    initializationTask.IsCanceled ||
                    initializationTask.IsFaulted)
                {
                    initializationTask = InitializeCoreAsync(cancellationToken);
                }
                return initializationTask;
            }
        }

        public async Task SaveAsync(CancellationToken cancellationToken)
        {
            if (!IsReady)
            {
                throw new InvalidOperationException(
                    "Player session must be initialized before it can be saved.");
            }

            var snapshot = CurrentPlayer.CreateSaveData();
            await saveRepository.SaveAsync(snapshot, cancellationToken);
        }

        private async Task InitializeCoreAsync(CancellationToken cancellationToken)
        {
            newGameProfile.ValidateOrThrow();
            cancellationToken.ThrowIfCancellationRequested();

            var gameplayDefinitions = new List<CharacterDefinition>();
            foreach (var content in newGameProfile.CharacterCatalog.Definitions)
            {
                gameplayDefinitions.Add(content.GameplayDefinition);
            }

            var playerFactory = new PlayerFactory(
                new CharacterFactory(gameplayDefinitions));
            var saveData = await saveRepository.TryLoadAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            var player = saveData != null
                ? playerFactory.Restore(saveData)
                : playerFactory.CreateNew(
                    newGameProfile.InitialCharacter.DefinitionId,
                    CreateInstanceId());

            currentPlayer = player;
            IsReady = true;
            Ready?.Invoke(player);
        }

        private string CreateInstanceId()
        {
            var instanceId = instanceIdGenerator.Create()?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(instanceId))
            {
                throw new InvalidOperationException(
                    "The character instance ID generator returned an empty ID.");
            }
            return instanceId;
        }
    }
}
