using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ShaderRunner : MonoBehaviour
{

    public ComputeShader shader;

    public Material mat;
    // Start is called before the first frame update
    void Start()
    {
        var kernel = shader.FindKernel("CSMain");
        
        RenderTexture tex = new RenderTexture(256,256,24);
        tex.enableRandomWrite = true;
        tex.Create();
        
        shader.SetTexture(kernel, "Result", tex);
        shader.Dispatch(kernel, tex.width / 8, tex.height / 8, 1);
        mat.mainTexture = tex;
    }
}
