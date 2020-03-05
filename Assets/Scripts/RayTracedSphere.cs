using UnityEngine;

namespace HawkTracer {
    
    public class RayTracedSphere : MonoBehaviour {
        
        public float Radius;
        public RayTracedMaterial Material;

        private void Awake() {
            //RayTracingMaster.Instance.RegisterSphereObject(this);
        }

        private void OnDisable() {
            if (RayTracingMaster.Instance) {
                RayTracingMaster.Instance.UnregisterSphereObject(this);    
            }
        }
        
        
#if UNITY_EDITOR

        private void OnValidate() {
            var col = GetComponent<SphereCollider>();
            if (col) {
                col.radius = Radius;
            }
        }

        private void OnDrawGizmos() {
            if (Material) {
                Vector3 rgb = Material.Data.albedo;
                Gizmos.color = new Color(rgb.x, rgb.y, rgb.z, 1);    
            }
            else {
                Gizmos.color = Color.white;
            }
            Gizmos.DrawSphere(transform.position, Radius * transform.localScale.x);
        }
#endif
    }
}