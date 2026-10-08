using System;
using System.Collections.Generic;
using UnityEngine;

namespace TxTRPG.UI.Dice
{
    // Pure face/symmetry math for presentation. Only value types are used so Edit Mode tests can cover every face pair.
    public static class DieFaceGeometry
    {
        public const float DirectionTolerance = 0.999f;

        public static bool TryValidate(IReadOnlyList<Vector3> normals, IReadOnlyList<Vector3> readingUps, out string error)
        {
            error = string.Empty;
            if (normals == null || normals.Count < 2) { error = "At least two face normals are required."; return false; }
            if (readingUps == null || readingUps.Count != normals.Count)
            { error = "Each face needs exactly one reading direction."; return false; }
            for (var i = 0; i < normals.Count; i++)
            {
                if (Mathf.Abs(normals[i].sqrMagnitude - 1f) > 0.01f) { error = $"Face {i + 1} normal is not normalized."; return false; }
                if (Mathf.Abs(readingUps[i].sqrMagnitude - 1f) > 0.01f || Mathf.Abs(Vector3.Dot(normals[i], readingUps[i])) > 0.02f)
                { error = $"Face {i + 1} reading direction must be a unit vector on the face plane."; return false; }
                for (var j = 0; j < i; j++)
                    if (Vector3.Dot(normals[i], normals[j]) > DirectionTolerance)
                    { error = $"Faces {j + 1} and {i + 1} share a normal."; return false; }
            }
            if (!HasOppositeFaces(normals, out var missing))
            { error = $"Face {missing + 1} has no parallel opposite face, so a resting die cannot show a flat upward face."; return false; }
            var symmetries = ComputeRotationSymmetries(normals);
            for (var resting = 0; resting < normals.Count; resting++)
                for (var desired = 0; desired < normals.Count; desired++)
                    if (!HasCorrection(symmetries, normals, resting, desired))
                    { error = $"No rotation symmetry moves face {desired + 1} onto face {resting + 1}."; return false; }
            return true;
        }

        public static bool HasOppositeFaces(IReadOnlyList<Vector3> normals, out int missingFace)
        {
            for (var i = 0; i < normals.Count; i++)
            {
                var found = false;
                for (var j = 0; j < normals.Count && !found; j++)
                    found = Vector3.Dot(normals[i], -normals[j]) > DirectionTolerance;
                if (!found) { missingFace = i; return false; }
            }
            missingFace = -1;
            return true;
        }

        // Rotations that map the face-normal set onto itself. For the convex regular solids used here, this equals the
        // rotation group of the mesh, so a symmetry-corrected visual overlaps the collision proxy exactly.
        public static Quaternion[] ComputeRotationSymmetries(IReadOnlyList<Vector3> normals)
        {
            var result = new List<Quaternion>();
            if (normals == null || normals.Count < 2) return result.ToArray();
            var first = normals[0];
            var secondIndex = -1;
            for (var i = 1; i < normals.Count && secondIndex < 0; i++)
                if (Mathf.Abs(Vector3.Dot(first, normals[i])) < 0.99f) secondIndex = i;
            if (secondIndex < 0) return result.ToArray();
            var second = normals[secondIndex];
            var angle = Vector3.Dot(first, second);
            var inverseSource = Quaternion.Inverse(Quaternion.LookRotation(first, second));
            for (var i = 0; i < normals.Count; i++)
                for (var j = 0; j < normals.Count; j++)
                {
                    if (i == j || Mathf.Abs(Vector3.Dot(normals[i], normals[j]) - angle) > 0.001f) continue;
                    var rotation = Quaternion.LookRotation(normals[i], normals[j]) * inverseSource;
                    if (!MapsOntoItself(rotation, normals)) continue;
                    var duplicate = false;
                    foreach (var existing in result)
                        if (Quaternion.Angle(existing, rotation) < 0.5f) { duplicate = true; break; }
                    if (!duplicate) result.Add(rotation);
                }
            return result.ToArray();
        }

        public static bool MapsOntoItself(Quaternion rotation, IReadOnlyList<Vector3> directions)
        {
            foreach (var direction in directions)
            {
                var moved = rotation * direction;
                var found = false;
                foreach (var candidate in directions)
                    if (Vector3.Dot(moved, candidate) > DirectionTolerance) { found = true; break; }
                if (!found) return false;
            }
            return true;
        }

        public static int UpFace(Quaternion rotation, IReadOnlyList<Vector3> normals, Vector3 up, out float alignment)
        {
            var best = -1;
            alignment = float.NegativeInfinity;
            for (var i = 0; i < normals.Count; i++)
            {
                var dot = Vector3.Dot(rotation * normals[i], up);
                if (dot > alignment) { alignment = dot; best = i; }
            }
            return best;
        }

        // Returns S where S * normal[desired] == normal[resting]; among valid S, prefers the one whose face label reads
        // upright from the camera after the body ends at bodyRotation.
        public static bool TrySelectCorrection(IReadOnlyList<Quaternion> symmetries, IReadOnlyList<Vector3> normals,
            IReadOnlyList<Vector3> readingUps, int resting, int desired, Quaternion bodyRotation, Vector3 readForward,
            out Quaternion correction)
        {
            correction = Quaternion.identity;
            var bestScore = float.NegativeInfinity;
            foreach (var symmetry in symmetries)
            {
                if (Vector3.Dot(symmetry * normals[desired], normals[resting]) <= DirectionTolerance) continue;
                var score = Vector3.Dot(bodyRotation * (symmetry * readingUps[desired]), readForward);
                if (score > bestScore) { bestScore = score; correction = symmetry; }
            }
            return !float.IsNegativeInfinity(bestScore);
        }

        public static bool HasCorrection(IReadOnlyList<Quaternion> symmetries, IReadOnlyList<Vector3> normals,
            int resting, int desired)
        {
            foreach (var symmetry in symmetries)
                if (Vector3.Dot(symmetry * normals[desired], normals[resting]) > DirectionTolerance) return true;
            return false;
        }

        // Rest pose: the face normal points up and its label's up direction points away from the camera.
        public static Quaternion RestPose(Vector3 normal, Vector3 readingUp, Vector3 worldUp, Vector3 readForward) =>
            Quaternion.LookRotation(readForward, worldUp) * Quaternion.Inverse(Quaternion.LookRotation(readingUp, normal));
    }
}
