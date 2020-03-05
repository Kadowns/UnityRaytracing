using UnityEngine;

namespace HawkTracer {

    [System.Serializable]
    public struct RayTracedMaterialData {
        public Vector3 albedo;
        public Vector3 specular;
        public Vector3 emission;
        public float smoothness;
    }
    
    [CreateAssetMenu(menuName = "HawkTracer/RayTracedMaterial")]
    public class RayTracedMaterial : ScriptableObject {
        public RayTracedMaterialData Data;
    }
}