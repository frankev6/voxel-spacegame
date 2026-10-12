using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Game.Data;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Data.Validation;
using UnityEditor;
using UnityEngine;

namespace Game.Editor.Content
{
    /// <summary>The editor validation pass (SPEC-04 §5.7): catalog rules, every Validate (editor context), DATA-E010/E011/W012, and every
    /// IContentDatabaseValidator. Logs at most 200 lines per kind.</summary>
    public static class ContentValidatorRunner
    {
        const int MaxLogLines = 200;

        /// <summary>Rebuilds the database, validates it and logs the result. True when there are no errors.</summary>
        public static bool Run(out ValidationReport report)
        {
            report = Validate(ContentDatabaseBuilder.Rebuild());
            Log(report);
            return !report.HasErrors;
        }

        /// <summary>Validates without logging (tests and the CLI).</summary>
        public static ValidationReport Validate(ContentDatabase database)
        {
            var report = new ValidationReport("ContentDatabase");
            var defs = database != null ? database.Definitions : (IReadOnlyList<ContentDefinition>)Array.Empty<ContentDefinition>();
            var catalog = ContentCatalog.Build(defs, report);                                                     // step 1: §5.4
            var inDatabase = new HashSet<ContentDefinition>(defs.Where(d => d != null));
            foreach (var d in defs.Where(d => d != null).OrderBy(d => d.IdString, StringComparer.Ordinal))
            {
                string path = AssetDatabase.GetAssetPath(d);
                d.Validate(report.For(path), new ContentValidationContext(path, true, inDatabase.Contains, catalog));
                string file = Path.GetFileNameWithoutExtension(path);                                             // step 4: W012
                if (!string.IsNullOrEmpty(d.IdString) && !string.IsNullOrEmpty(file) && file != d.IdString)
                    report.Warning("DATA-W012", path, "id", $"File name '{file}' differs from the ID '{d.IdString}'.");
                CheckReferences(d, path, inDatabase, report);                                                     // step 3: E010
            }
            foreach (var guid in AssetDatabase.FindAssets("t:ContentDefinition", new[] { "Assets" }))           // step 2: E011
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.StartsWith(ContentDatabaseBuilder.ContentRoot + "/", StringComparison.Ordinal))
                    report.Error("DATA-E011", path, "", "Content definitions must live under Assets/Game/Content/.");
            }
            foreach (var type in TypeCache.GetTypesDerivedFrom<IContentDatabaseValidator>()                      // step 5
                         .Where(t => !t.IsAbstract && t.GetConstructor(Type.EmptyTypes) != null)
                         .Select(t => (IContentDatabaseValidator)Activator.CreateInstance(t))
                         .OrderBy(v => v.Order).ThenBy(v => v.GetType().FullName, StringComparer.Ordinal))
            {
                try { type.Validate(catalog, report); }
                catch (Exception e) { report.Error("DATA-E099", type.GetType().FullName, "", $"Validator threw {e.GetType().Name}: {e.Message}"); }
            }
            return report;
        }

        /// <summary>Step 6: one line per entry ("{Code} {AssetPath} {Field}: {Message}", the asset as context), at most 200 per kind, then a summary.</summary>
        public static void Log(ValidationReport report)
        {
            int errors = 0, warnings = 0;
            foreach (var e in report.Sorted())
            {
                var context = AssetDatabase.LoadMainAssetAtPath(e.AssetPath);
                string line = $"{e.Code} {e.AssetPath} {e.Field}: {e.Message}";
                if (e.Severity == ValidationSeverity.Error) { if (++errors <= MaxLogLines) Debug.LogError(line, context); }
                else if (e.Severity == ValidationSeverity.Warning) { if (++warnings <= MaxLogLines) Debug.LogWarning(line, context); }
            }
            if (errors > MaxLogLines) Debug.LogError($"… and {errors - MaxLogLines} more errors");
            if (warnings > MaxLogLines) Debug.LogWarning($"… and {warnings - MaxLogLines} more warnings");
            var summary = $"Content validation: {report.ErrorCount} errors, {report.WarningCount} warnings";
            if (report.HasErrors) Debug.LogError(summary); else Debug.Log(summary);
        }

        /// <summary>DATA-E010: every serialized reference to a ContentDefinition must point into the database (depth ≤ 4, arrays and lists included).</summary>
        static void CheckReferences(ContentDefinition owner, string path, HashSet<ContentDefinition> inDatabase, ValidationReport report)
        {
            void Visit(object value, string field, int depth)
            {
                if (value == null || depth > 4) return;
                if (value is ContentDefinition cd)
                {
                    if (depth > 0 && cd != null && !inDatabase.Contains(cd))
                        report.Error("DATA-E010", path, field, $"References '{cd.name}', which is not in the content database.");
                    if (depth > 0) return;
                }
                if (value is UnityEngine.Object && depth > 0) return;
                if (value is string) return;
                if (value is IList list) { for (int i = 0; i < list.Count; i++) Visit(list[i], $"{field}[{i}]", depth + 1); return; }
                var type = value.GetType();
                if (depth > 0 && !type.IsDefined(typeof(SerializableAttribute), false)) return;
                for (var t = type; t != null && t != typeof(ScriptableObject) && t != typeof(object); t = t.BaseType)
                    foreach (var f in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    {
                        if (f.IsNotSerialized) continue;
                        bool serialized = f.IsPublic || f.IsDefined(typeof(SerializeField), false) || f.IsDefined(typeof(SerializeReference), false);
                        if (!serialized || f.FieldType.IsPrimitive || f.FieldType.IsEnum) continue;
                        Visit(f.GetValue(value), string.IsNullOrEmpty(field) ? f.Name : field + "." + f.Name, depth + 1);
                    }
            }
            Visit(owner, "", 0);
        }
    }
}
