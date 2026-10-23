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
    public partial class AttachedContainer
    {
        private bool TryAttach(bool first = false)
        {
            if (mover == null)
            {
                if (first || (attachMode != ContainMode.RoomStart && attachMode != ContainMode.DelayedRoomStart))
                {
                    var closestDist = 0f;
                    Entity closest = null;
                    foreach (Entity entity in ((ISourceScene)Scene).SourceScene.Entities.Entities)
                    {
                        if (string.IsNullOrEmpty(attachTo)
                                ? (entity is JumpThru || entity is Solid)
                                : CheckTypeName(entity, attachTo))
                        {
                            if (node != null && entity.Collider != null && entity.Collider.Collide(Center + node.Value))
                            {
                                closest = entity;
                                break;
                            }

                            if (node == null || !restrictToNode)
                            {
                                if (!string.IsNullOrEmpty(attachTo))
                                {
                                    var dist = ((ISourceVectorEnvironment)engine).VectorArithmetic.Distance(
                                        node != null ? (Center + node.Value) : Center, entity.Center);
                                    if (closest == null || dist < closestDist)
                                    {
                                        closestDist = dist;
                                        closest = entity;
                                    }
                                }
                                else if (IsRiding(entity, false))
                                {
                                    closest = entity;
                                }
                            }
                        }
                    }

                    if (closest != null)
                    {
                        customAttached = closest;

                        // Only run OnAttach here if we're attaching after Awake
                        // otherwise the order of operations means the container may move
                        // before it contains entities
                        if (updatedOnce)
                        {
                            OnAttach(customAttached);
                        }

                        lastAttachedPos = ContainerMotion.GetPosition(customAttached);

                        return true;
                    }
                }
                else
                {
                    customAttached = firstAttached;
                    if (customAttached != null)
                    {
                        if (updatedOnce)
                        {
                            OnAttach(customAttached);
                        }

                        lastAttachedPos = ContainerMotion.GetPosition(customAttached);
                    }

                    return true;
                }
            }
            else if (mover.Platform != null && updatedOnce)
            {
                OnAttach((Entity)mover.Platform);
            }

            return false;
        }

        private bool CheckTypeName(Entity entity, string typeName)
        {
            var type = entity.GetType();

            return EeveeContainerRuntime.For(engine).GetTypeName(type) == typeName
                   || ((entity.SourceData as EntityData)?.Name is { } sourceSid
                       ? sourceSid == typeName
                       : ((IEverestEntityRegistryEnvironment)engine).EntityRegistry.GetKnownSidsFromType(type)
                       .Contains(typeName));
        }

        private bool IsValid(Entity entity)
        {
            if (mover != null)
            {
                return entity == mover.Platform;
            }
            else
            {
                return entity == customAttached;
            }
        }

        private bool IsRiding(Entity entity, bool checkNode)
        {
            if (checkNode && node != null)
            {
                return entity.Collider != null && entity.Collider.Collide(Center + node.Value);
            }
            else if (entity is JumpThru)
            {
                return CheckOutside(entity, Position + Vector2.UnitY);
            }
            else
            {
                return CheckOutside(entity, Position + Vector2.UnitY)
                       || CheckOutside(entity, Position - Vector2.UnitY)
                       || CheckOutside(entity, Position + Vector2.UnitX)
                       || CheckOutside(entity, Position - Vector2.UnitX);
            }
        }

        private void OnAttach(Entity entity)
        {
            if (!relativeAttachPos.HasValue)
            {
                return;
            }

            var newPos = Position;
            var attachedPos = ContainerMotion.GetPosition(entity);

            if (!onlyY)
            {
                newPos.X = attachedPos.X + relativeAttachPos.Value.X;
            }

            if (!onlyX)
            {
                newPos.Y = attachedPos.Y + relativeAttachPos.Value.Y;
            }

            if (Position != newPos)
            {
                _Container.DoMoveAction(() => Position = newPos,
                    (h, move) => entity is Platform platform ? platform.LiftSpeed : Vector2.Zero);
            }
        }

        private void OnMove(Vector2 amount)
        {
            if (onlyX) amount.Y = 0f;
            if (onlyY) amount.X = 0f;

            _Container.DoMoveAction(() => Position += amount, (h, move) => mover.Platform.LiftSpeed);
        }

        private void OnMoveSprites(Vector2 amount)
        {
            if (onlyX) amount.Y = 0f;
            if (onlyY) amount.X = 0f;

            foreach (var ieH in _Container.Contained)
            {
                foreach (var gc in ieH.Entity.Components.OfType<GraphicsComponent>())
                {
                    if (gc != null)
                    {
                        gc.RenderPosition += amount;
                    }
                }
            }
        }

        private void OnEnable()
        {
            Active = Visible = Collidable = true;
            foreach (var entity in _Container.GetEntities())
            {
                var state = lastStates[entity];
                entity.Active = state.Item1;
                entity.Visible = state.Item2;
                entity.Collidable = state.Item3;
            }

            lastStates.Clear();
        }

        private void OnDisable()
        {
            Active = Visible = Collidable = false;
            foreach (var entity in _Container.GetEntities())
            {
                lastStates.Add(entity, Tuple.Create(entity.Active, entity.Visible, entity.Collidable));
                entity.Active = entity.Visible = entity.Collidable = false;
            }
        }
    }
}
