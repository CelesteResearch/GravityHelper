// Copyright (c) Shane Woolcock. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Celeste.Core.Scheduling;
using Celeste.Game.Gameplay.Enemies.Badeline.Chasers;
using Celeste.Mods.GravityHelper.Gravity;
using Celeste.Mods.GravityHelper.Runtime;
using Celeste.Physics.Collision.Shapes;
using Celeste.Scene.Entities;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Enemies.Badeline.Chasers;
using Celeste.Simulation.Gameplay.Player.ChaseHistory;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using MonoMod.Utils;

namespace Celeste.Mods.GravityHelper.Hooks
{
    public sealed class GravityChaserHooks : IDisposable
    {
        private readonly GravityRuntimeContext runtime;
        private readonly List<IDisposable> hooks = [];

        private static readonly FieldInfo PlayerField = typeof(PlayerChaseHistory).GetField("actualPlayer",
            BindingFlags.Instance | BindingFlags.NonPublic);

        public GravityChaserHooks(GravityRuntimeContext runtime)
        {
            this.runtime = runtime;
            hooks.Add(new ILHook(typeof(PlayerChaseHistory).GetMethod(nameof(PlayerChaseHistory.Sample)),
                AdjustSample));
            hooks.Add(new ILHook(typeof(BadelineOldsiteState).GetMethod(nameof(BadelineOldsiteState.Update)),
                AdjustChaser));
        }

        private void AdjustSample(ILContext il)
        {
            var cursor = new ILCursor(il);
            cursor.GotoNext(MoveType.Before,
                instruction => instruction.MatchCallOrCallvirt<List<ChaserState>>("RemoveAt"));
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.Emit(OpCodes.Ldfld, PlayerField);
            cursor.EmitDelegate<Func<int, RoomPlayerSession, int>>((index, player) =>
            {
                if (ReferenceEquals(player.EngineContext, runtime.Engine))
                    runtime.ChaserHistory.RemoveGravityTypeForState(player.ChaserStates[index].TimeStamp);
                return index;
            });
            cursor.GotoNext(MoveType.Before, instruction => instruction.MatchCallOrCallvirt<List<ChaserState>>("Add"));
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.Emit(OpCodes.Ldfld, PlayerField);
            cursor.EmitDelegate<Func<ChaserState, RoomPlayerSession, ChaserState>>((state, player) =>
            {
                if (ReferenceEquals(player.EngineContext, runtime.Engine))
                    runtime.ChaserHistory.SetGravityTypeForState(state.TimeStamp,
                        runtime.PlayerComponent?.CurrentGravity ?? GravityType.Normal);
                return state;
            });
        }

        private void AdjustChaser(ILContext il)
        {
            var state = il.Body.Variables.Single(variable =>
                variable.VariableType.FullName == typeof(ChaserState).FullName);
            var cursor = new ILCursor(il);
            cursor.GotoNext(MoveType.After,
                instruction => instruction.MatchCallOrCallvirt<SceneEntityState>("set_Position"));
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.Emit(OpCodes.Ldloc, state);
            cursor.EmitDelegate<Action<BadelineOldsiteState, ChaserState>>((chaser, sample) =>
            {
                if (chaser.Scene is not ISourceEngineContextScene scene ||
                    !ReferenceEquals(scene.EngineContext, runtime.Engine) ||
                    !runtime.ChaserHistory.TryGravityTypeForState(sample.TimeStamp, out var gravity)) return;
                bool inverted = gravity == GravityType.Inverted;
                chaser.Collider = new HitboxColliderGeometry(6f, 6f, -3f, inverted ? 1f : -7f);
                DynamicData.For(chaser).Data["GravityHelper_Inverted"] = inverted;
            });
        }

        public void Dispose()
        {
            for (int index = hooks.Count - 1; index >= 0; index--) hooks[index].Dispose();
            hooks.Clear();
        }
    }
}
