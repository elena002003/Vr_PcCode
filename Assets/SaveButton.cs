using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class SaveButton : MonoBehaviour
{
    public static event Action SaveEvent;

    public void OnClickSave()
    {
        SaveEvent?.Invoke();
    }
}
