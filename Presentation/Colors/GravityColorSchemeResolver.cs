using Celeste.Core.Display;
using Celeste.Core.Scheduling;
using Celeste.Everest.Content.Particles;
using Celeste.Game.Execution.Processes;
using Celeste.Game.Presentation.Particles;
using Celeste.Mods.GravityHelper.Configuration;
using Celeste.Simulation.Presentation.Particles.Definitions;

namespace Celeste.Mods.GravityHelper.Presentation.Colors
{
    public sealed class GravityColorSchemeResolver : IGravityColorSchemeResolver
    {
        public GravityColorScheme Classic { get; }
        public GravityColorScheme Colorblind { get; }

        public GravityColorSchemeResolver(ISourceEngineContext engine)
            : this(((IOriginalGameplayProcess)engine).ParticleTypes, new EverestParticleTypeConstruction())
        {
        }

        public GravityColorSchemeResolver(OriginalParticleCatalog particles,
            ISourceParticleTypeConstruction construction)
        {
            Classic = GravityColorScheme.CreateColorScheme(particles, construction, PackedColor.Blue, PackedColor.Red,
                PackedColor.Purple, false);
            Colorblind = GravityColorScheme.CreateColorScheme(particles, construction, PackedColor.FromHex("0064b1"),
                PackedColor.FromHex("ec271b"), PackedColor.FromHex("fff500"));
        }

        public IGravityColorScheme Resolve(ColorSchemeSetting setting) => setting switch
        {
            ColorSchemeSetting.Default => Classic, ColorSchemeSetting.Colorblind => Colorblind,
            _ => throw new System.ArgumentOutOfRangeException(nameof(setting))
        };
    }
}
