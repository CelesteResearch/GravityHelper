// Copyright (c) Shane Woolcock. Licensed under the MIT Licence.

using System;
using System.Numerics;
using Celeste.Simulation.Gameplay.Platforms.Foundation;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;

namespace Celeste.Mods.GravityHelper.Hooks.Platforms
{
    public sealed partial class GravityPlatformHooks
    {
        private delegate void SolidMoveOrig(SourceSolidEntityState self, int move);

        private delegate void SolidMoveHook(SolidMoveOrig orig, SourceSolidEntityState self, int move);

        private void SolidMove(SolidMoveOrig orig, SourceSolidEntityState self, int move)
        {
            if (!Own(self))
            {
                orig(self, move);
                return;
            }

            runtime.Overrides.Semaphore++;
            orig(self, move);
            runtime.Overrides.Semaphore--;
        }

        private void SolidPlayerOnTop(ILContext il)
        {
            var cursor = new ILCursor(il);
            if (!cursor.TryGotoNext(MoveType.Before,
                    instruction => instruction.Operand is MethodReference
                    {
                        Name: "op_Subtraction", DeclaringType.FullName: "System.Numerics.Vector2"
                    }))
                throw new InvalidOperationException("Gravity Solid: position subtraction missing.");
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.EmitDelegate<Func<Vector2, SourceSolidEntityState, Vector2>>((offset, self) =>
                Inverted(self) ? -offset : offset);
        }
    }
}
