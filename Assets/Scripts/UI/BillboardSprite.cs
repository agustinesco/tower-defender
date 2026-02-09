using UnityEngine;

namespace TowerDefense.UI
{
    public class BillboardSprite : MonoBehaviour
    {
        private Camera cachedCamera;

        private void Start()
        {
            cachedCamera = Camera.main;
        }

        private void LateUpdate()
        {
            if (cachedCamera == null)
            {
                cachedCamera = Camera.main;
                if (cachedCamera == null) return;
            }

            transform.rotation = cachedCamera.transform.rotation;
        }
    }
}
