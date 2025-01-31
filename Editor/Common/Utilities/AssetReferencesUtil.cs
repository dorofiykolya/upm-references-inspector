﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Common.Utilities
{
    public class AssetReferencesUtil : EditorWindow
    {
        private static readonly Dictionary<string, HashSet<string>> _dependencyMap = new Dictionary<string, HashSet<string>>();
        private static readonly Dictionary<string, HashSet<string>> _referencesMap = new Dictionary<string, HashSet<string>>();
        private static readonly EditorApplication.ProjectWindowItemCallback ItemCallback = OnProjectWindowItemGUI;
        private static readonly List<Object> _resultAssets = new List<Object>();
        private static readonly List<string> _resultLog = new List<string>();

        private static string[] _allAssets;
        private static IEnumerator _process;
        private static bool _ready;
        
        private Vector2 _scroll;
        private int _index;

        public static string[] GetReferences(string assetPath)
        {
            _dependencyMap.Clear();
            _referencesMap.Clear();
            FillDependency(_dependencyMap, _referencesMap);
            Dictionary<string, HashSet<string>> map = _referencesMap;
            HashSet<string> set;
            if (map.TryGetValue(assetPath, out set))
            {
                return set.ToArray();
            }

            return new string[0];
        }

        [MenuItem("Assets/Tools/References")]
        private static void Open()
        {
            GetWindow<AssetReferencesUtil>("Reference").Show(true);

            if (EditorApplication.projectWindowItemOnGUI == null ||
                !EditorApplication.projectWindowItemOnGUI.GetInvocationList().Contains(ItemCallback))
            {
                EditorApplication.projectWindowItemOnGUI += ItemCallback;
            }
        }

        [InitializeOnLoadMethod]
        private static void ExecuteOnEditorReady()
        {
            if (EditorApplication.projectWindowItemOnGUI == null ||
                !EditorApplication.projectWindowItemOnGUI.GetInvocationList().Contains(ItemCallback))
            {
                EditorApplication.projectWindowItemOnGUI += ItemCallback;
            }
        }

        private static void OnProjectWindowItemGUI(string guid, Rect rect)
        {
            if (_referencesMap.Count != 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                HashSet<string> collection;
                if (_referencesMap.TryGetValue(path, out collection))
                {
                    rect.x = rect.xMax - 30f;
                    rect.width = 30f;
                    rect.y += 1f;
                    rect.height -= 2f;
                    rect.height = Math.Min(rect.height, 16f);
                    rect.width -= 1f;
                    var lastColor = GUI.color;
                    GUI.color = collection.Count > 1 ? Color.green : new Color(0.7f, 0.7f, 1f);
                    if (GUI.Button(rect, collection.Count.ToString(), EditorStyles.miniButton))
                    {
                        Selection.activeObject = AssetDatabase.LoadAssetAtPath<Object>(path);
                        Open();
                        CalculateReferences();
                    }

                    GUI.color = lastColor;
                }
            }
        }

        private void OnEnable()
        {
            EditorApplication.update += Repaint;
            if (EditorApplication.projectWindowItemOnGUI == null ||
                !EditorApplication.projectWindowItemOnGUI.GetInvocationList().Contains(ItemCallback))
            {
                EditorApplication.projectWindowItemOnGUI += ItemCallback;
            }
        }

        private void OnDisable()
        {
            EditorApplication.update -= Repaint;
        }

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            var rect = EditorGUILayout.GetControlRect(true);
            if (!_ready)
            {
                _allAssets = AssetDatabase.GetAllAssetPaths();
                _process = Process();
                EditorApplication.update += OnFrame;
                _ready = true;
            }
            else
            {
                if (_process != null)
                {
                    EditorGUI.ProgressBar(rect, _index / (float)_allAssets.Length,
                        $"processed: {_index}/{_allAssets.Length} ({(int)((_index / (float)_allAssets.Length) * 10000) / 100f}%)");
                }
                else
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Toggle(true, "☼ " + "<b>REFERENCES</b>", "dragtab", GUILayout.MinWidth(20f));
                    GUILayout.EndHorizontal();
                    EditorGUILayout.BeginHorizontal("ProgressBarBack");

                    if (GUILayout.Button("RESET", EditorStyles.miniButton, GUILayout.MaxWidth(100f)))
                    {
                        _ready = false;
                        _dependencyMap.Clear();
                        _referencesMap.Clear();
                    }

                    EditorGUILayout.Space();

                    if (Selection.activeObject != null)
                    {
                        if (GUILayout.Button("Find", EditorStyles.miniButton))
                        {
                            CalculateReferences();
                        }

                        if (GUILayout.Button("Select", EditorStyles.miniButton))
                        {
                            CalculateReferences();
                            Selection.objects = _resultAssets.ToArray();
                        }
                    }

                    EditorGUILayout.EndHorizontal();

                    var index = 0;
                    foreach (var log in _resultAssets)
                    {
                        EditorGUILayout.BeginVertical("ProgressBarBack");
                        GUILayout.Label(_resultLog[index]);
                        EditorGUILayout.ObjectField(log.name, log, typeof(Object), true);
                        EditorGUILayout.EndVertical();
                        index++;
                    }
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void OnFrame()
        {
            if (_process != null)
            {
                if (!_process.MoveNext())
                {
                    _process = null;
                    EditorApplication.update -= OnFrame;
                }

                Repaint();
            }
        }

        private IEnumerator Process()
        {
            _index = 0;
            yield return null;

            foreach (var asset in _allAssets)
            {
                HashSet<string> set;
                if (!_dependencyMap.TryGetValue(asset, out set))
                {
                    _dependencyMap[asset] = set = new HashSet<string>();
                }

                var dependencies = AssetDatabase.GetDependencies(asset);
                foreach (var dependency in dependencies)
                {
                    set.Add(dependency);
                    HashSet<string> references;
                    if (!_referencesMap.TryGetValue(dependency, out references))
                    {
                        _referencesMap[dependency] = references = new HashSet<string>();
                    }

                    references.Add(asset);
                }

                _index++;
                if ((_index % 20) == 0)
                {
                    yield return null;
                }
            }
        }

        private static void CalculateReferences()
        {
            string[] references;
            HashSet<string> set;
            if (_referencesMap.TryGetValue(AssetDatabase.GetAssetPath(Selection.activeObject), out set))
            {
                references = set.ToArray();
            }
            else
            {
                references = new string[0];
            }

            _resultLog.Clear();
            _resultAssets.Clear();
            foreach (var reference in references)
            {
                _resultLog.Add(reference);
                _resultAssets.Add(AssetDatabase.LoadAssetAtPath<Object>(reference));
            }
        }

        private static void FillDependency(Dictionary<string, HashSet<string>> dependencyMap,
          Dictionary<string, HashSet<string>> referencesMap)
        {
            var assets = AssetDatabase.GetAllAssetPaths();
            foreach (var asset in assets)
            {
                HashSet<string> set;
                if (!dependencyMap.TryGetValue(asset, out set))
                {
                    dependencyMap[asset] = set = new HashSet<string>();
                }

                var dependencies = AssetDatabase.GetDependencies(asset);
                foreach (var dependency in dependencies)
                {
                    set.Add(dependency);
                    HashSet<string> references;
                    if (!referencesMap.TryGetValue(dependency, out references))
                    {
                        referencesMap[dependency] = references = new HashSet<string>();
                    }

                    references.Add(asset);
                }
            }
        }
    }
}