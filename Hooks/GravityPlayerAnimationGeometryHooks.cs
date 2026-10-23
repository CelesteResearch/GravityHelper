// Copyright (c) Shane Woolcock. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Reflection;
using System.Reflection.Emit;
using Celeste.Game.Execution.Levels;
using Celeste.Mods.GravityHelper.Runtime;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Player.Audio.Frame;
using Celeste.Simulation.Gameplay.Player.Presentation.Animation;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using OpCodes = Mono.Cecil.Cil.OpCodes;

namespace Celeste.Mods.GravityHelper.Hooks
{
    public sealed class GravityPlayerAnimationGeometryHooks : IDisposable
    {
        private readonly GravityRuntimeContext runtime;
        private readonly List<IDisposable> hooks = [];
        private readonly Func<PlayerFrameAudioState, RoomPlayerSession> framePlayer;
        private readonly Func<object, RoomPlayerSession> frameWorldPlayer;

        public GravityPlayerAnimationGeometryHooks(GravityRuntimeContext runtime)
        {
            this.runtime = runtime;
            Type world = typeof(RoomPlayerSession).GetNestedType("FrameAudioWorld", BindingFlags.NonPublic);
            var playerField = world.GetField("player", BindingFlags.Instance | BindingFlags.NonPublic);
            frameWorldPlayer = MakeGetter<object>(world, playerField, null);
            framePlayer = MakeGetter<PlayerFrameAudioState>(world, playerField,
                typeof(PlayerFrameAudioState).GetField("world", BindingFlags.Instance | BindingFlags.NonPublic));
            hooks.Add(new ILHook(typeof(PlayerAnimation).GetMethod(nameof(PlayerAnimation.Update)), Animation));
            hooks.Add(new ILHook(world.GetMethod("StepSurface"), Footstep));
            hooks.Add(new ILHook(typeof(PlayerFrameAudioState).GetMethod(nameof(PlayerFrameAudioState.OnFrameChange)),
                PushDust));
            hooks.Add(new ILHook(typeof(OriginalLevelSceneRoot).GetMethod(
                "Celeste.Simulation.Gameplay.Player.Scene.Particles.ISourcePlayerJumpScene.BurstPlayerWallSlideDust",
                BindingFlags.Instance | BindingFlags.NonPublic), WallSlideDust));
        }

        private static Func<T, RoomPlayerSession> MakeGetter<T>(Type world, FieldInfo player, FieldInfo worldField)
        {
            var method = new DynamicMethod("GravityFramePlayer", typeof(RoomPlayerSession), [typeof(T)],
                typeof(GravityPlayerAnimationGeometryHooks), true);
            var il = method.GetILGenerator();
            il.Emit(System.Reflection.Emit.OpCodes.Ldarg_0);
            if (worldField != null) il.Emit(System.Reflection.Emit.OpCodes.Ldfld, worldField);
            il.Emit(System.Reflection.Emit.OpCodes.Isinst, world);
            var present = il.DefineLabel();
            il.Emit(System.Reflection.Emit.OpCodes.Dup);
            il.Emit(System.Reflection.Emit.OpCodes.Brtrue_S, present);
            il.Emit(System.Reflection.Emit.OpCodes.Pop);
            il.Emit(System.Reflection.Emit.OpCodes.Ldnull);
            il.Emit(System.Reflection.Emit.OpCodes.Ret);
            il.MarkLabel(present);
            il.Emit(System.Reflection.Emit.OpCodes.Ldfld, player);
            il.Emit(System.Reflection.Emit.OpCodes.Ret);
            return (Func<T, RoomPlayerSession>)method.CreateDelegate(typeof(Func<T, RoomPlayerSession>));
        }

        private bool Inverted(RoomPlayerSession player) => player != null &&
                                                           ReferenceEquals(player.EngineContext, runtime.Engine) &&
                                                           (runtime.PlayerComponent?.ShouldInvert ?? false);

        private float Flip(float value, RoomPlayerSession player) => Inverted(player) ? -value : value;

        private float AnimationFlip(float value, IPlayerAnimationWorld world) =>
            world is RoomPlayerWorld actual ? Flip(value, actual.SourcePlayer) : value;

        private void Animation(ILContext il)
        {
            var cursor = new ILCursor(il);
            cursor.GotoNext(instruction => instruction.MatchLdstr("dangling"));
            cursor.GotoPrev(MoveType.After, instruction => instruction.MatchLdcR4(6));
            cursor.Emit(OpCodes.Ldarg, 4);
            cursor.EmitDelegate<Func<float, IPlayerAnimationWorld, float>>(AnimationFlip);
            cursor.GotoNext(instruction => instruction.MatchLdstr("idle_carry"));
            int sites = 0;
            while (cursor.TryGotoNext(MoveType.Before,
                       instruction => instruction.MatchCallOrCallvirt<IPlayerAnimationWorld>("SolidPointAt") ||
                                      instruction.MatchCallOrCallvirt<IPlayerAnimationWorld>("JumpThruAt")))
            {
                bool jump = cursor.Next.MatchCallOrCallvirt<IPlayerAnimationWorld>("JumpThruAt");
                var previous = cursor.Next;
                cursor.Remove();
                cursor.EmitDelegate<Func<IPlayerAnimationWorld, Vector2, bool>>((world, offset) =>
                {
                    if (world is not RoomPlayerWorld actual ||
                        !ReferenceEquals(actual.SourcePlayer.EngineContext, runtime.Engine))
                        return jump ? world.JumpThruAt(offset) : world.SolidPointAt(offset);
                    var player = actual.SourcePlayer;
                    if (Inverted(player)) offset.Y = -offset.Y;
                    return jump
                        ? runtime.JumpThruContacts.First(player, player.Position + offset, Inverted(player)) != null
                        : world.SolidPointAt(offset);
                });
                sites++;
            }

            if (sites != 6) throw new InvalidOperationException("Player edge animation probe sites changed.");
        }

        private void Footstep(ILContext il)
        {
            var cursor = new ILCursor(il);
            cursor.GotoNext(MoveType.After, instruction => instruction.MatchCall<Vector2>("get_UnitY"));
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.EmitDelegate<Func<Vector2, object, Vector2>>((offset, world) =>
                Inverted(frameWorldPlayer(world)) ? new Vector2(offset.X, -offset.Y) : offset);
        }

        private void PushDust(ILContext il)
        {
            var cursor = new ILCursor(il);
            foreach (float source in new[] { -.5f, -1f })
            {
                cursor.GotoNext(MoveType.After, instruction => instruction.MatchLdcR4(source));
                cursor.Emit(OpCodes.Ldarg_0);
                cursor.EmitDelegate<Func<float, PlayerFrameAudioState, float>>((value, state) =>
                    Flip(value, framePlayer(state)));
            }
        }

        private void WallSlideDust(ILContext il)
        {
            var cursor = new ILCursor(il);
            int count = 0;
            while (cursor.TryGotoNext(MoveType.After,
                       instruction => instruction.MatchLdcR4(4) && instruction.Next.MatchNewobj<Vector2>()))
            {
                cursor.Emit(OpCodes.Ldarg_1);
                cursor.EmitDelegate<Func<float, RoomPlayerSession, float>>(Flip);
                count++;
            }

            cursor.GotoNext(MoveType.Before,
                instruction =>
                    instruction.MatchCall(typeof(Celeste.Game.Presentation.Particles.OriginalDust), "Burst"));
            // Burst consumes count and particle after direction: change the stored direction constant in place.
            cursor.GotoPrev(MoveType.After,
                instruction => instruction.MatchLdcR4(out float value) && value == -(float)Math.PI / 2f);
            cursor.Emit(OpCodes.Ldarg_1);
            cursor.EmitDelegate<Func<float, RoomPlayerSession, float>>(Flip);
            if (count != 2) throw new InvalidOperationException("Wall slide dust offset sites changed.");
        }

        public void Dispose()
        {
            for (int index = hooks.Count - 1; index >= 0; index--) hooks[index].Dispose();
            hooks.Clear();
        }
    }
}
