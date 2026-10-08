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

        // Launch positions packed from the camera-side wall toward the far side.
        public static Vector2[] ThrowStartPositions(DiceArena arena, int count)
        {
            var result = new Vector2[Mathf.Max(0, count)];
            var columns = Mathf.Max(1, arena.Columns);
            for (var i = 0; i < result.Length; i++)
            {
                var row = i / columns;
                var column = i % columns;
                var inRow = Mathf.Min(columns, result.Length - row * columns);
                result[i] = new Vector2((column - (inRow - 1) * 0.5f) * arena.Spacing,
                    Mathf.Min(arena.InnerFarZ, arena.InnerNearZ + row * arena.Spacing));
            }
            return result;
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
