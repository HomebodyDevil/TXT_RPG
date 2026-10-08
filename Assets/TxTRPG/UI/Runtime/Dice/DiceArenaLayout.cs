using System;
using System.Collections.Generic;
using UnityEngine;

namespace TxTRPG.UI.Dice
{
    [Serializable]
    public sealed class DiceArenaSettings
    {
        [SerializeField, Range(30f, 85f), Tooltip("Downward camera pitch over the dice floor.")]
        private float cameraPitch = 55f;
        [SerializeField, Min(5f)] private float cameraDistance = 60f;
        [SerializeField, Min(0f), Tooltip("Screen-edge padding in world units.")] private float padding = 0.5f;
        [SerializeField, Min(2f), Tooltip("Invisible ceiling height measured in die radii.")] private float ceilingRadii = 3f;
        [SerializeField, Min(2f), Tooltip("Centre spacing of the orderly layout in die radii.")] private float spacingRadii = 2.3f;
        [SerializeField, Min(1f), Tooltip("Free layout slots required per owned die so throws have room to settle.")]
        private float capacityPerDie = 2f;
        [SerializeField, Min(2f), Tooltip("Minimum floor depth and width in die radii.")] private float minimumExtentRadii = 5f;

        public float CameraPitch => Mathf.Clamp(cameraPitch, 30f, 85f);
        public float CameraDistance => Mathf.Max(5f, cameraDistance);
        public float Padding => Mathf.Max(0f, padding);
        public float CeilingRadii => Mathf.Max(2f, ceilingRadii);
        public float SpacingRadii => Mathf.Max(2f, spacingRadii);
        public float CapacityPerDie => Mathf.Max(1f, capacityPerDie);
        public float MinimumExtentRadii => Mathf.Max(2f, minimumExtentRadii);
    }

    // Floor-space box that is fully visible to an orthographic camera pitched down toward +Z.
    // X is horizontal, Y is up, Z points away from the camera. The camera looks at the origin.
    public readonly struct DiceArena
    {
        public DiceArena(float orthographicSize, float aspect, float pitch, float halfWidth, float nearZ, float farZ,
            float ceiling, float dieRadius, float spacing, int columns, int rows)
        {
            OrthographicSize = orthographicSize; Aspect = aspect; Pitch = pitch; HalfWidth = halfWidth;
            NearZ = nearZ; FarZ = farZ; Ceiling = ceiling; DieRadius = dieRadius; Spacing = spacing;
            Columns = columns; Rows = rows;
        }
        public float OrthographicSize { get; }
        public float Aspect { get; }
        public float Pitch { get; }
        public float HalfWidth { get; }
        public float NearZ { get; }
        public float FarZ { get; }
        public float Ceiling { get; }
        public float DieRadius { get; }
        public float Spacing { get; }
        public int Columns { get; }
        public int Rows { get; }
        public bool IsValid => OrthographicSize > 0f && FarZ > NearZ && HalfWidth > 0f;
        public float CenterZ => (NearZ + FarZ) * 0.5f;
        public float Depth => FarZ - NearZ;
        public float InnerHalfWidth => Mathf.Max(0f, HalfWidth - DieRadius);
        public float InnerNearZ => NearZ + DieRadius;
        public float InnerFarZ => FarZ - DieRadius;

        public bool HasSameBounds(DiceArena other) =>
            Mathf.Approximately(OrthographicSize, other.OrthographicSize) && Mathf.Approximately(Aspect, other.Aspect) &&
            Mathf.Approximately(Pitch, other.Pitch) && Mathf.Approximately(HalfWidth, other.HalfWidth) &&
            Mathf.Approximately(NearZ, other.NearZ) && Mathf.Approximately(FarZ, other.FarZ) &&
            Mathf.Approximately(Ceiling, other.Ceiling) && Mathf.Approximately(DieRadius, other.DieRadius);
    }

    // Screen edge a rested die leaves through when a new roll starts. The declaration order is the tie-break order.
    public enum DiceRecallEdge
    {
        Bottom = 0,
        Left = 1,
        Right = 2,
        Top = 3
    }

    public enum DiceRecallResolution
    {
        NearestEdges = 0,
        // A die was moved to its next-nearest edge because its nearest path met another die on screen.
        Reassigned = 1,
        // Per-die paths could not be separated; dice sharing an edge move by one common offset per edge.
        EdgeGroups = 2,
        // Nothing else separated the paths; every die slides by one common offset below the screen.
        CommonBottom = 3
    }

    public sealed class DiceRecallPlan
    {
        public DiceRecallPlan(DiceRecallEdge[] edges, Vector3[] targets, DiceRecallResolution resolution, int reassigned)
        { Edges = edges; Targets = targets; Resolution = resolution; Reassigned = reassigned; }
        public IReadOnlyList<DiceRecallEdge> Edges { get; }
        public IReadOnlyList<Vector3> Targets { get; }
        public DiceRecallResolution Resolution { get; }
        public int Reassigned { get; }
    }

    public static class DiceArenaLayout
    {
        public static DiceArena Compute(float aspect, int dieCount, float dieRadius, DiceArenaSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            aspect = Mathf.Clamp(float.IsNaN(aspect) ? 1f : aspect, 0.2f, 8f);
            dieCount = Mathf.Max(1, dieCount);
            dieRadius = Mathf.Max(0.1f, dieRadius);
            var pitch = settings.CameraPitch * Mathf.Deg2Rad;
            var sin = Mathf.Sin(pitch);
            var cos = Mathf.Cos(pitch);
            var padding = settings.Padding;
            var ceiling = dieRadius * settings.CeilingRadii;
            var spacing = dieRadius * settings.SpacingRadii;
            var minimumExtent = dieRadius * settings.MinimumExtentRadii;
            var required = Mathf.CeilToInt(dieCount * settings.CapacityPerDie);
            var size = Mathf.Max(dieRadius * 2f, padding + ceiling * cos * 0.5f + 0.01f);
            for (var attempt = 0; attempt < 400; attempt++, size *= 1.04f)
            {
                var halfWidth = size * aspect - padding;
                var nearZ = (-size + padding) / sin;
                var farZ = (size - padding - ceiling * cos) / sin;
                if (halfWidth <= dieRadius || farZ - nearZ <= 2f * dieRadius) continue;
                if (farZ - nearZ < minimumExtent || halfWidth * 2f < minimumExtent) continue;
                var columns = Mathf.FloorToInt((2f * (halfWidth - dieRadius)) / spacing) + 1;
                var rows = Mathf.FloorToInt((farZ - nearZ - 2f * dieRadius) / spacing) + 1;
                if (columns * rows < required) continue;
                return new DiceArena(size, aspect, settings.CameraPitch, halfWidth, nearZ, farZ, ceiling, dieRadius,
                    spacing, columns, rows);
            }
            throw new InvalidOperationException($"No dice arena fits {dieCount} dice at aspect {aspect:F2}.");
        }

        // Centered, row-major grid on the floor. Positions are XZ; callers supply each die's resting height.
        public static Vector2[] OrderlyPositions(DiceArena arena, int count)
        {
            var result = new Vector2[Mathf.Max(0, count)];
            if (count <= 0) return result;
            var columns = Mathf.Clamp(count, 1, Mathf.Max(1, arena.Columns));
            var rows = Mathf.CeilToInt(count / (float)columns);
            for (var i = 0; i < count; i++)
            {
                var row = i / columns;
                var column = i % columns;
                var inRow = Mathf.Min(columns, count - row * columns);
                result[i] = new Vector2((column - (inRow - 1) * 0.5f) * arena.Spacing,
                    arena.CenterZ + ((rows - 1) * 0.5f - row) * arena.Spacing);
            }
            return result;
        }

        // Gap kept between a die's bounding sphere and the bottom screen edge when it must be hidden.
        public const float OffscreenMarginRadii = 0.1f;

        // Rest footprints may touch but not overlap: two resting dice are at least their rest heights apart.
        public const float RestSeparationFactor = 0.98f;

        // Vertical orthographic view coordinate of a world point; the view spans [-OrthographicSize, OrthographicSize].
        public static float ScreenHeight(DiceArena arena, float y, float z)
        {
            var pitch = arena.Pitch * Mathf.Deg2Rad;
            return y * Mathf.Cos(pitch) + z * Mathf.Sin(pitch);
        }

        // True when a sphere around the point is entirely below the bottom screen edge (toward the camera).
        public static bool IsBelowScreen(DiceArena arena, Vector3 center, float radius) =>
            ScreenHeight(arena, center.y, center.z) + radius < -arena.OrthographicSize;

        // Largest Z at which a sphere at the given height is still entirely below the bottom screen edge.
        public static float BelowScreenZ(DiceArena arena, float height, float radius)
        {
            var pitch = arena.Pitch * Mathf.Deg2Rad;
            return (-arena.OrthographicSize - radius - height * Mathf.Cos(pitch)) / Mathf.Sin(pitch);
        }

        // Launch positions below the bottom screen edge. Columns stay inside the side walls; further rows are placed
        // further toward the camera so no two bounding spheres overlap.
        public static Vector2[] OffscreenStartPositions(DiceArena arena, int count, float height)
        {
            var result = new Vector2[Mathf.Max(0, count)];
            var columns = Mathf.Max(1, arena.Columns);
            var frontZ = BelowScreenZ(arena, height, arena.DieRadius) - arena.DieRadius * OffscreenMarginRadii;
            for (var i = 0; i < result.Length; i++)
            {
                var row = i / columns;
                var column = i % columns;
                var inRow = Mathf.Min(columns, result.Length - row * columns);
                result[i] = new Vector2((column - (inRow - 1) * 0.5f) * arena.Spacing, frontZ - row * arena.Spacing);
            }
            return result;
        }

        // One Z offset for all dice, applied together with an upward lift, that hides every die below the bottom
        // screen edge. A shared rigid translation keeps the dice from crossing each other while they leave.
        public static float RecallOffsetZ(DiceArena arena, IReadOnlyList<Vector3> positions, float lift)
        {
            var offset = 0f;
            var margin = arena.DieRadius * OffscreenMarginRadii;
            foreach (var position in positions)
                offset = Mathf.Min(offset, BelowScreenZ(arena, position.y + lift, arena.DieRadius) - margin - position.z);
            return offset;
        }

        // Number of evenly spaced progress samples used to check recall paths for overlaps.
        public const int RecallOverlapSamples = 48;

        public static bool IsAboveScreen(DiceArena arena, Vector3 center, float radius) =>
            ScreenHeight(arena, center.y, center.z) - radius > arena.OrthographicSize;

        public static bool IsOffscreen(DiceArena arena, Vector3 center, float radius) =>
            IsBelowScreen(arena, center, radius) || IsAboveScreen(arena, center, radius) ||
            Mathf.Abs(center.x) - radius > arena.OrthographicSize * arena.Aspect;

        // Smallest Z at which a sphere at the given height is entirely above the top screen edge.
        public static float AboveScreenZ(DiceArena arena, float height, float radius)
        {
            var pitch = arena.Pitch * Mathf.Deg2Rad;
            return (arena.OrthographicSize + radius - height * Mathf.Cos(pitch)) / Mathf.Sin(pitch);
        }

        // Screen distances from a point to the four edges, indexed by DiceRecallEdge.
        public static float[] EdgeDistances(DiceArena arena, Vector3 position)
        {
            var halfWidth = arena.OrthographicSize * arena.Aspect;
            var height = ScreenHeight(arena, position.y, position.z);
            return new[]
            {
                height + arena.OrthographicSize, position.x + halfWidth, halfWidth - position.x,
                arena.OrthographicSize - height
            };
        }

        // Edges ordered by screen distance; equal distances keep the Bottom, Left, Right, Top order.
        public static DiceRecallEdge[] EdgesByDistance(DiceArena arena, Vector3 position)
        {
            var distances = EdgeDistances(arena, position);
            var edges = new[] { DiceRecallEdge.Bottom, DiceRecallEdge.Left, DiceRecallEdge.Right, DiceRecallEdge.Top };
            Array.Sort(edges, (a, b) =>
            {
                var difference = distances[(int)a] - distances[(int)b];
                return Mathf.Abs(difference) <= 1e-4f ? ((int)a).CompareTo((int)b) : difference < 0f ? -1 : 1;
            });
            return edges;
        }

        public static DiceRecallEdge NearestEdge(DiceArena arena, Vector3 position) => EdgesByDistance(arena, position)[0];

        // Nearest position outside the given edge, moving only perpendicular to it: X for Left and Right, Z plus the
        // lift for Bottom and Top. The whole bounding sphere ends up outside with the usual margin.
        public static Vector3 RecallTarget(DiceArena arena, Vector3 position, DiceRecallEdge edge, float lift)
        {
            var radius = arena.DieRadius;
            var margin = radius * OffscreenMarginRadii;
            var halfWidth = arena.OrthographicSize * arena.Aspect;
            var lifted = position.y + lift;
            switch (edge)
            {
                case DiceRecallEdge.Left:
                    return new Vector3(Mathf.Min(position.x, -halfWidth - radius - margin), position.y, position.z);
                case DiceRecallEdge.Right:
                    return new Vector3(Mathf.Max(position.x, halfWidth + radius + margin), position.y, position.z);
                case DiceRecallEdge.Top:
                    return new Vector3(position.x, lifted, Mathf.Max(position.z, AboveScreenZ(arena, lifted, radius) + margin));
                default:
                    return new Vector3(position.x, lifted, Mathf.Min(position.z, BelowScreenZ(arena, lifted, radius) - margin));
            }
        }

        // Chooses each die's recall edge. Every die moves along a straight path with the same progress curve, so two
        // dice are compared at equal progress; a pair conflicts while either is on screen and their centres are closer
        // than their rest footprints. Dice are assigned in order, each taking its nearest edge whose path meets no
        // earlier die. If a die has no such edge, dice sharing an edge move by one common offset per edge (no crossing
        // inside a group); if groups still meet, every die uses the common downward recall.
        public static DiceRecallPlan PlanRecall(DiceArena arena, IReadOnlyList<Vector3> positions,
            IReadOnlyList<float> restHeights, float lift)
        {
            var count = positions.Count;
            var edges = new DiceRecallEdge[count];
            var targets = new Vector3[count];
            var reassigned = 0;
            var separated = true;
            for (var i = 0; i < count && separated; i++)
            {
                separated = false;
                var options = EdgesByDistance(arena, positions[i]);
                for (var option = 0; option < options.Length; option++)
                {
                    edges[i] = options[option];
                    targets[i] = RecallTarget(arena, positions[i], edges[i], lift);
                    if (FirstRecallConflict(arena, positions, targets, restHeights, i + 1, i) >= 0) continue;
                    if (option > 0) reassigned++;
                    separated = true;
                    break;
                }
            }
            if (separated)
                return new DiceRecallPlan(edges, targets, reassigned > 0 ? DiceRecallResolution.Reassigned : DiceRecallResolution.NearestEdges, reassigned);

            for (var i = 0; i < count; i++) edges[i] = NearestEdge(arena, positions[i]);
            GroupTargets(arena, positions, edges, lift, targets);
            if (FirstRecallConflict(arena, positions, targets, restHeights, count) < 0)
                return new DiceRecallPlan(edges, targets, DiceRecallResolution.EdgeGroups, 0);

            var offset = RecallOffsetZ(arena, positions, lift);
            for (var i = 0; i < count; i++)
            {
                edges[i] = DiceRecallEdge.Bottom;
                targets[i] = positions[i] + new Vector3(0f, lift, offset);
            }
            return new DiceRecallPlan(edges, targets, DiceRecallResolution.CommonBottom, 0);
        }

        // Index of the first die (among the first `count`, starting at `firstIndex`) whose path meets an earlier die's
        // path on screen, or -1.
        public static int FirstRecallConflict(DiceArena arena, IReadOnlyList<Vector3> from, IReadOnlyList<Vector3> to,
            IReadOnlyList<float> restHeights, int count, int firstIndex = 1)
        {
            for (var sample = 0; sample <= RecallOverlapSamples; sample++)
            {
                var t = sample / (float)RecallOverlapSamples;
                for (var i = Mathf.Max(1, firstIndex); i < count; i++)
                {
                    var a = Vector3.LerpUnclamped(from[i], to[i], t);
                    var aHidden = IsOffscreen(arena, a, arena.DieRadius);
                    for (var j = 0; j < i; j++)
                    {
                        var b = Vector3.LerpUnclamped(from[j], to[j], t);
                        if (aHidden && IsOffscreen(arena, b, arena.DieRadius)) continue;
                        var minimum = (restHeights[i] + restHeights[j]) * RestSeparationFactor;
                        if ((a - b).sqrMagnitude < minimum * minimum) return i;
                    }
                }
            }
            return -1;
        }

        private static void GroupTargets(DiceArena arena, IReadOnlyList<Vector3> positions, DiceRecallEdge[] edges,
            float lift, Vector3[] targets)
        {
            var offsets = new float[4];
            var used = new bool[4];
            for (var i = 0; i < positions.Count; i++)
            {
                var edge = (int)edges[i];
                var target = RecallTarget(arena, positions[i], edges[i], lift);
                var needed = edges[i] == DiceRecallEdge.Left || edges[i] == DiceRecallEdge.Right
                    ? target.x - positions[i].x : target.z - positions[i].z;
                var further = edges[i] == DiceRecallEdge.Right || edges[i] == DiceRecallEdge.Top
                    ? Mathf.Max(offsets[edge], needed) : Mathf.Min(offsets[edge], needed);
                offsets[edge] = used[edge] ? further : needed;
                used[edge] = true;
            }
            for (var i = 0; i < positions.Count; i++)
            {
                var offset = offsets[(int)edges[i]];
                targets[i] = edges[i] == DiceRecallEdge.Left || edges[i] == DiceRecallEdge.Right
                    ? positions[i] + new Vector3(offset, 0f, 0f)
                    : positions[i] + new Vector3(0f, lift, offset);
            }
        }

        public static Vector2 Clamp(DiceArena arena, Vector2 position) =>
            new(Mathf.Clamp(position.x, -arena.InnerHalfWidth, arena.InnerHalfWidth),
                Mathf.Clamp(position.y, arena.InnerNearZ, Mathf.Max(arena.InnerNearZ, arena.InnerFarZ)));

        public static bool HasOverlap(IReadOnlyList<Vector2> positions, float minimumDistance)
        {
            for (var i = 0; i < positions.Count; i++)
                for (var j = 0; j < i; j++)
                    if ((positions[i] - positions[j]).sqrMagnitude < minimumDistance * minimumDistance) return true;
            return false;
        }

        // Overlap of resting dice: closer than their rest footprints allow (dice touching after a throw do not count).
        public static bool HasRestOverlap(IReadOnlyList<Vector2> positions, IReadOnlyList<float> restHeights)
        {
            for (var i = 0; i < positions.Count; i++)
                for (var j = 0; j < i; j++)
                {
                    var minimum = (restHeights[i] + restHeights[j]) * RestSeparationFactor;
                    if ((positions[i] - positions[j]).sqrMagnitude < minimum * minimum) return true;
                }
            return false;
        }

        // Screen-space check used by tests: every point of the box [±HalfWidth] x [0, Ceiling] x [NearZ, FarZ]
        // must be inside the orthographic view.
        public static bool IsBoxVisible(DiceArena arena)
        {
            var pitch = arena.Pitch * Mathf.Deg2Rad;
            var top = arena.FarZ * Mathf.Sin(pitch) + arena.Ceiling * Mathf.Cos(pitch);
            var bottom = arena.NearZ * Mathf.Sin(pitch);
            return arena.HalfWidth <= arena.OrthographicSize * arena.Aspect + 0.0001f &&
                top <= arena.OrthographicSize + 0.0001f && bottom >= -arena.OrthographicSize - 0.0001f;
        }
    }
}
