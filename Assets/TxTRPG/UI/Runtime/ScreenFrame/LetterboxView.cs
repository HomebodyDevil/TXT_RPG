using System;
using UnityEngine;
using UnityEngine.UI;

namespace TxTRPG.UI.ScreenFrame
{
    // Lives on LetterboxLayer (full screen, drawn over the frame). Instantiates the profile's presentation Prefab as its
    // only child and forwards each layout to it. A missing Prefab, a Prefab whose root has no ILetterboxPresentation, or
    // a presentation that throws falls back to plain bars in the profile's fallback color. Every Graphic under the
    // letterbox has raycastTarget turned off. The presentation is created in Play Mode only (Awake), so Edit Mode never
    // adds objects to the Scene.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class LetterboxView : MonoBehaviour
    {
        [SerializeField] private ScreenFrameProfile profile;

        private GameObject instance;
        private ILetterboxPresentation presentation;
        private bool initialized;
        private bool reportedMissingProfile;
        private bool reportedMissingInterface;
        private bool reportedFailure;
        private ScreenFrameLayout lastLayout;

        public ScreenFrameProfile Profile => profile;
        public GameObject PresentationInstance => instance;
        public ILetterboxPresentation Presentation => presentation;
        public bool IsUsingFallback { get; private set; }
        public ScreenFrameLayout LastLayout => lastLayout;

        public void SetProfile(ScreenFrameProfile value) => profile = value;

        private void Awake() => Initialize();

        private void OnDestroy() => DestroyInstance();

        // Creates the presentation once; Awake calls it in Play Mode and tests call it directly.
        public void Initialize()
        {
            if (initialized) return;
            initialized = true;
            if (profile == null && !reportedMissingProfile)
            {
                reportedMissingProfile = true;
                Debug.LogError($"{name}: LetterboxView has no ScreenFrameProfile; using black bars.", this);
            }
            CreatePresentation(profile != null ? profile.LetterboxPresentation : null);
        }

        // Swaps the presentation at runtime: destroys the previous instance and applies the last layout to the new one.
        // null uses the fallback bars.
        public void SetPresentation(GameObject prefab)
        {
            initialized = true;
            DestroyInstance();
            CreatePresentation(prefab);
        }

        // Editor and test seam: uses an existing child instance and implementation (which need not be a component) as the
        // presentation, then applies the last layout.
        public void AttachPresentationForEditor(GameObject presentationInstance, ILetterboxPresentation implementation)
        {
            initialized = true;
            DestroyInstance();
            IsUsingFallback = false;
            instance = presentationInstance;
            presentation = implementation;
            if (presentation == null) CreateFallback();
            DisableRaycastTargets();
            Forward();
        }

        public void ApplyLayout(ScreenFrameLayout layout)
        {
            if (!layout.IsValid) return;
            lastLayout = layout;
            Forward();
        }

        private void CreatePresentation(GameObject prefab)
        {
            IsUsingFallback = false;
            if (prefab != null)
            {
                instance = Instantiate(prefab, transform, false);
                instance.name = prefab.name;
                presentation = instance.GetComponent<ILetterboxPresentation>();
                if (presentation == null)
                {
                    if (!reportedMissingInterface)
                    {
                        reportedMissingInterface = true;
                        Debug.LogError($"{name}: letterbox presentation '{prefab.name}' has no ILetterboxPresentation on its root; using plain bars.", this);
                    }
                    DestroyInstance();
                }
            }
            if (presentation == null) CreateFallback();
            DisableRaycastTargets();
            Forward();
        }

        private void CreateFallback()
        {
            IsUsingFallback = true;
            var color = profile != null ? profile.FallbackLetterboxColor : Color.black;
            var bars = LetterboxBarsPresentation.Create(transform, color);
            instance = bars.gameObject;
            presentation = bars;
        }

        private void Forward()
        {
            if (presentation == null || !lastLayout.IsValid) return;
            try
            {
                presentation.ApplyLayout(lastLayout);
            }
            catch (Exception exception)
            {
                if (IsUsingFallback) throw;
                if (!reportedFailure)
                {
                    reportedFailure = true;
                    Debug.LogError($"{name}: letterbox presentation failed and was replaced with plain bars.", this);
                    Debug.LogException(exception, this);
                }
                DestroyInstance();
                CreateFallback();
                DisableRaycastTargets();
                presentation.ApplyLayout(lastLayout);
            }
        }

        private void DisableRaycastTargets()
        {
            if (instance == null) return;
            foreach (var graphic in instance.GetComponentsInChildren<Graphic>(true))
            {
                if (graphic.raycastTarget) graphic.raycastTarget = false;
            }
        }

        private void DestroyInstance()
        {
            presentation = null;
            if (instance == null) return;
            instance.SetActive(false);
            if (UnityEngine.Application.isPlaying) Destroy(instance);
            else DestroyImmediate(instance);
            instance = null;
        }
    }
}
