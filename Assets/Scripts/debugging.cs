using UnityEngine;
using System.Collections;

public class debugging : MonoBehaviour {

	// Use this for initialization
	void Start () {
	
	}
	
	// Update is called once per frame
	void Update () {
	
	}


    public void OnMouseDown()
    {
        Singleton.i.Debug();
    }
}
