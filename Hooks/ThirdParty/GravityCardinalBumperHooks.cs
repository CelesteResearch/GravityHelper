// Copyright (c) Shane Woolcock. Licensed under the MIT Licence.

using System;
using System.Numerics;
using System.Reflection;
using Celeste.Everest.Modules.Runtime;
using Celeste.Everest.Reflection.Runtime;
using Celeste.Mods.GravityHelper.Runtime;
using Celeste.Scene.Entities;
using Celeste.Simulation;
using MonoMod.RuntimeDetour;

namespace Celeste.Mods.GravityHelper.Hooks.ThirdParty
{
    public sealed class GravityCardinalBumperHooks : IDisposable
    {
        private readonly GravityRuntimeContext runtime;
        private readonly SourceEverestModulesProcess modules;
        private Hook unloadHook;
        private Hook launchHook;
        private MethodInfo launchMethod;
        private bool disposed;

        public GravityCardinalBumperHooks(GravityRuntimeContext runtime)
        {
            this.runtime = runtime;
            modules = ((ISourceEverestModulesProcessEnvironment)runtime.Engine).ModulesProcess;
            try
            {
                unloadHook = new Hook(
                    typeof(SourceEverestModulesProcess).GetMethod(nameof(SourceEverestModulesProcess.UnloadAssembly)),
                    (Action<Action<SourceEverestModulesProcess, EverestModuleMetadata, Assembly>,
                        SourceEverestModulesProcess,
                        EverestModuleMetadata, Assembly>)UnloadAssembly);
                modules.Events.OnRegisterModule += Registered;
                Refresh();
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        private void Registered(SourceEverestModule module) => Refresh();

        private void Refresh()
        {
            if (disposed) return;
            var type = ((ISourceRuntimeTypeDiscoveryEnvironment)runtime.Engine).RuntimeTypeDiscovery
                .ResolveRuntimeTypeAlias("Celeste.Mod.JackalHelper.Entities.CardinalBumper");
            var method = type?.GetMethod("CardinalLaunch", BindingFlags.Instance | BindingFlags.Public,
                null, [typeof(RoomPlayerSession), typeof(Vector2)], null);
            if (method == launchMethod) return;
            launchHook?.Dispose();
            launchHook = null;
            launchMethod = null;
            if (method == null) return;
            launchHook = new Hook(method,
                (Action<Action<SceneEntityState, RoomPlayerSession, Vector2>, SceneEntityState, RoomPlayerSession,
                    Vector2>)Launch);
            launchMethod = method;
        }

        private void UnloadAssembly(Action<SourceEverestModulesProcess, EverestModuleMetadata, Assembly> original,
            SourceEverestModulesProcess process, EverestModuleMetadata metadata, Assembly assembly)
        {
            if (!ReferenceEquals(process, modules))
            {
                original(process, metadata, assembly);
                return;
            }

            try
            {
                original(process, metadata, assembly);
            }
            finally
            {
                Refresh();
            }
        }

        private void Launch(Action<SceneEntityState, RoomPlayerSession, Vector2> original,
            SceneEntityState bumper, RoomPlayerSession player, Vector2 direction)
        {
            original(bumper, player, direction);
            if (!ReferenceEquals(player.EngineContext, runtime.Engine) || runtime.PlayerComponent?.ShouldInvert != true)
                return;
            var speed = player.Speed;
            speed.Y *= -1;
            player.Speed = speed;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            modules.Events.OnRegisterModule -= Registered;
            launchHook?.Dispose();
            launchHook = null;
            launchMethod = null;
            unloadHook?.Dispose();
            unloadHook = null;
        }
    }
}
