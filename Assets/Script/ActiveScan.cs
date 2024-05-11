using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ActiveScan : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        
    }
    // Update is called once per frame
    void Update()
    {
        if( Input.GetKeyDown( KeyCode.Space ) ) {
            ScanFeature.ExecuteScan( transform );
            Debug.Log( "scan" );
        }
    }
}
