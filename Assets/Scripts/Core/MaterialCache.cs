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
                {
                    _unlitColor = Shader.Find("Unlit/Color");
                    if (_unlitColor == null)
                        _unlitColor = Shader.Find("Sprites/Default");
                }
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
            var shader = UnlitColor;
            if (shader == null) return null;
            var mat = new Material(shader);
            mat.color = color;
            return mat;
        }

        public static Material CreateSpriteDefault()
        {
            var shader = SpritesDefault;
            if (shader == null) return null;
            return new Material(shader);
        }

        public static Material CreateTransparent(Color color)
        {
            var shader = SpritesDefault;
            if (shader == null) return null;
            var mat = new Material(shader);
            mat.color = color;
            return mat;
        }

        public static Material CreateTransparent(Color color, float alpha)
        {
            color.a = alpha;
            return CreateTransparent(color);
        }

        // Cached primitive meshes loaded via Resources.GetBuiltinResource.
        // Avoids GameObject.CreatePrimitive which fails on Android when
        // CapsuleCollider/other collider classes are stripped from the build.
        private static Mesh _cylinderMesh;
        private static Mesh _cubeMesh;
        private static Mesh _sphereMesh;
        private static Mesh _capsuleMesh;
        private static Mesh _quadMesh;

        public static Mesh GetPrimitiveMesh(PrimitiveType type)
        {
            switch (type)
            {
                case PrimitiveType.Cube:
                    if (_cubeMesh == null) _cubeMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
                    return _cubeMesh;
                case PrimitiveType.Sphere:
                    if (_sphereMesh == null) _sphereMesh = Resources.GetBuiltinResource<Mesh>("New-Sphere.fbx");
                    return _sphereMesh;
                case PrimitiveType.Capsule:
                    if (_capsuleMesh == null) _capsuleMesh = Resources.GetBuiltinResource<Mesh>("New-Capsule.fbx");
                    return _capsuleMesh;
                case PrimitiveType.Quad:
                    if (_quadMesh == null) _quadMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
                    return _quadMesh;
                default: // Cylinder and others
                    if (_cylinderMesh == null) _cylinderMesh = Resources.GetBuiltinResource<Mesh>("New-Cylinder.fbx");
                    return _cylinderMesh;
            }
        }

        /// Creates a primitive mesh GameObject without colliders.
        public static GameObject CreatePrimitive(PrimitiveType type)
        {
            var obj = new GameObject(type.ToString());
            obj.AddComponent<MeshFilter>().sharedMesh = GetPrimitiveMesh(type);
            obj.AddComponent<MeshRenderer>();
            return obj;
        }
    }
}
