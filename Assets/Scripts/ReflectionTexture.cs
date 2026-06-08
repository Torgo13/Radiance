#nullable enable
using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TCGE
{
    [RequireComponent(typeof(ReflectionProbe))]
    public class ReflectionTexture : MonoBehaviour
    {
        void Start()
        {
            try
            {
                _ = RenderAsync(GetComponent<ReflectionProbe>(), destroyCancellationToken);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        static async Awaitable RenderAsync(ReflectionProbe probe,
            System.Threading.CancellationToken ct)
        {
            int renderID = probe.RenderProbe();

            Material skybox = RenderSettings.skybox;
            int Tex = Shader.PropertyToID("_Tex");

            AsyncGPUReadbackRequest readback = default;

            while (!ct.IsCancellationRequested)
            {
                if (!probe.isActiveAndEnabled || Mathf.Approximately(0, probe.intensity))
                {
                    skybox.SetTexture(Tex, null);
                    RenderSettings.customReflectionTexture = null;
                    RenderSettings.ambientSkyColor = default;
                    RenderSettings.ambientEquatorColor = default;
                    RenderSettings.ambientGroundColor = default;
                    await Awaitable.NextFrameAsync();
                    continue;
                }
                
                if (probe.IsFinishedRendering(renderID))
                {
                    RenderTexture tex = probe.realtimeTexture;
                    
                    if (readback.done && !readback.hasError)
                    {
                        renderID = probe.RenderProbe();
                        skybox.SetTexture(Tex, tex);
                        RenderSettings.customReflectionTexture = tex;

                        var colours = new Unity.Collections.NativeArray<Color32>(6,
                            Unity.Collections.Allocator.Temp,
                            Unity.Collections.NativeArrayOptions.UninitializedMemory);

                        for (int i = 0; i < 6; i++)
                        {
                            colours[i] = readback.GetData<Color32>(i)[0];
                        }

                        RenderSettings.ambientSkyColor = (Color)colours[(int)CubemapFace.PositiveY];
                        RenderSettings.ambientGroundColor = (Color)colours[(int)CubemapFace.NegativeY];
                        RenderSettings.ambientEquatorColor = 0.25f
                            * ((Color)colours[(int)CubemapFace.PositiveX]
                            + (Color)colours[(int)CubemapFace.NegativeX]
                            + (Color)colours[(int)CubemapFace.PositiveZ]
                            + (Color)colours[(int)CubemapFace.NegativeZ]);
                    }

                    readback = await AsyncGPUReadback.RequestAsync(tex, tex.mipmapCount - 1,
                        x: 0, width: 1, y: 0, height: 1, z: 0, depth: 6, TextureFormat.RGBA32);
                }
                else
                {
                    await Awaitable.NextFrameAsync();
                }
            }
        }
    }
}
