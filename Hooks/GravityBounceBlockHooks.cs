// Copyright (c) Shane Woolcock. Licensed under the MIT Licence.

using System;
using System.Numerics;
using Celeste.Mods.GravityHelper.Interop;
using Celeste.Mods.GravityHelper.Runtime;
using Celeste.Simulation.Gameplay.Platforms.BounceBlock;
using MonoMod.Cil;
using Mono.Cecil.Cil;
using MonoMod.RuntimeDetour;

namespace Celeste.Mods.GravityHelper.Hooks
{
    public sealed class GravityBounceBlockHooks : IDisposable
    {
        private readonly GravityRuntimeContext runtime;
        private readonly ILHook update, windUp;

        public GravityBounceBlockHooks(GravityRuntimeContext runtime)
        {
            this.runtime = runtime;
            update = new ILHook(typeof(BounceBlockState).GetMethod("Update"), Update);
            windUp = new ILHook(typeof(BounceBlockState).GetMethod("WindUpPlayerCheck"), WindUp);
        }

        private Vector2 Invert(Vector2 value, BounceBlockState self) =>
            ReferenceEquals(MovingBlockSourceLoans.World(self).EngineContext, runtime.Engine) &&
            runtime.PlayerComponent?.ShouldInvert == true
                ? new Vector2(value.X, -value.Y)
                : value;

        private void Emit(ILCursor cursor)
        {
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.EmitDelegate<Func<Vector2, BounceBlockState, Vector2>>(Invert);
        }

        private void Update(ILContext il)
        {
            var cursor = new ILCursor(il);
            for (int occurrence = 0; occurrence < 2; occurrence++)
            {
                if (!cursor.TryGotoNext(MoveType.After, i => i.MatchCall<Vector2>("get_UnitY"),
                        i => i.MatchCall<Vector2>("op_UnaryNegation")))
                    throw new InvalidOperationException("Bounce Update Ice direction branch not found: " + occurrence);
                Emit(cursor);
            }
        }

        private void WindUp(ILContext il)
        {
            var cursor = new ILCursor(il);
            if (!cursor.TryGotoNext(MoveType.Before, i => i.MatchCall<Vector2>("op_Subtraction")))
                throw new InvalidOperationException("Bounce WindUp first position subtraction not found");
            Emit(cursor);
        }

        public void Dispose()
        {
            windUp.Dispose();
            update.Dispose();
        }
    }
}
