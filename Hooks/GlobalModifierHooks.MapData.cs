using System;
using System.Collections.Generic;
using System.Linq;
using Celeste.Core.Geometry;
using Celeste.Game.Content.Maps;
using Celeste.Game.Content.Maps.Entities;
using Celeste.Game.Content.Maps.Levels;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Execution.Startup;
using Celeste.Mods.EeveeHelper.Entities.Modifiers;

namespace Celeste.Mods.EeveeHelper.Hooks
{
    public sealed partial class GlobalModifierHooks
    {
        private List<OriginalLevelEntityData> Transform(List<OriginalLevelEntityData> list,
            OriginalLevelSceneRoot level, bool triggers)
        {
            if (!Owns(level)) return list;
            if (!fromLoader) return modified.Count > 0 ? list.Except(modified).ToList() : list;
            if (!triggers) modified.Clear();
            var result = new List<OriginalLevelEntityData>(list);
            foreach (var room in ((OriginalChapterMapData)level.Session.MapData).Levels)
            foreach (var modifier in room.Entities.Where(data => data.Name == GlobalModifier.ID))
            {
                string[] whitelist = modifier.Attr("whitelist")
                    .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                var bounds = new IntRect((int)modifier.Position.X, (int)modifier.Position.Y, modifier.Width,
                    modifier.Height);
                var tagSet = ((IOriginalGameTagsEnvironment)engine).Tags;
                int tags = tagSet.Global.Value;
                if (modifier.Bool("frozenUpdate")) tags |= tagSet.FrozenUpdate.Value;
                if (modifier.Bool("pauseUpdate")) tags |= tagSet.PauseUpdate.Value;
                if (modifier.Bool("transitionUpdate")) tags |= tagSet.TransitionUpdate.Value;
                foreach (var record in triggers ? room.Triggers : room.Entities)
                    if (!ReferenceEquals(record, modifier) && (whitelist.Length == 0 ||
                                                               whitelist.Any(value =>
                                                                   record.Name == value ||
                                                                   record.ID.ToString() == value)))
                    {
                        bool matched = record.Width != 0 && record.Height != 0
                            ? bounds.Intersects(new IntRect((int)record.Position.X, (int)record.Position.Y,
                                record.Width, record.Height))
                            : bounds.Contains((int)record.Position.X, (int)record.Position.Y);
                        if (!matched) continue;
                        if (result.Contains(record)) result.Remove(record);
                        modified.Add(record);
                        var clone = Clone(record, (OriginalLevelRoomData)level.Session.LevelData);
                        clone.Values["globalModifierSpawned"] = tags;
                        result.Add(clone);
                    }
            }

            return result;
        }

        private static OriginalLevelEntityData Clone(OriginalLevelEntityData data, OriginalLevelRoomData room) => new()
        {
            Name = data.Name, Level = room, ID = data.ID,
            Position = data.Position + data.Level.Position - room.Position,
            Width = data.Width, Height = data.Height, Origin = data.Origin,
            Nodes = data.NodesOffset(data.Level.Position - room.Position),
            Values = data.Values == null
                ? new Dictionary<string, object>()
                : new Dictionary<string, object>(data.Values)
        };
    }
}
