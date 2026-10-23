// Copyright (c) Shane Woolcock. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Reflection;
using Celeste.Core.Scheduling;
using Celeste.Game.Entities.StarJump;
using Celeste.Game.Gameplay.Platforms.FloatySpaceBlocks;
using Celeste.Mods.GravityHelper.Runtime;
using Celeste.Simulation.Gameplay.Platforms.FloatySpaceBlock;
using Celeste.Scene.Entities;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;

namespace Celeste.Mods.GravityHelper.Hooks
{
    public sealed class GravityFloatySpaceBlockHooks : IDisposable
    {
        private readonly GravityRuntimeContext runtime;
        private readonly List<IDisposable> hooks = [];
        private static readonly Func<FloatySpaceBlockState, float> GetYLerp = CreateYLerpGetter();

        public GravityFloatySpaceBlockHooks(GravityRuntimeContext runtime)
        {
            this.runtime = runtime;
            try
            {
                hooks.Add(new ILHook(typeof(FloatySpaceBlockState).GetMethod(nameof(FloatySpaceBlockState.Update)),
                    Update));
                hooks.Add(new ILHook(
                    typeof(FloatySpaceBlockState).GetMethod("GetMoveTargetY",
                        BindingFlags.NonPublic | BindingFlags.Instance), MoveToTarget));
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        private bool Owns(SceneEntityState self) => self.Scene is ISourceEngineContextScene scene &&
                                                    ReferenceEquals(scene.EngineContext, runtime.Engine);

        private void Update(ILContext il)
        {
            var cursor = new ILCursor(il);
            if (!cursor.TryGotoNext(MoveType.After, i => i.MatchLdfld<FloatySpaceBlockState>("yLerp"),
                    i => i.MatchLdcR4(1f)))
                throw new InvalidOperationException("FloatySpaceBlock active sink target missing.");
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.EmitDelegate<Func<float, FloatySpaceBlockState, float>>((value, self) =>
                Owns(self) && (runtime.PlayerComponent?.ShouldInvert ?? false) ? -value : value);
        }

        private void MoveToTarget(ILContext il)
        {
            var cursor = new ILCursor(il);
            if (!cursor.TryGotoNext(i =>
                    i.MatchCallvirt<IFloatySpaceArithmetic>(nameof(IFloatySpaceArithmetic.GetTargetY))))
                throw new InvalidOperationException("FloatySpaceBlock arithmetic target missing.");
            cursor.Remove();
            cursor.Emit(OpCodes.Ldarg_0);
            cursor
                .EmitDelegate<Func<IFloatySpaceArithmetic, float, float, float, float, FloatySpaceBlockState, float>>((
                    arithmetic, origin, eased, sine, dash, self) =>
                {
                    if (!Owns(self)) return arithmetic.GetTargetY(origin, eased, sine, dash);
                    if (arithmetic is not IFloatySpaceSinkDistanceArithmetic directional)
                        throw new InvalidOperationException(
                            "Gravity floaty blocks require directional sink-distance arithmetic.");
                    return directional.GetTargetY(origin, eased, sine, dash, 12f * Math.Sign(GetYLerp(self)));
                });
        }

        private static Func<FloatySpaceBlockState, float> CreateYLerpGetter()
        {
            var method = new System.Reflection.Emit.DynamicMethod("GetFloatySinkLerp", typeof(float),
                [typeof(FloatySpaceBlockState)], typeof(GravityFloatySpaceBlockHooks).Module, true);
            var il = method.GetILGenerator();
            il.Emit(System.Reflection.Emit.OpCodes.Ldarg_0);
            il.Emit(System.Reflection.Emit.OpCodes.Ldfld,
                typeof(FloatySpaceBlockState).GetField("yLerp", BindingFlags.Instance | BindingFlags.NonPublic));
            il.Emit(System.Reflection.Emit.OpCodes.Ret);
            return (Func<FloatySpaceBlockState, float>)method.CreateDelegate(
                typeof(Func<FloatySpaceBlockState, float>));
        }

        public void Dispose()
        {
            for (int i = hooks.Count - 1; i >= 0; i--) hooks[i].Dispose();
            hooks.Clear();
        }
    }
}
