using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NewBehaviourScript : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        PropManager manager = other.GetComponent<PropManager>();
        if (manager != null)
        {
            bool pickedUp = manager.PickUpItem(gameObject);

            if (pickedUp)
            {
                RemoveItem();
            }
            
        }
    }

    void RemoveItem()
    {
        Destroy(gameObject);
    }
}
