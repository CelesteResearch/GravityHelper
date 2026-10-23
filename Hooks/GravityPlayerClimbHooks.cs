// Copyright (c) Shane Woolcock. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Celeste.Core.Geometry;
using Celeste.Mods.GravityHelper.Runtime;
using Celeste.Physics.Collision;
using Celeste.Physics.Collision.Vertical;
using Celeste.Scene.Tracking;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Collision.Blockers;
using Celeste.Simulation.Gameplay.Platforms.Foundation;
using Celeste.Simulation.Gameplay.Player.Collision.Contacts;
using Celeste.Simulation.Gameplay.Player.Movement;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;

namespace Celeste.Mods.GravityHelper.Hooks
{
    public sealed class GravityPlayerClimbHooks : IDisposable
    {
        private readonly GravityRuntimeContext runtime;
        private readonly Func<RoomPlayerSession, bool> handleInversionBlocks;
        private readonly List<IDisposable> hooks = [];

        public GravityPlayerClimbHooks(GravityRuntimeContext runtime,
            Func<RoomPlayerSession, bool> handleInversionBlocks)
        {
            this.runtime = runtime;
            this.handleInversionBlocks =
                handleInversionBlocks ?? throw new ArgumentNullException(nameof(handleInversionBlocks));
            try
            {
                hooks.Add(new Hook(typeof(PlayerContacts).GetMethod(nameof(PlayerContacts.JumpThruBoostBlocked)),
                    (Func<Func<PlayerContacts, bool>, PlayerContacts, bool>)JumpThruBoostBlocked));
                hooks.Add(new ILHook(typeof(PlayerContacts).GetMethod(nameof(PlayerContacts.ClimbHopBlocked)),
                    AdjustHopBlocked));
                hooks.Add(new ILHook(typeof(PlayerContacts).GetMethod(nameof(PlayerContacts.SlipCheck)), AdjustSlip));
                hooks.Add(new ILHook(typeof(ClimbSequence).GetMethod(nameof(ClimbSequence.Update)), AdjustClimb));
                hooks.Add(new ILHook(typeof(RoomPlayerSession).GetMethod(nameof(RoomPlayerSession.ClimbJump)),
                    AdjustClimbJumpDust));
                hooks.Add(new Hook(typeof(RoomPlayerSession).GetMethod(nameof(RoomPlayerSession.ClimbJump)),
                    (Action<Action<RoomPlayerSession>, RoomPlayerSession>)ClimbJump));
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        private bool Inverted(RoomPlayerSession player) => player != null &&
                                                           ReferenceEquals(player.EngineContext, runtime.Engine) &&
                                                           (runtime.PlayerComponent?.ShouldInvert ?? false);

        private bool Inverted(PlayerContacts contacts) =>
            Inverted(contacts.Body.SourceColliderOwner as RoomPlayerSession);

        private bool Inverted(ClimbSequence sequence) => ReferenceEquals(sequence.EngineContext, runtime.Engine) &&
                                                         (runtime.PlayerComponent?.ShouldInvert ?? false);

        private bool JumpThruBoostBlocked(Func<PlayerContacts, bool> original, PlayerContacts contacts)
        {
            if (!Inverted(contacts)) return original(contacts);
            var player = (RoomPlayerSession)contacts.Body.SourceColliderOwner;
            var scene = (ISourceScene)player.Scene;
            foreach (LedgeBlockerState blocker in scene.SourceScene.Components.GetTrackedComponents(
                         "Celeste.LedgeBlocker"))
                if (blocker.Blocking && SourceEntityCollision.CheckAt(player,
                        (ISourceEntityCollider)blocker.Entity, player.Position + Vector2.UnitY * 2f) &&
                    (blocker.BlockChecker == null || blocker.BlockChecker(player)))
                    return true;
            return false;
        }

        private void AdjustHopBlocked(ILContext il)
        {
            var cursor = new ILCursor(il);
            cursor.GotoNext(MoveType.After, instruction => instruction.MatchLdcR4(6));
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.EmitDelegate<Func<float, PlayerContacts, float>>((offset, contacts) =>
                Inverted(contacts) ? -offset : offset);
        }

        private void AdjustSlip(ILContext il)
        {
            var cursor = new ILCursor(il);
            int boundsIndex = -1, pointIndex = -1;
            cursor.GotoNext(MoveType.Before,
                instruction => instruction.MatchCall<PlayerContacts>(nameof(PlayerContacts.BoundsAt)),
                instruction => instruction.MatchStloc(out boundsIndex),
                instruction => instruction.MatchLdloca(out pointIndex));
            var bounds = il.Body.Variables[boundsIndex];
            var pointLocal = il.Body.Variables[pointIndex];
            cursor.GotoNext(MoveType.After, instruction => instruction.MatchCall<Vector2>(".ctor"));
            cursor.Emit(OpCodes.Ldloc, pointLocal);
            cursor.Emit(OpCodes.Ldloc, bounds);
            cursor.Emit(OpCodes.Ldarg_1);
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.EmitDelegate<Func<Vector2, Bounds, float, PlayerContacts, Vector2>>((point, box, addY, contacts) =>
                Inverted(contacts) ? new Vector2(point.X, box.Bottom - (5f + addY)) : point);
            cursor.Emit(OpCodes.Stloc, pointLocal);
            cursor.GotoNext(MoveType.After, instruction => instruction.MatchLdcR4(-4));
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.EmitDelegate<Func<float, PlayerContacts, float>>((offset, contacts) =>
                Inverted(contacts) ? offset - 1 : offset);
            cursor.GotoNext(MoveType.After, instruction => instruction.MatchCall<Vector2>("op_Multiply"));
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.EmitDelegate<Func<Vector2, PlayerContacts, Vector2>>((offset, contacts) =>
                Inverted(contacts) ? new Vector2(offset.X, -offset.Y) : offset);
        }

        private void AdjustClimb(ILContext il)
        {
            var cursor = new ILCursor(il);
            cursor.GotoNext(MoveType.After, instruction => instruction.MatchCall<Vector2>("get_UnitY"));
            cursor.GotoNext(MoveType.After, instruction => instruction.MatchCall<Vector2>("op_UnaryNegation"));
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.EmitDelegate<Func<Vector2, ClimbSequence, Vector2>>((offset, sequence) =>
                Inverted(sequence) ? new Vector2(offset.X, -offset.Y) : offset);
            cursor.GotoNext(MoveType.After, instruction => instruction.MatchLdcR4(1f),
                instruction => instruction.OpCode == OpCodes.Newobj &&
                               instruction.Operand is Mono.Cecil.MethodReference method &&
                               method.DeclaringType.FullName == typeof(Vector2).FullName);
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.EmitDelegate<Func<Vector2, ClimbSequence, Vector2>>((offset, sequence) =>
                Inverted(sequence) ? new Vector2(offset.X, -offset.Y) : offset);
            cursor.Index = il.Body.Instructions.Count;
            cursor.GotoPrev(MoveType.Before, instruction => instruction.MatchLdfld<
                Celeste.Simulation.Gameplay.Player.State.PlayerState>("ClimbNoMoveTimer"));
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.EmitDelegate<Action<ClimbSequence>>(UpdateClimbMove);
        }

        private void UpdateClimbMove(ClimbSequence sequence)
        {
            if (!ReferenceEquals(sequence.EngineContext, runtime.Engine) ||
                sequence.World is not RoomPlayerWorld world || world.SourcePlayer.Input.MoveY.Value != -1) return;
            var player = world.SourcePlayer;
            bool inverted = Inverted(sequence);
            if (runtime.JumpThruContacts.First(player,
                    player.Position + (inverted ? Vector2.UnitY : -Vector2.UnitY), !inverted, true) == null) return;
            sequence.Data.Motion.Normal.Speed.Y = 0f;
            sequence.Data.LastClimbMove = 0;
        }

        private void AdjustClimbJumpDust(ILContext il)
        {
            var cursor = new ILCursor(il);
            foreach (string path in new[]
                         { "event:/char/madeline/jump_climb_right", "event:/char/madeline/jump_climb_left" })
            {
                cursor.GotoNext(MoveType.After, instruction => instruction.MatchLdstr(path));
                cursor.GotoNext(MoveType.Before, instruction => instruction.MatchLdcI4(4));
                cursor.Emit(OpCodes.Ldarg_0);
                cursor.EmitDelegate<Func<float, RoomPlayerSession, float>>((angle, player) =>
                    Inverted(player) ? -angle : angle);
            }
        }

        private void ClimbJump(Action<RoomPlayerSession> original, RoomPlayerSession player)
        {
            if (!ReferenceEquals(player.EngineContext, runtime.Engine))
            {
                original(player);
                return;
            }

            int facing = player.Data.Facing;
            bool handled = handleInversionBlocks(player);
            if (handled && facing == player.Data.Facing) player.WallJump(player.Data.Facing);
            else original(player);
        }

        public void Dispose()
        {
            for (int i = hooks.Count - 1; i >= 0; i--) hooks[i].Dispose();
            hooks.Clear();
        }
    }
}
