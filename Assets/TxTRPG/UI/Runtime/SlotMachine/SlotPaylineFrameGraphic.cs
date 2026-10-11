using UnityEngine;
using UnityEngine.UI;

namespace TxTRPG.UI.SlotMachine
{
    // Rectangular payline border drawn inside its own rect. It is a dedicated Graphic so an outline or glow can be added
    // around it later without touching the reels. Takes no input.
    [DisallowMultipleComponent, RequireComponent(typeof(CanvasRenderer))]
    public sealed class SlotPaylineFrameGraphic : MaskableGraphic
    {
        [SerializeField, Min(0f), Tooltip("Border thickness in canvas units at the reference resolution.")]
        private float thickness = 4f;
        [SerializeField, Min(0f), Tooltip("The border is never thinner than this many screen pixels.")]
        private float minimumPixelThickness = 2f;

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
        }

        // Thickness in this graphic's units after the screen-pixel minimum.
        public float EffectiveThickness
        {
            get
            {
                var scale = canvas != null ? Mathf.Max(.0001f, canvas.scaleFactor) : 1f;
                return Mathf.Max(thickness, minimumPixelThickness / scale);
            }
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var rect = GetPixelAdjustedRect();
            var t = Mathf.Min(EffectiveThickness, rect.width * .5f, rect.height * .5f);
            if (t <= 0f) return;
            AddQuad(vh, rect.xMin, rect.yMax - t, rect.xMax, rect.yMax);
            AddQuad(vh, rect.xMin, rect.yMin, rect.xMax, rect.yMin + t);
            AddQuad(vh, rect.xMin, rect.yMin + t, rect.xMin + t, rect.yMax - t);
            AddQuad(vh, rect.xMax - t, rect.yMin + t, rect.xMax, rect.yMax - t);
        }

        private void AddQuad(VertexHelper vh, float xMin, float yMin, float xMax, float yMax)
        {
            var start = vh.currentVertCount;
            Color32 vertexColor = color;
            vh.AddVert(new Vector3(xMin, yMin), vertexColor, Vector2.zero);
            vh.AddVert(new Vector3(xMin, yMax), vertexColor, Vector2.up);
            vh.AddVert(new Vector3(xMax, yMax), vertexColor, Vector2.one);
            vh.AddVert(new Vector3(xMax, yMin), vertexColor, Vector2.right);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start + 2, start + 3, start);
        }

#if UNITY_EDITOR
        public void ConfigureForEditor(float targetThickness, float targetMinimumPixels)
        {
            thickness = Mathf.Max(0f, targetThickness);
            minimumPixelThickness = Mathf.Max(0f, targetMinimumPixels);
            SetVerticesDirty();
        }
#endif
    }
}
