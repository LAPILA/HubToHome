using System;
using UnityEditor;
using UnityEngine;

namespace HubToHome.EditorTools.ContentMaker
{
    // Styles are local to this editor. Never mutate EditorStyles or create textures per repaint.
    internal static class ContentMakerGUI
    {
        private static bool _ready;
        private static bool _dark;
        private static GUIStyle _card, _title, _muted, _section, _body, _textArea, _primary, _secondary, _foldout;

        public static GUIStyle Muted { get { EnsureStyles(); return _muted; } }
        public static GUIStyle SectionTitle { get { EnsureStyles(); return _section; } }
        public static GUIStyle Body { get { EnsureStyles(); return _body; } }
        public static GUIStyle TextArea { get { EnsureStyles(); return _textArea; } }

        public static IDisposable Card()
        {
            EnsureStyles();
            return new EditorGUILayout.VerticalScope(_card);
        }

        public static void Header(string title, string description)
        {
            EnsureStyles();
            EditorGUILayout.LabelField(title, _title);
            if (!string.IsNullOrEmpty(description)) EditorGUILayout.LabelField(description, _muted);
            EditorGUILayout.Space(10);
        }

        public static bool Foldout(bool open, string title)
        {
            EnsureStyles();
            return EditorGUILayout.Foldout(open, title, true, _foldout);
        }

        public static bool PrimaryButton(string text, params GUILayoutOption[] options)
        {
            EnsureStyles();
            Color previous = GUI.backgroundColor;
            try
            {
                GUI.backgroundColor = _dark ? new Color(0.30f, 0.76f, 0.66f) : new Color(0.12f, 0.52f, 0.44f);
                return GUILayout.Button(text, _primary, options);
            }
            finally { GUI.backgroundColor = previous; }
        }

        public static bool SecondaryButton(string text, params GUILayoutOption[] options)
        {
            EnsureStyles();
            return GUILayout.Button(text, _secondary, options);
        }

        public static IDisposable FormScope(float width) => new FormState(width);

        private sealed class FormState : IDisposable
        {
            private readonly float _labelWidth = EditorGUIUtility.labelWidth;
            private readonly bool _wideMode = EditorGUIUtility.wideMode;

            public FormState(float width)
            {
                EditorGUIUtility.labelWidth = Mathf.Clamp(width * 0.31f, 125f, 180f);
                EditorGUIUtility.wideMode = true;
            }

            public void Dispose()
            {
                EditorGUIUtility.labelWidth = _labelWidth;
                EditorGUIUtility.wideMode = _wideMode;
            }
        }

        private static void EnsureStyles()
        {
            if (_ready && _dark == EditorGUIUtility.isProSkin) return;
            _dark = EditorGUIUtility.isProSkin;
            _ready = true;
            Color muted = _dark ? new Color(0.70f, 0.74f, 0.77f) : new Color(0.34f, 0.39f, 0.42f);
            _card = new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(14, 14, 12, 14),
                margin = new RectOffset(0, 0, 0, 12)
            };
            _title = new GUIStyle(EditorStyles.boldLabel) { fontSize = 17, wordWrap = true, margin = new RectOffset(0, 0, 0, 5) };
            _muted = new GUIStyle(EditorStyles.wordWrappedLabel) { fontSize = 11, margin = new RectOffset(0, 0, 3, 5) };
            _muted.normal.textColor = muted;
            _section = new GUIStyle(EditorStyles.boldLabel) { fontSize = 12, wordWrap = true, margin = new RectOffset(0, 0, 3, 7) };
            _body = new GUIStyle(EditorStyles.wordWrappedLabel) { fontSize = 12 };
            _textArea = new GUIStyle(EditorStyles.textArea) { wordWrap = true, fontSize = 13, padding = new RectOffset(10, 10, 9, 9) };
            _secondary = new GUIStyle(GUI.skin.button) { fixedHeight = 28, fontSize = 11, padding = new RectOffset(10, 10, 4, 4) };
            _primary = new GUIStyle(_secondary) { fixedHeight = 32, fontStyle = FontStyle.Bold };
            _primary.normal.textColor = Color.white;
            _primary.hover.textColor = Color.white;
            _primary.active.textColor = Color.white;
            _primary.focused.textColor = Color.white;
            _foldout = new GUIStyle(EditorStyles.foldout) { fontSize = 12, fontStyle = FontStyle.Bold, fixedHeight = 26 };
        }
    }
}
