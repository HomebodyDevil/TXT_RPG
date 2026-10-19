namespace TxTRPG.UI.ScreenFrame
{
    // Implemented by the root component of a letterbox presentation Prefab. Receiving the same layout again must give the
    // same result. Graphics under the presentation must not be raycast targets (LetterboxView also turns them off).
    public interface ILetterboxPresentation
    {
        void ApplyLayout(ScreenFrameLayout layout);
    }
}
