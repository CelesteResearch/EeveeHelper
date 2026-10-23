using System.Collections.Generic;
using Celeste.Scene.Entities;

namespace Celeste.Mods.EeveeHelper.Handlers.Impl
{
    public class TrackSpinnerHandler(SceneEntityState entity) : EntityHandler(entity), IAnchorProvider
    {
        public List<string> GetAnchors() => new() { "Start", "End" };
    }
}
