// Copyright (c) Shane Woolcock. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Reflection;
using Celeste.Core.Gameplay.FlingBirds;
using Celeste.Core.Geometry;
using Celeste.Mods.GravityHelper.Components;
using Celeste.Mods.GravityHelper.Gravity;
using Celeste.Mods.GravityHelper.Runtime;
using Celeste.Scene.Entities;
using Celeste.Simulation;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;

namespace Celeste.Mods.GravityHelper.Hooks
{
    public sealed class GravityPlayerMiscHooks : IDisposable
    {
        private readonly GravityRuntimeContext runtime;
        private readonly List<IDisposable> hooks = [];

        public GravityPlayerMiscHooks(GravityRuntimeContext runtime)
        {
            this.runtime = runtime;
            try
            {
                var player = typeof(RoomPlayerSession);
                hooks.Add(new ILHook(player.GetMethod(nameof(RoomPlayerSession.SideBounce)), AdjustSideBounce));
                hooks.Add(new ILHook(
                    player.GetMethod("UpdateSummitLaunch", BindingFlags.NonPublic | BindingFlags.Instance),
                    AdjustSummitLaunch));
                hooks.Add(new Hook(player.GetMethod(nameof(RoomPlayerSession.DoFlingBird)), (FlingHook)DoFlingBird));
                hooks.Add(new ILHook(player.GetMethod(nameof(RoomPlayerSession.IsOverWater)), AdjustOverWater));
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        private bool Inverted(RoomPlayerSession player) => ReferenceEquals(player.EngineContext, runtime.Engine) &&
                                                           (runtime.PlayerComponent?.ShouldInvert ?? false);

        private void AdjustSideBounce(ILContext il)
        {
            var cursor = new ILCursor(il);
            cursor.GotoNext(MoveType.After,
                instruction => instruction.MatchCallOrCallvirt(typeof(SceneEntityState), "get_Bottom"));
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.EmitDelegate<Func<float, RoomPlayerSession, float>>((value, player) =>
                Inverted(player) ? player.Top : value);
            cursor.GotoNext(MoveType.Before,
                instruction => instruction.MatchLdnull() && instruction.Next.MatchLdnull());
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.EmitDelegate<Func<float, RoomPlayerSession, float>>((value, player) =>
                Inverted(player) ? -value : value);
        }

        private void AdjustSummitLaunch(ILContext il)
        {
            var cursor = new ILCursor(il);
            cursor.GotoNext(MoveType.After, instruction => instruction.MatchLdcR4(40f));
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.EmitDelegate<Func<float, RoomPlayerSession, float>>((value, player) =>
                Inverted(player) ? -28f : value);
        }

        private bool DoFlingBird(FlingOriginal original, RoomPlayerSession player, ISourceFlingBird bird)
        {
            if (Inverted(player)) player.Get<GravityComponent>()?.SetGravity(GravityType.Normal, 0f);
            return original(player, bird);
        }

        private void AdjustOverWater(ILContext il)
        {
            var cursor = new ILCursor(il);
            cursor.GotoNext(MoveType.After, instruction => instruction.MatchLdloc(0));
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.EmitDelegate<Func<IntRect, RoomPlayerSession, IntRect>>((bounds, player) =>
            {
                if (Inverted(player)) bounds.Y -= 2;
                return bounds;
            });
        }

        public void Dispose()
        {
            for (int index = hooks.Count - 1; index >= 0; index--) hooks[index].Dispose();
            hooks.Clear();
        }

        private delegate bool FlingOriginal(RoomPlayerSession player, ISourceFlingBird bird);

        private delegate bool FlingHook(FlingOriginal original, RoomPlayerSession player, ISourceFlingBird bird);
    }
}
