// Copyright (c) Shane Woolcock. Licensed under the MIT Licence.

using System;
using System.Numerics;
using Celeste.Core.Components;
using Celeste.Core.Display;
using Celeste.Mods.GravityHelper.Components;
using Celeste.Mods.GravityHelper.Configuration;
using Celeste.Mods.GravityHelper.Gravity;
using Celeste.Mods.GravityHelper.Runtime;
using Celeste.Scene.Entities;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Actors.Foundation;
using MonoMod.ModInterop;
using MonoMod.Utils;

namespace Celeste.Mods.GravityHelper.Interop
{
    [ModExportName("GravityHelper")]
    public sealed partial class GravityHelperExports
    {
        private readonly GravityRuntimeContext runtime;
        public GravityHelperExports(GravityRuntimeContext runtime) => this.runtime = runtime;

        public void RegisterModSupportBlacklist(string modName) => runtime.ModSupportBlacklist.Add(modName);

        public string GravityTypeFromInt(int gravityType) => ((GravityType)gravityType).ToString();

        public int GravityTypeToInt(string name)
            => (int)(Enum.TryParse<GravityType>(name, out var value) ? value : GravityType.Normal);

        public int GetPlayerGravity() => (int)(runtime.PlayerComponent?.CurrentGravity ?? GravityType.Normal);

        public int GetActorGravity(SourceActorEntityState actor)
            => (int)(actor?.Get<GravityComponent>()?.CurrentGravity ?? GravityType.Normal);

        public void SetPlayerGravity(int gravityType, float momentumMultiplier)
            => runtime.PlayerComponent?.SetGravity((GravityType)gravityType, momentumMultiplier);

        public void SetActorGravity(SourceActorEntityState actor, int gravityType, float momentumMultiplier)
            => actor?.Get<GravityComponent>()?.SetGravity((GravityType)gravityType, momentumMultiplier);

        public bool IsPlayerInverted() => runtime.PlayerComponent?.ShouldInvert ?? false;
        public bool IsActorInverted(SourceActorEntityState actor) => actor != null && Inverted(actor);

        private static bool Inverted(SceneEntityState entity)
            => DynamicData.For(entity).Data.TryGetValue("GravityHelper_Inverted", out var value) && (bool)value;

        public Vector2 GetAboveVector(SourceActorEntityState actor) =>
            actor != null && Inverted(actor) ? Vector2.UnitY : -Vector2.UnitY;

        public Vector2 GetBelowVector(SourceActorEntityState actor) =>
            actor != null && Inverted(actor) ? -Vector2.UnitY : Vector2.UnitY;

        public Vector2 GetTopCenter(SourceActorEntityState actor)
            => actor != null && Inverted(actor) ? actor.BottomCenter : actor?.TopCenter ?? Vector2.Zero;

        public Vector2 GetBottomCenter(SourceActorEntityState actor)
            => actor != null && Inverted(actor) ? actor.TopCenter : actor?.BottomCenter ?? Vector2.Zero;

        public Vector2 GetTopLeft(SourceActorEntityState actor)
            => actor != null && Inverted(actor) ? actor.BottomLeft : actor?.TopLeft ?? Vector2.Zero;

        public Vector2 GetBottomLeft(SourceActorEntityState actor)
            => actor != null && Inverted(actor) ? actor.TopLeft : actor?.BottomLeft ?? Vector2.Zero;

        public Vector2 GetTopRight(SourceActorEntityState actor)
            => actor != null && Inverted(actor) ? actor.BottomRight : actor?.TopRight ?? Vector2.Zero;

        public Vector2 GetBottomRight(SourceActorEntityState actor)
            => actor != null && Inverted(actor) ? actor.TopRight : actor?.BottomRight ?? Vector2.Zero;

        public Vector2 TransformVector(Vector2 vector)
            => !IsPlayerInverted() || IsControlSchemeRelative() ? vector : new Vector2(vector.X, -vector.Y);

        public Vector2 TransformFeatherVector(Vector2 vector)
            => !IsPlayerInverted() || IsFeatherControlSchemeRelative() ? vector : new Vector2(vector.X, -vector.Y);

        public Vector2 TransformVectorForActor(Vector2 vector, SourceActorEntityState actor)
            => actor is RoomPlayerSession
                ? TransformVector(vector)
                : new Vector2(vector.X, Inverted(actor) ? -vector.Y : vector.Y);

        public bool IsControlSchemeRelative() => runtime.Settings.ControlScheme == ControlSchemeSetting.Relative;

        public bool IsFeatherControlSchemeRelative() =>
            runtime.Settings.FeatherControlScheme == ControlSchemeSetting.Relative;

        public SourceComponentState CreateGravityListener(SourceActorEntityState actor,
            Action<SceneEntityState, int, float> gravityChanged)
            => new GravityListener(runtime, actor,
                (entity, args) => gravityChanged(entity, (int)args.NewValue, args.MomentumMultiplier));

        public SourceComponentState CreatePlayerGravityListener(Action<RoomPlayerSession, int, float> gravityChanged)
            => new PlayerGravityListener(runtime,
                (entity, args) =>
                    gravityChanged(entity as RoomPlayerSession, (int)args.NewValue, args.MomentumMultiplier));

        public void BeginForceInvertPlayerRender() => runtime.ForceInvertPlayerRenderSemaphore++;
        public void EndForceInvertPlayerRender() => runtime.ForceInvertPlayerRenderSemaphore--;
        public void BeginOverride() => runtime.Overrides.Semaphore++;
        public void EndOverride() => runtime.Overrides.Semaphore--;

        public void ExecuteOverride(Action action)
        {
            runtime.Overrides.Semaphore++;
            action?.Invoke();
            runtime.Overrides.Semaphore--;
        }

        public IDisposable WithOverride()
        {
            runtime.Overrides.Semaphore++;
            return new InvokeOnDispose(() => runtime.Overrides.Semaphore--);
        }

        public PackedColor GetColor(int gravityType) => runtime.Settings.GetColorScheme()[(GravityType)gravityType];
        public PackedColor GetNormalColor() => runtime.Settings.GetColorScheme().NormalColor;
        public PackedColor GetInvertedColor() => runtime.Settings.GetColorScheme().InvertedColor;
        public PackedColor GetToggleColor() => runtime.Settings.GetColorScheme().ToggleColor;
    }
}
