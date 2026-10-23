// Copyright (c) Shane Woolcock. Licensed under the MIT Licence.

using System;
using System.Numerics;
using Celeste.Scene.Entities;
using Celeste.Physics.Collision;
using Celeste.Game.Execution.LevelLoader.Terrain;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;

namespace Celeste.Mods.GravityHelper.Hooks.Platforms
{
    public sealed partial class GravityPlatformHooks
    {
        private void TilesSound(ILContext il)
        {
            var cursor = new ILCursor(il);
            int anchors = 0;
            while (cursor.TryGotoNext(MoveType.After,
                       instruction => instruction.Operand is MethodReference
                       {
                           Name: "get_BottomCenter" or "get_BottomLeft" or "get_BottomRight"
                       }))
            {
                cursor.Emit(OpCodes.Ldarg_0);
                cursor.Emit(OpCodes.Ldarg_1);
                cursor.EmitDelegate<Func<Vector2, OriginalSolidTiles, ISourceEntityCollider, Vector2>>((anchor, self,
                        entity) =>
                    Inverted(self) ? new Vector2(anchor.X, ((SceneEntityState)entity).Top) : anchor);
                anchors++;
            }

            cursor.Index = 0;
            int offsets = 0;
            while (cursor.TryGotoNext(MoveType.After, instruction => instruction.MatchLdcR4(4f)))
            {
                cursor.Emit(OpCodes.Ldarg_0);
                cursor.EmitDelegate<Func<float, OriginalSolidTiles, float>>((offset, self) =>
                    Inverted(self) ? -offset : offset);
                offsets++;
            }

            if (anchors != 3 || offsets != 3)
                throw new InvalidOperationException(
                    $"Gravity SolidTiles: expected three anchors/offsets, found {anchors}/{offsets}.");
        }
    }
}
