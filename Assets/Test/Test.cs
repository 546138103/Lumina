using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Test : MonoBehaviour
{
    public Transform topParent;
    public Transform fparent;
    public Transform fchild;
    public Transform tparent;
    public Transform tchild;
    // Start is called before the first frame update
    CalibrationData data;
    void Start()
    {
        data = new CalibrationData(topParent,fparent,fchild,tparent,tchild);
        
    }

    // Update is called once per frame
    void Update()
    {
       
        Quaternion deltaRotTracked = Quaternion.FromToRotation(data.initialDir, data.CurrentDirection);
        data.parent.rotation = deltaRotTracked * data.initialRotation;
    }
}
