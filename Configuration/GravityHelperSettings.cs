// Copyright (c) Shane Woolcock. Licensed under the MIT Licence.

using System;
using Celeste.Everest.Input.Bindings;
using Celeste.Everest.Modules.Settings;
using YamlDotNet.Serialization;

namespace Celeste.Mods.GravityHelper.Configuration
{
    [global::Celeste.Core.Reflection.Runtime.RuntimeTypeAlias("Celeste.Mod.GravityHelper.GravityHelperModuleSettings")]
    public sealed partial class GravityHelperSettings : EverestModuleSettings
    {
        public bool AllowInAllMaps { get; set; }
        public ControlSchemeSetting ControlScheme { get; set; } = ControlSchemeSetting.Absolute;
        public ControlSchemeSetting FeatherControlScheme { get; set; } = ControlSchemeSetting.Absolute;
        public VvvvvvSetting VvvvvvMode { get; set; }
        public VvvvvvSetting VvvvvvFlipSound { get; set; }
        public VvvvvvSetting VvvvvvAllowGrabbing { get; set; }
        public VvvvvvSetting VvvvvvAllowDashing { get; set; }
        public bool ReplaceRefills { get; set; }
        public bool MHHUDJTCornerCorrection { get; set; } = true;
        public EverestButtonBinding ToggleInvertGravity { get; set; }
        public ArrowSetting SpringArrowType { get; set; }
        public ArrowSetting FieldArrowType { get; set; }
        public bool FieldEdges { get; set; } = true;
        public bool FieldParticles { get; set; } = true;
        public int FieldOpacity { get; set; } = -1;
        public bool FieldBloom { get; set; } = true;
        public bool FieldArrowOutline { get; set; }
        public bool RefillBloom { get; set; } = true;
        public bool HighVisibilityLines { get; set; }
        public ColorSchemeSetting ColorSchemeType { get; set; }
        public int SettingsVersion { get; set; }
        public const int LatestSettingsVersion = 2;
        [YamlIgnore] public IGravityColorSchemeResolver ColorSchemeResolver { get; set; }
        public event Action AccessibilityChanged;
        public IGravityColorScheme GetColorScheme() => ColorSchemeResolver.Resolve(ColorSchemeType);
        public void NotifyAccessibilityChanged() => AccessibilityChanged?.Invoke();
    }
}
