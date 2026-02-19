using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using TowerDefense.Data;

namespace TowerDefense.Editor
{
    public class SpawnRateVisualizer : EditorWindow
    {
        // --- Data ---
        private ContinuousDifficulty _config;
        private SerializedObject _serializedObject;

        // --- Simulation ---
        private struct SpawnEvent
        {
            public float time;
            public EnemyType enemyType;
            public int blockIndex;
            public float healthMul;
            public float speedMul;
            public int spawnIndex;
            public float laneOffset; // 0..1 random Y position
        }

        private List<SpawnEvent> _events = new();
        private float _maxSimTime = 600f;
        private float _currentTime;
        private bool _dirty = true;

        // --- Playback ---
        private bool _playing;
        private float _playSpeed = 1f;
        private double _lastEditorTime;

        // --- Sprite lookup (EnemyType -> Sprite from EnemyData SOs) ---
        private Dictionary<EnemyType, Sprite> _enemySprites;
        private Dictionary<EnemyType, Texture2D> _spriteTextures;

        // --- Layout ---
        private float _leftPanelWidth = 280f;
        private bool _isResizing;
        private Vector2 _leftScroll;

        private const float MinPanelWidth = 200f;
        private const float MaxPanelWidth = 450f;
        private const float SplitterWidth = 4f;
        private const float ToolbarHeight = 22f;
        private const float TimelineHeight = 60f;
        private const float LegendHeight = 24f;
        private const float HallwayLength = 20f; // virtual length in "units"

        // --- Enemy colors (match Enemy.SetupVisuals) ---
        private static readonly Dictionary<EnemyType, Color> EnemyColors = new()
        {
            { EnemyType.Ground, Color.red },
            { EnemyType.Flying, new Color(0.9f, 0.6f, 0.1f) },
            { EnemyType.Cart, new Color(0.55f, 0.35f, 0.15f) },
            { EnemyType.Goblin, new Color(0.2f, 0.8f, 0.2f) },
            { EnemyType.Tank, new Color(0.5f, 0.5f, 0.55f) }
        };

        // --- Block tint colors for timeline bands ---
        private static readonly Color[] BlockTints = new[]
        {
            new Color(0.3f, 0.5f, 0.8f, 0.25f),
            new Color(0.8f, 0.4f, 0.3f, 0.25f),
            new Color(0.3f, 0.7f, 0.4f, 0.25f),
            new Color(0.7f, 0.6f, 0.2f, 0.25f),
            new Color(0.6f, 0.3f, 0.7f, 0.25f),
            new Color(0.3f, 0.7f, 0.7f, 0.25f),
        };

        [MenuItem("Tools/Spawn Rate Visualizer")]
        public static void Open()
        {
            var window = GetWindow<SpawnRateVisualizer>("Spawn Visualizer");
            window.minSize = new Vector2(700f, 400f);
        }

        private void OnEnable()
        {
            _dirty = true;
            RebuildSpriteLookup();
            EditorApplication.update += OnEditorUpdate;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
        }

        private void OnEditorUpdate()
        {
            if (!_playing) return;

            double now = EditorApplication.timeSinceStartup;
            float dt = (float)(now - _lastEditorTime);
            _lastEditorTime = now;

            _currentTime += dt * _playSpeed;
            if (_currentTime >= _maxSimTime)
            {
                _currentTime = _maxSimTime;
                _playing = false;
            }

            Repaint();
        }

        private void OnGUI()
        {
            DrawToolbar();

            if (_config == null)
            {
                EditorGUILayout.HelpBox("Assign a ContinuousDifficulty asset above to begin.", MessageType.Info);
                return;
            }

            if (_dirty)
            {
                RecomputeEvents();
                _dirty = false;
            }

            Rect body = new Rect(0, ToolbarHeight + 4f, position.width, position.height - ToolbarHeight - 4f);
            GUILayout.BeginArea(body);
            EditorGUILayout.BeginHorizontal();

            // Left panel
            DrawLeftPanel(body.height);

            // Splitter
            DrawSplitter(body.height);

            // Right panel (hallway + timeline)
            float rightWidth = body.width - _leftPanelWidth - SplitterWidth;
            if (rightWidth > 50f)
                DrawRightPanel(rightWidth, body.height);

            EditorGUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        // ===== TOOLBAR =====
        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            EditorGUI.BeginChangeCheck();
            _config = (ContinuousDifficulty)EditorGUILayout.ObjectField(
                _config, typeof(ContinuousDifficulty), false, GUILayout.Width(220f));
            if (EditorGUI.EndChangeCheck())
            {
                _serializedObject = _config != null ? new SerializedObject(_config) : null;
                _dirty = true;
                _currentTime = 0f;
            }

            GUILayout.Label("Max Time:", GUILayout.Width(60f));
            EditorGUI.BeginChangeCheck();
            _maxSimTime = EditorGUILayout.FloatField(_maxSimTime, GUILayout.Width(50f));
            if (EditorGUI.EndChangeCheck())
            {
                _maxSimTime = Mathf.Max(10f, _maxSimTime);
                _dirty = true;
            }

            if (GUILayout.Button("Recompute", EditorStyles.toolbarButton, GUILayout.Width(75f)))
                _dirty = true;

            GUILayout.Space(12f);

            if (GUILayout.Button(_playing ? "||" : "\u25B6", EditorStyles.toolbarButton, GUILayout.Width(28f)))
            {
                _playing = !_playing;
                if (_playing)
                {
                    _lastEditorTime = EditorApplication.timeSinceStartup;
                    if (_currentTime >= _maxSimTime)
                        _currentTime = 0f;
                }
            }

            GUILayout.Label("Speed:", GUILayout.Width(40f));
            _playSpeed = EditorGUILayout.FloatField(_playSpeed, GUILayout.Width(35f));
            _playSpeed = Mathf.Clamp(_playSpeed, 0.1f, 50f);

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        // ===== LEFT PANEL =====
        private void DrawLeftPanel(float height)
        {
            _leftScroll = EditorGUILayout.BeginScrollView(_leftScroll,
                GUILayout.Width(_leftPanelWidth), GUILayout.Height(height));

            if (_serializedObject != null)
            {
                _serializedObject.Update();

                EditorGUILayout.LabelField("Configuration", EditorStyles.boldLabel);
                EditorGUILayout.Space(2f);

                SerializedProperty prop = _serializedObject.GetIterator();
                if (prop.NextVisible(true))
                {
                    do
                    {
                        bool isScript = prop.name == "m_Script";
                        EditorGUI.BeginDisabledGroup(isScript);
                        EditorGUI.BeginChangeCheck();
                        EditorGUILayout.PropertyField(prop, true);
                        if (EditorGUI.EndChangeCheck())
                            _dirty = true;
                        EditorGUI.EndDisabledGroup();
                    }
                    while (prop.NextVisible(false));
                }

                _serializedObject.ApplyModifiedProperties();
            }

            // Current block info
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Current Block Info", EditorStyles.boldLabel);
            DrawBlockInfo();

            EditorGUILayout.EndScrollView();
        }

        private void DrawBlockInfo()
        {
            if (_config == null || _config.blocks == null || _config.blocks.Count == 0)
            {
                EditorGUILayout.HelpBox("No blocks defined.", MessageType.Warning);
                return;
            }

            int blockIndex;
            int lastBlockCycles;
            GetBlockAtTime(_currentTime, out blockIndex, out lastBlockCycles);

            var block = _config.blocks[blockIndex];
            bool isLast = blockIndex == _config.blocks.Count - 1;

            EditorGUILayout.LabelField($"Block: \"{block.blockName}\" ({blockIndex + 1}/{_config.blocks.Count})");

            float healthMul = block.healthMultiplier;
            float speedMul = block.speedMultiplier;
            float goldMul = block.goldMultiplier;
            if (isLast && lastBlockCycles > 0)
            {
                healthMul += lastBlockCycles * (block.healthMultiplier - 1f);
                speedMul += lastBlockCycles * (block.speedMultiplier - 1f);
                goldMul += lastBlockCycles * (block.goldMultiplier - 1f);
            }

            EditorGUILayout.LabelField($"Health: x{healthMul:F1}  Speed: x{speedMul:F1}  Gold: x{goldMul:F1}");
            EditorGUILayout.LabelField($"Spawn interval: {block.spawnInterval:F2}s");

            float enemiesPerMin = block.spawnInterval > 0f ? 60f / block.spawnInterval : 0f;
            EditorGUILayout.LabelField($"Enemies/min: {enemiesPerMin:F0}");

            if (isLast && lastBlockCycles > 0)
                EditorGUILayout.LabelField($"Last-block cycle: {lastBlockCycles}", EditorStyles.miniLabel);
        }

        // ===== SPLITTER =====
        private void DrawSplitter(float height)
        {
            var rect = EditorGUILayout.GetControlRect(false,
                GUILayout.Width(SplitterWidth), GUILayout.Height(height));
            EditorGUI.DrawRect(rect, new Color(0.12f, 0.12f, 0.12f, 1f));
            EditorGUIUtility.AddCursorRect(rect, MouseCursor.ResizeHorizontal);

            if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
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

        // ===== RIGHT PANEL =====
        private void DrawRightPanel(float width, float totalHeight)
        {
            Rect area = EditorGUILayout.GetControlRect(false,
                GUILayout.Width(width), GUILayout.Height(totalHeight));

            float hallwayHeight = area.height - TimelineHeight - LegendHeight;
            if (hallwayHeight < 40f) hallwayHeight = 40f;

            Rect hallwayRect = new Rect(area.x, area.y, area.width, hallwayHeight);
            Rect legendRect = new Rect(area.x, area.y + hallwayHeight, area.width, LegendHeight);
            Rect timelineRect = new Rect(area.x, area.y + hallwayHeight + LegendHeight, area.width, TimelineHeight);

            DrawHallway(hallwayRect);
            DrawLegend(legendRect);
            DrawTimeline(timelineRect);
        }

        // ===== SPRITES =====
        private void RebuildSpriteLookup()
        {
            _enemySprites = new Dictionary<EnemyType, Sprite>();
            _spriteTextures = new Dictionary<EnemyType, Texture2D>();

            var guids = AssetDatabase.FindAssets("t:EnemyData");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var data = AssetDatabase.LoadAssetAtPath<EnemyData>(path);
                if (data == null || data.sprite == null) continue;
                if (_enemySprites.ContainsKey(data.enemyType)) continue;

                _enemySprites[data.enemyType] = data.sprite;
                _spriteTextures[data.enemyType] = data.sprite.texture;
            }
        }

        /// <summary>
        /// Returns the UV rect for a sprite within its atlas texture,
        /// suitable for GUI.DrawTextureWithTexCoords.
        /// </summary>
        private Rect GetSpriteTexCoords(Sprite sprite)
        {
            var texRect = sprite.textureRect;
            var tex = sprite.texture;
            return new Rect(
                texRect.x / tex.width,
                texRect.y / tex.height,
                texRect.width / tex.width,
                texRect.height / tex.height);
        }

        // ===== HALLWAY =====
        private void DrawHallway(Rect rect)
        {
            EditorGUI.DrawRect(rect, new Color(0.15f, 0.15f, 0.18f, 1f));

            // Direction arrow label
            GUI.Label(new Rect(rect.x + 4f, rect.y + 2f, 100f, 16f), ">>> travel >>>",
                EditorStyles.miniLabel);

            float margin = 10f; // top/bottom padding so circles don't clip
            float usableHeight = rect.height - margin * 2f;

            // Draw spawned enemies visible in the hallway at _currentTime
            for (int i = 0; i < _events.Count; i++)
            {
                var evt = _events[i];
                if (evt.time > _currentTime) break; // events are sorted

                float age = _currentTime - evt.time;
                float normalizedX = age * evt.speedMul / HallwayLength;

                if (normalizedX < 0f || normalizedX > 1f) continue;

                float x = rect.x + normalizedX * rect.width;
                float y = rect.y + margin + evt.laneOffset * usableHeight;

                float size = (6f + Mathf.Log(evt.healthMul + 1f) * 2f) * 2f;

                Rect drawRect = new Rect(x - size * 0.5f, y - size * 0.5f, size, size);

                if (_enemySprites != null && _enemySprites.TryGetValue(evt.enemyType, out var sprite) && sprite != null)
                {
                    var texCoords = GetSpriteTexCoords(sprite);
                    // Maintain aspect ratio from sprite
                    float aspect = sprite.textureRect.width / sprite.textureRect.height;
                    if (aspect > 1f)
                        drawRect.height = drawRect.width / aspect;
                    else
                        drawRect.width = drawRect.height * aspect;

                    GUI.DrawTextureWithTexCoords(drawRect, sprite.texture, texCoords);
                }
                else
                {
                    // Fallback: colored square
                    Color color = EnemyColors.TryGetValue(evt.enemyType, out var c) ? c : Color.white;
                    EditorGUI.DrawRect(drawRect, color);
                    DrawRectOutline(drawRect, Color.black);
                }
            }
        }

        private void DrawRectOutline(Rect r, Color color)
        {
            EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, 1f), color);
            EditorGUI.DrawRect(new Rect(r.x, r.yMax - 1f, r.width, 1f), color);
            EditorGUI.DrawRect(new Rect(r.x, r.y, 1f, r.height), color);
            EditorGUI.DrawRect(new Rect(r.xMax - 1f, r.y, 1f, r.height), color);
        }

        // ===== LEGEND =====
        private void DrawLegend(Rect rect)
        {
            EditorGUI.DrawRect(rect, new Color(0.18f, 0.18f, 0.2f, 1f));

            float x = rect.x + 8f;
            float y = rect.y + 2f;
            float swatchSize = 18f;

            foreach (var kvp in EnemyColors)
            {
                Rect swatchRect = new Rect(x, y, swatchSize, swatchSize);

                if (_enemySprites != null && _enemySprites.TryGetValue(kvp.Key, out var sprite) && sprite != null)
                {
                    var texCoords = GetSpriteTexCoords(sprite);
                    GUI.DrawTextureWithTexCoords(swatchRect, sprite.texture, texCoords);
                }
                else
                {
                    EditorGUI.DrawRect(swatchRect, kvp.Value);
                    DrawRectOutline(swatchRect, Color.black);
                }

                x += swatchSize + 2f;
                GUI.Label(new Rect(x, y + 1f, 60f, 16f), kvp.Key.ToString(), EditorStyles.miniLabel);
                x += 56f;
            }
        }

        // ===== TIMELINE =====
        private void DrawTimeline(Rect rect)
        {
            EditorGUI.DrawRect(rect, new Color(0.2f, 0.2f, 0.22f, 1f));

            if (_config == null || _config.blocks == null) return;

            float sliderY = rect.y + 4f;
            float sliderH = 18f;

            // Slider
            EditorGUI.BeginChangeCheck();
            _currentTime = GUI.HorizontalSlider(
                new Rect(rect.x + 4f, sliderY, rect.width - 8f, sliderH),
                _currentTime, 0f, _maxSimTime);
            if (EditorGUI.EndChangeCheck())
                Repaint();

            // Time label
            GUI.Label(new Rect(rect.x + 4f, sliderY + sliderH, 200f, 16f),
                $"t = {_currentTime:F1}s  ({_currentTime / 60f:F1} min)",
                EditorStyles.miniLabel);

            // Block bands
            float bandY = sliderY + sliderH + 16f;
            float bandH = rect.yMax - bandY - 2f;
            if (bandH < 4f) return;

            float elapsed = 0f;
            for (int i = 0; i < _config.blocks.Count; i++)
            {
                var block = _config.blocks[i];
                bool isLast = i == _config.blocks.Count - 1;
                float duration = (isLast || block.duration <= 0f)
                    ? _maxSimTime - elapsed
                    : Mathf.Min(block.duration, _maxSimTime - elapsed);

                if (duration <= 0f) break;

                float startNorm = elapsed / _maxSimTime;
                float endNorm = (elapsed + duration) / _maxSimTime;

                Rect bandRect = new Rect(
                    rect.x + startNorm * rect.width,
                    bandY,
                    (endNorm - startNorm) * rect.width,
                    bandH);

                Color tint = BlockTints[i % BlockTints.Length];
                EditorGUI.DrawRect(bandRect, tint);

                // Block boundary line
                if (i > 0)
                {
                    float lineX = rect.x + startNorm * rect.width;
                    EditorGUI.DrawRect(new Rect(lineX, bandY, 1f, bandH), new Color(1f, 1f, 1f, 0.4f));
                }

                // Block label
                string label = string.IsNullOrEmpty(block.blockName) ? $"B{i + 1}" : block.blockName;
                if (isLast) label += " >>";
                var labelSize = EditorStyles.miniLabel.CalcSize(new GUIContent(label));
                if (bandRect.width > labelSize.x + 4f)
                {
                    GUI.Label(new Rect(bandRect.x + 2f, bandRect.y, bandRect.width, bandRect.height),
                        label, EditorStyles.miniLabel);
                }

                elapsed += duration;
                if (elapsed >= _maxSimTime) break;
            }

            // Playhead
            float playheadNorm = _currentTime / _maxSimTime;
            float playheadX = rect.x + playheadNorm * rect.width;
            EditorGUI.DrawRect(new Rect(playheadX, bandY, 2f, bandH), Color.yellow);

            // Spawn density ticks
            float tickY = bandY - 3f;
            for (int i = 0; i < _events.Count; i++)
            {
                var evt = _events[i];
                if (evt.time > _maxSimTime) break;
                float norm = evt.time / _maxSimTime;
                float tx = rect.x + norm * rect.width;
                Color col = EnemyColors.TryGetValue(evt.enemyType, out var c) ? c : Color.white;
                col.a = 0.6f;
                EditorGUI.DrawRect(new Rect(tx, tickY, 1f, 3f), col);
            }
        }

        // ===== SIMULATION =====
        private void RecomputeEvents()
        {
            _events.Clear();

            if (_config == null || _config.blocks == null || _config.blocks.Count == 0)
                return;

            var rng = new System.Random(42);
            int blockIndex = 0;
            float blockElapsed = 0f;
            int lastBlockCycles = 0;
            float simTime = 0f;
            int spawnIndex = 0;

            while (simTime <= _maxSimTime)
            {
                var block = _config.blocks[blockIndex];
                bool isLast = blockIndex == _config.blocks.Count - 1;

                // Pick enemy type via weighted random (deterministic)
                EnemyType type = PickWeightedEnemy(block.enemies, rng);

                // Compute stacking multipliers
                float healthMul = block.healthMultiplier;
                float speedMul = block.speedMultiplier;
                if (isLast && lastBlockCycles > 0)
                {
                    healthMul += lastBlockCycles * (block.healthMultiplier - 1f);
                    speedMul += lastBlockCycles * (block.speedMultiplier - 1f);
                }

                _events.Add(new SpawnEvent
                {
                    time = simTime,
                    enemyType = type,
                    blockIndex = blockIndex,
                    healthMul = healthMul,
                    speedMul = speedMul,
                    spawnIndex = spawnIndex,
                    laneOffset = (float)rng.NextDouble()
                });
                spawnIndex++;

                float interval = Mathf.Max(0.1f, block.spawnInterval);
                simTime += interval;
                blockElapsed += interval;

                // Advance block
                if (block.duration > 0f && blockElapsed >= block.duration)
                {
                    if (isLast)
                    {
                        lastBlockCycles++;
                        blockElapsed = 0f;
                    }
                    else
                    {
                        blockIndex++;
                        blockElapsed = 0f;
                    }
                }
            }
        }

        private EnemyType PickWeightedEnemy(List<EnemySpawnWeight> enemies, System.Random rng)
        {
            if (enemies == null || enemies.Count == 0)
                return EnemyType.Ground;

            float totalWeight = 0f;
            for (int i = 0; i < enemies.Count; i++)
                totalWeight += enemies[i].weight;

            if (totalWeight <= 0f)
                return EnemyType.Ground;

            float roll = (float)(rng.NextDouble() * totalWeight);
            float cumulative = 0f;
            for (int i = 0; i < enemies.Count; i++)
            {
                cumulative += enemies[i].weight;
                if (roll <= cumulative)
                    return enemies[i].enemyType;
            }

            return enemies[enemies.Count - 1].enemyType;
        }

        private void GetBlockAtTime(float time, out int blockIndex, out int lastBlockCycles)
        {
            blockIndex = 0;
            lastBlockCycles = 0;

            if (_config == null || _config.blocks == null || _config.blocks.Count == 0)
                return;

            float elapsed = 0f;
            for (int i = 0; i < _config.blocks.Count; i++)
            {
                var block = _config.blocks[i];
                bool isLast = i == _config.blocks.Count - 1;

                if (isLast)
                {
                    blockIndex = i;
                    if (block.duration > 0f)
                    {
                        float remaining = time - elapsed;
                        lastBlockCycles = (int)(remaining / block.duration);
                    }
                    return;
                }

                if (block.duration <= 0f)
                {
                    blockIndex = i;
                    return;
                }

                if (elapsed + block.duration > time)
                {
                    blockIndex = i;
                    return;
                }

                elapsed += block.duration;
            }
        }
    }
}
