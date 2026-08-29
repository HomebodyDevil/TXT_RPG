using System.Collections;
using UnityEngine;

namespace TxTRPG.UI
{
    public abstract class CharacterViewBase : MonoBehaviour, ICharacterView
    {
        [Header("Visibility Transition")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private bool animateVisibility = true;
        [SerializeField, Min(0f)] private float showDuration = 0.35f;
        [SerializeField, Min(0f)] private float hideDuration = 0.2f;
        [SerializeField, Min(0.001f)] private float maximumFrameDelta = 0.05f;

        private Coroutine visibilityRoutine;

        public bool IsVisible { get; private set; }

        public void Show(in CharacterPresentation presentation)
        {
            StopVisibilityRoutine();
            gameObject.SetActive(true);
            ApplyPresentation(presentation);
            IsVisible = true;

            if (!animateVisibility || showDuration <= 0f || canvasGroup == null)
            {
                SetOpacity(1f);
                return;
            }

            SetOpacity(0f);
            visibilityRoutine = StartCoroutine(Fade(0f, 1f, showDuration, false));
        }

        public void UpdatePresentation(in CharacterPresentation presentation)
        {
            ApplyPresentation(presentation);
        }

        public abstract void PlayAnimation(string animationId);

        public abstract void PlayEffect(string effectId);

        public void Hide()
        {
            StopVisibilityRoutine();
            IsVisible = false;

            if (!animateVisibility || hideDuration <= 0f || canvasGroup == null || !gameObject.activeInHierarchy)
            {
                SetOpacity(0f);
                gameObject.SetActive(false);
                return;
            }

            visibilityRoutine = StartCoroutine(Fade(canvasGroup.alpha, 0f, hideDuration, true));
        }

        public void Clear()
        {
            StopVisibilityRoutine();
            IsVisible = false;
            ClearPresentation();
            SetOpacity(0f);
            gameObject.SetActive(false);
        }

        protected abstract void ApplyPresentation(in CharacterPresentation presentation);

        protected abstract void ClearPresentation();

        private IEnumerator Fade(float start, float end, float duration, bool deactivateWhenFinished)
        {
            SetOpacity(start);
            yield return null;

            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Mathf.Min(Time.unscaledDeltaTime, maximumFrameDelta);
                SetOpacity(Mathf.Lerp(start, end, Mathf.Clamp01(elapsed / duration)));
                yield return null;
            }

            SetOpacity(end);
            visibilityRoutine = null;
            if (deactivateWhenFinished)
            {
                gameObject.SetActive(false);
            }
        }

        private void SetOpacity(float opacity)
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = opacity;
            }
        }

        private void StopVisibilityRoutine()
        {
            if (visibilityRoutine == null)
            {
                return;
            }

            StopCoroutine(visibilityRoutine);
            visibilityRoutine = null;
        }
    }
}
