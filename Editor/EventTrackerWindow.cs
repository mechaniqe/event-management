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
        [MenuItem("Window/DynamicBox/Event Tracker")]
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
        }

        private const int MaxEvents = 10000;
        private List<CapturedEvent> _events = new List<CapturedEvent>();
        private List<CapturedEvent> _filteredEvents = new List<CapturedEvent>();
        private int _selectedFrameFilter = -1;
        private bool _isCapturing = true;

        private ListView _listView;
        private Label _detailLabel;
        private Label _detailHeaderLabel;
        private Label _lblClear;

        private void OnEnable()
        {
            EventManager.OnEventFiredDebuggerHook += OnEventFired;
        }

        private void OnDisable()
        {
            EventManager.OnEventFiredDebuggerHook -= OnEventFired;
        }

        private void OnEventFired(IGameEvent evt)
        {
            if (!_isCapturing) return;

            var captured = new CapturedEvent
            {
                RealTime = Time.realtimeSinceStartup,
                FrameCount = Time.frameCount,
                TypeName = evt.GetType().Name,
                PayloadJson = JsonUtility.ToJson(evt, true)
            };

            if (string.IsNullOrEmpty(captured.PayloadJson) || captured.PayloadJson == "{}")
            {
                captured.PayloadJson = evt.ToString();
            }

            _events.Add(captured);
            if (_events.Count > MaxEvents)
            {
                _events.RemoveAt(0);
            }

            bool needsRefresh = false;

            if (_selectedFrameFilter != -1)
            {
                if (captured.FrameCount == _selectedFrameFilter)
                {
                    _filteredEvents.Add(captured);
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
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;

            var toolbar = new Toolbar();
            
            var btnClear = new ToolbarButton(() => { 
                if (_selectedFrameFilter != -1)
                {
                    _selectedFrameFilter = -1;
                    _lblClear.text = "Clear";
                    _listView.itemsSource = _events;
                    RefreshListView();
                }
                else
                {
                    _events.Clear(); 
                    _filteredEvents.Clear();
                    RefreshListView();
                    _detailHeaderLabel.text = "Select an event...";
                    _detailLabel.text = "";
                }
            });
            
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

            toolbar.Add(btnClear);
            toolbar.Add(toggleCapture);
            root.Add(toolbar);

            var graphContainer = new IMGUIContainer(DrawGraphBase);
            graphContainer.style.height = 100;
            graphContainer.style.flexShrink = 0;
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
            var headerType = new Label("Event Type") { style = { flexGrow = 1, unityFontStyleAndWeight = FontStyle.Bold } };
            listHeader.Add(headerTime);
            listHeader.Add(headerType);
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

            _detailHeaderLabel = new Label("Select an event...") { style = { unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 5 } };
            rightPane.Add(_detailHeaderLabel);
            
            _detailLabel = new Label();
            _detailLabel.style.whiteSpace = WhiteSpace.Normal;
            rightPane.Add(_detailLabel);

            splitView.Add(rightPane);
        }

        private VisualElement MakeItem()
        {
            var element = new VisualElement();
            element.style.flexDirection = FlexDirection.Row;
            
            var timeLabel = new Label { name = "time", style = { width = 80 } };
            var typeLabel = new Label { name = "type", style = { flexGrow = 1, color = new Color(0.3f, 0.6f, 1f) } };
            
            element.Add(timeLabel);
            element.Add(typeLabel);
            
            return element;
        }

        private void BindItem(VisualElement element, int index)
        {
            var sourceList = _listView.itemsSource as List<CapturedEvent>;
            if (sourceList == null || index < 0 || index >= sourceList.Count) return;

            var timeLabel = element.Q<Label>("time");
            var typeLabel = element.Q<Label>("type");

            var evt = sourceList[index];
            timeLabel.text = evt.RealTime.ToString("F2");
            typeLabel.text = evt.TypeName;
        }

        private void OnSelectionChanged(IEnumerable<object> selection)
        {
            foreach (var item in selection)
            {
                if (item is CapturedEvent cap)
                {
                    _detailHeaderLabel.text = $"Event: {cap.TypeName}\nTime: {cap.RealTime:F2} (Frame {cap.FrameCount})";
                    _detailLabel.text = cap.PayloadJson;
                }
                return;
            }
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
            }
            
            if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
            {
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
            _filteredEvents.Clear();
            foreach(var e in _events)
            {
                if (e.FrameCount == frame) _filteredEvents.Add(e);
            }
            _listView.itemsSource = _filteredEvents;
            
            _isCapturing = false;
            var toggleCapture = rootVisualElement.Q<ToolbarToggle>("toggleCapture");
            if (toggleCapture != null) toggleCapture.value = false;
            
            RefreshListView();
        }
    }
}
