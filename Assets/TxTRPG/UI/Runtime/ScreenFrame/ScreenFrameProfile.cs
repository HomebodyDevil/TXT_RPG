using UnityEngine;

namespace TxTRPG.UI.ScreenFrame
{
    // Screen-frame settings shared by the Scenes that follow the 16:9 frame policy. Swap the letterbox look by assigning
    // another presentation Prefab (its root implements ILetterboxPresentation), not by editing components.
    [CreateAssetMenu(menuName = "TxT RPG/UI/Screen Frame Profile", fileName = "ScreenFrameProfile")]
    public sealed class ScreenFrameProfile : ScriptableObject
    {
        [SerializeField, Tooltip("Canvas units inside the frame. Each axis is at least 1.")]
        private Vector2 referenceResolution = new Vector2(1920f, 1080f);
        [SerializeField, Tooltip("Places the frame inside Screen.safeArea. The letterbox still fills the whole screen.")]
        private bool respectSafeArea = true;
        [SerializeField, Tooltip("Letterbox presentation Prefab; its root implements ILetterboxPresentation. Empty uses plain bars.")]
        private GameObject letterboxPresentation;
        [SerializeField, Tooltip("Bar color used when no presentation Prefab is set or it fails.")]
        private Color fallbackLetterboxColor = Color.black;

        public Vector2 ReferenceResolution => ScreenFrameMath.SanitizeReference(referenceResolution);
        public bool RespectSafeArea => respectSafeArea;
        public GameObject LetterboxPresentation => letterboxPresentation;
        public Color FallbackLetterboxColor => fallbackLetterboxColor;

        public void Configure(Vector2 reference, bool safeArea, GameObject presentation, Color fallbackColor)
        {
            referenceResolution = reference;
            respectSafeArea = safeArea;
            letterboxPresentation = presentation;
            fallbackLetterboxColor = fallbackColor;
        }

        private void OnValidate()
        {
            referenceResolution = new Vector2(Mathf.Max(1f, referenceResolution.x), Mathf.Max(1f, referenceResolution.y));
        }
    }
}
