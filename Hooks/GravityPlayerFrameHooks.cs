// Copyright (c) Shane Woolcock. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Reflection;
using Celeste.Mods.GravityHelper.Collision;
using Celeste.Mods.GravityHelper.Runtime;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Player.Audio.Frame;
using Celeste.Simulation.Gameplay.Player.Movement;
using Celeste.Simulation.Gameplay.Player.Runtime;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;

namespace Celeste.Mods.GravityHelper.Hooks
{
    public sealed class GravityPlayerFrameHooks : IDisposable
    {
        private readonly GravityRuntimeContext runtime;
        private readonly List<IDisposable> hooks = [];

        public GravityPlayerFrameHooks(GravityRuntimeContext runtime)
        {
            this.runtime = runtime;
            hooks.Add(new Hook(typeof(RoomPlayerWorld).GetMethod(nameof(RoomPlayerWorld.GroundAtOnePixel)),
                (GroundHook)Ground));
            hooks.Add(new Hook(typeof(RoomPlayerWorld).GetMethod(nameof(RoomPlayerWorld.RefillGroundAtOnePixel)),
                (RefillHook)Refill));
            hooks.Add(new ILHook(typeof(PlayerFrameMotion).GetMethod(nameof(PlayerFrameMotion.Update)),
                AdjustMovingClimbHop));
            hooks.Add(new ILHook(
                typeof(RoomPlayerWorld).GetMethod("CreateSpeedRing", BindingFlags.Instance | BindingFlags.NonPublic),
                AdjustSpeedRing));
        }

        private bool Owns(RoomPlayerWorld world) => ReferenceEquals(world.SourcePlayer.EngineContext, runtime.Engine);
        private bool Inverted(RoomPlayerWorld world) => Owns(world) && (runtime.PlayerComponent?.ShouldInvert ?? false);

        private bool Ground(GroundOriginal original, RoomPlayerWorld world, out bool safe)
        {
            if (!Owns(world)) return original(world, out safe);
            var platform =
                GravityPlayerGroundContact.FirstPlatform(world.SourcePlayer, Inverted(world), runtime.JumpThruContacts);
            safe = platform != null && platform.Safe;
            return platform != null;
        }

        private bool Refill(RefillOriginal original, RoomPlayerWorld world)
            => Owns(world)
                ? GravityPlayerGroundContact.RefillGround(world.SourcePlayer, Inverted(world), runtime.JumpThruContacts)
                : original(world);

        private void AdjustMovingClimbHop(ILContext il)
        {
            var cursor = new ILCursor(il);
            cursor.GotoNext(MoveType.Before,
                instruction =>
                    instruction.MatchCallOrCallvirt<IPlayerFrameWorld>(nameof(IPlayerFrameWorld.MoveVExact)));
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.Emit(OpCodes.Ldfld,
                typeof(PlayerFrameMotion).GetField("world", BindingFlags.NonPublic | BindingFlags.Instance));
            cursor.EmitDelegate<Func<int, IPlayerFrameWorld, int>>((amount, world)
                => world is RoomPlayerWorld actual && Inverted(actual) ? -amount : amount);
        }

        private void AdjustSpeedRing(ILContext il)
        {
            var cursor = new ILCursor(il);
            cursor.GotoNext(MoveType.After,
                instruction => instruction.MatchLdfld<NormalMotionState>(nameof(NormalMotionState.Speed)));
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.EmitDelegate<Func<Vector2, RoomPlayerWorld, Vector2>>((speed, world)
                => Inverted(world) ? new Vector2(speed.X, -speed.Y) : speed);
        }

        public void Dispose()
        {
            for (int index = hooks.Count - 1; index >= 0; index--) hooks[index].Dispose();
            hooks.Clear();
        }

        private delegate bool GroundOriginal(RoomPlayerWorld world, out bool safe);

        private delegate bool GroundHook(GroundOriginal original, RoomPlayerWorld world, out bool safe);

        private delegate bool RefillOriginal(RoomPlayerWorld world);

        private delegate bool RefillHook(RefillOriginal original, RoomPlayerWorld world);
    }
}
