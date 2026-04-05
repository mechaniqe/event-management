using System.Collections.Generic;
using UnityEngine;

namespace DynamicBox.EventManagement
{
	public class EventManager
	{
		private static readonly EventManager _instance = new EventManager();
		public static EventManager Instance => _instance;

		private EventManager() { }

		public delegate void EventDelegate<T>(T eventDetails) where T : IGameEvent;

		private interface IEventDispatcher
		{
			void Dispatch(IGameEvent eventDetails);
#if UNITY_EDITOR
			System.Delegate[] GetListeners();
#endif
		}

		private class EventDispatcher<T> : IEventDispatcher where T : IGameEvent
		{
			public EventDelegate<T> OnEvent;

			public void Dispatch(IGameEvent eventDetails)
			{
				OnEvent?.Invoke((T)eventDetails);
			}

#if UNITY_EDITOR
			public System.Delegate[] GetListeners()
			{
				return OnEvent?.GetInvocationList() ?? new System.Delegate[0];
			}
#endif
		}

		private readonly Dictionary<System.Type, IEventDispatcher> _delegates = new Dictionary<System.Type, IEventDispatcher>();

		public void AddListener<T>(EventDelegate<T> del) where T : IGameEvent
		{
			var type = typeof(T);

			if (!_delegates.TryGetValue(type, out var dispatcherBase))
			{
				var dispatcher = new EventDispatcher<T>();
				dispatcher.OnEvent += del;
				_delegates[type] = dispatcher;
			}
			else if (dispatcherBase is EventDispatcher<T> dispatcher)
			{
				dispatcher.OnEvent += del;
			}
		}

		public void RemoveListener<T>(EventDelegate<T> del) where T : IGameEvent
		{
			var type = typeof(T);

			if (_delegates.TryGetValue(type, out var dispatcherBase) && dispatcherBase is EventDispatcher<T> dispatcher)
			{
				dispatcher.OnEvent -= del;
				if (dispatcher.OnEvent == null)
				{
					_delegates.Remove(type);
				}
			}
		}

		#if UNITY_EDITOR
		public static event System.Action<IGameEvent> OnEventFiredDebuggerHook;

		public System.Delegate[] GetDebugListeners(System.Type eventType)
		{
			if (_delegates.TryGetValue(eventType, out var dispatcher))
			{
				return dispatcher.GetListeners();
			}
			return new System.Delegate[0];
		}
		#endif

		public void Raise(IGameEvent eventDetails)
		{
			if (eventDetails == null)
			{
				Debug.LogError("Invalid event argument: null");
				return;
			}

			#if UNITY_EDITOR
			OnEventFiredDebuggerHook?.Invoke(eventDetails);
			#endif

			if (_delegates.TryGetValue(eventDetails.GetType(), out var dispatcher))
			{
				dispatcher.Dispatch(eventDetails);
			}
		}
	}
}