using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;

namespace HawkTracer {

    [RequireComponent(typeof(Camera))]
    public class RayTracingMaster : Singleton<RayTracingMaster> {

        private struct Sphere {
            public Vector3 position;
            public float radius;
            public RayTracedMaterial material;
        }
        
        struct MeshObject {
            public Matrix4x4 localToWorldMatrix;
            public int indices_offset;
            public int indices_count;
        }
        private List<MeshObject> m_meshObjects = new List<MeshObject>();
        private List<Vector3> m_vertices = new List<Vector3>();
        private List<int> m_indices = new List<int>();
        private ComputeBuffer m_meshObjectBuffer;
        private ComputeBuffer m_vertexBuffer;
        private ComputeBuffer m_indexBuffer;


        public ComputeShader RayTracingShader;
        public Texture SkyboxTexture;
        public List<SphereCollider> Spheres;

        [Range(1, 32)] public uint ReflectionCount = 8;

        private RenderTexture m_target, m_converged;
        private Camera m_camera;

        private readonly List<RaytracedMesh> m_raytracedMeshes = new List<RaytracedMesh>();
        private bool m_rebuildMeshes;
        
        private int m_kernel;
        private List<Sphere> m_sphereData;
        private ComputeBuffer m_sphereBuffer;
        private Material m_addMaterial;
        private int m_sampleIndex;

        private uint m_groupX, m_groupY, m_groupZ;

        public void RegisterObject(RaytracedMesh obj) {
            m_raytracedMeshes.Add(obj);
            m_rebuildMeshes = true;
        }

        public void UnregisterObject(RaytracedMesh obj) {
            m_raytracedMeshes.Remove(obj);
            m_rebuildMeshes = true;
        }

        private void Awake() {
            m_camera = GetComponent<Camera>();
            m_kernel = RayTracingShader.FindKernel("CSMain");
            RayTracingShader.GetKernelThreadGroupSizes(m_kernel, out m_groupX, out m_groupY, out m_groupZ);
        }

        private void OnEnable() {
            SetUpScene();
        }

        private void OnDisable() {
            m_sphereBuffer?.Release();
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
            m_meshObjects.Clear();
            m_vertices.Clear();
            m_indices.Clear();
            // Loop over all objects and gather their data
            foreach (RaytracedMesh obj in m_raytracedMeshes) {
                Mesh mesh = obj.GetComponent<MeshFilter>().sharedMesh;
                // Add vertex data
                int firstVertex = m_vertices.Count;
                m_vertices.AddRange(mesh.vertices);
                // Add index data - if the vertex buffer wasn't empty before, the
                // indices need to be offset
                int firstIndex = m_indices.Count;
                var indices = mesh.GetIndices(0);
                m_indices.AddRange(indices.Select(index => index + firstVertex));
                // Add the object itself
                m_meshObjects.Add(new MeshObject {
                    localToWorldMatrix = obj.transform.localToWorldMatrix,
                    indices_offset = firstIndex,
                    indices_count = indices.Length
                });
            }

            unsafe {
                CreateComputeBuffer(ref m_meshObjectBuffer, m_meshObjects, sizeof(MeshObject));
                CreateComputeBuffer(ref m_vertexBuffer, m_vertices, sizeof(Vector3));
                CreateComputeBuffer(ref m_indexBuffer, m_indices, sizeof(int));
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

        private void SetUpScene() {

            m_sphereData = new List<Sphere>();

            // Add a number of random spheres
            for (int i = 0; i < Spheres.Count; i++) {

                Sphere sphere = new Sphere();
                sphere.radius = Spheres[i].radius * Spheres[i].transform.localScale.x;
                sphere.position = Spheres[i].transform.position;

                // Albedo and specular color
                Color color = Random.ColorHSV();
                bool metal = Random.value < 0.5f;

                RayTracedMaterial mat;
                mat.albedo = metal ? Vector3.zero : new Vector3(color.r, color.g, color.b);
                mat.specular = metal ? new Vector3(color.r, color.g, color.b) : Vector3.one * 0.04f;

                bool emissive = Random.value < 0.2;
                mat.emission = emissive ? new Vector3(color.r, color.g, color.b) : Vector3.zero;
                mat.smoothness = Random.value;
                sphere.material = mat;

                // Add the sphere to the list
                m_sphereData.Add(sphere);
            }

            m_sphereBuffer?.Dispose();
            // Assign to compute buffer
            unsafe {
                m_sphereBuffer = new ComputeBuffer(m_sphereData.Count, sizeof(Sphere));
            }

            m_sampleIndex = 0;
        }

        private void UpdateScene() {
            for (int i = 0; i < Spheres.Count; i++) {
                Sphere aux = m_sphereData[i];
                aux.position = Spheres[i].transform.position;
                m_sphereData[i] = aux;
            }

            m_sphereBuffer.SetData(m_sphereData);
        }

        private void SetShaderParameters() {
            RayTracingShader.SetMatrix("_CameraToWorld", m_camera.cameraToWorldMatrix);
            RayTracingShader.SetMatrix("_CameraInverseProjection", m_camera.projectionMatrix.inverse);
            RayTracingShader.SetVector("_PixelOffset", new Vector2(Random.value, Random.value));
            RayTracingShader.SetInt("_ReflectionCount", (int) ReflectionCount);
            RayTracingShader.SetFloat("_Seed", Random.value);
            RayTracingShader.SetTexture(m_kernel, "_SkyboxTexture", SkyboxTexture);
            SetComputeBuffer("_Spheres", m_sphereBuffer);
            SetComputeBuffer("_MeshObjects", m_meshObjectBuffer);
            SetComputeBuffer("_Vertices", m_vertexBuffer);
            SetComputeBuffer("_Indices", m_indexBuffer);
        }
        
        private void SetComputeBuffer(string name, ComputeBuffer buffer) {
            if (buffer != null) {
                RayTracingShader.SetBuffer(m_kernel, name, buffer);
            }
        }

        private void Update() {
            if (transform.hasChanged) {
                m_sampleIndex = 0;
                transform.hasChanged = false;
            }

            Spheres.ForEach(s => {
                if (s.transform.hasChanged) {
                    s.transform.hasChanged = false;
                    m_sampleIndex = 0;
                }
            });
        }

        private void OnRenderImage(RenderTexture source, RenderTexture destination) {

            UpdateScene();
            SetShaderParameters();
            Render(destination);
        }

        private void Render(RenderTexture destination) {
            // Make sure we have a current render target
            InitRenderTexture();
            RebuildMeshObjectBuffers();
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