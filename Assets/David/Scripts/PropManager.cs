using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PropManager : MonoBehaviour
{

    public int resourceCount = 0;

    public bool PickUpItem(GameObject item)
    {
        // Implement logic to determine if the item can be picked up
        // For example, check if the item is within a certain distance or has a specific tag
        switch (item.tag)
        {
            case ItemConstants.RESOURCE:
                PickUpResource();
                return true;
            default:
                return false; // Item cannot be picked up
        }
     }

    private void PickUpResource()
    {
        resourceCount++;
    }

    private void OnGUI()
    {
        GUI.skin.label.fontSize = 24;
        GUI.color = Color.red;
        GUI.Label(new Rect(20, 20, 500, 500), $"Resources: {resourceCount}");
    }

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
