// Copyright (c) Shane Woolcock. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Reflection;
using Celeste.Core.Mathematics;
using Celeste.Mods.GravityHelper.Runtime;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Player.Scene.Particles;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;

namespace Celeste.Mods.GravityHelper.Hooks
{
    public sealed class GravityPlayerFeatherHooks : IDisposable
    {
        private readonly GravityRuntimeContext runtime;
        private readonly List<IDisposable> hooks = new();

        public GravityPlayerFeatherHooks(GravityRuntimeContext runtime)
        {
            this.runtime = runtime;
            try
            {
                hooks.Add(new Hook(
                    typeof(RoomPlayerSession).GetMethod("BeginStarFly", BindingFlags.Instance | BindingFlags.NonPublic),
                    (BeginHook)Begin));
                hooks.Add(new ILHook(
                    typeof(RoomPlayerSession).GetMethod("UpdateStarFly",
                        BindingFlags.Instance | BindingFlags.NonPublic),
                    AdjustFlyingParticles));
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        private bool Inverted(RoomPlayerSession player)
            => ReferenceEquals(player.EngineContext, runtime.Engine) &&
               (runtime.PlayerComponent?.ShouldInvert ?? false);

        private void Begin(BeginOriginal original, RoomPlayerSession player)
        {
            original(player);
            if (Inverted(player) && player.StarFlyBloom is { } bloom)
                bloom.Y = Math.Abs(bloom.Y);
        }

        private void AdjustFlyingParticles(ILContext il)
        {
            var cursor = new ILCursor(il);
            cursor.GotoNext(MoveType.Before,
                instruction =>
                    instruction.MatchCallOrCallvirt<ISourcePlayerStarFlyScene>(nameof(ISourcePlayerStarFlyScene
                        .EmitStarFlyParticles)));
            cursor.GotoPrev(MoveType.Before,
                instruction => instruction.MatchCall(typeof(Vector2Geometry), nameof(Vector2Geometry.Angle)));
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.EmitDelegate<Func<Vector2, RoomPlayerSession, Vector2>>((direction, player)
                => Inverted(player) ? new Vector2(direction.X, -direction.Y) : direction);
        }

        public void Dispose()
        {
            for (int index = hooks.Count - 1; index >= 0; index--) hooks[index].Dispose();
            hooks.Clear();
        }

        private delegate void BeginOriginal(RoomPlayerSession player);

        private delegate void BeginHook(BeginOriginal original, RoomPlayerSession player);
    }
}
