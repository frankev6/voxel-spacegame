using System;
using System.Collections.Generic;
using Game.Data.Blocks;
using Game.Data.Definitions;
using Game.Data.Validation;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Tests.EditMode.Data
{
    /// <summary>Creates content definitions in memory (ScriptableObject.CreateInstance + SerializedObject; no assets) for tests (SPEC-04 DATA-004).
    /// Call <see cref="DestroyAll"/> in TearDown.</summary>
    public static class TestContentFactory
    {
        static readonly List<Object> Created = new List<Object>();

        public static T Create<T>(string id, Action<SerializedObject> edit = null) where T : ContentDefinition
        {
            var d = ScriptableObject.CreateInstance<T>();
            d.name = id;
            Created.Add(d);
            var so = new SerializedObject(d);
            Set(so, "id", id);
            edit?.Invoke(so);
            so.ApplyModifiedPropertiesWithoutUndo();
            return d;
        }

        /// <summary>A valid plain item: display name, icon, category Misc.</summary>
        public static ItemDefinition Item(string id, Action<SerializedObject> edit = null)
            => Create<ItemDefinition>(id, so => { Set(so, "displayName", "Test item"); Set(so, "icon", Icon()); edit?.Invoke(so); });

        /// <summary>A valid block of the given kind: Structural = 1 cell, category Structure; Prop = mesh, Detail;
        /// Functional = mesh, Power, functionHp 50 and one PowerStorage module (unless <paramref name="modules"/> is given).</summary>
        public static BlockDefinition Block(string id, BlockKind kind, Vector3Int[] cells = null, BlockModule[] modules = null, Action<SerializedObject> edit = null)
            => Create<BlockDefinition>(id, so =>
            {
                Set(so, "displayName", "Test block"); Set(so, "icon", Icon()); Set(so, "category", (int)ItemCategory.Block);
                Set(so, "kind", (int)kind);
                if (cells != null) Set(so, "cells", cells);
                switch (kind)
                {
                    case BlockKind.Structural: Set(so, "blockCategory", (int)BlockCategory.Structure); break;
                    case BlockKind.Prop: Set(so, "blockCategory", (int)BlockCategory.Detail); Set(so, "propMesh", Mesh()); break;
                    default:
                        Set(so, "blockCategory", (int)BlockCategory.Power); Set(so, "propMesh", Mesh()); Set(so, "functionHp", 50f);
                        Set(so, "modules", modules ?? new BlockModule[] { new PowerStorageModule { Params = new PowerStorageParams { Storage = 100f } } });
                        break;
                }
                if (kind != BlockKind.Functional && modules != null) Set(so, "modules", modules);
                edit?.Invoke(so);
            });

        public static Sprite Icon()
        {
            var tex = new Texture2D(2, 2); Created.Add(tex);
            var s = Sprite.Create(tex, new Rect(0, 0, 2, 2), Vector2.zero); Created.Add(s);
            return s;
        }

        public static Mesh Mesh() { var m = new Mesh(); Created.Add(m); return m; }

        /// <summary>Sets a serialized field by path. Enums and bytes take int values; arrays take Vector3Int[] or BlockModule[].</summary>
        public static void Set(SerializedObject so, string path, object value)
        {
            var p = so.FindProperty(path) ?? throw new ArgumentException($"no serialized field '{path}' on {so.targetObject.GetType().Name}");
            switch (value)
            {
                case string s: p.stringValue = s; break;
                case int i: p.intValue = i; break;
                case uint u: p.longValue = u; break;
                case float f: p.floatValue = f; break;
                case bool b: p.boolValue = b; break;
                case Object o: p.objectReferenceValue = o; break;
                case null: if (p.propertyType == SerializedPropertyType.ObjectReference) p.objectReferenceValue = null; else p.managedReferenceValue = null; break;
                case Vector3Int[] cells:
                    p.arraySize = cells.Length;
                    for (int k = 0; k < cells.Length; k++) p.GetArrayElementAtIndex(k).vector3IntValue = cells[k];
                    break;
                case BlockModule[] modules:
                    p.arraySize = modules.Length;
                    for (int k = 0; k < modules.Length; k++) p.GetArrayElementAtIndex(k).managedReferenceValue = modules[k];
                    break;
                default: throw new ArgumentException($"unsupported value type {value.GetType().Name} for '{path}'");
            }
        }

        /// <summary>Applies one more edit to an existing definition.</summary>
        public static void Edit(ContentDefinition d, Action<SerializedObject> edit)
        {
            var so = new SerializedObject(d);
            edit(so);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Runs the definition's Validate into a fresh report bound to "test:&lt;id&gt;".</summary>
        public static ValidationReport Validate(ContentDefinition d)
        {
            var r = new ValidationReport("test");
            d.Validate(r.For("test:" + d.IdString), null);
            return r;
        }

        public static void DestroyAll()
        {
            foreach (var o in Created) if (o != null) Object.DestroyImmediate(o);
            Created.Clear();
        }
    }
}
