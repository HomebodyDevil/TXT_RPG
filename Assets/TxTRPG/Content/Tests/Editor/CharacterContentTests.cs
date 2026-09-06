using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TxTRPG.Content.Characters;
using TxTRPG.Gameplay.Characters;
using TxTRPG.UI;
using UnityEditor;
using UnityEngine;

namespace TxTRPG.Content.Tests
{
    public sealed class CharacterContentTests
    {
        private readonly List<UnityEngine.Object> createdObjects = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var createdObject in createdObjects)
            {
                UnityEngine.Object.DestroyImmediate(createdObject);
            }
            createdObjects.Clear();
        }

        [Test]
        public void ValidContent_CreatesIndependentRuntimeStates()
        {
            var content = CreateContent("character.knight", "characters/knight/default");

            Assert.That(content.TryValidate(out var error), Is.True, error);
            var first = content.CreateRuntimeState("knight-1");
            var second = content.CreateRuntimeState("knight-2");
            first.Health.ApplyDamage(30);

            Assert.That(first.CharacterDefinitionId, Is.EqualTo("character.knight"));
            Assert.That(first.Health.Current, Is.EqualTo(70));
            Assert.That(second.Health.Current, Is.EqualTo(100));
        }

        [Test]
        public void ContentValidation_RejectsMismatchedIdAndMissingAddressableArtwork()
        {
            var mismatched = CreateContent(
                "character.knight",
                "characters/knight/default",
                "character.other");
            var missingArtwork = CreateContent("character.mage", string.Empty);
            var missingVariantArtwork = CreateContent(
                "character.rogue",
                "characters/rogue/default");
            var appearanceSerialized = new SerializedObject(
                missingVariantArtwork.AppearanceDefinition);
            appearanceSerialized.FindProperty("variants").arraySize = 1;
            appearanceSerialized.ApplyModifiedPropertiesWithoutUndo();

            Assert.That(mismatched.TryValidate(out var mismatchError), Is.False);
            Assert.That(mismatchError, Does.Contain("appearance ID"));
            Assert.That(missingArtwork.TryValidate(out var artworkError), Is.False);
            Assert.That(artworkError, Does.Contain("Addressable artwork ID"));
            Assert.That(missingVariantArtwork.TryValidate(out var variantError), Is.False);
            Assert.That(variantError, Does.Contain("variant 0"));
        }

        [Test]
        public void CollectionValidation_RejectsDuplicateDefinitionIds()
        {
            var first = CreateContent("character.knight", "characters/knight/default");
            var second = CreateContent("character.knight", "characters/knight/default");

            var errors = CharacterContentValidation.CollectErrors(new[] { first, second });

            Assert.That(errors.Any(error => error.Contains("duplicated")), Is.True);
        }

        [Test]
        public void Catalog_IndexesValidContentAndReportsMissingIds()
        {
            var content = CreateContent("character.knight", "characters/knight/default");
            var catalog = Create<CharacterContentCatalog>();
            var serialized = new SerializedObject(catalog);
            var definitions = serialized.FindProperty("definitions");
            definitions.arraySize = 1;
            definitions.GetArrayElementAtIndex(0).objectReferenceValue = content;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Assert.That(catalog.TryGetLoaded(" character.knight ", out var loaded), Is.True);
            Assert.That(loaded, Is.SameAs(content));
            Assert.That(catalog.TryGetLoaded("character.missing", out _), Is.False);
            Assert.Throws<KeyNotFoundException>(() =>
                catalog.GetRequired("character.missing"));
        }

        [Test]
        public void PresentationFactory_UsesRuntimeDefinitionAndRequestedVisualIds()
        {
            var state = CreateContent("character.knight", "characters/knight/default")
                .CreateRuntimeState("knight-1");
            var factory = new CharacterPresentationFactory();

            var presentation = factory.Create(
                state,
                appearanceId: "armor.red",
                poseId: "battle",
                expressionId: "focused",
                animationId: "idle",
                mirrored: true);

            Assert.That(presentation.CharacterId, Is.EqualTo("character.knight"));
            Assert.That(presentation.AppearanceId, Is.EqualTo("armor.red"));
            Assert.That(presentation.PoseId, Is.EqualTo("battle"));
            Assert.That(presentation.ExpressionId, Is.EqualTo("focused"));
            Assert.That(presentation.AnimationId, Is.EqualTo("idle"));
            Assert.That(presentation.Mirrored, Is.True);
        }

        private CharacterContentDefinition CreateContent(
            string definitionId,
            string artworkAssetId,
            string appearanceCharacterId = null)
        {
            var gameplay = Create<CharacterDefinition>();
            var gameplaySerialized = new SerializedObject(gameplay);
            gameplaySerialized.FindProperty("characterId").stringValue = definitionId;
            var stats = gameplaySerialized.FindProperty("baseStats");
            stats.arraySize = 2;
            SetStat(stats.GetArrayElementAtIndex(0), CoreStatIds.AttackPower, 10);
            SetStat(stats.GetArrayElementAtIndex(1), CoreStatIds.MaxHealth, 100);
            gameplaySerialized.ApplyModifiedPropertiesWithoutUndo();

            var appearance = Create<CharacterAppearanceDefinition>();
            var appearanceSerialized = new SerializedObject(appearance);
            appearanceSerialized.FindProperty("characterId").stringValue =
                appearanceCharacterId ?? definitionId;
            appearanceSerialized.FindProperty("fallbackSpriteAssetId").stringValue = artworkAssetId;
            appearanceSerialized.ApplyModifiedPropertiesWithoutUndo();

            var content = Create<CharacterContentDefinition>();
            var contentSerialized = new SerializedObject(content);
            contentSerialized.FindProperty("gameplayDefinition").objectReferenceValue = gameplay;
            contentSerialized.FindProperty("appearanceDefinition").objectReferenceValue = appearance;
            contentSerialized.FindProperty("displayNameLocalizationKey").stringValue =
                $"characters.{definitionId}.name";
            contentSerialized.ApplyModifiedPropertiesWithoutUndo();
            return content;
        }

        private T Create<T>() where T : ScriptableObject
        {
            var instance = ScriptableObject.CreateInstance<T>();
            createdObjects.Add(instance);
            return instance;
        }

        private static void SetStat(
            SerializedProperty entry,
            StatId statId,
            int value)
        {
            entry.FindPropertyRelative("statId").stringValue = statId.Value;
            entry.FindPropertyRelative("value").intValue = value;
        }
    }
}
