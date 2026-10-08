using System;
using UnityEngine;

namespace TxTRPG.UI.Dice
{
    // Fixed-step recording of every simulated die. Playback samples by elapsed time, so the screen frame rate changes
    // only how often a pose is sampled, never the path or the final pose.
    public sealed class DiceTrajectory
    {
        private readonly Vector3[] positions;
        private readonly Quaternion[] rotations;

        public DiceTrajectory(int dieCount, int frameCount, float step, Vector3[] positions, Quaternion[] rotations)
        {
            if (dieCount <= 0 || frameCount <= 0 || step <= 0f) throw new ArgumentOutOfRangeException(nameof(frameCount));
            if (positions == null || rotations == null || positions.Length < dieCount * frameCount ||
                rotations.Length < dieCount * frameCount) throw new ArgumentException("Trajectory buffers are too small.");
            DieCount = dieCount; FrameCount = frameCount; Step = step;
            this.positions = positions; this.rotations = rotations;
        }

        public int DieCount { get; }
        public int FrameCount { get; }
        public float Step { get; }
        public float Duration => (FrameCount - 1) * Step;

        public void Sample(int die, float time, out Vector3 position, out Quaternion rotation)
        {
            if ((uint)die >= (uint)DieCount) throw new ArgumentOutOfRangeException(nameof(die));
            var frame = Mathf.Clamp(time, 0f, Duration) / Step;
            var index = Mathf.Min(FrameCount - 1, Mathf.FloorToInt(frame));
            var next = Mathf.Min(FrameCount - 1, index + 1);
            var blend = Mathf.Clamp01(frame - index);
            position = Vector3.Lerp(positions[index * DieCount + die], positions[next * DieCount + die], blend);
            rotation = Quaternion.Slerp(rotations[index * DieCount + die], rotations[next * DieCount + die], blend);
        }

        public void Final(int die, out Vector3 position, out Quaternion rotation) => Sample(die, Duration, out position, out rotation);
    }
}
