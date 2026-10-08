using System;
using UnityEngine;

namespace TxTRPG.UI.Dice
{
    // Presentation-only tuning. None of these values can change the accepted roll result.
    [Serializable]
    public sealed class DiceThrowSettings
    {
        [Header("Simulation")]
        [SerializeField, Range(0.002f, 0.04f)] private float simulationStep = 0.01f;
        [SerializeField, Range(0.5f, 6f)] private float maximumSimulationSeconds = 2.2f;
        [SerializeField, Range(1, 10)] private int maximumAttempts = 5;
        [SerializeField, Min(1f)] private float gravity = 40f;
        [Header("Settling")]
        [SerializeField, Min(0.001f)] private float settleLinearSpeed = 0.06f;
        [SerializeField, Min(0.001f)] private float settleAngularSpeed = 0.12f;
        [SerializeField, Min(0f)] private float settleHoldSeconds = 0.18f;
        [SerializeField, Range(0.5f, 15f), Tooltip("Maximum angle between the upward face normal and world up.")]
        private float flatToleranceDegrees = 3f;
        [SerializeField, Min(0.001f)] private float floorHeightTolerance = 0.06f;
        [Header("Throw")]
        [SerializeField] private Vector2 forwardSpeed = new(9f, 15f);
        [SerializeField, Range(0f, 1f)] private float sidewaysSpread = 0.35f;
        [SerializeField] private Vector2 upwardSpeed = new(1f, 5f);
        [SerializeField] private Vector2 spinSpeed = new(10f, 24f);
        [Header("Materials")]
        [SerializeField, Range(0f, 1f)] private float bounciness = 0.3f;
        [SerializeField, Range(0f, 2f)] private float friction = 0.45f;
        [SerializeField, Min(0f)] private float linearDamping = 0.05f;
        [SerializeField, Min(0f)] private float angularDamping = 0.6f;
        [Header("Playback")]
        [SerializeField, Range(0.25f, 4f)] private float playbackSpeed = 1.1f;
        [SerializeField, Range(0f, 1f)] private float liftDuration = 0.16f;
        [SerializeField, Tooltip("Logs the presentation seed in the Editor and development builds.")]
        private bool logSeeds = true;

        public float SimulationStep => Mathf.Clamp(simulationStep, 0.002f, 0.04f);
        public float MaximumSimulationSeconds => Mathf.Clamp(maximumSimulationSeconds, 0.5f, 6f);
        public int MaximumAttempts => Mathf.Clamp(maximumAttempts, 1, 10);
        public float Gravity => Mathf.Max(1f, gravity);
        public float SettleLinearSpeed => Mathf.Max(0.001f, settleLinearSpeed);
        public float SettleAngularSpeed => Mathf.Max(0.001f, settleAngularSpeed);
        public float SettleHoldSeconds => Mathf.Max(0f, settleHoldSeconds);
        public float FlatToleranceDegrees => Mathf.Clamp(flatToleranceDegrees, 0.5f, 15f);
        public float FloorHeightTolerance => Mathf.Max(0.001f, floorHeightTolerance);
        public Vector2 ForwardSpeed => Ordered(forwardSpeed);
        public float SidewaysSpread => Mathf.Clamp01(sidewaysSpread);
        public Vector2 UpwardSpeed => Ordered(upwardSpeed);
        public Vector2 SpinSpeed => Ordered(spinSpeed);
        public float Bounciness => Mathf.Clamp01(bounciness);
        public float Friction => Mathf.Clamp(friction, 0f, 2f);
        public float LinearDamping => Mathf.Max(0f, linearDamping);
        public float AngularDamping => Mathf.Max(0f, angularDamping);
        public float PlaybackSpeed => Mathf.Clamp(playbackSpeed, 0.25f, 4f);
        public float LiftDuration => Mathf.Clamp01(liftDuration);
        public bool LogSeeds => logSeeds;
        public float MaximumPresentationSeconds => LiftDuration + MaximumSimulationSeconds / PlaybackSpeed;

#if UNITY_EDITOR
        public void SetSimulationLimitsForEditor(float seconds, int attempts)
        { maximumSimulationSeconds = seconds; maximumAttempts = attempts; }
#endif

        private static Vector2 Ordered(Vector2 value) =>
            new(Mathf.Max(0f, Mathf.Min(value.x, value.y)), Mathf.Max(0f, Mathf.Max(value.x, value.y)));
    }
}
