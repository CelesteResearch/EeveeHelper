using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Celeste.Core.Components;
using Celeste.Core.Components.Animation;
using Celeste.Core.Display;
using Celeste.Core.Mathematics;
using Celeste.Core.Mathematics.Easing;
using Celeste.Core.Randomness;
using Celeste.Core.Reflection.Runtime;
using Celeste.Core.Rendering;
using Celeste.Core.Scheduling;
using Celeste.Everest.Content.Extensions.Entities.Registry;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Formats.Map.Entities.Attributes;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Game.Components.Switches;
using Celeste.Game.Content.Maps.Entities;
using Celeste.Game.Content.Maps.Levels;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Execution.Processes;
using Celeste.Graphics.Animation;
using Celeste.Graphics.Content;
using Celeste.Graphics.Display.Colors;
using Celeste.Mods.EeveeHelper.Components;
using Celeste.Mods.EeveeHelper.Runtime;
using Celeste.Physics.Collision;
using Celeste.Physics.Collision.Shapes;
using Celeste.Scene.Components.Oscillation;
using Celeste.Scene.Scenes;
using Celeste.Scene.Tracking;
using Celeste.Simulation.Audio.Sources;
using Celeste.Simulation.Gameplay.Platforms.Foundation;
using Celeste.Simulation.Gameplay.Platforms.StaticMovers;
using Entity = Celeste.Scene.Entities.SceneEntityState;
using Scene = System.Object;
using Level = Celeste.Game.Execution.Levels.OriginalLevelSceneRoot;
using Platform = Celeste.Simulation.Gameplay.Platforms.Foundation.SourcePlatformEntityState;
using Solid = Celeste.Simulation.Gameplay.Platforms.Foundation.SourceSolidEntityState;
using JumpThru = Celeste.Simulation.Gameplay.Platforms.Foundation.SourceJumpThruEntityState;
using Hitbox = Celeste.Physics.Collision.Shapes.HitboxColliderGeometry;
using Color = Celeste.Core.Display.PackedColor;
using GraphicsComponent = Celeste.Graphics.Animation.SourceGraphicsComponentState;
using Tween = Celeste.Core.Components.Animation.SourceTweenState;
using Coroutine = Celeste.Core.Scheduling.SimulationCoroutine;
using Wiggler = Celeste.Scene.Components.Oscillation.WigglerComponent;
using SoundSource = Celeste.Simulation.Audio.Sources.SoundSourceComponentState;
using Switch = Celeste.Game.Components.Switches.OriginalSwitchComponent;

namespace Celeste.Mods.EeveeHelper.Entities.Containers
{
    [CustomEntity("EeveeHelper/AttachedContainer")]
    [RuntimeTypeAlias("Celeste.Mod.EeveeHelper.Entities.AttachedContainer")]
    public partial class AttachedContainer : Entity, IContainer
    {
        public EntityContainer Container => _Container;
        private ContainMode attachMode;
        private string attachFlag;
        private bool notAttachFlag;
        private string attachTo;
        private bool restrictToNode;
        private bool onlyX;
        private bool onlyY;
        private bool matchCollidable;
        private bool matchVisible;
        private bool destroyable;
        private Vector2? node;
        private Vector2? relativeAttachPos;

        public EntityContainerMover _Container;
        private SourceStaticMoverState mover;
        private bool attached;
        private Entity customAttached;
        private Vector2 lastAttachedPos;
        private Entity firstAttached = null;
        private Tuple<bool, bool> lastAttachedState = Tuple.Create(true, true);
        private Dictionary<Entity, bool> lastMatchCollidable = new();
        private Dictionary<Entity, bool> lastMatchVisible = new();
        private Dictionary<Entity, Tuple<bool, bool, bool>> lastStates = new();
        private bool legacyDestroySometimesAnyway;
        private bool updatedOnce;


        private readonly ISourceEngineContext engine;

        public static Entity Load(OriginalLevelSceneRoot level, OriginalLevelRoomData room, Vector2 offset,
            OriginalLevelEntityData data) => new AttachedContainer(level.EngineContext, data, offset);

        public AttachedContainer(ISourceEngineContext engine, EntityData data, Vector2 offset) : base(
            ((ISourceEntitySourceDataEnvironment)engine).EntitySourceDataRegistration, data.Position + offset)
        {
            this.engine = engine;
            Collider = new Hitbox(data.Width, data.Height);
            Depth = -1000011;

            attachMode = data.Enum("attachMode", ContainMode.RoomStart);
            var parsedFlag = ContainerMotion.ParseFlagAttr(data.Attr("attachFlag"));
            attachFlag = parsedFlag.Item1;
            notAttachFlag = parsedFlag.Item2;
            attachTo = data.Attr("attachTo");
            restrictToNode = data.Bool("restrictToNode");
            onlyX = data.Bool("onlyX");
            onlyY = data.Bool("onlyY");
            matchCollidable = data.Bool("matchCollidable");
            matchVisible = data.Bool("matchVisible");
            destroyable = data.Bool("destroyable");
            legacyDestroySometimesAnyway = !data.Has("destroyable");
            relativeAttachPos = OptionalVector(data, "relativeAttachX", "relativeAttachY");

            var nodes = data.NodesOffset(offset);
            if (nodes.Length > 0)
            {
                node = nodes[0] - Center;
            }

            Add(_Container = new EntityContainerMover(engine, data)
            {
                OnFit = OnFit,
                IsValid = e => !IsValid(e),
                DefaultIgnored = e => e is AttachedContainer
            });

            if (string.IsNullOrEmpty(attachTo) && attachMode == ContainMode.RoomStart)
            {
                Add(mover = new SourceStaticMoverState
                {
                    JumpThruChecker = (entity) => IsRiding((Entity)entity, true),
                    SolidChecker = (entity) => IsRiding((Entity)entity, true),
                    OnAttach = (entity) =>
                    {
                        if (attached)
                        {
                            OnAttach((Entity)entity);
                        }
                    },
                    OnMove = (amount) =>
                    {
                        if (attached)
                        {
                            OnMove(amount);
                        }
                    },
                    OnShake = (amount) =>
                    {
                        if (attached)
                        {
                            OnMoveSprites(amount);
                        }
                    },
                    OnEnable = () =>
                    {
                        if (attached)
                        {
                            OnEnable();
                        }
                    },
                    OnDisable = () =>
                    {
                        if (attached)
                        {
                            OnDisable();
                        }
                    },
                    OnDestroy = () =>
                    {
                        if (attached && (destroyable || legacyDestroySometimesAnyway))
                        {
                            RemoveSelf();
                        }
                    }
                });
            }
        }

        private static Vector2? OptionalVector(EntityData data, string keyX, string keyY)
        {
            if (!data.Has(keyX) || !data.Has(keyY)) return null;
            bool definedX = float.TryParse(data.Attr(keyX), out float x);
            bool definedY = float.TryParse(data.Attr(keyY), out float y);
            return definedX || definedY ? new Vector2(x, y) : null;
        }

        private bool CheckOutside(Entity other, Vector2 at) => !SourceEntityCollision.Check(this, other) &&
                                                               SourceEntityCollision.CheckAt(this, other, at);

        private void OnFit(Vector2 pos, float width, float height)
        {
            var lastCenter = Center;
            Position = pos;
            Collider.Width = width;
            Collider.Height = height;
            _Container.DoMoveAction(() => Center = lastCenter);
        }

        public override void Awake(object scene)
        {
            updatedOnce = false;

            attached = string.IsNullOrEmpty(attachFlag) || ((Level)Scene).Session.GetFlag(attachFlag) != notAttachFlag;

            if (attachMode == ContainMode.RoomStart)
            {
                TryAttach(true);
                if (!attached)
                {
                    customAttached = null;
                }
            }
            else if (attachMode != ContainMode.DelayedRoomStart)
            {
                if (attached)
                {
                    TryAttach();
                }
            }

            base.Awake(scene);

            if (customAttached != null)
            {
                OnAttach(customAttached);
            }
            else if (attached && mover?.Platform != null)
            {
                OnAttach((Entity)mover.Platform);
            }
        }

        public override void Update()
        {
            base.Update();

            var newAttached = string.IsNullOrEmpty(attachFlag) ||
                              ((Level)Scene).Session.GetFlag(attachFlag) != notAttachFlag;

            if (!updatedOnce)
            {
                updatedOnce = true;

                if (newAttached && attachMode == ContainMode.DelayedRoomStart)
                {
                    attached = newAttached;

                    TryAttach(true);
                }
            }

            if (attachMode != ContainMode.Always)
            {
                if (newAttached != attached)
                {
                    attached = newAttached;

                    if (attached)
                    {
                        TryAttach();
                    }
                    else
                    {
                        customAttached = null;
                    }
                }
            }
            else
            {
                var attachChanged = newAttached != attached;

                attached = newAttached;

                if (attached && customAttached == null)
                {
                    TryAttach();
                }
                else if (!attached)
                {
                    customAttached = null;
                }
            }

            if (customAttached != null)
            {
                var newPos = ContainerMotion.GetPosition(customAttached);
                if (newPos != lastAttachedPos)
                {
                    var delta = newPos - lastAttachedPos;
                    if (onlyX) delta.Y = 0f;
                    if (onlyY) delta.X = 0f;
                    _Container.DoMoveAction(() => Position += delta);
                    lastAttachedPos = newPos;
                }

                // legacy check
                if (legacyDestroySometimesAnyway && customAttached.Scene == null)
                {
                    RemoveSelf();
                    return;
                }
            }

            var attachedTo = customAttached ?? (Entity)mover?.Platform;

            if (attachedTo != null && attachedTo.Scene == null)
            {
                attachedTo = null;
                if (destroyable)
                {
                    RemoveSelf();
                    return;
                }
            }

            var currentState = attachedTo != null
                ? Tuple.Create(attachedTo.Collidable, attachedTo.Visible)
                : Tuple.Create(true, true);
            if (attachedTo != null)
            {
                foreach (var entity in _Container.GetEntities())
                {
                    if (matchCollidable && currentState.Item1 != lastAttachedState.Item1)
                    {
                        if (!currentState.Item1)
                        {
                            lastMatchCollidable[entity] = entity.Collidable;
                            entity.Collidable = false;
                        }
                        else
                        {
                            entity.Collidable = lastMatchCollidable[entity];
                            lastMatchCollidable.Remove(entity);
                        }
                    }

                    if (matchVisible && currentState.Item2 != lastAttachedState.Item2)
                    {
                        if (!currentState.Item2)
                        {
                            lastMatchVisible[entity] = entity.Visible;
                            entity.Visible = false;
                        }
                        else
                        {
                            entity.Visible = lastMatchVisible[entity];
                            lastMatchVisible.Remove(entity);
                        }
                    }
                }
            }

            lastAttachedState = currentState;
        }
    }
}
