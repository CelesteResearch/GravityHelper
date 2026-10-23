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
    internal class ColorChangeTextMenuOption<T> : TextMenuPresentation.Option<T> where T : struct
    {
        public T DefaultValue;
        public PackedColor ChangedUnselectedColor = PackedColor.Goldenrod;

        private int _lastFrameIndex = int.MinValue;

        public ColorChangeTextMenuOption(string label, T defaultValue = default) : base(label)
        {
            DefaultValue = defaultValue;
        }

        public override void Render(ISourceRenderContext context, Vector2 position, bool highlighted)
        {
            if (_lastFrameIndex == int.MinValue || _lastFrameIndex != Index)
            {
                _lastFrameIndex = Index;
                UpdateUnselectedColor();
            }

            base.Render(context, position, highlighted);
        }

        protected virtual void UpdateUnselectedColor() =>
            UnselectedColor = IsDefaultValue() ? PackedColor.White : ChangedUnselectedColor;

        protected virtual bool IsDefaultValue() =>
            EqualityComparer<T>.Default.Equals(Values[Index].Item2, DefaultValue);
    }
}
