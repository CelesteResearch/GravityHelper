// Copyright (c) Shane Woolcock. Licensed under the MIT Licence.

using System;
using System.Linq;
using System.Linq.Expressions;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using Celeste.Core.Scheduling;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Execution.Processes;
using Celeste.Mods.GravityHelper.Runtime;
using Celeste.Mods.GravityHelper.Runtime.Particles;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Player.Intro;
using Celeste.Simulation.Presentation.Particles.Definitions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;

namespace Celeste.Mods.GravityHelper.Hooks
{
    public sealed class GravityPlayerIntroHooks : IDisposable
    {
        private static readonly Func<PlayerIntroSequence, ISourceEngineContext> ReadEngine = CreateEngineReader();
        private static readonly Func<IPlayerIntroWorld, RoomPlayerSession> ReadPlayer = CreatePlayerReader();
        private readonly GravityRuntimeContext runtime;
        private readonly ILHook hook;
        private readonly InvertedSummitLandParticles particles;

        public GravityPlayerIntroHooks(GravityRuntimeContext runtime)
        {
            this.runtime = runtime;
            particles = InvertedSummitLandParticles.For(runtime.Engine);
            var method = typeof(PlayerIntroSequence).GetMethod(nameof(PlayerIntroSequence.JumpCoroutine));
            var iterator = method.GetCustomAttribute<IteratorStateMachineAttribute>().StateMachineType;
            hook = new ILHook(iterator.GetMethod("MoveNext", BindingFlags.NonPublic | BindingFlags.Instance),
                il => Patch(il, iterator.GetField("<>4__this")));
        }

        private bool Inverted(PlayerIntroSequence sequence) => ReferenceEquals(ReadEngine(sequence), runtime.Engine) &&
                                                               (runtime.PlayerComponent?.ShouldInvert ?? false);

        private static Func<PlayerIntroSequence, ISourceEngineContext> CreateEngineReader()
        {
            var value = Expression.Parameter(typeof(PlayerIntroSequence));
            return Expression
                .Lambda<Func<PlayerIntroSequence, ISourceEngineContext>>(Expression.Field(value, "engine"), value)
                .Compile();
        }

        private static Func<IPlayerIntroWorld, RoomPlayerSession> CreatePlayerReader()
        {
            Type type = typeof(RoomPlayerSession).GetNestedType("IntroWorld", BindingFlags.NonPublic);
            var value = Expression.Parameter(typeof(IPlayerIntroWorld));
            return Expression.Lambda<Func<IPlayerIntroWorld, RoomPlayerSession>>(
                Expression.Condition(Expression.TypeIs(value, type),
                    Expression.Field(Expression.Convert(value, type), "p"),
                    Expression.Constant(null, typeof(RoomPlayerSession))), value).Compile();
        }

        private void Patch(ILContext il, FieldInfo owner)
        {
            var cursor = new ILCursor(il);
            if (!cursor.TryGotoNext(MoveType.After, instruction => instruction.MatchLdcI4(16),
                    instruction => instruction.OpCode == OpCodes.Add))
                throw new InvalidOperationException("IntroJump initial Bottom+16 missing.");
            LoadOwner(cursor, owner);
            cursor.EmitDelegate<Func<int, PlayerIntroSequence, int>>((value, sequence) =>
                Inverted(sequence) ? sequence.World.RoomBounds.Top - 16 : value);
            if (!cursor.TryGotoNext(MoveType.Before,
                    instruction => instruction.Operand is FieldReference field &&
                                   field.DeclaringType.FullName == typeof(Vector2).FullName && field.Name == "Y" &&
                                   instruction.OpCode == OpCodes.Stfld))
                throw new InvalidOperationException("IntroJump summit start.Y missing.");
            LoadOwner(cursor, owner);
            cursor.EmitDelegate<Func<float, PlayerIntroSequence, float>>((value, sequence) =>
                Inverted(sequence) ? sequence.World.RoomBounds.Top + 24 : value);
            if (!cursor.TryGotoNext(MoveType.After, instruction => instruction.MatchLdcR4(-120)))
                throw new InvalidOperationException("IntroJump rise -120 missing.");
            LoadOwner(cursor, owner);
            cursor.EmitDelegate<Func<float, PlayerIntroSequence, float>>((value, sequence) =>
                Inverted(sequence) ? -value : value);
            if (!cursor.TryGotoNext(MoveType.After,
                    instruction => instruction.MatchLdcR4(8) && instruction.Next.OpCode == OpCodes.Sub))
                throw new InvalidOperationException("IntroJump rise threshold -8 missing.");
            LoadOwner(cursor, owner);
            cursor.EmitDelegate<Func<float, PlayerIntroSequence, float>>((value, sequence) =>
                Inverted(sequence) ? -value : value);
            if (!cursor.TryGotoNext(MoveType.Before,
                    instruction => instruction.OpCode == OpCodes.Bgt || instruction.OpCode == OpCodes.Bgt_S))
                throw new InvalidOperationException("IntroJump rise comparison missing.");
            Instruction compare = cursor.Next, afterCompare = compare.Next;
            object rise = compare.Operand;
            cursor.Goto(compare, MoveType.AfterLabel);
            LoadOwner(cursor, owner);
            cursor.EmitDelegate<Func<PlayerIntroSequence, bool>>(Inverted);
            cursor.Emit(OpCodes.Brfalse, compare);
            cursor.Emit(OpCodes.Blt, rise);
            cursor.Emit(OpCodes.Br, afterCompare);
            var emissions = il.Instrs.Where(instruction =>
                    instruction.Operand is MethodReference method &&
                    method.Name == nameof(IPlayerIntroWorld.EmitSummitLand))
                .ToArray();
            if (emissions.Length != 4)
                throw new InvalidOperationException("IntroJump four summit particle emissions missing.");
            cursor.Goto(emissions[0]);
            if (!cursor.TryGotoPrev(MoveType.Before, instruction => instruction.MatchLdcI4(0),
                    instruction => instruction.MatchLdcI4(0), instruction => instruction.MatchLdcI4(12)))
                throw new InvalidOperationException("IntroJump summit particle entry missing.");
            cursor.Index -= 2;
            cursor.Goto(cursor.Next, MoveType.AfterLabel);
            LoadOwner(cursor, owner);
            cursor.EmitDelegate<Func<PlayerIntroSequence, bool>>(EmitParticles);
            cursor.Emit(OpCodes.Brtrue, emissions[3].Next);
        }

        private bool EmitParticles(PlayerIntroSequence sequence)
        {
            if (!Inverted(sequence)) return false;
            var player = ReadPlayer(sequence.World);
            if (player == null)
                throw new InvalidOperationException("Inverted source IntroJump requires its actual player world.");
            var level = (OriginalLevelSceneRoot)player.Scene;
            level.Particles.Emit(((IOriginalGameplayProcess)runtime.Engine).ParticleTypes.Player.P_SummitLandA, 12,
                player.TopCenter, Vector2.UnitX * 3, (float)Math.PI / 2);
            level.Particles.Emit(particles.B, 8, player.TopCenter - Vector2.UnitX * 2, Vector2.UnitX * 2,
                -(float)Math.PI * 13 / 12);
            level.Particles.Emit(particles.B, 8, player.TopCenter + Vector2.UnitX * 2, Vector2.UnitX * 2,
                (float)Math.PI / 12);
            level.ParticlesBG.Emit(particles.C, 30, player.TopCenter, Vector2.UnitX * 5);
            return true;
        }

        private static void LoadOwner(ILCursor cursor, FieldInfo owner)
        {
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.Emit(OpCodes.Ldfld, owner);
        }

        public void Dispose() => hook.Dispose();
    }
}
