// Copyright (c) Shane Woolcock. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using Celeste.Core.Rendering;
using Celeste.Mods.GravityHelper.Runtime;
using Celeste.Simulation;
using Celeste.Simulation.Presentation.Player.Death;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using MonoMod.Utils;

namespace Celeste.Mods.GravityHelper.Hooks
{
    public sealed class GravityPlayerDeathHooks : IDisposable
    {
        private readonly GravityRuntimeContext runtime;
        private readonly List<IDisposable> hooks = [];

        public GravityPlayerDeathHooks(GravityRuntimeContext runtime)
        {
            this.runtime = runtime;
            hooks.Add(new Hook(
                typeof(PlayerDeathPresentation).GetConstructor([typeof(RoomPlayerSession), typeof(Vector2)]),
                (ConstructorHook)Construct));
            hooks.Add(new ILHook(typeof(PlayerDeathPresentation).GetMethod(nameof(PlayerDeathPresentation.Render)),
                AdjustRender));
            var routine =
                typeof(PlayerDeathPresentation).GetMethod("DeathRoutine",
                    BindingFlags.Instance | BindingFlags.NonPublic);
            hooks.Add(new ILHook(routine.GetStateMachineTarget(), AdjustRoutine));
        }

        private void Construct(ConstructorOriginal original, PlayerDeathPresentation body, RoomPlayerSession player,
            Vector2 direction)
        {
            original(body, player, direction);
            if (ReferenceEquals(player.EngineContext, runtime.Engine))
                DynamicData.For(body).Set("GravityHelper_Inverted", runtime.PlayerComponent?.ShouldInvert ?? false);
        }

        private static bool Inverted(PlayerDeathPresentation body)
            => DynamicData.For(body).Data.TryGetValue("GravityHelper_Inverted", out var value) && (bool)value;

        private static void AdjustRender(ILContext il)
        {
            var cursor = new ILCursor(il);
            cursor.GotoNext(MoveType.Before, instruction => instruction.MatchStfld<Vector2>(nameof(Vector2.Y)));
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.EmitDelegate<Func<float, PlayerDeathPresentation, float>>((scale, body) =>
                Inverted(body) ? -scale : scale);
        }

        private static void AdjustRoutine(ILContext il)
        {
            var bodyField = il.Body.Instructions.Select(instruction => instruction.Operand).OfType<FieldReference>()
                .First(field => field.FieldType.FullName == typeof(PlayerDeathPresentation).FullName);
            var cursor = new ILCursor(il);
            cursor.GotoNext(MoveType.After, instruction => instruction.MatchLdcR4(-5));
            EmitBody(cursor, bodyField);
            cursor.EmitDelegate<Func<float, PlayerDeathPresentation, float>>((amount, body) =>
                Inverted(body) ? -amount : amount);
            cursor.GotoNext(instruction => instruction.MatchLdfld<PlayerDeathPresentation>("initialHairColor"));
            cursor.GotoNext(MoveType.After, instruction => instruction.MatchCall<Vector2>("op_Subtraction"));
            EmitBody(cursor, bodyField);
            cursor.EmitDelegate<Func<Vector2, PlayerDeathPresentation, Vector2>>((offset, body)
                => Inverted(body) ? new Vector2(offset.X, -offset.Y) : offset);
        }

        private static void EmitBody(ILCursor cursor, FieldReference bodyField)
        {
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.Emit(OpCodes.Ldfld, bodyField);
        }

        public void Dispose()
        {
            for (int index = hooks.Count - 1; index >= 0; index--) hooks[index].Dispose();
            hooks.Clear();
        }

        private delegate void ConstructorOriginal(PlayerDeathPresentation body, RoomPlayerSession player,
            Vector2 direction);

        private delegate void ConstructorHook(ConstructorOriginal original, PlayerDeathPresentation body,
            RoomPlayerSession player, Vector2 direction);
    }
}
