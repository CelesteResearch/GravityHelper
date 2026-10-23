using System;
using System.Collections.Generic;
using System.Reflection;
using Celeste.Core.Scheduling;
using Celeste.Everest.Gameplay.Platforms.Cassette;
using Celeste.Game.Gameplay.Platforms.Cassette;
using Celeste.Mods.GravityHelper.Components.Cassette;
using Celeste.Mods.GravityHelper.Entities.Controllers;
using Celeste.Core.Components;
using Celeste.Scene.Tracking;
using MonoMod.RuntimeDetour;

namespace Celeste.Mods.GravityHelper.Hooks
{
    public sealed class GravityCassetteHooks : IDisposable
    {
        private readonly ISourceEngineContext engine;
        private readonly List<IDisposable> hooks = new();
        private GravityCassetteHooks(ISourceEngineContext engine) => this.engine = engine;

        public static IDisposable Install(ISourceEngineContext engine)
        {
            var process = new GravityCassetteHooks(engine);
            try
            {
                var t = typeof(InstalledCassetteBlockManagerState);
                process.hooks.Add(new Hook(
                    t.GetMethod("SetActiveIndex",
                        BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)!,
                    (Action<Action<InstalledCassetteBlockManagerState, int>, InstalledCassetteBlockManagerState, int>)
                    process.SetActiveIndex));
                process.hooks.Add(new Hook(
                    t.GetMethod("StopBlocks", BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)!,
                    (Action<Action<InstalledCassetteBlockManagerState>, InstalledCassetteBlockManagerState>)process
                        .StopBlocks));
                process.hooks.Add(new Hook(
                    t.GetMethod("SetWillActivate",
                        BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)!,
                    (Action<Action<InstalledCassetteBlockManagerState, int>, InstalledCassetteBlockManagerState, int>)
                    process.SetWillActivate));
                process.hooks.Add(new Hook(
                    t.GetMethod("SilentUpdateBlocks",
                        BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly)!,
                    (Action<Action<InstalledCassetteBlockManagerState>, InstalledCassetteBlockManagerState>)process
                        .SilentUpdateBlocks));
                return process;
            }
            catch
            {
                process.Dispose();
                throw;
            }
        }

        private bool Own(OriginalCassetteBlockManagerState self) => self.Scene is ISourceEngineContextScene scene &&
                                                                    ReferenceEquals(scene.EngineContext, engine);

        private static List<SourceComponentState> Components<T>(OriginalCassetteBlockManagerState self)
            where T : SourceComponentState
        {
            var tracker = ((ISourceScene)self.Scene).SourceScene.Components;
            return tracker.GetTrackedComponents(tracker.Catalog.Find(typeof(T)).RegistryKey);
        }

        private void SetActiveIndex(Action<InstalledCassetteBlockManagerState, int> orig,
            InstalledCassetteBlockManagerState self, int index)
        {
            orig(self, index);
            if (!Own(self)) return;
            foreach (CassetteComponent c in Components<CassetteComponent>(self))
            {
                if (!c.Enabled) continue;
                c.CassetteState = c.CassetteIndex == index ? CassetteStates.On : CassetteStates.Off;
            }

            foreach (CassetteListener c in Components<CassetteListener>(self))
            {
                if (!c.Enabled) continue;
                c.DidBecomeActive?.Invoke(index);
            }
        }

        private void StopBlocks(Action<InstalledCassetteBlockManagerState> orig,
            InstalledCassetteBlockManagerState self)
        {
            orig(self);
            if (!Own(self)) return;
            foreach (CassetteComponent c in Components<CassetteComponent>(self))
            {
                if (!c.Enabled) continue;
                c.CassetteState = CassetteStates.Off;
            }

            foreach (CassetteListener c in Components<CassetteListener>(self))
            {
                if (!c.Enabled) continue;
                c.DidStop?.Invoke();
            }
        }

        private void SetWillActivate(Action<InstalledCassetteBlockManagerState, int> orig,
            InstalledCassetteBlockManagerState self, int index)
        {
            orig(self, index);
            if (!Own(self)) return;
            foreach (CassetteComponent c in Components<CassetteComponent>(self))
            {
                if (!c.Enabled) continue;
                if (c.CassetteState == CassetteStates.Off && c.CassetteIndex == index)
                    c.CassetteState = CassetteStates.Appearing;
                else if (c.CassetteState == CassetteStates.On && c.CassetteIndex != index)
                    c.CassetteState = CassetteStates.Disappearing;
            }

            int current = GravityCassetteFields.CurrentIndex(self);
            foreach (CassetteListener c in Components<CassetteListener>(self))
            {
                if (!c.Enabled) continue;
                c.WillToggle?.Invoke(index, current != index);
            }
        }

        private void SilentUpdateBlocks(Action<InstalledCassetteBlockManagerState> orig,
            InstalledCassetteBlockManagerState self)
        {
            orig(self);
            if (!Own(self)) return;
            int current = GravityCassetteFields.CurrentIndex(self);
            foreach (CassetteComponent c in Components<CassetteComponent>(self))
            {
                if (!c.Enabled) continue;
                c.TrySetActivatedSilently(current);
            }

            foreach (CassetteListener c in Components<CassetteListener>(self))
            {
                if (!c.Enabled) continue;
                c.DidBecomeActive?.Invoke(current);
            }
        }

        public void Dispose()
        {
            for (int i = hooks.Count - 1; i >= 0; i--) hooks[i].Dispose();
            hooks.Clear();
        }
    }
}
