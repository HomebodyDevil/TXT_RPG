using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace TxTRPG.Gameplay.Dice
{
    public sealed class Dice
    {
        private DiceFace[] faces;
        private ReadOnlyCollection<DiceFace> readOnlyFaces;

        public Dice(IEnumerable<DiceFace> faces, int minimumValue, int maximumValue)
        {
            var validated = CopyAndValidate(faces, minimumValue, maximumValue);
            this.faces = validated;
            readOnlyFaces = Array.AsReadOnly(validated);
            MinimumValue = minimumValue;
            MaximumValue = maximumValue;
        }

        public int FaceCount => faces.Length;
        public int MinimumValue { get; private set; }
        public int MaximumValue { get; private set; }
        public IReadOnlyList<DiceFace> Faces => readOnlyFaces;

        public DiceFace GetFace(int faceIndex)
        {
            ValidateFaceIndex(faceIndex);
            return faces[faceIndex];
        }

        public void SetFace(int faceIndex, DiceFace face)
        {
            ValidateFaceIndex(faceIndex);
            ValidateFace(face, MinimumValue, MaximumValue, nameof(face));
            faces[faceIndex] = face;
        }

        public void ReplaceFaces(IEnumerable<DiceFace> newFaces) => ReplaceConfiguration(newFaces, MinimumValue, MaximumValue);

        public void SetAllowedRange(int minimumValue, int maximumValue) =>
            ReplaceConfiguration(faces, minimumValue, maximumValue);

        public void ReplaceConfiguration(IEnumerable<DiceFace> newFaces, int minimumValue, int maximumValue)
        {
            var validated = CopyAndValidate(newFaces, minimumValue, maximumValue);
            faces = validated;
            readOnlyFaces = Array.AsReadOnly(validated);
            MinimumValue = minimumValue;
            MaximumValue = maximumValue;
        }

        public DiceRollResult Roll(IRandomIndexSource randomIndexSource)
        {
            if (randomIndexSource == null) throw new ArgumentNullException(nameof(randomIndexSource));
            var faceIndex = randomIndexSource.NextIndex(FaceCount);
            if ((uint)faceIndex >= (uint)FaceCount)
                throw new InvalidOperationException($"The random index source returned {faceIndex} for a die with {FaceCount} faces.");
            return ResolveFace(faceIndex);
        }

        public DiceRollResult ResolveFace(int faceIndex)
        {
            ValidateFaceIndex(faceIndex);
            return new DiceRollResult(faceIndex, faces[faceIndex]);
        }

        private static DiceFace[] CopyAndValidate(IEnumerable<DiceFace> values, int minimumValue, int maximumValue)
        {
            if (values == null) throw new ArgumentNullException(nameof(values));
            if (minimumValue > maximumValue)
                throw new ArgumentException("The minimum allowed amount cannot be greater than the maximum allowed amount.");
            var copy = values as DiceFace[];
            copy = copy == null ? new List<DiceFace>(values).ToArray() : (DiceFace[])copy.Clone();
            if (copy.Length == 0) throw new ArgumentException("A die requires at least one face.", nameof(values));
            for (var index = 0; index < copy.Length; index++)
                ValidateFace(copy[index], minimumValue, maximumValue, nameof(values));
            return copy;
        }

        private static void ValidateFace(DiceFace face, int minimumValue, int maximumValue, string parameterName)
        {
            if (face.EffectKind != DiceEffectKind.Attack && face.EffectKind != DiceEffectKind.Heal)
                throw new ArgumentOutOfRangeException(parameterName, face.EffectKind, "Only Attack and Heal faces are currently supported.");
            if (face.Amount < minimumValue || face.Amount > maximumValue)
                throw new ArgumentOutOfRangeException(parameterName, face.Amount,
                    $"Face amounts must be in the inclusive range [{minimumValue}, {maximumValue}].");
        }

        private void ValidateFaceIndex(int faceIndex)
        {
            if ((uint)faceIndex >= (uint)faces.Length) throw new ArgumentOutOfRangeException(nameof(faceIndex));
        }
    }
}
