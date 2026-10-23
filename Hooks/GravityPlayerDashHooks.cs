// Copyright (c) Shane Woolcock. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Numerics;
using Celeste.Core.Mathematics;
using Celeste.Mods.GravityHelper.Runtime;
using Celeste.Physics.Collision;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Platforms.Foundation;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;

namespace Celeste.Mods.GravityHelper.Hooks
{
    public sealed class GravityPlayerDashHooks : IDisposable
    {
        private readonly GravityRuntimeContext runtime;
        private readonly Func<RoomPlayerSession, IEnumerable<SourceJumpThruEntityState>> upsideDownJumpThrus;
        private readonly List<IDisposable> hooks = [];

        public GravityPlayerDashHooks(GravityRuntimeContext runtime,
            Func<RoomPlayerSession, IEnumerable<SourceJumpThruEntityState>> upsideDownJumpThrus)
        {
            this.runtime = runtime;
            this.upsideDownJumpThrus =
                upsideDownJumpThrus ?? throw new ArgumentNullException(nameof(upsideDownJumpThrus));
            try
            {
                hooks.Add(new ILHook(
                    typeof(RoomPlayerWorld).GetProperty(nameof(RoomPlayerWorld.DreamDashIntoBlock)).GetMethod,
                    AdjustDreamProbe));
                hooks.Add(new ILHook(
                    typeof(RoomPlayerWorld).GetMethod(nameof(RoomPlayerWorld.Emit),
                        new[] { typeof(Celeste.Simulation.Gameplay.Player.Dash.DashEffect) }), AdjustSlash));
                hooks.Add(new Hook(typeof(RoomPlayerWorld).GetMethod(nameof(RoomPlayerWorld.CorrectJumpThrus)),
                    (Action<Action<RoomPlayerWorld>, RoomPlayerWorld>)CorrectJumpThrus));
                hooks.Add(new Hook(typeof(RoomPlayerWorld).GetMethod(nameof(RoomPlayerWorld.RedDashCorrectJumpThrus)),
                    (Action<Action<RoomPlayerWorld>, RoomPlayerWorld>)CorrectJumpThrus));
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        private bool Inverted(RoomPlayerWorld world) =>
            ReferenceEquals(world.SourcePlayer.EngineContext, runtime.Engine) &&
            (runtime.PlayerComponent?.ShouldInvert ?? false);

        private Vector2 Transform(Vector2 direction, RoomPlayerWorld world) =>
            Inverted(world) ? new Vector2(direction.X, -direction.Y) : direction;

        private void AdjustDreamProbe(ILContext il)
        {
            var cursor = new ILCursor(il);
            cursor.GotoNext(MoveType.After, instruction => instruction.MatchCall<Vector2>("get_UnitY"));
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.EmitDelegate<Func<Vector2, RoomPlayerWorld, Vector2>>(Transform);
        }

        private void AdjustSlash(ILContext il)
        {
            var cursor = new ILCursor(il);
            cursor.GotoNext(MoveType.Before,
                instruction => instruction.MatchCallOrCallvirt(typeof(Vector2Geometry), nameof(Vector2Geometry.Angle)));
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.EmitDelegate<Func<Vector2, RoomPlayerWorld, Vector2>>(Transform);
        }

        private void CorrectJumpThrus(Action<RoomPlayerWorld> original, RoomPlayerWorld world)
        {
            if (!Inverted(world))
            {
                original(world);
                return;
            }

            var player = world.SourcePlayer;
            foreach (var platform in upsideDownJumpThrus(player))
            {
                if (SourceEntityCollision.Check(player, platform) && platform.Bottom - player.Top <= 6f &&
                    !world.DashCorrectBlocked(Vector2.UnitY * (platform.Bottom - player.Top)))
                    world.MoveVExact((int)(player.Top - platform.Bottom));
            }
        }

        public void Dispose()
        {
            for (int i = hooks.Count - 1; i >= 0; i--) hooks[i].Dispose();
            hooks.Clear();
        }
    }
}
