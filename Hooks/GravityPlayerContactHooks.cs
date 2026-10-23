// Copyright (c) Shane Woolcock. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Reflection;
using Celeste.Mods.GravityHelper.Collision;
using Celeste.Mods.GravityHelper.Runtime;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Player.Collision.Contacts;
using Celeste.Simulation.Gameplay.Player.Movement;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;

namespace Celeste.Mods.GravityHelper.Hooks
{
    public sealed class GravityPlayerContactHooks : IDisposable
    {
        private readonly GravityRuntimeContext runtime;
        private readonly GravityJumpThruContactQueries queries;
        private readonly List<IDisposable> hooks = new();

        private static readonly FieldInfo MovementWorld =
            typeof(PlayerMovement).GetField("world", BindingFlags.Instance | BindingFlags.NonPublic);

        public GravityPlayerContactHooks(GravityRuntimeContext runtime, GravityJumpThruContactQueries queries)
        {
            this.runtime = runtime;
            this.queries = queries;
            try
            {
                hooks.Add(new Hook(typeof(PlayerContacts).GetMethod("StandingUnduckBlockedAt"),
                    (StandingHook)StandingBlocked));
                hooks.Add(new ILHook(typeof(PlayerMovement).GetMethod(nameof(PlayerMovement.Update)), AdjustContacts));
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        private bool Owns(RoomPlayerSession player) => ReferenceEquals(player.EngineContext, runtime.Engine);

        private bool Inverted(IPlayerMovementWorld world)
            => world is RoomPlayerWorld actual && Owns(actual.SourcePlayer) &&
               (runtime.PlayerComponent?.ShouldInvert ?? false);

        private bool StandingBlocked(StandingOriginal original, PlayerContacts contacts, Vector2 offset)
        {
            bool blocked = original(contacts, offset);
            if (blocked || contacts.Body.SourceColliderOwner is not RoomPlayerSession player || !Owns(player))
                return blocked;
            bool inverted = runtime.PlayerComponent?.ShouldInvert ?? false;
            var saved = contacts.Collider;
            contacts.SetCollider(contacts.DuckHitbox);
            bool duckCollides = queries.FirstCurrent(player, !inverted) != null;
            contacts.SetCollider(saved);
            return !duckCollides && queries.FirstCurrent(player, !inverted) != null;
        }

        private void AdjustContacts(ILContext il)
        {
            var cursor = new ILCursor(il);
            cursor.GotoNext(MoveType.Before,
                instruction =>
                    instruction.MatchCallOrCallvirt<IPlayerMovementWorld>(nameof(IPlayerMovementWorld.JumpThruAt)));
            cursor.Remove();
            cursor.EmitDelegate<Func<IPlayerMovementWorld, Vector2, bool>>((world, offset)
                => Inverted(world)
                    ? queries.FirstUpsideDown(((RoomPlayerWorld)world).SourcePlayer,
                        ((RoomPlayerWorld)world).SourcePlayer.Position + offset) != null
                    : world.JumpThruAt(offset));

            InvertCollisionOffset(cursor, nameof(IPlayerMovementWorld.SolidAt));
            InvertCollisionOffset(cursor, nameof(IPlayerMovementWorld.JumpThruOutsideAt));
            cursor.Index++;
            EmitWorld(cursor);
            cursor.EmitDelegate<Func<bool, IPlayerMovementWorld, bool>>((originalResult, world) =>
            {
                if (world is not RoomPlayerWorld actual || !Owns(actual.SourcePlayer)) return originalResult;
                bool inverted = runtime.PlayerComponent?.ShouldInvert ?? false;
                return queries.First(actual.SourcePlayer,
                    actual.SourcePlayer.Position + Vector2.UnitY * (inverted ? -3f : 3f), inverted, true) != null;
            });
            InvertCollisionOffset(cursor, nameof(IPlayerMovementWorld.DashCorrectBlocked));

            cursor.GotoNext(MoveType.After,
                instruction => instruction.MatchCallOrCallvirt<IPlayerMovementWorld>("get_PlayerCenterY"));
            EmitWorld(cursor);
            cursor.EmitDelegate<Func<float, IPlayerMovementWorld, float>>((value, world) =>
                Inverted(world) ? -value : value);
            cursor.GotoNext(MoveType.After, instruction => instruction.MatchLdfld<Vector2>(nameof(Vector2.Y)));
            EmitWorld(cursor);
            cursor.EmitDelegate<Func<float, IPlayerMovementWorld, float>>((value, world) =>
                Inverted(world) ? -value : value);
        }

        private void InvertCollisionOffset(ILCursor cursor, string method)
        {
            cursor.GotoNext(MoveType.Before,
                instruction => instruction.MatchCallOrCallvirt<IPlayerMovementWorld>(method));
            EmitWorld(cursor);
            cursor.EmitDelegate<Func<Vector2, IPlayerMovementWorld, Vector2>>((offset, world)
                => Inverted(world) ? new Vector2(offset.X, -offset.Y) : offset);
        }

        private static void EmitWorld(ILCursor cursor)
        {
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.Emit(OpCodes.Ldfld, MovementWorld);
        }

        public void Dispose()
        {
            for (int index = hooks.Count - 1; index >= 0; index--) hooks[index].Dispose();
            hooks.Clear();
        }

        private delegate bool StandingOriginal(PlayerContacts contacts, Vector2 offset);

        private delegate bool StandingHook(StandingOriginal original, PlayerContacts contacts, Vector2 offset);
    }
}
