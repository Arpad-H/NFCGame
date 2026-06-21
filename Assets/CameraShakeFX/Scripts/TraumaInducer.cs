using UnityEngine;
using System.Collections;

/* Example script to apply trauma to the camera or any game object */
public class TraumaInducer : MonoBehaviour
{

    [Tooltip("Maximum stress the effect can inflict upon objects Range([0,1])")]
   
    public float baseStress = 0.6f;
    private StressReceiver receiver;

    private void Start()
    {
        receiver = FindAnyObjectByType<StressReceiver>();
    }

    public void ShakeCamera()
    {
        if (!receiver) return;
      
        receiver.InduceStress(baseStress);
    }
    
}