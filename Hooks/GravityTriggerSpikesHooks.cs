// Copyright (c) Shane Woolcock. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Reflection;
using Celeste.Core.Scheduling;
using Celeste.Mods.GravityHelper.Runtime;
using Celeste.Mods.GravityHelper.Components;
using Celeste.Mods.GravityHelper.Gravity;
using Celeste.Mods.GravityHelper.Entities.Platforms;
using Celeste.Simulation.Gameplay.Platforms.Foundation;
using Celeste.Physics.Collision;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Hazards.TriggerSpikes;
using Celeste.Simulation.Gameplay.Hazards.TriggerSpikes.Components;
using Celeste.Simulation.Gameplay.Collision.Blockers;
using MonoMod.RuntimeDetour;

namespace Celeste.Mods.GravityHelper.Hooks
{
    public sealed class GravityTriggerSpikesHooks : IDisposable
    {
        private readonly GravityRuntimeContext runtime;
        private readonly List<IDisposable> hooks = [];
        private readonly Func<TriggerSpikesState, RoomPlayerSession, bool> upSafe;

        public GravityTriggerSpikesHooks(GravityRuntimeContext runtime)
        {
            this.runtime = runtime;
            upSafe = (Func<TriggerSpikesState, RoomPlayerSession, bool>)typeof(TriggerSpikesState)
                .GetMethod("UpSafeBlockCheck", BindingFlags.Instance | BindingFlags.NonPublic)
                .CreateDelegate(typeof(Func<TriggerSpikesState, RoomPlayerSession, bool>));
            try
            {
                hooks.Add(new Hook(
                    typeof(TriggerSpikesState).GetConstructor(new[]
                        { typeof(Vector2), typeof(int), typeof(TriggerSpikesDirection), typeof(ISourceEngineContext) }),
                    (ConstructorHook)Construct));
                hooks.Add(new Hook(
                    typeof(TriggerSpikesState).GetMethod("IsRiding", BindingFlags.Instance | BindingFlags.NonPublic,
                        null,
                        new[] { typeof(ISourceJumpThruEntity) }, null), (RidingHook)IsRiding));
                hooks.Add(new Hook(
                    typeof(TriggerSpikesState).GetMethod("GetPlayerCollideIndex",
                        BindingFlags.Instance | BindingFlags.NonPublic), (IndicesHook)CollideIndices));
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        private bool Owns(TriggerSpikesState spikes) => spikes.Scene is ISourceEngineContextScene scene &&
                                                        ReferenceEquals(scene.EngineContext, runtime.Engine);

        private void Construct(ConstructorOriginal original, TriggerSpikesState spikes, Vector2 position, int size,
            TriggerSpikesDirection direction, ISourceEngineContext engine)
        {
            original(spikes, position, size, direction, engine);
            if (!ReferenceEquals(engine, runtime.Engine)) return;
            if (spikes.SourceDirection == TriggerSpikesDirection.Down)
            {
                spikes.Add(new SafeGroundBlockerComponentState());
                spikes.Add(new LedgeBlockerState(player => upSafe(spikes, (RoomPlayerSession)player)));
            }

            if (spikes.SourceDirection is TriggerSpikesDirection.Up or TriggerSpikesDirection.Down)
                spikes.Add(new PlayerGravityListener(runtime, (_, args) =>
                {
                    var ledge = spikes.Get<LedgeBlockerState>();
                    var safe = spikes.Get<SafeGroundBlockerComponentState>();
                    safe.Blocking = ledge.Blocking = spikes.SourceDirection == TriggerSpikesDirection.Up
                        ? args.NewValue == GravityType.Normal
                        : args.NewValue == GravityType.Inverted;
                }));
        }

        private bool IsRiding(RidingOriginal original, TriggerSpikesState spikes, ISourceJumpThruEntity platform)
        {
            if (original(spikes, platform)) return true;
            return Owns(spikes) && spikes.SourceDirection == TriggerSpikesDirection.Down &&
                   platform is UpsideDownJumpThru &&
                   SourceEntityCollision.CheckAt(spikes, platform, spikes.Position - Vector2.UnitY);
        }

        private void CollideIndices(IndicesOriginal original, TriggerSpikesState spikes, RoomPlayerSession player,
            out int min, out int max)
        {
            bool invert = Owns(spikes) &&
                          spikes.SourceDirection is TriggerSpikesDirection.Up or TriggerSpikesDirection.Down &&
                          (runtime.PlayerComponent?.ShouldInvert ?? false);
            if (!invert)
            {
                original(spikes, player, out min, out max);
                return;
            }

            var speed = player.Speed;
            speed.Y *= -1;
            player.Speed = speed;
            original(spikes, player, out min, out max);
            speed = player.Speed;
            speed.Y *= -1;
            player.Speed = speed;
        }

        public void Dispose()
        {
            for (int i = hooks.Count - 1; i >= 0; i--) hooks[i].Dispose();
            hooks.Clear();
        }

        private delegate void ConstructorOriginal(TriggerSpikesState spikes, Vector2 position, int size,
            TriggerSpikesDirection direction, ISourceEngineContext engine);

        private delegate void ConstructorHook(ConstructorOriginal original, TriggerSpikesState spikes, Vector2 position,
            int size, TriggerSpikesDirection direction, ISourceEngineContext engine);

        private delegate bool RidingOriginal(TriggerSpikesState spikes, ISourceJumpThruEntity platform);

        private delegate bool RidingHook(RidingOriginal original, TriggerSpikesState spikes,
            ISourceJumpThruEntity platform);

        private delegate void IndicesOriginal(TriggerSpikesState spikes, RoomPlayerSession player, out int min,
            out int max);

        private delegate void IndicesHook(IndicesOriginal original, TriggerSpikesState spikes, RoomPlayerSession player,
            out int min, out int max);
    }
}
