using System;
using Celeste.Core.Components;
using Celeste.Core.Scheduling;
using Celeste.Core.Rendering;
using Celeste.Scene.Entities;
using Celeste.Scene.Tracking;

namespace Celeste.Mods.EeveeHelper.Components
{
    public partial class DepthPartner
    {
        public class PartnerEntity : SceneEntityState
        {
            private DepthPartner component;

            public PartnerEntity(DepthPartner component) : base(((ISourceEntitySourceDataEnvironment)component.engine)
                .EntitySourceDataRegistration)
            {
                this.component = component;
                Depth = component.Depth;
            }

            public override void Update()
            {
                base.Update();
                if (component.Entity != null && ((ISourceMutableEntityState)component.Entity).Active)
                {
                    component.OnUpdate?.Invoke();
                }
            }

            public override void Render(ISourceRenderContext context)
            {
                base.Render(context);
                if (component.Entity != null && ((ISourceMutableEntityState)component.Entity).Visible)
                {
                    component.OnRender?.Invoke();
                }
            }
        }
    }
}
