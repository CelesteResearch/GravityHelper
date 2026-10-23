// Copyright (c) Shane Woolcock. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Numerics;
using Celeste.Mods.GravityHelper.Components;
using Celeste.Mods.GravityHelper.Runtime;
using Celeste.Scene.Entities;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Holdables;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;

namespace Celeste.Mods.GravityHelper.Hooks
{
    public sealed class GravityHoldableHooks : IDisposable
    {
        private readonly GravityRuntimeContext runtime;
        private readonly List<IDisposable> hooks = [];

        public GravityHoldableHooks(GravityRuntimeContext runtime)
        {
            this.runtime = runtime;
            try
            {
                hooks.Add(new Hook(typeof(HoldableState).GetMethod(nameof(HoldableState.Added)),
                    (Action<Action<HoldableState, object>, HoldableState, object>)Added));
                hooks.Add(new Hook(typeof(HoldableState).GetMethod(nameof(HoldableState.Pickup)),
                    (Func<Func<HoldableState, RoomPlayerSession, bool>, HoldableState, RoomPlayerSession, bool>)
                    Pickup));
                hooks.Add(new ILHook(typeof(HoldableState).GetMethod(nameof(HoldableState.Release)), Release));
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        private bool Owns(HoldableState holdable) => ReferenceEquals(holdable.EngineContext, runtime.Engine);

        private void Added(Action<HoldableState, object> original, HoldableState holdable, object entity)
        {
            original(holdable, entity);
            if (Owns(holdable) && entity is SceneEntityState owner && owner.Get<GravityHoldable>() == null)
                owner.Add(new GravityHoldable(runtime));
        }

        private bool Pickup(Func<HoldableState, RoomPlayerSession, bool> original, HoldableState holdable,
            RoomPlayerSession player)
        {
            bool result = original(holdable, player);
            if (result && Owns(holdable)) ((SceneEntityState)holdable.Entity).Get<GravityHoldable>()?.SetGravityHeld();
            return result;
        }

        private void Release(ILContext il)
        {
            var cursor = new ILCursor(il);
            if (!cursor.TryGotoNext(MoveType.After, i => i.MatchCall<Vector2>("get_UnitY")))
                throw new InvalidOperationException("Holdable.Release original UnitY was not found.");
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.EmitDelegate<Func<Vector2, HoldableState, Vector2>>((value, holdable) =>
                Owns(holdable) && ((SceneEntityState)holdable.Entity)?.Get<GravityComponent>()?.ShouldInvertChecked ==
                true
                    ? new Vector2(value.X, -value.Y)
                    : value);
        }

        public void Dispose()
        {
            for (int i = hooks.Count - 1; i >= 0; i--) hooks[i].Dispose();
            hooks.Clear();
        }
    }
}
