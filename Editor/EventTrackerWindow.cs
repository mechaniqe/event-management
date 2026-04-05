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
            wnd.titleContent = new GUIContent("Event Tracker");
        }

        private class CapturedEvent
        {
            public float RealTime;
            public string TypeName;
            public string PayloadJson;
        }

        private const int MaxEvents = 500;
        private List<CapturedEvent> _events = new List<CapturedEvent>();
        private bool _isCapturing = true;

        private ListView _listView;
        private Label _detailLabel;
        private Label _detailHeaderLabel;

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

            EditorApplication.delayCall += () =>
            {
                if (_listView != null)
                {
#if UNITY_2021_2_OR_NEWER
                    _listView.RefreshItems();
#else
                    _listView.Rebuild();
#endif
                }
            };
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;

            var toolbar = new Toolbar();
            
            var btnClear = new ToolbarButton(() => { 
                _events.Clear(); 
                _listView?.RefreshItems();
                _detailHeaderLabel.text = "Select an event...";
                _detailLabel.text = "";
            }) { text = "Clear" };
            
            var toggleCapture = new ToolbarToggle() { text = "Record", value = _isCapturing };
            toggleCapture.RegisterValueChangedCallback(evt => _isCapturing = evt.newValue);

            toolbar.Add(btnClear);
            toolbar.Add(toggleCapture);
            root.Add(toolbar);

            var splitView = new TwoPaneSplitView(0, 250, TwoPaneSplitViewOrientation.Horizontal);
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
            if (index < 0 || index >= _events.Count) return;

            var timeLabel = element.Q<Label>("time");
            var typeLabel = element.Q<Label>("type");

            var evt = _events[index];
            timeLabel.text = evt.RealTime.ToString("F2");
            typeLabel.text = evt.TypeName;
        }

        private void OnSelectionChanged(IEnumerable<object> selection)
        {
            foreach (var item in selection)
            {
                if (item is CapturedEvent cap)
                {
                    _detailHeaderLabel.text = $"Event: {cap.TypeName}\nTime: {cap.RealTime:F2}";
                    _detailLabel.text = cap.PayloadJson;
                }
                return;
            }
        }
    }
}
