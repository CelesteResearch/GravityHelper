// Copyright (c) Shane Woolcock. Licensed under the MIT Licence.

using System;
using Celeste.Everest.Tracking.Attributes;
using Celeste.Mods.GravityHelper.Gravity;
using Celeste.Mods.GravityHelper.Runtime;
using Celeste.Scene.Entities;
using Celeste.Simulation;

namespace Celeste.Mods.GravityHelper.Components
{
    [global::Celeste.Core.Reflection.Runtime.RuntimeTypeAlias(
        "Celeste.Mod.GravityHelper.Components.PlayerGravityListener")]
    [EverestTrackedAs(typeof(GravityListener))]
    public class PlayerGravityListener : GravityListener
    {
        public PlayerGravityListener(GravityRuntimeContext runtime,
            Action<SceneEntityState, GravityChangeArgs> gravityChanged = null)
            : base(runtime, typeof(RoomPlayerSession), gravityChanged)
        {
        }
    }
}
