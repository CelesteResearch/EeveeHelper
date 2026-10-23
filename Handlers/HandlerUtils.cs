using System.Numerics;
using Celeste.Core.Components;
using Celeste.Core.Scheduling;
using Celeste.Mods.EeveeHelper.Runtime;
using Celeste.Mods.EeveeHelper.Entities.Containers;
using Celeste.Scene.Tracking;
using Entity = Celeste.Scene.Entities.SceneEntityState;
using EntityData = Celeste.Formats.Map.Entities.Structure.EntityData;
using Rectangle = Celeste.Core.Geometry.IntRect;
using Level = Celeste.Game.Execution.Levels.OriginalLevelSceneRoot;
using Platform = Celeste.Simulation.Gameplay.Platforms.Foundation.SourcePlatformEntityState;
using Player = Celeste.Simulation.RoomPlayerSession;
using SolidTiles = Celeste.Game.Execution.LevelLoader.Terrain.OriginalSolidTiles;
using BackgroundTiles = Celeste.Game.Execution.LevelLoader.Terrain.OriginalBackgroundTiles;
using Decal = Celeste.Simulation.Presentation.Decals.Entities.SourceDecalEntityState;
using Trigger = Celeste.Simulation.Gameplay.Triggers.Foundation.SourceTriggerEntityState;
using WindController = Celeste.Game.Entities.Environment.Wind.OriginalWindController;
using Follower = Celeste.Scene.Following.FollowerState;
using TransitionListener = Celeste.Simulation.RoomTransitionListener;
using Celeste.Mods.EeveeHelper.Handlers;
using System;

namespace Celeste.Mods.EeveeHelper.Handlers
{
    public static class HandlerUtils
    {
        public static bool DoAs<T>(IEntityHandler handler, Action<T> action, Action<Entity> or = null)
        {
            if (handler is T castedHandler)
            {
                action(castedHandler);
                return true;
            }
            else if (or != null && handler.Entity != null && handler.Container != null &&
                     handler.Container.IsFirstHandler(handler) && !handler.Container.HasHandlerFor<T>(handler.Entity))
            {
                or(handler.Entity);
            }

            return false;
        }

        public static R GetAs<T, R>(IEntityHandler handler, Func<T, R> func, Func<Entity, R> or = null)
        {
            if (handler is T castedHandler)
            {
                return func(castedHandler);
            }
            else if (or != null && handler.Entity != null && handler.Container != null &&
                     handler.Container.IsFirstHandler(handler) && !handler.Container.HasHandlerFor<T>(handler.Entity))
            {
                return or(handler.Entity);
            }

            return default;
        }
    }
}
