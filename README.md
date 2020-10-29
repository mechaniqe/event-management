# Event Management System
**Event Management System** used in our projects. Has been copied/stolen from one of **Unite talks**.

## 1. Getting Started

1. Open up project packages manifest JSON: [Project Folder]/Packages/manifest.json
2. Add following line on top of **dependencies**: `"net.dynamicbox.eventmanagement": "https://github.com/mechaniqe/event-management.git",`

The end result should be similar to:
```
{
  "dependencies": {
    "net.dynamicbox.eventmanagement": "https://github.com/mechaniqe/event-management.git",
    ...
  }
}
```

## 2. How to Use

Event Manager is a singleton. You will need to create game events and then listen to these to be aware of those events firing up.

### 2.1 Creating game events

Create a new class that extends **GameEvent** class. It is recommended to start the name with **On** and end it with **Event**. Example game event:
```
namespace DynamicBox.EventManagement.GameEvents
{
  public class OnNewPlayerJoinedEvent : GameEvent
  {
    // some public fields here
    
    // a constructor?!
  }
}
```

### 2.2 Handling game events

Create handlers where required and add these as listeners. And yeah, don't forget to remove them as well. An ugly sample:

```
using DynamicBox.EventManagement;
using DynamicBox.EventManagement.GameEvents;

namespace SomeOtherNamespace
{
  public class SomeOtherClass
  {
    void OnEnable ()
    {
      EventManager.Instance.AddListener<OnNewPlayerJoinedEvent> (OnNewPlayerJoinedHandler);
    }
    
    void OnDisable ()
    {
      EventManager.Instance.RemoveListener<OnNewPlayerJoinedEvent> (OnNewPlayerJoinedHandler);
    }
    
    private void OnNewPlayerJoinedHandler (OnNewPlayerJoinedEvent eventDetails)
    {
      // handle the event here
    }
  }
}
```

### 2.3 Raising game events

Raise events wherever required and someone will hear you. Unless of course if you did something wrong in previous steps.

```
using DynamicBox.EventManagement;
using DynamicBox.EventManagement.GameEvents;

namespace SomeYetAnotherNamespace
{
  public class SomeYetAnotherClass
  {
    private void SomeMethod ()
    {
      EventManager.Instance.Raise (new OnNewPlayerJoinedEvent ()); // Constructor comes in handy if you need to send over certain parameters
    }
  }
}
```
