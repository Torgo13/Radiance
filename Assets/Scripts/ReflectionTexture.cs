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
                _ = RenderAsync(GetComponent<ReflectionProbe>(), transformHandle, destroyCancellationToken);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        public static void SetAngle(float angle)
        {
            var sun = RenderSettings.sun;
            if (sun != null)
            {
                var sunTransform = sun.transformHandle;
                sunTransform.rotation = Quaternion.Euler(new Vector3(angle, -60, 0));
            }
        }

        public static void SetIndirect()
        {
            var cam = Camera.main;
            if (cam != null && cam.TryGetComponent<ReflectionProbe>(out var probe))
            {
                var intensity = probe.intensity;
                probe.intensity = Mathf.Approximately(0, intensity) ? 1 : 0;
            }
        }

        public static void SetIndirect(bool enabled)
        {
            var cam = Camera.main;
            if (cam != null && cam.TryGetComponent<ReflectionProbe>(out var probe))
            {
                probe.intensity = enabled ? 1 : 0;
            }
        }

        public static void SetPostProcessing(bool enabled)
        {
            var volume = FindAnyObjectByType<Volume>();
            if (volume != null)
            {
                var profile = volume.profile;
                if (profile != null)
                {
                    if (profile.TryGet<Bloom>(out var bloom))
                    {
                        bloom.active = enabled;
                    }

                    if (profile.TryGet<ChromaticAberration>(out var chromaticAberration))
                    {
                        chromaticAberration.active = enabled;
                    }

                    if (profile.TryGet<DepthOfField>(out var depthOfField))
                    {
                        depthOfField.active = enabled;
                    }

                    if (profile.TryGet<FilmGrain>(out var filmGrain))
                    {
                        filmGrain.active = enabled;
                    }

                    if (profile.TryGet<MotionBlur>(out var motionBlur))
                    {
                        motionBlur.active = enabled;
                    }

                    /*if (profile.TryGet<Vignette>(out var vignette))
                    {
                        vignette.active = enabled;
                    }*/
                    
#if ZERO // USING_VOLUMETRIC_FOG
                    if (profile.TryGet<VolumetricFogVolumeComponent>(out var volumetricFog))
                    {
                        var cam = Camera.main;
                        volumetricFog.active = enabled && cam != null && cam.TryGetComponent<Light>(out var light) && light.enabled;
                    }
#endif // ZERO // USING_VOLUMETRIC_FOG
                }
            }
        }

        [System.Diagnostics.Conditional("USING_VOLUMETRIC_FOG")]
        public static void SetVolumetricFog(bool enabled)
        {
            var volume = FindAnyObjectByType<Volume>();
            if (volume == null)
                return;
            
            var profile = volume.profile;
            if (profile == null || !profile.TryGet<VolumetricFogVolumeComponent>(out var volumetricFog))
                return;
            
            var cam = Camera.main;
            volumetricFog.active = enabled && cam != null && cam.TryGetComponent<Light>(out var light) && light.enabled;
        }

        static async Awaitable RenderAsync(ReflectionProbe probe, TransformHandle handle,
            System.Threading.CancellationToken ct)
        {
            handle.GetPositionAndRotation(out var previousPos, out var previousRot);
            int renderID = probe.RenderProbe();

            Material skybox = RenderSettings.skybox;
            int Tex = Shader.PropertyToID("_Tex");

            AsyncGPUReadbackRequest readback = default;

            while (!ct.IsCancellationRequested)
            {
                handle.GetPositionAndRotation(out var currentPos, out var currentRot);
                if (currentPos != previousPos || currentRot != previousRot)
                {
                    previousPos = currentPos;
                    previousRot = currentRot;
                    //renderID = probe.RenderProbe();
                }

                if (probe == null || !probe.isActiveAndEnabled || Mathf.Approximately(0, probe.intensity))
                {
                    skybox.SetTexture(Tex, null);
                    RenderSettings.customReflectionTexture = null;
                    RenderSettings.ambientSkyColor = default;
                    RenderSettings.ambientEquatorColor = default;
                    RenderSettings.ambientGroundColor = default;
                    await Awaitable.NextFrameAsync();
                    continue;
                }
                
                if (probe!.IsFinishedRendering(renderID))
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

                        var ambientSkyColor = readback.GetData<Color32>((int)CubemapFace.PositiveY)[0];
                        var ambientGroundColor = readback.GetData<Color32>((int)CubemapFace.NegativeY)[0];
#if !ZERO
                        var ambientEquatorColor = 0.25f
                            * ((Color)readback.GetData<Color32>((int)CubemapFace.PositiveX)[0]
                            + (Color)readback.GetData<Color32>((int)CubemapFace.NegativeX)[0]
                            + (Color)readback.GetData<Color32>((int)CubemapFace.PositiveZ)[0]
                            + (Color)readback.GetData<Color32>((int)CubemapFace.NegativeZ)[0]);
#else
                        var positiveX = (Color)readback.GetData<Color32>((int)CubemapFace.PositiveX)[0];
                        var negativeX = (Color)readback.GetData<Color32>((int)CubemapFace.NegativeX)[0];
                        var positiveZ = (Color)readback.GetData<Color32>((int)CubemapFace.PositiveZ)[0];
                        var negativeZ = (Color)readback.GetData<Color32>((int)CubemapFace.NegativeZ)[0];
                        Color.RGBToHSV(positiveX, out float pxh, out float pxs, out float pxv);
                        Color.RGBToHSV(negativeX, out float nxh, out float nxs, out float nxv);
                        Color.RGBToHSV(positiveZ, out float pzh, out float pzs, out float pzv);
                        Color.RGBToHSV(negativeZ, out float nzh, out float nzs, out float nzv);
                        var ambientEquatorColor = Color.HSVToRGB(
                            0.25f * (pxh + nxh + pzh + nzh),
                            0.25f * (pxs + nxs + pzs + nzs),
                            Math.Max(pxv, Math.Max(nxv, Math.Max(pzv, nzv))),
                            hdr: false);
#endif // ZERO

                        RenderSettings.ambientSkyColor = ambientSkyColor; //Color.LerpUnclamped(RenderSettings.ambientSkyColor, ambientSkyColor, 0.5f);
                        RenderSettings.ambientEquatorColor = ambientEquatorColor; //Color.LerpUnclamped(RenderSettings.ambientEquatorColor, ambientEquatorColor, 0.5f);
                        RenderSettings.ambientGroundColor = ambientGroundColor; //Color.LerpUnclamped(RenderSettings.ambientGroundColor, ambientGroundColor, 0.5f);
                    }

                    //if (enabled)
                    {
                        readback = await AsyncGPUReadback.RequestAsync(tex, tex!.mipmapCount - 1,
                            x: 0, width: 1, y: 0, height: 1, z: 0, depth: 6, TextureFormat.RGBA32);
                    }
                }
                else
                {
                    await Awaitable.NextFrameAsync();
                }
            }
        }

        static Color SampleCubemapBilinear(Vector3 forward, Unity.Collections.NativeArray<Color32> colours)
        {
            float l = Vector3.Distance(forward, Vector3.left);
            float r = Vector3.Distance(forward, Vector3.right);
            float d = Vector3.Distance(forward, Vector3.down);
            float u = Vector3.Distance(forward, Vector3.up);
            float b = Vector3.Distance(forward, Vector3.back);
            float f = Vector3.Distance(forward, Vector3.forward);

            /*
            l *= System.Math.Abs(l);
            r *= System.Math.Abs(r);
            d *= System.Math.Abs(d);
            u *= System.Math.Abs(u);
            b *= System.Math.Abs(b);
            f *= System.Math.Abs(f);
            */

            Color sum
                = l * (Color)colours[(int)CubemapFace.PositiveX]
                + r * (Color)colours[(int)CubemapFace.NegativeX]
                + d * (Color)colours[(int)CubemapFace.PositiveY]
                + u * (Color)colours[(int)CubemapFace.NegativeY]
                + b * (Color)colours[(int)CubemapFace.PositiveZ]
                + f * (Color)colours[(int)CubemapFace.NegativeZ];

            // Scale the output channels to 0 - 1
            return sum / (l + r + d + u + b + f);
        }

        static Color SampleCubemapBilinear(Vector3 forward, AsyncGPUReadbackRequest readback)
        {
            float l = Vector3.Distance(forward, Vector3.left);
            float r = Vector3.Distance(forward, Vector3.right);
            float d = Vector3.Distance(forward, Vector3.down);
            float u = Vector3.Distance(forward, Vector3.up);
            float b = Vector3.Distance(forward, Vector3.back);
            float f = Vector3.Distance(forward, Vector3.forward);

            Color sum
                = l * (Color)readback.GetData<Color32>((int)CubemapFace.PositiveX)[0]
                + r * (Color)readback.GetData<Color32>((int)CubemapFace.NegativeX)[0]
                + d * (Color)readback.GetData<Color32>((int)CubemapFace.PositiveY)[0]
                + u * (Color)readback.GetData<Color32>((int)CubemapFace.NegativeY)[0]
                + b * (Color)readback.GetData<Color32>((int)CubemapFace.PositiveZ)[0]
                + f * (Color)readback.GetData<Color32>((int)CubemapFace.NegativeZ)[0];

            // Scale the output channels to 0 - 1
            return sum / (l + r + d + u + b + f);
        }

        static Color SampleEquatorBilinear(Vector3 forward, Unity.Collections.NativeArray<Color32> colours)
        {
            float l = Vector3.Distance(forward, Vector3.left);
            float r = Vector3.Distance(forward, Vector3.right);
            float b = Vector3.Distance(forward, Vector3.back);
            float f = Vector3.Distance(forward, Vector3.forward);

            Color sum
                = l * (Color)colours[(int)CubemapFace.PositiveX]
                + r * (Color)colours[(int)CubemapFace.NegativeX]
                + b * (Color)colours[(int)CubemapFace.PositiveZ]
                + f * (Color)colours[(int)CubemapFace.NegativeZ];

            // Scale the output channels to 0 - 1
            return sum / (l + r + b + f);
        }

        static Color SampleEquatorBilinear(Vector2 forward, Unity.Collections.NativeArray<Color32> colours)
        {
#if ZERO
            float l = System.Math.Clamp(0.5f * Vector2.Distance(forward, Vector2.left), 0, 1);
            float r = System.Math.Clamp(0.5f * Vector2.Distance(forward, Vector2.right), 0, 1);
            float b = System.Math.Clamp(0.5f * Vector2.Distance(forward, Vector2.down), 0, 1);
            float f = System.Math.Clamp(0.5f * Vector2.Distance(forward, Vector2.up), 0, 1);
#else
            float l = Vector2.Distance(forward, Vector2.left);
            float r = Vector2.Distance(forward, Vector2.right);
            float b = Vector2.Distance(forward, Vector2.down);
            float f = Vector2.Distance(forward, Vector2.up);
#endif // ZERO

            /*l = Mathf.Sqrt(l);
            r = Mathf.Sqrt(r);
            b = Mathf.Sqrt(b);
            f = Mathf.Sqrt(f);*/

            Color sum
                = l * (Color)colours[(int)CubemapFace.PositiveX]
                + r * (Color)colours[(int)CubemapFace.NegativeX]
                + b * (Color)colours[(int)CubemapFace.PositiveZ]
                + f * (Color)colours[(int)CubemapFace.NegativeZ];

            // Scale the output channels to 0 - 1
            return sum / (l + r + b + f);
        }

        static Color SampleEquatorBilinear(Vector3 forward, AsyncGPUReadbackRequest readback)
        {
            float l = Vector3.Distance(forward, Vector3.left);
            float r = Vector3.Distance(forward, Vector3.right);
            float b = Vector3.Distance(forward, Vector3.back);
            float f = Vector3.Distance(forward, Vector3.forward);

            Color sum
                = l * (Color)readback.GetData<Color32>((int)CubemapFace.PositiveX)[0]
                + r * (Color)readback.GetData<Color32>((int)CubemapFace.NegativeX)[0]
                + b * (Color)readback.GetData<Color32>((int)CubemapFace.PositiveZ)[0]
                + f * (Color)readback.GetData<Color32>((int)CubemapFace.NegativeZ)[0];

            // Scale the output channels to 0 - 1
            return sum / (l + r + b + f);
        }
    }
}
