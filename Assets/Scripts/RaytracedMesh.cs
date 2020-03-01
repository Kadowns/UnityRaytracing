using UnityEngine;

namespace HawkTracer {

    [RequireComponent(typeof(MeshRenderer), typeof(MeshFilter))]
    public class RaytracedMesh : MonoBehaviour {

        private void OnEnable() {
            RayTracingMaster.Instance.RegisterObject(this);
        }

        private void OnDisable() {
            RayTracingMaster.Instance.UnregisterObject(this);
        }
    }
}