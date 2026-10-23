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
    public partial class FlagGateContainer
    {
        private IEnumerator Sequence(Vector2 node)
        {
            while (moving)
            {
                cancelMoving = true;
                yield return null;
            }

            cancelMoving = false;
            moving = true;
            var start = anchorPos;
            wiggler.Stop();
            icon.Scale = Vector2.One;
            if (node != start)
            {
                if (shakeTime > 0f)
                {
                    yield return 0.1f;
                    if (playSounds)
                    {
                        openSfx.Play("event:/game/general/touchswitch_gate_open");
                    }

                    StartShaking(shakeTime);
                    while (icon.Rate < 1f)
                    {
                        var lastColor = icon.Color;
                        icon.Color = SourceColorInterpolation.Lerp(lastColor, activeColor, icon.Rate,
                            ((ISourceScalarEnvironment)engine).ScalarArithmetic);
                        icon.Rate += engine.DeltaTime / shakeTime;
                        yield return null;
                    }

                    yield return 0.1f;
                }
                else
                {
                    if (playSounds)
                    {
                        openSfx.Play("event:/game/general/touchswitch_gate_open");
                    }

                    icon.Rate = 1f;
                    icon.Color = activeColor;
                }

                if (cancelMoving)
                {
                    moving = false;
                    yield break;
                }

                var tween = Tween.Create(engine.TweenPool, Tween.TweenMode.Oneshot, easer, moveTime, start: true);
                var waiting = true;
                tween.OnUpdate = (t) =>
                    MoveTo(Vector2Math.Lerp(start, node, t.Eased, ((ISourceScalarEnvironment)engine).ScalarArithmetic));
                tween.OnComplete = (t) => waiting = false;
                Add(tween);
                while (waiting)
                {
                    if (cancelMoving)
                    {
                        tween.Stop();
                        Remove(tween);
                        moving = false;
                        yield break;
                    }

                    yield return null;
                }

                Remove(tween);
                MoveTo(node);
                if (playSounds)
                {
                    ((Celeste.Core.Audio.ISourceSpatialAudioEnvironment)engine).SpatialAudio.Play(
                        "event:/game/general/touchswitch_gate_finish", Position);
                }

                if (shakeTime > 0f)
                {
                    StartShaking(0.2f);
                }

                while (icon.Rate > 0f && !cancelMoving)
                {
                    icon.Color = SourceColorInterpolation.Lerp(activeColor, activated ? finishColor : inactiveColor,
                        1f - icon.Rate, ((ISourceScalarEnvironment)engine).ScalarArithmetic);
                    icon.Rate -= engine.DeltaTime * 4f;
                    yield return null;
                }

                if (cancelMoving)
                {
                    moving = false;
                    yield break;
                }

                icon.Rate = 0f;
                icon.SetAnimationFrame(0);
                wiggler.Start();
                if (iconVisible)
                {
                    var was = Collidable;
                    Collidable = false;
                    for (var i = 0; i < 32; i++)
                    {
                        var angle = SourceRandomMath.NextFloat(((ISourceRandomEnvironment)engine).Random,
                            (float)Math.PI * 2f);
                        ((Level)Scene).ParticlesFG.Emit(
                            ((IOriginalGameplayProcess)engine).ParticleTypes.TouchSwitch.P_Fire,
                            Center + iconOffset + Vector2Geometry.AngleToVector(angle, 4f), angle);
                    }

                    Collidable = was;
                }
            }
            else
            {
                icon.Rate = 1f;
                while (icon.Rate > 0f && !cancelMoving)
                {
                    var lastColor = icon.Color;
                    icon.Color = SourceColorInterpolation.Lerp(lastColor, activated ? finishColor : inactiveColor,
                        1f - icon.Rate, ((ISourceScalarEnvironment)engine).ScalarArithmetic);
                    icon.Rate -= engine.DeltaTime * 4f;
                    yield return null;
                }

                icon.Color = finishColor;
                icon.Rate = 0f;
                icon.SetAnimationFrame(0);
            }

            moving = false;
        }
    }
}
