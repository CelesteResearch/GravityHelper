// Copyright (c) Shane Woolcock. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Numerics;
using Celeste.Mods.GravityHelper.Runtime;
using Celeste.Simulation;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;

namespace Celeste.Mods.GravityHelper.Hooks
{
    public sealed class GravityPlayerTransitionHooks : IDisposable
    {
        private readonly GravityRuntimeContext runtime;
        private readonly List<IDisposable> hooks = new();
        private int preparationDepth;
        private bool preparingUp;

        public GravityPlayerTransitionHooks(GravityRuntimeContext runtime)
        {
            this.runtime = runtime;
            try
            {
                hooks.Add(new ILHook(typeof(RoomPlayerSession).GetMethod(nameof(RoomPlayerSession.BeforeUpTransition)),
                    il => InsertPreparation(il, BeforeUp)));
                hooks.Add(new ILHook(
                    typeof(RoomPlayerSession).GetMethod(nameof(RoomPlayerSession.BeforeDownTransition)),
                    PatchBeforeDown));
                hooks.Add(new Hook(typeof(RoomPlayerSession).GetMethod(nameof(RoomPlayerSession.TransitionTo)),
                    (Func<Func<RoomPlayerSession, Vector2, Vector2, bool>, RoomPlayerSession, Vector2, Vector2, bool>)
                    TransitionTo));
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        private bool Inverted(RoomPlayerSession player) => ReferenceEquals(player.EngineContext, runtime.Engine) &&
                                                           (runtime.PlayerComponent?.ShouldInvert ?? false);

        private bool BeforeUp(RoomPlayerSession player)
        {
            if (preparationDepth != 0 || !Inverted(player)) return false;
            preparationDepth++;
            preparingUp = true;
            player.BeforeDownTransition();
            preparingUp = false;
            preparationDepth--;
            return true;
        }

        private bool BeforeDown(RoomPlayerSession player)
        {
            if (preparationDepth != 0 || !Inverted(player)) return false;
            preparationDepth++;
            bool redDash = player.Machine.State == 5;
            float speedX = player.Data.Motion.Normal.Speed.X;
            player.BeforeUpTransition();
            // The fixed Gravity source deliberately keeps horizontal speed during red dash.
            if (redDash) player.Data.Motion.Normal.Speed.X = speedX;
            preparationDepth--;
            return true;
        }

        private void PatchBeforeDown(ILContext il)
        {
            var cursor = new ILCursor(il);
            if (!cursor.TryGotoNext(MoveType.After,
                    instruction => instruction.Operand is MethodReference method &&
                                   method.DeclaringType.FullName == typeof(Vector2).FullName &&
                                   method.Name == "get_UnitY"))
                throw new InvalidOperationException("BeforeDownTransition platform offset UnitY missing.");
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.EmitDelegate<Func<Vector2, RoomPlayerSession, Vector2>>((value, player) =>
                preparingUp && preparationDepth != 0 && Inverted(player) ? -value : value);
            InsertPreparation(il, BeforeDown);
        }

        private static void InsertPreparation(ILContext il, Func<RoomPlayerSession, bool> prepare)
        {
            var cursor = new ILCursor(il);
            Instruction original = cursor.Next;
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.EmitDelegate(prepare);
            cursor.Emit(OpCodes.Brfalse, original);
            cursor.Emit(OpCodes.Ret);
        }

        private bool TransitionTo(Func<RoomPlayerSession, Vector2, Vector2, bool> original, RoomPlayerSession player,
            Vector2 target, Vector2 direction)
        {
            if (!ReferenceEquals(player.EngineContext, runtime.Engine)) return original(player, target, direction);
            runtime.Overrides.Semaphore++;
            bool result = original(player, target, direction);
            runtime.Overrides.Semaphore--;
            return result;
        }

        public void Dispose()
        {
            for (int index = hooks.Count - 1; index >= 0; index--) hooks[index].Dispose();
            hooks.Clear();
        }
    }
}
