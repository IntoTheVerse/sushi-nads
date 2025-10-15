using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// KitchenObjectSO (Scriptable Object) class defines the properties of a kitchen object
// This allows creating different types of kitchen objects in the Unity Editor
[CreateAssetMenu()] 

public class KitchenObjectSO : ScriptableObject
{
    // The prefab that will be instantiated when this kitchen object is created
    public Transform prefab;
    // The sprite used to represent this kitchen object in UI elements
    public Sprite sprite;
    // The display name of this kitchen object
    public string objectName;
}
                