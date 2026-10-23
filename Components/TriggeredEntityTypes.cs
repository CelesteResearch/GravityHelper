using System;

namespace Celeste.Mods.GravityHelper.Components
{
    [Flags]
    public enum TriggeredEntityTypes
    {
        None = 0,
        Player,
        HoldableActors,
        NonHoldableActors,
        NonPlayer = HoldableActors | NonHoldableActors,
        All = Player | NonPlayer
    }
}
