// Copyright (c) Shane Woolcock. Licensed under the MIT Licence.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Celeste.Core.Scheduling;
using Celeste.Game.Chapters.Flow;
using Celeste.Game.Content.Maps;
using Celeste.Game.Entities.Postcards;
using Celeste.Game.Execution.Areas;
using Celeste.Mods.GravityHelper.Configuration;
using Celeste.Simulation.Runtime.Session;
using MonoMod.RuntimeDetour;

namespace Celeste.Mods.GravityHelper.Hooks
{
    public sealed class GravityLevelEnterHooks : IDisposable
    {
        private readonly Runtime.GravityRuntimeContext runtime;
        private readonly List<IDisposable> hooks = [];
        private bool hasVvvvvv;
        private PostcardState postcard;

        public GravityLevelEnterHooks(Runtime.GravityRuntimeContext runtime)
        {
            this.runtime = runtime;
            try
            {
                hooks.Add(new Hook(typeof(LevelEnterSequence).GetMethod(nameof(LevelEnterSequence.Go)),
                    (GoHook)Go));
                hooks.Add(new Hook(
                    typeof(LevelEnterSequence).GetMethod("Routine", BindingFlags.Instance | BindingFlags.NonPublic),
                    (Func<Func<LevelEnterSequence, IEnumerator>, LevelEnterSequence, IEnumerator>)Routine));
                hooks.Add(new Hook(typeof(LevelEnterSequence).GetMethod(nameof(LevelEnterSequence.BeforeRender)),
                    (Action<Action<LevelEnterSequence>, LevelEnterSequence>)BeforeRender));
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        private void Go(GoOriginal original,
            ChapterCompletionSession session, bool fromSaveData, IOriginalLevelEnterProcess process)
        {
            if (ReferenceEquals(process, runtime.Engine))
            {
                var areas = ((IOriginalAreaSourceTable)runtime.Engine).Areas;
                if (areas.Count > session.AreaID && areas[session.AreaID].Mode.Length > session.Mode &&
                    areas[session.AreaID].Mode[session.Mode] != null)
                    hasVvvvvv = ((OriginalChapterMapData)session.MapData).Levels.Exists(level =>
                        level.Entities.Exists(entity => entity.Name == "GravityHelper/VvvvvvGravityController"));
            }

            original(session, fromSaveData, process);
        }

        private IEnumerator Routine(Func<LevelEnterSequence, IEnumerator> original, LevelEnterSequence scene)
            => ReferenceEquals(scene.EngineContext, runtime.Engine) ? OwnedRoutine(original, scene) : original(scene);

        private IEnumerator OwnedRoutine(Func<LevelEnterSequence, IEnumerator> original, LevelEnterSequence scene)
        {
            var settings = runtime.Settings;
            if (hasVvvvvv && (settings.VvvvvvMode != VvvvvvSetting.Default ||
                              settings.VvvvvvAllowDashing != VvvvvvSetting.Default ||
                              settings.VvvvvvAllowGrabbing != VvvvvvSetting.Default ||
                              settings.VvvvvvFlipSound != VvvvvvSetting.Default))
            {
                settings.VvvvvvMode = VvvvvvSetting.Default;
                settings.VvvvvvAllowDashing = VvvvvvSetting.Default;
                settings.VvvvvvAllowGrabbing = VvvvvvSetting.Default;
                settings.VvvvvvFlipSound = VvvvvvSetting.Default;
                postcard = new PostcardState(runtime.Engine,
                    ((IOriginalLevelEnterProcess)runtime.Engine).Text.Dialog.Clean("GRAVITYHELPER_POSTCARD_VVVVVV"),
                    "event:/ui/main/postcard_csides_in", "event:/ui/main/postcard_csides_out");
                scene.SourceScene.QueueSourceEntity(postcard);
                yield return postcard.DisplayRoutine();
                postcard = null;
            }

            hasVvvvvv = false;
            yield return new SourceSwapImmediately(original(scene));
        }

        private void BeforeRender(Action<LevelEnterSequence> original, LevelEnterSequence scene)
        {
            original(scene);
            if (ReferenceEquals(scene.EngineContext, runtime.Engine)) postcard?.BeforeRender();
        }

        public void Dispose()
        {
            for (int index = 0; index < hooks.Count; index++) hooks[index].Dispose();
            hooks.Clear();
        }

        private delegate void GoOriginal(ChapterCompletionSession session, bool fromSaveData,
            IOriginalLevelEnterProcess process);

        private delegate void GoHook(GoOriginal original, ChapterCompletionSession session, bool fromSaveData,
            IOriginalLevelEnterProcess process);
    }
}
