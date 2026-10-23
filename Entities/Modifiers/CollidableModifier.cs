using System.Collections.Generic;
using System.Numerics;
using Celeste.Core.Scheduling;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Game.Content.Maps.Entities;
using Celeste.Game.Content.Maps.Levels;
using Celeste.Game.Execution.Levels;
using Celeste.Mods.EeveeHelper.Components;
using Celeste.Mods.EeveeHelper.Entities.Containers;
using Celeste.Mods.EeveeHelper.Runtime;
using Celeste.Mods.EeveeHelper.Runtime.Modifiers;
using Celeste.Physics.Collision.Shapes;
using Celeste.Scene.Entities;
using Celeste.Core.Components;
using Celeste.Simulation.Gameplay.Platforms.Foundation;
using Celeste.Simulation.Gameplay.Player.Collision.Components;
using Celeste.Scene.Tracking;

namespace Celeste.Mods.EeveeHelper.Entities.Modifiers
{
    [Celeste.Core.Reflection.Runtime.RuntimeTypeAlias("Celeste.Mod.EeveeHelper.Entities.Modifiers.CollidableModifier")]
    [CustomEntity("EeveeHelper/CollidableModifier = Create")]
    public partial class CollidableModifier : SceneEntityState, IContainer
    {
        public enum Mode
        {
            NoCollide,
            Solid,
            Hazardous
        }

        private readonly ISourceEngineContext engine;
        private readonly CollidableModifierRuntime runtime;
        private readonly Mode mode;
        private readonly bool keepBaseCollision;
        private readonly Dictionary<SceneEntityState, bool> wasCollidable = new();
        private readonly Dictionary<SceneEntityState, SourceSolidEntityState> solids = new();
        private readonly Dictionary<SceneEntityState, Hazard> hazards = new();
        private readonly Dictionary<SceneEntityState, SourceComponentState> hazardComponents = new();
        public EntityContainer Container { get; set; }

        public static CollidableModifier Create(OriginalLevelSceneRoot level, OriginalLevelRoomData room,
            Vector2 offset, OriginalLevelEntityData data) => new(data, offset, level.EngineContext);

        public CollidableModifier(EntityData data, Vector2 offset, ISourceEngineContext engine)
            : base(((Celeste.Core.Components.ISourceEntitySourceDataEnvironment)engine).EntitySourceDataRegistration,
                data.Position + offset)
        {
            this.engine = engine;
            runtime = CollidableModifierRuntime.For(engine);
            Collider = new HitboxColliderGeometry(data.Width, data.Height);
            mode = data.Has("collisionMode") ? data.Enum("collisionMode", Mode.NoCollide) :
                data.Bool("solidify") ? Mode.Solid : Mode.NoCollide;
            keepBaseCollision = data.Bool("keepBaseCollision");
            Add(Container = new EntityContainer(engine, data)
            {
                IsValid = e =>
                    (!(e is Solidifier solidifier) || !Container.ContainsEntity(solidifier.Entity)) && !(e is Hazard),
                DefaultIgnored = e => e.Get<EntityContainer>() != null,
                OnAttach = h => OnAttach(h.Entity),
                OnDetach = h => OnDetach(h.Entity)
            });
        }

        private void OnAttach(SceneEntityState entity)
        {
            if (mode == Mode.Solid && !solids.ContainsKey(entity) && entity.Collider != null &&
                (!keepBaseCollision || !(entity is SourceSolidEntityState)))
            {
                var solidifier = new Solidifier(entity,
                    ((Celeste.Simulation.Gameplay.Platforms.Collision.ISourcePlatformCollisionScene)Scene)
                    .SolidWorldFactory);
                runtime.Data(entity).Set("solidModifierSolidifier", solidifier);
                solids.Add(entity, solidifier);
                ((ISourceScene)Scene).SourceScene.QueueSourceEntity(solidifier);
            }

            if (mode == Mode.Hazardous && entity.Collider != null)
            {
                if (keepBaseCollision && !hazardComponents.ContainsKey(entity))
                {
                    var component = new PlayerColliderComponentState(player =>
                        player.Die(
                            ((Celeste.Core.Mathematics.ISourceVectorEnvironment)engine).VectorArithmetic.SafeNormalize(
                                player.Center - entity.Center), false, true));
                    hazardComponents.Add(entity, component);
                    entity.Add(component);
                }
                else if (!keepBaseCollision && !hazards.ContainsKey(entity))
                {
                    var hazard = new Hazard(entity, engine);
                    runtime.Data(entity).Set("collidableModifierHazard", hazard);
                    hazards.Add(entity, hazard);
                    ((ISourceScene)Scene).SourceScene.QueueSourceEntity(hazard);
                }
            }

            if (mode == Mode.NoCollide || !keepBaseCollision)
            {
                if (!wasCollidable.ContainsKey(entity)) wasCollidable.Add(entity, entity.Collidable);
                entity.Collidable = false;
            }
        }

        private void OnDetach(SceneEntityState entity)
        {
            if (mode == Mode.Solid && solids.ContainsKey(entity))
            {
                var solid = solids[entity];
                runtime.Data(entity).Set<Solidifier>("solidModifierSolidifier", null);
                // The published implementation removes the value, not the source key.
                solids.Remove(solid);
                if (solid.Scene != null) solid.RemoveSelf();
            }

            if (mode == Mode.Hazardous)
            {
                if (hazardComponents.ContainsKey(entity))
                {
                    entity.Remove(hazardComponents[entity]);
                    hazardComponents.Remove(entity);
                }
                else if (hazards.ContainsKey(entity))
                {
                    var hazard = hazards[entity];
                    runtime.Data(entity).Set<Hazard>("collidableModifierHazard", null);
                    hazards.Remove(entity);
                    if (hazard.Scene != null) hazard.RemoveSelf();
                }
            }

            if (mode == Mode.NoCollide || !keepBaseCollision)
                entity.Collidable = wasCollidable.TryGetValue(entity, out var previous) ? previous : true;
        }

        public override void Update()
        {
            base.Update();
            if (mode != Mode.NoCollide && keepBaseCollision) return;
            foreach (var entity in Container.GetEntities()) entity.Collidable = false;
        }
    }
}
