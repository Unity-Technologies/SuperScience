using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEditor;
using UnityEngine;

namespace Unity.Labs.SuperScience
{
    public class SolidColorTextures : EditorWindow
    {
        [StructLayout(LayoutKind.Explicit)]
        struct Color32ToInt
        {
            [FieldOffset(0)]
            int m_IntValue;

            [FieldOffset(0)]
            Color32 m_Color;

            Color32ToInt(Color32 color)
            {
                m_IntValue = 0;
                m_Color = color;
            }

            public static int Convert(Color32 color)
            {
                var convert = new Color32ToInt(color);
                return convert.m_IntValue;
            }
        }

        const int k_PreviewTextureWidth = 120;

        Vector2 m_ScrollPosition;
        List<(string, Texture2D)> m_TextureAssets;
        readonly HashSet<(string, Texture2D)> m_PreviewPool = new HashSet<(string, Texture2D)>();
        readonly List<(string, Texture2D)> m_CurrentPreviewPool = new List<(string, Texture2D)>();

        [MenuItem("Window/SuperScience/Solid Color Textures")]
        static void Init()
        {
            GetWindow<SolidColorTextures>("Solid Color Textures").Show();
        }

        void OnGUI()
        {
            EditorGUIUtility.labelWidth = position.width - k_PreviewTextureWidth;
            if (GUILayout.Button("Refresh"))
                Refresh();

            if (m_TextureAssets == null)
            {
                GUIUtility.ExitGUI();
                return;
            }

            using (var scrollView = new GUILayout.ScrollViewScope(m_ScrollPosition))
            {
                m_ScrollPosition = scrollView.scrollPosition;
                foreach (var (path, texture) in m_TextureAssets)
                {
                    EditorGUILayout.ObjectField(path, texture, typeof(Texture2D), false);
                }
            }
        }

        void Update()
        {
            m_CurrentPreviewPool.Clear();
            m_CurrentPreviewPool.AddRange(m_PreviewPool);
            foreach (var (path, textureAsset) in m_CurrentPreviewPool)
            {
                var preview = AssetPreview.GetAssetPreview(textureAsset);
                if (preview == null)
                    continue;

                CheckForSolidColorTexture(path, preview, textureAsset);
                m_PreviewPool.Remove((path, textureAsset));
            }
        }

        void Refresh()
        {
            var guids = AssetDatabase.FindAssets("t:Texture2D", new[] {"Assets", "Packages"});
            if (guids == null || guids.Length == 0)
                return;

            m_TextureAssets = new List<(string, Texture2D)>();
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path))
                {
                    Debug.Log($"Could not convert {guid} to path");
                    continue;
                }

                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (texture == null)
                {
                    Debug.LogWarning($"Could not load texture at {path}");
                    continue;
                }

                // For non-readable textures, get a preview texture which is readable
                // TODO: get a full-size preview; AssetPreview.GetAssetPreview returns a 128x128 texture
                if (!texture.isReadable)
                {
                    m_PreviewPool.Add((path, texture));
                    continue;
                }

                CheckForSolidColorTexture(path, texture, texture);
            }
        }

        void CheckForSolidColorTexture(string path, Texture2D texture, Texture2D textureAsset)
        {
            // Skip "degenerate" textures like font atlases
            if (texture.width == 0 || texture.height == 0)
                return;

            var pixels = texture.GetPixels32();
            if (pixels == null)
            {
                Debug.LogWarning($"Could not read {texture}");
                return;
            }

            var pixelCount = pixels.Length;
            if (pixelCount == 0)
            {
                Debug.LogWarning($"No pixels in {texture}");
                return;
            }

            var firstPixel = Color32ToInt.Convert(pixels[0]);
            var isSolidColor = true;
            for (var i = 0; i < pixelCount; i++)
            {
                var pixel = Color32ToInt.Convert(pixels[i]);
                if (pixel != firstPixel)
                {
                    isSolidColor = false;
                    break;
                }
            }

            if (isSolidColor)
                m_TextureAssets.Add((path, textureAsset));
        }
    }
}
