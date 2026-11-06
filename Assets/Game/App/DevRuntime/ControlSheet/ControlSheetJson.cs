#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.App.DevRuntime.ControlSheet
{
    /// <summary>The control sheet's two JSON documents (SPEC-70 DEV-029): the layout of every action, and the live state.</summary>
    internal static class ControlSheetJson
    {
        /// <summary>Every map and action of <paramref name="asset"/>, in asset order, with the label <paramref name="binding"/> gives each action path.</summary>
        internal static string Layout(InputActionAsset asset, Func<string, string> binding, int version)
        {
            var json = new StringBuilder(8192).Append("{\"version\":").Append(version).Append(",\"maps\":[");
            var maps = asset.actionMaps;
            for (int m = 0; m < maps.Count; m++)
            {
                if (m > 0) json.Append(',');
                json.Append("{\"name\":").Append(DevJsonWriter.Quote(maps[m].name)).Append(",\"actions\":[");
                var actions = maps[m].actions;
                for (int a = 0; a < actions.Count; a++)
                {
                    string path = maps[m].name + "/" + actions[a].name;
                    if (a > 0) json.Append(',');
                    json.Append("{\"path\":").Append(DevJsonWriter.Quote(path))
                        .Append(",\"name\":").Append(DevJsonWriter.Quote(actions[a].name))
                        .Append(",\"kind\":").Append(DevJsonWriter.Quote(Kind(actions[a])))
                        .Append(",\"binding\":").Append(DevJsonWriter.Quote(binding(path))).Append('}');
                }
                json.Append("]}");
            }
            return json.Append("]}").ToString();
        }

        /// <summary>The live state: enabled maps, each enabled action's pressed state and value, and the requested probes (null when unknown or failing).</summary>
        internal static string State(InputActionAsset asset, int layoutVersion, string context, string scheme, IReadOnlyList<string> probeNames, Func<string, string> probeJson)
        {
            var json = new StringBuilder(4096).Append("{\"layout\":").Append(layoutVersion)
                .Append(",\"context\":").Append(DevJsonWriter.Quote(context))
                .Append(",\"scheme\":").Append(DevJsonWriter.Quote(scheme)).Append(",\"active\":[");
            var maps = asset.actionMaps;
            bool first = true;
            for (int m = 0; m < maps.Count; m++)
            {
                if (!maps[m].enabled) continue;
                if (!first) json.Append(',');
                json.Append(DevJsonWriter.Quote(maps[m].name)); first = false;
            }
            json.Append("],\"actions\":{");
            first = true;
            for (int m = 0; m < maps.Count; m++)
            {
                if (!maps[m].enabled) continue;              // a disabled map's actions read idle
                var actions = maps[m].actions;
                for (int a = 0; a < actions.Count; a++)
                {
                    var action = actions[a];
                    if (!first) json.Append(',');
                    first = false;
                    json.Append(DevJsonWriter.Quote(maps[m].name + "/" + action.name)).Append(":{\"pressed\":").Append(action.IsPressed() ? "true" : "false");
                    // activeValueType is null while nothing drives the action; reading another type would throw.
                    var type = action.activeValueType;
                    if (type == typeof(float)) json.Append(",\"value\":").Append(DevJsonWriter.Number(action.ReadValue<float>()));
                    else if (type == typeof(Vector2))
                    {
                        var v = action.ReadValue<Vector2>();
                        json.Append(",\"value\":[").Append(DevJsonWriter.Number(v.x)).Append(',').Append(DevJsonWriter.Number(v.y)).Append(']');
                    }
                    json.Append('}');
                }
            }
            json.Append("},\"probes\":{");
            for (int i = 0; i < probeNames.Count; i++)
            {
                if (i > 0) json.Append(',');
                json.Append(DevJsonWriter.Quote(probeNames[i])).Append(':').Append(probeJson(probeNames[i]));
            }
            return json.Append("}}").ToString();
        }

        /// <summary>The page picks each control's default visual from this: a light, a bar or a stick dot.</summary>
        internal static string Kind(InputAction action)
        {
            switch (action.expectedControlType)
            {
                case "Vector2": case "Stick": case "Dpad": return "vector2";
                case "Axis": case "Analog": case "Integer": return "axis";
                case "Button": case "Key": return "button";
                default: return action.type == InputActionType.Button ? "button" : "value";
            }
        }
    }
}
#endif
