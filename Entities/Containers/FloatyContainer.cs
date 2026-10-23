using System;
using System.Numerics;
using Celeste.Core.Components;
using Celeste.Core.Mathematics;
using Celeste.Core.Mathematics.Easing;
using Celeste.Core.Mathematics.Extrema;
using Celeste.Core.Randomness;
using Celeste.Core.Reflection.Runtime;
using Celeste.Core.Scheduling;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Game.Content.Maps.Entities;
using Celeste.Game.Content.Maps.Levels;
using Celeste.Game.Execution.Levels;
using Celeste.Mods.EeveeHelper.Components;
using Celeste.Physics.Collision.Shapes;
using Celeste.Scene.Entities;
using Celeste.Simulation.Gameplay.Platforms.Foundation;
using Celeste.Simulation.Gameplay.Player.Collision.Surfaces;

namespace Celeste.Mods.EeveeHelper.Entities.Containers
{
    [RuntimeTypeAlias("Celeste.Mod.EeveeHelper.Entities.FloatyContainer")]
    [CustomEntity("EeveeHelper/FloatyContainer")]
    public sealed class FloatyContainer : SceneEntityState, IContainer
    {
        private readonly ISourceEngineContext engine;
        public EntityContainer Container => _Container;
        public EntityContainerMover _Container { get; set; }
        private bool disablePush;
        private float floatSpeed;
        private float floatMove;
        private float pushSpeed;
        private float pushMove;
        private float sinkSpeed;
        private float sinkMove;
        private Vector2 anchorPosition;
        private float yLerp;
        private float sinkTimer;
        private float sineWave;
        private float dashEase;
        private Vector2 dashDirection;
        private bool liftSpeedFix;

        public static SceneEntityState Load(OriginalLevelSceneRoot level, OriginalLevelRoomData room, Vector2 offset,
            OriginalLevelEntityData data) => new FloatyContainer(level.EngineContext, data, offset);

        public FloatyContainer(ISourceEngineContext engine, EntityData data, Vector2 offset) : base(
            ((ISourceEntitySourceDataEnvironment)engine).EntitySourceDataRegistration, data.Position + offset)
        {
            this.engine = engine;
            Collider = new HitboxColliderGeometry(data.Width, data.Height);
            Depth = -1000010;
            disablePush = data.Bool("disablePush");
            floatSpeed = data.Float("floatSpeed", 1f);
            floatMove = data.Float("floatMove", 4f);
            pushSpeed = data.Float("pushSpeed", 1f);
            pushMove = data.Float("pushMove", 8f);
            sinkSpeed = data.Float("sinkSpeed", 1f);
            sinkMove = data.Float("sinkMove", 12f);
            if (!data.Bool("disableSpawnOffset"))
                sineWave = SourceRandomMath.NextFloat(((ISourceRandomEnvironment)engine).Random, (float)Math.PI * 2f);
            Add(_Container = new EntityContainerMover(engine, data, fitContained: false)
            {
                DefaultIgnored = entity => entity is FloatyContainer
            });
            liftSpeedFix = data.Bool("liftSpeedFix", false);
            anchorPosition = Position;
        }

        public override void Awake(object scene)
        {
            base.Awake(scene);
            if (disablePush) return;
            foreach (var contained in _Container.Contained)
            {
                if (contained.Entity is SourcePlatformEntityState platform)
                {
                    var previous = platform.OnDashCollide;
                    platform.OnDashCollide = null;
                    platform.OnDashCollide = (player, direction) =>
                    {
                        var result = previous?.Invoke(player, direction) ?? DashCollisionResult.NormalCollision;
                        if (dashEase <= .2f)
                        {
                            dashEase = 1f;
                            dashDirection = direction;
                        }

                        return result == DashCollisionResult.NormalCollision
                            ? DashCollisionResult.NormalOverride
                            : result;
                    };
                }
            }
        }

        public override void Update()
        {
            base.Update();
            if (!_Container.Attached) return;
            if (HasRider()) sinkTimer = .3f;
            else if (sinkTimer > 0f) sinkTimer -= engine.DeltaTime;
            yLerp = sinkTimer > 0f
                ? ScalarMath.Approach(yLerp, 1f, sinkSpeed * engine.DeltaTime,
                    ((ISourceExtremaEnvironment)engine).ExtremaArithmetic)
                : ScalarMath.Approach(yLerp, 0f, sinkSpeed * engine.DeltaTime,
                    ((ISourceExtremaEnvironment)engine).ExtremaArithmetic);
            sineWave += engine.DeltaTime;
            dashEase = ScalarMath.Approach(dashEase, 0f, engine.DeltaTime * 1.5f * pushSpeed,
                ((ISourceExtremaEnvironment)engine).ExtremaArithmetic);
            _Container.DoMoveAction(MoveToTarget, null, liftSpeedFix);
        }

        private void MoveToTarget()
        {
            var easing = ((ISourceEasingEnvironment)engine).EasingArithmetic;
            float sine = (float)Math.Sin(sineWave * floatSpeed) * floatMove;
            Vector2 push = ScalarMath.YoYo(easing.QuadIn(dashEase)) * dashDirection * pushMove;
            float targetY = ((ISourceScalarEnvironment)engine).ScalarArithmetic.Lerp(anchorPosition.Y,
                anchorPosition.Y + sinkMove * (HasRider() ? 3 : 1), easing.SineInOut(yLerp)) + sine;
            Position = new Vector2(anchorPosition.X + push.X, targetY + push.Y);
        }

        private bool HasRider()
        {
            foreach (var contained in _Container.Contained)
                if (contained.Entity is SourceSolidEntityState solid && solid.HasPlayerRider() ||
                    contained.Entity is SourceJumpThruEntityState jumpThru && jumpThru.HasPlayerRider())
                    return true;
            return false;
        }
    }
}
