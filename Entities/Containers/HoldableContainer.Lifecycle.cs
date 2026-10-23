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
        private IEnumerator DestroyRoutine()
        {
            destroyed = true;
            Collidable = false;
            if (tutorialGui != null)
            {
                tutorialGui.Open = false;
            }

            ((IOriginalGameplayProcess)engine).AudioSession.Play(
                "event:/new_content/game/10_farewell/glider_emancipate", Position,
                ((IOriginalGameplayProcess)engine).SaveContext);
            var tween = Tween.Create(engine.TweenPool, Tween.TweenMode.Oneshot, null, 0.2f, true);
            tween.OnUpdate = (t) => whiteAlpha = t.Eased;
            Add(tween);
            yield return 0.2f;
            if (!respawn)
            {
                _Container.FitContained = false;
                _Container.DestroyContained();
            }
            else
            {
                grabbedOnce = false;
                Speed = Vector2.Zero;
                _Container.DoMoveAction(() => Position = respawnPosition);
            }

            var tween2 = Tween.Create(engine.TweenPool, Tween.TweenMode.Oneshot, null, 0.1f, true);
            tween2.OnUpdate = (t) => whiteAlpha = (1f - t.Eased);
            Add(tween2);
            yield return 0.1f;
            if (!respawn)
            {
                RemoveSelf();
            }
            else
            {
                destroyed = false;
                Collidable = true;
            }
        }
    }
}
