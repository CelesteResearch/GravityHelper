// Copyright (c) Shane Woolcock. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Reflection;
using Celeste.Core.Scheduling;
using Celeste.Mods.GravityHelper.Components;
using Celeste.Mods.GravityHelper.Entities.Platforms;
using Celeste.Mods.GravityHelper.Gravity;
using Celeste.Mods.GravityHelper.Runtime;
using Celeste.Physics.Collision;
using Celeste.Scene.Tracking;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Collision.Blockers;
using Celeste.Simulation.Gameplay.Hazards.Spikes;
using Celeste.Simulation.Gameplay.Platforms.Foundation;
using MonoMod.RuntimeDetour;

namespace Celeste.Mods.GravityHelper.Hooks
{
    public sealed class GravitySpikesHooks : IDisposable
    {
        private readonly GravityRuntimeContext runtime;
        private readonly List<IDisposable> hooks = [];

        public GravitySpikesHooks(GravityRuntimeContext runtime)
        {
            this.runtime = runtime;
            try
            {
                hooks.Add(new Hook(
                    typeof(SpikesState).GetConstructor(new[]
                    {
                        typeof(Vector2), typeof(int), typeof(SpikeDirection), typeof(string),
                        typeof(ISourceEngineContext)
                    }), (ConstructorHook)Construct));
                hooks.Add(new Hook(
                    typeof(SpikesState).GetMethod("IsRiding", BindingFlags.Instance | BindingFlags.NonPublic, null,
                        new[] { typeof(ISourceJumpThruEntity) }, null), (RidingHook)IsRiding));
                using (new DetourConfigContext(new DetourConfig("GravityHelper.Spikes", priority: int.MinValue)).Use())
                    hooks.Add(new Hook(
                        typeof(SpikesState).GetMethod("OnCollide", BindingFlags.Instance | BindingFlags.NonPublic),
                        (Action<Action<SpikesState, RoomPlayerSession>, SpikesState, RoomPlayerSession>)OnCollide));
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        private bool Owns(SpikesState spikes) => spikes.Scene is ISourceEngineContextScene scene &&
                                                 ReferenceEquals(scene.EngineContext, runtime.Engine);

        private void Construct(ConstructorOriginal original, SpikesState spikes, Vector2 position, int size,
            SpikeDirection direction, string type, ISourceEngineContext engine)
        {
            original(spikes, position, size, direction, type, engine);
            if (!ReferenceEquals(engine, runtime.Engine)) return;
            if (spikes.Direction == SpikeDirection.Down) spikes.Add(new LedgeBlockerState { Blocking = false });
            if (spikes.Direction == SpikeDirection.Down || spikes.Direction == SpikeDirection.Up)
                spikes.Add(new PlayerGravityListener(runtime, (_, args) =>
                {
                    var blocker = spikes.Get<LedgeBlockerState>();
                    if (spikes.Direction == SpikeDirection.Up) blocker.Blocking = args.NewValue == GravityType.Normal;
                    else if (spikes.Direction == SpikeDirection.Down)
                        blocker.Blocking = args.NewValue == GravityType.Inverted;
                }));
        }

        private bool IsRiding(RidingOriginal original, SpikesState spikes, ISourceJumpThruEntity platform)
        {
            if (original(spikes, platform)) return true;
            return Owns(spikes) && spikes.Direction == SpikeDirection.Down && platform is UpsideDownJumpThru &&
                   SourceEntityCollision.CheckAt(spikes, platform, spikes.Position - Vector2.UnitY);
        }

        private void OnCollide(Action<SpikesState, RoomPlayerSession> original, SpikesState spikes,
            RoomPlayerSession player)
        {
            if (!ReferenceEquals(player.EngineContext, runtime.Engine) || spikes.Direction == SpikeDirection.Left ||
                spikes.Direction == SpikeDirection.Right)
            {
                original(spikes, player);
                return;
            }

            bool invert = runtime.PlayerComponent?.ShouldInvert ?? false;
            bool dream = player.Machine.State == 9;
            if (!invert && !dream)
            {
                original(spikes, player);
                return;
            }

            if (spikes.Direction == SpikeDirection.Up)
            {
                if (!invert && player.Data.Motion.Normal.Speed.Y >= 0 && player.Bottom <= spikes.Bottom)
                    player.Die(-Vector2.UnitY);
                else if (invert && player.Data.Motion.Normal.Speed.Y <= 0 && (!dream || player.Bottom <= spikes.Bottom))
                    player.Die(-Vector2.UnitY);
            }
            else
            {
                var component = runtime.PlayerComponent as PlayerGravityComponent;
                bool changedGravity = component?.PreDreamBlockGravityType != component?.CurrentGravity;
                if (!invert && player.Data.Motion.Normal.Speed.Y <= 0 && (!changedGravity || player.Top >= spikes.Top))
                    player.Die(Vector2.UnitY);
                else if (invert && player.Data.Motion.Normal.Speed.Y >= 0 && player.Top >= spikes.Top)
                    player.Die(Vector2.UnitY);
            }
        }

        public void Dispose()
        {
            for (int i = hooks.Count - 1; i >= 0; i--) hooks[i].Dispose();
            hooks.Clear();
        }

        private delegate void ConstructorOriginal(SpikesState spikes, Vector2 position, int size,
            SpikeDirection direction,
            string type, ISourceEngineContext engine);

        private delegate void ConstructorHook(ConstructorOriginal original, SpikesState spikes, Vector2 position,
            int size,
            SpikeDirection direction, string type, ISourceEngineContext engine);

        private delegate bool RidingOriginal(SpikesState spikes, ISourceJumpThruEntity platform);

        private delegate bool RidingHook(RidingOriginal original, SpikesState spikes, ISourceJumpThruEntity platform);
    }
}
