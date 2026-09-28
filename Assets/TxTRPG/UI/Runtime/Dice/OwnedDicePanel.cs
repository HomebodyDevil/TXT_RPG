using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using TxTRPG.Gameplay.Dice;
using UnityEngine;
using UnityEngine.UI;

namespace TxTRPG.UI.Dice
{
    public sealed class OwnedDieDisplay
    {
        public OwnedDieDisplay(string instanceId, string definitionId, string displayName, IReadOnlyList<DiceFace> source)
        {
            InstanceId = instanceId;
            DefinitionId = definitionId;
            DisplayName = displayName;
            var copy = new DiceFace[source.Count];
            for (var i = 0; i < copy.Length; i++) copy[i] = source[i];
            Faces = Array.AsReadOnly(copy);
        }
        public string InstanceId { get; }
        public string DefinitionId { get; }
        public string DisplayName { get; }
        public IReadOnlyList<DiceFace> Faces { get; }
    }

    public readonly struct DieRollDisplay
    {
        public DieRollDisplay(string instanceId, int faceIndex, DiceEffectKind effect, int amount)
        { InstanceId = instanceId; FaceIndex = faceIndex; Effect = effect; Amount = amount; }
        public string InstanceId { get; }
        public int FaceIndex { get; }
        public DiceEffectKind Effect { get; }
        public int Amount { get; }
    }

    [DisallowMultipleComponent]
    public sealed class OwnedDicePanel : MonoBehaviour
    {
        [SerializeField] private RawImage diceViewport;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private Button resultsButton;
        [SerializeField] private Camera stageCamera;
        [SerializeField] private Transform modelRoot;
        [SerializeField] private DieShapeCatalog shapeCatalog;
        [SerializeField, Min(0f)] private float rollDuration = 0.85f;
        [SerializeField, Min(0f)] private float reducedMotionDuration = 0.05f;
        [SerializeField, Range(128, 2048)] private int maximumTextureDimension = 1024;

        private sealed class Visual
        {
            public string DefinitionId;
            public DieShapeDefinition Shape;
            public Transform Root;
            public DieModelView Model;
        }

        private readonly Dictionary<string, Visual> visuals = new(StringComparer.Ordinal);
        private readonly List<OwnedDieDisplay> owned = new();
        private RenderTexture texture;
        private Vector2Int textureSize;
        private int generation;
        private bool rolling;
        public Button ResultsButton => resultsButton;
        public bool IsRolling => rolling;
        public int DisplayCount => owned.Count;
        public RenderTexture CurrentTexture => texture;

        private void OnEnable()
        {
            if (stageCamera != null) stageCamera.enabled = true;
            UpdateRenderTarget();
        }

        private void LateUpdate()
        {
            if (diceViewport == null || stageCamera == null) return;
            var desired = TargetSize(diceViewport.rectTransform.rect.size);
            if (desired != textureSize) UpdateRenderTarget();
        }

        private void OnDisable()
        {
            generation++;
            rolling = false;
            if (stageCamera != null) stageCamera.enabled = false;
            ReleaseTexture();
        }

        private void OnDestroy()
        {
            ReleaseTexture();
            foreach (var visual in visuals.Values)
                if (visual.Root != null) Destroy(visual.Root.gameObject);
            visuals.Clear();
        }

        public void SetDice(IReadOnlyList<OwnedDieDisplay> dice)
        {
            owned.Clear();
            if (dice != null) owned.AddRange(dice);
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var die in owned) ids.Add(die.InstanceId);
            var removed = new List<string>();
            foreach (var pair in visuals)
                if (!ids.Contains(pair.Key)) removed.Add(pair.Key);
            foreach (var id in removed)
            {
                if (visuals[id].Root != null) Destroy(visuals[id].Root.gameObject);
                visuals.Remove(id);
            }

            var failures = new List<string>();
            for (var i = 0; i < owned.Count; i++)
            {
                var die = owned[i];
                var shape = shapeCatalog != null ? shapeCatalog.Find(die.DefinitionId, die.Faces.Count) : null;
                if (shape == null || modelRoot == null)
                {
                    failures.Add($"{die.DisplayName}: 3D 형태 없음 ({die.Faces.Count}면)");
                    continue;
                }
                if (visuals.TryGetValue(die.InstanceId, out var visual) && visual.Shape != shape)
                {
                    if (visual.Root != null) Destroy(visual.Root.gameObject);
                    visuals.Remove(die.InstanceId);
                    visual = null;
                }
                if (visual == null)
                {
                    var instance = Instantiate(shape.ModelPrefab, modelRoot);
                    instance.name = $"Die-{die.InstanceId}";
                    visual = new Visual { DefinitionId = die.DefinitionId, Shape = shape,
                        Root = instance.transform, Model = instance.GetComponent<DieModelView>() };
                    visuals.Add(die.InstanceId, visual);
                }
                try { visual.Model.SetFaces(die.Faces); }
                catch (Exception exception) { failures.Add($"{die.DisplayName}: {exception.Message}"); }
            }
            Arrange();
            if (statusText != null)
                statusText.text = owned.Count == 0 ? "보유 주사위가 없습니다." :
                    failures.Count == 0 ? $"보유 {owned.Count}개 · 공=공격 · 회=회복" : string.Join("\n", failures);
        }

        public async Task<bool> PlayRollAsync(IReadOnlyList<DieRollDisplay> results,
            bool reduceMotion, CancellationToken cancellationToken = default)
        {
            if (!isActiveAndEnabled || stageCamera == null || modelRoot == null) return false;
            if (results == null) return false;
            foreach (var roll in results)
                if (!visuals.TryGetValue(roll.InstanceId, out var visual) || visual.Root == null ||
                    (uint)roll.FaceIndex >= (uint)visual.Shape.FaceCount) return false;
            var token = ++generation;
            rolling = true;
            var duration = reduceMotion ? reducedMotionDuration : rollDuration;
            var start = Time.realtimeSinceStartup;
            try
            {
                while (Time.realtimeSinceStartup - start < duration)
                {
                    if (cancellationToken.IsCancellationRequested || token != generation || this == null || !isActiveAndEnabled)
                        return false;
                    var progress = Mathf.Clamp01((Time.realtimeSinceStartup - start) / Mathf.Max(0.001f, duration));
                    for (var i = 0; i < results.Count; i++)
                    {
                        var roll = results[i];
                        if (!visuals.TryGetValue(roll.InstanceId, out var visual) || visual.Root == null ||
                            (uint)roll.FaceIndex >= (uint)visual.Shape.FaceCount) continue;
                        var final = visual.Shape.PoseFor(roll.FaceIndex);
                        visual.Root.localRotation = Quaternion.Euler(360f * progress, 720f * progress, 360f * progress) * final;
                        var position = visual.Root.localPosition;
                        position.z = reduceMotion ? 0f : Mathf.Sin(progress * Mathf.PI) * 0.35f;
                        visual.Root.localPosition = position;
                    }
                    await Task.Yield();
                }
                if (cancellationToken.IsCancellationRequested || token != generation || this == null || !isActiveAndEnabled)
                    return false;
                for (var i = 0; i < results.Count; i++)
                {
                    var roll = results[i];
                    if (!visuals.TryGetValue(roll.InstanceId, out var visual) || visual.Root == null ||
                        (uint)roll.FaceIndex >= (uint)visual.Shape.FaceCount) continue;
                    visual.Root.localRotation = visual.Shape.PoseFor(roll.FaceIndex);
                    var position = visual.Root.localPosition;
                    position.z = 0f;
                    visual.Root.localPosition = position;
                }
                return true;
            }
            finally { if (token == generation) rolling = false; }
        }

        public void ShowRollFallback(IReadOnlyList<DieRollDisplay> results)
        {
            if (statusText == null) return;
            var parts = new List<string>();
            foreach (var roll in results)
                parts.Add($"{roll.InstanceId}: {(roll.Effect == DiceEffectKind.Attack ? "공격" : "회복")} {roll.Amount}");
            statusText.text = string.Join(" / ", parts);
        }

        public void SetResultsAvailable(bool available)
        {
            if (resultsButton != null) resultsButton.interactable = available;
        }

        private void Arrange()
        {
            if (stageCamera == null || diceViewport == null) return;
            var count = Mathf.Max(1, owned.Count);
            var rect = diceViewport.rectTransform.rect;
            var aspect = Mathf.Max(0.5f, rect.width / Mathf.Max(1f, rect.height));
            var columns = Mathf.Clamp(Mathf.CeilToInt(Mathf.Sqrt(count * aspect)), 1, count);
            var rows = Mathf.CeilToInt(count / (float)columns);
            const float spacing = 2.6f;
            for (var i = 0; i < owned.Count; i++)
            {
                if (!visuals.TryGetValue(owned[i].InstanceId, out var visual) || visual.Root == null) continue;
                var row = i / columns;
                var column = i % columns;
                var inRow = Mathf.Min(columns, owned.Count - row * columns);
                visual.Root.localPosition = new Vector3((column - (inRow - 1) * 0.5f) * spacing,
                    ((rows - 1) * 0.5f - row) * spacing, 0f);
                visual.Root.localScale = Vector3.one;
            }
            stageCamera.orthographicSize = Mathf.Max(2f, rows * spacing * 0.65f,
                columns * spacing * 0.65f / aspect);
        }

        private Vector2Int TargetSize(Vector2 size)
        {
            if (size.x <= 0f || size.y <= 0f) return Vector2Int.zero;
            var max = Mathf.Clamp(maximumTextureDimension, 128, 2048);
            var scale = Mathf.Min(1f, max / Mathf.Max(size.x, size.y));
            return new Vector2Int(Mathf.Clamp(Mathf.CeilToInt(size.x * scale / 64f) * 64, 64, max),
                Mathf.Clamp(Mathf.CeilToInt(size.y * scale / 64f) * 64, 64, max));
        }

        private void UpdateRenderTarget()
        {
            if (diceViewport == null || stageCamera == null) return;
            var desired = TargetSize(diceViewport.rectTransform.rect.size);
            if (desired == textureSize) return;
            ReleaseTexture();
            if (desired == Vector2Int.zero) return;
            texture = new RenderTexture(desired.x, desired.y, 16, RenderTextureFormat.ARGB32)
            { name = "OwnedDicePanel-RenderTexture", antiAliasing = 1 };
            texture.Create();
            textureSize = desired;
            stageCamera.targetTexture = texture;
            diceViewport.texture = texture;
            Arrange();
        }

        private void ReleaseTexture()
        {
            if (stageCamera != null) stageCamera.targetTexture = null;
            if (diceViewport != null) diceViewport.texture = null;
            if (texture != null) { texture.Release(); Destroy(texture); texture = null; }
            textureSize = Vector2Int.zero;
        }

#if UNITY_EDITOR
        public void ConfigureForEditor(RawImage viewport, TMP_Text state, Button button, Camera camera,
            Transform models, DieShapeCatalog catalog)
        { diceViewport = viewport; statusText = state; resultsButton = button; stageCamera = camera;
          modelRoot = models; shapeCatalog = catalog; }
#endif
    }
}
