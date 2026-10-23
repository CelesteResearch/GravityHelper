// Copyright (c) Shane Woolcock. Licensed under the MIT Licence.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using Celeste.Core.Scheduling;
using Celeste.Game.Execution.Processes;
using Celeste.Simulation.Presentation.Particles.Definitions;

namespace Celeste.Mods.GravityHelper.Runtime.Particles
{
    internal sealed class InvertedSummitLandParticles
    {
        private static readonly ConditionalWeakTable<ISourceEngineContext, InvertedSummitLandParticles> Owners = new();

        internal static InvertedSummitLandParticles For(ISourceEngineContext engine) =>
            Owners.GetValue(engine, static owner => new InvertedSummitLandParticles(owner));

        private readonly Lazy<ParticleTypePresentation> b;
        private readonly Lazy<ParticleTypePresentation> c;

        private InvertedSummitLandParticles(ISourceEngineContext engine)
        {
            b = new Lazy<ParticleTypePresentation>(() =>
                new ParticleTypePresentation(((IOriginalGameplayProcess)engine).ParticleTypes.Player.P_SummitLandB)
                    { Acceleration = Vector2.UnitY * 60 });
            c = new Lazy<ParticleTypePresentation>(() =>
                new ParticleTypePresentation(((IOriginalGameplayProcess)engine).ParticleTypes.Player.P_SummitLandC)
                    { Acceleration = Vector2.UnitY * -20, Direction = (float)Math.PI / 2 });
        }

        internal ParticleTypePresentation B => b.Value;
        internal ParticleTypePresentation C => c.Value;
    }
}
