using System;
using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using Celeste.Core.Components;
using Celeste.Core.Components.Animation;
using Celeste.Core.Display;
using Celeste.Core.Mathematics;
using Celeste.Core.Mathematics.Extrema;
using Celeste.Core.Reflection.Runtime;
using Celeste.Core.Rendering;
using Celeste.Core.Scheduling;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Formats.Save.Xml;
using Celeste.Game.Content.Maps.Entities;
using Celeste.Game.Content.Maps.Levels;
using Celeste.Game.Controls.Source;
using Celeste.Game.Execution.Processes;
using Celeste.Game.Execution.Startup;
using Celeste.Game.Gameplay.Mechanisms.DashSwitches;
using Celeste.Graphics.Rendering;
using Celeste.Input.Gameplay.Feedback;
using Celeste.Mods.EeveeHelper.Components;
using Celeste.Physics.Collision;
using Celeste.Scene.Entities;
using Celeste.Scene.Tracking;
using Celeste.Scene.Tracking.Attributes;
using Celeste.Simulation.Gameplay.Holdables.TheoCrystal;
using Celeste.Simulation.Gameplay.Interaction.Springs;
using Celeste.Simulation.Gameplay.Platforms.Foundation;
using Celeste.Simulation.Gameplay.Enemies.Seeker;
using Celeste.Simulation.Presentation.Hud.Tutorials.Birds;
using Actor = Celeste.Simulation.Gameplay.Actors.Foundation.SourceActorEntityState;
using BirdTutorialGui = Celeste.Simulation.Presentation.Hud.Tutorials.Birds.BirdTutorialGuiState;
using CollisionData = Celeste.Physics.Collision.PixelCollision;
using Coroutine = Celeste.Core.Scheduling.SimulationCoroutine;
using Entity = Celeste.Scene.Entities.SceneEntityState;
using Hitbox = Celeste.Physics.Collision.Shapes.HitboxColliderGeometry;
using Holdable = Celeste.Simulation.Gameplay.Holdables.HoldableState;
using JumpThru = Celeste.Physics.Collision.Riding.ISourceJumpThruCollider;
using Level = Celeste.Game.Execution.Levels.OriginalLevelSceneRoot;
using Spring = Celeste.Game.Gameplay.Interaction.Springs.OriginalSpringState;
using TransitionListener = Celeste.Simulation.RoomTransitionListener;
using Tween = Celeste.Core.Components.Animation.SourceTweenState;

namespace Celeste.Mods.EeveeHelper.Entities.Containers
{
    public partial class HoldableContainer
    {
        private void OnPickup()
        {
            grabbedOnce = true;
            Speed = Vector2.Zero;
            AddTag(((IOriginalGameTagsEnvironment)engine).Tags.Persistent.Value);
            wasCollidable.Clear();
            wasPersistent.Clear();
            foreach (var entity in _Container.GetEntities())
            {
                wasCollidable.Add(entity, entity.Collidable);
                var persistent = (entity.Tags.Bits & ((IOriginalGameTagsEnvironment)engine).Tags.Persistent.Value) != 0;
                wasPersistent.Add(entity, persistent);
                if (!persistent)
                {
                    entity.Tags |=
                        new Celeste.Scene.Scheduling.SceneTagSet(((IOriginalGameTagsEnvironment)engine).Tags.Persistent
                            .Value);
                }

                entity.Collidable = false;
            }

            highFrictionTimer = 0.5f;
        }

        private void OnRelease(Vector2 force)
        {
            if (!destroyed && SolidAt())
            {
                ReleasedSquishWiggle();
                force = Vector2.Zero;
            }

            RemoveTag(((IOriginalGameTagsEnvironment)engine).Tags.Persistent.Value);
            if (!destroyed)
            {
                foreach (var entity in _Container.GetEntities())
                {
                    if (!(wasPersistent.ContainsKey(entity) && wasPersistent[entity]))
                    {
                        entity.Tags &=
                            ~new Celeste.Scene.Scheduling.SceneTagSet(((IOriginalGameTagsEnvironment)engine).Tags
                                .Persistent.Value);
                    }

                    entity.Collidable = !wasCollidable.ContainsKey(entity) || wasCollidable[entity];
                }
            }

            if (slowFall)
            {
                force.Y *= 0.5f;
            }

            if (force.X != 0f && force.Y == 0f)
            {
                force.Y = -0.4f;
            }

            Speed = force * (slowFall ? 100f : 200f);
            if (Speed != Vector2.Zero)
            {
                noGravityTimer = 0.1f;
            }
        }

        private void OnCarry(Vector2 target)
        {
            holdTarget = target;
            _Container.DoMoveAction(() => Position = target);
        }

        private void OnSquishNew(CollisionData data)
        {
            var wiggled = false;
            _Container.DoMoveAction(() => wiggled = TryBigSquishWiggle(data));
            return;
        }

        private bool TryBigSquishWiggle(CollisionData data)
        {
            ((ISourceMutableEntityState)data.Pusher).Collidable = true;
            for (var i = 0; i <= Math.Max(3, (int)Width / 2); i++)
            {
                for (var j = 0; j <= Math.Max(3, (int)Height / 2); j++)
                {
                    if (i != 0 || j != 0)
                    {
                        for (var k = 1; k >= -1; k -= 2)
                        {
                            for (var l = 1; l >= -1; l -= 2)
                            {
                                var value = new Vector2(i * k, j * l);
                                if (!SolidAt(Position + value))
                                {
                                    Position += value;
                                    ((ISourceMutableEntityState)data.Pusher).Collidable = false;
                                    return true;
                                }
                            }
                        }
                    }
                }
            }

            ((ISourceMutableEntityState)data.Pusher).Collidable = false;
            return false;
        }

        private void ReleasedSquishWiggle()
        {
            _Container.DoMoveAction(() =>
            {
                for (var i = 0; i <= Math.Max(3, (int)Width); i++)
                {
                    for (var j = 0; j <= Math.Max(3, (int)Height); j++)
                    {
                        if (i != 0 || j != 0)
                        {
                            for (var k = 1; k >= -1; k -= 2)
                            {
                                for (var l = 1; l >= -1; l -= 2)
                                {
                                    var value = new Vector2(i * k, j * l);
                                    if (!SolidAt(Position + value))
                                    {
                                        Position += value;
                                        return;
                                    }
                                }
                            }
                        }
                    }
                }
            });
        }

        private void OnFit(Vector2 pos, float width, float height)
        {
            Position = new Vector2(pos.X + width / 2f, pos.Y + height);
            Collider.Position = new Vector2(-width / 2f, -height);
            Collider.Width = width;
            Collider.Height = height;
            if (holdable)
            {
                Hold.PickupCollider.Position = Collider.Position - new Vector2(4f, 4f);
                Hold.PickupCollider.Width = Collider.Width + 8f;
                Hold.PickupCollider.Height = Collider.Height + 8f;

                if (Hold.IsHeld)
                {
                    _Container.DoMoveAction(() => Position = holdTarget);
                }
            }
        }
    }
}
