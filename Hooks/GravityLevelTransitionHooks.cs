// Copyright (c) Shane Woolcock. Licensed under the MIT Licence.

using System;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using Celeste.Game.Execution.Levels.Transitions;
using Celeste.Mods.GravityHelper.Runtime;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;

namespace Celeste.Mods.GravityHelper.Hooks
{
    public sealed class GravityLevelTransitionHooks : IDisposable
    {
        private readonly GravityRuntimeContext runtime;
        private readonly ILHook hook;

        public GravityLevelTransitionHooks(GravityRuntimeContext runtime)
        {
            this.runtime = runtime;
            var method =
                typeof(OriginalLevelTransitionSequence).GetMethod(nameof(OriginalLevelTransitionSequence
                    .CreateOriginalRoutine));
            var iterator = method.GetCustomAttribute<IteratorStateMachineAttribute>().StateMachineType;
            hook = new ILHook(iterator.GetMethod("MoveNext", BindingFlags.Instance | BindingFlags.NonPublic),
                il => Patch(il, iterator.GetField("<>4__this")));
        }

        private bool Inverted(OriginalLevelTransitionSequence sequence) =>
            ReferenceEquals(sequence.Level.EngineContext, runtime.Engine) &&
            (runtime.PlayerComponent?.ShouldInvert ?? false);

        private void Patch(ILContext il, FieldInfo owner)
        {
            var cursor = new ILCursor(il);
            if (!cursor.TryGotoNext(MoveType.Before,
                    instruction => instruction.Operand is MethodReference method && method.Name == "get_UnitY" &&
                                   instruction.Next.Operand is MethodReference equality &&
                                   equality.Name == "op_Equality"))
                throw new InvalidOperationException("Transition direction UnitY comparison missing.");
            LoadOwner(cursor, owner);
            cursor.EmitDelegate<Func<Vector2, OriginalLevelTransitionSequence, Vector2>>((value, sequence) =>
                Inverted(sequence) ? new Vector2(value.X, -value.Y) : value);
            if (!cursor.TryGotoNext(MoveType.After,
                    instruction => instruction.MatchLdcR4(1) && instruction.Next.OpCode == OpCodes.Sub))
                throw new InvalidOperationException("Transition playerTo.Y step missing.");
            LoadOwner(cursor, owner);
            cursor.EmitDelegate<Func<float, OriginalLevelTransitionSequence, float>>((value, sequence) =>
                Inverted(sequence) ? -value : value);
            if (!cursor.TryGotoNext(MoveType.After,
                    instruction => instruction.Operand is FieldReference field &&
                                   field.DeclaringType.FullName == typeof(Vector2).FullName && field.Name == "Y"))
                throw new InvalidOperationException("Transition playerTo.Y bounds comparison missing.");
            LoadOwner(cursor, owner);
            cursor.EmitDelegate<Func<float, OriginalLevelTransitionSequence, float>>((value, sequence) =>
                Inverted(sequence) ? -value : value);
            if (!cursor.TryGotoNext(MoveType.After,
                    instruction => instruction.Operand is MethodReference method && method.Name == "get_Bottom"))
                throw new InvalidOperationException("Transition lower bounds comparison missing.");
            LoadOwner(cursor, owner);
            cursor.EmitDelegate<Func<int, OriginalLevelTransitionSequence, int>>((value, sequence) =>
                Inverted(sequence) ? -sequence.Level.Bounds.Top : value);
        }

        private static void LoadOwner(ILCursor cursor, FieldInfo owner)
        {
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.Emit(OpCodes.Ldfld, owner);
        }

        public void Dispose() => hook.Dispose();
    }
}
