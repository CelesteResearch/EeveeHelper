using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Celeste.Core.Scheduling;
using Celeste.Everest.Content.Maps.Metadata;
using Celeste.Everest.Reflection;
using Celeste.Core.Reflection.Members;
using Celeste.Mods.EeveeHelper.Handlers;
using Celeste.Scene.Entities;

namespace Celeste.Mods.EeveeHelper.Runtime
{
    public sealed class EeveeContainerRuntime
    {
        private static readonly ConditionalWeakTable<ISourceEngineContext, EeveeContainerRuntime> Owners = new();
        private ConditionalWeakTable<SceneEntityState, EeveeEntityData> data = new();
        private readonly Func<Type, string> resolveTypeName;
        private EeveeTypeNameCache typeNames;

        public static EeveeContainerRuntime For(ISourceEngineContext engine) =>
            Owners.GetValue(engine, static owner => new EeveeContainerRuntime(owner));

        private EeveeContainerRuntime(ISourceEngineContext engine)
        {
            Engine = engine;
            resolveTypeName = ResolveTypeName;
            Handlers = new EntityHandlerRegistry(this);
        }

        public ISourceEngineContext Engine { get; }
        public EntityHandlerRegistry Handlers { get; }
        public bool LiftSpeedFix;
        public bool DecalStaticMoverFix;
        public Dictionary<Type, (bool, HashSet<string>)> WhitelistedAnchors { get; } = new();
        public Dictionary<Type, (bool, HashSet<string>)> BlacklistedAnchors { get; } = new();
        public Dictionary<Type, List<Type>> LegacyEntityHandlers { get; } = new();
        public SourceMemberCatalog Members => ((ISourceEverestReflectionEnvironment)Engine).Reflection.SourceMembers;
        public Type[] GetRuntimeTypes() => ((IEverestFakeEntryAssemblyEnvironment)Engine).FakeEntryAssembly.GetTypes();

        internal EeveeEntityData GetData(SceneEntityState entity)
        {
            if (entity != null && EeveeEntityDataLookup.CanUseFastLookup &&
                data.TryGetValue(entity, out var existing)) return existing;
            return data.GetValue(entity, value => new EeveeEntityData(this, value));
        }

        public bool TryGetExistingData(SceneEntityState entity, out EeveeEntityData value) =>
            data.TryGetValue(entity, out value);

        public string GetTypeName(Type type) =>
            (typeNames ??= new EeveeTypeNameCache(resolveTypeName.Method)).Get(type, Members, resolveTypeName);

        private string ResolveTypeName(Type type) =>
            Members.TryGetOriginalTypeName(type, out var name) ? name : type.Name;

        public void Clear()
        {
            typeNames?.Dispose();
            typeNames = null;
            Handlers.Clear();
            WhitelistedAnchors.Clear();
            BlacklistedAnchors.Clear();
            LegacyEntityHandlers.Clear();
            data = new();
            LiftSpeedFix = false;
            DecalStaticMoverFix = false;
        }
    }
}
