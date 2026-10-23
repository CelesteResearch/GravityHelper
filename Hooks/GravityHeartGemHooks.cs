// Copyright (c) Shane Woolcock. Licensed under the MIT Licence.

using System;
using System.Linq;
using System.Reflection;
using Celeste.Mods.GravityHelper.Runtime;
using Celeste.Core.Scheduling;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Entities.Collectibles.Hearts;
using Celeste.Game.Entities.Platforms.Barriers;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using MonoMod.Utils;

namespace Celeste.Mods.GravityHelper.Hooks
{
    public sealed class GravityHeartGemHooks : IDisposable
    {
        private readonly GravityRuntimeContext runtime;
        private readonly ILHook hook;

        public GravityHeartGemHooks(GravityRuntimeContext runtime)
        {
            this.runtime = runtime;
            hook = new ILHook(
                typeof(OriginalHeartGemState)
                    .GetMethod("OriginalCollectRoutine", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetStateMachineTarget(), Modify);
        }

        private void Modify(ILContext il)
        {
            var cursor = new ILCursor(il);
            for (int index = 0; index < 3; index++)
                cursor.GotoNext(MoveType.After,
                    instruction => instruction.MatchNewobj<OriginalInvisibleBarrierState>());
            var owner = typeof(OriginalHeartGemState)
                .GetMethod("OriginalCollectRoutine", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetStateMachineTarget().DeclaringType
                .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Single(field => field.FieldType == typeof(OriginalHeartGemState));
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.Emit(OpCodes.Ldfld, il.Import(owner));
            cursor
                .EmitDelegate<
                    Func<OriginalInvisibleBarrierState, OriginalHeartGemState, OriginalInvisibleBarrierState>>((barrier,
                    heart) =>
                {
                    if (heart.Scene is OriginalLevelSceneRoot level &&
                        ReferenceEquals(level.EngineContext, runtime.Engine) &&
                        runtime.PlayerComponent?.ShouldInvert == true)
                        barrier.Y += level.Bounds.Height;
                    return barrier;
                });
        }

        public void Dispose() => hook.Dispose();
    }
}
