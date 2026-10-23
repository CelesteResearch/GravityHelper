// Copyright (c) Shane Woolcock. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using Celeste.Mods.GravityHelper.Runtime;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Player.Collision.Contacts;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;

namespace Celeste.Mods.GravityHelper.Hooks
{
    public sealed class GravityPlayerSwimHooks : IDisposable
    {
        private readonly GravityRuntimeContext runtime;
        private readonly List<IDisposable> hooks = [];

        public GravityPlayerSwimHooks(GravityRuntimeContext runtime)
        {
            this.runtime = runtime;
            try
            {
                Install(nameof(PlayerContacts.SwimCheck), 8);
                Install(nameof(PlayerContacts.SwimUnderwaterCheck), 9);
                Install(nameof(PlayerContacts.SwimJumpCheck), 14);
                Install(nameof(PlayerContacts.SwimRiseCheck), 18);
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        private void Install(string method, float expectedOffset) => hooks.Add(new ILHook(
            typeof(PlayerContacts).GetMethod(method), il =>
            {
                var cursor = new ILCursor(il);
                cursor.GotoNext(MoveType.After, instruction => instruction.MatchLdcR4(expectedOffset));
                cursor.Emit(OpCodes.Ldarg_0);
                cursor.EmitDelegate<Func<float, PlayerContacts, float>>((offset, contacts) =>
                    contacts.Body.SourceColliderOwner is RoomPlayerSession player &&
                    ReferenceEquals(player.EngineContext, runtime.Engine) &&
                    (runtime.PlayerComponent?.ShouldInvert ?? false)
                        ? -offset
                        : offset);
            }));

        public void Dispose()
        {
            for (int i = hooks.Count - 1; i >= 0; i--) hooks[i].Dispose();
            hooks.Clear();
        }
    }
}
