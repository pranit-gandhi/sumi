using UnityEngine;
namespace Sumi {
    public class SumiGoldSurface : MonoBehaviour
    {
        public float threshold=20;MaterialPropertyBlock block;Renderer surface;Color original;
        void Awake(){surface=GetComponent<Renderer>();block=new MaterialPropertyBlock();original=surface.sharedMaterial.GetColor("_BaseColor");}
        public void SetMastery(float value){if(!surface)return;float a=Mathf.SmoothStep(0,1,(value-threshold)/16);block.SetColor("_BaseColor",Color.Lerp(original,SumiArt.Gold,a));surface.SetPropertyBlock(block);}
    }
}
