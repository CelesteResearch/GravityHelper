// Copyright (c) Shane Woolcock. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using Celeste.Audio.Fmod.Events;
using Celeste.Core.Reflection.Interop;
using Celeste.Core.Scheduling;
using Celeste.Everest.Content.Reloading;
using Celeste.Everest.Modules.Persistence;
using Celeste.Everest.Modules.Runtime;
using Celeste.Everest.Modules.Settings;
using Celeste.Mods.GravityHelper.Activation;
using Celeste.Mods.GravityHelper.Components.Accessibility;
using Celeste.Mods.GravityHelper.Configuration;
using Celeste.Mods.GravityHelper.Configuration.SettingsMenu;
using Celeste.Mods.GravityHelper.Entities.Controllers;
using Celeste.Mods.GravityHelper.Hooks;
using Celeste.Mods.GravityHelper.Interop;
using Celeste.Mods.GravityHelper.Presentation.Colors;
using Celeste.Mods.GravityHelper.Presentation.Shaders;
using Celeste.Mods.GravityHelper.Runtime;
using Celeste.Game.Execution.Levels;
using Celeste.Scene.Tracking;
using Celeste.Simulation.UI.Menus.Text;

namespace Celeste.Mods.GravityHelper.Modules
{
    public sealed partial class GravityHelperModule : SourceEverestModule
    {
        private readonly GravityRuntimeContext runtime;
        private readonly List<IDisposable> bootstrapHooks = [];
        private readonly List<IDisposable> mapHooks = [];
        private GravityHookLevel currentHookLevel;

        public GravityHelperModule(ISourceEngineContext engine) : base(engine)
            => runtime = GravityRuntimeContext.For(engine);

        public override Type SettingsType => typeof(GravityHelperSettings);
        public override Type SessionType => typeof(GravityHelperSession);

        public override EverestModuleSettings _Settings
        {
            get => GravityRuntimeContext.For(SourceEngine).Settings;
            set => GravityRuntimeContext.For(SourceEngine).Settings = (GravityHelperSettings)value;
        }

        public override EverestModuleSession _Session
        {
            get => GravityRuntimeContext.For(SourceEngine).Session;
            set => GravityRuntimeContext.For(SourceEngine).Session = (GravityHelperSession)value;
        }

        public override void Load()
        {
            runtime.Settings.MigrateIfRequired();
            bootstrapHooks.Add(((ISourceModInteropEnvironment)SourceEngine).ModInterop.RegisterOwned(
                new SourceModInteropActualTypeBinding(typeof(GravityHelperExports),
                    new GravityHelperExports(runtime))));
            bootstrapHooks.Add(new GravityLoaderHooks(runtime, UpdateHooks));
            bootstrapHooks.Add(new GravityLevelEnterHooks(runtime));
            runtime.Settings.AccessibilityChanged += AccessibilityChanged;
            ((IEverestAssetReloadEnvironment)SourceEngine).AssetReload.Events.OnAfterReload += AfterReload;
        }

        public override void LoadContent(bool firstLoad)
            => runtime.Settings.ColorSchemeResolver = new GravityColorSchemeResolver(SourceEngine);

        public override void CreateModMenuSection(TextMenuPresentation menu, bool inGame, FmodEventReference snapshot)
        {
            CreateModMenuSectionHeader(menu, inGame, snapshot);
            GravityHelperSettingsMenu.Build(SourceEngine, runtime.Settings, menu, inGame,
                currentHookLevel is GravityHookLevel.GravityHelperMap or GravityHookLevel.Forced,
                RefreshVvvvvvController);
            CreateModMenuSectionKeyBindings(menu, inGame, snapshot);
        }

        private void RefreshVvvvvvController(bool createIfRequired)
        {
            if (SourceEngine.Frame.Scenes.CurrentScene is not OriginalLevelSceneRoot level) return;
            // The mode setting creates the persistent controller; the other settings only query it.
            var controller = GravityControllerQueries.GetPersistent<VvvvvvGravityController>(level, createIfRequired);
            if (controller != null) controller.Transitioned();
        }

        private void AccessibilityChanged()
        {
            if (SourceEngine.Frame.Scenes.CurrentScene is not ISourceScene scene) return;
            foreach (AccessibilityListener listener in scene.SourceScene.Entities
                         .GetComponentsTrackIfNeeded<AccessibilityListener>()) listener.OnAccessibilityChange?.Invoke();
        }

        private void BeforeReload(bool silent) => runtime.GravityBeforeReload = runtime.PlayerComponent?.CurrentGravity;
        private void AfterReload(bool silent) => GravityTintShader.For(runtime).Clear();

        private void UpdateHooks(GravityHookLevel required)
        {
            if (required == currentHookLevel) return;
            if (currentHookLevel is GravityHookLevel.GravityHelperMap or GravityHookLevel.Forced)
                ((IEverestAssetReloadEnvironment)SourceEngine).AssetReload.Events.OnBeforeReload -= BeforeReload;
            for (int index = mapHooks.Count - 1; index >= 0; index--) mapHooks[index].Dispose();
            mapHooks.Clear();
            currentHookLevel = GravityHookLevel.None;
            try
            {
                if (required == GravityHookLevel.RenderOnly)
                    mapHooks.Add(new GravityPlayerPresentationHooks(runtime, ResolveGhostType(), true));
                else if (required is GravityHookLevel.GravityHelperMap or GravityHookLevel.Forced)
                {
                    InstallMapHooks();
                    if (required == GravityHookLevel.GravityHelperMap && runtime.Settings.MHHUDJTCornerCorrection)
                        mapHooks.Add(
                            new global::Celeste.Mods.GravityHelper.Hooks.ThirdParty.GravityMhhUpsideDownCornerHooks(
                                runtime));
                    ((IEverestAssetReloadEnvironment)SourceEngine).AssetReload.Events.OnBeforeReload += BeforeReload;
                }

                currentHookLevel = required;
            }
            catch
            {
                for (int index = mapHooks.Count - 1; index >= 0; index--) mapHooks[index].Dispose();
                mapHooks.Clear();
                throw;
            }
        }

        public override void Unload()
        {
            UpdateHooks(GravityHookLevel.None);
            for (int index = bootstrapHooks.Count - 1; index >= 0; index--) bootstrapHooks[index].Dispose();
            bootstrapHooks.Clear();
            runtime.Settings.AccessibilityChanged -= AccessibilityChanged;
            ((IEverestAssetReloadEnvironment)SourceEngine).AssetReload.Events.OnAfterReload -= AfterReload;
        }
    }
}
