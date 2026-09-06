using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using TxTRPG.Application.Configuration;
using TxTRPG.Application.Editor;
using TxTRPG.Application.Persistence;
using TxTRPG.Application.Players;
using TxTRPG.Gameplay.Players;
using UnityEditor;
using UnityEngine;

namespace TxTRPG.Application.Tests
{
    public sealed class PlayerSessionTests
    {
        private NewGameProfile profile;

        [SetUp]
        public void SetUp()
        {
            profile = AssetDatabase.LoadAssetAtPath<NewGameProfile>(
                ApplicationProjectBuilder.DefaultNewGameProfilePath);
            Assert.That(profile, Is.Not.Null);
        }

        [Test]
        public void DefaultNewGameProfile_IsValidAndSelectsDefaultCharacter()
        {
            Assert.That(profile.TryValidate(out var error), Is.True, error);
            Assert.That(profile.InitialCharacter.DefinitionId, Is.EqualTo("character.default"));
            Assert.That(
                profile.CharacterCatalog.GetRequired("character.default"),
                Is.SameAs(profile.InitialCharacter));
        }

        [Test]
        public void AppRoot_ContainsConfiguredPlayerSessionHost()
        {
            const string appRootPath =
                "Assets/TxTRPG/SceneTransition/Prefabs/AppRoot.prefab";
            var appRoot = AssetDatabase.LoadAssetAtPath<GameObject>(appRootPath);

            Assert.That(appRoot, Is.Not.Null, $"Missing prefab: {appRootPath}");
            var host = appRoot.GetComponentInChildren<PlayerSessionHost>(true);
            Assert.That(host, Is.Not.Null);

            var serializedHost = new SerializedObject(host);
            Assert.That(
                serializedHost.FindProperty("newGameProfile").objectReferenceValue,
                Is.SameAs(profile));
            Assert.That(
                serializedHost.FindProperty("initializeOnAwake").boolValue,
                Is.True);
        }

        [Test]
        public void InitialContentScene_UsesPlayerSessionInsteadOfDemoLoader()
        {
            const string scenePath = "Assets/Scenes/TMP_MainScene.unity";
            var fullPath = Path.GetFullPath(Path.Combine(
                UnityEngine.Application.dataPath,
                "../",
                scenePath));
            Assert.That(File.Exists(fullPath), Is.True, $"Missing scene: {scenePath}");

            var serializedScene = File.ReadAllText(fullPath);
            Assert.That(
                serializedScene,
                Does.Contain(
                    "TxTRPG.Application.Players.ActiveCharacterDisplayBinder"));
            Assert.That(
                serializedScene,
                Does.Contain("TxTRPG.Content.Characters.CharacterDisplayPresenter"));
            Assert.That(
                serializedScene,
                Does.Not.Contain("TxTRPG.UI.CharacterStatusPanel"));
            Assert.That(
                serializedScene,
                Does.Not.Contain("ActiveCharacterStatusBinder"));
            Assert.That(
                serializedScene,
                Does.Not.Contain("AttackPowerText"));
            Assert.That(
                serializedScene,
                Does.Not.Contain("CharacterDisplayPanelDemoLoader"));
            Assert.That(
                serializedScene,
                Does.Not.Contain("PanelStartupController"));
        }

        [Test]
        public void StatusBinder_WithoutStatusPanel_IsAValidOptionalConfiguration()
        {
            var instance = new GameObject(
                "OptionalStatusBinder",
                typeof(ActiveCharacterStatusBinder));
            try
            {
                var binder = instance.GetComponent<ActiveCharacterStatusBinder>();
                Assert.DoesNotThrowAsync(async () =>
                    await binder.InitializeAsync(default, CancellationToken.None));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public async Task NoSave_CreatesDefaultCharacterWithIndependentRuntimeState()
        {
            var repository = new MemoryRepository();
            var session = new PlayerSession(
                profile,
                repository,
                new FixedIdGenerator("instance.default"));

            await session.InitializeAsync(CancellationToken.None);

            var character = session.CurrentPlayer.ActiveCharacter;
            Assert.That(character.CharacterDefinitionId, Is.EqualTo("character.default"));
            Assert.That(character.CharacterInstanceId, Is.EqualTo("instance.default"));
            Assert.That(character.Stats.AttackPower, Is.EqualTo(10));
            Assert.That(character.Health.Current, Is.EqualTo(100));
            Assert.That(character.Health.Maximum, Is.EqualTo(100));

            character.Health.ApplyDamage(25);
            Assert.That(character.Health.Current, Is.EqualTo(75));
            Assert.That(
                profile.InitialCharacter.GameplayDefinition.CreateRuntimeState("fresh").Health.Current,
                Is.EqualTo(100));
        }

        [Test]
        public async Task ExistingSave_IsRestoredWithoutCreatingAnotherCharacter()
        {
            var repository = new MemoryRepository();
            var firstGenerator = new FixedIdGenerator("instance.saved");
            var firstSession = new PlayerSession(profile, repository, firstGenerator);
            await firstSession.InitializeAsync(CancellationToken.None);
            firstSession.CurrentPlayer.ActiveCharacter.Health.ApplyDamage(35);
            await firstSession.SaveAsync(CancellationToken.None);

            var restoreGenerator = new FixedIdGenerator("must-not-be-used");
            var restoredSession = new PlayerSession(profile, repository, restoreGenerator);
            await restoredSession.InitializeAsync(CancellationToken.None);

            Assert.That(restoredSession.CurrentPlayer.Characters.Count, Is.EqualTo(1));
            Assert.That(
                restoredSession.CurrentPlayer.ActiveCharacter.CharacterInstanceId,
                Is.EqualTo("instance.saved"));
            Assert.That(restoredSession.CurrentPlayer.ActiveCharacter.Health.Current, Is.EqualTo(65));
            Assert.That(restoreGenerator.CallCount, Is.Zero);
        }

        [Test]
        public void InvalidSave_FailsWithoutReplacingStoredData()
        {
            var repository = new FailingLoadRepository();
            var session = new PlayerSession(
                profile,
                repository,
                new FixedIdGenerator("unused"));

            Assert.ThrowsAsync<InvalidDataException>(async () =>
                await session.InitializeAsync(CancellationToken.None));
            Assert.That(session.IsReady, Is.False);
            Assert.That(repository.SaveCallCount, Is.Zero);
        }

        [Test]
        public void LocalRepository_InvalidJsonIsPreserved()
        {
            var folder = Path.Combine(
                Path.GetTempPath(),
                "TxTRPG-PlayerSessionTests-" + Guid.NewGuid().ToString("N"));
            var path = Path.Combine(folder, "player-save.json");
            Directory.CreateDirectory(folder);
            File.WriteAllText(path, "{ invalid save");
            try
            {
                var repository = new LocalPlayerSaveRepository(path);
                Assert.ThrowsAsync<InvalidDataException>(async () =>
                    await repository.TryLoadAsync(CancellationToken.None));
                Assert.That(File.ReadAllText(path), Is.EqualTo("{ invalid save"));
            }
            finally
            {
                if (Directory.Exists(folder))
                {
                    Directory.Delete(folder, true);
                }
            }
        }

        private sealed class FixedIdGenerator : ICharacterInstanceIdGenerator
        {
            private readonly string value;

            public FixedIdGenerator(string value)
            {
                this.value = value;
            }

            public int CallCount { get; private set; }

            public string Create()
            {
                CallCount++;
                return value;
            }
        }

        private sealed class MemoryRepository : IPlayerSaveRepository
        {
            private PlayerSaveData saveData;

            public Task<PlayerSaveData> TryLoadAsync(CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult(saveData);
            }

            public Task SaveAsync(
                PlayerSaveData data,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                saveData = data;
                return Task.CompletedTask;
            }
        }

        private sealed class FailingLoadRepository : IPlayerSaveRepository
        {
            public int SaveCallCount { get; private set; }

            public Task<PlayerSaveData> TryLoadAsync(CancellationToken cancellationToken)
            {
                return Task.FromException<PlayerSaveData>(
                    new InvalidDataException("corrupt"));
            }

            public Task SaveAsync(
                PlayerSaveData saveData,
                CancellationToken cancellationToken)
            {
                SaveCallCount++;
                return Task.CompletedTask;
            }
        }
    }
}
