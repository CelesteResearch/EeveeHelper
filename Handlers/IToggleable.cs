using System.Numerics;
using Celeste.Core.Components;
using Celeste.Core.Scheduling;
using Celeste.Mods.EeveeHelper.Runtime;
using Celeste.Mods.EeveeHelper.Entities.Containers;
using Celeste.Scene.Tracking;
using Entity = Celeste.Scene.Entities.SceneEntityState;
using EntityData = Celeste.Formats.Map.Entities.Structure.EntityData;
using Rectangle = Celeste.Core.Geometry.IntRect;
using Level = Celeste.Game.Execution.Levels.OriginalLevelSceneRoot;
using Platform = Celeste.Simulation.Gameplay.Platforms.Foundation.SourcePlatformEntityState;
using Player = Celeste.Simulation.RoomPlayerSession;
using SolidTiles = Celeste.Game.Execution.LevelLoader.Terrain.OriginalSolidTiles;
using BackgroundTiles = Celeste.Game.Execution.LevelLoader.Terrain.OriginalBackgroundTiles;
using Decal = Celeste.Simulation.Presentation.Decals.Entities.SourceDecalEntityState;
using Trigger = Celeste.Simulation.Gameplay.Triggers.Foundation.SourceTriggerEntityState;
using WindController = Celeste.Game.Entities.Environment.Wind.OriginalWindController;
using Follower = Celeste.Scene.Following.FollowerState;
using TransitionListener = Celeste.Simulation.RoomTransitionListener;

namespace Celeste.Mods.EeveeHelper.Handlers
{
    public interface IToggleable
    {
        object GetDefaultState();

        object SaveState();

        void ReadState(object state, bool toggleActive, bool toggleVisible, bool toggleCollidable);

        void Disable(bool toggleActive, bool toggleVisible, bool toggleCollidable);
    }
}
