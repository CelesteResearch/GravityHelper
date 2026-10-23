// Copyright (c) Shane Woolcock. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Reflection;
using Celeste.Mods.GravityHelper.Runtime;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Camera;
using Celeste.Simulation.Gameplay.Player.Movement.Wind;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;

namespace Celeste.Mods.GravityHelper.Hooks
{
    public sealed class GravityPlayerSpatialHooks : IDisposable
    {
        private readonly GravityRuntimeContext runtime;
        private readonly List<IDisposable> hooks = [];

        private static readonly FieldInfo WindWorld = typeof(PlayerWindMovement).GetField("world",
            BindingFlags.Instance | BindingFlags.NonPublic);

        public GravityPlayerSpatialHooks(GravityRuntimeContext runtime)
        {
            this.runtime = runtime;
            hooks.Add(new ILHook(typeof(RoomCamera).GetMethod(nameof(RoomCamera.Target)), AdjustCamera));
            hooks.Add(new ILHook(typeof(PlayerWindMovement).GetMethod(nameof(PlayerWindMovement.Move)), AdjustWind));
        }

        private bool Inverted(RoomPlayerSession player)
            => ReferenceEquals(player.EngineContext, runtime.Engine) &&
               (runtime.PlayerComponent?.ShouldInvert ?? false);

        private void AdjustCamera(ILContext il)
        {
            var cursor = new ILCursor(il);
            foreach (string field in new[]
                         { nameof(CameraMotionProfile.LookAheadScale), nameof(CameraMotionProfile.DirectionBias) })
            {
                cursor.Index = 0;
                cursor.GotoNext(instruction => instruction.MatchLdfld<CameraMotionProfile>(field));
                cursor.Index++;
                cursor.GotoNext(MoveType.After, instruction => instruction.MatchLdfld<CameraMotionProfile>(field));
                EmitCameraY(cursor);
            }

            cursor.Index = 0;
            cursor.GotoNext(MoveType.After,
                instruction => instruction.MatchLdfld<CameraMotionProfile>(nameof(CameraMotionProfile.RiseBias)));
            EmitCameraY(cursor);
        }

        private void EmitCameraY(ILCursor cursor)
        {
            cursor.Emit(OpCodes.Ldarg_1);
            cursor.EmitDelegate<Func<float, ISourceCameraPlayer, float>>((offset, player)
                => player is RoomPlayerSession actual && Inverted(actual) ? -offset : offset);
        }

        private void AdjustWind(ILContext il)
        {
            var cursor = new ILCursor(il);
            cursor.Emit(OpCodes.Ldarg_1);
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.Emit(OpCodes.Ldfld, WindWorld);
            cursor.EmitDelegate<Func<Vector2, RoomPlayerWorld, Vector2>>((move, world)
                => Inverted(world.SourcePlayer) ? new Vector2(move.X, -move.Y) : move);
            cursor.Emit(OpCodes.Starg, 1);
        }

        public void Dispose()
        {
            for (int index = hooks.Count - 1; index >= 0; index--) hooks[index].Dispose();
            hooks.Clear();
        }
    }
}
