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
        // Measurements of the successful attempt: when the camera-side wall closed, the smallest gap between a die's
        // bounding sphere and the ceiling before that, and the pose change from the last replayed frame to the final rest.
        public float EntrySeconds { get; internal set; }
        public float EntryCeilingClearance { get; internal set; }
        public float FinalSnapDistance { get; internal set; }
        public float FinalSnapDegrees { get; internal set; }
    }

    // Owns an isolated physics Scene (LocalPhysicsMode.Physics3D). Non-default physics scenes are never simulated by
    // Unity automatically, so only Simulate advances it and the project's default physics scene is untouched.
    // Dice launch below the bottom screen edge inside a corridor (floor, ceiling and side walls extended toward the
    // camera). Dice beyond the first row wait hidden (kinematic) behind the launch line and are released from that line
    // in waves once the previous row has moved one spacing ahead, so every die travels the same distance. The
    // camera-side wall ignores each die until that die is fully inside, then blocks it from flying back out.
    public sealed class DiceThrowSimulator : IDisposable
    {
        private const float WallThickness = 4f;
        private static int sceneCounter;
        private Scene scene;
        private PhysicsMaterial material;
        private GameObject root;
        private Collider nearWallCollider;

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
                    if (TryRun(bodies, arena, unchecked(seed + attempt * 7919), settings, outcome, out var trajectory,
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
            DiceThrowOutcome outcome, out DiceTrajectory trajectory, out int[] faces, out string failure)
        {
            trajectory = null;
            faces = new int[bodies.Count];
            failure = string.Empty;
            EnsureScene(settings);
            DestroyRoot();
            root = new GameObject("OwnedDicePhysicsRoot");
            SceneManager.MoveGameObjectToScene(root, scene);

            var random = new System.Random(seed);
            var dynamicCount = 0;
            var lowestRest = float.MaxValue;
            foreach (var body in bodies)
                if (!body.IsObstacle) { dynamicCount++; lowestRest = Mathf.Min(lowestRest, body.RestHeight); }
            var radius = arena.DieRadius;
            var launchHeight = Mathf.Max(lowestRest + radius * 0.2f, radius * settings.LaunchHeightRadii);
            var starts = DiceArenaLayout.OffscreenStartPositions(arena, dynamicCount, launchHeight);
            var corridorZ = arena.NearZ;
            foreach (var start in starts) corridorZ = Mathf.Min(corridorZ, start.y - radius * 1.5f);
            BuildBounds(arena, corridorZ);
            var columns = Mathf.Max(1, arena.Columns);
            var launchZ = starts.Length > 0 ? starts[0].y : 0f;
            var rows = new int[bodies.Count];
            var launchVelocities = new Vector3[bodies.Count];
            var launchSpins = new Vector3[bodies.Count];
            var rigidbodies = new Rigidbody[bodies.Count];
            var speedScale = settings.ForwardSpeedScale(arena);
            for (int i = 0, launched = 0; i < bodies.Count; i++)
            {
                var body = bodies[i];
                var proxy = new GameObject($"DieProxy-{i}");
                proxy.transform.SetParent(root.transform, false);
                Vector3 position;
                Quaternion rotation;
                rows[i] = -1;
                if (body.IsObstacle) { position = body.Position; rotation = body.Rotation; }
                else
                {
                    rows[i] = launched / columns;
                    var start = starts[launched++];
                    position = new Vector3(start.x, launchHeight, start.y);
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
                // Later rows wait without moving until their wave is released.
                rigidbody.isKinematic = body.IsObstacle || rows[i] > 0;
                if (!body.IsObstacle)
                {
                    var forward = Range(random, settings.ForwardSpeed) * speedScale;
                    var sideways = ((float)random.NextDouble() * 2f - 1f) * settings.SidewaysSpread * forward;
                    launchVelocities[i] = new Vector3(sideways, Range(random, settings.UpwardSpeed), forward);
                    launchSpins[i] = RandomUnit(random) * Range(random, settings.SpinSpeed);
                    if (rows[i] == 0) { rigidbody.linearVelocity = launchVelocities[i]; rigidbody.angularVelocity = launchSpins[i]; }
                    Physics.IgnoreCollision(collider, nearWallCollider, true);
                }
                rigidbodies[i] = rigidbody;
            }
            Physics.SyncTransforms();
            var lastRow = 0;
            foreach (var row in rows) lastRow = Mathf.Max(lastRow, row);
            var vertices = new Vector3[bodies.Count][];
            for (var i = 0; i < bodies.Count; i++) vertices[i] = bodies[i].IsObstacle ? null : bodies[i].ColliderMesh.vertices;
            var releasedRow = 0;
            var lastReleaseFrame = 0;

            var physics = scene.GetPhysicsScene();
            var step = settings.SimulationStep;
            var maximumFrames = Mathf.CeilToInt(settings.MaximumSimulationSeconds / step) + 1;
            var count = bodies.Count;
            var positions = new Vector3[maximumFrames * count];
            var rotations = new Quaternion[maximumFrames * count];
            Record(rigidbodies, 0, positions, rotations);
            var holdFrames = Mathf.Max(1, Mathf.CeilToInt(settings.SettleHoldSeconds / step));
            var entryFrames = Mathf.CeilToInt(settings.MaximumEntrySeconds / step);
            var entered = new bool[count];
            var wallClosed = false;
            var ceilingClearance = float.MaxValue;
            var stillFrames = 0;
            var settledFrame = -1;
            for (var frame = 1; frame < maximumFrames; frame++)
            {
                if (releasedRow < lastRow && RowCleared(rigidbodies, rows, entered, releasedRow, launchZ + arena.Spacing, radius, arena.NearZ))
                {
                    // Next wave: move to the launch line (still below the screen) and throw with the pre-drawn velocity.
                    releasedRow++;
                    lastReleaseFrame = frame;
                    for (var i = 0; i < count; i++)
                    {
                        if (rows[i] != releasedRow) continue;
                        var rigidbody = rigidbodies[i];
                        rigidbody.isKinematic = false;
                        rigidbody.position = new Vector3(rigidbody.position.x, launchHeight, launchZ);
                        rigidbody.linearVelocity = launchVelocities[i];
                        rigidbody.angularVelocity = launchSpins[i];
                    }
                }
                for (var i = 0; i < count; i++)
                    if (!bodies[i].IsObstacle && !rigidbodies[i].isKinematic)
                        rigidbodies[i].AddForce(Vector3.down * settings.Gravity, ForceMode.Acceleration);
                physics.Simulate(step);
                Record(rigidbodies, frame, positions, rotations);
                if (!wallClosed)
                {
                    // A die is inside once its whole bounding sphere is past the camera-side wall plane.
                    var allInside = true;
                    for (var i = 0; i < count; i++)
                    {
                        if (bodies[i].IsObstacle) continue;
                        var position = rigidbodies[i].position;
                        ceilingClearance = Mathf.Min(ceilingClearance, arena.Ceiling - position.y - radius);
                        if (entered[i]) continue;
                        // Fully inside when every collider vertex is past the wall plane; from then on the wall blocks it,
                        // so an entered die cannot return to the corridor.
                        if (MinimumZ(rigidbodies[i], vertices[i]) >= arena.NearZ)
                        {
                            Physics.IgnoreCollision(rigidbodies[i].GetComponent<Collider>(), nearWallCollider, false);
                            entered[i] = true;
                            continue;
                        }
                        allInside = false;
                    }
                    if (allInside)
                    {
                        wallClosed = true;
                        outcome.EntrySeconds = frame * step;
                        outcome.EntryCeilingClearance = ceilingClearance;
                    }
                    else if (frame - lastReleaseFrame >= entryFrames)
                    {
                        var waiting = 0;
                        for (var i = 0; i < count; i++) if (!bodies[i].IsObstacle && !entered[i]) { waiting = i; break; }
                        failure = $"Dice did not enter the arena within {settings.MaximumEntrySeconds:F2}s of their release " +
                            $"(die {waiting + 1}, wave {rows[waiting] + 1}/{lastRow + 1}, released {releasedRow + 1}, " +
                            $"kinematic={rigidbodies[waiting].isKinematic}, z={rigidbodies[waiting].position.z:F2}, nearZ={arena.NearZ:F2}, " +
                            $"launchZ={launchZ:F2}, speed={rigidbodies[waiting].linearVelocity.magnitude:F2}).";
                        return false;
                    }
                    continue;
                }
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
            }
            var snapDistance = 0f;
            var snapDegrees = 0f;
            for (var i = 0; i < count; i++)
            {
                if (bodies[i].IsObstacle) continue;
                // The final pose replaces the first still frame so the hold period is not replayed.
                var previous = Mathf.Max(0, settledFrame - 1) * count + i;
                positions[settledFrame * count + i] = rigidbodies[i].position;
                rotations[settledFrame * count + i] = rigidbodies[i].rotation;
                snapDistance = Mathf.Max(snapDistance, Vector3.Distance(positions[previous], rigidbodies[i].position));
                snapDegrees = Mathf.Max(snapDegrees, Quaternion.Angle(rotations[previous], rigidbodies[i].rotation));
            }
            outcome.FinalSnapDistance = snapDistance;
            outcome.FinalSnapDegrees = snapDegrees;
            trajectory = new DiceTrajectory(count, settledFrame + 1, step, positions, rotations);
            return true;
        }

        // Floor, ceiling and side walls run from the corridor end (behind the launch rows) to the far wall. The
        // camera-side wall exists from the start; each die ignores it until that die has entered.
        private void BuildBounds(DiceArena arena, float corridorZ)
        {
            var width = arena.HalfWidth * 2f + WallThickness * 2f;
            var centerZ = (corridorZ + arena.FarZ) * 0.5f;
            var depth = arena.FarZ - corridorZ + WallThickness * 2f;
            var height = arena.Ceiling + WallThickness * 2f;
            var halfThickness = WallThickness * 0.5f;
            Box("Floor", new Vector3(0f, -halfThickness, centerZ), new Vector3(width, WallThickness, depth));
            Box("Ceiling", new Vector3(0f, arena.Ceiling + halfThickness, centerZ), new Vector3(width, WallThickness, depth));
            Box("WallLeft", new Vector3(-arena.HalfWidth - halfThickness, arena.Ceiling * 0.5f, centerZ),
                new Vector3(WallThickness, height, depth));
            Box("WallRight", new Vector3(arena.HalfWidth + halfThickness, arena.Ceiling * 0.5f, centerZ),
                new Vector3(WallThickness, height, depth));
            Box("WallCorridorEnd", new Vector3(0f, arena.Ceiling * 0.5f, corridorZ - halfThickness), new Vector3(width, height, WallThickness));
            Box("WallFar", new Vector3(0f, arena.Ceiling * 0.5f, arena.FarZ + halfThickness), new Vector3(width, height, WallThickness));
            nearWallCollider = Box("WallNear", new Vector3(0f, arena.Ceiling * 0.5f, arena.NearZ - halfThickness),
                new Vector3(width, height, WallThickness)).GetComponent<Collider>();
        }

        private GameObject Box(string name, Vector3 center, Vector3 size)
        {
            var box = new GameObject(name).AddComponent<BoxCollider>();
            box.transform.SetParent(root.transform, false);
            box.transform.localPosition = center;
            box.size = size;
            box.sharedMaterial = material;
            return box.gameObject;
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
            nearWallCollider = null;
        }

        // True when every die of the row has moved the given distance past the launch line or has entered the arena.
        private static bool RowCleared(Rigidbody[] bodies, int[] rows, bool[] entered, int row, float clearZ, float radius, float nearZ)
        {
            for (var i = 0; i < bodies.Length; i++)
                if (rows[i] == row && !entered[i] && bodies[i].position.z < clearZ && bodies[i].position.z - radius < nearZ) return false;
            return true;
        }

        private static float MinimumZ(Rigidbody body, Vector3[] vertices)
        {
            var minimum = float.MaxValue;
            var rotation = body.rotation;
            var position = body.position;
            foreach (var vertex in vertices) minimum = Mathf.Min(minimum, position.z + (rotation * vertex).z);
            return minimum;
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
