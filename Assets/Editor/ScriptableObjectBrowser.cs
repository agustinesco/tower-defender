using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace TowerDefense.Editor
{
    public class ScriptableObjectBrowser : EditorWindow
    {
        private Dictionary<string, List<ScriptableObject>> _assetsByType = new();
        private List<string> _typeNames = new();
        private HashSet<string> _expandedTypes = new();
        private ScriptableObject _selectedAsset;
        private SerializedObject _serializedObject;

        // Table view state
        private string _selectedTypeName;
        private Dictionary<ScriptableObject, SerializedObject> _serializedObjectCache = new();

        private Vector2 _leftScroll;
        private Vector2 _rightScroll;
        private string _searchFilter = "";
        private float _leftPanelWidth = 250f;
        private bool _isResizing;

        private const float MinPanelWidth = 150f;
        private const float MaxPanelWidth = 400f;
        private const float SplitterWidth = 4f;
        private const float NameColumnWidth = 130f;
        private const float RowHeight = 22f;

        private static readonly HashSet<string> TableTypes = new() { "EnemyData", "TowerData", "UpgradeCard" };

        [MenuItem("Tools/ScriptableObject Browser")]
        public static void Open()
        {
            var window = GetWindow<ScriptableObjectBrowser>("SO Browser");
            window.minSize = new Vector2(500f, 300f);
        }

        private void OnEnable()
        {
            RefreshAssets();
        }

        private void OnFocus()
        {
            if (_selectedAsset == null && _serializedObject != null)
                _serializedObject = null;
        }

        private void RefreshAssets()
        {
            _assetsByType.Clear();
            _typeNames.Clear();
            _serializedObjectCache.Clear();

            string[] guids = AssetDatabase.FindAssets("t:ScriptableObject");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.StartsWith("Assets/"))
                    continue;

                var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                if (asset == null)
                    continue;

                string typeName = asset.GetType().Name;
                if (!_assetsByType.TryGetValue(typeName, out var list))
                {
                    list = new List<ScriptableObject>();
                    _assetsByType[typeName] = list;
                }
                list.Add(asset);
            }

            foreach (var list in _assetsByType.Values)
                list.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.OrdinalIgnoreCase));

            _typeNames = _assetsByType.Keys.OrderBy(k => k).ToList();

            if (_selectedAsset == null)
            {
                _serializedObject = null;
                _selectedTypeName = null;
            }
        }

        private SerializedObject GetCachedSerializedObject(ScriptableObject asset)
        {
            if (!_serializedObjectCache.TryGetValue(asset, out var so) || so.targetObject == null)
            {
                so = new SerializedObject(asset);
                _serializedObjectCache[asset] = so;
            }
            return so;
        }

        private void OnGUI()
        {
            DrawToolbar();

            EditorGUILayout.BeginHorizontal();
            DrawLeftPanel();
            DrawSplitter();
            DrawRightPanel();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            EditorGUI.BeginChangeCheck();
            _searchFilter = EditorGUILayout.TextField(_searchFilter, EditorStyles.toolbarSearchField,
                GUILayout.MinWidth(100f));
            if (EditorGUI.EndChangeCheck())
                Repaint();

            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(60f)))
                RefreshAssets();

            EditorGUILayout.EndHorizontal();
        }

        private void DrawLeftPanel()
        {
            _leftScroll = EditorGUILayout.BeginScrollView(_leftScroll,
                GUILayout.Width(_leftPanelWidth));

            bool hasSearch = !string.IsNullOrEmpty(_searchFilter);

            foreach (string typeName in _typeNames)
            {
                var assets = _assetsByType[typeName];

                List<ScriptableObject> filtered;
                if (hasSearch)
                {
                    filtered = assets.Where(a =>
                        a.name.IndexOf(_searchFilter, System.StringComparison.OrdinalIgnoreCase) >= 0)
                        .ToList();
                    if (filtered.Count == 0)
                        continue;
                }
                else
                {
                    filtered = assets;
                }

                string label = $"{typeName} ({filtered.Count})";
                bool isTableType = TableTypes.Contains(typeName);

                bool wasExpanded = _expandedTypes.Contains(typeName);
                bool expanded = hasSearch || wasExpanded;

                expanded = EditorGUILayout.Foldout(expanded, label, true, EditorStyles.foldoutHeader);

                if (expanded && !wasExpanded)
                    _expandedTypes.Add(typeName);
                else if (!expanded && wasExpanded)
                    _expandedTypes.Remove(typeName);

                if (!expanded)
                    continue;

                EditorGUI.indentLevel++;
                foreach (var asset in filtered)
                {
                    bool isSelected = isTableType
                        ? _selectedTypeName == typeName && _selectedAsset == asset
                        : _selectedAsset == asset;
                    var style = isSelected ? EditorStyles.boldLabel : EditorStyles.label;

                    Rect rect = EditorGUILayout.GetControlRect();
                    rect.x += EditorGUI.indentLevel * 15f;
                    rect.width -= EditorGUI.indentLevel * 15f;

                    // Highlight: for table types highlight all rows when type is selected
                    if (isTableType && _selectedTypeName == typeName)
                        EditorGUI.DrawRect(rect, new Color(0.24f, 0.48f, 0.9f, isSelected ? 0.25f : 0.08f));
                    else if (!isTableType && _selectedAsset == asset)
                        EditorGUI.DrawRect(rect, new Color(0.24f, 0.48f, 0.9f, 0.25f));

                    if (GUI.Button(rect, asset.name, style))
                    {
                        if (isTableType)
                        {
                            _selectedTypeName = typeName;
                            _selectedAsset = asset;
                            _serializedObject = null;
                        }
                        else
                        {
                            _selectedTypeName = null;
                            SelectAsset(asset);
                        }
                    }
                }
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawSplitter()
        {
            var splitterRect = EditorGUILayout.GetControlRect(false,
                GUILayout.Width(SplitterWidth), GUILayout.ExpandHeight(true));

            EditorGUI.DrawRect(splitterRect, new Color(0.12f, 0.12f, 0.12f, 1f));
            EditorGUIUtility.AddCursorRect(splitterRect, MouseCursor.ResizeHorizontal);

            if (Event.current.type == EventType.MouseDown && splitterRect.Contains(Event.current.mousePosition))
            {
                _isResizing = true;
                Event.current.Use();
            }

            if (_isResizing)
            {
                if (Event.current.type == EventType.MouseDrag)
                {
                    _leftPanelWidth = Mathf.Clamp(Event.current.mousePosition.x, MinPanelWidth, MaxPanelWidth);
                    Repaint();
                    Event.current.Use();
                }
                else if (Event.current.type == EventType.MouseUp)
                {
                    _isResizing = false;
                    Event.current.Use();
                }
            }
        }

        private void DrawRightPanel()
        {
            // Table view for table types
            if (_selectedTypeName != null && _assetsByType.ContainsKey(_selectedTypeName))
            {
                DrawTableView(_selectedTypeName);
                return;
            }

            // Single asset view for other types
            _rightScroll = EditorGUILayout.BeginScrollView(_rightScroll);

            if (_selectedAsset == null)
            {
                EditorGUILayout.LabelField("Select an asset from the left panel.", EditorStyles.centeredGreyMiniLabel);
                EditorGUILayout.EndScrollView();
                return;
            }

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(_selectedAsset.name, EditorStyles.boldLabel);
            if (GUILayout.Button("Ping", GUILayout.Width(40f)))
                EditorGUIUtility.PingObject(_selectedAsset);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField(_selectedAsset.GetType().Name, EditorStyles.miniLabel);
            EditorGUILayout.Space(4f);

            if (_serializedObject == null || _serializedObject.targetObject != _selectedAsset)
                _serializedObject = new SerializedObject(_selectedAsset);

            _serializedObject.Update();

            SerializedProperty prop = _serializedObject.GetIterator();
            if (prop.NextVisible(true))
            {
                do
                {
                    bool isScript = prop.name == "m_Script";
                    EditorGUI.BeginDisabledGroup(isScript);
                    EditorGUILayout.PropertyField(prop, true);
                    EditorGUI.EndDisabledGroup();
                }
                while (prop.NextVisible(false));
            }

            _serializedObject.ApplyModifiedProperties();

            EditorGUILayout.EndScrollView();
        }

        private void DrawTableView(string typeName)
        {
            var assets = _assetsByType[typeName];
            if (assets.Count == 0) return;

            // Build column info from first asset's properties
            var columns = BuildColumnInfo(assets[0]);
            if (columns.Count == 0) return;

            float totalWidth = NameColumnWidth + columns.Sum(c => c.width);
            float headerHeight = RowHeight + 2f;
            float totalHeight = headerHeight + assets.Count * RowHeight;

            // Header label
            EditorGUILayout.LabelField(typeName, EditorStyles.boldLabel);
            EditorGUILayout.Space(2f);

            _rightScroll = EditorGUILayout.BeginScrollView(_rightScroll);

            Rect contentRect = GUILayoutUtility.GetRect(totalWidth, totalHeight);
            float startX = contentRect.x;
            float y = contentRect.y;

            // --- Header row ---
            EditorGUI.DrawRect(new Rect(startX, y, totalWidth, headerHeight),
                new Color(0.18f, 0.18f, 0.18f, 0.6f));

            float x = startX;
            GUI.Label(new Rect(x + 4f, y + 2f, NameColumnWidth - 8f, RowHeight), "Name", EditorStyles.boldLabel);
            x += NameColumnWidth;

            // Column divider after name
            EditorGUI.DrawRect(new Rect(x - 1f, y, 1f, totalHeight), new Color(0.3f, 0.3f, 0.3f, 0.8f));

            foreach (var col in columns)
            {
                GUI.Label(new Rect(x + 4f, y + 2f, col.width - 8f, RowHeight), col.displayName, EditorStyles.miniBoldLabel);
                x += col.width;
                // Column divider
                EditorGUI.DrawRect(new Rect(x - 1f, y, 1f, totalHeight), new Color(0.3f, 0.3f, 0.3f, 0.4f));
            }

            // Header bottom border
            EditorGUI.DrawRect(new Rect(startX, y + headerHeight - 1f, totalWidth, 1f),
                new Color(0.4f, 0.4f, 0.4f, 0.8f));

            y += headerHeight;

            // --- Data rows ---
            for (int i = 0; i < assets.Count; i++)
            {
                var asset = assets[i];
                var so = GetCachedSerializedObject(asset);
                so.Update();

                Rect rowRect = new Rect(startX, y, totalWidth, RowHeight);

                // Alternate row background
                if (i % 2 == 1)
                    EditorGUI.DrawRect(rowRect, new Color(0.5f, 0.5f, 0.5f, 0.06f));

                // Highlight selected asset
                if (asset == _selectedAsset)
                    EditorGUI.DrawRect(rowRect, new Color(0.24f, 0.48f, 0.9f, 0.18f));

                x = startX;

                // Name cell - clickable to ping
                Rect nameRect = new Rect(x + 4f, y + 1f, NameColumnWidth - 8f, RowHeight - 2f);
                if (GUI.Button(nameRect, asset.name, EditorStyles.label))
                {
                    _selectedAsset = asset;
                    EditorGUIUtility.PingObject(asset);
                }
                x += NameColumnWidth;

                // Property cells
                foreach (var col in columns)
                {
                    var prop = so.FindProperty(col.name);
                    if (prop != null)
                    {
                        Rect cellRect = new Rect(x + 2f, y + 2f, col.width - 4f, EditorGUIUtility.singleLineHeight);
                        EditorGUI.PropertyField(cellRect, prop, GUIContent.none);
                    }
                    x += col.width;
                }

                so.ApplyModifiedProperties();
                y += RowHeight;
            }

            EditorGUILayout.EndScrollView();
        }

        private struct ColumnInfo
        {
            public string name;
            public string displayName;
            public float width;
        }

        private List<ColumnInfo> BuildColumnInfo(ScriptableObject asset)
        {
            var columns = new List<ColumnInfo>();
            var so = GetCachedSerializedObject(asset);
            var iter = so.GetIterator();

            if (!iter.NextVisible(true))
                return columns;

            do
            {
                if (iter.name == "m_Script") continue;

                // Skip arrays and complex nested types - they don't fit in table cells
                if (iter.isArray && iter.propertyType != SerializedPropertyType.String) continue;
                if (iter.propertyType == SerializedPropertyType.Generic) continue;

                float typeWidth = GetColumnWidth(iter.propertyType);
                float textWidth = EditorStyles.miniBoldLabel.CalcSize(new GUIContent(iter.displayName)).x + 16f;
                columns.Add(new ColumnInfo
                {
                    name = iter.name,
                    displayName = iter.displayName,
                    width = Mathf.Max(typeWidth, textWidth)
                });
            }
            while (iter.NextVisible(false));

            return columns;
        }

        private float GetColumnWidth(SerializedPropertyType type)
        {
            switch (type)
            {
                case SerializedPropertyType.Boolean: return 50f;
                case SerializedPropertyType.Integer: return 70f;
                case SerializedPropertyType.Float: return 75f;
                case SerializedPropertyType.String: return 130f;
                case SerializedPropertyType.Color: return 100f;
                case SerializedPropertyType.ObjectReference: return 140f;
                case SerializedPropertyType.Enum: return 115f;
                case SerializedPropertyType.Vector2: return 150f;
                case SerializedPropertyType.Vector3: return 190f;
                default: return 100f;
            }
        }

        private void SelectAsset(ScriptableObject asset)
        {
            _selectedAsset = asset;
            _serializedObject = new SerializedObject(asset);
            Repaint();
        }
    }
}
