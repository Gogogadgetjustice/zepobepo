using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ManagedDoom.Video;

namespace ManagedDoom.Unity
{
    public sealed class UnityVideo : IVideo, IDisposable
    {
        private UnityContext unityContext;

        private Video.Renderer renderer;

        private int textureWidth;
        private int textureHeight;

        private byte[] textureData;

        private MeshRenderer meshRenderer;
        private Texture2D texture;
        private Material material;

        private static readonly string[] ShaderCandidates =
        {
            "Unlit/Texture",
            "Universal Render Pipeline/Unlit",
            "Universal Render Pipeline/2D/Sprite-Unlit-Default",
            "HDRP/Unlit",
            "Sprites/Default",
            "UI/Default",
        };

        private static Shader FindWorkingShader()
        {
            foreach (var name in ShaderCandidates)
            {
                var shader = Shader.Find(name);
                if (shader != null)
                {
                    Logger.Log($"Doom video shader: using '{name}'.");
                    return shader;
                }
            }

            throw new InvalidOperationException(
                "UnityVideo: none of the candidate shaders were found in this build ("
                + string.Join(", ", ShaderCandidates)
                + "). The host game's shader stripping removed all of them - "
                + "bundle a custom shader in an AssetBundle and load it explicitly instead.");
        }

        public UnityVideo(Config config, GameContent content, UnityContext unityContext)
        {
            try
            {
                Logger.Log("Initialize video: ");

                renderer = new Video.Renderer(config, content);

                config.video_gamescreensize = Mathf.Clamp(config.video_gamescreensize, 0, MaxWindowSize);
                config.video_gammacorrection = Mathf.Clamp(config.video_gammacorrection, 0, MaxGammaCorrectionLevel);

                if (config.video_highresolution)
                {
                    textureWidth = 512;
                    textureHeight = 1024;
                }
                else
                {
                    textureWidth = 256;
                    textureHeight = 512;
                }

                this.unityContext = unityContext;

                var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
                go.transform.SetParent(unityContext.Root, false);
                go.name = "Doom_Video";

                // keep rotation but flip the texture vertically via UVs
                go.transform.localEulerAngles = Vector3.forward * -90f;

                // rotate the quad 180 degrees around Y so the visible object is flipped
                go.transform.Rotate(180f, 0f, 0f);

                // shrink the quad slightly so HUD elements aren't cut off
                const float displayShrink = 0.92f;
                go.transform.localScale = new Vector3(3f / 4f * displayShrink, 1f * displayShrink, 1f);

                go.layer = unityContext.Root.gameObject.layer;
                UnityEngine.Object.Destroy(go.GetComponent<Collider>());
                meshRenderer = go.GetComponent<MeshRenderer>();

                textureData = new byte[4 * renderer.Width * renderer.Height];

                // create texture matching renderer buffer (width/height mapping)
                texture = new Texture2D(renderer.Height, renderer.Width, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp
                };

                material = new Material(FindWorkingShader());
                material.name = "Doom_Video";
                material.mainTexture = texture;

                // Flip vertically to match renderer row order and set offset so UVs map correctly.
                material.mainTextureScale = new Vector2(1f, -1f);
                material.mainTextureOffset = new Vector2(0f, 1f);

                meshRenderer.sharedMaterial = material;

                unityContext.Texture = texture;
                unityContext.Material = material;
                unityContext.Renderer = meshRenderer;

                Logger.Log("OK");
            }
            catch
            {
                Logger.Log("Failed");
                Dispose();
                throw;
            }
        }

        public void Render(Doom doom)
        {
            renderer.Render(doom, textureData);

            // Use raw upload to avoid IL2CPP marshalling and keep performance.
            texture.LoadRawTextureData(textureData);
            texture.Apply(false, false);
        }

        public void InitializeWipe()
        {
            renderer.InitializeWipe();
        }

        public bool HasFocus()
        {
            return Application.isFocused;
        }

        public void Dispose()
        {
            Logger.Log("Shutdown renderer.");

            if (texture != null)
            {
                unityContext.Texture = null;
                UnityEngine.Object.Destroy(texture);
                texture = null;
            }

            if (material != null)
            {
                unityContext.Material = null;
                UnityEngine.Object.Destroy(material);
                material = null;
            }

            if (meshRenderer != null)
            {
                unityContext.Renderer = null;
                UnityEngine.Object.Destroy(meshRenderer.gameObject);
                UnityEngine.Object.Destroy(meshRenderer);
            }
        }

        public int WipeBandCount => renderer.WipeBandCount;
        public int WipeHeight => renderer.WipeHeight;

        public int MaxWindowSize => renderer.MaxWindowSize;

        public int WindowSize
        {
            get => renderer.WindowSize;
            set => renderer.WindowSize = value;
        }

        public bool DisplayMessage
        {
            get => renderer.DisplayMessage;
            set => renderer.DisplayMessage = value;
        }

        public int MaxGammaCorrectionLevel => renderer.MaxGammaCorrectionLevel;

        public int GammaCorrectionLevel
        {
            get => renderer.GammaCorrectionLevel;
            set => renderer.GammaCorrectionLevel = value;
        }
    }
}