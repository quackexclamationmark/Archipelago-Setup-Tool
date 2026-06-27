using System;
using UnityEngine.Events;

[Serializable]
public class ButtonSettings
{
    public int activeButtonIndex = 0;
    public bool ignoreLeadingThe = false;
}

[Serializable]
public class IntEvent : UnityEvent<int> { }
