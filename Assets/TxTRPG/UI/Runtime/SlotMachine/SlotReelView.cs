using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TxTRPG.UI.SlotMachine
{
    [Serializable]
    public sealed class SlotSymbolSprite
    {
        [SerializeField] private int value;
        [SerializeField] private Sprite sprite;
        public int Value => value;
        public Sprite Sprite => sprite;
    }

    // 2D uGUI reel. Hierarchy (see slot-machine-and-runes.md):
    //   SlotReel_{index} (layout only) > Frame, Window (RectMask2D) > Strip (motion root) > SymbolCell x5 > VisualRoot,
    //   and EffectRoot outside the Window for effects that must not be clipped.
    // Cells k = -2..2 sit at y = -k * cellHeight inside Strip; cell k shows strip[(base + k) mod n], so the cell above the
    // payline is the previous value and the one below is the next. Spinning moves Strip up by a fraction of a cell; at
    // each whole cell the base index advances and Strip snaps back, so the payline shows the strip values in order.
    [DisallowMultipleComponent]
    public sealed class SlotReelView : SlotReelViewBase
    {
        private const int VisibleCells = 3;
        private const int CellCount = 5;

        [SerializeField] private RectTransform window;
        [SerializeField] private RectTransform strip;
        [SerializeField] private RectTransform[] cells = Array.Empty<RectTransform>();
        [SerializeField] private TMP_Text[] cellTexts = Array.Empty<TMP_Text>();
        [SerializeField] private Image[] cellIcons = Array.Empty<Image>();
        [SerializeField, Tooltip("Optional value-to-sprite map. A value without a sprite is shown as its number.")]
        private List<SlotSymbolSprite> symbolSprites = new();
        [SerializeField, Min(1f), Tooltip("Approximate spin speed in cells per second; the exact speed is chosen so the reel lands on the decided cell.")]
        private float cellsPerSecond = 14f;
        [SerializeField, Range(.1f, 1f)] private float paylineFontRatio = .62f;
        [SerializeField, Range(.3f, 1f)] private float sideFontScale = .72f;
        [SerializeField, Range(0f, 1f)] private float sideAlpha = .45f;
        [SerializeField] private float minimumFontSize = 18f;
        [SerializeField] private float maximumFontSize = 64f;

        private readonly int[] noValues = Array.Empty<int>();
        private int[] values = Array.Empty<int>();
        private string reelId = string.Empty;
        private int baseIndex;
        private int boundBase = -1;
        private int generation;
        private bool emphasizePayline = true;
        private Vector2 cellLayoutSize = new(-1f, -1f);

        public override string ReelId => reelId;
        public override int PaylineValue => values.Length == 0 ? 0 : values[baseIndex];
        public override IReadOnlyList<int> Strip => values.Length == 0 ? noValues : Array.AsReadOnly(values);
        public override int StopIndex => baseIndex;
        // Values shown above, on and below the payline (verification only).
        public int ValueAt(int offsetFromPayline) => values.Length == 0 ? 0 : values[Wrap(baseIndex + offsetFromPayline)];
        public string TextAt(int offsetFromPayline) => cellTexts.Length == CellCount ? cellTexts[offsetFromPayline + 2].text : string.Empty;

        public override void SetFontSizeRange(float minimum, float maximum)
        {
            minimumFontSize = Mathf.Max(1f, minimum);
            maximumFontSize = Mathf.Max(minimumFontSize, maximum);
            cellLayoutSize = new Vector2(-1f, -1f);
            UpdateCellLayout();
        }

        public override void ShowStop(SlotReelDisplay reel, int stopIndex)
        {
            if (reel == null) throw new ArgumentNullException(nameof(reel));
            generation++;
            Assign(reel);
            baseIndex = Wrap(stopIndex);
            emphasizePayline = true;
            if (strip != null) strip.anchoredPosition = Vector2.zero;
            boundBase = -1;
            UpdateCellLayout();
            Bind();
        }

        public override async Task<bool> PlaySpinAsync(SlotReelDisplay reel, int stopIndex, SlotReelSpinTiming timing,
            CancellationToken cancellationToken)
        {
            if (reel == null) throw new ArgumentNullException(nameof(reel));
            var token = ++generation;
            var start = Wrap(baseIndex);
            Assign(reel);
            start = Wrap(start);
            var target = Wrap(stopIndex);
            var startTime = Time.unscaledTime;

            if (timing.ReduceMotion)
            {
                while (Time.unscaledTime - startTime < timing.ReducedMotionSeconds)
                {
                    await Task.Yield();
                    if (Interrupted(token, cancellationToken)) return false;
                }
                FinishAt(target);
                return true;
            }

            var spin = timing.SpinSeconds;
            var deceleration = timing.DecelerationSeconds;
            var effective = spin + deceleration * .5f;
            if (effective <= 0f) { FinishAt(target); return true; }
            var delta = (target - start + values.Length) % values.Length;
            var loops = Mathf.Max(1, Mathf.CeilToInt((cellsPerSecond * effective - delta) / values.Length));
            var distance = delta + loops * values.Length;
            var speed = distance / effective;
            emphasizePayline = false;
            boundBase = -1;
            UpdateCellLayout();

            while (true)
            {
                var elapsed = Time.unscaledTime - startTime;
                if (elapsed >= spin + deceleration) break;
                float position;
                if (elapsed < spin) position = speed * elapsed;
                else
                {
                    var tau = Mathf.Min(elapsed - spin, deceleration);
                    position = speed * spin + speed * tau - speed * tau * tau / (2f * deceleration);
                }
                SetPosition(start, Mathf.Min(position, distance - .0001f));
                await Task.Yield();
                if (Interrupted(token, cancellationToken)) return false;
            }
            FinishAt(target);
            return true;
        }

        private void OnRectTransformDimensionsChange() => UpdateCellLayout();

        private void OnDisable() => generation++;

        private bool Interrupted(int token, CancellationToken cancellationToken) =>
            this == null || token != generation || cancellationToken.IsCancellationRequested || !isActiveAndEnabled;

        private void FinishAt(int target)
        {
            baseIndex = target;
            emphasizePayline = true;
            if (strip != null) strip.anchoredPosition = Vector2.zero;
            boundBase = -1;
            UpdateCellLayout();
            Bind();
        }

        private void SetPosition(int start, float cellsTravelled)
        {
            var whole = Mathf.FloorToInt(cellsTravelled);
            baseIndex = Wrap(start + whole);
            if (strip != null) strip.anchoredPosition = new Vector2(0f, (cellsTravelled - whole) * CellHeight);
            Bind();
        }

        private void Assign(SlotReelDisplay reel)
        {
            reelId = reel.ReelId;
            if (values.Length != reel.Values.Count) values = new int[reel.Values.Count];
            for (var i = 0; i < values.Length; i++) values[i] = reel.Values[i];
            boundBase = -1;
        }

        // Rebinds the cell texts only when the base index changes; never per frame otherwise.
        private void Bind()
        {
            if (values.Length == 0 || cellTexts.Length != CellCount || boundBase == baseIndex) return;
            boundBase = baseIndex;
            for (var i = 0; i < CellCount; i++)
            {
                var value = values[Wrap(baseIndex + i - 2)];
                var sprite = FindSprite(value);
                var text = cellTexts[i];
                if (text != null)
                {
                    text.enabled = sprite == null;
                    text.text = Label(value);
                }
                if (i < cellIcons.Length && cellIcons[i] != null)
                {
                    cellIcons[i].sprite = sprite;
                    cellIcons[i].enabled = sprite != null;
                }
            }
        }

        private float CellHeight => window == null ? 0f : window.rect.height / VisibleCells;

        // Sizes and positions the cells from the Window height, and derives the font sizes from the cell size.
        private void UpdateCellLayout()
        {
            if (window == null || cells.Length != CellCount) return;
            var size = window.rect.size;
            var key = new Vector2(size.x, size.y * (emphasizePayline ? 1f : -1f));
            if (key == cellLayoutSize) return;
            cellLayoutSize = key;
            var height = size.y / VisibleCells;
            var paylineSize = Mathf.Clamp(Mathf.Min(height, size.x) * paylineFontRatio, minimumFontSize, maximumFontSize);
            for (var i = 0; i < CellCount; i++)
            {
                var cell = cells[i];
                if (cell == null) continue;
                cell.anchorMin = new Vector2(0f, .5f);
                cell.anchorMax = new Vector2(1f, .5f);
                cell.pivot = new Vector2(.5f, .5f);
                cell.sizeDelta = new Vector2(0f, height);
                cell.anchoredPosition = new Vector2(0f, -(i - 2) * height);
                var text = i < cellTexts.Length ? cellTexts[i] : null;
                if (text == null) continue;
                var onPayline = emphasizePayline && i == 2;
                text.enableAutoSizing = false;
                text.fontSize = onPayline || !emphasizePayline ? paylineSize : paylineSize * sideFontScale;
                text.fontStyle = onPayline ? FontStyles.Bold : FontStyles.Normal;
                var color = text.color;
                color.a = onPayline ? 1f : emphasizePayline ? sideAlpha : .8f;
                text.color = color;
            }
        }

        private Sprite FindSprite(int value)
        {
            for (var i = 0; i < symbolSprites.Count; i++)
                if (symbolSprites[i] != null && symbolSprites[i].Value == value) return symbolSprites[i].Sprite;
            return null;
        }

        // Cached labels, so rebinding during a spin does not allocate a string per wrap.
        private static readonly string[] Labels = new string[256];
        private static string Label(int value)
        {
            if (value < 0 || value >= Labels.Length) return value.ToString();
            return Labels[value] ??= value.ToString();
        }

        private int Wrap(int index) => values.Length == 0 ? 0 : ((index % values.Length) + values.Length) % values.Length;

#if UNITY_EDITOR
        public void ConfigureForEditor(RectTransform targetWindow, RectTransform targetStrip, RectTransform[] targetCells,
            TMP_Text[] targetTexts, Image[] targetIcons)
        {
            window = targetWindow;
            strip = targetStrip;
            cells = targetCells ?? Array.Empty<RectTransform>();
            cellTexts = targetTexts ?? Array.Empty<TMP_Text>();
            cellIcons = targetIcons ?? Array.Empty<Image>();
            cellLayoutSize = new Vector2(-1f, -1f);
        }

        public RectTransform StripForEditor => strip;
#endif
    }
}
