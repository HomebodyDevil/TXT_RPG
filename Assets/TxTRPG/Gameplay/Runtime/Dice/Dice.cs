using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace TxTRPG.Gameplay.Dice
{
    public sealed class Dice
    {
        private int[] faceValues;
        private ReadOnlyCollection<int> readOnlyFaceValues;

        public Dice(IEnumerable<int> faceValues, int minimumValue, int maximumValue)
        {
            var validated = CopyAndValidate(faceValues, minimumValue, maximumValue);
            this.faceValues = validated;
            readOnlyFaceValues = Array.AsReadOnly(validated);
            MinimumValue = minimumValue;
            MaximumValue = maximumValue;
        }

        public int FaceCount => faceValues.Length;
        public int MinimumValue { get; private set; }
        public int MaximumValue { get; private set; }
        public IReadOnlyList<int> FaceValues => readOnlyFaceValues;

        public int GetFaceValue(int faceIndex)
        {
            ValidateFaceIndex(faceIndex);
            return faceValues[faceIndex];
        }

        public void SetFaceValue(int faceIndex, int value)
        {
            ValidateFaceIndex(faceIndex);
            ValidateValue(value, MinimumValue, MaximumValue, nameof(value));
            faceValues[faceIndex] = value;
        }

        public void ReplaceFaces(IEnumerable<int> newFaceValues)
        {
            ReplaceConfiguration(newFaceValues, MinimumValue, MaximumValue);
        }

        public void SetAllowedRange(int minimumValue, int maximumValue)
        {
            ReplaceConfiguration(faceValues, minimumValue, maximumValue);
        }

        public void ReplaceConfiguration(
            IEnumerable<int> newFaceValues,
            int minimumValue,
            int maximumValue)
        {
            var validated = CopyAndValidate(newFaceValues, minimumValue, maximumValue);
            faceValues = validated;
            readOnlyFaceValues = Array.AsReadOnly(validated);
            MinimumValue = minimumValue;
            MaximumValue = maximumValue;
        }

        public DiceRollResult Roll(IRandomIndexSource randomIndexSource)
        {
            if (randomIndexSource == null)
            {
                throw new ArgumentNullException(nameof(randomIndexSource));
            }

            var faceIndex = randomIndexSource.NextIndex(FaceCount);
            if ((uint)faceIndex >= (uint)FaceCount)
            {
                throw new InvalidOperationException(
                    $"The random index source returned {faceIndex} for a die with {FaceCount} faces.");
            }

            return new DiceRollResult(faceIndex, faceValues[faceIndex]);
        }

        private static int[] CopyAndValidate(
            IEnumerable<int> values,
            int minimumValue,
            int maximumValue)
        {
            if (values == null)
            {
                throw new ArgumentNullException(nameof(values));
            }
            if (minimumValue > maximumValue)
            {
                throw new ArgumentException(
                    "The minimum allowed value cannot be greater than the maximum allowed value.");
            }

            var copy = values as int[];
            copy = copy == null ? new List<int>(values).ToArray() : (int[])copy.Clone();
            if (copy.Length == 0)
            {
                throw new ArgumentException("A die requires at least one face.", nameof(values));
            }

            for (var index = 0; index < copy.Length; index++)
            {
                ValidateValue(copy[index], minimumValue, maximumValue, nameof(values));
            }

            return copy;
        }

        private static void ValidateValue(
            int value,
            int minimumValue,
            int maximumValue,
            string parameterName)
        {
            if (value < minimumValue || value > maximumValue)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    value,
                    $"Face values must be in the inclusive range [{minimumValue}, {maximumValue}].");
            }
        }

        private void ValidateFaceIndex(int faceIndex)
        {
            if ((uint)faceIndex >= (uint)faceValues.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(faceIndex));
            }
        }
    }
}
