using System;
using System.Linq;
using NUnit.Framework;
using TxTRPG.Content.Characters;
using TxTRPG.Content.Characters.Editor;
using TxTRPG.Gameplay.Characters;
using TxTRPG.UI;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;

namespace TxTRPG.Content.Tests
{
    public sealed class CharacterContentAssetFactoryTests
    {
        private string rootFolder;
        private CharacterContentCatalog catalog;
        private Sprite sprite;
        private Sprite secondSprite;
        private Sprite separateSprite;

        [SetUp]
        public void SetUp()
        {
            rootFolder = $"Assets/TxTRPG/Content/Tests/Temp/CharacterAuthoring_{Guid.NewGuid():N}";
            EnsureFolder(rootFolder);

            var texture = new Texture2D(2, 2) { name = "TestCharacterTexture" };
            var texturePath = $"{rootFolder}/TestCharacterTexture.asset";
            AssetDatabase.CreateAsset(texture, texturePath);
            sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, 2f, 2f),
                new Vector2(0.5f, 0.5f));
            sprite.name = "TestCharacterSprite";
            AssetDatabase.AddObjectToAsset(sprite, texture);
            secondSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, 2f, 2f),
                new Vector2(0.5f, 0.5f));
            secondSprite.name = "TestCharacterCriticalSprite";
            AssetDatabase.AddObjectToAsset(secondSprite, texture);

            var separateTexture = new Texture2D(2, 2) { name = "SeparateTexture" };
            AssetDatabase.CreateAsset(
                separateTexture,
                $"{rootFolder}/SeparateTexture.asset");
            separateSprite = Sprite.Create(
                separateTexture,
                new Rect(0f, 0f, 2f, 2f),
                new Vector2(0.5f, 0.5f));
            separateSprite.name = "SeparateSprite";
            AssetDatabase.AddObjectToAsset(separateSprite, separateTexture);

            catalog = ScriptableObject.CreateInstance<CharacterContentCatalog>();
            AssetDatabase.CreateAsset(catalog, $"{rootFolder}/CharacterCatalog.asset");
            AssetDatabase.SaveAssets();
        }

        [TearDown]
        public void TearDown()
        {
            if (!string.IsNullOrEmpty(rootFolder) &&
                AssetDatabase.IsValidFolder(rootFolder))
            {
                AssetDatabase.DeleteAsset(rootFolder);
            }
            AssetDatabase.Refresh();
        }

        [Test]
        public void Create_GeneratesConnectedAssetsAndRegistersCatalogOnce()
        {
            var request = CreateRequest("character.authoring.test", "CreatedCharacter");
            var result = new CharacterContentAssetFactory().Create(request);

            Assert.That(AssetDatabase.LoadAssetAtPath<CharacterDefinition>(
                result.GameplayAssetPath), Is.SameAs(result.GameplayDefinition));
            Assert.That(AssetDatabase.LoadAssetAtPath<CharacterAppearanceDefinition>(
                result.AppearanceAssetPath), Is.SameAs(result.AppearanceDefinition));
            Assert.That(AssetDatabase.LoadAssetAtPath<CharacterContentDefinition>(
                result.ContentAssetPath), Is.SameAs(result.ContentDefinition));
            Assert.That(result.ContentDefinition.GameplayDefinition,
                Is.SameAs(result.GameplayDefinition));
            Assert.That(result.ContentDefinition.AppearanceDefinition,
                Is.SameAs(result.AppearanceDefinition));
            Assert.That(result.ContentDefinition.VisualStatePolicy,
                Is.SameAs(result.VisualStatePolicy));
            Assert.That(AssetDatabase.LoadAssetAtPath<CharacterVisualStatePolicy>(
                result.VisualStatePolicyAssetPath), Is.SameAs(result.VisualStatePolicy));
            Assert.That(result.GameplayDefinition.CreateStatBlock().AttackPower, Is.EqualTo(17));
            Assert.That(result.GameplayDefinition.CreateStatBlock().MaxHealth, Is.EqualTo(140));
            Assert.That(catalog.Definitions.Count(
                item => item == result.ContentDefinition), Is.EqualTo(1));

            AssetDatabase.ImportAsset(result.ContentAssetPath, ImportAssetOptions.ForceUpdate);
            var reimported = AssetDatabase.LoadAssetAtPath<CharacterContentDefinition>(
                result.ContentAssetPath);
            Assert.That(reimported.GameplayDefinition, Is.Not.Null);
            Assert.That(reimported.AppearanceDefinition, Is.Not.Null);
            Assert.That(reimported.CreateRuntimeState("created-1").Health.Maximum,
                Is.EqualTo(140));
        }

        [Test]
        public void Create_RegistersSpriteSheetOnceAndResolvesStateSubObjects()
        {
            var request = CreateRequest("character.sprite.sheet", "SpriteSheetCharacter");
            request.RegisterSpriteWithAddressables = true;
            request.AddressablesGroup = $"CharacterAuthoringTest_{Guid.NewGuid():N}";
            request.SpriteAddress = $"characters/tests/{Guid.NewGuid():N}/states";
            request.VisualVariants.Add(new CharacterVisualVariantInput
            {
                VisualStateId = "critical",
                Sprite = secondSprite,
                FramingPreset = CharacterFramingPreset.ThighUp,
                AdditionalScale = 1f,
                Address = request.SpriteAddress
            });

            var result = new CharacterContentAssetFactory().Create(request);
            var spriteGuid = AssetDatabase.AssetPathToGUID(
                AssetDatabase.GetAssetPath(sprite));
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            var entry = settings.FindAssetEntry(spriteGuid);

            Assert.That(entry, Is.Not.Null);
            Assert.That(entry.address, Is.EqualTo(request.SpriteAddress));
            Assert.That(result.AppearanceDefinition.TryResolveReference(
                new CharacterPresentation(
                    request.DefinitionId,
                    string.Empty,
                    "critical",
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    false),
                out var artwork), Is.True);
            Assert.That(
                artwork.AssetId,
                Is.EqualTo($"{request.SpriteAddress}[{secondSprite.name}]"));

            settings.RemoveAssetEntry(spriteGuid);
            var group = settings.FindGroup(request.AddressablesGroup);
            if (group != null && group.entries.Count == 0)
            {
                settings.RemoveGroup(group);
            }
        }

        [Test]
        public void Validator_RejectsAmbiguousVariantsAndSpriteSheetBaseAddressMismatch()
        {
            var request = CreateRequest("character.variant.invalid", "InvalidVariants");
            request.RegisterSpriteWithAddressables = true;
            request.AddressablesGroup = "CharacterAuthoringTests";
            request.VisualVariants.Add(new CharacterVisualVariantInput
            {
                VisualStateId = "critical",
                Sprite = secondSprite,
                AdditionalScale = 1f,
                Address = "characters/tests/critical"
            });
            request.VisualVariants.Add(new CharacterVisualVariantInput
            {
                VisualStateId = string.Empty,
                PoseId = "battle",
                Sprite = secondSprite,
                AdditionalScale = 1f,
                Address = "characters/tests/battle"
            });
            var paths = new CharacterContentAssetPaths(
                request.OutputFolder,
                request.AssetName);

            var errors = CharacterContentCreationValidator.Validate(request, paths);

            Assert.That(errors.Any(error => error.Contains("Visual State ID")), Is.True);
            Assert.That(errors.Any(error => error.Contains("overlap")), Is.True);
            Assert.That(errors.Any(error => error.Contains("share one Addressables")), Is.True);
        }

        [Test]
        public void Validator_RejectsInvalidInputBeforeCreatingAssets()
        {
            var request = CreateRequest(string.Empty, "InvalidCharacter");
            request.MaxHealth = 0;
            request.DefaultSprite = null;
            var paths = new CharacterContentAssetPaths(
                request.OutputFolder,
                request.AssetName);

            var errors = CharacterContentCreationValidator.Validate(request, paths);

            Assert.That(errors.Any(error => error.Contains("Definition ID")), Is.True);
            Assert.That(errors.Any(error => error.Contains("Max Health")), Is.True);
            Assert.That(errors.Any(error => error.Contains("Default Sprite")), Is.True);
            Assert.Throws<ArgumentException>(() =>
                new CharacterContentAssetFactory().Create(request));
            Assert.That(AssetDatabase.LoadMainAssetAtPath(paths.Content), Is.Null);
        }

        [Test]
        public void Create_RejectsExistingPathWithoutOverwritingIt()
        {
            var request = CreateRequest("character.collision", "CollisionCharacter");
            var paths = new CharacterContentAssetPaths(
                request.OutputFolder,
                request.AssetName);
            EnsureFolder(request.OutputFolder);
            var marker = ScriptableObject.CreateInstance<CharacterContentCatalog>();
            AssetDatabase.CreateAsset(marker, paths.Gameplay);

            Assert.Throws<ArgumentException>(() =>
                new CharacterContentAssetFactory().Create(request));
            Assert.That(AssetDatabase.LoadAssetAtPath<CharacterContentCatalog>(paths.Gameplay),
                Is.SameAs(marker));
            Assert.That(AssetDatabase.LoadMainAssetAtPath(paths.Appearance), Is.Null);
            Assert.That(AssetDatabase.LoadMainAssetAtPath(paths.Content), Is.Null);
        }

        [Test]
        public void Create_RejectsDuplicateDefinitionIdBeforeSecondWrite()
        {
            var firstRequest = CreateRequest("character.duplicate", "FirstCharacter");
            new CharacterContentAssetFactory().Create(firstRequest);
            var secondRequest = CreateRequest("character.duplicate", "SecondCharacter");
            var secondPaths = new CharacterContentAssetPaths(
                secondRequest.OutputFolder,
                secondRequest.AssetName);

            Assert.Throws<ArgumentException>(() =>
                new CharacterContentAssetFactory().Create(secondRequest));
            Assert.That(AssetDatabase.LoadMainAssetAtPath(secondPaths.Gameplay), Is.Null);
            Assert.That(AssetDatabase.LoadMainAssetAtPath(secondPaths.Appearance), Is.Null);
            Assert.That(AssetDatabase.LoadMainAssetAtPath(secondPaths.Content), Is.Null);
        }

        [Test]
        public void Create_MidFailureRollsBackAssetsFolderAndCatalog()
        {
            var request = CreateRequest("character.rollback", "RollbackCharacter");
            var paths = new CharacterContentAssetPaths(
                request.OutputFolder,
                request.AssetName);
            var factory = new CharacterContentAssetFactory(step =>
            {
                if (step == CharacterContentCreationStep.CatalogUpdated)
                {
                    throw new InvalidOperationException("Injected failure");
                }
            });

            Assert.Throws<InvalidOperationException>(() => factory.Create(request));

            Assert.That(AssetDatabase.LoadMainAssetAtPath(paths.Gameplay), Is.Null);
            Assert.That(AssetDatabase.LoadMainAssetAtPath(paths.Appearance), Is.Null);
            Assert.That(AssetDatabase.LoadMainAssetAtPath(paths.Content), Is.Null);
            Assert.That(AssetDatabase.IsValidFolder(paths.Folder), Is.False);
            Assert.That(catalog.Definitions, Is.Empty);
        }

        [Test]
        public void Create_AddressablesFailureRollsBackEntryGroupAssetsAndCatalog()
        {
            var request = CreateRequest("character.addressable.rollback", "AddressableRollback");
            request.RegisterSpriteWithAddressables = true;
            request.AddressablesGroup = $"CharacterAuthoringTest_{Guid.NewGuid():N}";
            request.SpriteAddress = $"characters/tests/{Guid.NewGuid():N}";
            var variantAddress = $"characters/tests/{Guid.NewGuid():N}";
            request.VisualVariants.Add(new CharacterVisualVariantInput
            {
                VisualStateId = "critical",
                Sprite = separateSprite,
                AdditionalScale = 1f,
                Address = variantAddress
            });
            var spriteGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(sprite));
            var separateGuid = AssetDatabase.AssetPathToGUID(
                AssetDatabase.GetAssetPath(separateSprite));
            var factory = new CharacterContentAssetFactory(step =>
            {
                if (step == CharacterContentCreationStep.AddressablesRegistered)
                {
                    throw new InvalidOperationException("Injected Addressables failure");
                }
            });

            Assert.Throws<InvalidOperationException>(() => factory.Create(request));

            var settings = AddressableAssetSettingsDefaultObject.Settings;
            Assert.That(settings.FindAssetEntry(spriteGuid), Is.Null);
            Assert.That(settings.FindAssetEntry(separateGuid), Is.Null);
            Assert.That(settings.FindGroup(request.AddressablesGroup), Is.Null);
            Assert.That(AssetDatabase.IsValidFolder(request.OutputFolder), Is.False);
            Assert.That(catalog.Definitions, Is.Empty);
        }

        private CharacterContentCreationRequest CreateRequest(
            string definitionId,
            string assetName)
        {
            return new CharacterContentCreationRequest
            {
                DefinitionId = definitionId,
                AssetName = assetName,
                OutputFolder = $"{rootFolder}/{assetName}",
                DisplayNameLocalizationKey = $"characters.{assetName}.name",
                AttackPower = 17,
                MaxHealth = 140,
                DefaultSprite = sprite,
                FramingPreset = CharacterFramingPreset.ThighUp,
                AdditionalScale = 1.1f,
                PixelOffset = new Vector2(2f, -3f),
                Catalog = catalog,
                RegisterSpriteWithAddressables = false,
                SpriteAddress = $"characters/tests/{assetName}/default"
            };
        }

        private static void EnsureFolder(string folder)
        {
            var segments = folder.Split('/');
            var current = segments[0];
            for (var index = 1; index < segments.Length; index++)
            {
                var next = $"{current}/{segments[index]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, segments[index]);
                }
                current = next;
            }
        }
    }
}
