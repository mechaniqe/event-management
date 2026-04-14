using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using System.Collections.Generic;
using DynamicBox.EventManagement;

namespace DynamicBox.EventManagement.Editor
{
    public class EventTrackerWindow : EditorWindow
    {
        [MenuItem("Tools/DynamicBox/Event Tracker")]
        public static void ShowExample()
        {
            EventTrackerWindow wnd = GetWindow<EventTrackerWindow>();
            wnd.titleContent = new GUIContent("Event Tracker", EditorGUIUtility.IconContent("d_EventSystem Icon").image);
            
            wnd.minSize = new Vector2(400, 300);
            if (wnd.position.width < 800)
            {
                var pos = wnd.position;
                pos.width = 800;
                pos.height = Mathf.Max(pos.height, 450);
                wnd.position = pos;
            }
        }

        private class CapturedEvent
        {
            public float RealTime;
            public int FrameCount;
            public string TypeName;
            public string PayloadJson;
            public string SenderClass;
            public string SenderMethod;
            public int ListenerCount;
            public string[] ListenerDetails;
            public string FullStackTrace;
        }

        private int _timeRangeSeconds = 5;
        private List<CapturedEvent> _events = new List<CapturedEvent>();
        private List<CapturedEvent> _filteredEvents = new List<CapturedEvent>();
        private int _selectedFrameFilter = -1;
        private int _highlightedFrame = -1;
        private bool _isCapturing = true;

        private ListView _listView;
        private Label _detailHeaderLabel;
        private VisualElement _detailContainer;
        private Label _lblClear;
        private Vector2 _hoverMousePos = new Vector2(-1, -1);
        private bool _isHoveringGraph = false;

        private ToolbarButton _btnPrevFrame;
        private ToolbarButton _btnNextFrame;
        private Label _lblFrameStatus;

        private void OnEnable()
        {
            EventManager.OnEventFiredDebuggerHook += OnEventFired;
            EditorApplication.update += OnEditorUpdate;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            _timeRangeSeconds = EditorPrefs.GetInt("EventTracker_TimeRange", 5);
        }

        private void OnDisable()
        {
            EventManager.OnEventFiredDebuggerHook -= OnEventFired;
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                ClearFrameFilter();
            }
        }

        private void OnEditorUpdate()
        {
            if (!_isCapturing || _events.Count == 0 || EditorApplication.isPaused) return;
            
            float cutoff = Time.realtimeSinceStartup - _timeRangeSeconds;
            int countToRemove = 0;
            // Old events are at the end of the list now
            for (int i = _events.Count - 1; i >= 0; i--)
            {
                if (_events[i].RealTime < cutoff) countToRemove++;
                else break;
            }

            if (countToRemove > 0)
            {
                 bool fullRefresh = false;
                 if (_selectedFrameFilter != -1)
                 {
                      int fRemove = 0;
                      for (int i = _filteredEvents.Count - 1; i >= 0; i--) {
                           if (_filteredEvents[i].RealTime < cutoff) fRemove++;
                           else break;
                      }
                      if (fRemove > 0)
                      {
                           _filteredEvents.RemoveRange(_filteredEvents.Count - fRemove, fRemove);
                           fullRefresh = true;
                      }
                 }
                 else
                 {
                      fullRefresh = true;
                 }
                 
                 _events.RemoveRange(_events.Count - countToRemove, countToRemove);
                 
                 if (fullRefresh) 
                 {
                     RefreshListView();
                 }
            }
        }

        private void OnEventFired(IGameEvent evt)
        {
            if (!_isCapturing) return;

            string senderClass = "Unknown";
            string senderMethod = "Unknown";
            var st = new System.Diagnostics.StackTrace(true);
            for (int i = 0; i < st.FrameCount; i++)
            {
                var method = st.GetFrame(i)?.GetMethod();
                if (method != null && method.DeclaringType != null)
                {
                    if (method.DeclaringType != typeof(EventManager) && method.DeclaringType.Namespace != "DynamicBox.EventManagement.Editor")
                    {
                        senderClass = method.DeclaringType.Name;
                        senderMethod = method.Name;
                        break;
                    }
                }
            }

            var delegates = EventManager.Instance.GetDebugListeners(evt.GetType());
            int listenerCount = delegates.Length;
            string[] listenerDetails = new string[listenerCount];
            for (int i = 0; i < listenerCount; i++)
            {
                var d = delegates[i];
                string targetName = d.Target != null ? d.Target.GetType().Name : "Static";
                listenerDetails[i] = $"{targetName}.{d.Method.Name}()";
            }

            var captured = new CapturedEvent
            {
                RealTime = Time.realtimeSinceStartup,
                FrameCount = Time.frameCount,
                TypeName = evt.GetType().Name,
                PayloadJson = JsonUtility.ToJson(evt, true),
                SenderClass = senderClass,
                SenderMethod = senderMethod,
                ListenerCount = listenerCount,
                ListenerDetails = listenerDetails,
                FullStackTrace = st.ToString()
            };

            if (string.IsNullOrEmpty(captured.PayloadJson) || captured.PayloadJson == "{}")
            {
                captured.PayloadJson = evt.ToString();
            }

            _events.Insert(0, captured);

            bool needsRefresh = false;

            if (_selectedFrameFilter != -1)
            {
                if (captured.FrameCount == _selectedFrameFilter)
                {
                    _filteredEvents.Insert(0, captured);
                    needsRefresh = true;
                }
            }
            else
            {
                needsRefresh = true;
            }

            if (needsRefresh)
            {
                EditorApplication.delayCall += RefreshListView;
            }
        }

        private void RefreshListView()
        {
            if (_listView != null)
            {
#if UNITY_2021_2_OR_NEWER
                _listView.RefreshItems();
#else
                _listView.Rebuild();
#endif
            }
            Repaint();
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;

            var toolbar = new Toolbar();
            
            var btnClear = new ToolbarButton(() => { ClearFrameFilter(); });
            
            var clearIcon = new Image { image = EditorGUIUtility.IconContent("TreeEditor.Trash").image };
            clearIcon.style.width = 16;
            clearIcon.style.height = 16;
            clearIcon.style.marginRight = 4;
            btnClear.Add(clearIcon);
            _lblClear = new Label("Clear");
            btnClear.Add(_lblClear);
            btnClear.style.flexDirection = FlexDirection.Row;
            btnClear.style.alignItems = Align.Center;
            
            var toggleCapture = new ToolbarToggle() { name = "toggleCapture", value = _isCapturing };
            toggleCapture.RegisterValueChangedCallback(evt => _isCapturing = evt.newValue);

            var recordIcon = new Image { image = EditorGUIUtility.IconContent("Animation.Record").image };
            recordIcon.style.width = 16;
            recordIcon.style.height = 16;
            recordIcon.style.marginRight = 4;
            toggleCapture.Add(recordIcon);
            toggleCapture.Add(new Label("Record"));
            toggleCapture.style.flexDirection = FlexDirection.Row;
            toggleCapture.style.alignItems = Align.Center;

            var spacer = new ToolbarSpacer() { style = { flexGrow = 1 } };
            
            var limitChoices = new List<string> { "3s", "5s", "10s", "20s", "30s" };
            var limitDropdown = new DropdownField(limitChoices, _timeRangeSeconds.ToString() + "s");
            limitDropdown.style.width = 60;
            limitDropdown.RegisterValueChangedCallback(evt => {
                string val = evt.newValue.Replace("s", "");
                if (int.TryParse(val, out int newRange))
                {
                    _timeRangeSeconds = newRange;
                    EditorPrefs.SetInt("EventTracker_TimeRange", newRange);
                }
            });

            _btnPrevFrame = new ToolbarButton(() => StepFrame(-1)) { text = "◀" };
            _lblFrameStatus = new Label("Viewing All") { style = { unityTextAlign = TextAnchor.MiddleCenter, width = 100, marginLeft = 4, marginRight = 4 } };
            _btnNextFrame = new ToolbarButton(() => StepFrame(1)) { text = "▶" };

            _btnPrevFrame.SetEnabled(false);
            _btnNextFrame.SetEnabled(false);

            toolbar.Add(btnClear);
            toolbar.Add(toggleCapture);
            toolbar.Add(spacer);
            toolbar.Add(limitDropdown);
            toolbar.Add(_btnPrevFrame);
            toolbar.Add(_lblFrameStatus);
            toolbar.Add(_btnNextFrame);
            root.Add(toolbar);

            var graphContainer = new IMGUIContainer(DrawGraphBase);
            graphContainer.style.height = 100;
            graphContainer.style.flexShrink = 0;
            graphContainer.RegisterCallback<MouseMoveEvent>(evt => {
                _hoverMousePos = evt.localMousePosition;
                _isHoveringGraph = true;
                graphContainer.MarkDirtyRepaint();
            });
            graphContainer.RegisterCallback<MouseLeaveEvent>(evt => {
                _isHoveringGraph = false;
                graphContainer.MarkDirtyRepaint();
            });
            root.Add(graphContainer);

            var splitView = new TwoPaneSplitView(1, 400, TwoPaneSplitViewOrientation.Horizontal);
            splitView.style.flexGrow = 1;
            root.Add(splitView);

            // Left Pane (List)
            var leftPane = new VisualElement();
            leftPane.style.flexGrow = 1;
            
            // Header for List
            var listHeader = new VisualElement();
            listHeader.style.flexDirection = FlexDirection.Row;
            listHeader.style.backgroundColor = new Color(0, 0, 0, 0.2f);
            listHeader.style.paddingLeft = 4;
            listHeader.style.paddingRight = 4;
            listHeader.style.paddingTop = 2;
            listHeader.style.paddingBottom = 2;
            
            var headerTime = new Label("Time") { style = { width = 80, unityFontStyleAndWeight = FontStyle.Bold } };
            var headerType = new Label("Event Type") { style = { width = 150, unityFontStyleAndWeight = FontStyle.Bold } };
            var headerSender = new Label("Sender") { style = { width = 200, flexGrow = 1, unityFontStyleAndWeight = FontStyle.Bold } };
            var headerListeners = new Label("Lsn") { style = { width = 30, unityFontStyleAndWeight = FontStyle.Bold } };
            listHeader.Add(headerTime);
            listHeader.Add(CreateResizer(headerTime, "time"));
            listHeader.Add(headerType);
            listHeader.Add(CreateResizer(headerType, "type"));
            listHeader.Add(headerSender);
            listHeader.Add(CreateResizer(headerSender, "sender"));
            listHeader.Add(headerListeners);
            leftPane.Add(listHeader);

            _listView = new ListView(_events, 22, MakeItem, BindItem);
            _listView.style.flexGrow = 1;
            _listView.selectionChanged += OnSelectionChanged;
            leftPane.Add(_listView);
            splitView.Add(leftPane);

            // Right Pane (Details)
            var rightPane = new ScrollView(ScrollViewMode.VerticalAndHorizontal);
            rightPane.style.flexGrow = 1;
            rightPane.style.paddingBottom = 8;
            rightPane.style.paddingLeft = 8;
            rightPane.style.paddingRight = 8;
            rightPane.style.paddingTop = 8;

            _detailHeaderLabel = new Label("Select an event...") { style = { unityFontStyleAndWeight = FontStyle.Bold, fontSize = 14, marginBottom = 10 } };
            rightPane.Add(_detailHeaderLabel);
            
            _detailContainer = new VisualElement();
            rightPane.Add(_detailContainer);

            splitView.Add(rightPane);
        }

        private VisualElement MakeItem()
        {
            var element = new VisualElement();
            element.style.flexDirection = FlexDirection.Row;
            
            // Note: Flex shrink is 0 to ensure they don't squash when dragged explicitly
            var timeLabel = new Label { name = "time", style = { width = 80, flexShrink = 0, paddingRight = 4 } };
            var typeLabel = new Label { name = "type", style = { width = 150, flexShrink = 0, color = new Color(0.4f, 0.7f, 1f), paddingRight = 4 } };
            var senderLabel = new Label { name = "sender", style = { width = 200, flexGrow = 1, flexShrink = 0, color = new Color(0.7f, 0.7f, 0.7f), paddingRight = 4 } };
            var listenersLabel = new Label { name = "listeners", style = { width = 30, flexShrink = 0, unityTextAlign = TextAnchor.MiddleCenter } };
            
            element.Add(timeLabel);
            element.Add(typeLabel);
            element.Add(senderLabel);
            element.Add(listenersLabel);
            
            return element;
        }

        private void BindItem(VisualElement element, int index)
        {
            var sourceList = _listView.itemsSource as List<CapturedEvent>;
            if (sourceList == null || index < 0 || index >= sourceList.Count) return;

            var timeLabel = element.Q<Label>("time");
            var typeLabel = element.Q<Label>("type");
            var senderLabel = element.Q<Label>("sender");
            var listenersLabel = element.Q<Label>("listeners");

            var evt = sourceList[index];
            timeLabel.text = evt.RealTime.ToString("F2");
            typeLabel.text = evt.TypeName;
            senderLabel.text = evt.SenderClass;
            listenersLabel.text = evt.ListenerCount.ToString();
        }

        private void OnSelectionChanged(IEnumerable<object> selection)
        {
            CapturedEvent selectedCap = null;
            if (selection != null)
            {
                foreach (var item in selection)
                {
                    if (item is CapturedEvent cap)
                    {
                        selectedCap = cap;
                        break;
                    }
                }
            }

            if (selectedCap != null)
            {
                _highlightedFrame = selectedCap.FrameCount;
                _detailHeaderLabel.text = $"Event: {selectedCap.TypeName}";
                
                _detailContainer.Clear();
                
                // Info block
                var info = new Label($"Time: {selectedCap.RealTime:F2} (Frame {selectedCap.FrameCount})\nSender: {selectedCap.SenderClass}.{selectedCap.SenderMethod}()");
                info.style.marginBottom = 10;
                info.style.color = new Color(0.7f, 0.7f, 0.7f);
                _detailContainer.Add(info);

                if (selectedCap.ListenerCount > 0)
                {
                    _detailContainer.Add(CreateHeader($"Listeners ({selectedCap.ListenerCount})"));
                    var listenersBox = new VisualElement();
                    listenersBox.style.paddingLeft = 10;
                    foreach (var detail in selectedCap.ListenerDetails)
                    {
                        var row = new VisualElement();
                        row.style.flexDirection = FlexDirection.Row;
                        row.style.alignItems = Align.Center;
                        row.style.marginBottom = 2;
                        
                        var dot = new VisualElement();
                        dot.style.width = 4;
                        dot.style.height = 4;
                        dot.style.backgroundColor = new Color(0.4f, 0.7f, 1f);
                        dot.style.marginRight = 6;
                        row.Add(dot);
                        
                        row.Add(new Label(detail));
                        listenersBox.Add(row);
                    }
                    _detailContainer.Add(listenersBox);
                }
                    
                _detailContainer.Add(CreateHeader("Payload"));
                _detailContainer.Add(CreateCodeBlock(selectedCap.PayloadJson));
                
                _detailContainer.Add(CreateHeader("Stack Trace"));
                var stackText = CreateCodeBlock(selectedCap.FullStackTrace);
                stackText.Q<Label>().style.fontSize = 10; // Smaller stack trace
                _detailContainer.Add(stackText);
            }
            else
            {
                _highlightedFrame = -1;
                _detailHeaderLabel.text = "Select an event...";
                _detailContainer.Clear();
            }
            
            Repaint();
        }

        private VisualElement CreateHeader(string text)
        {
            var label = new Label(text);
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.fontSize = 12;
            label.style.marginTop = 15;
            label.style.marginBottom = 5;
            label.style.color = new Color(0.9f, 0.9f, 0.9f);
            return label;
        }

        private VisualElement CreateCodeBlock(string content)
        {
            var container = new VisualElement();
            container.style.backgroundColor = new Color(0, 0, 0, 0.2f);
            container.style.borderLeftWidth = 1;
            container.style.borderRightWidth = 1;
            container.style.borderTopWidth = 1;
            container.style.borderBottomWidth = 1;
            container.style.borderLeftColor = new Color(1, 1, 1, 0.05f);
            container.style.borderRightColor = new Color(1, 1, 1, 0.05f);
            container.style.borderTopColor = new Color(1, 1, 1, 0.05f);
            container.style.borderBottomColor = new Color(1, 1, 1, 0.05f);
            container.style.borderTopLeftRadius = 4;
            container.style.borderTopRightRadius = 4;
            container.style.borderBottomLeftRadius = 4;
            container.style.borderBottomRightRadius = 4;
            container.style.paddingLeft = 10;
            container.style.paddingRight = 10;
            container.style.paddingTop = 10;
            container.style.paddingBottom = 10;

            var label = new Label(content);
            label.style.whiteSpace = WhiteSpace.Pre;
            
            // Try to find a monospace font
            var font = EditorGUIUtility.Load("Fonts/LucidaConsole.ttf") as Font;
            if (font != null) label.style.unityFont = font;
            else label.style.fontFamily = "monospace";
            
            container.Add(label);
            return container;
        }

        private void DrawGraphBase()
        {
            Rect rect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(rect, new Color(0.15f, 0.15f, 0.15f, 1f));
                
                if (_events.Count == 0) return;

                int minFrame = int.MaxValue;
                int maxFrame = int.MinValue;
                int maxCount = 0;
                
                var frameCounts = new Dictionary<int, int>();
                foreach (var e in _events)
                {
                    if (!frameCounts.TryGetValue(e.FrameCount, out int cnt)) cnt = 0;
                    frameCounts[e.FrameCount] = cnt + 1;
                    if (e.FrameCount < minFrame) minFrame = e.FrameCount;
                    if (e.FrameCount > maxFrame) maxFrame = e.FrameCount;
                }
                
                foreach(var kv in frameCounts)
                {
                    if (kv.Value > maxCount) maxCount = kv.Value;
                }

                if (minFrame == maxFrame) return;

                int frameRange = maxFrame - minFrame;
                if (frameRange < 60) frameRange = 60; // minimum graph width 60 frames

                Handles.color = new Color(0.3f, 0.8f, 0.3f, 1f);
                var points = new List<Vector3>();
                for (int i = 0; i <= frameRange; i++)
                {
                    int f = minFrame + i;
                    frameCounts.TryGetValue(f, out int cnt);
                    
                    float x = rect.x + (i / (float)frameRange) * rect.width;
                    float h = maxCount > 0 ? ((float)cnt / maxCount) * (rect.height - 10) : 0;
                    float y = rect.yMax - 5 - h;
                    
                    points.Add(new Vector3(x, y, 0));
                }

                if (points.Count > 1)
                {
                    Handles.DrawAAPolyLine(2f, points.ToArray());
                }
                
                if (_selectedFrameFilter != -1)
                {
                    float x = rect.x + ((_selectedFrameFilter - minFrame) / (float)frameRange) * rect.width;
                    if (x >= rect.x && x <= rect.xMax)
                    {
                        Handles.color = Color.yellow;
                        Handles.DrawLine(new Vector3(x, rect.y, 0), new Vector3(x, rect.yMax, 0));
                    }
                }

                if (_highlightedFrame != -1 && _highlightedFrame != _selectedFrameFilter)
                {
                    float x = rect.x + ((_highlightedFrame - minFrame) / (float)frameRange) * rect.width;
                    if (x >= rect.x && x <= rect.xMax)
                    {
                        Handles.color = new Color(0.4f, 0.7f, 1f, 0.5f);
                        Handles.DrawLine(new Vector3(x, rect.y, 0), new Vector3(x, rect.yMax, 0));
                    }
                }
                
                if (_isHoveringGraph && rect.Contains(_hoverMousePos))
                {
                    float t = (_hoverMousePos.x - rect.x) / rect.width;
                    t = Mathf.Clamp01(t);
                    int hoveredFrame = minFrame + Mathf.RoundToInt(t * frameRange);
                    frameCounts.TryGetValue(hoveredFrame, out int hCnt);

                    float x = rect.x + ((hoveredFrame - minFrame) / (float)frameRange) * rect.width;
                    
                    Handles.color = new Color(0.7f, 0.7f, 0.7f, 0.5f);
                    Handles.DrawLine(new Vector3(x, rect.y, 0), new Vector3(x, rect.yMax, 0));
                    
                    string tooltip = $"Frame: {hoveredFrame}\nEvents: {hCnt}";
                    Vector2 size = GUI.skin.label.CalcSize(new GUIContent(tooltip));
                    Rect tipRect = new Rect(_hoverMousePos.x + 10, _hoverMousePos.y + 10, size.x + 8, size.y + 4);
                    
                    if (tipRect.xMax > rect.xMax) tipRect.x = _hoverMousePos.x - tipRect.width - 10;
                    if (tipRect.yMax > rect.yMax) tipRect.y = _hoverMousePos.y - tipRect.height - 10;
                    
                    EditorGUI.DrawRect(tipRect, new Color(0.1f, 0.1f, 0.1f, 0.95f));
                    GUI.Label(tipRect, tooltip, new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } });
                }
            }
            
            if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
            {
                if (Event.current.button == 1) // Right click clears filter
                {
                    if (_selectedFrameFilter != -1)
                    {
                        ClearFrameFilter();
                        Event.current.Use();
                    }
                    return;
                }

                if (_events.Count == 0) return;
                int minFrame = int.MaxValue;
                int maxFrame = int.MinValue;
                foreach (var e in _events)
                {
                    if (e.FrameCount < minFrame) minFrame = e.FrameCount;
                    if (e.FrameCount > maxFrame) maxFrame = e.FrameCount;
                }
                
                int frameRange = maxFrame - minFrame;
                if (frameRange < 60) frameRange = 60;
                
                float t = (Event.current.mousePosition.x - rect.x) / rect.width;
                t = Mathf.Clamp01(t);
                
                int clickedFrame = minFrame + Mathf.RoundToInt(t * frameRange);
                SelectFrame(clickedFrame);
                Event.current.Use();
            }
        }

        private void SelectFrame(int frame)
        {
            _selectedFrameFilter = frame;
            _lblClear.text = "Clear Filter";
            if (_lblFrameStatus != null) _lblFrameStatus.text = "Frame: " + frame;
            _btnPrevFrame?.SetEnabled(true);
            _btnNextFrame?.SetEnabled(true);
            
            _filteredEvents.Clear();
            foreach(var e in _events)
            {
                if (e.FrameCount == frame) _filteredEvents.Add(e);
            }
            _listView.itemsSource = _filteredEvents;
            
            _isCapturing = false;
            var toggleCapture = rootVisualElement.Q<ToolbarToggle>("toggleCapture");
            if (toggleCapture != null) toggleCapture.value = false;
            
            _listView.ClearSelection();
            _detailHeaderLabel.text = "Select an event...";
            _detailLabel.text = "";

            RefreshListView();
        }

        private void StepFrame(int dir)
        {
            if (_selectedFrameFilter == -1 || _events.Count == 0) return;
            
            var uniqueFrames = new SortedSet<int>();
            foreach (var e in _events) uniqueFrames.Add(e.FrameCount);
            
            if (uniqueFrames.Count == 0) return;

            var frameList = new List<int>(uniqueFrames);
            int idx = frameList.IndexOf(_selectedFrameFilter);
            if (idx != -1)
            {
                idx += dir;
                if (idx >= 0 && idx < frameList.Count)
                {
                    SelectFrame(frameList[idx]);
                }
            }
            else
            {
                if (dir > 0)
                {
                    foreach (var f in frameList) if (f > _selectedFrameFilter) { SelectFrame(f); return; }
                }
                else
                {
                    for (int i = frameList.Count - 1; i >= 0; i--) if (frameList[i] < _selectedFrameFilter) { SelectFrame(frameList[i]); return; }
                }
            }
        }
        private void ClearFrameFilter()
        {
            if (_selectedFrameFilter != -1)
            {
                _selectedFrameFilter = -1;
                _lblClear.text = "Clear";
                _listView.itemsSource = _events;
                _btnPrevFrame?.SetEnabled(false);
                _btnNextFrame?.SetEnabled(false);
                if (_lblFrameStatus != null) _lblFrameStatus.text = "Viewing All";
                
                _listView.ClearSelection();
                _detailHeaderLabel.text = "Select an event...";
                _detailLabel.text = "";
                
                RefreshListView();
            }
            else
            {
                _events.Clear(); 
                _filteredEvents.Clear();
                _btnPrevFrame?.SetEnabled(false);
                _btnNextFrame?.SetEnabled(false);
                if (_lblFrameStatus != null) _lblFrameStatus.text = "Viewing All";
                RefreshListView();
                _detailHeaderLabel.text = "Select an event...";
                _detailLabel.text = "";
            }
        }

        private VisualElement CreateResizer(VisualElement targetCol, string classNameBinding)
        {
            var resizer = new VisualElement();
            resizer.style.width = 4;
            resizer.style.cursor = new StyleCursor(StyleKeyword.Null); // Ideally split-resize, falling back to null initially
            resizer.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.8f); 
            
            bool isDragging = false;
            float startMouseX = 0;
            float startWidth = 0;

            resizer.RegisterCallback<MouseDownEvent>(evt => {
                if (evt.button != 0) return;
                isDragging = true;
                startMouseX = evt.mousePosition.x;
                startWidth = targetCol.layout.width;
                resizer.style.backgroundColor = new Color(1, 1, 1, 0.3f);
                resizer.CaptureMouse();
                evt.StopPropagation();
            });

            resizer.RegisterCallback<MouseMoveEvent>(evt => {
                if (!isDragging) return;
                float delta = evt.mousePosition.x - startMouseX;
                float newWidth = Mathf.Max(30, startWidth + delta);
                
                targetCol.style.width = newWidth;
                targetCol.style.flexGrow = 0;
                
                _listView?.Query<Label>(classNameBinding).ForEach(lbl => {
                    lbl.style.width = newWidth;
                    lbl.style.flexGrow = 0;
                });
                
                evt.StopPropagation();
            });

            resizer.RegisterCallback<MouseUpEvent>(evt => {
                if (!isDragging) return;
                isDragging = false;
                resizer.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);
                resizer.ReleaseMouse();
                evt.StopPropagation();
            });

            return resizer;
        }
    }
}
