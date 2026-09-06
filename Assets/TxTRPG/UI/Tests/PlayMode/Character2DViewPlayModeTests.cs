using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace TxTRPG.UI.Tests
{
    public sealed class Character2DViewPlayModeTests
    {
        [UnityTest]
        public IEnumerator RapidVisualStateChange_CancelsStaleArtworkLoad()
        {
            var root = new GameObject("CharacterViewTest");
            var imageObject = new GameObject("BaseImage", typeof(RectTransform), typeof(Image));
            imageObject.transform.SetParent(root.transform, false);
            var image = imageObject.GetComponent<Image>();
            var view = root.AddComponent<Character2DView>();
            typeof(Character2DView)
                .GetField("baseImage", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(view, image);

            var texture = new Texture2D(2, 2);
            var normalSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, 2f, 2f),
                new Vector2(0.5f, 0.5f));
            var criticalSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, 2f, 2f),
                new Vector2(0.5f, 0.5f));
            var framing = new CharacterArtworkFraming(CharacterFramingPreset.ThighUp);
            var appearance = ScriptableObject.CreateInstance<CharacterAppearanceDefinition>();
            appearance.ConfigureForEditor(
                "character.test",
                normalSprite,
                "artwork/fallback",
                framing,
                new[]
                {
                    CharacterAppearanceDefinition.Variant.CreateForEditor(
                        string.Empty, "normal", string.Empty, string.Empty,
                        normalSprite, "artwork/normal", framing),
                    CharacterAppearanceDefinition.Variant.CreateForEditor(
                        string.Empty, "critical", string.Empty, string.Empty,
                        criticalSprite, "artwork/critical", framing)
                });

            var provider = new ControlledSpriteProvider();
            view.SetAppearanceDefinitions(new[] { appearance });
            view.SetAssetProvider(provider);
            view.ShowImmediately(Presentation("normal"));
            yield return null;
            Assert.That(provider.Requests.Count, Is.EqualTo(1));

            view.UpdatePresentation(Presentation("critical"));
            yield return null;
            Assert.That(provider.Requests.Count, Is.EqualTo(2));
            Assert.That(provider.Requests[0].CancellationToken.IsCancellationRequested, Is.True);

            provider.Requests[0].Completion.SetResult(normalSprite);
            provider.Requests[1].Completion.SetResult(criticalSprite);
            var ready = view.WhenAssetsReady;
            while (!ready.IsCompleted)
            {
                yield return null;
            }

            Assert.That(ready.IsFaulted, Is.False);
            Assert.That(image.sprite, Is.SameAs(criticalSprite));

            Object.Destroy(root);
            Object.Destroy(appearance);
            Object.Destroy(normalSprite);
            Object.Destroy(criticalSprite);
            Object.Destroy(texture);
            yield return null;
        }

        private static CharacterPresentation Presentation(string stateId) =>
            new(
                "character.test",
                string.Empty,
                stateId,
                string.Empty,
                string.Empty,
                string.Empty,
                false);

        private sealed class ControlledSpriteProvider : IAssetProvider
        {
            public readonly List<Request> Requests = new();

            public async Task<AssetLease<T>> LoadAsync<T>(
                string assetId,
                CancellationToken cancellationToken = default)
                where T : Object
            {
                var request = new Request(cancellationToken);
                Requests.Add(request);
                var sprite = await request.Completion.Task;
                cancellationToken.ThrowIfCancellationRequested();
                return new AssetLease<T>(
                    (T)(Object)sprite,
                    assetId,
                    () => { });
            }
        }

        private sealed class Request
        {
            public Request(CancellationToken cancellationToken)
            {
                CancellationToken = cancellationToken;
            }

            public CancellationToken CancellationToken { get; }
            public TaskCompletionSource<Sprite> Completion { get; } = new(
                TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }
}
