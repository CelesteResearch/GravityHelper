// Copyright (c) Shane Woolcock. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using Celeste.Core.Scheduling;
using Celeste.Mods.GravityHelper.Runtime;
using Celeste.Scene.Entities;
using Celeste.Simulation.Gameplay.Platforms.Foundation;
using Celeste.Game.Execution.LevelLoader.Terrain;
using MonoMod.RuntimeDetour;

namespace Celeste.Mods.GravityHelper.Hooks.Platforms
{
    public sealed partial class GravityPlatformHooks : IDisposable
    {
        private readonly GravityRuntimeContext runtime;
        private readonly List<IDisposable> leases = new();

        public static IDisposable Install(ISourceEngineContext engine) =>
            new GravityPlatformHooks(GravityRuntimeContext.For(engine));

        private GravityPlatformHooks(GravityRuntimeContext runtime)
        {
            this.runtime = runtime;
            try
            {
                leases.Add(new ILHook(
                    typeof(SourcePlatformEntityState).GetMethod(
                        nameof(SourcePlatformEntityState.MoveVExactCollideSolids)),
                    PlatformCollision));
                leases.Add(new ILHook(
                    typeof(SourceSolidEntityState).GetMethod(nameof(SourceSolidEntityState.GetPlayerOnTop)),
                    SolidPlayerOnTop));
                leases.Add(new Hook(typeof(SourceSolidEntityState).GetMethod(nameof(SourceSolidEntityState.MoveVExact)),
                    (SolidMoveHook)SolidMove));
                leases.Add(new ILHook(
                    typeof(OriginalSolidTiles).GetMethod(nameof(OriginalSolidTiles.GetLandSoundIndex)),
                    TilesSound));
                leases.Add(new ILHook(
                    typeof(OriginalSolidTiles).GetMethod(nameof(OriginalSolidTiles.GetStepSoundIndex)),
                    TilesSound));
                leases.Add(new Hook(
                    typeof(SourceJumpThruEntityState).GetMethod(nameof(SourceJumpThruEntityState.MoveVExact)),
                    (JumpMoveHook)JumpMove));
                leases.Add(new ILHook(
                    typeof(SourceJumpThruEntityState).GetMethod(nameof(SourceJumpThruEntityState.MoveVExact)),
                    JumpMovement));
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        private bool Own(SceneEntityState entity) => entity.Scene is ISourceEngineContextScene scene &&
                                                     ReferenceEquals(scene.EngineContext, runtime.Engine);

        private bool Inverted(SceneEntityState entity) =>
            Own(entity) && (runtime.PlayerComponent?.ShouldInvert ?? false);

        public void Dispose()
        {
            for (int i = leases.Count - 1; i >= 0; i--) leases[i].Dispose();
            leases.Clear();
        }
    }
}
