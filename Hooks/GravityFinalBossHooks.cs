// Copyright (c) Shane Woolcock. Licensed under the MIT Licence.

using System;
using System.Reflection;
using System.Linq;
using Celeste.Mods.GravityHelper.Runtime;
using Celeste.Core.Scheduling;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Gameplay.Enemies.BadelineBoss;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using MonoMod.Utils;

namespace Celeste.Mods.GravityHelper.Hooks
{
    public sealed class GravityFinalBossHooks : IDisposable
    {
        private readonly GravityRuntimeContext runtime;
        private readonly ILHook hook;

        public GravityFinalBossHooks(GravityRuntimeContext runtime)
        {
            this.runtime = runtime;
            hook = new ILHook(
                typeof(FinalBossState).GetMethod(nameof(FinalBossState.MoveSequence)).GetStateMachineTarget(), Modify);
        }

        private void Modify(ILContext il)
        {
            var cursor = new ILCursor(il);
            cursor.GotoNext(MoveType.After, instruction => instruction.MatchLdcR4(4f));
            var owner = typeof(FinalBossState).GetMethod(nameof(FinalBossState.MoveSequence)).GetStateMachineTarget()
                .DeclaringType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Single(field => field.FieldType == typeof(FinalBossState));
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.Emit(OpCodes.Ldfld, il.Import(owner));
            cursor.EmitDelegate<Func<float, FinalBossState, float>>((offset, boss) =>
                boss.Scene is ISourceEngineContextScene scene && ReferenceEquals(scene.EngineContext, runtime.Engine) &&
                runtime.PlayerComponent?.ShouldInvert == true
                    ? -offset
                    : offset);
        }

        public void Dispose() => hook.Dispose();
    }
}
