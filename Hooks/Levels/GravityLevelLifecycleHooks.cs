// Copyright (c) Shane Woolcock. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Reflection;
using Celeste.Core.Scheduling;
using Celeste.Game.Execution.Levels;
using Celeste.Mods.GravityHelper.Components;
using Celeste.Mods.GravityHelper.Entities.Collectibles;
using Celeste.Mods.GravityHelper.Runtime;
using Celeste.Scene.Tracking;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Player.Intro;
using MonoMod.RuntimeDetour;

namespace Celeste.Mods.GravityHelper.Hooks.Levels
{
    public sealed partial class GravityLevelLifecycleHooks : IDisposable
    {
        private readonly GravityRuntimeContext runtime;
        private readonly List<IDisposable> hooks = new();

        private delegate void UpdateOrig(OriginalLevelSceneRoot self);

        private delegate void UpdateHook(UpdateOrig orig, OriginalLevelSceneRoot self);

        private delegate void LoadOrig(OriginalLevelSceneRoot self, PlayerIntroType intro, bool fromLoader);

        private delegate void LoadHook(LoadOrig orig, OriginalLevelSceneRoot self, PlayerIntroType intro,
            bool fromLoader);

        public static IDisposable Install(ISourceEngineContext engine) =>
            new GravityLevelLifecycleHooks(GravityRuntimeContext.For(engine));

        private GravityLevelLifecycleHooks(GravityRuntimeContext runtime)
        {
            this.runtime = runtime;
            try
            {
                hooks.Add(new Hook(typeof(OriginalLevelSceneRoot).GetMethod(nameof(OriginalLevelSceneRoot.LoadLevel)),
                    (LoadHook)LoadLevel));
                hooks.Add(new Hook(typeof(OriginalLevelSceneRoot).GetMethod(nameof(OriginalLevelSceneRoot.Update)),
                    (UpdateHook)Update));
                hooks.Add(new Hook(typeof(OriginalLevelSceneRoot).GetMethod(nameof(OriginalLevelSceneRoot.End)),
                    (UpdateHook)End));
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        private void Update(UpdateOrig orig, OriginalLevelSceneRoot self)
        {
            if (ReferenceEquals(self.EngineContext, runtime.Engine) && runtime.Settings.ToggleInvertGravity.Pressed &&
                !self.FrozenOrPaused && self.Scheduler.Entities.GetTrackedEntity<RoomPlayerSession>() != null)
            {
                runtime.Settings.ToggleInvertGravity.ConsumePress();
                self.Session.SetFlag(PlayerGravityComponent.PlayerFlag,
                    !self.Session.GetFlag(PlayerGravityComponent.PlayerFlag));
            }

            orig(self);
        }

        private void End(UpdateOrig orig, OriginalLevelSceneRoot self)
        {
            orig(self);
            if (ReferenceEquals(self.EngineContext, runtime.Engine)) runtime.ClearStatics();
        }

        private void LoadLevel(LoadOrig orig, OriginalLevelSceneRoot self, PlayerIntroType intro, bool fromLoader)
        {
            if (!ReferenceEquals(self.EngineContext, runtime.Engine))
            {
                orig(self, intro, fromLoader);
                return;
            }

            if (!fromLoader)
            {
                orig(self, intro, false);
                GravityRefillReplacement.Replace(self);
                return;
            }

            runtime.ClearStatics();
            var controllers = PreloadControllers(self);
            orig(self, intro, true);
            GravityRefillReplacement.Replace(self);
            ApplyControllers(self, controllers);
        }

        public void Dispose()
        {
            for (int i = hooks.Count - 1; i >= 0; i--) hooks[i].Dispose();
            hooks.Clear();
        }
    }
}
