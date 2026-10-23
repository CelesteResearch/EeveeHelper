using System;
using Celeste.Core.Components;
using Celeste.Core.Scheduling;
using Celeste.Core.Rendering;
using Celeste.Scene.Entities;
using Celeste.Scene.Tracking;

namespace Celeste.Mods.EeveeHelper.Components
{
    public partial class DepthPartner : SourceComponentState
    {
        private readonly ISourceEngineContext engine;
        public PartnerEntity Partner;

        public int Depth;
        public Action OnUpdate;
        public Action OnRender;

        public DepthPartner(ISourceEngineContext engine, int depth) : base(true, true)
        {
            this.engine = engine;
            Depth = depth;
        }

        public override void EntityAdded(object scene)
        {
            base.EntityAdded(scene);
            ((ISourceScene)scene).SourceScene.QueueSourceEntity(Partner = new PartnerEntity(this));
        }

        public override void EntityRemoved(object scene)
        {
            base.EntityRemoved(scene);
            Partner.RemoveSelf();
        }
    }
}
