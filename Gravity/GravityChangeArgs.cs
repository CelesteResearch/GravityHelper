// Copyright (c) Shane Woolcock. Licensed under the MIT Licence.

namespace Celeste.Mods.GravityHelper.Gravity
{
    [global::Celeste.Core.Reflection.Runtime.RuntimeTypeAlias("Celeste.Mod.GravityHelper.GravityChangeArgs")]
    public struct GravityChangeArgs
    {
        public GravityType NewValue;
        public GravityType? OldValue;
        public float MomentumMultiplier;
        public bool WasToggled;
        public bool Instant;
        public bool Changed => OldValue != null && NewValue != OldValue;

        public GravityChangeArgs(GravityType newValue, GravityType? oldValue = null, float momentumMultiplier = 1f,
            bool wasToggled = false, bool instant = false)
        {
            NewValue = newValue;
            OldValue = oldValue;
            MomentumMultiplier = momentumMultiplier;
            WasToggled = wasToggled;
            Instant = instant;
        }
    }
}
