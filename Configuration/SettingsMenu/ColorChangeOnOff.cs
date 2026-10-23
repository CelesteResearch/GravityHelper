// Copyright (c) Shane Woolcock. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Numerics;
using Celeste.Core.Display;
using Celeste.Core.Rendering;
using Celeste.Core.Scheduling;
using Celeste.Localization.Lookup;
using Celeste.Simulation.UI.Menus.Text;

namespace Celeste.Mods.GravityHelper.Configuration.SettingsMenu
{
    internal class ColorChangeOnOff : ColorChangeTextMenuOption<bool>
    {
        public ColorChangeOnOff(ISourceEngineContext engine, string label, bool on, bool defaultValue = false) : base(
            label,
            defaultValue)
        {
            Add(((ISourceDialogEnvironment)engine).Dialog.Clean("options_off"), false, !on);
            Add(((ISourceDialogEnvironment)engine).Dialog.Clean("options_on"), true, on);
        }
    }
}
