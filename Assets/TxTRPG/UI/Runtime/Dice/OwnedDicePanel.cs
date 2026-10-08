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

    // Verification record of the last PlayRollAsync call. Times are Time.realtimeSinceStartup values.
    public sealed class OwnedDiceRollTimeline
    {
        public bool UsedThrow { get; internal set; }
        // Every participant was outside one of the four screen edges when the recalled pose was replaced by the launch
        // pose, and still is in the launch pose.
        public bool HiddenAtSwap { get; internal set; } = true;
        public string SwapDetail { get; internal set; } = string.Empty;
        // Recall edge chosen per participant (results order) and how path overlaps were resolved.
        public IReadOnlyList<DiceRecallEdge> RecallEdges { get; internal set; } = Array.Empty<DiceRecallEdge>();
        public DiceRecallResolution RecallResolution { get; internal set; }
        public float StartedAt { get; internal set; }
        public float RecallEndedAt { get; internal set; }
        public float ThrowStartedAt { get; internal set; }
        public float SettledAt { get; internal set; }
        public float RevealedAt { get; internal set; }
        // Largest position change of any die between the settled pose and the end of the roll (hold and final layout).
        public float ShiftAfterSettle { get; internal set; }
    }

    // Presents an already decided roll. The physics throw is pre-computed in an isolated physics Scene and replayed;
    // a symmetry rotation on each visual makes the decided face the one that ends up pointing at the ceiling.
    [DisallowMultipleComponent]
    public sealed class OwnedDicePanel : MonoBehaviour
    {
        private const float DefaultDieRadius = 1.75f;

        [SerializeField] private RawImage diceViewport;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private Button resultsButton;
        [SerializeField] private Camera stageCamera;
        [SerializeField] private Transform modelRoot;
        [SerializeField] private DieShapeCatalog shapeCatalog;
        [SerializeField, Min(0f), Tooltip("Placement transition used when the throw simulation cannot produce a valid rest.")]
        private float fallbackDuration = 0.35f;
        [SerializeField, Min(0f)] private float reducedMotionDuration = 0.05f;
        [SerializeField, Range(128, 2048)] private int maximumTextureDimension = 1024;
        [SerializeField] private DiceArenaSettings arenaSettings = new();
        [SerializeField] private DiceThrowSettings throwSettings = new();
        [SerializeField, Tooltip("Non-zero fixes the presentation seed for reproduction. It never affects roll results.")]
        private int debugThrowSeed;

        // Root = placement and physics trajectory. Body (the model instance) = symmetry correction only.
        private sealed class Visual
        {
            public string DefinitionId;
            public DieShapeDefinition Shape;
            public Transform Root;
            public Transform Body;
            public DieModelView Model;
            public int RestFace;
            public bool Hidden;
        }

        private sealed class ThrowPlan
        {
            public Visual[] Dice;
            public Quaternion[] Corrections;
            public DiceTrajectory Trajectory;
        }

        private readonly Dictionary<string, Visual> visuals = new(StringComparer.Ordinal);
        private readonly List<OwnedDieDisplay> owned = new();
        private readonly System.Random seedSource = new();
        private RenderTexture texture;
        private Vector2Int textureSize;
        private int generation;
        private bool rolling;
        private bool interrupted;
        private bool layoutPending = true;
        private bool hasArena;
        private DiceArena arena;
        private DiceStageRig rig;
        private DiceThrowSimulator simulator;

        public Button ResultsButton => resultsButton;
        public bool IsRolling => rolling;
        public int DisplayCount => owned.Count;
        public RenderTexture CurrentTexture => texture;
        public DiceThrowOutcome LastThrowOutcome { get; private set; }
        public bool LastRollUsedPlacementFallback { get; private set; }
        public DiceArena CurrentArena => arena;
        public OwnedDiceRollTimeline LastRollTimeline { get; private set; }
        // Upper bound of PlayRollAsync after the synchronous simulation: the throw path, or the placement paths, plus the
        // result hold. Callers derive their timeout from it.
        public float MaximumPresentationSeconds =>
            Mathf.Max(throwSettings.MaximumThrowPresentationSeconds,
                Mathf.Max(fallbackDuration, reducedMotionDuration) + throwSettings.ResultRevealDelay);

        private void OnEnable()
        {
            if (stageCamera != null) stageCamera.enabled = true;
            if (interrupted) { interrupted = false; layoutPending = true; }
            UpdateRenderTarget();
            if (layoutPending) Compose();
        }

        private void LateUpdate()
        {
            // The composition of an in-progress throw is kept; size changes are applied when it finishes.
            if (rolling || diceViewport == null || stageCamera == null) return;
            var desired = TargetSize(diceViewport.rectTransform.rect.size);
            if (desired != textureSize) UpdateRenderTarget();
        }

        private void OnDisable()
        {
            if (rolling) interrupted = true;
            generation++;
            rolling = false;
            if (stageCamera != null) stageCamera.enabled = false;
            ReleaseTexture();
            simulator?.Dispose();
            simulator = null;
        }

        private void OnDestroy()
        {
            ReleaseTexture();
            simulator?.Dispose();
            simulator = null;
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
                    var root = new GameObject($"Die-{die.InstanceId}").transform;
                    root.gameObject.layer = modelRoot.gameObject.layer;
                    root.SetParent(modelRoot, false);
                    var instance = Instantiate(shape.ModelPrefab, root);
                    instance.name = "Visual";
                    visual = new Visual { DefinitionId = die.DefinitionId, Shape = shape, Root = root,
                        Body = instance.transform, Model = instance.GetComponent<DieModelView>() };
                    // Dice added during a throw join the next action and appear when the throw finishes.
                    if (rolling) { visual.Hidden = true; root.gameObject.SetActive(false); }
                    visuals.Add(die.InstanceId, visual);
                }
                try { visual.Model.SetFaces(die.Faces); }
                catch (Exception exception) { failures.Add($"{die.DisplayName}: {exception.Message}"); }
            }
            // Ownership changes always return to the orderly layout; each die keeps its last shown face.
            layoutPending = true;
            if (!rolling) Compose();
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
                if (!visuals.TryGetValue(roll.InstanceId, out var visual) || visual.Root == null || visual.Hidden ||
                    (uint)roll.FaceIndex >= (uint)visual.Shape.FaceCount) return false;
            if (!hasArena && !Compose()) return false;
            var token = ++generation;
            rolling = true;
            interrupted = false;
            LastRollUsedPlacementFallback = false;
            var timeline = new OwnedDiceRollTimeline { StartedAt = Time.realtimeSinceStartup };
            LastRollTimeline = timeline;
            var shown = false;
            Vector3[] settled = null;
            foreach (var roll in results) visuals[roll.InstanceId].RestFace = roll.FaceIndex;
            try
            {
                if (reduceMotion)
                    shown = await PlaceResultsAsync(reducedMotionDuration, token, cancellationToken);
                else
                {
                    var plan = TryPlanThrow(results, out var reason);
                    if (plan == null)
                    {
                        Debug.LogWarning($"OWNED_DICE_SIMULATION_FALLBACK: {reason} The accepted faces are placed without physics.", this);
                        LastRollUsedPlacementFallback = true;
                        shown = await PlaceResultsAsync(fallbackDuration, token, cancellationToken);
                    }
                    else
                    {
                        timeline.UsedThrow = true;
                        shown = await PlayThrowAsync(plan, timeline, token, cancellationToken);
                    }
                }
                if (!shown) return false;
                EnsureResultFaces(results);
                // Dice added during the roll and a viewport resize are applied now, before the dice are read as still.
                ApplyDeferredLayout();
                timeline.SettledAt = Time.realtimeSinceStartup;
                settled = CapturePositions();
                shown = await HoldAsync(throwSettings.ResultRevealDelay, token, cancellationToken);
                return shown;
            }
            finally
            {
                if (token == generation)
                {
                    rolling = false;
                    if (this != null && isActiveAndEnabled)
                    {
                        // A cancelled or timed-out replay must not leave dice mid-air: show the accepted faces at rest.
                        if (!shown) layoutPending = true;
                        FinishRollLayout();
                        if (shown && settled != null) timeline.ShiftAfterSettle = MaximumShift(settled);
                    }
                }
                timeline.RevealedAt = Time.realtimeSinceStartup;
            }
        }

        // Used when the caller stops waiting (timeout): ends the running presentation at once and leaves every die at
        // rest with its accepted face up, so the caller can apply the result afterwards. Does nothing when idle.
        public void StopRollAtResults()
        {
            if (!rolling) return;
            generation++;
            rolling = false;
            if (this == null || !isActiveAndEnabled) return;
            layoutPending = true;
            FinishRollLayout();
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

        public bool TryGetUpFace(string instanceId, out int faceIndex)
        {
            faceIndex = -1;
            if (instanceId == null || !visuals.TryGetValue(instanceId, out var visual) || visual.Root == null) return false;
            faceIndex = DieFaceGeometry.UpFace(WorldRotation(visual), visual.Shape.FaceNormals, Vector3.up, out _);
            return true;
        }

        // Verification helper: every visible die's renderer bounds project inside the stage camera viewport and
        // no two dice are closer than their resting footprints allow.
        public bool AreAllDiceVisibleAndSeparated(out string detail)
        {
            detail = string.Empty;
            if (stageCamera == null) { detail = "Stage camera is missing."; return false; }
            var active = new List<Visual>();
            foreach (var visual in visuals.Values)
                if (visual.Root != null && !visual.Hidden) active.Add(visual);
            foreach (var visual in active)
            {
                // Mesh vertices, not the renderer's world AABB: a rotated die's AABB reaches far past its real outline.
                var mesh = visual.Shape.ColliderMesh;
                if (mesh == null || !mesh.isReadable) continue;
                var toWorld = visual.Body.localToWorldMatrix;
                foreach (var vertex in mesh.vertices)
                {
                    var viewport = stageCamera.WorldToViewportPoint(toWorld.MultiplyPoint3x4(vertex));
                    if (viewport.x < -0.001f || viewport.x > 1.001f || viewport.y < -0.001f || viewport.y > 1.001f)
                    { detail = $"{visual.Root.name} leaves the dice viewport ({viewport.x:F3}, {viewport.y:F3})."; return false; }
                }
            }
            for (var i = 0; i < active.Count; i++)
                for (var j = 0; j < i; j++)
                {
                    var a = active[i].Root.localPosition; var b = active[j].Root.localPosition;
                    var minimum = (active[i].Shape.RestHeight + active[j].Shape.RestHeight) * DiceArenaLayout.RestSeparationFactor;
                    if (new Vector2(a.x - b.x, a.z - b.z).magnitude < minimum)
                    { detail = $"{active[i].Root.name} overlaps {active[j].Root.name}."; return false; }
                }
            return true;
        }

        private ThrowPlan TryPlanThrow(IReadOnlyList<DieRollDisplay> results, out string reason)
        {
            reason = string.Empty;
            try
            {
                var dice = new Visual[results.Count];
                var bodies = new List<DiceThrowBody>(visuals.Count);
                var participants = new HashSet<Visual>();
                for (var i = 0; i < results.Count; i++)
                {
                    dice[i] = visuals[results[i].InstanceId];
                    participants.Add(dice[i]);
                    bodies.Add(new DiceThrowBody(dice[i].Shape.ColliderMesh, dice[i].Shape.FaceNormals, dice[i].Shape.RestHeight));
                }
                foreach (var visual in visuals.Values)
                    if (!participants.Contains(visual) && visual.Root != null && !visual.Hidden)
                        bodies.Add(new DiceThrowBody(visual.Shape.ColliderMesh, visual.Shape.FaceNormals, visual.Shape.RestHeight,
                            true, visual.Root.localPosition, visual.Root.localRotation));
                var seed = debugThrowSeed != 0 ? debugThrowSeed : seedSource.Next(1, int.MaxValue);
                simulator ??= new DiceThrowSimulator();
                var outcome = simulator.Simulate(bodies, arena, seed, throwSettings);
                LastThrowOutcome = outcome;
                if (throwSettings.LogSeeds && Debug.isDebugBuild)
                    Debug.Log($"OWNED_DICE_THROW: seed={seed} attempts={outcome.Attempts} success={outcome.Success} " +
                        $"simulateMs={outcome.ElapsedMilliseconds:F1} replay={(outcome.Trajectory?.Duration ?? 0f) / throwSettings.PlaybackSpeed:F2}s " +
                        outcome.FailureReason, this);
                if (!outcome.Success) { reason = outcome.FailureReason; return null; }
                var corrections = new Quaternion[dice.Length];
                for (var i = 0; i < dice.Length; i++)
                {
                    var shape = dice[i].Shape;
                    outcome.Trajectory.Final(i, out _, out var finalRotation);
                    if (!DieFaceGeometry.TrySelectCorrection(shape.Symmetries, shape.FaceNormals, shape.FaceReadingUps,
                            outcome.UpFaces[i], results[i].FaceIndex, finalRotation, Vector3.forward, out corrections[i]))
                    { reason = $"{shape.name} has no symmetry for faces {outcome.UpFaces[i] + 1}->{results[i].FaceIndex + 1}."; return null; }
                }
                return new ThrowPlan { Dice = dice, Corrections = corrections, Trajectory = outcome.Trajectory };
            }
            catch (Exception exception)
            {
                Debug.LogError($"OWNED_DICE_SIMULATION_ERROR: {exception}", this);
                reason = exception.Message;
                return null;
            }
        }

        private async Task<bool> PlayThrowAsync(ThrowPlan plan, OwnedDiceRollTimeline timeline, int token,
            CancellationToken cancellationToken)
        {
            var count = plan.Dice.Length;
            // Recall: every die leaves through its nearest screen edge in the same time with the same progress curve.
            var fromPositions = new Vector3[count];
            var fromRotations = new Quaternion[count];
            var restHeights = new float[count];
            for (var i = 0; i < count; i++)
            {
                fromPositions[i] = plan.Dice[i].Root.localPosition;
                fromRotations[i] = plan.Dice[i].Root.localRotation;
                restHeights[i] = plan.Dice[i].Shape.RestHeight;
            }
            var lift = arena.DieRadius * throwSettings.RecallLiftRadii;
            var recallPlan = DiceArenaLayout.PlanRecall(arena, fromPositions, restHeights, lift);
            timeline.RecallEdges = recallPlan.Edges;
            timeline.RecallResolution = recallPlan.Resolution;
            if (recallPlan.Resolution == DiceRecallResolution.CommonBottom)
                Debug.LogWarning("OWNED_DICE_RECALL_FALLBACK: nearest-edge recall paths overlapped; all dice leave below the screen together.", this);
            // Each die rolls toward its own direction of travel (axis = up x direction).
            var tumbleAxes = new Vector3[count];
            for (var i = 0; i < count; i++)
            {
                var travel = recallPlan.Targets[i] - fromPositions[i];
                travel.y = 0f;
                tumbleAxes[i] = travel.sqrMagnitude > 1e-6f ? Vector3.Cross(Vector3.up, travel.normalized) : Vector3.zero;
            }
            var tumble = throwSettings.RecallTumbleDegrees;
            var recall = throwSettings.RecallDuration;
            var start = Time.realtimeSinceStartup;
            while (recall > 0f)
            {
                if (IsStale(token, cancellationToken)) return false;
                var t = Mathf.Clamp01((Time.realtimeSinceStartup - start) / recall);
                var eased = t * t;
                for (var i = 0; i < count; i++)
                {
                    var visual = plan.Dice[i];
                    if (visual.Root == null) continue;
                    visual.Root.localPosition = Vector3.LerpUnclamped(fromPositions[i], recallPlan.Targets[i], eased);
                    visual.Root.localRotation = tumbleAxes[i] == Vector3.zero ? fromRotations[i] :
                        Quaternion.AngleAxis(tumble * eased, tumbleAxes[i]) * fromRotations[i];
                }
                if (t >= 1f) break;
                await Task.Yield();
            }
            if (IsStale(token, cancellationToken)) return false;
            for (var i = 0; i < count; i++)
                if (plan.Dice[i].Root != null) plan.Dice[i].Root.localPosition = recallPlan.Targets[i];
            timeline.RecallEndedAt = Time.realtimeSinceStartup;
            var recalledHidden = AreOffscreen(plan.Dice, "recalled", out var swapDetail);

            // Hidden outside the screen: switch to the launch pose and the new symmetry correction in a single step.
            for (var i = 0; i < count; i++)
            {
                var visual = plan.Dice[i];
                if (visual.Root == null) continue;
                plan.Trajectory.Sample(i, 0f, out var position, out var rotation);
                visual.Root.localPosition = position;
                visual.Root.localRotation = rotation;
                visual.Body.localRotation = plan.Corrections[i];
            }
            var launchHidden = AreOffscreen(plan.Dice, "launch", out var launchDetail);
            timeline.HiddenAtSwap = recalledHidden && launchHidden;
            timeline.SwapDetail = recalledHidden ? launchDetail : swapDetail;

            // Fixed pause with the screen clear of participants before the throw enters.
            if (!await HoldAsync(throwSettings.ThrowDelay, token, cancellationToken)) return false;
            timeline.ThrowStartedAt = Time.realtimeSinceStartup;

            var speed = throwSettings.PlaybackSpeed;
            var replay = plan.Trajectory.Duration / speed;
            start = Time.realtimeSinceStartup;
            while (true)
            {
                if (IsStale(token, cancellationToken)) return false;
                var elapsed = Time.realtimeSinceStartup - start;
                for (var i = 0; i < count; i++)
                {
                    var visual = plan.Dice[i];
                    if (visual.Root == null) continue;
                    plan.Trajectory.Sample(i, elapsed * speed, out var position, out var rotation);
                    visual.Root.localPosition = position;
                    visual.Root.localRotation = rotation;
                }
                if (elapsed >= replay) break;
                await Task.Yield();
            }
            for (var i = 0; i < count; i++)
            {
                if (plan.Dice[i].Root == null) continue;
                plan.Trajectory.Final(i, out var position, out var rotation);
                plan.Dice[i].Root.localPosition = position;
                plan.Dice[i].Root.localRotation = rotation;
                plan.Dice[i].Body.localRotation = plan.Corrections[i];
            }
            return true;
        }

        // Used by reduced motion and simulation fallback: non-overlapping orderly slots, accepted face up.
        private async Task<bool> PlaceResultsAsync(float duration, int token, CancellationToken cancellationToken)
        {
            var ordered = OrderedVisuals();
            var slots = DiceArenaLayout.OrderlyPositions(arena, ordered.Count);
            var fromPositions = new Vector3[ordered.Count];
            var fromRotations = new Quaternion[ordered.Count];
            for (var i = 0; i < ordered.Count; i++)
            {
                fromPositions[i] = ordered[i].Root.localPosition;
                fromRotations[i] = WorldRotation(ordered[i]);
            }
            var start = Time.realtimeSinceStartup;
            while (duration > 0f)
            {
                if (IsStale(token, cancellationToken)) return false;
                var t = Mathf.SmoothStep(0f, 1f, (Time.realtimeSinceStartup - start) / duration);
                for (var i = 0; i < ordered.Count; i++)
                {
                    var visual = ordered[i];
                    if (visual.Root == null) continue;
                    var target = new Vector3(slots[i].x, visual.Shape.RestHeight, slots[i].y);
                    visual.Root.localPosition = Vector3.Lerp(fromPositions[i], target, t);
                    visual.Root.localRotation = Quaternion.Slerp(fromRotations[i], visual.Shape.PoseFor(visual.RestFace), t);
                    visual.Body.localRotation = Quaternion.identity;
                }
                if (t >= 1f) break;
                await Task.Yield();
            }
            if (IsStale(token, cancellationToken)) return false;
            PlaceOrderly(ordered, slots);
            return true;
        }

        private bool IsStale(int token, CancellationToken cancellationToken) =>
            cancellationToken.IsCancellationRequested || token != generation || this == null || !isActiveAndEnabled;

        // Result hold: the dice stay where they stopped so the faces can be read before the result is applied.
        private async Task<bool> HoldAsync(float duration, int token, CancellationToken cancellationToken)
        {
            var start = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - start < duration)
            {
                if (IsStale(token, cancellationToken)) return false;
                await Task.Yield();
            }
            return !IsStale(token, cancellationToken);
        }

        // Applies layout work deferred while rolling (dice added during the roll, viewport size changes).
        private void ApplyDeferredLayout()
        {
            rolling = false;
            try { FinishRollLayout(); }
            finally { rolling = true; }
        }

        // Each bounding sphere must lie entirely outside one of the four edges of the orthographic stage camera's view.
        private bool AreOffscreen(Visual[] dice, string stage, out string detail)
        {
            detail = string.Empty;
            if (stageCamera == null || !stageCamera.orthographic) { detail = "Orthographic stage camera is missing."; return false; }
            var height = stageCamera.orthographicSize * 2f;
            var width = height * stageCamera.aspect;
            foreach (var visual in dice)
            {
                if (visual.Root == null) continue;
                var radius = visual.Shape.BoundingRadius * visual.Root.lossyScale.x;
                var viewport = stageCamera.WorldToViewportPoint(visual.Root.position);
                var rx = radius / width; var ry = radius / height;
                if (viewport.x + rx < 0f || viewport.x - rx > 1f || viewport.y + ry < 0f || viewport.y - ry > 1f) continue;
                detail = $"{visual.Root.name} is visible in the {stage} pose (viewport {viewport.x:F3}, {viewport.y:F3}).";
                return false;
            }
            return true;
        }

        private Vector3[] CapturePositions()
        {
            var ordered = OrderedVisuals();
            var result = new Vector3[ordered.Count];
            for (var i = 0; i < result.Length; i++) result[i] = ordered[i].Root.localPosition;
            return result;
        }

        private float MaximumShift(Vector3[] before)
        {
            var ordered = OrderedVisuals();
            if (ordered.Count != before.Length) return float.PositiveInfinity;
            var shift = 0f;
            for (var i = 0; i < before.Length; i++) shift = Mathf.Max(shift, Vector3.Distance(before[i], ordered[i].Root.localPosition));
            return shift;
        }

        private void EnsureResultFaces(IReadOnlyList<DieRollDisplay> results)
        {
            foreach (var roll in results)
            {
                if (!visuals.TryGetValue(roll.InstanceId, out var visual) || visual.Root == null) continue;
                var shown = DieFaceGeometry.UpFace(WorldRotation(visual), visual.Shape.FaceNormals, Vector3.up, out _);
                if (shown == roll.FaceIndex) continue;
                Debug.LogError($"OWNED_DICE_DISPLAY_MISMATCH: {roll.InstanceId} showed face {shown + 1} instead of {roll.FaceIndex + 1}; correcting the pose.", this);
                visual.Root.localRotation = visual.Shape.PoseFor(roll.FaceIndex);
                visual.Body.localRotation = Quaternion.identity;
            }
        }

        private void FinishRollLayout()
        {
            foreach (var visual in visuals.Values)
            {
                if (!visual.Hidden || visual.Root == null) continue;
                visual.Hidden = false;
                visual.Root.gameObject.SetActive(true);
                layoutPending = true;
            }
            var desired = diceViewport != null ? TargetSize(diceViewport.rectTransform.rect.size) : textureSize;
            if (desired != textureSize) UpdateRenderTarget();
            else Compose();
        }

        // Applies camera, floor and wall bounds for the current viewport aspect and dice count. Orderly placement is used
        // first and after ownership changes; otherwise rested dice keep their pose and are clamped into the new bounds.
        private bool Compose()
        {
            if (rolling || modelRoot == null) return false;
            if (rig == null && stageCamera != null) rig = stageCamera.GetComponentInParent<DiceStageRig>(true);
            if (rig == null || !rig.IsComplete) return false;
            var ordered = OrderedVisuals();
            var radius = 0f;
            foreach (var visual in ordered) radius = Mathf.Max(radius, visual.Shape.BoundingRadius);
            if (radius <= 0f) radius = DefaultDieRadius;
            var aspect = textureSize.x > 0 && textureSize.y > 0 ? textureSize.x / (float)textureSize.y :
                diceViewport != null && diceViewport.rectTransform.rect.height > 0f ?
                    diceViewport.rectTransform.rect.width / diceViewport.rectTransform.rect.height : 1.6f;
            var previous = arena;
            var hadArena = hasArena;
            try { arena = DiceArenaLayout.Compute(aspect, ordered.Count, radius, arenaSettings); }
            catch (Exception exception) { Debug.LogWarning($"Dice arena could not be computed: {exception.Message}", this); return false; }
            hasArena = true;
            rig.ApplyComposition(arena, arenaSettings.CameraDistance);
            // Rested dice keep their pose while the bounds are unchanged; the physics walls already kept them inside.
            if (!layoutPending && !(hadArena && arena.HasSameBounds(previous)))
            {
                var clamped = new Vector2[ordered.Count];
                var restHeights = new float[ordered.Count];
                for (var i = 0; i < ordered.Count; i++)
                {
                    var position = ordered[i].Root.localPosition;
                    clamped[i] = DiceArenaLayout.Clamp(arena, new Vector2(position.x, position.z));
                    restHeights[i] = ordered[i].Shape.RestHeight;
                }
                if (DiceArenaLayout.HasRestOverlap(clamped, restHeights)) layoutPending = true;
                else
                    for (var i = 0; i < ordered.Count; i++)
                        ordered[i].Root.localPosition = new Vector3(clamped[i].x, ordered[i].Root.localPosition.y, clamped[i].y);
            }
            if (layoutPending) PlaceOrderly(ordered, DiceArenaLayout.OrderlyPositions(arena, ordered.Count));
            layoutPending = false;
            return true;
        }

        private void PlaceOrderly(List<Visual> ordered, Vector2[] slots)
        {
            for (var i = 0; i < ordered.Count; i++)
            {
                var visual = ordered[i];
                visual.Root.localPosition = new Vector3(slots[i].x, visual.Shape.RestHeight, slots[i].y);
                visual.Root.localRotation = visual.Shape.PoseFor(Mathf.Clamp(visual.RestFace, 0, visual.Shape.FaceCount - 1));
                visual.Root.localScale = Vector3.one;
                visual.Body.localRotation = Quaternion.identity;
            }
        }

        private List<Visual> OrderedVisuals()
        {
            var result = new List<Visual>(owned.Count);
            foreach (var die in owned)
                if (visuals.TryGetValue(die.InstanceId, out var visual) && visual.Root != null && !visual.Hidden)
                    result.Add(visual);
            return result;
        }

        private static Quaternion WorldRotation(Visual visual) => visual.Root.localRotation * visual.Body.localRotation;

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
            Compose();
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

        public void SetThrowSeedForEditor(int seed) => debugThrowSeed = seed;

        // Verification only: rests the displayed dice (display order) at the given floor positions, showing their last face.
        public void PlaceDiceForEditor(IReadOnlyList<Vector2> positions)
        {
            if (rolling) throw new InvalidOperationException("Dice cannot be placed during a roll.");
            var ordered = OrderedVisuals();
            for (var i = 0; i < ordered.Count && i < positions.Count; i++)
            {
                var visual = ordered[i];
                visual.Root.localPosition = new Vector3(positions[i].x, visual.Shape.RestHeight, positions[i].y);
                visual.Root.localRotation = visual.Shape.PoseFor(Mathf.Clamp(visual.RestFace, 0, visual.Shape.FaceCount - 1));
                visual.Body.localRotation = Quaternion.identity;
            }
        }
        public DiceThrowSettings ThrowSettingsForEditor => throwSettings;
#endif
    }
}
