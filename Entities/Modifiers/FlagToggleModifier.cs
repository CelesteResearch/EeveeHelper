using Celeste.Core.Components;
using System.Numerics;
using System.Collections.Generic;
using Celeste.Core.Scheduling;
using Celeste.Core.Reflection.Runtime;
using Celeste.Mods.EeveeHelper.Components;
using Celeste.Mods.EeveeHelper.Handlers;
using Celeste.Mods.EeveeHelper.Runtime;
using Celeste.Mods.EeveeHelper.Entities.Containers;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Game.Content.Maps.Entities;
using Celeste.Game.Content.Maps.Levels;
using Celeste.Game.Execution.Levels;
using Celeste.Physics.Collision.Shapes;
using Celeste.Scene.Entities;
using Celeste.Simulation;
using Celeste.Game.Components.Talking;
using Entity = Celeste.Scene.Entities.SceneEntityState;
using Level = Celeste.Game.Execution.Levels.OriginalLevelSceneRoot;
using TransitionListener = Celeste.Simulation.RoomTransitionListener;

namespace Celeste.Mods.EeveeHelper.Entities.Modifiers
{
    [CustomEntity("EeveeHelper/FlagToggleModifier")]
    public class FlagToggleModifier : SceneEntityState, IContainer
    {
        public bool Toggled => string.IsNullOrEmpty(flag) ? !notFlag : ((Level)Scene).Session.GetFlag(flag) != notFlag;

        private string flag;
        private bool notFlag;

        private bool toggleActive;
        private bool toggleVisible;
        private bool toggleCollidable;

        private bool rememberInitialState;
        private bool delayedToggle;

        public EntityContainer Container { get; set; }
        private Dictionary<IEntityHandler, object> entityStates = new();
        private readonly System.Func<IToggleable, object> saveToggleState;
        private readonly System.Func<Entity, object> saveEntityState;
        private readonly System.Action<IToggleable> disableToggleable;
        private readonly System.Action<Entity> disablePlainEntity;

        public static SceneEntityState Load(OriginalLevelSceneRoot level, OriginalLevelRoomData room, Vector2 offset,
            OriginalLevelEntityData data) => new FlagToggleModifier(level.EngineContext, data, offset);

        public FlagToggleModifier(ISourceEngineContext engine, EntityData data, Vector2 offset) : base(
            ((ISourceEntitySourceDataEnvironment)engine).EntitySourceDataRegistration, data.Position + offset)
        {
            Collider = new HitboxColliderGeometry(data.Width, data.Height);
            Depth = -1000009;

            var parsedFlag = ContainerMotion.ParseFlagAttr(data.Attr("flag"));
            flag = parsedFlag.Item1;
            notFlag = parsedFlag.Item2;

            if (data.Bool("notFlag"))
            {
                notFlag = !notFlag;
            }

            toggleActive = data.Bool("toggleActive", true);
            toggleVisible = data.Bool("toggleVisible", true);
            toggleCollidable = data.Bool("toggleCollidable", true);

            rememberInitialState = data.Bool("rememberInitialState", true);
            delayedToggle = data.Bool("delayedToggle", true);

            saveToggleState = t => rememberInitialState ? t.SaveState() : t.GetDefaultState();
            saveEntityState = e =>
                rememberInitialState ? new FlagToggleEntityState(e) : new FlagToggleEntityState(true);
            disableToggleable = t => t.Disable(toggleActive, toggleVisible, toggleCollidable);
            disablePlainEntity = e => FlagToggleEntityState.Disable(e, toggleActive, toggleVisible, toggleCollidable);

            Add(Container = new EntityContainer(engine, data)
            {
                DefaultIgnored = e => e.Get<EntityContainer>() != null,
                OnAttach = OnAttach,
                OnDetach = EnableEntity
            });

            Add(new TransitionListener
            {
                OnIn = (f) => CheckToggled()
            });
        }

        public override void Update()
        {
            base.Update();
            CheckToggled();
        }

        private void CheckToggled()
        {
            if (Toggled)
            {
                EnableEntities();
            }
            else
            {
                DisableEntities();
            }
        }

        private void OnAttach(IEntityHandler handler)
        {
            if (delayedToggle || Toggled)
            {
                return;
            }

            DisableEntity(handler);
        }

        private void DisableEntities()
        {
            foreach (var handler in Container.Contained)
            {
                DisableEntity(handler);
            }
        }

        private void DisableEntity(IEntityHandler handler)
        {
            if (!entityStates.ContainsKey(handler))
            {
                var state = HandlerUtils.GetAs<IToggleable, object>(handler, saveToggleState, saveEntityState);

                if (state != null)
                {
                    entityStates.Add(handler, state);
                }
            }

            HandlerUtils.DoAs<IToggleable>(handler, disableToggleable, disablePlainEntity);
        }

        private void EnableEntities()
        {
            foreach (var handler in Container.Contained)
            {
                EnableEntity(handler);
            }

            entityStates.Clear();
        }

        private void EnableEntity(IEntityHandler handler)
        {
            if (entityStates.ContainsKey(handler))
            {
                RestoreEntity(handler, entityStates[handler]);
            }
        }

        private void RestoreEntity(IEntityHandler handler, object state)
        {
            HandlerUtils.DoAs<IToggleable>(handler,
                t => t.ReadState(state, toggleActive, toggleVisible, toggleCollidable),
                e => ((FlagToggleEntityState)state).Apply(e, toggleActive, toggleVisible, toggleCollidable));
        }
    }
}
