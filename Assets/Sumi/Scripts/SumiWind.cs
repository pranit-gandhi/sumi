using UnityEngine;
namespace Sumi {
    public class SumiWind : MonoBehaviour
    {Quaternion rest;void Awake(){rest=transform.localRotation;}void Update(){transform.localRotation=rest*Quaternion.Euler(Mathf.Sin(Time.time*1.8f+transform.position.x)*8,Mathf.Sin(Time.time+transform.position.z)*4,0);}}
}
