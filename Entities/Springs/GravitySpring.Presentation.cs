using System;
using Celeste.Core.Display;
using Celeste.Core.Rendering;
using Celeste.Everest.Diagnostics.Logging;
using Celeste.Mods.GravityHelper.Configuration;
using Celeste.Mods.GravityHelper.Entities.Controllers;
using Celeste.Mods.GravityHelper.Gravity;
using Celeste.Scene.Scheduling;

namespace Celeste.Mods.GravityHelper.Entities.Springs
{
    public partial class GravitySpring
    {
        private string GetAnimId(string id) => GravityType switch
        {
            GravityType.None => "none_" + id,
            GravityType.Normal => "normal_" + id,
            GravityType.Inverted => "invert_" + id,
            GravityType.Toggle => "toggle_" + id,
            _ => id
        };

        private string GetOverlayAnimId(string id)
        {
            if (Runtime.Settings.ColorSchemeType != ColorSchemeSetting.Default) return "white_" + id;
            if (!RefillStamina) return "no_stamina_" + id;
            return RefillDashCount switch { 0 => "no_dash_" + id, >= 2 => "two_dash_" + id, _ => "" };
        }

        private void PlayOverlay(string id, bool restart = false)
        {
            if (_overlaySprite != null && _overlaySprite.Has(id)) _overlaySprite.Play(id, restart);
        }

        private void LogInvalidSprite(string name, bool overlay)
            => ((IEverestLoggerProcessEnvironment)Engine).Logger.Log("GravityHelperModule",
                "GravitySpring: Invalid " + (overlay ? "overlay sprite" : "sprite") + " name: " + name);

        public override void Added(object scene)
        {
            base.Added(scene);
            OnAccessibilityChange();
            if (_defaultToController && GravityControllerQueries.GetActive<BehaviorGravityController>(Scene) is
                    { } controller)
                _gravityCooldown = controller.SpringCooldown;
            if (_showIndicator && GravityType is GravityType.Normal or GravityType.Inverted or GravityType.Toggle)
                ((Celeste.Scene.Scenes.SourceSceneState)scene).SourceScene.QueueSourceEntity(_indicatorRenderer =
                    new GravitySpringIndicatorRenderer(this));
        }

        public override void Removed(object scene)
        {
            base.Removed(scene);
            _indicatorRenderer?.RemoveSelf();
            _indicatorRenderer = null;
        }

        private void OnEnable()
        {
            Visible = Collidable = true;
            sprite.Color = PackedColor.White;
            sprite.Play(GetAnimId("idle"));
            OnAccessibilityChange();
        }

        private void OnDisable()
        {
            Collidable = false;
            if (VisibleWhenDisabled)
            {
                sprite.Play("disabled");
                sprite.Color = DisabledColor;
                if (_overlaySprite != null) _overlaySprite.Color = PackedColor.White;
                PlayOverlay(GetOverlayAnimId("idle"));
            }
            else Visible = false;
        }

        private void OnAccessibilityChange()
        {
            var colors = Runtime.Settings.GetColorScheme();
            bool isDefault = Runtime.Settings.ColorSchemeType == ColorSchemeSetting.Default;
            if (Scene != null && _indicatorRenderer == null &&
                GravityType is GravityType.Normal or GravityType.Inverted or GravityType.Toggle)
                ((Celeste.Scene.Scenes.SourceSceneState)Scene).SourceScene.QueueSourceEntity(_indicatorRenderer =
                    new GravitySpringIndicatorRenderer(this));
            string id = GetOverlayAnimId("idle");
            if (_overlaySprite != null)
            {
                _overlaySprite.Visible = (_showOverlay || !isDefault) && _overlaySprite.Has(id) &&
                                         GravityType != GravityType.None;
                _overlaySprite.Color = isDefault ? PackedColor.White : colors[GravityType];
                if (_overlaySprite.Visible) PlayOverlay(id);
            }

            _indicatorRenderer?.UpdateIndicator();
        }

        public override void Update()
        {
            base.Update();
            if (_overlaySprite != null) _overlaySprite.Scale = sprite.Scale;
            if (_indicatorRenderer != null) _indicatorRenderer.Visible = Visible;
            if (_cooldownRemaining > 0f) _cooldownRemaining = Math.Max(0f, _cooldownRemaining - Engine.DeltaTime);
        }

        public override void Render(ISourceRenderContext context)
        {
            if (_drawOutline) sprite.DrawOutline(context);
            OwnedComponents.Render(context);
        }

        public void CustomBounceAnimate()
        {
            ((Celeste.Core.Audio.ISourceSpatialAudioEnvironment)Engine).SpatialAudio.Play("event:/game/general/spring",
                Position);
            staticMover.TriggerPlatform();
            sprite.Play(GetAnimId("bounce"), true);
            PlayOverlay(GetOverlayAnimId("bounce"), true);
            wiggler.Start();
        }
    }
}
