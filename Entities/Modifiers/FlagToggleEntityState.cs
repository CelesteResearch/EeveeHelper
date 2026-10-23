using Celeste.Scene.Entities;
using Celeste.Game.Components.Talking;
using Entity = Celeste.Scene.Entities.SceneEntityState;

namespace Celeste.Mods.EeveeHelper.Entities.Modifiers
{
    public struct FlagToggleEntityState
    {
        public bool Active;
        public bool Visible;
        public bool Collidable;

        public bool TalkComponentEnabled;

        public FlagToggleEntityState(bool value)
        {
            Active = value;
            Visible = value;
            Collidable = value;

            TalkComponentEnabled = value;
        }

        public FlagToggleEntityState(Entity entity)
        {
            Active = entity.Active;
            Visible = entity.Visible;
            Collidable = entity.Collidable;

            var talkComponent = entity.Get<OriginalTalkComponent>();
            if (talkComponent != null)
            {
                TalkComponentEnabled = talkComponent.Enabled;
            }
            else
            {
                TalkComponentEnabled = false;
            }
        }

        public void Apply(Entity entity, bool toggleActive, bool toggleVisible, bool toggleCollidable)
        {
            if (toggleActive)
            {
                entity.Active = Active;
            }

            if (toggleVisible)
            {
                entity.Visible = Visible;
            }

            if (toggleCollidable)
            {
                entity.Collidable = Collidable;

                var talkComponent = entity.Get<OriginalTalkComponent>();
                if (talkComponent != null)
                {
                    talkComponent.Enabled = TalkComponentEnabled;
                }
            }
        }

        public static void Disable(Entity entity, bool toggleActive, bool toggleVisible, bool toggleCollidable)
        {
            if (toggleActive)
            {
                entity.Active = false;
            }

            if (toggleVisible)
            {
                entity.Visible = false;
            }

            if (toggleCollidable)
            {
                entity.Collidable = false;

                var talkComponent = entity.Get<OriginalTalkComponent>();
                if (talkComponent != null)
                {
                    talkComponent.Enabled = false;
                }
            }
        }
    }
}
