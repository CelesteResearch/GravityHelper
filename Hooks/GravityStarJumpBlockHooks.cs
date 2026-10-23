// Copyright (c) Shane Woolcock. Licensed under the MIT Licence.

using System;
using Celeste.Core.Scheduling;
using Celeste.Game.Entities.StarJump;
using Celeste.Mods.GravityHelper.Runtime;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;

namespace Celeste.Mods.GravityHelper.Hooks
{
    public sealed class GravityStarJumpBlockHooks : IDisposable
    {
        private readonly GravityRuntimeContext runtime;
        private readonly ILHook hook;
        private static readonly Func<OriginalStarJumpBlockState, float> GetYLerp = CreateYLerpGetter();

        public GravityStarJumpBlockHooks(GravityRuntimeContext runtime)
        {
            this.runtime = runtime;
            hook = new ILHook(typeof(OriginalStarJumpBlockState).GetMethod(nameof(OriginalStarJumpBlockState.Update)),
                Update);
        }

        private bool Owns(OriginalStarJumpBlockState self) => self.Scene is ISourceEngineContextScene scene &&
                                                              ReferenceEquals(scene.EngineContext, runtime.Engine);

        private void Update(ILContext il)
        {
            var cursor = new ILCursor(il);
            if (!cursor.TryGotoNext(MoveType.After, i => i.MatchLdfld<OriginalStarJumpBlockState>("yLerp"),
                    i => i.MatchLdcR4(1f)))
                throw new InvalidOperationException("StarJumpBlock active sink target missing.");
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.EmitDelegate<Func<float, OriginalStarJumpBlockState, float>>((value, self) =>
                Owns(self) && (runtime.PlayerComponent?.ShouldInvert ?? false) ? -value : value);
            if (!cursor.TryGotoNext(MoveType.After, i => i.MatchLdcR4(12f)))
                throw new InvalidOperationException("StarJumpBlock sink distance missing.");
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.EmitDelegate<Func<float, OriginalStarJumpBlockState, float>>((value, self) =>
                Owns(self) ? value * Math.Sign(GetYLerp(self)) : value);
        }

        private static Func<OriginalStarJumpBlockState, float> CreateYLerpGetter()
        {
            var method = new System.Reflection.Emit.DynamicMethod("GetStarJumpSinkLerp", typeof(float),
                [typeof(OriginalStarJumpBlockState)], typeof(GravityStarJumpBlockHooks).Module, true);
            var il = method.GetILGenerator();
            il.Emit(System.Reflection.Emit.OpCodes.Ldarg_0);
            il.Emit(System.Reflection.Emit.OpCodes.Ldfld,
                typeof(OriginalStarJumpBlockState).GetField("yLerp",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic));
            il.Emit(System.Reflection.Emit.OpCodes.Ret);
            return (Func<OriginalStarJumpBlockState, float>)method.CreateDelegate(
                typeof(Func<OriginalStarJumpBlockState, float>));
        }

        public void Dispose() => hook.Dispose();
    }
}
