using System;
using Celeste.Core.Components;
using Celeste.Scene.Tracking.Attributes;

namespace Celeste.Mods.GravityHelper.Components.Accessibility
{
    [SceneTracked]
    [global::Celeste.Core.Reflection.Runtime.RuntimeTypeAlias(
        "Celeste.Mod.GravityHelper.Components.AccessibilityListener")]
    public class AccessibilityListener : SourceComponentState
    {
        public Action OnAccessibilityChange { get; set; }

        public AccessibilityListener(Action onAccessibilityChange) : base(false, false)
            => OnAccessibilityChange = onAccessibilityChange;
    }
}
