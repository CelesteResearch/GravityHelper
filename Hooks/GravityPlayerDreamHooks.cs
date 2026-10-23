// Copyright (c) Shane Woolcock. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Reflection;
using Celeste.Mods.GravityHelper.Runtime;
using Celeste.Mods.GravityHelper.Contracts.DreamBlocks;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Platforms.DreamBlock;
using MonoMod.RuntimeDetour;

namespace Celeste.Mods.GravityHelper.Hooks
{
    public sealed class GravityPlayerDreamHooks : IDisposable
    {
        private readonly GravityRuntimeContext runtime;
        private readonly List<IDisposable> hooks = [];

        public GravityPlayerDreamHooks(GravityRuntimeContext runtime)
        {
            this.runtime = runtime;
            try
            {
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                Type player = typeof(RoomPlayerSession);
                hooks.Add(new Hook(player.GetMethod("DreamDashBegin", flags),
                    (Action<Action<RoomPlayerSession>, RoomPlayerSession>)Begin));
                hooks.Add(new Hook(player.GetMethod("DreamDashCheck", flags), (CheckHook)Check));
                hooks.Add(new Hook(player.GetMethod("DreamDashUpdate", flags),
                    (Func<Func<RoomPlayerSession, int>, RoomPlayerSession, int>)Update));
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        private bool Owns(RoomPlayerSession player) => ReferenceEquals(player.EngineContext, runtime.Engine);

        private bool Inverted(RoomPlayerSession player) =>
            Owns(player) && (runtime.PlayerComponent?.ShouldInvert ?? false);

        private static void FlipPlayer(RoomPlayerSession player)
        {
            player.Data.Motion.Normal.Speed.Y *= -1f;
            player.Data.Direction.Y *= -1f;
        }

        private void Begin(Action<RoomPlayerSession> original, RoomPlayerSession player)
        {
            original(player);
            if (Owns(player) && runtime.PlayerComponent != null && player.CurrentDreamBlock is IGravityDreamBlock block)
                block.PlayerEntered();
        }

        private bool Check(CheckOriginal original, RoomPlayerSession player, Vector2 direction)
        {
            if (!Inverted(player)) return original(player, direction);
            FlipPlayer(player);
            bool result = original(player, new Vector2(direction.X, -direction.Y));
            FlipPlayer(player);
            return result;
        }

        private int Update(Func<RoomPlayerSession, int> original, RoomPlayerSession player)
        {
            if (!Inverted(player)) return original(player);
            FlipPlayer(player);
            int result = original(player);
            if (!player.Data.DreamJump) player.Data.Motion.Normal.Speed.Y *= -1f;
            player.Data.Direction.Y *= -1f;
            return result;
        }

        public void Dispose()
        {
            for (int index = hooks.Count - 1; index >= 0; index--) hooks[index].Dispose();
            hooks.Clear();
        }

        private delegate bool CheckOriginal(RoomPlayerSession player, Vector2 direction);

        private delegate bool CheckHook(CheckOriginal original, RoomPlayerSession player, Vector2 direction);
    }
}
