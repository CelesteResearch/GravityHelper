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
    internal class ColorChangePercent : ColorChangeTextMenuOption<int>
    {
        public ColorChangePercent(string label, int currentValue, int defaultValue = 50) : base(label, defaultValue)
        {
            for (int i = 0; i <= 100; i += 10)
            {
                Add($"{i}%", i, currentValue == i);
            }
        }

        public ColorChangePercent(string label, string defaultLabel, int currentValue, int defaultValue = -1) : base(
            label,
            defaultValue)
        {
            Add(defaultLabel, -1, currentValue < 0);
            for (int i = 0; i <= 100; i += 10)
            {
                Add($"{i}%", i, currentValue == i);
            }
        }
    }
}
