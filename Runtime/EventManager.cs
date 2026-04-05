using System.Collections.Generic;
using UnityEngine;

namespace DynamicBox.EventManagement
{
	public class EventManager
	{
		private static EventManager _instance = null;
		public static EventManager Instance
		{
			get
			{
				if (_instance == null)
				{
					_instance = new EventManager ();
				}

				return _instance;
			}
		}

		private EventManager() { }

		public delegate void EventDelegate<T> (T eventDetails) where T : IGameEvent;

		private interface IEventDispatcher
		{
			void Dispatch(IGameEvent eventDetails);
		}

		private class EventDispatcher<T> : IEventDispatcher where T : IGameEvent
		{
			public EventDelegate<T> OnEvent;

			public void Dispatch(IGameEvent eventDetails)
			{
				OnEvent?.Invoke((T)eventDetails);
			}
		}

		private Dictionary<System.Type, IEventDispatcher> delegates = new Dictionary<System.Type, IEventDispatcher> ();

		public void AddListener<T> (EventDelegate<T> del) where T : IGameEvent
		{
			var type = typeof(T);

			if (!delegates.TryGetValue(type, out var dispatcherBase))
			{
				var dispatcher = new EventDispatcher<T>();
				dispatcher.OnEvent += del;
				delegates[type] = dispatcher;
			}
			else
			{
				var dispatcher = dispatcherBase as EventDispatcher<T>;
				if (dispatcher != null)
				{
					dispatcher.OnEvent += del;
				}
			}
		}

		public void RemoveListener<T> (EventDelegate<T> del) where T : IGameEvent
		{
			var type = typeof(T);

			if (delegates.TryGetValue(type, out var dispatcherBase))
			{
				var dispatcher = dispatcherBase as EventDispatcher<T>;
				if (dispatcher != null)
				{
					dispatcher.OnEvent -= del;
					if (dispatcher.OnEvent == null)
					{
						delegates.Remove(type);
					}
				}
			}
		}

		public void Raise (IGameEvent eventDetails)
		{
			if (eventDetails == null)
			{
				Debug.LogError ("Invalid event argument: null");

				return;
			}

			if (delegates.TryGetValue(eventDetails.GetType (), out var dispatcher))
			{
				dispatcher.Dispatch(eventDetails);
			}
		}
	}
}