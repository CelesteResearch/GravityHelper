// Copyright (c) Shane Woolcock. Licensed under the MIT Licence.

using System;
using System.Reflection;
using Celeste.Game.Gameplay.Platforms.CrushBlocks;
using Celeste.Mods.GravityHelper.Interop;
using Celeste.Mods.GravityHelper.Runtime;
using MonoMod.Cil;
using Mono.Cecil.Cil;
using MonoMod.RuntimeDetour;

namespace Celeste.Mods.GravityHelper.Hooks
{
    public sealed class GravityCrushBlockHooks : IDisposable
    {
        private readonly GravityRuntimeContext runtime;
        private readonly ILHook hook;

        public GravityCrushBlockHooks(GravityRuntimeContext runtime)
        {
            this.runtime = runtime;
            hook = new ILHook(typeof(OriginalCrushBlockState).GetMethod("Update"), Patch);
        }

        private float Invert(float value, OriginalCrushBlockState self) =>
            ReferenceEquals(MovingBlockSourceLoans.CrushEngine(self), runtime.Engine) &&
            runtime.PlayerComponent?.ShouldInvert == true
                ? -value
                : value;

        private void Patch(ILContext il)
        {
            var cursor = new ILCursor(il);
            if (!cursor.TryGotoNext(MoveType.After, i => i.MatchLdcR4(0), i => i.MatchLdcR4(-1)))
                throw new InvalidOperationException("Crush Update vertical player offset not found");
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.EmitDelegate<Func<float, OriginalCrushBlockState, float>>(Invert);
            if (!cursor.TryGotoNext(MoveType.After, i => i.MatchLdcR4(1)))
                throw new InvalidOperationException("Crush Update face.Y decrement not found");
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.EmitDelegate<Func<float, OriginalCrushBlockState, float>>(Invert);
        }

        public void Dispose() => hook.Dispose();
    }
}
