// Copyright (c) Shane Woolcock. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Celeste.Everest.Reflection.Runtime;
using Celeste.Game.Entities.BadelineBoosts;
using Celeste.Game.Entities.BadelineDummy;
using Celeste.Mods.GravityHelper.Gravity;
using Celeste.Mods.GravityHelper.Runtime;
using Celeste.Scene.Tracking;
using Celeste.Core.Scheduling;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using MonoMod.Utils;
using Celeste.Simulation;

namespace Celeste.Mods.GravityHelper.Hooks
{
    public sealed class GravityBadelineBoostHooks : IDisposable
    {
        private readonly GravityRuntimeContext runtime;
        private readonly List<IDisposable> hooks = [];
        private readonly FieldInfo routineOwner;

        public GravityBadelineBoostHooks(GravityRuntimeContext runtime)
        {
            this.runtime = runtime;
            routineOwner = typeof(BadelineBoostState)
                .GetMethod("BoostRoutine", BindingFlags.Instance | BindingFlags.NonPublic).GetStateMachineTarget()
                .DeclaringType.GetField("<>4__this",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            try
            {
                hooks.Add(new Hook(
                    typeof(BadelineBoostState).GetMethod("OnPlayer", BindingFlags.Instance | BindingFlags.NonPublic),
                    (Action<Action<BadelineBoostState, RoomPlayerSession>, BadelineBoostState, RoomPlayerSession>)
                    OnPlayer));
                hooks.Add(new ILHook(
                    typeof(BadelineBoostState).GetMethod("BoostRoutine", BindingFlags.Instance | BindingFlags.NonPublic)
                        .GetStateMachineTarget(), AdjustRoutine));
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        private bool Owns(BadelineBoostState booster) => booster.Scene is ISourceEngineContextScene scene &&
                                                         ReferenceEquals(scene.EngineContext, runtime.Engine);

        private void OnPlayer(Action<BadelineBoostState, RoomPlayerSession> original, BadelineBoostState booster,
            RoomPlayerSession player)
        {
            if (Owns(booster) && runtime.PlayerComponent is { } component)
            {
                var customType =
                    ((ISourceRuntimeTypeDiscoveryEnvironment)runtime.Engine).RuntimeTypeDiscovery
                    .ResolveRuntimeTypeAlias(
                        "Celeste.Mod.GravityHelper.Entities.GravityBadelineBoost");
                if (customType?.IsInstanceOfType(booster) == true)
                    component.SetGravity(((IGravityBadelineBoostGravity)(object)booster).CurrentDirection, 0f);
                else if (component.CurrentGravity != GravityType.Normal) component.SetGravity(GravityType.Normal, 0f);
            }

            original(booster, player);
        }

        private BadelineDummyPresentation SetInversion(BadelineDummyPresentation dummy, BadelineBoostState booster)
        {
            if (Owns(booster))
                DynamicData.For(dummy).Set("GravityHelper_Inverted", runtime.PlayerComponent?.ShouldInvert ?? false);
            return dummy;
        }

        private void AdjustRoutine(ILContext il)
        {
            var cursor = new ILCursor(il);
            cursor.GotoNext(MoveType.Before,
                i => i.MatchLdfld<BadelineDummyPresentation>(nameof(BadelineDummyPresentation.Sprite)),
                i => i.OpCode == OpCodes.Ldflda && i.Operand is FieldReference field && field.Name == "Scale");
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.Emit(OpCodes.Ldfld, il.Import(routineOwner));
            cursor.EmitDelegate<Func<BadelineDummyPresentation, BadelineBoostState, BadelineDummyPresentation>>(
                SetInversion);
        }

        public void Dispose()
        {
            for (int i = hooks.Count - 1; i >= 0; i--) hooks[i].Dispose();
            hooks.Clear();
        }
    }
}
