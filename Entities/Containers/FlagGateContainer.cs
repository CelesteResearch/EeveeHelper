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
    [CustomEntity("EeveeHelper/FlagGateContainer")]
    [RuntimeTypeAlias("Celeste.Mod.EeveeHelper.Entities.Containers.FlagGateContainer")]
    public partial class FlagGateContainer : Entity, IContainer
    {
        // Originally "im so hecking gay".GetHashCode(), which desynced TASes on different platforms
        // Hardcoded to keep compatibility with older TASes but sync across future ones
        private const int RandomGay = -1569374549;

        public EntityContainer Container
        {
            get => _Container;
            set
            {
                if (value is EntityContainerMover mover)
                {
                    _Container = mover;
                }
            }
        }

        public EntityContainerMover _Container;

        private Vector2[] nodes;
        private string flag; // replace

        private bool notFlag; // replace

        //private bool resetFlags;
        private bool canReturn;
        private float shakeTime;

        private float moveTime;

        //private bool progression;
        private Func<float, float> easer;
        private Color inactiveColor;
        private Color activeColor;
        private Color finishColor;
        private bool iconVisible;
        private bool staticFit;
        private bool playSounds;

        private Vector2 startPos;
        private Vector2 anchorPos;
        private Vector2 offset;
        private Sprite icon;
        private Vector2 iconOffset;
        private Wiggler wiggler;
        private SoundSource openSfx;
        private bool moving;
        private bool cancelMoving;

        private bool activated; // replace

        //private bool[] wasEnabled;
        private bool vanilla;

        private bool shaking;
        private float shakeTimer;
        private float shakeStrength;
        private Vector2 shakeAmount;
        private ISourceRandom shakeRand;

        private readonly ISourceEngineContext engine;

        public static Entity Load(OriginalLevelSceneRoot level, OriginalLevelRoomData room, Vector2 offset,
            OriginalLevelEntityData data) => new FlagGateContainer(level.EngineContext, data, offset);

        public FlagGateContainer(ISourceEngineContext engine, EntityData data, Vector2 offset) : base(
            ((ISourceEntitySourceDataEnvironment)engine).EntitySourceDataRegistration, data.Position + offset)
        {
            this.engine = engine;
            Collider = new Hitbox(data.Width, data.Height);
            Depth = 8999;

            var rawNodes = data.NodesOffset(offset);
            if (rawNodes.Length > 0)
            {
                iconOffset = rawNodes[0] - Center;
            }

            nodes = rawNodes.Skip(1).ToArray();
            for (var i = 0; i < nodes.Length; i++)
            {
                nodes[i] += new Vector2(Width / 2f, Height / 2f);
            }

            var parsedFlag = ContainerMotion.ParseFlagAttr(data.Attr("moveFlag"));
            flag = parsedFlag.Item1;
            notFlag = parsedFlag.Item2;
            //resetFlags = data.Bool("resetFlags", true);
            canReturn = data.Bool("canReturn", true);
            shakeTime = data.Float("shakeTime", 0.5f);
            shakeStrength = data.Float("shakeStrength", 1f);
            moveTime = data.Float("moveTime", 2f);
            //progression = data.Bool("progression");
            var easingName = data.Attr("easing", "CubeOut");
            var easers = ((ISourceNamedEasingArithmetic)((ISourceEasingEnvironment)engine).EasingArithmetic)
                .NamedEasers;
            if (!easers.Keys.Contains(easingName, StringComparer.Ordinal)) throw new KeyNotFoundException(easingName);
            easer = easers[easingName];
            inactiveColor = SourceHexColor.HexToColor(data.Attr("inactiveColor", "5fcde4"));
            activeColor = SourceHexColor.HexToColor(data.Attr("activeColor", "ffffff"));
            finishColor = SourceHexColor.HexToColor(data.Attr("finishColor", "f141df"));
            iconVisible = data.Bool("iconVisible");
            staticFit = data.Bool("staticFit");
            playSounds = data.Bool("playSounds");
            var iconName = data.Attr("icon", "objects/switchgate/icon");

            shakeRand = ((ISourceRandomProcessEnvironment)engine).RandomProcess.Factory.CreateSeeded(RandomGay);

            startPos = anchorPos = Center;
            //wasEnabled = new bool[flags.Length];

            //if (flags.Length == 0 && nodes.Length > 0)
            if (string.IsNullOrEmpty(flag))
            {
                vanilla = true;
            }

            Add(icon = new Sprite(engine, ((ISourceGameGraphicsEnvironment)engine).GameGraphics.GameAtlas, iconName));
            icon.Add("spin", "", 0.1f, new Chooser<string>("spin"));
            icon.Play("spin");
            icon.Rate = 0f;
            icon.Color = inactiveColor;
            icon.Position = new Vector2(Width / 2f, Height / 2f) + iconOffset;
            icon.Visible = iconVisible;
            icon.CenterOrigin();

            Add(wiggler =
                ((ISourceWigglerPoolScope)engine).WigglerPool.Create(0.5f, 4f,
                    f => icon.Scale = Vector2.One * (1f + f)));
            Add(openSfx = ((ISourceSoundSourceEnvironment)engine).SoundSources.Create());

            Add(_Container = new EntityContainerMover(engine, data, true)
            {
                OnFit = OnFit,
                DefaultIgnored = e => e is FlagGateContainer,
                OnAttach = h => Depth = Math.Min(Depth, h.Entity.Depth - 1)
            });
        }

        public override void Awake(object scene)
        {
            base.Awake(scene);

            activated = vanilla
                ? Switch.CheckLevelFlag(scene as Level)
                : ((scene as Level).Session.GetFlag(flag) != notFlag);

            if (activated)
            {
                MoveTo(nodes[0]);
                icon.Rate = 0f;
                icon.SetAnimationFrame(0);
                icon.Color = finishColor;
            }
        }

        public override void Update()
        {
            base.Update();

            var newActivated = vanilla ? Switch.Check(Scene) : (((Level)Scene).Session.GetFlag(flag) != notFlag);

            if (newActivated != activated && (!activated || canReturn))
            {
                activated = newActivated;
                Add(new Coroutine(engine, Sequence(activated ? nodes[0] : startPos)));
            }

            var newDepth = 8999;
            foreach (var entity in _Container.GetEntities())
            {
                newDepth = Math.Min(newDepth, entity.Depth - 1);
            }

            Depth = newDepth;

            if (shaking)
            {
                if (((ISourceScene)Scene).SourceScene.Clock.OnInterval(0.04f, engine))
                {
                    shakeAmount = shakeStrength * RandomShakeVector.Next(shakeRand);
                }

                if (shakeTimer > 0f)
                {
                    shakeTimer -= engine.DeltaTime;
                    if (shakeTimer <= 0f)
                    {
                        shaking = false;
                        StopShaking();
                    }
                }
            }

            MoveTo(anchorPos);
        }

        public override void Render(ISourceRenderContext context)
        {
            if (iconVisible)
            {
                icon.DrawOutline(context);
            }

            base.Render(context);
        }

        private void OnFit(Vector2 pos, float width, float height)
        {
            var lastCenter = Center;
            Position = pos;
            Collider.Width = width;
            Collider.Height = height;
            icon.Position = new Vector2(Width / 2f, Height / 2f) + iconOffset;
            if (staticFit)
            {
                _Container.DoMoveAction(() => Center = lastCenter);
            }
            else
            {
                offset += Center - lastCenter;
            }
        }

        private void MoveTo(Vector2 center)
        {
            anchorPos = center;
            if (Center != center + offset + shakeAmount)
            {
                _Container.DoMoveAction(() => Center = center + offset + shakeAmount);
            }
        }

        public void StartShaking(float time = 0f)
        {
            shaking = true;
            shakeTimer = time;
        }

        public void StopShaking()
        {
            shaking = false;
            shakeAmount = Vector2.Zero;
        }
    }
}
