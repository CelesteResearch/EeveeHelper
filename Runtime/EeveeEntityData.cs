using System;
using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using System.Reflection;
using Celeste.Core.Scheduling;
using Celeste.Core.Reflection.Members;
using Celeste.Scene.Entities;

namespace Celeste.Mods.EeveeHelper.Runtime
{
    public sealed class EeveeEntityData : IEnumerable<KeyValuePair<string, object>>
    {
        private readonly EeveeContainerRuntime runtime;
        private readonly SceneEntityState entity;
        private readonly Dictionary<string, EeveeMemberAccessor> accessors = new();
        public Dictionary<string, object> Extras { get; } = new();

        internal EeveeEntityData(EeveeContainerRuntime runtime, SceneEntityState entity)
        {
            this.runtime = runtime;
            this.entity = entity;
        }

        public static EeveeEntityData For(ISourceEngineContext engine, SceneEntityState entity) =>
            EeveeContainerRuntime.For(engine).GetData(entity);

        private Type GetBaseType(Type type)
        {
            if (!runtime.Members.ContainsSourceType(type)) return type.BaseType;
            var definition = runtime.Members.GetOriginalBaseTypeDefinition(
                runtime.Members.GetOriginalTypeDefinition(type));
            return definition == null || definition.Identity == "System.Object"
                ? null
                : runtime.Members.GetActualSourceType(definition.AssemblyIdentity, definition.Identity);
        }

        private EeveeMemberAccessor Find(string name)
        {
            if (accessors.TryGetValue(name, out var cached)) return cached;
            for (Type type = entity.GetType(); type != null; type = GetBaseType(type))
            {
                if (runtime.Members.ContainsSourceType(type))
                {
                    foreach (var definition in runtime.Members.GetOriginalMemberDefinitions(type, true))
                        if (definition.Name == name && definition.CanRead && !definition.IsStatic)
                        {
                            var binding = runtime.Members.RequireBinding(type, definition);
                            return accessors[name] = new EeveeMemberAccessor
                                { Read = () => binding.Read(entity), Write = value => binding.Write(entity, value) };
                        }
                }
                else
                {
                    const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic |
                                               BindingFlags.DeclaredOnly;
                    if (type.GetField(name, flags) is { } field)
                        return accessors[name] = new EeveeMemberAccessor
                            { Read = () => field.GetValue(entity), Write = value => field.SetValue(entity, value) };
                    if (type.GetProperty(name, flags) is { CanRead: true } property &&
                        property.GetIndexParameters().Length == 0)
                        return accessors[name] = new EeveeMemberAccessor
                        {
                            Read = () => property.GetValue(entity), Write = value => property.SetValue(entity, value)
                        };
                }
            }

            return accessors[name] = null;
        }

        public bool TryGet<T>(string name, out T value)
        {
            bool found = TryGet(name, out object result);
            value = found ? (T)result : default;
            return found;
        }

        public bool TryGet(string name, out object value)
        {
            var accessor = Find(name);
            if (accessor != null)
            {
                value = accessor.Read();
                return true;
            }

            return Extras.TryGetValue(name, out value);
        }

        public T Get<T>(string name)
        {
            TryGet<T>(name, out var value);
            return value;
        }

        public T Set<T>(string name, T value)
        {
            var accessor = Find(name);
            if (accessor != null) accessor.Write(value);
            else Extras[name] = value;
            return value;
        }

        public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
        {
            var seen = new HashSet<string>();
            for (Type type = entity.GetType(); type != null; type = GetBaseType(type))
            {
                if (runtime.Members.ContainsSourceType(type))
                {
                    foreach (var definition in runtime.Members.GetOriginalMemberDefinitions(type, true))
                        if (!definition.IsStatic && definition.CanRead && definition.IndexParameterCount == 0 &&
                            definition.ValueTypeIdentity.EndsWith(".Vector2", StringComparison.Ordinal) &&
                            seen.Add(definition.Name))
                            yield return new(definition.Name, Get<Vector2>(definition.Name));
                }
                else
                {
                    const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic |
                                               BindingFlags.DeclaredOnly;
                    foreach (var field in type.GetFields(flags))
                        if (field.FieldType == typeof(Vector2) && seen.Add(field.Name))
                            yield return new(field.Name, field.GetValue(entity));
                    foreach (var property in type.GetProperties(flags))
                        if (property.PropertyType == typeof(Vector2) && property.CanRead &&
                            property.GetIndexParameters().Length == 0 && seen.Add(property.Name))
                            yield return new(property.Name, property.GetValue(entity));
                }
            }

            foreach (var pair in Extras)
                if (seen.Add(pair.Key))
                    yield return pair;
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
