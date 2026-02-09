using UnityEngine;
using System.Collections.Generic;

namespace TowerDefense.Core
{
    public static class MaterialCache
    {
        private static Shader _unlitColor;
        private static Shader _spritesDefault;
        private static Shader _standard;

        private static readonly Queue<MaterialPropertyBlock> _propertyBlockPool = new Queue<MaterialPropertyBlock>();

        public static MaterialPropertyBlock GetPropertyBlock()
        {
            if (_propertyBlockPool.Count > 0)
                return _propertyBlockPool.Dequeue();
            return new MaterialPropertyBlock();
        }

        public static void ReturnPropertyBlock(MaterialPropertyBlock block)
        {
            if (block == null) return;
            block.Clear();
            _propertyBlockPool.Enqueue(block);
        }

        public static Shader UnlitColor
        {
            get
            {
                if (_unlitColor == null)
                    _unlitColor = Shader.Find("Unlit/Color");
                return _unlitColor;
            }
        }

        public static Shader SpritesDefault
        {
            get
            {
                if (_spritesDefault == null)
                    _spritesDefault = Shader.Find("Sprites/Default");
                return _spritesDefault;
            }
        }

        public static Shader Standard
        {
            get
            {
                if (_standard == null)
                    _standard = Shader.Find("Standard");
                return _standard;
            }
        }

        public static Material CreateUnlit(Color color)
        {
            var mat = new Material(UnlitColor);
            mat.color = color;
            return mat;
        }

        public static Material CreateSpriteDefault()
        {
            return new Material(SpritesDefault);
        }

        public static Material CreateTransparent(Color color)
        {
            var mat = new Material(Standard);
            mat.SetFloat("_Mode", 3);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = 3000;
            mat.color = color;
            return mat;
        }

        public static Material CreateTransparent(Color color, float alpha)
        {
            color.a = alpha;
            return CreateTransparent(color);
        }
    }
}
