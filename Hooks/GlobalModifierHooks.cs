using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using Celeste.Game.Content.Maps.Entities;
using Celeste.Core.Scheduling;
using Celeste.Game.Execution.Levels;
using Celeste.Scene.Scheduling;
using Celeste.Simulation.Gameplay.Player.Intro;
using MonoMod.RuntimeDetour;

namespace Celeste.Mods.EeveeHelper.Hooks
{
    public sealed partial class GlobalModifierHooks : IDisposable
    {
        private static readonly ConditionalWeakTable<ISourceEngineContext, HashSet<OriginalLevelEntityData>>
            modifiedSets = new();

        private readonly HashSet<OriginalLevelEntityData> modified;
        private readonly ISourceEngineContext engine;
        private readonly List<IDisposable> hooks = [];
        private bool fromLoader;
        private int loadingTags = -1;
        private int firstNewEntity;

        public GlobalModifierHooks(ISourceEngineContext engine)
        {
            this.engine = engine;
            modified = modifiedSets.GetValue(engine, static _ => new());
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            try
            {
                hooks.Add(new Hook(typeof(OriginalLevelSceneRoot).GetMethod(nameof(OriginalLevelSceneRoot.LoadLevel)),
                    (LoadHook)LoadLevel));
                hooks.Add(new ILHook(typeof(OriginalLevelSceneRoot).GetMethod("LoadSourceRoomMapPhases", flags),
                    LoadMapPhases));
                hooks.Add(new Hook(
                    typeof(SceneUpdateScheduler).GetMethod(nameof(SceneUpdateScheduler.CommitQueuedEntityLists)),
                    (CommitHook)Commit));
                hooks.Add(new Hook(typeof(SceneUpdateScheduler).GetMethod("DispatchEntityAdded", flags),
                    (LifecycleHook)AddedLifecycle));
                hooks.Add(new Hook(typeof(SceneUpdateScheduler).GetMethod("DispatchEntityAwake", flags),
                    (LifecycleHook)AwakeLifecycle));
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        private bool Owns(OriginalLevelSceneRoot level) => ReferenceEquals(level.EngineContext, engine);

        private void LoadLevel(LoadOriginal original, OriginalLevelSceneRoot level, PlayerIntroType intro,
            bool isFromLoader)
        {
            if (!Owns(level))
            {
                original(level, intro, isFromLoader);
                return;
            }

            bool previous = fromLoader;
            int previousTags = loadingTags, previousCount = firstNewEntity;
            fromLoader = isFromLoader;
            loadingTags = -1;
            try
            {
                original(level, intro, isFromLoader);
            }
            finally
            {
                fromLoader = previous;
                loadingTags = previousTags;
                firstNewEntity = previousCount;
            }
        }

        public void Dispose()
        {
            for (int i = hooks.Count - 1; i >= 0; i--) hooks[i].Dispose();
            hooks.Clear();
        }

        private delegate void LoadOriginal(OriginalLevelSceneRoot level, PlayerIntroType intro, bool isFromLoader);

        private delegate void LoadHook(LoadOriginal original, OriginalLevelSceneRoot level, PlayerIntroType intro,
            bool isFromLoader);

        private delegate void CommitOriginal(SceneUpdateScheduler scheduler);

        private delegate void CommitHook(CommitOriginal original, SceneUpdateScheduler scheduler);

        private delegate void LifecycleOriginal(SceneUpdateScheduler scheduler, object entity);

        private delegate void LifecycleHook(LifecycleOriginal original, SceneUpdateScheduler scheduler, object entity);
    }
}
