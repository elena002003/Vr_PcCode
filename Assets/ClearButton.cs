using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ClearButton : MonoBehaviour
{
    public static event Action ClearEvent; 

    public void OnClickClear()
    {
        ClearEvent?.Invoke();
    }
}
