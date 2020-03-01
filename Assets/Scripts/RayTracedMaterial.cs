using UnityEngine;

namespace HawkTracer {

    [System.Serializable]
    public struct RayTracedMaterial {
        public Vector3 albedo;
        public Vector3 specular;
        public Vector3 emission;
        public float smoothness;
    }
}