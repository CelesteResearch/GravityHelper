// Copyright (c) Shane Woolcock. Licensed under the MIT Licence.

using System;
using System.Numerics;
using Celeste.Core.Components;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Gameplay.Holdables.Gliders;
using Celeste.Graphics.Animation;
using Celeste.Mods.GravityHelper.Gravity;
using Celeste.Mods.GravityHelper.Runtime;
using Celeste.Scene.Entities;
using Celeste.Scene.Tracking;
using Celeste.Scene.Tracking.Attributes;
using Celeste.Simulation.Gameplay.Holdables;
using MonoMod.Utils;

namespace Celeste.Mods.GravityHelper.Components
{
    [global::Celeste.Core.Reflection.Runtime.RuntimeTypeAlias("Celeste.Mod.GravityHelper.Components.GravityComponent")]
    [SceneTracked(true)]
    public class GravityComponent : SourceComponentState
    {
        private static int nextId;
        public readonly int GlobalId = nextId++;
        internal const string InvertedKey = "GravityHelper_Inverted";
        private readonly GravityRuntimeContext runtime;
        private DynamicData data;
        private GravityType currentGravity;
        private int lockSemaphore;

        public GravityType CurrentGravity
        {
            get => currentGravity;
            private set
            {
                currentGravity = value;
                if (data != null) data.Data[InvertedKey] = value == GravityType.Inverted;
            }
        }

        public bool Locked => lockSemaphore > 0;
        public bool UpdateEntity { get; set; } = true;
        public Func<bool> CheckInvert;
        public Action<GravityChangeArgs> UpdateVisuals, UpdateColliders, UpdatePosition, UpdateSpeed;
        public Func<Vector2> GetSpeed;
        public Action<Vector2> SetSpeed;
        public string Flag;
        public bool ShouldInvert => currentGravity == GravityType.Inverted;
        public bool ShouldInvertChecked => ShouldInvert && (CheckInvert?.Invoke() ?? true);
        private SceneEntityState ActualEntity => (SceneEntityState)Entity;

        public Vector2 EntitySpeed
        {
            get => GetSpeed?.Invoke() ?? Vector2.Zero;
            set => SetSpeed?.Invoke(value);
        }

        public GravityComponent(GravityRuntimeContext runtime) : base(true, false) => this.runtime = runtime;
        public int Lock() => lockSemaphore = Math.Max(lockSemaphore + 1, 1);
        public int Unlock(bool force = false) => lockSemaphore = force ? 0 : Math.Max(0, lockSemaphore - 1);

        public override void Added(object entity)
        {
            base.Added(entity);
            data = DynamicData.For(entity);
            data.Data[InvertedKey] = ShouldInvert;
        }

        public override void Removed(object entity)
        {
            base.Removed(entity);
            UpdateGravity(new GravityChangeArgs(GravityType.Normal, CurrentGravity));
            data.Data[InvertedKey] = false;
            data = null;
        }

        public override void EntityAdded(object scene)
        {
            base.EntityAdded(scene);
            if (Flag != null && scene is OriginalLevelSceneRoot level) level.Session.SetFlag(Flag, ShouldInvert);
        }

        public override void EntityAwake() => TriggerGravityListeners(new GravityChangeArgs(CurrentGravity));

        public override void Update()
        {
            if (Flag != null && Scene is OriginalLevelSceneRoot level)
            {
                bool flagValue = level.Session.GetFlag(Flag);
                if (flagValue != ShouldInvert) SetGravity(flagValue ? GravityType.Inverted : GravityType.Normal);
            }
        }

        public bool SetGravity(GravityType newValue, float momentumMultiplier = 1f, bool instant = false)
        {
            if (Locked || newValue < 0) return false;
            GravityType oldGravity = currentGravity;
            GravityType newGravity = newValue == GravityType.Toggle
                ? currentGravity switch
                {
                    GravityType.Normal => GravityType.Inverted,
                    GravityType.Inverted => GravityType.Normal,
                    _ => currentGravity
                }
                : newValue;
            var args = new GravityChangeArgs(newGravity, oldGravity, momentumMultiplier,
                newValue == GravityType.Toggle, instant);
            CurrentGravity = newGravity;
            UpdateGravity(args);
            TriggerGravityListeners(args);
            return true;
        }

        private void UpdateGravity(GravityChangeArgs args)
        {
            if (Locked || Scene == null || !UpdateEntity) return;
            var entity = ActualEntity;
            var holdable = entity.Get<HoldableState>();
            if (UpdatePosition != null) UpdatePosition(args);
            else if (args.Changed && entity.Collider != null)
                entity.Position = new Vector2(entity.Position.X, args.NewValue == GravityType.Inverted
                    ? entity.Collider.AbsoluteTop
                    : entity.Collider.AbsoluteBottom);
            if (UpdateColliders != null) UpdateColliders(args);
            else if (args.Changed)
            {
                if (entity.Collider != null) entity.Collider.Top = -entity.Collider.Bottom;
                if (holdable != null) holdable.PickupCollider.Top = -holdable.PickupCollider.Bottom;
            }

            if (UpdateSpeed != null) UpdateSpeed(args);
            else if (args.Changed && GetSpeed != null && SetSpeed != null)
            {
                Vector2 speed = GetSpeed();
                if (args.Instant) speed.Y = 160f * (SceneAs<OriginalLevelSceneRoot>().InSpace ? .6f : 1f);
                else speed.Y *= -args.MomentumMultiplier;
                SetSpeed(speed);
            }

            if (UpdateVisuals != null) UpdateVisuals(args);
            else if (args.Changed && entity is not OriginalGliderState && holdable != null &&
                     entity.Get<Sprite>() is { } sprite)
                sprite.Scale = new Vector2(sprite.Scale.X, args.NewValue == GravityType.Inverted ? -1 : 1);
            if (Flag != null)
                SceneAs<OriginalLevelSceneRoot>()?.Session.SetFlag(Flag, args.NewValue == GravityType.Inverted);
        }

        private void TriggerGravityListeners(GravityChangeArgs args)
        {
            var scene = (ISourceScene)runtime.Engine.Frame.Scenes.CurrentScene;
            foreach (var component in scene.SourceScene.Entities.GetComponentsTrackIfNeeded<GravityListener>())
                (component as GravityListener)?.OnGravityChanged(ActualEntity, args);
        }
    }
}
