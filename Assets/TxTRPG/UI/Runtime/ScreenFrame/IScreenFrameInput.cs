using UnityEngine;

namespace TxTRPG.UI.ScreenFrame
{
    // Screen values the frame is computed from. The default reads UnityEngine.Screen; tests supply fixed values.
    public interface IScreenFrameInput
    {
        Vector2 ScreenSize { get; }
        Rect SafeArea { get; }
    }

    public sealed class UnityScreenFrameInput : IScreenFrameInput
    {
        public static readonly UnityScreenFrameInput Instance = new UnityScreenFrameInput();

        private UnityScreenFrameInput() { }

        public Vector2 ScreenSize => new Vector2(Screen.width, Screen.height);
        public Rect SafeArea => Screen.safeArea;
    }
}
