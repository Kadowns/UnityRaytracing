using UnityEngine;

namespace HawkTracer {

    [RequireComponent(typeof(MeshRenderer), typeof(MeshFilter))]
    public class RayTracedMesh : MonoBehaviour {

        public RayTracedMaterial Material;

        private void Awake() {
            //RayTracingMaster.Instance.RegisterMeshObject(this);
        }

        private void OnDisable() {
            if (RayTracingMaster.Instance) {
                RayTracingMaster.Instance.UnregisterMeshObject(this);    
            }
        }
    }
}