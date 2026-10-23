// Copyright (c) Shane Woolcock. Licensed under the MIT Licence.

using System;
using Celeste.Core.Mathematics.LinearAlgebra;
using System.Numerics;
using Celeste.Simulation.Gameplay.Platforms.Foundation;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;

namespace Celeste.Mods.GravityHelper.Hooks.Platforms
{
    public sealed partial class GravityPlatformHooks
    {
        private void PlatformCollision(ILContext il)
        {
            var cursor = new ILCursor(il);
            ILLabel exit = null;
            if (!cursor.TryGotoNext(MoveType.After,
                    instruction => instruction.Operand is MethodReference { Name: "FirstAt" }) ||
                !cursor.TryGotoNext(MoveType.After, instruction => instruction.MatchStloc(3),
                    instruction => instruction.MatchLdloc(3), instruction => instruction.MatchBrtrue(out exit)))
                throw new InvalidOperationException("Gravity Platform: solid probe/loop exit missing.");
            int start = cursor.Index;
            ILLabel increment = null;
            if (!cursor.TryGotoNext(MoveType.Before, instruction => instruction.MatchLdarg(1),
                    instruction => instruction.MatchLdcI4(0), instruction => instruction.MatchBle(out increment)))
                throw new InvalidOperationException("Gravity Platform: positive jumpthru branch missing.");
            cursor.Index = start;
            var original = cursor.DefineLabel();
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.EmitDelegate<Func<SourcePlatformEntityState, bool>>(Own);
            cursor.Emit(OpCodes.Brfalse, original);
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.Emit(OpCodes.Ldarg_1);
            cursor.Emit(OpCodes.Ldloc_1);
            cursor.EmitDelegate<Func<SourcePlatformEntityState, int, int, SourcePlatformEntityState>>((self, move,
                    step) =>
                runtime.JumpThruContacts.First(self,
                    SourceVector2Math.Add(self.Position, SourceVector2Math.Multiply(Vector2.UnitY, step)), move < 0,
                    true));
            cursor.Emit(OpCodes.Stloc_3);
            cursor.Emit(OpCodes.Ldloc_3);
            cursor.Emit(OpCodes.Brtrue, exit);
            cursor.Emit(OpCodes.Br, increment);
            cursor.MarkLabel(original);
        }
    }
}
