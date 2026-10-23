// Copyright (c) Shane Woolcock. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using Celeste.Core.Scheduling;
using Celeste.Everest.UI.Settings;
using Celeste.Everest.UI.Text;
using Celeste.Localization.Lookup;
using Celeste.Simulation.UI.Menus.Text;

namespace Celeste.Mods.GravityHelper.Configuration.SettingsMenu
{
    public static class GravityHelperSettingsMenu
    {
        public static void Build(ISourceEngineContext engine, GravityHelperSettings settings, TextMenuPresentation menu,
            bool inGame, bool gravityHookActive, Action<bool> refreshPersistentVvvvvvController)
        {
            string Clean(string key) => ((ISourceDialogEnvironment)engine).Dialog.Clean(key);

            void AddWithDescription(TextMenuPresentation.Item item, string description)
            {
                menu.Add(item);
                item.AddDescription(menu, description);
            }

            void AddSubHeader(string key) => menu.Add(new TextMenuPresentation.SubHeader(
                SourceDialogTextLookup.CleanOrNull(((ISourceDialogEnvironment)engine).Dialog, key) ?? key, false));

            IEnumerable<Tuple<string, T>> GetEnumOptions<T>() where T : struct, Enum => Enum.GetValues(typeof(T))
                .Cast<T>()
                .Select(value => Tuple.Create(Clean(EnumDialogKey(value)), value));

            // settings.AllowInAllMaps
            AddWithDescription(
                new ColorChangeOnOff(engine, Clean("GRAVITYHELPER_MENU_ALLOW_IN_ALL_MAPS"), settings.AllowInAllMaps)
                {
                    Disabled = inGame,
                    OnValueChange = value => settings.AllowInAllMaps = value,
                }, Clean("GRAVITYHELPER_MENU_ALLOW_IN_ALL_MAPS_SUBTEXT"));

            var ghActive = gravityHookActive;

            if (!inGame || ghActive)
            {
                // Fun Options Subheader
                AddSubHeader("GRAVITYHELPER_MENU_SUBHEADER_FUN_OPTIONS");

                // settings.VvvvvvMode
                AddWithDescription(new ColorChangeTextMenuOption<VvvvvvSetting>(Clean("GRAVITYHELPER_MENU_VVVVVV_MODE"))
                {
                    Values = GetEnumOptions<VvvvvvSetting>().ToList(),
                    Index = (int)settings.VvvvvvMode,
                    OnValueChange = value =>
                    {
                        settings.VvvvvvMode = value;
                        refreshPersistentVvvvvvController(true);
                    },
                }, Clean("GRAVITYHELPER_MENU_VVVVVV_MODE_SUBTEXT"));

                // settings.VvvvvvAllowGrabbing
                AddWithDescription(
                    new ColorChangeTextMenuOption<VvvvvvSetting>(Clean("GRAVITYHELPER_MENU_VVVVVV_ALLOW_GRABBING"))
                    {
                        Values = GetEnumOptions<VvvvvvSetting>().ToList(),
                        Index = (int)settings.VvvvvvAllowGrabbing,
                        OnValueChange = value =>
                        {
                            settings.VvvvvvAllowGrabbing = value;
                            refreshPersistentVvvvvvController(false);
                        },
                    }, Clean("GRAVITYHELPER_MENU_VVVVVV_ALLOW_GRABBING_SUBTEXT"));

                // settings.VvvvvvAllowDashing
                AddWithDescription(
                    new ColorChangeTextMenuOption<VvvvvvSetting>(Clean("GRAVITYHELPER_MENU_VVVVVV_ALLOW_DASHING"))
                    {
                        Values = GetEnumOptions<VvvvvvSetting>().ToList(),
                        Index = (int)settings.VvvvvvAllowDashing,
                        OnValueChange = value =>
                        {
                            settings.VvvvvvAllowDashing = value;
                            refreshPersistentVvvvvvController(false);
                        },
                    }, Clean("GRAVITYHELPER_MENU_VVVVVV_ALLOW_DASHING_SUBTEXT"));

                // settings.VvvvvvFlipSound
                AddWithDescription(
                    new ColorChangeTextMenuOption<VvvvvvSetting>(Clean("GRAVITYHELPER_MENU_VVVVVV_FLIP_SOUND"))
                    {
                        Values = GetEnumOptions<VvvvvvSetting>().ToList(),
                        Index = (int)settings.VvvvvvFlipSound,
                        OnValueChange = value =>
                        {
                            settings.VvvvvvFlipSound = value;
                            refreshPersistentVvvvvvController(false);
                        },
                    }, Clean("GRAVITYHELPER_MENU_VVVVVV_FLIP_SOUND_SUBTEXT"));

                // settings.ReplaceRefills
                AddWithDescription(new ColorChangeOnOff(engine, Clean("GRAVITYHELPER_MENU_REPLACE_REFILLS"),
                    settings.ReplaceRefills)
                {
                    Disabled = inGame,
                    OnValueChange = value => settings.ReplaceRefills = value,
                }, Clean("GRAVITYHELPER_MENU_REPLACE_REFILLS_SUBTEXT"));

                // Advanced Subheader
                AddSubHeader("GRAVITYHELPER_MENU_SUBHEADER_ADVANCED");

                // settings.MHHUDJTCornerCorrection
                AddWithDescription(new ColorChangeOnOff(engine, Clean("GRAVITYHELPER_MENU_MHH_UDJT_CORNER_CORRECTION"),
                    settings.MHHUDJTCornerCorrection, true)
                {
                    Disabled = inGame,
                    OnValueChange = value => settings.MHHUDJTCornerCorrection = value,
                }, Clean("GRAVITYHELPER_MENU_MHH_UDJT_CORNER_CORRECTION_SUBTEXT"));
            }

            // Controls Subheader
            AddSubHeader("GRAVITYHELPER_MENU_SUBHEADER_CONTROLS");

            // settings.ControlScheme
            AddWithDescription(
                new ColorChangeTextMenuOption<ControlSchemeSetting>(Clean("GRAVITYHELPER_MENU_CONTROL_SCHEME"))
                {
                    Values = GetEnumOptions<ControlSchemeSetting>().ToList(),
                    Index = (int)settings.ControlScheme,
                    OnValueChange = value =>
                    {
                        settings.ControlScheme = value;
                        settings.FeatherControlScheme = settings.ControlScheme;
                        foreach (var item in menu.Items.OfType<ColorChangeTextMenuOption<ControlSchemeSetting>>()
                                     .Where(i => i.Index != (int)value))
                        {
                            item.Index = (int)value;
                        }
                    },
                }, Clean("GRAVITYHELPER_MENU_CONTROL_SCHEME_SUBTEXT"));

            // settings.FeatherControlScheme
            AddWithDescription(
                new ColorChangeTextMenuOption<ControlSchemeSetting>(Clean("GRAVITYHELPER_MENU_FEATHER_CONTROL_SCHEME"))
                {
                    Values = GetEnumOptions<ControlSchemeSetting>().ToList(),
                    Index = (int)settings.FeatherControlScheme,
                    OnValueChange = value => settings.FeatherControlScheme = value,
                }, Clean("GRAVITYHELPER_MENU_FEATHER_CONTROL_SCHEME_SUBTEXT"));

            // Accessibility Subheader
            AddSubHeader("GRAVITYHELPER_MENU_SUBHEADER_ACCESSIBILITY");

            // Spring Arrows
            menu.Add(new ColorChangeTextMenuOption<ArrowSetting>(Clean("GRAVITYHELPER_MENU_ACCESS_SPRING_ARROWS"))
            {
                Values = GetEnumOptions<ArrowSetting>().ToList(),
                Index = (int)settings.SpringArrowType,
                OnValueChange = value =>
                {
                    settings.SpringArrowType = value;
                    settings.NotifyAccessibilityChanged();
                },
            });

            // Field Arrows
            menu.Add(new ColorChangeTextMenuOption<ArrowSetting>(Clean("GRAVITYHELPER_MENU_ACCESS_FIELD_ARROWS"))
            {
                Values = GetEnumOptions<ArrowSetting>().ToList(),
                Index = (int)settings.FieldArrowType,
                OnValueChange = value =>
                {
                    settings.FieldArrowType = value;
                    settings.NotifyAccessibilityChanged();
                },
            });

            // Field Edges
            menu.Add(new ColorChangeOnOff(engine, Clean("GRAVITYHELPER_MENU_ACCESS_FIELD_EDGES"), settings.FieldEdges,
                true)
            {
                OnValueChange = value =>
                {
                    settings.FieldEdges = value;
                    settings.NotifyAccessibilityChanged();
                },
            });

            // Field Particles
            menu.Add(new ColorChangeOnOff(engine, Clean("GRAVITYHELPER_MENU_ACCESS_FIELD_PARTICLES"),
                settings.FieldParticles, true)
            {
                OnValueChange = value =>
                {
                    settings.FieldParticles = value;
                    settings.NotifyAccessibilityChanged();
                },
            });

            // Field Opacity
            menu.Add(new ColorChangePercent(Clean("GRAVITYHELPER_MENU_ACCESS_FIELD_OPACITY"),
                Clean("GRAVITYHELPER_ENUM_ACCESS_FIELD_OPACITY_DEFAULT"), settings.FieldOpacity, -1)
            {
                OnValueChange = value =>
                {
                    settings.FieldOpacity = value;
                    settings.NotifyAccessibilityChanged();
                }
            });

            // Field Bloom
            menu.Add(new ColorChangeOnOff(engine, Clean("GRAVITYHELPER_MENU_ACCESS_FIELD_BLOOM"), settings.FieldBloom,
                true)
            {
                OnValueChange = value =>
                {
                    settings.FieldBloom = value;
                    settings.NotifyAccessibilityChanged();
                },
            });

            // Field Arrow Outline
            menu.Add(new ColorChangeOnOff(engine, Clean("GRAVITYHELPER_MENU_ACCESS_FIELD_ARROW_OUTLINE"),
                settings.FieldArrowOutline, false)
            {
                OnValueChange = value =>
                {
                    settings.FieldArrowOutline = value;
                    settings.NotifyAccessibilityChanged();
                },
            });

            // Refill Bloom
            menu.Add(new ColorChangeOnOff(engine, Clean("GRAVITYHELPER_MENU_ACCESS_REFILL_BLOOM"), settings.RefillBloom,
                true)
            {
                OnValueChange = value =>
                {
                    settings.RefillBloom = value;
                    settings.NotifyAccessibilityChanged();
                },
            });

            // High Vis Lines
            menu.Add(new ColorChangeOnOff(engine, Clean("GRAVITYHELPER_MENU_ACCESS_HIGH_VIS_LINES"),
                settings.HighVisibilityLines, false)
            {
                OnValueChange = value =>
                {
                    settings.HighVisibilityLines = value;
                    settings.NotifyAccessibilityChanged();
                }
            });

            // Colour Scheme
            menu.Add(new ColorChangeTextMenuOption<ColorSchemeSetting>(Clean("GRAVITYHELPER_MENU_ACCESS_COLOR_SCHEME"))
            {
                Values = GetEnumOptions<ColorSchemeSetting>().ToList(),
                Index = (int)settings.ColorSchemeType,
                OnValueChange = value =>
                {
                    settings.ColorSchemeType = value;
                    settings.NotifyAccessibilityChanged();
                },
            });
        }

        private static string EnumDialogKey<T>(T value) where T : struct, Enum
        {
            var prefix = typeof(T) == typeof(VvvvvvSetting) ? "GRAVITYHELPER_ENUM_VVVVVV_"
                : typeof(T) == typeof(ControlSchemeSetting) ? "GRAVITYHELPER_ENUM_CONTROL_SCHEME_"
                : typeof(T) == typeof(ArrowSetting) ? "GRAVITYHELPER_ENUM_ARROWSETTING_"
                : typeof(T) == typeof(ColorSchemeSetting) ? "GRAVITYHELPER_ENUM_COLORSCHEMESETTING_"
                : throw new ArgumentOutOfRangeException(nameof(value));
            return prefix + value.ToString().ToUpperInvariant();
        }
    }
}
