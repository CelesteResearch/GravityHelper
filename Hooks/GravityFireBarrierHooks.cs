// Copyright (c) Shane Woolcock. Licensed under the MIT Licence.

using System;
using System.Reflection;
using System.Reflection.Emit;
using Celeste.Core.Scheduling;
using Celeste.Game.Gameplay.Environment.Lava.Barriers;
using Celeste.Mods.GravityHelper.Components;
using Celeste.Mods.GravityHelper.Gravity;
using Celeste.Mods.GravityHelper.Runtime;
using Celeste.Simulation.Gameplay.Platforms.Foundation;
using MonoMod.RuntimeDetour;

namespace Celeste.Mods.GravityHelper.Hooks
{
    public sealed class GravityFireBarrierHooks : IDisposable
    {
        private readonly GravityRuntimeContext runtime;
        private readonly Hook hook;
        private static readonly Func<OriginalFireBarrierState, SourceSolidEntityState> GetSolid = CreateSolidGetter();

        public GravityFireBarrierHooks(GravityRuntimeContext runtime)
        {
            this.runtime = runtime;
            hook = new Hook(typeof(OriginalFireBarrierState).GetMethod(nameof(OriginalFireBarrierState.Added)),
                (AddedHook)Added);
        }

        private void Added(AddedOriginal original, OriginalFireBarrierState barrier, object scene)
        {
            original(barrier, scene);
            if (scene is not ISourceEngineContextScene actual ||
                !ReferenceEquals(actual.EngineContext, runtime.Engine)) return;
            SourceSolidEntityState solid = GetSolid(barrier);
            float initialY = solid.Y;
            barrier.Add(new PlayerGravityListener(runtime, (_, args)
                => solid.Y = args.NewValue == GravityType.Inverted ? initialY - 1 : initialY));
        }

        private static Func<OriginalFireBarrierState, SourceSolidEntityState> CreateSolidGetter()
        {
            var getter = new DynamicMethod("GetFireBarrierSolid", typeof(SourceSolidEntityState),
                new[] { typeof(OriginalFireBarrierState) }, typeof(GravityFireBarrierHooks).Module, true);
            ILGenerator il = getter.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldfld,
                typeof(OriginalFireBarrierState).GetField("solid", BindingFlags.Instance | BindingFlags.NonPublic));
            il.Emit(OpCodes.Ret);
            return (Func<OriginalFireBarrierState, SourceSolidEntityState>)getter.CreateDelegate(
                typeof(Func<OriginalFireBarrierState, SourceSolidEntityState>));
        }

        public void Dispose() => hook.Dispose();

        private delegate void AddedOriginal(OriginalFireBarrierState barrier, object scene);

        private delegate void AddedHook(AddedOriginal original, OriginalFireBarrierState barrier, object scene);
    }
}
