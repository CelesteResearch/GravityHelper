using System;
using Celeste.Core.Components;
using Celeste.Scene.Tracking.Attributes;

namespace Celeste.Mods.GravityHelper.Components.Cassette
{
    [SceneTracked]
    [global::Celeste.Core.Reflection.Runtime.RuntimeTypeAlias("Celeste.Mod.GravityHelper.Components.CassetteListener")]
    public class CassetteListener : SourceComponentState
    {
        public bool Enabled { get; set; } = true;
        public Action<int, bool> WillToggle { get; set; }
        public Action<int> DidBecomeActive { get; set; }
        public Action DidStop { get; set; }

        public CassetteListener() : base(false, false)
        {
        }
    }
}
