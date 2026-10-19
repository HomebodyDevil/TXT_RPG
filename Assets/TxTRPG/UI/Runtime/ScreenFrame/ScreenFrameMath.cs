using UnityEngine;

namespace TxTRPG.UI.ScreenFrame
{
    // Fits the reference-resolution frame uniformly into the safe area (the largest frame that fits, centered) and
    // returns the frame and the four letterbox bars in canvas units.
    public static class ScreenFrameMath
    {
        public static readonly Vector2 DefaultReferenceResolution = new Vector2(1920f, 1080f);

        public static ScreenFrameLayout Calculate(Vector2 screenPixels, Rect safeAreaPixels, Vector2 referenceResolution)
        {
            if (!IsPositive(screenPixels.x) || !IsPositive(screenPixels.y)) return ScreenFrameLayout.Invalid;
            var reference = SanitizeReference(referenceResolution);
            var screen = new Rect(0f, 0f, screenPixels.x, screenPixels.y);
            var safe = ClipSafeArea(safeAreaPixels, screen);

            var scale = Mathf.Min(safe.width / reference.x, safe.height / reference.y);
            if (!IsPositive(scale)) return ScreenFrameLayout.Invalid;

            var canvasSize = screenPixels / scale;
            var framePixelSize = reference * scale;
            var framePosition = (safe.center - framePixelSize * .5f) / scale;
            var frame = new Rect(framePosition, reference);
            var safeRect = new Rect(safe.position / scale, safe.size / scale);

            var top = new Rect(0f, frame.yMax, canvasSize.x, Mathf.Max(0f, canvasSize.y - frame.yMax));
            var bottom = new Rect(0f, 0f, canvasSize.x, Mathf.Max(0f, frame.yMin));
            var left = new Rect(0f, frame.yMin, Mathf.Max(0f, frame.xMin), frame.height);
            var right = new Rect(frame.xMax, frame.yMin, Mathf.Max(0f, canvasSize.x - frame.xMax), frame.height);
            return new ScreenFrameLayout(scale, canvasSize, frame, safeRect, top, bottom, left, right);
        }

        public static Vector2 SanitizeReference(Vector2 reference) => new Vector2(
            IsFinite(reference.x) ? Mathf.Max(1f, reference.x) : DefaultReferenceResolution.x,
            IsFinite(reference.y) ? Mathf.Max(1f, reference.y) : DefaultReferenceResolution.y);

        // The part of the safe area inside the screen; an empty, invalid or fully outside safe area means the whole screen.
        private static Rect ClipSafeArea(Rect safe, Rect screen)
        {
            if (!IsFinite(safe.x) || !IsFinite(safe.y) || !IsFinite(safe.width) || !IsFinite(safe.height)) return screen;
            var xMin = Mathf.Max(safe.xMin, screen.xMin);
            var yMin = Mathf.Max(safe.yMin, screen.yMin);
            var xMax = Mathf.Min(safe.xMax, screen.xMax);
            var yMax = Mathf.Min(safe.yMax, screen.yMax);
            return xMax - xMin <= 0f || yMax - yMin <= 0f ? screen : Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        private static bool IsPositive(float value) => IsFinite(value) && value > 0f;
        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
