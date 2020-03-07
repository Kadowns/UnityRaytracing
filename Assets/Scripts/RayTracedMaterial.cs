using UnityEngine;

namespace HawkTracer {

    public struct RayTracedMaterialData {
        public Vector3 albedo;
        public Vector3 specular;
        public Vector3 emission;
        public float smoothness;
    }
    
    [CreateAssetMenu(menuName = "HawkTracer/RayTracedMaterial")]
    public class RayTracedMaterial : ScriptableObject {
        public Color Albedo, Specular, Emission;
        public float Smoothness;

        public RayTracedMaterialData Data {
            get {
                RayTracedMaterialData data = new RayTracedMaterialData {
                    albedo = new Vector3(Albedo.r, Albedo.g, Albedo.b),
                    specular = new Vector3(Specular.r, Specular.g, Specular.b),
                    emission = new Vector3(Emission.r, Emission.g, Emission.b),
                    smoothness = Smoothness
                };
                return data;
            }
        }
    }
}