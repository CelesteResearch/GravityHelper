// Copyright (c) Shane Woolcock. Licensed under the MIT Licence.

using System;
using Celeste.Core.Scheduling;
using Celeste.Game.Gameplay.Environment.Lava.Barriers;
using Celeste.Mods.GravityHelper.Components;
using Celeste.Mods.GravityHelper.Gravity;
using Celeste.Mods.GravityHelper.Runtime;
using MonoMod.RuntimeDetour;

namespace Celeste.Mods.GravityHelper.Hooks
{
    public sealed class GravityIceBlockHooks : IDisposable
    {
        private readonly GravityRuntimeContext runtime;
        private readonly Hook hook;

        public GravityIceBlockHooks(GravityRuntimeContext runtime)
        {
            this.runtime = runtime;
            hook = new Hook(typeof(OriginalIceBlockState).GetMethod(nameof(OriginalIceBlockState.Added)),
                (AddedHook)Added);
        }

        private void Added(AddedOriginal original, OriginalIceBlockState block, object scene)
        {
            original(block, scene);
            if (scene is not ISourceEngineContextScene actual ||
                !ReferenceEquals(actual.EngineContext, runtime.Engine)) return;
            var solid = block.Solid;
            var initialY = solid.Y;
            block.Add(new PlayerGravityListener(runtime, (_, args) =>
                solid.Y = args.NewValue == GravityType.Inverted ? initialY - 1 : initialY));
        }

        public void Dispose() => hook.Dispose();

        private delegate void AddedOriginal(OriginalIceBlockState block, object scene);

        private delegate void AddedHook(AddedOriginal original, OriginalIceBlockState block, object scene);
    }
}
