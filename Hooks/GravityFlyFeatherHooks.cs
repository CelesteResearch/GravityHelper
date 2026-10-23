// Copyright (c) Shane Woolcock. Licensed under the MIT Licence.

using System;
using System.Numerics;
using System.Reflection;
using Celeste.Core.Scheduling;
using Celeste.Game.Entities.StarJump;
using Celeste.Game.Gameplay.Effects.Slashes;
using Celeste.Mods.GravityHelper.Runtime;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using MonoMod.Utils;

namespace Celeste.Mods.GravityHelper.Hooks
{
    public sealed class GravityFlyFeatherHooks : IDisposable
    {
        private readonly GravityRuntimeContext runtime;
        private readonly FieldInfo routineOwner;
        private readonly ILHook hook;

        public GravityFlyFeatherHooks(GravityRuntimeContext runtime)
        {
            this.runtime = runtime;
            var method = typeof(OriginalFlyFeatherState)
                .GetMethod("CollectRoutine", BindingFlags.Instance | BindingFlags.NonPublic).GetStateMachineTarget();
            routineOwner = method.DeclaringType.GetField("<>4__this",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            hook = new ILHook(method, AdjustCollect);
        }

        private float Transform(float angle, OriginalFlyFeatherState feather) =>
            feather.Scene is ISourceEngineContextScene scene && ReferenceEquals(scene.EngineContext, runtime.Engine) &&
            (runtime.PlayerComponent?.ShouldInvert ?? false)
                ? -angle
                : angle;

        private void AdjustCollect(ILContext il)
        {
            var cursor = new ILCursor(il);
            cursor.GotoNext(MoveType.Before,
                i => i.MatchCall(typeof(OriginalSlashFxState), nameof(OriginalSlashFxState.Burst)));
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.Emit(OpCodes.Ldfld, il.Import(routineOwner));
            cursor.EmitDelegate<Func<float, OriginalFlyFeatherState, float>>(Transform);
        }

        public void Dispose() => hook.Dispose();
    }
}
