// Copyright (c) Shane Woolcock. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Reflection;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Execution.Processes;
using Celeste.Game.Presentation.Particles;
using Celeste.Mods.GravityHelper.Runtime;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Player.Scene.Particles;
using Celeste.Simulation.Presentation.Particles.Definitions;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;

namespace Celeste.Mods.GravityHelper.Hooks
{
    public sealed class GravityPlayerJumpLaunchHooks : IDisposable
    {
        private readonly GravityRuntimeContext runtime;
        private readonly Func<RoomPlayerSession, bool> handleInversionBlocks;
        private readonly List<IDisposable> hooks = [];
        private ParticleTypePresentation invertedDust;
        private ParticleTypePresentation invertedSparkyDust;

        public GravityPlayerJumpLaunchHooks(GravityRuntimeContext runtime,
            Func<RoomPlayerSession, bool> handleInversionBlocks)
        {
            this.runtime = runtime;
            this.handleInversionBlocks =
                handleInversionBlocks ?? throw new ArgumentNullException(nameof(handleInversionBlocks));
            try
            {
                Type player = typeof(RoomPlayerSession);
                hooks.Add(new Hook(player.GetMethod(nameof(RoomPlayerSession.Jump)), (JumpHook)Jump));
                hooks.Add(new Hook(player.GetMethod(nameof(RoomPlayerSession.SuperJump)),
                    (Action<Action<RoomPlayerSession>, RoomPlayerSession>)SuperJump));
                hooks.Add(new ILHook(player.GetMethod(nameof(RoomPlayerSession.Jump)), AdjustJump));
                hooks.Add(new ILHook(player.GetMethod(nameof(RoomPlayerSession.SuperJump)), AdjustJump));
                hooks.Add(new ILHook(player.GetMethod(nameof(RoomPlayerSession.SuperWallJump)), AdjustWallDust));
                hooks.Add(new ILHook(player.GetMethod(nameof(RoomPlayerSession.Bounce)), AdjustBounce));
                hooks.Add(new ILHook(player.GetMethod(nameof(RoomPlayerSession.SuperBounce)), AdjustSuperBounce));
                hooks.Add(new ILHook(player.GetMethod(nameof(RoomPlayerSession.PointBounce)), AdjustPointBounce));
                hooks.Add(
                    new Hook(player.GetMethod(nameof(RoomPlayerSession.ExplodeLaunch)), (LaunchHook)ExplodeLaunch));
                hooks.Add(new ILHook(player.GetMethod(nameof(RoomPlayerSession.ExplodeLaunch)), AdjustLaunchSlash));
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                string prefix = typeof(ISourcePlayerJumpScene).FullName + ".";
                hooks.Add(new ILHook(
                    typeof(OriginalLevelSceneRoot).GetMethod(prefix + nameof(ISourcePlayerJumpScene.BurstPlayerDust),
                        flags), AdjustDustType));
                hooks.Add(new ILHook(
                    typeof(OriginalLevelSceneRoot).GetMethod(
                        prefix + nameof(ISourcePlayerJumpScene.BurstPlayerWallSlideDust), flags),
                    AdjustWallSlideDustType));
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

        private bool Inverted(OriginalLevelSceneRoot level) => ReferenceEquals(level.EngineContext, runtime.Engine) &&
                                                               (runtime.PlayerComponent?.ShouldInvert ?? false);

        private Vector2 Flip(Vector2 value, RoomPlayerSession player) =>
            Inverted(player) ? new Vector2(value.X, -value.Y) : value;

        private float Flip(float value, RoomPlayerSession player) => Inverted(player) ? -value : value;
        private int Flip(int value, RoomPlayerSession player) => Inverted(player) ? -value : value;

        private Vector2 JumpPoint(Vector2 original, RoomPlayerSession player) =>
            Inverted(player) ? player.TopCenter : original;

        private float BouncePoint(float original, RoomPlayerSession player) => Inverted(player) ? player.Top : original;

        private void Jump(JumpOriginal original, RoomPlayerSession player, bool particles, bool playSfx)
        {
            if (Owns(player) && player.Machine.State != 1) handleInversionBlocks(player);
            original(player, particles, playSfx);
        }

        private void SuperJump(Action<RoomPlayerSession> original, RoomPlayerSession player)
        {
            if (Owns(player)) handleInversionBlocks(player);
            original(player);
        }

        private Vector2 ExplodeLaunch(LaunchOriginal original, RoomPlayerSession player, Vector2 from, bool snapUp,
            bool sidesOnly)
        {
            if (!Inverted(player)) return original(player, from, snapUp, sidesOnly);
            var reflected = new Vector2(from.X, player.Center.Y + (player.Center.Y - from.Y));
            Vector2 result = original(player, reflected, snapUp, sidesOnly);
            return new Vector2(result.X, -result.Y);
        }

        private void AdjustJump(ILContext il)
        {
            var cursor = new ILCursor(il);
            cursor.GotoNext(MoveType.After, instruction => instruction.MatchCall<Vector2>("get_UnitY"));
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.EmitDelegate<Func<Vector2, RoomPlayerSession, Vector2>>(Flip);
            cursor.GotoNext(MoveType.After,
                instruction =>
                    instruction.MatchCallOrCallvirt(typeof(Celeste.Scene.Entities.SceneEntityState),
                        "get_BottomCenter"));
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.EmitDelegate<Func<Vector2, RoomPlayerSession, Vector2>>(JumpPoint);
            AdjustWallDust(il);
        }

        private void AdjustWallDust(ILContext il)
        {
            var cursor = new ILCursor(il);
            int found = 0;
            while (cursor.TryGotoNext(MoveType.Before,
                       instruction => instruction.MatchCallOrCallvirt(typeof(ISourcePlayerJumpScene),
                           nameof(ISourcePlayerJumpScene.BurstPlayerDust))))
            {
                cursor.Emit(OpCodes.Ldarg_0);
                cursor.EmitDelegate<Action<ISourcePlayerJumpScene, Vector2, float, int, int, RoomPlayerSession>>(
                    BurstDust);
                cursor.Remove();
                found++;
            }

            if (found == 0)
                throw new InvalidOperationException("Expected player jump dust call is absent: " + il.Method.FullName);
        }

        private void BurstDust(ISourcePlayerJumpScene scene, Vector2 position, float direction, int count,
            int surfaceIndex,
            RoomPlayerSession player)
            => scene.BurstPlayerDust(position, Flip(direction, player), count, surfaceIndex);

        private void AdjustBounce(ILContext il) => AdjustBounce(il, true);
        private void AdjustSuperBounce(ILContext il) => AdjustBounce(il, false);

        private void AdjustBounce(ILContext il, bool integer)
        {
            var cursor = new ILCursor(il);
            cursor.GotoNext(MoveType.After,
                instruction =>
                    instruction.MatchCallOrCallvirt(typeof(Celeste.Scene.Entities.SceneEntityState), "get_Bottom"));
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.EmitDelegate<Func<float, RoomPlayerSession, float>>(BouncePoint);
            cursor.GotoNext(MoveType.Before,
                instruction => instruction.MatchLdnull() && instruction.Next.MatchLdnull());
            cursor.Emit(OpCodes.Ldarg_0);
            if (integer) cursor.EmitDelegate<Func<int, RoomPlayerSession, int>>(Flip);
            else cursor.EmitDelegate<Func<float, RoomPlayerSession, float>>(Flip);
        }

        private void AdjustPointBounce(ILContext il)
        {
            var cursor = new ILCursor(il);
            cursor.GotoNext(MoveType.After, instruction => instruction.MatchCall<Vector2>("op_Subtraction"));
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.EmitDelegate<Func<Vector2, RoomPlayerSession, Vector2>>(Flip);
        }

        private void AdjustLaunchSlash(ILContext il)
        {
            var cursor = new ILCursor(il);
            cursor.GotoNext(MoveType.Before,
                instruction => instruction.MatchCallOrCallvirt(typeof(RoomPlayerSession), "CreateDashSlash"));
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.EmitDelegate<Func<float, RoomPlayerSession, float>>(Flip);
        }

        private ParticleTypePresentation DustType(ParticleTypePresentation original, OriginalLevelSceneRoot level)
        {
            if (!Inverted(level)) return original;
            var types = ((IOriginalGameplayProcess)runtime.Engine).ParticleTypes;
            if (ReferenceEquals(original, types.SparkyDust)) return invertedSparkyDust ??= CloneDust(types.SparkyDust);
            if (ReferenceEquals(original, types.Dust)) return invertedDust ??= CloneDust(types.Dust);
            return original;
        }

        private static ParticleTypePresentation CloneDust(ParticleTypePresentation original) => new(original)
            { Acceleration = new Vector2(0f, -4f), Direction = -(float)Math.PI / 2f };

        private void AdjustDustType(ILContext il)
        {
            var cursor = new ILCursor(il);
            int found = 0;
            while (cursor.TryGotoNext(MoveType.After,
                       instruction =>
                           instruction.MatchLdfld<OriginalParticleCatalog>(nameof(OriginalParticleCatalog.Dust)) ||
                           instruction.MatchLdfld<OriginalParticleCatalog>(nameof(OriginalParticleCatalog.SparkyDust))))
            {
                cursor.Emit(OpCodes.Ldarg_0);
                cursor.EmitDelegate<Func<ParticleTypePresentation, OriginalLevelSceneRoot, ParticleTypePresentation>>(
                    DustType);
                found++;
            }

            if (found != 2)
                throw new InvalidOperationException("Expected two surface dust alternatives: " + il.Method.FullName);
        }

        private void AdjustWallSlideDustType(ILContext il)
        {
            var cursor = new ILCursor(il);
            cursor.GotoNext(MoveType.After,
                instruction => instruction.MatchLdfld<OriginalParticleCatalog>(nameof(OriginalParticleCatalog.Dust)));
            int local = -1;
            cursor.GotoNext(MoveType.After, instruction => instruction.MatchStloc(out local));
            cursor.Emit(OpCodes.Ldloc, il.Body.Variables[local]);
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.EmitDelegate<Func<ParticleTypePresentation, OriginalLevelSceneRoot, ParticleTypePresentation>>(
                DustType);
            cursor.Emit(OpCodes.Stloc, il.Body.Variables[local]);
        }

        public void Dispose()
        {
            for (int index = hooks.Count - 1; index >= 0; index--) hooks[index].Dispose();
            hooks.Clear();
            invertedDust = invertedSparkyDust = null;
        }

        private delegate void JumpOriginal(RoomPlayerSession player, bool particles, bool playSfx);

        private delegate void JumpHook(JumpOriginal original, RoomPlayerSession player, bool particles, bool playSfx);

        private delegate Vector2 LaunchOriginal(RoomPlayerSession player, Vector2 from, bool snapUp, bool sidesOnly);

        private delegate Vector2 LaunchHook(LaunchOriginal original, RoomPlayerSession player, Vector2 from,
            bool snapUp,
            bool sidesOnly);
    }
}
