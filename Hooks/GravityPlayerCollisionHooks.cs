// Copyright (c) Shane Woolcock. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Reflection;
using Celeste.Mods.GravityHelper.Runtime;
using Celeste.Physics.Collision;
using Celeste.Physics.Collision.Riding;
using Celeste.Physics.Collision.Vertical;
using Celeste.Scene.Tracking;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Platforms.Foundation;
using Celeste.Simulation.Gameplay.Player.Collision.Responses;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;

namespace Celeste.Mods.GravityHelper.Hooks
{
    public sealed class GravityPlayerCollisionHooks : IDisposable
    {
        private static readonly FieldInfo ActualPlayer = typeof(PlayerCollisionResponse)
            .GetField("actualPlayer", BindingFlags.Instance | BindingFlags.NonPublic);

        private readonly GravityRuntimeContext runtime;
        private readonly List<IDisposable> hooks = [];

        public GravityPlayerCollisionHooks(GravityRuntimeContext runtime)
        {
            this.runtime = runtime;
            hooks.Add(new ILHook(typeof(PlayerCollisionResponse).GetMethod(nameof(PlayerCollisionResponse.Horizontal)),
                AdjustHorizontal));
            hooks.Add(new ILHook(typeof(PlayerCollisionResponse).GetMethod(nameof(PlayerCollisionResponse.Vertical)),
                AdjustVertical));
            hooks.Add(new ILHook(
                typeof(PlayerCollisionResponse).GetMethod(nameof(PlayerCollisionResponse.ReflectBounce)),
                AdjustReflectBounce));
            hooks.Add(new ILHook(typeof(RoomPlayerWorld).GetMethod(nameof(RoomPlayerWorld.LandingSurface)),
                AdjustLandingSurface));
            hooks.Add(new ILHook(typeof(RoomPlayerWorld).GetMethod(nameof(RoomPlayerWorld.LandingDust)),
                AdjustLandingDust));
        }

        private bool Inverted(RoomPlayerSession player)
            => ReferenceEquals(player.EngineContext, runtime.Engine) &&
               (runtime.PlayerComponent?.ShouldInvert ?? false);

        private Vector2 Transform(Vector2 offset, RoomPlayerSession player)
            => Inverted(player) ? new Vector2(offset.X, -offset.Y) : offset;

        private static void EmitPlayer(ILCursor cursor)
        {
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.Emit(OpCodes.Ldfld, ActualPlayer);
        }

        private void TransformVector(ILCursor cursor)
        {
            EmitPlayer(cursor);
            cursor.EmitDelegate<Func<Vector2, RoomPlayerSession, Vector2>>(Transform);
        }

        private void TransformInteger(ILCursor cursor)
        {
            EmitPlayer(cursor);
            cursor.EmitDelegate<Func<int, RoomPlayerSession, int>>((value, player) =>
                Inverted(player) ? -value : value);
        }

        private static bool VectorConstructor(Mono.Cecil.Cil.Instruction instruction)
            => instruction.OpCode == OpCodes.Newobj && instruction.Operand is Mono.Cecil.MethodReference method &&
               method.DeclaringType.FullName == typeof(Vector2).FullName;

        private void AdjustHorizontal(ILContext il)
        {
            var cursor = new ILCursor(il);
            cursor.GotoNext(instruction => instruction.MatchCall<PlayerCollisionResponse>("HorizontalSolidAt"));
            cursor.GotoPrev(MoveType.Before, instruction => instruction.MatchCall<Vector2>(".ctor"));
            EmitPlayer(cursor);
            cursor.EmitDelegate<Func<float, RoomPlayerSession, float>>((value, player) =>
                Inverted(player) ? -value : value);
            cursor.GotoNext(MoveType.After, instruction => instruction.MatchCall<Vector2>("get_UnitY"));
            TransformVector(cursor);
        }

        private void AdjustVertical(ILContext il)
        {
            var cursor = new ILCursor(il);
            cursor.GotoNext(instruction => instruction.MatchCall<PlayerCollisionResponse>("get_DashAttacking"));
            cursor.GotoNext(MoveType.After, instruction => instruction.MatchCall("System.Math", nameof(Math.Sign)));
            TransformInteger(cursor);
            cursor.GotoNext(MoveType.Before,
                instruction =>
                    instruction.MatchCall<PlayerCollisionResponse>(nameof(PlayerCollisionResponse.ReflectBounce)));
            TransformVector(cursor);
            for (int index = 0; index < 2; index++)
            {
                cursor.GotoNext(MoveType.Before,
                    instruction =>
                        instruction.MatchCallOrCallvirt<IPlayerCollisionWorld>(
                            nameof(IPlayerCollisionWorld.MoveVExact)));
                TransformInteger(cursor);
                cursor.Index++;
            }

            int ceilingOffsets = 0;
            while (cursor.TryGotoNext(MoveType.Before, VectorConstructor))
            {
                bool ceiling = cursor.Prev.MatchLdcR4(-1);
                cursor.Index++;
                if (!ceiling) continue;
                TransformVector(cursor);
                ceilingOffsets++;
                if (cursor.Next.MatchCallOrCallvirt<IPlayerCollisionWorld>(nameof(IPlayerCollisionWorld.SolidAt)))
                {
                    cursor.Remove();
                    EmitPlayer(cursor);
                    cursor.EmitDelegate<Func<IPlayerCollisionWorld, Vector2, RoomPlayerSession, bool>>(CeilingBlocked);
                }
            }

            if (ceilingOffsets != 4) throw new InvalidOperationException("Player ceiling correction sites changed.");
        }

        private bool CeilingBlocked(IPlayerCollisionWorld world, Vector2 offset, RoomPlayerSession player)
        {
            if (world.SolidAt(offset)) return true;
            if (!ReferenceEquals(player.EngineContext, runtime.Engine)) return false;
            bool inverted = runtime.PlayerComponent?.ShouldInvert ?? false;
            return runtime.JumpThruContacts.First(player, player.Position + offset, !inverted) != null;
        }

        private void AdjustReflectBounce(ILContext il)
        {
            var cursor = new ILCursor(il);
            cursor.Emit(OpCodes.Ldarg_1);
            TransformVector(cursor);
            cursor.Emit(OpCodes.Starg, 1);
        }

        private void AdjustLandingSurface(ILContext il)
        {
            var cursor = new ILCursor(il);
            cursor.GotoNext(MoveType.After, instruction => instruction.MatchCall<Vector2>("get_UnitY"));
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.EmitDelegate<Func<Vector2, RoomPlayerWorld, Vector2>>((offset, world) =>
                Transform(offset, world.SourcePlayer));
        }

        private void AdjustLandingDust(ILContext il)
        {
            var cursor = new ILCursor(il);
            cursor.GotoNext(MoveType.After, instruction => instruction.MatchLdcR4(-(float)Math.PI / 2));
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.EmitDelegate<Func<float, RoomPlayerWorld, float>>((angle, world) =>
                Inverted(world.SourcePlayer) ? -angle : angle);
        }

        public void Dispose()
        {
            for (int index = hooks.Count - 1; index >= 0; index--) hooks[index].Dispose();
            hooks.Clear();
        }
    }
}
