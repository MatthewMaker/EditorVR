﻿#if UNITY_EDITOR
using UnityEngine;

namespace Unity.Labs.EditorXR.Editor
{
    abstract class EditorEventArgs
    {
        public string name;
        public override string ToString() { return name; }
    }

    class ExrStartStopArgs : EditorEventArgs
    {
        public bool active;
        public bool play_mode;

        public ExrStartStopArgs(bool active, bool playMode)
        {
            this.active = active;
            play_mode = playMode;
        }

        public override string ToString() { return $"{name}, {active}, play mode: {play_mode}"; }
    }

    class UiComponentArgs : EditorEventArgs
    {
        public string label;
        public bool active;

        public UiComponentArgs(string label, bool active)
        {
            this.label = label;
            this.active = active;
        }

        public override string ToString() { return $"{name}, {label}, {active}"; }
    }

    class SelectToolArgs : EditorEventArgs
    {
        public string label;

        public override string ToString() { return $"{name}, {label}"; }
    }

    static class EditorXREvents
    {
        internal const string k_TopLevelName = "editorxr";

        public static EditorXREvent<SelectToolArgs> ToolSelected =
            new EditorXREvent<SelectToolArgs>(k_TopLevelName, "toolUsed");

        public static EditorXREvent<ExrStartStopArgs> StartStop =
            new EditorXREvent<ExrStartStopArgs>(k_TopLevelName, "startStop");

        public static EditorXREvent<UiComponentArgs> WorkspaceState =
            new EditorXREvent<UiComponentArgs>(k_TopLevelName, "workspaceState");
    }
}
#endif
