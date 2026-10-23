// Copyright (c) Shane Woolcock. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Reflection.Emit;
using Celeste.Mods.GravityHelper.Runtime;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Holdables;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using MonoMod.Utils;
using OpCodes = Mono.Cecil.Cil.OpCodes;

namespace Celeste.Mods.GravityHelper.Hooks
{
    public sealed class GravityPlayerCarryHooks : IDisposable
    {
        private readonly GravityRuntimeContext runtime;
        private readonly Func<PlayerHoldingController, RoomPlayerSession> owner;
        private readonly List<IDisposable> hooks = [];

        public GravityPlayerCarryHooks(GravityRuntimeContext runtime)
        {
            this.runtime = runtime;
            var getter = new DynamicMethod("GravityHoldingPlayer", typeof(RoomPlayerSession),
                [typeof(PlayerHoldingController)], typeof(GravityPlayerCarryHooks), true);
            var il = getter.GetILGenerator();
            il.Emit(System.Reflection.Emit.OpCodes.Ldarg_0);
            il.Emit(System.Reflection.Emit.OpCodes.Ldfld,
                typeof(PlayerHoldingController).GetField("player", BindingFlags.Instance | BindingFlags.NonPublic));
            il.Emit(System.Reflection.Emit.OpCodes.Ret);
            owner = (Func<PlayerHoldingController, RoomPlayerSession>)getter.CreateDelegate(
                typeof(Func<PlayerHoldingController, RoomPlayerSession>));
            hooks.Add(new ILHook(typeof(PlayerHoldingController).GetMethod(nameof(PlayerHoldingController.UpdateCarry)),
                Carry));
            hooks.Add(new ILHook(
                typeof(PlayerHoldingController)
                    .GetMethod("PickupCoroutine", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetStateMachineTarget(), Pickup));
        }

        private Vector2 Flip(Vector2 value, PlayerHoldingController controller)
        {
            if (ReferenceEquals(owner(controller).EngineContext, runtime.Engine) &&
                (runtime.PlayerComponent?.ShouldInvert ?? false)) value.Y = -value.Y;
            return value;
        }

        private void Carry(ILContext il)
        {
            var cursor = new ILCursor(il);
            cursor.GotoNext(MoveType.After,
                instruction => instruction.MatchCall<PlayerHoldingController>("get_CarryOffset"));
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.EmitDelegate<Func<Vector2, PlayerHoldingController, Vector2>>(Flip);
            cursor.GotoNext(MoveType.After, instruction => instruction.MatchCall<Vector2>("get_UnitY"));
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.EmitDelegate<Func<Vector2, PlayerHoldingController, Vector2>>(Flip);
        }

        private void Pickup(ILContext il)
        {
            var routine = typeof(PlayerHoldingController)
                .GetMethod("PickupCoroutine", BindingFlags.Instance | BindingFlags.NonPublic).GetStateMachineTarget();
            var controller = routine.DeclaringType
                .GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)
                .Single(field => field.FieldType == typeof(PlayerHoldingController));
            var cursor = new ILCursor(il);
            cursor.GotoNext(MoveType.After, instruction => instruction.MatchCall<Vector2>("op_Subtraction"));
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.Emit(OpCodes.Ldfld, controller);
            cursor.EmitDelegate<Func<Vector2, PlayerHoldingController, Vector2>>(Flip);
        }

        public void Dispose()
        {
            for (int index = hooks.Count - 1; index >= 0; index--) hooks[index].Dispose();
            hooks.Clear();
        }
    }
}
