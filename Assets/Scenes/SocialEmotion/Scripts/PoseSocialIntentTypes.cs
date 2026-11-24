using System;
using UnityEngine.Events;

public enum SocialIntent
{
    None,
    RaiseHand,
    WaveInvite,
    WaitInZone,
    FaceAndAttend,
    RequestObject
}

[Serializable]
public class SocialIntentUnityEvent : UnityEvent<SocialIntent> { }
