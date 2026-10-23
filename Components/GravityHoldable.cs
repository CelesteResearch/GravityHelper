// Copyright (c) Shane Woolcock. Licensed under the MIT Licence.

using System;
using Celeste.Core.Components;
using Celeste.Core.Reflection.Runtime;
using Celeste.Mods.GravityHelper.Entities.Controllers;
using Celeste.Mods.GravityHelper.Gravity;
using Celeste.Mods.GravityHelper.Runtime;
using Celeste.Mods.GravityHelper.Triggers;
using Celeste.Physics.Collision;
using Celeste.Scene.Entities;
using Celeste.Scene.Tracking;
using Celeste.Scene.Tracking.Attributes;
using Celeste.Simulation.Gameplay.Holdables;

namespace Celeste.Mods.GravityHelper.Components
{
    [SceneTracked]
    [RuntimeTypeAlias("Celeste.Mod.GravityHelper.Components.GravityHoldable")]
    public class GravityHoldable : SourceComponentState
    {
        private readonly GravityRuntimeContext runtime;

        [Obsolete("Use ResetTime and ResetType instead.")]
        public float InvertTime { get; set; }

        private float sceneResetTime = BehaviorGravityController.DEFAULT_HOLDABLE_RESET_TIME;
        private float resetTimeRemaining;
        private float? resetTime;

        public float ResetTime
        {
            get => resetTime ?? sceneResetTime;
            set
            {
                resetTime = value < 0 ? null : value;
                resetTimeRemaining = ResetTime;
            }
        }

        public GravityType ResetType { get; set; } = GravityType.Normal;
        private SceneEntityState Owner => (SceneEntityState)Entity;
        public GravityHoldable(GravityRuntimeContext runtime) : base(true, false) => this.runtime = runtime;
        public void ResetTimer() => resetTimeRemaining = ResetTime;

        public void SetGravityHeld()
        {
            if (Owner?.Get<GravityComponent>() is not { } gravity) return;
            ResetTimer();
            GravityType target = runtime.PlayerComponent?.CurrentGravity ?? GravityType.Normal;
            if (gravity.CurrentGravity != target) gravity.SetGravity(target);
        }

        public override void Added(object entity)
        {
            base.Added(entity);
            if (Owner.Scene != null) UpdateResetTime(Owner.Scene);
            if (Owner.Get<GravityComponent>() == null && Owner.Get<HoldableState>() is { } holdable)
                Owner.Add(new GravityComponent(runtime) { GetSpeed = holdable.GetSpeed, SetSpeed = holdable.SetSpeed });
            if (Owner.Get<GravityListener>() == null)
                Owner.Add(new GravityListener(runtime, Owner, (_, _) => ResetTimer()));
        }

        public override void EntityAdded(object scene)
        {
            base.EntityAdded(scene);
            UpdateResetTime(scene);
        }

        public override void EntityAwake()
        {
            base.EntityAwake();
            if (((ISourceScene)Scene).SourceScene.Entities.TryGetTrackedEntities(typeof(HoldableSpawnGravityTrigger),
                    out var list))
                foreach (var item in list)
                    if (item is HoldableSpawnGravityTrigger trigger && SourceEntityCollision.Check(Owner, trigger))
                    {
                        Owner.Get<GravityComponent>()?.SetGravity(GravityType.Inverted);
                        break;
                    }
        }

        private void UpdateResetTime(object scene)
        {
            sceneResetTime = GravityControllerQueries.GetActive<BehaviorGravityController>(scene)?.HoldableResetTime ??
                             BehaviorGravityController.DEFAULT_HOLDABLE_RESET_TIME;
            resetTimeRemaining = sceneResetTime;
        }

        public override void Update()
        {
            base.Update();
            var holdable = Owner.Get<HoldableState>();
            var gravity = Owner.Get<GravityComponent>();
            if (holdable == null || gravity == null) return;
            if (holdable.IsHeld) SetGravityHeld();
            else if (ResetTime > 0 && resetTimeRemaining > 0)
            {
                if (((ISourceScene)Scene).SourceScene.Entities.TryGetTrackedEntities(typeof(GravityTrigger),
                        out var list))
                    foreach (var item in list)
                        if (item is GravityTrigger trigger && trigger.GravityType != ResetType &&
                            trigger.AffectsHoldableActors && SourceEntityCollision.Check(Owner, trigger))
                        {
                            ResetTimer();
                            return;
                        }

                resetTimeRemaining -= runtime.Engine.DeltaTime;
                if (resetTimeRemaining <= 0) gravity.SetGravity(ResetType);
            }
        }
    }
}
