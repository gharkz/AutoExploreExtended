using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Elin_AutoExplore;
using Explorer = Elin_AutoExplore.Plugin;

namespace AutoExploreExtended
{
    // Shearing (moved from the Auto-Explore Shearing Extension; same rules).
    internal static class Shearing
    {
        private sealed class FailedTarget { public int X, Z; public DateTime Until; }
        private static readonly ConditionalWeakTable<Chara, FailedTarget> failures = new ConditionalWeakTable<Chara, FailedTarget>();

        internal static bool Eligible(Chara target)
        {
            var pc = EClass.pc;
            return target != null && target != pc && !RecentlyFailed(target) && target.IsAliveInCurrentZone &&
                target.pos.IsInBounds && target.CanBeSheared() &&
                !target.IsHostile(pc) && !pc.IsHostile(target) &&
                !Explorer.Instance.IgnoreList.Gathering.Contains(target.Name) &&
                CanAffordOrRest(pc, target);
        }

        // Only what stamina covers now; when low, AutoExplore's own resting (bed, etc.) takes over.
        private static bool CanAffordOrRest(Chara pc, Chara target) => pc.stamina.max > ExploreShear.StaminaCost(pc, target); // the job rests first when low

        internal static bool RecentlyFailed(Chara target) => failures.TryGetValue(target, out var failed) &&
            failed.X == target.pos.x && failed.Z == target.pos.z && failed.Until > DateTime.UtcNow;

        internal static void NoteFailure(Chara target)
        {
            failures.Remove(target);
            failures.Add(target, new FailedTarget { X = target.pos.x, Z = target.pos.z, Until = DateTime.UtcNow.AddSeconds(10) });
        }
    }

    // Not an AI_Shear at the SetAI boundary, so AutoAct's global SetAI prefix
    // cannot turn this into its own map-wide AutoActShear job. The native child
    // starts via AIAct.Do/SetChild, not Chara.SetAI.
    public sealed class ExploreShear : AIAct
    {
        public Chara Target { get; }
        public ExploreShear(Chara target) => Target = target ?? throw new ArgumentNullException(nameof(target));
        public override Point GetDestination() => Target.pos;
        public override bool ShouldAllyAttack(Chara target) => target != Target;
        public override bool CanManualCancel() => true;

        public override IEnumerable<Status> Run()
        {
            isFail = () => !Plugin.ShearOn || !Plugin.Working || !Plugin.Shears || !Target.IsAliveInCurrentZone;
            if (!Plugin.ShearOn || !Plugin.Working || !Plugin.Shears || !Shearing.Eligible(Target)) yield break;
            // Native shearing spends one stamina per affected NPC. Preserve a
            // positive reserve, including wide shears, using native meditation.
            if (!EnoughStamina(owner, Target)) { Plugin.Rest(); yield break; } // AutoExplore's own rest (bed), then this sheep again
            yield return Do(new NativeShear { target = Target }, Failed);
        }

        private Status Failed() { Shearing.NoteFailure(Target); return Cancel(); }

        internal static bool EnoughStamina(Chara owner, Chara target)
            => owner.stamina.value > StaminaCost(owner, target);

        internal static int StaminaCost(Chara owner, Chara target)
        {
            var wide = owner.Tool?.Evalue(770) ?? 0;
            var cost = 1;
            if (wide > 0)
            {
                cost = 0;
                var radius = 1 + wide / 10;
                foreach (var point in EClass._map.ListPointsInSquare(target.pos, radius))
                    foreach (var c in point.ListCharas())
                        if (c.CanBeSheared()) cost++;
            }
            return cost;
        }

        private sealed class NativeShear : AI_Shear
        {
            // Native progress rechecks IsValidTC, so switching tools/modes or an
            // animal moving into the wide area cannot spend the stamina reserve.
            public override bool IsValidTC(Card c) => base.IsValidTC(c) && Plugin.ShearOn && Plugin.Shears &&
                EnoughStamina(owner, c.Chara);
        }
    }
}
