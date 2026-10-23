// Copyright (c) Shane Woolcock. Licensed under the MIT Licence.

using Celeste.Mods.GravityHelper.Gravity;

namespace Celeste.Mods.GravityHelper.Runtime
{
    [global::Celeste.Core.Reflection.Runtime.RuntimeTypeAlias("Celeste.Mod.GravityHelper.GravityHelperModuleSession")]
    public sealed class GravityHelperSession : Celeste.Everest.Modules.Persistence.EverestModuleSession
    {
        public GravityType InitialGravity { get; set; } = GravityType.Normal;
        public bool VvvvvvTrigger { get; set; }
        public bool DisableGrab { get; set; }
        public VvvvvvMode VvvvvvMode { get; set; }
    }
}
