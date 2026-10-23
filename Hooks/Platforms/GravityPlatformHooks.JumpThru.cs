// Copyright (c) Shane Woolcock. Licensed under the MIT Licence.

using System;
using Celeste.Physics.Collision.Riding;
using Celeste.Scene.Entities;
using Celeste.Simulation.Gameplay.Platforms.Foundation;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;

namespace Celeste.Mods.GravityHelper.Hooks.Platforms
{
    public sealed partial class GravityPlatformHooks
    {
        private delegate void JumpMoveOrig(SourceJumpThruEntityState self, int move);

        private delegate void JumpMoveHook(JumpMoveOrig orig, SourceJumpThruEntityState self, int move);

        private void JumpMove(JumpMoveOrig orig, SourceJumpThruEntityState self, int move)
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

        private void JumpMovement(ILContext il)
        {
            var cursor = new ILCursor(il);
            ILLabel unused = null;
            if (!cursor.TryGotoNext(MoveType.After, instruction => instruction.MatchLdarg(1),
                    instruction => instruction.MatchLdcI4(0), instruction => instruction.MatchBge(out unused)))
                throw new InvalidOperationException("Gravity JumpThru: movement sign branch missing.");
            cursor.Index -= 2;
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.EmitDelegate<Func<int, SourceJumpThruEntityState, int>>((move, self) =>
                Own(self) && runtime.JumpThruContacts.IsUpsideDown(self) ? -move : move);
            if (!cursor.TryGotoNext(MoveType.After,
                    instruction => instruction.Operand is MethodReference { Name: "get_Top" }) ||
                !cursor.TryGotoNext(MoveType.After, instruction => instruction.OpCode == OpCodes.Conv_I4))
                throw new InvalidOperationException("Gravity JumpThru: push displacement missing.");
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.Emit(OpCodes.Ldarg_1);
            cursor.Emit(OpCodes.Ldloc_1);
            cursor.EmitDelegate<Func<int, SourceJumpThruEntityState, int, ISourcePlatformActor, int>>((normal, self,
                    move,
                    actor) =>
                Own(self) && runtime.JumpThruContacts.IsUpsideDown(self)
                    ? (int)(self.Bottom - ((SceneEntityState)actor).Top + move)
                    : normal);
        }
    }
}
