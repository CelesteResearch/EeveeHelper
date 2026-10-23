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
using System.Collections.Generic;
using System.Linq;

namespace Celeste.Mods.EeveeHelper.Components
{
    public class EntityContainerMover : EntityContainer
    {
        private static readonly HashSet<string> DefaultWhitelistedAnchors =
            ["anchor", "anchorPosition", "start", "startPosition"];


        private static readonly HashSet<string> DefaultBlacklistedAnchors =
        [
            "Position", "ExactPosition", "TopLeft", "TopCenter", "TopRight", "Center", "CenterLeft", "CenterRight",
            "BottomLeft", "BottomCenter", "BottomRight"
        ];


        public Vector4 Padding;

        public bool FitContained;
        public bool IgnoreAnchors;
        public Action<Vector2, float, float> OnFit;
        public Action OnPreMove;
        public Action OnPostMove;

        public EntityContainerMover(ISourceEngineContext engine) : base(engine)
        {
        }

        public EntityContainerMover(ISourceEngineContext engine, EntityData data, bool fitContained = true) : base(
            engine, data)
        {
            if (fitContained)
            {
                FitContained = data.Bool("fitContained");
            }

            IgnoreAnchors = data.Bool("ignoreAnchors");
        }

        public override void Added(object owner)
        {
            base.Added(owner);
            var entity = (Entity)owner;
            entity.Add(new TransitionListener
            {
                OnOutBegin = () =>
                {
                    if (!Entity.TagCheck(((Celeste.Game.Execution.Startup.IOriginalGameTagsEnvironment)Engine).Tags
                            .Persistent.Value))
                    {
                        Contained.RemoveAll(h =>
                            h.Entity != null &&
                            h.Entity.TagCheck(((Celeste.Game.Execution.Startup.IOriginalGameTagsEnvironment)Engine).Tags
                                .Persistent.Value));
                    }
                }
            });
        }

        public override void EntityAwake()
        {
            base.EntityAwake();

            if (!Attached)
            {
                Padding = new Vector4(Entity.Width / 2f, Entity.Height / 2f, Entity.Width / 2f, Entity.Height / 2f);
            }
        }

        public override void EntityRemoved(object scene)
        {
            base.EntityRemoved(scene);
            DestroyContained();
        }

        public override void Update()
        {
            if (Attached && FitContained)
            {
                var bounds = GetContainedBounds();
                var targetPos = new Vector2(bounds.X + Padding.X, bounds.Y + Padding.Y);
                var targetCorner = new Vector2(bounds.X + bounds.Width + Padding.Z,
                    bounds.Y + bounds.Height + Padding.W);
                var targetWidth = targetCorner.X - targetPos.X;
                var targetHeight = targetCorner.Y - targetPos.Y;

                if (Entity.TopLeft != targetPos || Entity.BottomRight != targetCorner)
                {
                    if (OnFit != null)
                    {
                        OnFit(targetPos, targetWidth, targetHeight);
                    }
                    else
                    {
                        Entity.Position = targetPos;
                        Entity.Collider.Width = targetWidth;
                        Entity.Collider.Height = targetHeight;
                    }
                }
            }

            base.Update();
        }

        protected override void AddContained(IEntityHandler handler)
        {
            base.AddContained(handler);

            if (IgnoreAnchors)
            {
                return;
            }

            if (handler is not IAnchorProvider)
            {
                var data = EeveeEntityData.For(Engine, handler.Entity);
                if (!data.TryGet<List<string>>("entityContainerAnchors", out _))
                {
                    var anchors = FindAnchors(Engine, handler.Entity);
                    if (anchors.Count > 0)
                    {
                        data.Set("entityContainerAnchors", anchors);
                    }
                }
            }
        }

        protected override void AttachInside(bool first = false)
        {
            base.AttachInside(first);

            var bounds = GetContainedBounds();
            Padding = new Vector4(Entity.Left - bounds.X, Entity.Top - bounds.Y,
                Entity.Right - (bounds.X + bounds.Width), Entity.Bottom - (bounds.Y + bounds.Height));
        }

        protected override void DetachOutside()
        {
            base.DetachOutside();

            var bounds = GetContainedBounds();
            Padding = new Vector4(Entity.Left - bounds.X, Entity.Top - bounds.Y,
                Entity.Right - (bounds.X + bounds.Width), Entity.Bottom - (bounds.Y + bounds.Height));
        }

        protected override void DetachAll()
        {
            base.DetachAll();

            Padding = new Vector4(Entity.Width / 2f, Entity.Height / 2f, Entity.Width / 2f, Entity.Height / 2f);
        }

        public void DoMoveAction(Action moveAction, Func<Entity, Vector2, Vector2?> liftSpeedGetter = null,
            bool liftSpeedFix = false)
        {
            Cleanup();
            var anchorOffsets = new Dictionary<Entity, Dictionary<string, Vector2>>();
            var collidable = new Dictionary<Entity, bool>();
            var startPosition = ContainerMotion.GetPosition(Entity);
            foreach (var handler in Contained)
            {
                if (handler is IMoveable moveable)
                {
                    moveable.PreMove();
                }

                if (!collidable.ContainsKey(handler.Entity))
                {
                    collidable.Add(handler.Entity, handler.Entity.Collidable);
                    handler.Entity.Collidable = false;
                }
            }

            moveAction();
            var selfCollidable = Entity.Collidable;
            Entity.Collidable = false;
            OnPreMove?.Invoke();
            var newPosition = ContainerMotion.GetPosition(Entity);
            var moveOffset = newPosition - startPosition;
            var toMove = GetEntities();
            foreach (var handler in Contained)
            {
                if (collidable.ContainsKey(handler.Entity))
                {
                    handler.Entity.Collidable = collidable[handler.Entity];
                    collidable.Remove(handler.Entity);
                }

                if (!IgnoreAnchors)
                {
                    var anchors = EeveeAnchorLookup.GetAnchors(this, handler);
                    var data = EeveeEntityData.For(Engine, handler.Entity);
                    foreach (var anchor in anchors)
                    {
                        data.Set(anchor, data.Get<Vector2>(anchor) + moveOffset);
                    }
                }

                if (handler is IMoveable moveable)
                {
                    var liftSpeed = liftSpeedGetter?.Invoke(handler.Entity, moveOffset);
                    if (moveable.Move(moveOffset, liftSpeed) && toMove.Contains(handler.Entity))
                    {
                        toMove.Remove(handler.Entity);
                    }
                }
            }

            if (liftSpeedFix)
            {
                Runtime.LiftSpeedFix = true;
            }

            Runtime.DecalStaticMoverFix = true;
            foreach (var entity in toMove)
            {
                if (entity is Platform platform)
                {
                    var liftSpeed = liftSpeedGetter?.Invoke(entity, moveOffset);
                    if (liftSpeed != null)
                    {
                        platform.MoveH(moveOffset.X, liftSpeed.Value.X);
                        platform.MoveV(moveOffset.Y, liftSpeed.Value.Y);
                    }
                    else
                    {
                        platform.MoveH(moveOffset.X);
                        platform.MoveV(moveOffset.Y);
                    }
                }
                else
                {
                    entity.Position += moveOffset;
                }
            }

            Runtime.DecalStaticMoverFix = false;
            if (liftSpeedFix)
            {
                Runtime.LiftSpeedFix = false;
            }

            Entity.Collidable = selfCollidable;
            OnPostMove?.Invoke();
        }

        public void DoIgnoreCollision(Action action)
        {
            var lastCollidable = new Dictionary<Entity, bool>();
            foreach (var entity in GetEntities())
            {
                lastCollidable.Add(entity, entity.Collidable);
                entity.Collidable = false;
            }

            action();
            foreach (var pair in lastCollidable)
            {
                pair.Key.Collidable = pair.Value;
            }
        }

        public List<string> GetAnchors(IEntityHandler handler)
        {
            return HandlerUtils.GetAs<IAnchorProvider, List<string>>(handler, (provider) => provider.GetAnchors(),
                (entity) =>
                {
                    var data = EeveeEntityData.For(Engine, handler.Entity);
                    var anchorNames = data.Get<List<string>>("entityContainerAnchors");
                    if (anchorNames != null)
                    {
                        return anchorNames;
                    }

                    return null;
                }) ?? new List<string>();
        }

        private static HashSet<string> GetAnchorFields(Dictionary<Type, (bool, HashSet<string>)> anchorFieldDict,
            Type target)
        {
            var addedAnchorFields = false;
            var result = new HashSet<string>();
            foreach (var (fieldHolder, (includeDerived, fields)) in anchorFieldDict)
            {
                if (includeDerived
                        ? !fieldHolder.IsAssignableFrom(target)
                        : fieldHolder != target)
                    continue;

                result.UnionWith(fields);
                addedAnchorFields = true;
            }

            return addedAnchorFields ? result : null;
        }

        public static List<string> FindAnchors(ISourceEngineContext engine, Entity entity)
        {
            var result = new List<string>();
            var data = EeveeEntityData.For(engine, entity);

            var type = entity.GetType();
            var whitelistedAnchorsForType = GetAnchorFields(EeveeContainerRuntime.For(engine).WhitelistedAnchors, type);
            var blacklistedAnchorsForType = GetAnchorFields(EeveeContainerRuntime.For(engine).BlacklistedAnchors, type);

            foreach (var (field, value) in data)
            {
                if (value is not Vector2 vector)
                    continue;

                var whitelisted = whitelistedAnchorsForType?.Contains(field)
                                  ?? DefaultWhitelistedAnchors.Contains(field) ||
                                  vector == ContainerMotion.GetPosition(entity);
                var blacklisted = blacklistedAnchorsForType?.Contains(field)
                                  ?? DefaultBlacklistedAnchors.Contains(field);
                if (whitelisted && !blacklisted)
                    result.Add(field);
            }

            return result;
        }

        /// <summary>
        /// Registers whitelisted anchor members for a type (and for all its derived types if <paramref name="includeDerived"/> is <c>true</c>),
        /// which marks these members as "anchors", i.e. EeveeHelper will update them when an entity of this type is moved in a container.<br/>
        /// Once a type has been registered in this whitelist, EeveeHelper will use that instead of the automatic whitelist, which means only
        /// the members present here will be used as anchors. <br/>
        /// Passing an empty <see cref="HashSet{T}"/> to <paramref name="anchors"/> will exempt this type from the automatic whitelist, meaning no
        /// members will be used as anchors for this type.
        /// </summary>
        /// <param name="type">The type to register the whitelisted anchors for</param>
        /// <param name="includeDerived">Whether to whitelist these members for any derived types of the registered type as well as the registered type itself</param>
        /// <param name="anchors">The names of all members of this type and any base types to register</param>
        public static void RegisterWhitelistedAnchors(ISourceEngineContext engine, Type type, bool includeDerived,
            HashSet<string> anchors)
        {
            if (EeveeContainerRuntime.For(engine).WhitelistedAnchors.TryGetValue(type, out var data) &&
                data.Item1 == includeDerived)
                data.Item2.UnionWith(anchors);
            else
                EeveeContainerRuntime.For(engine).WhitelistedAnchors.Add(type, (includeDerived, anchors));
        }

        /// <summary>
        /// Registers blacklisted anchor members for a type (and for all its derived types if <paramref name="includeDerived"/> is <c>true</c>),
        /// which marks these members as never to be used as "anchors", i.e. EeveeHelper will not update them when an entity of this type is
        /// moved in a container.<br/>
        /// Once a type has been registered in this blacklist, EeveeHelper will use that instead of the automatic blacklist, which means its
        /// `Position`, `ExactPosition`, `Center`, etc. members will be able to be used as anchors if not present here.<br/>
        /// Passing an empty <see cref="HashSet{T}"/> to <paramref name="anchors"/> will exempt this type from the automatic blacklist.
        /// </summary>
        /// <param name="type">The type to register the blacklisted anchors for</param>
        /// <param name="includeDerived">Whether to blacklist these members for any derived types of the registered type as well as the registered type itself</param>
        /// <param name="anchors">The names of all members of this type and any base types to register</param>
        public static void RegisterBlacklistedAnchors(ISourceEngineContext engine, Type type, bool includeDerived,
            HashSet<string> anchors)
        {
            if (EeveeContainerRuntime.For(engine).BlacklistedAnchors.TryGetValue(type, out var data) &&
                data.Item1 == includeDerived)
                data.Item2.UnionWith(anchors);
            else
                EeveeContainerRuntime.For(engine).BlacklistedAnchors.Add(type, (includeDerived, anchors));
        }

        public static void AddEntityHandler(ISourceEngineContext engine, Type entityType, Type handlerType)
        {
            foreach (var type in EeveeContainerRuntime.For(engine).GetRuntimeTypes())
            {
                if (entityType.IsAssignableFrom(type))
                {
                    Console.WriteLine($"Registering handler {handlerType.Name} [{type.Name} : {entityType.Name}]");
                    if (!EeveeContainerRuntime.For(engine).LegacyEntityHandlers.ContainsKey(type))
                    {
                        EeveeContainerRuntime.For(engine).LegacyEntityHandlers.Add(type, new List<Type>());
                    }

                    EeveeContainerRuntime.For(engine).LegacyEntityHandlers[type].Add(handlerType);
                }
            }
        }

        public static void AddEntityHandler<H>(ISourceEngineContext engine, Type entityType) where H : EntityHandler
        {
            AddEntityHandler(engine, entityType, typeof(H));
        }

        public static void AddEntityHandler<E, H>(ISourceEngineContext engine) where E : Entity where H : EntityHandler
        {
            AddEntityHandler(engine, typeof(E), typeof(H));
        }
    }
}
