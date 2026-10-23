using Celeste.Core.Scheduling;
using Celeste.Scene.Entities;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Player.Collision.Components;

namespace Celeste.Mods.EeveeHelper.Entities.Modifiers
{
    public partial class CollidableModifier
    {
        [Celeste.Core.Reflection.Runtime.RuntimeTypeAlias(
            "Celeste.Mod.EeveeHelper.Entities.Modifiers.CollidableModifier+Hazard")]
        public class Hazard : SceneEntityState
        {
            public SceneEntityState Entity;
            private readonly ISourceEngineContext engine;

            public Hazard(SceneEntityState entity, ISourceEngineContext engine)
                : base(
                    ((Celeste.Core.Components.ISourceEntitySourceDataEnvironment)engine).EntitySourceDataRegistration,
                    entity.Position)
            {
                this.engine = engine;
                Collider = entity.Collider.Clone();
                Depth = entity.Depth + 1;
                Entity = entity;
                Add(new PlayerColliderComponentState(OnPlayer));
            }

            public override void Update()
            {
                base.Update();
                if (Entity == null || Entity.Scene == null)
                {
                    RemoveSelf();
                    return;
                }

                Depth = Entity.Depth + 1;
                if (Entity.Collider.Size != Collider.Size) Collider = Entity.Collider.Clone();
                Position = Entity.Position;
            }

            private void OnPlayer(RoomPlayerSession player) => player.Die(
                ((Celeste.Core.Mathematics.ISourceVectorEnvironment)engine).VectorArithmetic.SafeNormalize(
                    player.Center - Center), false, true);
        }
    }
}
