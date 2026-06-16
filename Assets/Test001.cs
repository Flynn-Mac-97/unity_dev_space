using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Test001 : MonoBehaviour
{
    public GameObject player;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        // this.transform.position = player.transform.position + new Vector3(0, 1, -2);
        // Rotate this game object around the player
        this.transform.RotateAround(player.transform.position, Vector3.up, 100f * Time.deltaTime);
    
        // This game object always keeps the same distance from the player
        this.transform.position = player.transform.position + (this.transform.position - player.transform.position).normalized * 2f;

        // This game object always keeps the same height from the player
        this.transform.position = new Vector3(this.transform.position.x, player.transform.position.y + 1f, this.transform.position.z);
    }
}
