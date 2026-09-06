using System;

namespace TxTRPG.Gameplay.Characters
{
    public enum DamageType
    {
        Physical,
        Magical,
        True
    }

    public readonly struct DamageRequest
    {
        public DamageRequest(
            string sourceId,
            string targetId,
            int baseDamage,
            DamageType damageType = DamageType.Physical)
        {
            SourceId = sourceId?.Trim() ?? string.Empty;
            TargetId = targetId?.Trim() ?? string.Empty;
            BaseDamage = baseDamage;
            DamageType = damageType;
        }

        public string SourceId { get; }
        public string TargetId { get; }
        public int BaseDamage { get; }
        public DamageType DamageType { get; }
    }

    public readonly struct DamageResult
    {
        public DamageResult(int rawDamage, int finalDamage)
        {
            if (rawDamage < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(rawDamage));
            }
            if (finalDamage < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(finalDamage));
            }

            RawDamage = rawDamage;
            FinalDamage = finalDamage;
            PreventedDamage = Math.Max(0, rawDamage - finalDamage);
            AmplifiedDamage = Math.Max(0, finalDamage - rawDamage);
        }

        public int RawDamage { get; }
        public int FinalDamage { get; }
        public int PreventedDamage { get; }
        public int AmplifiedDamage { get; }
    }

    public interface IDamageResolver
    {
        DamageResult Resolve(in DamageRequest request);
    }

    public sealed class PassthroughDamageResolver : IDamageResolver
    {
        public DamageResult Resolve(in DamageRequest request)
        {
            if (request.BaseDamage < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(request),
                    "Base damage cannot be negative.");
            }
            return new DamageResult(request.BaseDamage, request.BaseDamage);
        }
    }
}
