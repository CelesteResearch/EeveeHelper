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
        public override void Update()
        {
            base.Update();

            AllowPushing = HasGravity;
            Collidable = !destroyed && _Container.Contained.Count > 0;

            if (!Collidable && holdable && Hold.IsHeld)
            {
                var speed = Hold.Holder.Speed;
                Hold.Holder.Drop();
                Speed = speed * 0.5f;
                ((ISourceRumbleEnvironment)engine).RumbleSession.Rumble((int)RumbleStrength.Medium,
                    (int)RumbleLength.Medium);
            }

            if (!destroyed)
            {
                if (_Container.Contained.Count == 0)
                {
                    return;
                }

                if (wasDestroyed)
                {
                    foreach (var entity in _Container.GetEntities())
                    {
                        if (wasCollidable.ContainsKey(entity) && wasCollidable[entity])
                        {
                            entity.Collidable = true;
                        }
                    }
                }
                else if (destroyable && TryDestroyAtBarrier())
                {
                    return;
                }

                if (tutorial)
                {
                    tutorialGui.Open = !grabbedOnce;
                }

                wasDestroyed = false;
            }
            else if (!wasDestroyed)
            {
                wasCollidable.Clear();
                foreach (var entity in _Container.GetEntities())
                {
                    wasCollidable[entity] = entity.Collidable;
                    entity.Collidable = false;
                }

                wasDestroyed = true;
            }

            if (holdable && Hold.IsHeld)
            {
                prevLiftSpeed = Vector2.Zero;
                foreach (var entity in _Container.GetEntities())
                {
                    entity.Collidable = false;
                }
            }
            else
            {
                holdTarget = Vector2.Zero;
                if (highFrictionTimer > 0f)
                {
                    highFrictionTimer -= engine.DeltaTime;
                }

                var level = (Level)Scene;
                Spring spring = null;
                if (!holdable)
                {
                    foreach (var entity in ((ISourceScene)Scene).SourceScene.Entities.Entities)
                    {
                        if (entity is Spring currentSpring && CollideCheck((ISourceEntityCollider)entity))
                        {
                            spring = currentSpring;
                        }
                    }
                }

                if (spring != null)
                {
                    HitSpring(spring);
                    spring.AnimateSpringBounce();
                }
                else if (OnGround(1))
                {
                    float target;
                    if (!OnGround(Position + Vector2.UnitX * 3f, 1))
                    {
                        target = 20f;
                    }
                    else if (!OnGround(Position - Vector2.UnitX * 3f, 1))
                    {
                        target = -20f;
                    }
                    else
                    {
                        target = 0f;
                    }

                    Speed.X = Approach(Speed.X, target, 800f * engine.DeltaTime);
                    var liftSpeed = LiftSpeed;
                    if (liftSpeed == Vector2.Zero && prevLiftSpeed != Vector2.Zero)
                    {
                        Speed = prevLiftSpeed;
                        prevLiftSpeed = Vector2.Zero;
                        Speed.Y = Math.Min(Speed.Y * 0.6f, 0f);
                        if (Speed.X != 0f && Speed.Y == 0f)
                        {
                            Speed.Y = -60f;
                        }

                        if (Speed.Y < 0f)
                        {
                            noGravityTimer = 0.15f;
                        }
                    }
                    else
                    {
                        prevLiftSpeed = liftSpeed;
                        if (liftSpeed.Y < 0f && Speed.Y < 0f)
                        {
                            Speed.Y = 0f;
                        }
                    }
                }
                else if (!holdable || Hold.ShouldHaveGravity)
                {
                    float xAccel;
                    float yAccel;
                    if (!slowFall)
                    {
                        yAccel = 800f;
                        if (Math.Abs(Speed.Y) <= 30f)
                        {
                            yAccel *= 0.5f;
                        }

                        xAccel = 350f;
                        if (Speed.Y < 0f)
                        {
                            xAccel *= 0.5f;
                        }
                    }
                    else
                    {
                        yAccel = 200f;
                        if (Speed.Y >= -30f)
                        {
                            yAccel *= 0.5f;
                        }

                        if (Speed.Y < 0f)
                        {
                            xAccel = 40f;
                        }
                        else if (highFrictionTimer <= 0f)
                        {
                            xAccel = 40f;
                        }
                        else
                        {
                            xAccel = 10f;
                        }
                    }

                    Speed.X = Approach(Speed.X, 0f, xAccel * engine.DeltaTime);
                    if (noGravityTimer > 0f)
                    {
                        noGravityTimer -= engine.DeltaTime;
                    }
                    else
                    {
                        Speed.Y = Approach(Speed.Y, slowFall ? 30f : 200f, yAccel * engine.DeltaTime);
                    }
                }

                if (!HasGravity)
                {
                    Speed.Y = 0;
                }

                MoveH(Speed.X * engine.DeltaTime, OnCollideH, null);
                MoveV(Speed.Y * engine.DeltaTime, OnCollideV, null);
                if (Left < level.Bounds.Left)
                {
                    _Container.DoMoveAction(() => Left = level.Bounds.Left);
                    Speed.X *= -0.4f;
                }
                else if (Top < (level.Bounds.Top - 4))
                {
                    _Container.DoMoveAction(() => Top = level.Bounds.Top + 4);
                    Speed.Y = 0f;
                }
                else if (Top > level.Bounds.Bottom)
                {
                    if (!respawn)
                    {
                        RemoveSelf();
                    }
                    else if (!destroyed)
                    {
                        Add(new Coroutine(engine, DestroyRoutine()));
                    }

                    return;
                }
            }

            Hold?.CheckAgainstColliders();
        }

        public override void Render(ISourceRenderContext context)
        {
            base.Render(context);
            SourcePrimitiveDrawing.Rect(context, this, Left, Top, Width, Height, PackedColor.White * 0.8f * whiteAlpha);
        }

        public override bool IsRiding(ISourceEntityCollider solid)
        {
            return HasGravity && !_Container.ContainsEntity(solid as Entity) && base.IsRiding(solid);
        }

        public override bool IsRiding(JumpThru jumpThru)
        {
            return HasGravity && !_Container.ContainsEntity(jumpThru as Entity) && base.IsRiding(jumpThru);
        }
    }
}
