// Copyright (c) Shane Woolcock. Licensed under the MIT Licence.

using System;
using Celeste.Core.Scheduling;
using Celeste.Everest.Reflection.Runtime;
using Celeste.Game.Gameplay.Hazards.Bumpers;
using Celeste.Game.Presentation.Particles.Definitions.Bumper;
using Celeste.Mods.GravityHelper.Runtime;
using Celeste.Simulation.Presentation.Particles.Definitions;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;

namespace Celeste.Mods.GravityHelper.Hooks
{
    public sealed class GravityBumperHooks : IDisposable
    {
        private readonly GravityRuntimeContext runtime;
        private readonly Type gravityBumperType;
        private readonly ILHook hook;

        public GravityBumperHooks(GravityRuntimeContext runtime)
        {
            this.runtime = runtime;
            gravityBumperType =
                ((ISourceRuntimeTypeDiscoveryEnvironment)runtime.Engine).RuntimeTypeDiscovery.ResolveRuntimeTypeAlias(
                    "Celeste.Mod.GravityHelper.Entities.GravityBumper");
            hook = new ILHook(typeof(OriginalBumperState).GetMethod(nameof(OriginalBumperState.Update)), PatchUpdate);
        }

        private void PatchUpdate(ILContext il)
        {
            var cursor = new ILCursor(il);
            if (!cursor.TryGotoNext(MoveType.After,
                    instruction =>
                        instruction.MatchLdfld<BumperParticleDefinitions>(nameof(BumperParticleDefinitions
                            .P_Ambience))))
                throw new InvalidOperationException(
                    "Could not find Bumper.P_Ambience in the actual Bumper.Update body.");
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.EmitDelegate<Func<ParticleTypePresentation, OriginalBumperState, ParticleTypePresentation>>(
                SelectAmbience);
        }

        private ParticleTypePresentation SelectAmbience(ParticleTypePresentation particle, OriginalBumperState bumper)
        {
            if (bumper.Scene is not ISourceEngineContextScene scene ||
                !ReferenceEquals(scene.EngineContext, runtime.Engine) || gravityBumperType == null ||
                !gravityBumperType.IsInstanceOfType(bumper)) return particle;
            if (bumper is not IGravityBumperGravity gravity)
                throw new InvalidOperationException("Actual GravityBumper must implement its gravity capability.");
            return runtime.Settings.GetColorScheme().GetBumperAmbienceParticleType(gravity.GravityType);
        }

        public void Dispose() => hook.Dispose();
    }
}
