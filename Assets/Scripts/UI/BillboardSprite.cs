using UnityEngine;

namespace TowerDefense.UI
{
    public class BillboardSprite : MonoBehaviour
    {
        private void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null) return;

            transform.rotation = cam.transform.rotation;
        }
    }
}
