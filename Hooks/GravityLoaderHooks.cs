// Copyright (c) Shane Woolcock. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Reflection;
using Celeste.Core.Scheduling;
using Celeste.Game.Content.Maps;
using Celeste.Game.Execution.LevelLoader;
using Celeste.Game.Menus.Overworld;
using Celeste.Mods.GravityHelper.Activation;
using Celeste.Mods.GravityHelper.Runtime;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;

namespace Celeste.Mods.GravityHelper.Hooks
{
    public sealed class GravityLoaderHooks : IDisposable
    {
        private readonly GravityRuntimeContext runtime;
        private readonly Action<GravityHookLevel> updateHooks;
        private readonly List<IDisposable> hooks = [];

        public GravityLoaderHooks(GravityRuntimeContext runtime, Action<GravityHookLevel> updateHooks)
        {
            this.runtime = runtime;
            this.updateHooks = updateHooks ?? throw new ArgumentNullException(nameof(updateHooks));
            foreach (var constructor in typeof(OriginalLevelLoaderScene).GetConstructors())
                if (constructor.GetParameters().Length == 8)
                    hooks.Add(new ILHook(constructor, AdjustLevelLoader));
            hooks.Add(new ILHook(typeof(OverworldLoaderSequence).GetConstructors()[0], AdjustOverworldLoader));
        }

        private void AdjustLevelLoader(ILContext il)
        {
            var cursor = new ILCursor(il);
            cursor.Emit(OpCodes.Ldarg_1);
            cursor.EmitDelegate<Action<ISourceEngineContext>>(engine =>
            {
                if (ReferenceEquals(engine, runtime.Engine)) runtime.ClearStatics();
            });
            cursor.Index = 0;
            while (cursor.TryGotoNext(MoveType.Before, instruction => instruction.OpCode == OpCodes.Ret))
            {
                cursor.Emit(OpCodes.Ldarg_0);
                cursor.EmitDelegate<Action<OriginalLevelLoaderScene>>(loader =>
                {
                    if (ReferenceEquals(loader.EngineContext, runtime.Engine))
                        updateHooks(GravityHookActivationPolicy.Select((OriginalChapterMapData)loader.Session.MapData,
                            runtime.Settings.AllowInAllMaps));
                });
                cursor.Index++;
            }
        }

        private void AdjustOverworldLoader(ILContext il)
        {
            var cursor = new ILCursor(il);
            while (cursor.TryGotoNext(MoveType.Before, instruction => instruction.OpCode == OpCodes.Ret))
            {
                cursor.Emit(OpCodes.Ldarg_0);
                cursor.EmitDelegate<Action<OverworldLoaderSequence>>(loader =>
                {
                    if (ReferenceEquals(loader.EngineContext, runtime.Engine) && (int)loader.StartMode != -1)
                        updateHooks(GravityHookLevel.None);
                });
                cursor.Index++;
            }
        }

        public void Dispose()
        {
            for (int index = hooks.Count - 1; index >= 0; index--) hooks[index].Dispose();
            hooks.Clear();
        }
    }
}
