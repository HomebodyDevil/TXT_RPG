using UnityEngine;

namespace TxTRPG.UI.ScreenFrame
{
    // One computed screen-frame placement. Every rect is in canvas units with the origin at the canvas' bottom left;
    // bars of size zero are kept as they are.
    public readonly struct ScreenFrameLayout
    {
        public ScreenFrameLayout(float scaleFactor, Vector2 canvasSize, Rect frameRect, Rect safeRect,
            Rect topBar, Rect bottomBar, Rect leftBar, Rect rightBar)
        {
            IsValid = true;
            ScaleFactor = scaleFactor;
            CanvasSize = canvasSize;
            FrameRect = frameRect;
            SafeRect = safeRect;
            TopBar = topBar;
            BottomBar = bottomBar;
            LeftBar = leftBar;
            RightBar = rightBar;
        }

        public static ScreenFrameLayout Invalid => default;

        public bool IsValid { get; }
        public float ScaleFactor { get; }
        public Vector2 CanvasSize { get; }
        public Rect FrameRect { get; }
        public Rect SafeRect { get; }
        public Rect TopBar { get; }
        public Rect BottomBar { get; }
        public Rect LeftBar { get; }
        public Rect RightBar { get; }

        public override string ToString() => IsValid
            ? $"scale {ScaleFactor:0.###}, canvas {CanvasSize}, frame {FrameRect}, top {TopBar.height:0.#}, bottom {BottomBar.height:0.#}, left {LeftBar.width:0.#}, right {RightBar.width:0.#}"
            : "invalid";
    }
}
