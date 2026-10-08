using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace TxTRPG.UI.Dice
{
    public readonly struct DiceThrowBody
    {
        public DiceThrowBody(Mesh colliderMesh, IReadOnlyList<Vector3> faceNormals, float restHeight,
            bool isObstacle = false, Vector3 position = default, Quaternion rotation = default)
        {
            ColliderMesh = colliderMesh; FaceNormals = faceNormals; RestHeight = restHeight; IsObstacle = isObstacle;
            Position = position; Rotation = rotation == default ? Quaternion.identity : rotation;
        }
        public Mesh ColliderMesh { get; }
        public IReadOnlyList<Vector3> FaceNormals { get; }
        public float RestHeight { get; }
        public bool IsObstacle { get; }
        public Vector3 Position { get; }
        public Quaternion Rotation { get; }
    }

    public sealed class DiceThrowOutcome
    {
        public bool Success { get; internal set; }
        public DiceTrajectory Trajectory { get; internal set; }
        public int[] UpFaces { get; internal set; } = Array.Empty<int>();
        public int Seed { get; internal set; }
        public int Attempts { get; internal set; }
        public string FailureReason { get; internal set; } = string.Empty;
        public double ElapsedMilliseconds { get; internal set; }
    }

    // Owns an isolated physics Scene (LocalPhysicsMode.Physics3D). Non-default physics scenes are never simulated by
    // Unity automatically, so only Simulate advances it and the project's default physics scene is untouched.
    public sealed class DiceThrowSimulator : IDisposable
    {
        private const float WallThickness = 4f;
        private static int sceneCounter;
        private Scene scene;
        private PhysicsMaterial material;
        private GameObject root;

        public bool HasScene => scene.IsValid() && scene.isLoaded;

        public DiceThrowOutcome Simulate(IReadOnlyList<DiceThrowBody> bodies, DiceArena arena, int seed,
            DiceThrowSettings settings)
        {
            if (bodies == null || bodies.Count == 0) throw new ArgumentException("At least one die is required.", nameof(bodies));
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (!arena.IsValid) throw new ArgumentException("The dice arena is invalid.", nameof(arena));
            foreach (var body in bodies)
                if (body.ColliderMesh == null || body.FaceNormals == null || body.FaceNormals.Count == 0)
                    throw new ArgumentException("Every die needs a collider mesh and face normals.", nameof(bodies));
            var watch = Stopwatch.StartNew();
            var outcome = new DiceThrowOutcome { Seed = seed };
            try
            {
                for (var attempt = 0; attempt < settings.MaximumAttempts; attempt++)
                {
                    outcome.Attempts = attempt + 1;
                    if (TryRun(bodies, arena, unchecked(seed + attempt * 7919), settings, out var trajectory,
                            out var faces, out var failure))
                    {
                        outcome.Success = true;
                        outcome.Trajectory = trajectory;
                        outcome.UpFaces = faces;
                        outcome.FailureReason = string.Empty;
                        return outcome;
                    }
                    outcome.FailureReason = failure;
                }
                return outcome;
            }
            finally
            {
                DestroyRoot();
                outcome.ElapsedMilliseconds = watch.Elapsed.TotalMilliseconds;
            }
        }

        public void Dispose()
        {
            DestroyRoot();
            if (HasScene) SceneManager.UnloadSceneAsync(scene);
            scene = default;
            if (material != null) Object.Destroy(material);
            material = null;
        }

        private bool TryRun(IReadOnlyList<DiceThrowBody> bodies, DiceArena arena, int seed, DiceThrowSettings settings,
            out DiceTrajectory trajectory, out int[] faces, out string failure)
        {
            trajectory = null;
            faces = new int[bodies.Count];
            failure = string.Empty;
            EnsureScene(settings);
            DestroyRoot();
            root = new GameObject("OwnedDicePhysicsRoot");
            SceneManager.MoveGameObjectToScene(root, scene);
            BuildBounds(arena);

            var random = new System.Random(seed);
            var dynamicCount = 0;
            foreach (var body in bodies) if (!body.IsObstacle) dynamicCount++;
            var starts = DiceArenaLayout.ThrowStartPositions(arena, dynamicCount);
            var rigidbodies = new Rigidbody[bodies.Count];
            for (int i = 0, launched = 0; i < bodies.Count; i++)
            {
                var body = bodies[i];
                var proxy = new GameObject($"DieProxy-{i}");
                proxy.transform.SetParent(root.transform, false);
                Vector3 position;
                Quaternion rotation;
                if (body.IsObstacle) { position = body.Position; rotation = body.Rotation; }
                else
                {
                    var start = starts[launched++];
                    var height = Mathf.Max(body.RestHeight + arena.DieRadius * 0.2f, arena.Ceiling - arena.DieRadius * 1.05f);
                    position = new Vector3(start.x, height, start.y);
                    rotation = RandomRotation(random);
                }
                proxy.transform.SetPositionAndRotation(position, rotation);
                var collider = proxy.AddComponent<MeshCollider>();
                collider.convex = true;
                collider.sharedMesh = body.ColliderMesh;
                collider.sharedMaterial = material;
                var rigidbody = proxy.AddComponent<Rigidbody>();
                rigidbody.useGravity = false;
                rigidbody.mass = 1f;
                rigidbody.linearDamping = settings.LinearDamping;
                rigidbody.angularDamping = settings.AngularDamping;
                rigidbody.maxAngularVelocity = 60f;
                rigidbody.solverIterations = 12;
                rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                rigidbody.isKinematic = body.IsObstacle;
                if (!body.IsObstacle)
                {
                    var forward = Range(random, settings.ForwardSpeed);
                    var sideways = ((float)random.NextDouble() * 2f - 1f) * settings.SidewaysSpread * forward;
                    rigidbody.linearVelocity = new Vector3(sideways, Range(random, settings.UpwardSpeed), forward);
                    rigidbody.angularVelocity = RandomUnit(random) * Range(random, settings.SpinSpeed);
                }
                rigidbodies[i] = rigidbody;
            }
            Physics.SyncTransforms();

            var physics = scene.GetPhysicsScene();
            var step = settings.SimulationStep;
            var maximumFrames = Mathf.CeilToInt(settings.MaximumSimulationSeconds / step) + 1;
            var count = bodies.Count;
            var positions = new Vector3[maximumFrames * count];
            var rotations = new Quaternion[maximumFrames * count];
            Record(rigidbodies, 0, positions, rotations);
            var holdFrames = Mathf.Max(1, Mathf.CeilToInt(settings.SettleHoldSeconds / step));
            var stillFrames = 0;
            var settledFrame = -1;
            for (var frame = 1; frame < maximumFrames; frame++)
            {
                for (var i = 0; i < count; i++)
                    if (!bodies[i].IsObstacle)
                        rigidbodies[i].AddForce(Vector3.down * settings.Gravity, ForceMode.Acceleration);
                physics.Simulate(step);
                Record(rigidbodies, frame, positions, rotations);
                var still = true;
                for (var i = 0; i < count && still; i++)
                    still = bodies[i].IsObstacle ||
                        rigidbodies[i].linearVelocity.magnitude <= settings.SettleLinearSpeed &&
                        rigidbodies[i].angularVelocity.magnitude <= settings.SettleAngularSpeed;
                stillFrames = still ? stillFrames + 1 : 0;
                if (stillFrames >= holdFrames) { settledFrame = frame - stillFrames + 1; break; }
            }
            if (settledFrame < 0)
            {
                failure = $"Dice did not settle within {settings.MaximumSimulationSeconds:F2}s.";
                return false;
            }

            var flat = Mathf.Cos(settings.FlatToleranceDegrees * Mathf.Deg2Rad);
            for (var i = 0; i < count; i++)
            {
                if (bodies[i].IsObstacle) { faces[i] = -1; continue; }
                var rigidbody = rigidbodies[i];
                faces[i] = DieFaceGeometry.UpFace(rigidbody.rotation, bodies[i].FaceNormals, Vector3.up, out var alignment);
                if (alignment < flat)
                {
                    failure = $"Die {i + 1} came to rest tilted by {Mathf.Acos(Mathf.Clamp(alignment, -1f, 1f)) * Mathf.Rad2Deg:F1} degrees.";
                    return false;
                }
                if (Mathf.Abs(rigidbody.position.y - bodies[i].RestHeight) > settings.FloorHeightTolerance)
                {
                    failure = $"Die {i + 1} is not resting on the floor (height {rigidbody.position.y:F3}).";
                    return false;
                }
                // The final pose replaces the first still frame so the hold period is not replayed.
                positions[settledFrame * count + i] = rigidbody.position;
                rotations[settledFrame * count + i] = rigidbody.rotation;
            }
            trajectory = new DiceTrajectory(count, settledFrame + 1, step, positions, rotations);
            return true;
        }

        private void BuildBounds(DiceArena arena)
        {
            var width = arena.HalfWidth * 2f + WallThickness * 2f;
            var depth = arena.Depth + WallThickness * 2f;
            var height = arena.Ceiling + WallThickness * 2f;
            var halfThickness = WallThickness * 0.5f;
            Box("Floor", new Vector3(0f, -halfThickness, arena.CenterZ), new Vector3(width, WallThickness, depth));
            Box("Ceiling", new Vector3(0f, arena.Ceiling + halfThickness, arena.CenterZ), new Vector3(width, WallThickness, depth));
            Box("WallLeft", new Vector3(-arena.HalfWidth - halfThickness, arena.Ceiling * 0.5f, arena.CenterZ),
                new Vector3(WallThickness, height, depth));
            Box("WallRight", new Vector3(arena.HalfWidth + halfThickness, arena.Ceiling * 0.5f, arena.CenterZ),
                new Vector3(WallThickness, height, depth));
            Box("WallNear", new Vector3(0f, arena.Ceiling * 0.5f, arena.NearZ - halfThickness), new Vector3(width, height, WallThickness));
            Box("WallFar", new Vector3(0f, arena.Ceiling * 0.5f, arena.FarZ + halfThickness), new Vector3(width, height, WallThickness));
        }

        private void Box(string name, Vector3 center, Vector3 size)
        {
            var box = new GameObject(name).AddComponent<BoxCollider>();
            box.transform.SetParent(root.transform, false);
            box.transform.localPosition = center;
            box.size = size;
            box.sharedMaterial = material;
        }

        private void EnsureScene(DiceThrowSettings settings)
        {
            if (!HasScene)
                scene = SceneManager.CreateScene($"OwnedDicePhysics-{++sceneCounter}",
                    new CreateSceneParameters(LocalPhysicsMode.Physics3D));
            if (material == null)
                material = new PhysicsMaterial("OwnedDicePhysics")
                { bounceCombine = PhysicsMaterialCombine.Average, frictionCombine = PhysicsMaterialCombine.Average };
            material.bounciness = settings.Bounciness;
            material.dynamicFriction = settings.Friction;
            material.staticFriction = settings.Friction;
        }

        private void DestroyRoot()
        {
            if (root != null) Object.DestroyImmediate(root);
            root = null;
        }

        private static void Record(Rigidbody[] bodies, int frame, Vector3[] positions, Quaternion[] rotations)
        {
            for (var i = 0; i < bodies.Length; i++)
            {
                positions[frame * bodies.Length + i] = bodies[i].position;
                rotations[frame * bodies.Length + i] = bodies[i].rotation;
            }
        }

        private static float Range(System.Random random, Vector2 range) =>
            Mathf.Lerp(range.x, range.y, (float)random.NextDouble());

        private static Vector3 RandomUnit(System.Random random)
        {
            var z = (float)random.NextDouble() * 2f - 1f;
            var angle = (float)random.NextDouble() * Mathf.PI * 2f;
            var radius = Mathf.Sqrt(1f - z * z);
            return new Vector3(radius * Mathf.Cos(angle), radius * Mathf.Sin(angle), z);
        }

        private static Quaternion RandomRotation(System.Random random)
        {
            var u1 = (float)random.NextDouble();
            var u2 = (float)random.NextDouble() * Mathf.PI * 2f;
            var u3 = (float)random.NextDouble() * Mathf.PI * 2f;
            var a = Mathf.Sqrt(1f - u1);
            var b = Mathf.Sqrt(u1);
            return new Quaternion(a * Mathf.Sin(u2), a * Mathf.Cos(u2), b * Mathf.Sin(u3), b * Mathf.Cos(u3));
        }
    }
}
