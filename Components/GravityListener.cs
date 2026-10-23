// Copyright (c) Shane Woolcock. Licensed under the MIT Licence.

using System;
using Celeste.Core.Components;
using Celeste.Mods.GravityHelper.Gravity;
using Celeste.Mods.GravityHelper.Runtime;
using Celeste.Scene.Entities;
using Celeste.Scene.Tracking.Attributes;
using Celeste.Simulation;

namespace Celeste.Mods.GravityHelper.Components
{
    [global::Celeste.Core.Reflection.Runtime.RuntimeTypeAlias("Celeste.Mod.GravityHelper.Components.GravityListener")]
    [SceneTracked]
    public class GravityListener : SourceComponentState
    {
        private readonly GravityRuntimeContext runtime;
        private readonly WeakReference<SceneEntityState> target;
        public SceneEntityState Target => target != null && target.TryGetTarget(out var entity) ? entity : null;
        public Type TargetType { get; }
        public Action<SceneEntityState, GravityChangeArgs> GravityChanged;

        public GravityListener(GravityRuntimeContext runtime,
            Action<SceneEntityState, GravityChangeArgs> gravityChanged = null)
            : base(true, false)
        {
            this.runtime = runtime;
            GravityChanged = gravityChanged;
        }

        public GravityListener(GravityRuntimeContext runtime, SceneEntityState target,
            Action<SceneEntityState, GravityChangeArgs> gravityChanged = null) : this(runtime, gravityChanged)
            => this.target = new WeakReference<SceneEntityState>(target);

        public GravityListener(GravityRuntimeContext runtime, Type type,
            Action<SceneEntityState, GravityChangeArgs> gravityChanged = null) : this(runtime, gravityChanged)
            => TargetType = type;

        public override void EntityAwake()
        {
            base.EntityAwake();
            var component = Target is RoomPlayerSession || TargetType == typeof(RoomPlayerSession)
                ? runtime.PlayerComponent
                : Target?.Get<GravityComponent>();
            if (component != null) GravityChanged?.Invoke(Target, new GravityChangeArgs(component.CurrentGravity));
        }

        public void OnGravityChanged(SceneEntityState entity, GravityChangeArgs args)
        {
            if (entity != null && Target != null && Target != entity) return;
            if (entity != null && TargetType != null && entity.GetType() != TargetType) return;
            GravityChanged?.Invoke(entity, args);
        }
    }
}
