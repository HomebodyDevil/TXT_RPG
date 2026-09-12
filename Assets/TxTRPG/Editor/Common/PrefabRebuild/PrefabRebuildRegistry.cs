using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace TxTRPG.Editor.Common.PrefabRebuild
{
    public enum PrefabRebuildCategory { Production, Demo }

    public sealed class PrefabRebuildTaskDescriptor
    {
        public string Id { get; }
        public string DisplayName { get; }
        public PrefabRebuildCategory Category { get; }
        public bool IncludedByDefault { get; }
        public IReadOnlyList<string> Inputs { get; }
        public IReadOnlyList<string> Outputs { get; }
        public IReadOnlyList<string> Dependencies { get; }
        public string Description { get; }
        public Action Execute { get; }
        public Func<IReadOnlyList<string>> Validate { get; }
        public Func<IReadOnlyList<string>> ValidateResult { get; }

        public PrefabRebuildTaskDescriptor(string id, string displayName,
            PrefabRebuildCategory category, bool includedByDefault,
            IEnumerable<string> inputs, IEnumerable<string> outputs,
            IEnumerable<string> dependencies, string description, Action execute,
            Func<IReadOnlyList<string>> validate = null,
            Func<IReadOnlyList<string>> validateResult = null)
        {
            Id = id?.Trim(); DisplayName = displayName?.Trim(); Category = category;
            IncludedByDefault = includedByDefault;
            Inputs = (inputs ?? Array.Empty<string>()).ToArray();
            Outputs = (outputs ?? Array.Empty<string>()).ToArray();
            Dependencies = (dependencies ?? Array.Empty<string>()).ToArray();
            Description = description ?? string.Empty; Execute = execute;
            Validate = validate ?? (() => Array.Empty<string>());
            ValidateResult = validateResult ?? (() => Array.Empty<string>());
        }
    }

    public sealed class PrefabRebuildExclusion
    {
        public string Id { get; }
        public string DisplayName { get; }
        public string Reason { get; }
        public PrefabRebuildExclusion(string id, string displayName, string reason)
        { Id = id; DisplayName = displayName; Reason = reason; }
    }

    public interface IPrefabRebuildTaskProvider
    {
        IEnumerable<PrefabRebuildTaskDescriptor> GetTasks();
        IEnumerable<PrefabRebuildExclusion> GetExclusions();
    }

    public sealed class PrefabRebuildRegistry
    {
        public IReadOnlyList<PrefabRebuildTaskDescriptor> Tasks { get; }
        public IReadOnlyList<PrefabRebuildExclusion> Exclusions { get; }

        private PrefabRebuildRegistry(IEnumerable<PrefabRebuildTaskDescriptor> tasks,
            IEnumerable<PrefabRebuildExclusion> exclusions)
        { Tasks = tasks.OrderBy(x => x.Id, StringComparer.Ordinal).ToArray(); Exclusions = exclusions.OrderBy(x => x.Id, StringComparer.Ordinal).ToArray(); }

        public static PrefabRebuildRegistry CreateForValidation(IEnumerable<PrefabRebuildTaskDescriptor> tasks,
            IEnumerable<PrefabRebuildExclusion> exclusions = null) =>
            new(tasks ?? Array.Empty<PrefabRebuildTaskDescriptor>(), exclusions ?? Array.Empty<PrefabRebuildExclusion>());

        public static PrefabRebuildRegistry Discover()
        {
            var tasks = new List<PrefabRebuildTaskDescriptor>();
            var exclusions = new List<PrefabRebuildExclusion>();
            foreach (var type in TypeCache.GetTypesDerivedFrom<IPrefabRebuildTaskProvider>().OrderBy(x => x.FullName, StringComparer.Ordinal))
            {
                if (type.IsAbstract || type.IsInterface) continue;
                var provider = (IPrefabRebuildTaskProvider)Activator.CreateInstance(type);
                tasks.AddRange(provider.GetTasks() ?? Array.Empty<PrefabRebuildTaskDescriptor>());
                exclusions.AddRange(provider.GetExclusions() ?? Array.Empty<PrefabRebuildExclusion>());
            }
            var registry = new PrefabRebuildRegistry(tasks, exclusions);
            var errors = registry.ValidateRegistry();
            if (errors.Count > 0) throw new InvalidOperationException(string.Join("\n", errors));
            return registry;
        }

        public IReadOnlyList<string> ValidateRegistry()
        {
            var errors = new List<string>();
            foreach (var group in Tasks.GroupBy(x => x.Id, StringComparer.Ordinal).Where(x => string.IsNullOrWhiteSpace(x.Key) || x.Count() > 1)) errors.Add($"Duplicate or empty task id: '{group.Key}'.");
            var ids = new HashSet<string>(Tasks.Select(x => x.Id), StringComparer.Ordinal);
            foreach (var task in Tasks)
            {
                if (string.IsNullOrWhiteSpace(task.DisplayName) || task.Execute == null) errors.Add($"Task '{task.Id}' is incomplete.");
                foreach (var path in task.Inputs.Concat(task.Outputs))
                    if (!IsAllowedProjectPath(path)) errors.Add($"Task '{task.Id}' declares invalid path '{path}'.");
                foreach (var dependency in task.Dependencies)
                    if (!ids.Contains(dependency)) errors.Add($"Task '{task.Id}' has unknown dependency '{dependency}'.");
            }
            foreach (var group in Tasks.SelectMany(task => task.Outputs.Select(path => (task.Id, Path: path))).GroupBy(x => x.Path, StringComparer.OrdinalIgnoreCase).Where(x => x.Count() > 1))
                errors.Add($"Output path has multiple owners: '{group.Key}'.");
            if (Tasks.Select(x => x.Id).Distinct(StringComparer.Ordinal).Count() == Tasks.Count)
                try { BuildPlan(Tasks.Select(x => x.Id)); } catch (InvalidOperationException exception) { errors.Add(exception.Message); }
            return errors;
        }

        public IReadOnlyList<PrefabRebuildTaskDescriptor> BuildPlan(IEnumerable<string> selectedIds)
        {
            var byId = Tasks.ToDictionary(x => x.Id, StringComparer.Ordinal);
            var result = new List<PrefabRebuildTaskDescriptor>();
            var visiting = new HashSet<string>(StringComparer.Ordinal);
            var visited = new HashSet<string>(StringComparer.Ordinal);
            void Visit(string id)
            {
                if (!byId.TryGetValue(id, out var task)) throw new InvalidOperationException($"Unknown prefab rebuild task '{id}'.");
                if (visited.Contains(id)) return;
                if (!visiting.Add(id)) throw new InvalidOperationException($"Prefab rebuild dependency cycle includes '{id}'.");
                foreach (var dependency in task.Dependencies.OrderBy(x => x, StringComparer.Ordinal)) Visit(dependency);
                visiting.Remove(id); visited.Add(id); result.Add(task);
            }
            foreach (var id in (selectedIds ?? Array.Empty<string>()).Distinct().OrderBy(x => x, StringComparer.Ordinal)) Visit(id);
            return result;
        }

        private static bool IsAllowedProjectPath(string path) => !string.IsNullOrWhiteSpace(path) && path.StartsWith("Assets/TxTRPG/", StringComparison.Ordinal) && !path.Contains("..") && !System.IO.Path.IsPathRooted(path);
    }
}
