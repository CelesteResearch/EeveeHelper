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
using Celeste.Mods.EeveeHelper.Components;
using System;
using System.Collections.Generic;

namespace Celeste.Mods.EeveeHelper.Handlers
{
    public class EntityHandler : IEntityHandler
    {
        public static List<IEntityHandler> CreateAll(Entity entity, EntityContainer container,
            bool forceDefault = false) => EeveeContainerRuntime.For(container.Engine).Handlers
            .CreateAll(entity, container, forceDefault);

        public static bool DefaultInsideCheck(Entity entity, EntityContainer container) =>
            container.CheckCollision(entity); // Impl

        public Entity Entity { get; }
        public EntityContainer Container { get; set; }

        public EntityHandler(Entity entity)
        {
            Entity = entity;
        }

        public virtual void OnAttach(EntityContainer container)
        {
            Container = container;
        }

        public virtual void OnDetach(EntityContainer container)
        {
            Container = null;
        }

        public virtual bool IsInside(EntityContainer container)
        {
            return container.CheckCollision(Entity);
        }

        public virtual Rectangle GetBounds()
        {
            return new Rectangle((int)Entity.Left, (int)Entity.Top, (int)Entity.Width, (int)Entity.Height);
        }

        public virtual void Destroy()
        {
            if (Entity is Platform platform)
            {
                platform.DestroyStaticMovers();
            }

            var follower = Entity.Get<Follower>();
            if (follower != null && follower.HasLeader)
            {
                follower.Leader.LoseFollower(follower);
            }

            Entity.RemoveSelf();
        }
    }
}
