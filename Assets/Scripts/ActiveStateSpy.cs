using UnityEngine;
using System;

public class ActiveStateSpy : MonoBehaviour
{
    [SerializeField] string label = "";

    void OnEnable()
    {
        Debug.Log($"[Spy] ENABLE {name} {label}\n{Environment.StackTrace}");
    }

    void OnDisable()
    {
        Debug.Log($"[Spy] DISABLE {name} {label}\n{Environment.StackTrace}");
    }
}