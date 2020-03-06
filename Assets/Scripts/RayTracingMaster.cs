using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;

namespace HawkTracer {

    [RequireComponent(typeof(Camera))]
    public class RayTracingMaster : Singleton<RayTracingMaster> {

        [System.Serializable]
        private struct SphereObjectData {
            public Vector3 position;
            public float radius;
            public RayTracedMaterialData material;
        }
        
        [System.Serializable]
        private struct MeshIndex {
            public int offset;
            public int count;
        }
        
        [System.Serializable]
        private struct MeshObjectData {
            public Matrix4x4 localToWorldMatrix;
            public MeshIndex index;
            public RayTracedMaterialData material;
        }
        
        public ComputeShader RayTracingShader;
        public Texture SkyboxTexture;
        
        [Range(1, 32)] public uint ReflectionCount = 8;
        
        private int m_kernel;
        private uint m_groupX, m_groupY, m_groupZ;
        private RenderTexture m_target, m_converged;
        private Camera m_camera;
        private Material m_addMaterial;
        private int m_sampleIndex;
        private bool m_updateScene;
        
        //objects references
        private readonly List<Transform> m_transformsToWatch = new List<Transform>();
        private readonly List<RayTracedSphere> m_raytracedSpheres = new List<RayTracedSphere>();
        private readonly List<RayTracedMesh> m_raytracedMeshes = new List<RayTracedMesh>();
        private bool m_rebuildSpheres, m_rebuildMeshes;
        
        ///object data
        private readonly List<MeshObjectData> m_meshObjectsData = new List<MeshObjectData>();
        private readonly List<SphereObjectData> m_sphereObjectsData = new List<SphereObjectData>();
        
        //buffers
        private ComputeBuffer m_sphereObjectBuffer;
        private ComputeBuffer m_meshObjectBuffer;
        private ComputeBuffer m_vertexBuffer;
        private ComputeBuffer m_indexBuffer;
        
        
        //mesh buffers
        private List<Vector3> m_vertices = new List<Vector3>();
        private List<int> m_indices = new List<int>();

        public void RegisterMeshObject(RayTracedMesh obj) {
            m_transformsToWatch.Add(obj.transform);
            m_raytracedMeshes.Add(obj);
            m_rebuildMeshes = true;
        }

        public void UnregisterMeshObject(RayTracedMesh obj) {
            m_transformsToWatch.Remove(obj.transform);
            m_raytracedMeshes.Remove(obj);
            m_rebuildMeshes = true;
            
        }
        
        public void RegisterSphereObject(RayTracedSphere obj) {
            m_transformsToWatch.Add(obj.transform);
            m_raytracedSpheres.Add(obj);
            m_rebuildSpheres = true;
        }

        public void UnregisterSphereObject(RayTracedSphere obj) {
            m_transformsToWatch.Remove(obj.transform);
            m_raytracedSpheres.Remove(obj);
            m_rebuildSpheres = true;
        }

        private void Awake() {
            m_camera = GetComponent<Camera>();
            m_kernel = RayTracingShader.FindKernel("CSMain");
            RayTracingShader.GetKernelThreadGroupSizes(m_kernel, out m_groupX, out m_groupY, out m_groupZ);
        }

        private void OnDisable() {
            m_sphereObjectBuffer?.Release();
            m_meshObjectBuffer?.Release();
            m_vertexBuffer?.Release();
            m_indexBuffer?.Release();
        }

        private void RebuildMeshObjectBuffers() {
            if (!m_rebuildMeshes) {
                return;
            }

            m_rebuildMeshes = false;
            m_sampleIndex = 0;
            // Clear all lists
            m_meshObjectsData.Clear();
            m_vertices.Clear();
            m_indices.Clear();
            
            Dictionary<Mesh, MeshIndex> meshMap = new Dictionary<Mesh, MeshIndex>();
            
            // Loop over all objects and gather their data
            foreach (RayTracedMesh obj in m_raytracedMeshes) {
                Mesh mesh = obj.GetComponent<MeshFilter>().sharedMesh;

                //checks if this mesh is already present on scene
                if (!meshMap.ContainsKey(mesh)) {
                    // Add vertex data
                    int firstVertex = m_vertices.Count;
                    m_vertices.AddRange(mesh.vertices);
                    // Add index data - if the vertex buffer wasn't empty before, the
                    // indices need to be offset
                    int firstIndex = m_indices.Count;
                    var indices = mesh.GetIndices(0);
                    m_indices.AddRange(indices.Select(index => index + firstVertex));
                    meshMap[mesh] = new MeshIndex {offset = firstIndex, count = indices.Length};
                }
                
                // Add the object itself
                m_meshObjectsData.Add(new MeshObjectData {
                    localToWorldMatrix = obj.transform.localToWorldMatrix,
                    index = meshMap[mesh],
                    material = obj.Material.Data
                });
            }

            unsafe {
                CreateComputeBuffer(ref m_meshObjectBuffer, m_meshObjectsData, sizeof(MeshObjectData));
                CreateComputeBuffer(ref m_vertexBuffer, m_vertices, sizeof(Vector3));
                CreateComputeBuffer(ref m_indexBuffer, m_indices, sizeof(int));
            }
        }
        
        private void RebuildSphereObjectBuffers() {
            if (!m_rebuildSpheres) {
                return;
            }

            m_rebuildSpheres = false;
            m_sampleIndex = 0;
            m_sphereObjectsData.Clear();
            Debug.Log("Limpei");

            // Add a number of random spheres
            for (int i = 0; i < m_raytracedSpheres.Count; i++) {

                SphereObjectData sphereObjectData = new SphereObjectData();
                sphereObjectData.radius = m_raytracedSpheres[i].Radius * m_raytracedSpheres[i].transform.localScale.x;
                sphereObjectData.position = m_raytracedSpheres[i].transform.position;
                sphereObjectData.material = m_raytracedSpheres[i].Material.Data;

                // Add the sphere to the list
                m_sphereObjectsData.Add(sphereObjectData);
            }

            unsafe {
                CreateComputeBuffer(ref m_sphereObjectBuffer, m_sphereObjectsData, sizeof(SphereObjectData));
            }
        }
        

        private static void CreateComputeBuffer<T>(ref ComputeBuffer buffer, List<T> data, int stride)
            where T : struct {
            // Do we already have a compute buffer?
            if (buffer != null) {
                // If no data or buffer doesn't match the given criteria, release it
                if (data.Count == 0 || buffer.count != data.Count || buffer.stride != stride) {
                    buffer.Release();
                    buffer = null;
                }
            }

            if (data.Count != 0) {
                // If the buffer has been released or wasn't there to
                // begin with, create it
                if (buffer == null) {
                    buffer = new ComputeBuffer(data.Count, stride);
                }

                // Set data on the buffer
                buffer.SetData(data);
            }
        }    
        
        private void SetComputeBuffer(string name, ComputeBuffer buffer) {
            if (buffer != null) {
                RayTracingShader.SetBuffer(m_kernel, name, buffer);
            }
        }

        private void SetShaderParameters() {
            RayTracingShader.SetMatrix("_CameraToWorld", m_camera.cameraToWorldMatrix);
            RayTracingShader.SetMatrix("_CameraInverseProjection", m_camera.projectionMatrix.inverse);
            RayTracingShader.SetVector("_PixelOffset", new Vector2(Random.value, Random.value));
            RayTracingShader.SetInt("_ReflectionCount", (int) ReflectionCount);
            RayTracingShader.SetFloat("_Seed", Random.value);
            RayTracingShader.SetTexture(m_kernel, "_SkyboxTexture", SkyboxTexture);
            SetComputeBuffer("_Spheres", m_sphereObjectBuffer);
            SetComputeBuffer("_MeshObjects", m_meshObjectBuffer);
            SetComputeBuffer("_Vertices", m_vertexBuffer);
            SetComputeBuffer("_Indices", m_indexBuffer);
        }

        private void Update() {
            if (transform.hasChanged) {
                m_sampleIndex = 0;
                transform.hasChanged = false;
            }

            m_transformsToWatch.ForEach(t => {
                if (t.hasChanged) {
                    m_updateScene = true;
                    t.hasChanged = false;
                    m_sampleIndex = 0;
                }
            });
        }
        
        private void UpdateScene() {
            if (!m_updateScene) {
                return;
            }

            if (m_sphereObjectsData.Count == m_raytracedSpheres.Count) {
                for (int i = 0; i < m_raytracedSpheres.Count; i++) {
                    SphereObjectData aux = m_sphereObjectsData[i];
                    aux.position = m_raytracedSpheres[i].transform.position;
                    m_sphereObjectsData[i] = aux;
                }

                m_sphereObjectBuffer.SetData(m_sphereObjectsData);
            }


            if (m_meshObjectsData.Count == m_raytracedMeshes.Count) {
                for (int i = 0; i < m_raytracedMeshes.Count; i++) {
                    MeshObjectData aux = m_meshObjectsData[i];
                    aux.localToWorldMatrix = m_raytracedMeshes[i].transform.localToWorldMatrix;
                    m_meshObjectsData[i] = aux;
                }

                m_meshObjectBuffer.SetData(m_meshObjectsData);
            }

            m_updateScene = false;
        }

        private void OnRenderImage(RenderTexture source, RenderTexture destination) {
            RebuildSphereObjectBuffers();
            RebuildMeshObjectBuffers();
            
            UpdateScene();
            
            SetShaderParameters();
            
            Render(destination);
        }

        private void Render(RenderTexture destination) {
            // Make sure we have a current render target
            InitRenderTexture();
            
            // Set the target and dispatch the compute shader
            RayTracingShader.SetTexture(m_kernel, "Result", m_target);

            int threadGroupsX = Mathf.CeilToInt(Screen.width / (float) m_groupX);
            int threadGroupsY = Mathf.CeilToInt(Screen.height / (float) m_groupY);
            
            RayTracingShader.Dispatch(m_kernel, threadGroupsX, threadGroupsY, 1);
            
            
            if (!m_addMaterial) {
                m_addMaterial = new Material(Shader.Find("Hidden/AddShader"));
            }

            m_addMaterial.SetFloat("_Sample", m_sampleIndex);       
            
            Graphics.Blit(m_target, m_converged, m_addMaterial);
            Graphics.Blit(m_converged, destination);
            m_sampleIndex++;
        }

        private void InitRenderTexture() {
            if (m_target == null || m_target.width != Screen.width || m_target.height != Screen.height) {
                // Release render texture if we already have one
                if (m_target != null) {
                    m_target.Release();
                    m_converged.Release();
                }

                // Get a render target for Ray Tracing
                m_target = new RenderTexture(Screen.width, Screen.height, 0,
                    RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
                m_target.enableRandomWrite = true;
                m_target.Create();
                m_converged = new RenderTexture(Screen.width, Screen.height, 0,
                    RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
                m_converged.enableRandomWrite = true;
                m_converged.Create();

                // Reset sampling
                m_sampleIndex = 0;
            }
        }
    }
}