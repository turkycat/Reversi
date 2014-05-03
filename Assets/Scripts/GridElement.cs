using UnityEngine;
using System.Collections;

public class GridElement : MonoBehaviour
{
    void OnMouseDown()
    {
        Singleton.i.itemClicked( this.name );
    }
}
