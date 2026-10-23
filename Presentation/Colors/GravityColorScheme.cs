// Copyright (c) Shane Woolcock. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using Celeste.Mods.GravityHelper.Configuration;
using Celeste.Mods.GravityHelper.Gravity;
using Celeste.Game.Presentation.Particles;
using Celeste.Simulation.Presentation.Particles.Definitions;
using Celeste.Core.Display;


namespace Celeste.Mods.GravityHelper.Presentation.Colors
{
    [global::Celeste.Core.Reflection.Runtime.RuntimeTypeAlias("Celeste.Mod.GravityHelper.GravityColorScheme")]
    public class GravityColorScheme : IGravityColorScheme
    {
        #region Colors

        // GravityType
        public PackedColor NormalColor;
        public PackedColor InvertedColor;
        public PackedColor ToggleColor;

        #endregion

        #region Particle Types

        // GravityBumper
        public readonly ParticleTypePresentation P_GravityBumper_Ambience_Normal;
        public readonly ParticleTypePresentation P_GravityBumper_Ambience_Inverted;
        public readonly ParticleTypePresentation P_GravityBumper_Ambience_Toggle;
        public readonly ParticleTypePresentation P_GravityBumper_Launch_Normal;
        public readonly ParticleTypePresentation P_GravityBumper_Launch_Inverted;
        public readonly ParticleTypePresentation P_GravityBumper_Launch_Toggle;

        // GravityRefill
        public readonly ParticleTypePresentation P_GravityRefill_Shatter;
        public readonly ParticleTypePresentation P_GravityRefill_Regen;
        public readonly ParticleTypePresentation P_GravityRefill_Glow_Normal;
        public readonly ParticleTypePresentation P_GravityRefill_Glow_Inverted;

        // InversionBlock
        public readonly ParticleTypePresentation P_InversionBlock_NormalInverted;
        public readonly ParticleTypePresentation P_InversionBlock_NormalToggle;
        public readonly ParticleTypePresentation P_InversionBlock_InvertedToggle;

        #endregion

        public bool NeedsShader;

        public PackedColor this[GravityType type] =>
            type switch
            {
                GravityType.Normal => NormalColor,
                GravityType.Inverted => InvertedColor,
                GravityType.Toggle => ToggleColor,
                _ => PackedColor.White
            };

        private readonly OriginalParticleCatalog particles;


        public static GravityColorScheme CreateColorScheme(OriginalParticleCatalog particles,
            ISourceParticleTypeConstruction construction, PackedColor normalColor, PackedColor invertedColor,
            PackedColor toggleColor, bool needsShader = true)
        {
            const float bumper_lightness = 0.5f;

            normalColor.ToHSV(out var normalHue, out var normalSaturation, out var normalValue);
            invertedColor.ToHSV(out var invertedHue, out var invertedSaturation, out var invertedValue);
            toggleColor.ToHSV(out var toggleHue, out var toggleSaturation, out var toggleValue);

            // these are calculated roughly based on "classic" base colours and the legacy secondary XNA colour constants
            // they will not match the variable name if other base colours are provided, but they should be used in place
            var mediumPurpleFromToggle =
                ColorExtensions.FromHSV(toggleHue - 41, toggleSaturation * 0.49f, toggleValue * 1.72f);
            var blueVioletFromNormal =
                ColorExtensions.FromHSV(normalHue + 31, normalSaturation * 0.81f, normalValue * 0.89f);
            var mediumVioletRedFromInverted =
                ColorExtensions.FromHSV(invertedHue - 38, invertedSaturation * 0.89f, invertedValue * 0.78f);
            var blueVioletFromToggle =
                ColorExtensions.FromHSV(toggleHue - 29, toggleSaturation * 0.81f, toggleValue * 1.78f);
            var violetFromToggle = ColorExtensions.FromHSV(toggleHue, toggleSaturation * 0.45f, toggleValue * 1.86f);

            return new(particles, construction)
            {
                NeedsShader = needsShader,
                NormalColor = normalColor,
                InvertedColor = invertedColor,
                ToggleColor = toggleColor,
                P_GravityBumper_Launch_Normal = { Color = normalColor, Color2 = normalColor.Lighter(bumper_lightness) },
                P_GravityBumper_Launch_Inverted =
                    { Color = invertedColor, Color2 = invertedColor.Lighter(bumper_lightness) },
                P_GravityBumper_Launch_Toggle = { Color = toggleColor, Color2 = toggleColor.Lighter(bumper_lightness) },
                P_GravityBumper_Ambience_Normal =
                    { Color = normalColor, Color2 = normalColor.Lighter(bumper_lightness) },
                P_GravityBumper_Ambience_Inverted =
                    { Color = invertedColor, Color2 = invertedColor.Lighter(bumper_lightness) },
                P_GravityBumper_Ambience_Toggle =
                    { Color = toggleColor, Color2 = toggleColor.Lighter(bumper_lightness) },
                P_GravityRefill_Shatter = { Color = toggleColor, Color2 = mediumPurpleFromToggle },
                P_GravityRefill_Regen = { Color = blueVioletFromToggle, Color2 = violetFromToggle },
                P_GravityRefill_Glow_Normal = { Color = normalColor, Color2 = blueVioletFromNormal },
                P_GravityRefill_Glow_Inverted = { Color = invertedColor, Color2 = mediumVioletRedFromInverted },
                P_InversionBlock_NormalInverted =
                {
                    Color = normalColor, Color2 = invertedColor,
                    DirectionRange = MathF.PI / 4f
                },
                P_InversionBlock_NormalToggle =
                {
                    Color = normalColor, Color2 = toggleColor,
                    DirectionRange = MathF.PI / 4f
                },
                P_InversionBlock_InvertedToggle =
                {
                    Color = invertedColor, Color2 = toggleColor,
                    DirectionRange = MathF.PI / 4f
                },
            };
        }


        // particle type helpers

        public ParticleTypePresentation GetBumperAmbienceParticleType(GravityType gravityType) =>
            gravityType switch
            {
                GravityType.Normal => P_GravityBumper_Ambience_Normal,
                GravityType.Inverted => P_GravityBumper_Ambience_Inverted,
                GravityType.Toggle => P_GravityBumper_Ambience_Toggle,
                _ => particles.Bumper.P_Ambience,
            };

        public ParticleTypePresentation GetBumperLaunchParticleType(GravityType gravityType) =>
            gravityType switch
            {
                GravityType.Normal => P_GravityBumper_Launch_Normal,
                GravityType.Inverted => P_GravityBumper_Launch_Inverted,
                GravityType.Toggle => P_GravityBumper_Launch_Toggle,
                _ => particles.Bumper.P_Launch,
            };

        public PackedColor GetRippleColor(GravityType gravityType) => this[gravityType].Saturation(2f);

        private GravityColorScheme(OriginalParticleCatalog particles, ISourceParticleTypeConstruction construction)
        {
            this.particles = particles;
            P_GravityBumper_Ambience_Normal = construction.Clone(particles.Bumper.P_Ambience);
            P_GravityBumper_Ambience_Inverted = construction.Clone(particles.Bumper.P_Ambience);
            P_GravityBumper_Ambience_Toggle = construction.Clone(particles.Bumper.P_Ambience);
            P_GravityBumper_Launch_Normal = construction.Clone(particles.Bumper.P_Launch);
            P_GravityBumper_Launch_Inverted = construction.Clone(particles.Bumper.P_Launch);
            P_GravityBumper_Launch_Toggle = construction.Clone(particles.Bumper.P_Launch);
            P_GravityRefill_Shatter = construction.Clone(particles.Refill.P_Shatter);
            P_GravityRefill_Regen = construction.Clone(particles.Refill.P_Regen);
            P_GravityRefill_Glow_Normal = construction.Clone(particles.Refill.P_Glow);
            P_GravityRefill_Glow_Inverted = construction.Clone(particles.Refill.P_Glow);
            P_InversionBlock_NormalInverted = construction.Clone(particles.Player.P_Split);
            P_InversionBlock_NormalToggle = construction.Clone(particles.Player.P_Split);
            P_InversionBlock_InvertedToggle = construction.Clone(particles.Player.P_Split);
        }

        PackedColor IGravityColorScheme.NormalColor => NormalColor;
        PackedColor IGravityColorScheme.InvertedColor => InvertedColor;
        PackedColor IGravityColorScheme.ToggleColor => ToggleColor;
        bool IGravityColorScheme.NeedsShader => NeedsShader;
        ParticleTypePresentation IGravityColorScheme.P_GravityBumper_Ambience_Normal => P_GravityBumper_Ambience_Normal;

        ParticleTypePresentation IGravityColorScheme.P_GravityBumper_Ambience_Inverted =>
            P_GravityBumper_Ambience_Inverted;

        ParticleTypePresentation IGravityColorScheme.P_GravityBumper_Ambience_Toggle => P_GravityBumper_Ambience_Toggle;
        ParticleTypePresentation IGravityColorScheme.P_GravityBumper_Launch_Normal => P_GravityBumper_Launch_Normal;
        ParticleTypePresentation IGravityColorScheme.P_GravityBumper_Launch_Inverted => P_GravityBumper_Launch_Inverted;
        ParticleTypePresentation IGravityColorScheme.P_GravityBumper_Launch_Toggle => P_GravityBumper_Launch_Toggle;
        ParticleTypePresentation IGravityColorScheme.P_GravityRefill_Shatter => P_GravityRefill_Shatter;
        ParticleTypePresentation IGravityColorScheme.P_GravityRefill_Regen => P_GravityRefill_Regen;
        ParticleTypePresentation IGravityColorScheme.P_GravityRefill_Glow_Normal => P_GravityRefill_Glow_Normal;
        ParticleTypePresentation IGravityColorScheme.P_GravityRefill_Glow_Inverted => P_GravityRefill_Glow_Inverted;
        ParticleTypePresentation IGravityColorScheme.P_InversionBlock_NormalInverted => P_InversionBlock_NormalInverted;
        ParticleTypePresentation IGravityColorScheme.P_InversionBlock_NormalToggle => P_InversionBlock_NormalToggle;
        ParticleTypePresentation IGravityColorScheme.P_InversionBlock_InvertedToggle => P_InversionBlock_InvertedToggle;
    }
}
