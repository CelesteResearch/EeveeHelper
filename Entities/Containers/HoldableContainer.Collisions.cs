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
        private void OnCollideH(CollisionData data)
        {
            if (data.Hit is OriginalDashSwitchState)
            {
                (data.Hit as OriginalDashSwitchState).OnDashCollide(null, Vector2.UnitX * Math.Sign(Speed.X));
            }

            Speed.X *= -0.4f;
        }

        private void OnCollideV(CollisionData data)
        {
            if (data.Hit is OriginalDashSwitchState)
            {
                (data.Hit as OriginalDashSwitchState).OnDashCollide(null, Vector2.UnitY * Math.Sign(Speed.Y));
            }

            Speed.Y = 0f;
        }

        public bool HitSpring(Spring spring)
        {
            if (!holdable || !Hold.IsHeld)
            {
                if (spring.Orientation == SpringOrientation.Floor && Speed.Y >= 0f)
                {
                    Speed.X *= 0.5f;
                    Speed.Y = -160f;
                    noGravityTimer = 0.15f;
                    return true;
                }

                if (spring.Orientation == SpringOrientation.WallLeft && Speed.X <= 0f)
                {
                    MoveTowardsY(spring.CenterY + 5f, 4f, null);
                    Speed.X = slowFall ? 160f : 220f;
                    Speed.Y = -80f;
                    noGravityTimer = 0.1f;
                    return true;
                }

                if (spring.Orientation == SpringOrientation.WallRight && Speed.X >= 0f)
                {
                    MoveTowardsY(spring.CenterY + 5f, 4f, null);
                    Speed.X = slowFall ? -160f : -220f;
                    Speed.Y = -80f;
                    noGravityTimer = 0.1f;
                    return true;
                }
            }

            return false;
        }
    }
}
