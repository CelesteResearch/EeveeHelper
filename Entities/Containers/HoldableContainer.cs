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
    [SceneTracked]
    [RuntimeTypeAlias("Celeste.Mod.EeveeHelper.Entities.HoldableContainer")]
    [CustomEntity("EeveeHelper/HoldableContainer = Load")]
    public partial class HoldableContainer : Actor, IContainer
    {
        private readonly ISourceEngineContext engine;

        public static Entity
            Load(Level level, OriginalLevelRoomData room, Vector2 offset, OriginalLevelEntityData data) =>
            new HoldableContainer(level.EngineContext, data, offset, new EntityID { Key = room.Name + ":" + data.ID });

        private float Approach(float from, float to, float amount) => ScalarMath.Approach(from, to, amount,
            ((ISourceExtremaEnvironment)engine).ExtremaArithmetic);

        private bool SolidAt() => SolidAt(Position);

        private bool SolidAt(Vector2 at) =>
            SourceTrackedEntityCollision.CheckAt(((ISourceScene)Scene).SourceScene.Entities, "Celeste.Solid", this, at);

        private bool CollideCheck(ISourceEntityCollider other) => SourceEntityCollision.Check(this, other);
        public EntityContainer Container => _Container;
        public EntityContainerMover _Container;

        public bool HasGravity
        {
            get => hasGravity && _Container?.Contained.Count > 0 && ((!waitForGrab) || grabbedOnce);
            set => hasGravity = value;
        }

        private bool hasGravity;
        private bool holdable;
        private bool noDuplicate;
        private bool destroyable;
        private bool tutorial;
        private bool respawn;
        private bool waitForGrab;

        public EntityID ID;
        public Vector2 Speed;
        public Holdable Hold;
        private Dictionary<Entity, bool> wasCollidable = new();
        private Dictionary<Entity, bool> wasPersistent = new();
        private float noGravityTimer;
        private float highFrictionTimer;
        private bool destroyed;
        private bool wasDestroyed;
        private bool slowFall;
        private bool grabbedOnce;
        private float whiteAlpha;
        private Vector2 prevLiftSpeed;
        private Vector2 holdTarget = Vector2.Zero;
        private Vector2 respawnPosition;
        private BirdTutorialGui tutorialGui;

        public HoldableContainer(ISourceEngineContext engine, EntityData data, Vector2 offset, EntityID id) : base(
            new PixelActor(null, data.Position + offset + new Vector2(data.Width / 2f, data.Height)), engine,
            ((ISourcePlatformTargetMotionEnvironment)engine).TargetMotionPolicy)
        {
            this.engine = engine;
            holdable = data.Bool("holdable");
            hasGravity = data.Bool("gravity");
            noDuplicate = data.Bool("noDuplicate");
            destroyable = data.Bool("destroyable", true);
            respawn = data.Bool("respawn");
            slowFall = data.Bool("slowFall");
            tutorial = data.Bool("tutorial");
            waitForGrab = data.Bool("waitForGrab");

            ID = id;
            Collider = new Hitbox(data.Width, data.Height);
            Collider.Position = new Vector2(-Width / 2f, -Height);
            AllowPushing = HasGravity;
            Depth = -1000010;
            respawnPosition = Position;

            Add(_Container = new EntityContainerMover(engine, data)
            {
                DefaultIgnored = e => e is HoldableContainer,
                OnFit = OnFit,
                OnPreMove = () => AllowPushing = false,
                OnPostMove = () => AllowPushing = HasGravity,
            });

            if (holdable)
            {
                Add(Hold = new Holdable(engine)
                {
                    PickupCollider = new Hitbox(Width + 8f, Height + 8f, -Width / 2f - 4f, -Height - 4f),
                    SlowFall = slowFall,
                    SlowRun = data.Bool("slowRun", true),
                    OnPickup = OnPickup,
                    OnRelease = OnRelease,
                    OnHitSpring = spring => HitSpring((Spring)spring),
                    SpeedGetter = () => Speed,
                    SpeedSetter = (speed) => Speed = speed,
                    OnCarry = OnCarry
                });

                Add(new DepthPartner(engine, 1)
                {
                    OnUpdate = () =>
                    {
                        if (Hold.IsHeld || destroyed)
                        {
                            foreach (var handler in _Container.Contained)
                            {
                                handler.Entity.Collidable = false;
                            }
                        }
                    }
                });

                Add(new TransitionListener
                {
                    OnOutBegin = () =>
                    {
                        if (Hold.IsHeld)
                        {
                            foreach (var handler in _Container.Contained)
                            {
                                if (handler.Entity != null)
                                    handler.Entity.Tags |=
                                        new Celeste.Scene.Scheduling.SceneTagSet(((IOriginalGameTagsEnvironment)engine)
                                            .Tags.Persistent.Value);
                            }
                        }
                    }
                });
            }

            SquishCallback = OnSquishNew;
        }

        public override void Awake(object scene)
        {
            base.Awake(scene);
            foreach (HoldableContainer container in ((ISourceScene)scene).SourceScene.Entities.GetTrackedEntities(
                         typeof(HoldableContainer)))
            {
                if (noDuplicate && container != this && container.ID.Key == ID.Key && container.Hold != null &&
                    container.Hold.IsHeld)
                {
                    RemoveSelf();
                    return;
                }
            }

            if (tutorial)
            {
                tutorialGui = new BirdTutorialGui(engine, ((IOriginalGameTagsEnvironment)engine).Tags.HUD.Value,
                    (ITheoCrystalTutorialResources)engine, this, new Vector2(0f, -Height),
                    ((ITheoCrystalTutorialResources)engine).CleanDialog("tutorial_carry"), new object[]
                    {
                        ((ITheoCrystalTutorialResources)engine).CleanDialog("tutorial_hold"),
                        BirdTutorialButton.Grab
                    });
                tutorialGui.Open = true;
                ((ISourceScene)scene).SourceScene.QueueSourceEntity(tutorialGui);
            }
        }
    }
}
