// Copyright (c) Shane Woolcock. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using Celeste.Mods.GravityHelper.Components;
using Celeste.Mods.GravityHelper.Gravity;
using Celeste.Mods.GravityHelper.Runtime;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Player.Dash;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;

namespace Celeste.Mods.GravityHelper.Hooks
{
    public sealed class GravityPlayerLifecycleHooks : IDisposable
    {
        private readonly GravityRuntimeContext runtime;
        private readonly Func<RoomPlayerSession, (GravityType Gravity, bool FireOnBubbleReturn)?> firstSpawn;
        private readonly Func<RoomPlayerSession, bool> dashToToggle;
        private readonly Func<RoomPlayerSession, GravityType, bool> gravityForced;
        private readonly Func<RoomPlayerSession, bool> vvvvvvTriggerAtSpawn;
        private readonly Action<RoomPlayerSession> addIndicators;
        private readonly Action clearChaserStates;
        private readonly List<IDisposable> hooks = [];

        public GravityPlayerLifecycleHooks(GravityRuntimeContext runtime,
            Func<RoomPlayerSession, (GravityType Gravity, bool FireOnBubbleReturn)?> firstSpawn,
            Func<RoomPlayerSession, bool> dashToToggle, Func<RoomPlayerSession, GravityType, bool> gravityForced,
            Func<RoomPlayerSession, bool> vvvvvvTriggerAtSpawn, Action<RoomPlayerSession> addIndicators,
            Action clearChaserStates)
        {
            this.runtime = runtime;
            this.firstSpawn = firstSpawn;
            this.dashToToggle = dashToToggle;
            this.gravityForced = gravityForced;
            this.vvvvvvTriggerAtSpawn = vvvvvvTriggerAtSpawn;
            this.addIndicators = addIndicators;
            this.clearChaserStates = clearChaserStates;
            hooks.Add(new ILHook(typeof(RoomPlayerSession).GetConstructors().Single(), AppendPlayerComponents));
            hooks.Add(new Hook(typeof(RoomPlayerSession).GetMethod(nameof(RoomPlayerSession.Added)), (AddedHook)Added));
            hooks.Add(new Hook(
                typeof(RoomPlayerSession).GetMethod("CassetteFlyBegin", BindingFlags.Instance | BindingFlags.NonPublic),
                (PlayerActionHook)CassetteBegin));
            hooks.Add(new Hook(
                typeof(RoomPlayerSession).GetMethod("CassetteFlyEnd", BindingFlags.Instance | BindingFlags.NonPublic),
                (PlayerActionHook)CassetteEnd));
            hooks.Add(new ILHook(typeof(PlayerBoostSequence).GetMethod(nameof(PlayerBoostSequence.BoostUpdate)),
                BoostScope));
            hooks.Add(new ILHook(typeof(PlayerBoostSequence).GetMethod(nameof(PlayerBoostSequence.BoostEnd)),
                BoostScope));
        }

        private bool Owns(RoomPlayerSession player) => ReferenceEquals(player.EngineContext, runtime.Engine);

        private void AppendPlayerComponents(ILContext il)
        {
            var cursor = new ILCursor(il);
            while (cursor.TryGotoNext(MoveType.Before, instruction => instruction.OpCode == OpCodes.Ret))
            {
                cursor.Emit(OpCodes.Ldarg_0);
                cursor.EmitDelegate<Action<RoomPlayerSession>>(Constructed);
                cursor.Index++;
            }
        }

        private void Constructed(RoomPlayerSession player)
        {
            if (!Owns(player)) return;
            clearChaserStates();
            player.Add(new RoomTransitionListener
            {
                OnOutBegin = () =>
                    runtime.Session.InitialGravity = runtime.PlayerComponent?.CurrentGravity ?? GravityType.Normal
            });
            player.Add(new PlayerGravityComponent(runtime));
            player.Add(new DashListenerState { OnDash = _ => HandleDash(player) });
        }

        private void HandleDash(RoomPlayerSession player)
        {
            if (runtime.PlayerComponent is not PlayerGravityComponent component) return;
            if (component.GravityCharges <= 0 && !dashToToggle(player)) return;
            component.ConsumeGravityCharge();
            if (!gravityForced(player, component.CurrentGravity)) component.SetGravity(GravityType.Toggle);
        }

        private void Added(AddedOriginal original, RoomPlayerSession player, object scene)
        {
            original(player, scene);
            if (!Owns(player)) return;
            var spawn = firstSpawn(player);
            runtime.PlayerComponent?.SetGravity(runtime.GravityBeforeReload ??
                                                spawn?.Gravity ?? runtime.Session.InitialGravity);
            runtime.GravityBeforeReload = null;
            runtime.Session.VvvvvvTrigger = vvvvvvTriggerAtSpawn(player);
            addIndicators(player);
        }

        private void CassetteBegin(PlayerAction original, RoomPlayerSession player)
        {
            if (Owns(player))
            {
                runtime.PlayerComponent?.SetGravity(GravityType.Normal);
                runtime.PlayerComponent?.Lock();
            }

            original(player);
        }

        private void CassetteEnd(PlayerAction original, RoomPlayerSession player)
        {
            original(player);
            if (!Owns(player)) return;
            runtime.PlayerComponent?.Unlock();
            var spawn = firstSpawn(player);
            if (spawn is { FireOnBubbleReturn: true }) runtime.PlayerComponent?.SetGravity(spawn.Value.Gravity);
        }

        private void BoostScope(ILContext il)
        {
            var playerField =
                typeof(PlayerBoostSequence).GetField("player", BindingFlags.Instance | BindingFlags.NonPublic);
            var cursor = new ILCursor(il);
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.Emit(OpCodes.Ldfld, playerField);
            cursor.EmitDelegate<Action<RoomPlayerSession>>(player =>
            {
                if (Owns(player)) runtime.Overrides.Semaphore++;
            });
            while (cursor.TryGotoNext(MoveType.Before, instruction => instruction.OpCode == OpCodes.Ret))
            {
                cursor.Emit(OpCodes.Ldarg_0);
                cursor.Emit(OpCodes.Ldfld, playerField);
                cursor.EmitDelegate<Action<RoomPlayerSession>>(player =>
                {
                    if (Owns(player)) runtime.Overrides.Semaphore--;
                });
                cursor.Index++;
            }
        }

        public void Dispose()
        {
            for (int index = hooks.Count - 1; index >= 0; index--) hooks[index].Dispose();
            hooks.Clear();
        }

        private delegate void AddedOriginal(RoomPlayerSession player, object scene);

        private delegate void AddedHook(AddedOriginal original, RoomPlayerSession player, object scene);

        private delegate void PlayerAction(RoomPlayerSession player);

        private delegate void PlayerActionHook(PlayerAction original, RoomPlayerSession player);
    }
}
