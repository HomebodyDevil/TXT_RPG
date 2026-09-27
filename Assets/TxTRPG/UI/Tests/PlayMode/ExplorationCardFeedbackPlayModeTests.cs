#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using TMPro;
using TxTRPG.UI.Exploration;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace TxTRPG.UI.Tests
{
    public sealed class ExplorationCardFeedbackPlayModeTests
    {
        [UnityTest]
        public IEnumerator Pulse_InputSettingsReuseAndTextRebuild_AreSafe()
        {
            const string effectsKey = "TxTRPG.UI.ExplorationCardEffects";
            const string motionKey = "TxTRPG.UI.ExplorationReduceMotion";
            var hadEffects = PlayerPrefs.HasKey(effectsKey); var effects = PlayerPrefs.GetInt(effectsKey);
            var hadMotion = PlayerPrefs.HasKey(motionKey); var motion = PlayerPrefs.GetInt(motionKey);
            var timeScale = Time.timeScale;
            var canvas = new GameObject("CardTestCanvas", typeof(Canvas));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TxTRPG/UI/Prefabs/ExplorationNodeChoiceCard.prefab");
            var cardObject = Object.Instantiate(prefab, canvas.transform);
            var view = cardObject.GetComponent<ExplorationNodeChoiceCardView>();
            var feedback = view.Feedback;
            var root = cardObject.transform.Find("MotionRoot");
            var localProfile = Object.Instantiate(AssetDatabase.LoadAssetAtPath<ExplorationCardPresentationProfile>("Assets/TxTRPG/UI/Styles/ExplorationCardDefaultPresentation.asset"));
            try
            {
                ExplorationCardPlayerPreferences.EffectsEnabled = true;
                ExplorationCardPlayerPreferences.ReduceMotion = false;
                Time.timeScale = 0f;
                var pointer = new PointerEventData(EventSystem.current);
                feedback.OnPointerEnter(pointer);
                var minimum = float.MaxValue; var maximum = float.MinValue;
                var start = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - start < 5.6f)
                {
                    minimum = Mathf.Min(minimum, root.localScale.x); maximum = Mathf.Max(maximum, root.localScale.x);
                    Assert.That(root.localScale.x, Is.InRange(.9999f, 1.0301f));
                    yield return null;
                }
                Assert.That(maximum - minimum, Is.GreaterThan(.025f));
                feedback.OnSelect(new BaseEventData(EventSystem.current)); feedback.OnPointerExit(pointer);
                yield return new WaitForSecondsRealtime(.5f);
                Assert.That(root.localScale.x, Is.GreaterThan(1f), "Navigation focus survives pointer exit.");
                ExplorationCardPlayerPreferences.ReduceMotion = true;
                Assert.That(root.localScale, Is.EqualTo(Vector3.one));
                ExplorationCardPlayerPreferences.ReduceMotion = false;
                yield return new WaitForSecondsRealtime(.5f);
                Assert.That(root.localScale.x, Is.GreaterThan(1f));
                view.Button.interactable = false;
                yield return new WaitForSecondsRealtime(.8f);
                Assert.That(root.localScale.x, Is.EqualTo(1f).Within(.001f));
                view.Button.interactable = true;
                feedback.ResetState();
                var touch = new ExtendedPointerEventData(EventSystem.current) { pointerType = UIPointerType.Touch };
                feedback.OnPointerEnter(touch); feedback.OnSelect(touch); feedback.OnPointerDown(touch); feedback.OnPointerUp(touch);
                yield return new WaitForSecondsRealtime(.5f);
                Assert.That(root.localScale.x, Is.EqualTo(1f).Within(.001f));
                feedback.OnPointerEnter(pointer); feedback.OnPointerDown(pointer); pointer.dragging = true;
                yield return new WaitForSecondsRealtime(.5f);
                Assert.That(root.localScale.x, Is.EqualTo(1f).Within(.001f));
                pointer.dragging = false;

                var text = cardObject.GetComponentsInChildren<TMP_Text>(true).First(t => t.name == "Description");
                var effect = text.GetComponent<ExplorationCardTextEffectController>();
                localProfile.descriptionEffect = new ExplorationCardTextEffectSettings { waveEnabled = true, waveAmplitude = 2f, waveSpeed = 2f, characterPhase = .2f };
                effect.ConfigureForEditor(text, ExplorationCardTextRole.Description, localProfile);
                text.text = "한글 ABC 123";
                yield return null; yield return null;
                text.text = "새 문구 42";
                localProfile.descriptionColor = Color.white;
                localProfile.descriptionEffect = default;
                yield return null; yield return null;
                Assert.That(text.text, Is.EqualTo("새 문구 42"));
                Assert.That(text.color, Is.EqualTo(Color.white));
                Assert.That(text.textInfo.characterCount, Is.GreaterThan(0));
                foreach (var character in text.textInfo.characterInfo.Take(text.textInfo.characterCount).Where(c => c.isVisible))
                    Assert.That(text.textInfo.meshInfo[character.materialReferenceIndex].colors32[character.vertexIndex].a, Is.EqualTo(255));
                effect.ConfigureForEditor(text, ExplorationCardTextRole.Description, null);
                Assert.That(text.color, Is.EqualTo(Color.white));
                cardObject.SetActive(false);
                Assert.That(root.localScale, Is.EqualTo(Vector3.one));
                cardObject.SetActive(true);
                view.ResetPresentation();
                Assert.That(root.localScale, Is.EqualTo(Vector3.one));
            }
            finally
            {
                Time.timeScale = timeScale;
                Object.DestroyImmediate(canvas); Object.DestroyImmediate(localProfile);
                if (hadEffects) PlayerPrefs.SetInt(effectsKey, effects); else PlayerPrefs.DeleteKey(effectsKey);
                if (hadMotion) PlayerPrefs.SetInt(motionKey, motion); else PlayerPrefs.DeleteKey(motionKey);
                PlayerPrefs.Save();
            }
        }
    }
}
#endif
