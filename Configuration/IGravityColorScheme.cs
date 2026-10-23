using Celeste.Core.Display;
using Celeste.Mods.GravityHelper.Gravity;
using Celeste.Simulation.Presentation.Particles.Definitions;

namespace Celeste.Mods.GravityHelper.Configuration
{
    public interface IGravityColorScheme
    {
        PackedColor NormalColor { get; }
        PackedColor InvertedColor { get; }
        PackedColor ToggleColor { get; }
        bool NeedsShader { get; }
        PackedColor this[GravityType type] { get; }
        PackedColor GetRippleColor(GravityType type);
        ParticleTypePresentation GetBumperAmbienceParticleType(GravityType type);
        ParticleTypePresentation GetBumperLaunchParticleType(GravityType type);
        ParticleTypePresentation P_GravityBumper_Ambience_Normal { get; }
        ParticleTypePresentation P_GravityBumper_Ambience_Inverted { get; }
        ParticleTypePresentation P_GravityBumper_Ambience_Toggle { get; }
        ParticleTypePresentation P_GravityBumper_Launch_Normal { get; }
        ParticleTypePresentation P_GravityBumper_Launch_Inverted { get; }
        ParticleTypePresentation P_GravityBumper_Launch_Toggle { get; }
        ParticleTypePresentation P_GravityRefill_Shatter { get; }
        ParticleTypePresentation P_GravityRefill_Regen { get; }
        ParticleTypePresentation P_GravityRefill_Glow_Normal { get; }
        ParticleTypePresentation P_GravityRefill_Glow_Inverted { get; }
        ParticleTypePresentation P_InversionBlock_NormalInverted { get; }
        ParticleTypePresentation P_InversionBlock_NormalToggle { get; }
        ParticleTypePresentation P_InversionBlock_InvertedToggle { get; }
    }
}
