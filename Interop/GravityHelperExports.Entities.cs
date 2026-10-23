// Copyright (c) Shane Woolcock. Licensed under the MIT Licence.

using System;
using Celeste.Core.Components;
using Celeste.Game.Components.Talking;
using Celeste.Mods.GravityHelper.Components;
using Celeste.Mods.GravityHelper.Components.Accessibility;
using Celeste.Mods.GravityHelper.Entities.Springs;
using Celeste.Mods.GravityHelper.Entities.WatchTowers;
using Celeste.Mods.GravityHelper.Gravity;
using Celeste.Scene.Entities;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Holdables;

namespace Celeste.Mods.GravityHelper.Interop
{
    public sealed partial class GravityHelperExports
    {
        public OriginalTalkUiEntity CreateUpsideDownTalkComponentUI(OriginalTalkComponent talkComponent)
            => new UpsideDownTalkComponentUI(talkComponent);

        public SourceComponentState CreateAccessibilityListener(Action onAccessibilityChange)
            => new AccessibilityListener(onAccessibilityChange);

        public void SetHoldableResetTime(HoldableState holdable, float resetTime)
        {
            if ((holdable?.Entity as SceneEntityState)?.Get<GravityHoldable>() is { } gravityHoldable)
                gravityHoldable.ResetTime = resetTime;
        }

        public void SetHoldableResetType(HoldableState holdable, int gravityType)
        {
            if ((holdable?.Entity as SceneEntityState)?.Get<GravityHoldable>() is { } gravityHoldable)
                gravityHoldable.ResetType = (GravityType)gravityType;
        }

        public void InvertedSuperBounce(RoomPlayerSession player, float fromY)
            => GravitySpring.InvertedSuperBounce(player, fromY);
    }
}
