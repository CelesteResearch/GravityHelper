// Copyright (c) Shane Woolcock. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Reflection;
using System.Linq;
using Celeste.Everest.Reflection.Runtime;
using Celeste.Game.Entities.Interaction.Boosters;
using Celeste.Mods.GravityHelper.Runtime;
using Celeste.Scene.Entities;
using Celeste.Scene.Tracking;
using Celeste.Core.Scheduling;
using Celeste.Simulation;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using MonoMod.Utils;

namespace Celeste.Mods.GravityHelper.Hooks
{
    public sealed class GravityBoosterHooks : IDisposable
    {
        private readonly GravityRuntimeContext runtime;
        private readonly List<IDisposable> hooks = [];
        private readonly FieldInfo routineOwner;

        public GravityBoosterHooks(GravityRuntimeContext runtime)
        {
            this.runtime = runtime;
            routineOwner = typeof(OriginalBoosterState)
                .GetMethod("BoostRoutine", BindingFlags.Instance | BindingFlags.NonPublic).GetStateMachineTarget()
                .DeclaringType.GetField("<>4__this",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            try
            {
                hooks.Add(new Hook(typeof(OriginalBoosterState).GetMethod(nameof(OriginalBoosterState.PlayerBoosted)),
                    (Action<Action<OriginalBoosterState, RoomPlayerSession, Vector2>, OriginalBoosterState,
                        RoomPlayerSession, Vector2>)PlayerBoosted));
                hooks.Add(new ILHook(typeof(OriginalBoosterState).GetMethod(nameof(OriginalBoosterState.Update)),
                    AdjustUpdate));
                hooks.Add(new ILHook(
                    typeof(OriginalBoosterState)
                        .GetMethod("BoostRoutine", BindingFlags.Instance | BindingFlags.NonPublic)
                        .GetStateMachineTarget(), AdjustRoutine));
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        private bool Owns(SceneEntityState entity) => entity.Scene is ISourceEngineContextScene scene &&
                                                      ReferenceEquals(scene.EngineContext, runtime.Engine);

        private void PlayerBoosted(Action<OriginalBoosterState, RoomPlayerSession, Vector2> original,
            OriginalBoosterState booster, RoomPlayerSession player, Vector2 direction)
        {
            original(booster, player, direction);
            if (!Owns(booster)) return;
            var customType =
                ((ISourceRuntimeTypeDiscoveryEnvironment)runtime.Engine).RuntimeTypeDiscovery.ResolveRuntimeTypeAlias(
                    "Celeste.Mod.GravityHelper.Entities.GravityBooster");
            if (customType?.IsInstanceOfType(booster) == true)
                runtime.PlayerComponent?.SetGravity(((IGravityBoosterGravity)booster).GravityType);
        }

        private Vector2 Transform(Vector2 value, OriginalBoosterState booster) =>
            Owns(booster) && (runtime.PlayerComponent?.ShouldInvert ?? false) ? new Vector2(value.X, -value.Y) : value;

        private void AdjustUpdate(ILContext il)
        {
            var cursor = new ILCursor(il);
            cursor.GotoNext(MoveType.After,
                i => i.MatchLdsfld<OriginalBoosterState>(nameof(OriginalBoosterState.playerOffset)));
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.EmitDelegate<Func<Vector2, OriginalBoosterState, Vector2>>(Transform);
        }

        private void AdjustRoutine(ILContext il)
        {
            var cursor = new ILCursor(il);
            cursor.GotoNext(MoveType.After,
                i => i.MatchLdsfld<OriginalBoosterState>(nameof(OriginalBoosterState.playerOffset)));
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.Emit(OpCodes.Ldfld, il.Import(routineOwner));
            cursor.EmitDelegate<Func<Vector2, OriginalBoosterState, Vector2>>(Transform);
        }

        public void Dispose()
        {
            for (int i = hooks.Count - 1; i >= 0; i--) hooks[i].Dispose();
            hooks.Clear();
        }
    }
}
