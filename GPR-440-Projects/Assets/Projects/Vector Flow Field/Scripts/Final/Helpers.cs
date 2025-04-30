using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace VectorFlowFieldPathfinding
{
    public enum DepthMode
    {
        None = 0,
        Depth16 = 16,
        Dpeth24 = 24,
    }

    public static class VFFHelper
    {
        public static void Dispatch(ComputeShader shader, int numIterationsX, int numIterationsY = 1, int numIterationsZ = 1, int kernelIndex = 0)
        {
            Vector3Int threadGroupSizes = GetThreadGroupSizes(shader, kernelIndex);
            int numGroupsX = Mathf.CeilToInt(numIterationsX/threadGroupSizes.x);
            int numGroupsY = Mathf.CeilToInt(numIterationsY/threadGroupSizes.y);
            int numGroupsZ = Mathf.CeilToInt(numIterationsZ/threadGroupSizes.z);
            shader.Dispatch(kernelIndex,numGroupsX, numGroupsY, numGroupsZ);
        }
        public static Vector3Int GetThreadGroupSizes(ComputeShader shader, int kernelIndex = 0)
        {
            uint x, y, z;
            shader.GetKernelThreadGroupSizes(kernelIndex, out x, out y, out z);
            return new Vector3Int((int)x, (int)y, (int)z);
        }

        public static void Release(params ComputeBuffer[] buffers)
        {
            for(int i = 0; i < buffers.Length; i++)
            {
                if(buffers[i] != null) buffers[i].Release();
            }
        }

        public static void Release(params RenderTexture[] textures)
        {
            for(int i = 0;i < textures.Length; i++) 
            { 
                if (textures[i] != null) textures[i].Release(); 
            }
        }

        public static void SetTexture(ComputeShader shader, Texture texture, string id, params int[] kernels)
        {
            for (int i = 0; i < kernels.Length; i++)
            {
                shader.SetTexture(kernels[i],id, texture);
            }
        }

        public static void SetBuffer(ComputeShader shader, ComputeBuffer buffer, string id, params int[] kernels)
        {
            for (int i = 0; i <= kernels.Length; i++)
            {
                shader.SetBuffer(kernels[i],id,buffer);
            }
        }

        public static RenderTexture CreateRenderTexture(int width, int height, FilterMode filterMode, GraphicsFormat format, string name = "Unnamed", DepthMode mode = DepthMode.None, bool useMipMaps = false)
        {
            RenderTexture texture = new RenderTexture(width, height, (int)mode);
            texture.graphicsFormat = format;
            texture.enableRandomWrite = true;
            texture.autoGenerateMips = false;
            texture.useMipMap = useMipMaps;
            texture.Create();

            texture.name = name;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = filterMode;
            return texture;

        }
    }

    public class VFFParameters
    {
        private Dictionary<string,object> keyValuePairs = new Dictionary<string,object>();
        private Dictionary<string,Type> keyTypePairs = new Dictionary<string,Type>();

        public void Set<T>(string key, T value)
        {
            keyValuePairs[key] = value;
            keyTypePairs[key] = typeof(T);
        }

        public T Get<T>(string key)
        {
            if(!keyValuePairs.ContainsKey(key))
            {
                throw new KeyNotFoundException($"Parameter '{key}' not found");
            }
            if(typeof(T) != keyTypePairs[key])
            {
                throw new InvalidCastException($"Parameter '{key}' is of type '{keyTypePairs[key].Name}' and cannot be cast to '{typeof(T).Name}'");
            }
            return (T)keyValuePairs[key];
        }

        public bool TryGet<T>(string key, out T value)
        {
            value = default(T);
            if (!keyValuePairs.ContainsKey(key)) return false;
            if (typeof(T) != keyTypePairs[key]) return false;

            value = (T)keyValuePairs[key];
            return true;
        }

        public T GetOrDefault<T>(string key, T defaultValue = default)
        {
            return TryGet(key, out T value) ? value : defaultValue;
        }

        public Type GetParameterType(string key)
        {
            return keyTypePairs.TryGetValue(key, out Type type) ? type : null;
        }

        public bool Contains(string key)
        {
            return keyValuePairs.ContainsKey(key);
        }

        public bool Remove(string key)
        {
            bool removed = keyValuePairs.Remove(key);
            removed &= keyTypePairs.Remove(key);
            return removed;
        }

        public IEnumerable<string> Keys => keyValuePairs.Keys;
    }
}