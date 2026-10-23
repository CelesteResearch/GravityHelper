// Copyright (c) Shane Woolcock. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Reflection;
using Celeste.Core.Scheduling;
using Celeste.Mods.GravityHelper.Runtime;
using Celeste.Physics.Collision;
using Celeste.Physics.Collision.Riding;
using Celeste.Physics.Collision.Vertical;
using Celeste.Scene.Tracking;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Platforms.Foundation;
using Celeste.Simulation.Gameplay.Player.Movement;
using Celeste.Simulation.Gameplay.Player.State;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;

namespace Celeste.Mods.GravityHelper.Hooks
{
    public sealed class GravityPlayerNormalHooks : IDisposable
    {
        private readonly GravityRuntimeContext runtime;
        private readonly Func<bool> suppressWallSlide;
        private readonly List<IDisposable> hooks = [];

        public GravityPlayerNormalHooks(GravityRuntimeContext runtime, Func<bool> suppressWallSlide)
        {
            this.runtime = runtime;
            this.suppressWallSlide = suppressWallSlide;
            hooks.Add(new ILHook(
                typeof(NormalSequence).GetMethod("UpdateCore", BindingFlags.Instance | BindingFlags.NonPublic),
                AdjustNormal));
            hooks.Add(new Hook(typeof(RoomPlayerWorld).GetMethod(nameof(RoomPlayerWorld.ClimbCheck)),
                (ClimbHook)ClimbCheck));
            hooks.Add(new ILHook(typeof(RoomPlayerWorld).GetMethod(nameof(RoomPlayerWorld.JumpFromWater)),
                AdjustWaterJump));
        }

        private bool Inverted(NormalSequence sequence)
            => ReferenceEquals(sequence.EngineContext, runtime.Engine) &&
               (runtime.PlayerComponent?.ShouldInvert ?? false);

        private bool Owns(RoomPlayerWorld world)
            => ReferenceEquals(world.SourcePlayer.EngineContext, runtime.Engine);

        private Vector2 TransformOffset(Vector2 offset, NormalSequence sequence)
            => Inverted(sequence) ? new Vector2(offset.X, -offset.Y) : offset;

        private bool ClimbCheck(ClimbOriginal original, RoomPlayerWorld world, int direction, int yOffset)
            => original(world, direction,
                Owns(world) && (runtime.PlayerComponent?.ShouldInvert ?? false) ? -yOffset : yOffset);

        private bool LeniencySolid(INormalWorld world, Vector2 offset, NormalSequence sequence)
        {
            if (world.SolidAt(offset)) return true;
            if (!ReferenceEquals(sequence.EngineContext, runtime.Engine) || world is not RoomPlayerWorld actual)
                return false;
            var player = actual.SourcePlayer;
            bool inverted = runtime.PlayerComponent?.ShouldInvert ?? false;
            return runtime.JumpThruContacts.First(player, player.Position + offset, !inverted) != null;
        }

        private void AdjustNormal(ILContext il)
        {
            var cursor = new ILCursor(il);
            cursor.GotoNext(MoveType.After, instruction => instruction.MatchCallOrCallvirt<INormalWorld>("get_WindY"));
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.EmitDelegate<Func<float, NormalSequence, float>>((wind, sequence) =>
                Inverted(sequence) ? -wind : wind);
            cursor.GotoNext(MoveType.After, instruction => instruction.OpCode == OpCodes.Newobj &&
                                                           instruction.Operand is Mono.Cecil.MethodReference method &&
                                                           method.DeclaringType.FullName == typeof(Vector2).FullName);
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.EmitDelegate<Func<Vector2, NormalSequence, Vector2>>(TransformOffset);
            cursor.GotoNext(MoveType.Before,
                instruction => instruction.MatchCallOrCallvirt<INormalWorld>(nameof(INormalWorld.SolidAt)));
            cursor.Remove();
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.EmitDelegate<Func<INormalWorld, Vector2, NormalSequence, bool>>(LeniencySolid);
            cursor.GotoNext(MoveType.Before,
                instruction => instruction.MatchStfld<PlayerState>(nameof(PlayerState.WallSlideDirection)));
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.EmitDelegate<Func<int, NormalSequence, int>>((direction, sequence)
                => ReferenceEquals(sequence.EngineContext, runtime.Engine) && suppressWallSlide() ? 0 : direction);
        }

        private void AdjustWaterJump(ILContext il)
        {
            var cursor = new ILCursor(il);
            cursor.GotoNext(MoveType.After, instruction => instruction.MatchCall<Vector2>("get_UnitY"));
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.EmitDelegate<Func<Vector2, RoomPlayerWorld, Vector2>>((offset, world)
                => Owns(world) && (runtime.PlayerComponent?.ShouldInvert ?? false)
                    ? new Vector2(offset.X, -offset.Y)
                    : offset);
        }

        public void Dispose()
        {
            for (int index = hooks.Count - 1; index >= 0; index--) hooks[index].Dispose();
            hooks.Clear();
        }

        private delegate bool ClimbOriginal(RoomPlayerWorld world, int direction, int yOffset);

        private delegate bool ClimbHook(ClimbOriginal original, RoomPlayerWorld world, int direction, int yOffset);
    }
}
