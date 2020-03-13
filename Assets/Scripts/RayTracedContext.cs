using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.PackageManager.UI;
using UnityEngine;
using Random = UnityEngine.Random;

namespace HawkTracer {


    [CreateAssetMenu(menuName = "HawkTracer/RayTracedContext")]
    public class RayTracedContext : ScriptableObject {  
        
        [SerializeField] private ComputeShader m_computeShader;
        [Range(1, 32)] public uint ReflectionCount = 8;
        [Range(1, 32)] public uint SampleCount = 8;
        [SerializeField] private Texture m_noiseTexture;
        
        private int m_kernel;
        private uint m_groupX, m_groupY, m_groupZ, m_frameCount;
        
        private RenderTexture m_target, m_converged;
        private Camera m_camera;
        private Material m_addMaterial;
       
        public Texture SkyboxTexture;
        
        private void SetShaderParameters() {
            m_frameCount++;
            m_computeShader.SetMatrix("_CameraToWorld", m_camera.cameraToWorldMatrix);
            m_computeShader.SetMatrix("_CameraInverseProjection", m_camera.projectionMatrix.inverse);
            
            m_computeShader.SetInt("_ReflectionCount", (int) ReflectionCount);
            
            m_computeShader.SetFloat("_Seed", Random.value);
            m_computeShader.SetTexture(m_kernel, "_SkyboxTexture", SkyboxTexture);
            m_computeShader.SetTexture(m_kernel, "_NoiseTexture", m_noiseTexture);

        }
        
        public void OnInitialize(Camera camera) {
            m_camera = camera;
            m_kernel = m_computeShader.FindKernel("CSMain");
            m_computeShader.GetKernelThreadGroupSizes(m_kernel, out m_groupX, out m_groupY, out m_groupZ);
        }
        
        public void OnClear() {
            
        }

        public void OnRender(RenderTexture destination, ref int sampleIndex) {
            
            SetShaderParameters();
            
            Render(destination, ref sampleIndex);
        }

        public void SetComputeBuffer(string name, ComputeBuffer buffer) {
            if (buffer != null) {
                m_computeShader.SetBuffer(m_kernel, name, buffer);
            }
        }
        
        private void Render(RenderTexture destination, ref int sampleIndex) {
            // Make sure we have a current render target
            InitRenderTexture(ref sampleIndex);
            int threadGroupsX = Mathf.CeilToInt(m_target.width / (float) m_groupX);
            int threadGroupsY = Mathf.CeilToInt(m_target.height / (float) m_groupY);
            m_computeShader.SetTexture(m_kernel, "Result", m_target);

            for (int i = 0; i < SampleCount; i++)
            {

                
                m_computeShader.SetVector("_PixelOffset", new Vector2(Random.value, Random.value));
                m_computeShader.Dispatch(m_kernel, threadGroupsX, threadGroupsY, 1);

                if (!m_addMaterial)
                {
                    m_addMaterial = new Material(Shader.Find("Hidden/AddShader"));
                }

                m_addMaterial.SetFloat("_Sample", i + 1);

                Graphics.Blit(m_target, m_converged, m_addMaterial);
                
                sampleIndex++;
            }
            Graphics.Blit(m_converged, destination);
        }

        private void InitRenderTexture(ref int sampleIndex) {
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
                sampleIndex = 0;
            }
        }
       
    }
}