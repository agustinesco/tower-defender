using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections;
using System.Collections.Generic;

namespace TowerDefense.UI
{
    public class CameraController : MonoBehaviour
    {
        [SerializeField] private float minZoom = 20f;
        [SerializeField] private float maxZoom = 300f;
        [SerializeField] private float panSpeed = 0.004f;
        [SerializeField] private float zoomSpeed = 0.1f;
        [SerializeField] private float initialPanBounds = 100f;

        private float currentPanBounds;

        [Header("Auto Pan Settings")]
        [SerializeField] private float autoPanDuration = 1.5f;
        [SerializeField] private float expandedViewPadding = 10f;

        private Camera cam;
        private Vector3 lastPanPosition;
        private float lastPinchDistance;
        private bool isPanning;
        private bool isAutoPanning;

        private Vector3 targetPosition;
        private float targetZoom;
        private const float PanSmoothing = 8f;
        private const float ZoomSmoothing = 10f;

        [HideInInspector] public PieceDragHandler pieceDragHandler;

        private void Awake()
        {
            cam = GetComponent<Camera>();
            if (cam == null)
            {
                cam = Camera.main;
            }
            currentPanBounds = initialPanBounds;
            targetPosition = transform.position;
        }

        private void Start()
        {
            // Set up orthographic top-down view
            if (cam != null)
            {
                cam.orthographic = true;
                cam.orthographicSize = (minZoom + maxZoom) / 2f;
                targetZoom = cam.orthographicSize;
                transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                transform.position = new Vector3(0f, 50f, 0f);
                targetPosition = transform.position;

                // Inset viewport based on device safe area (handles notches/cutouts)
                var sa = Screen.safeArea;
                int sw = Screen.width;
                int sh = Screen.height;
                if (sw > 0 && sh > 0)
                {
                    float left = sa.x / sw;
                    float bottom = sa.y / sh;
                    float width = sa.width / sw;
                    float height = sa.height / sh;
                    cam.rect = new Rect(left, bottom, width, height);
                }

                // Background camera fills margins with black
                var bgCamObj = new GameObject("BackgroundCamera");
                var bgCam = bgCamObj.AddComponent<Camera>();
                bgCam.depth = cam.depth - 1;
                bgCam.clearFlags = CameraClearFlags.SolidColor;
                bgCam.backgroundColor = Color.black;
                bgCam.cullingMask = 0;
                bgCam.rect = new Rect(0f, 0f, 1f, 1f);
            }
        }

        private void Update()
        {
            HandleTouchInput();
            HandleMouseInput(); // For editor testing
        }

        private void HandleTouchInput()
        {
            if (pieceDragHandler != null && pieceDragHandler.IsInteracting) return;

            if (Input.touchCount == 1)
            {
                Touch touch = Input.GetTouch(0);

                // Check if touch is over UI element
                bool isOverUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(touch.fingerId);

                if (touch.phase == TouchPhase.Began && !isOverUI)
                {
                    lastPanPosition = touch.position;
                    isPanning = true;
                }
                else if (touch.phase == TouchPhase.Moved && isPanning)
                {
                    Vector3 delta = (Vector3)touch.position - lastPanPosition;
                    // Scale pan speed by camera zoom level for consistent feel
                    float zoomFactor = cam != null ? cam.orthographicSize / 40f : 1f;
                    Pan(-delta * panSpeed * zoomFactor);
                    lastPanPosition = touch.position;
                }
                else if (touch.phase == TouchPhase.Ended)
                {
                    isPanning = false;
                }
            }
            else if (Input.touchCount == 2)
            {
                isPanning = false;

                Touch touch0 = Input.GetTouch(0);
                Touch touch1 = Input.GetTouch(1);

                float currentDistance = Vector2.Distance(touch0.position, touch1.position);

                if (touch1.phase == TouchPhase.Began)
                {
                    lastPinchDistance = currentDistance;
                }
                else if (touch0.phase == TouchPhase.Moved || touch1.phase == TouchPhase.Moved)
                {
                    float delta = currentDistance - lastPinchDistance;
                    Zoom(-delta * zoomSpeed);
                    lastPinchDistance = currentDistance;
                }
            }
        }

        private void HandleMouseInput()
        {
            // On touch devices, let HandleTouchInput be the sole handler.
            // Unity simulates mouse from touches, but IsPointerOverGameObject()
            // without a fingerId is unreliable on mobile and causes false panning.
            if (Input.touchCount > 0) return;

            if (pieceDragHandler != null && pieceDragHandler.IsInteracting) return;

            // Check if mouse is over UI element
            bool isOverUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

            // Left mouse drag for pan (also middle mouse) - skip if over UI
            if ((Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(2)) && !isOverUI)
            {
                lastPanPosition = Input.mousePosition;
                isPanning = true;
            }
            else if ((Input.GetMouseButton(0) || Input.GetMouseButton(2)) && isPanning)
            {
                Vector3 delta = Input.mousePosition - lastPanPosition;
                float zoomFactor = cam != null ? cam.orthographicSize / 40f : 1f;
                Pan(-delta * panSpeed * zoomFactor);
                lastPanPosition = Input.mousePosition;
            }
            else if (Input.GetMouseButtonUp(0) || Input.GetMouseButtonUp(2))
            {
                isPanning = false;
            }

            // Scroll wheel for zoom
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (scroll != 0)
            {
                Zoom(-scroll * 20f);
            }
        }

        private void LateUpdate()
        {
            if (cam == null) return;

            transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * PanSmoothing);
            cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, targetZoom, Time.deltaTime * ZoomSmoothing);
        }

        private void Pan(Vector3 delta)
        {
            Vector3 move = new Vector3(delta.x, 0f, delta.y);
            Vector3 newPosition = targetPosition + move;

            newPosition.x = Mathf.Clamp(newPosition.x, -currentPanBounds, currentPanBounds);
            newPosition.z = Mathf.Clamp(newPosition.z, -currentPanBounds, currentPanBounds);

            targetPosition = newPosition;
        }

        private void Zoom(float delta)
        {
            if (cam == null) return;

            targetZoom = Mathf.Clamp(targetZoom + delta, minZoom, maxZoom);
        }

        /// <summary>
        /// Smoothly pans the camera toward the specified world positions without changing zoom.
        /// </summary>
        /// <param name="positions">List of world positions to pan toward</param>
        public void PanTowardPositions(List<Vector3> positions)
        {
            if (positions == null || positions.Count == 0 || cam == null)
                return;

            StartCoroutine(AnimateCameraPan(positions));
        }

        private IEnumerator AnimateCameraPan(List<Vector3> positions)
        {
            isAutoPanning = true;

            // Calculate center of new positions
            Vector3 center = Vector3.zero;
            foreach (var pos in positions)
            {
                center += pos;
            }
            center /= positions.Count;

            // Pan partially toward the new pieces (keep some of current view)
            Vector3 panTarget = new Vector3(
                Mathf.Lerp(transform.position.x, center.x, 0.5f),
                transform.position.y,
                Mathf.Lerp(transform.position.z, center.z, 0.5f)
            );

            // Clamp to pan bounds
            panTarget.x = Mathf.Clamp(panTarget.x, -currentPanBounds, currentPanBounds);
            panTarget.z = Mathf.Clamp(panTarget.z, -currentPanBounds, currentPanBounds);

            // Store starting position
            Vector3 startPosition = transform.position;

            // Animate
            float elapsed = 0f;
            while (elapsed < autoPanDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / autoPanDuration;

                // Use smooth step for easing
                t = t * t * (3f - 2f * t);

                Vector3 pos = Vector3.Lerp(startPosition, panTarget, t);
                targetPosition = pos;
                transform.position = pos;

                yield return null;
            }

            // Ensure final position is set
            targetPosition = panTarget;
            transform.position = panTarget;

            isAutoPanning = false;
        }

        public void PanToPosition(Vector3 worldTarget)
        {
            StartCoroutine(AnimatePanTo(worldTarget));
        }

        private IEnumerator AnimatePanTo(Vector3 worldTarget)
        {
            isAutoPanning = true;

            Vector3 panTarget = new Vector3(
                Mathf.Clamp(worldTarget.x, -currentPanBounds, currentPanBounds),
                transform.position.y,
                Mathf.Clamp(worldTarget.z, -currentPanBounds, currentPanBounds)
            );

            Vector3 startPosition = transform.position;
            float elapsed = 0f;
            while (elapsed < autoPanDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / autoPanDuration;
                t = t * t * (3f - 2f * t);

                Vector3 pos = Vector3.Lerp(startPosition, panTarget, t);
                targetPosition = pos;
                transform.position = pos;
                yield return null;
            }

            targetPosition = panTarget;
            transform.position = panTarget;
            isAutoPanning = false;
        }

        /// <summary>
        /// Checks if the camera is currently auto-panning
        /// </summary>
        public bool IsAutoPanning => isAutoPanning;

        /// <summary>
        /// Expands the camera pan bounds to include the specified positions.
        /// </summary>
        /// <param name="positions">World positions that should be reachable by the camera</param>
        /// <param name="padding">Extra padding around the positions</param>
        public void ExpandBoundsToInclude(List<Vector3> positions, float padding = 20f)
        {
            if (positions == null || positions.Count == 0)
                return;

            foreach (var pos in positions)
            {
                float requiredBoundsX = Mathf.Abs(pos.x) + padding;
                float requiredBoundsZ = Mathf.Abs(pos.z) + padding;
                float requiredBounds = Mathf.Max(requiredBoundsX, requiredBoundsZ);

                if (requiredBounds > currentPanBounds)
                {
                    currentPanBounds = requiredBounds;
                    Debug.Log($"CameraController: Pan bounds expanded to {currentPanBounds}");
                }
            }
        }

        /// <summary>
        /// Fits the camera to show all provided positions with padding.
        /// Adjusts both position and zoom level.
        /// </summary>
        public void FitToPositions(List<Vector3> positions, float padding = 30f)
        {
            if (positions == null || positions.Count == 0 || cam == null)
                return;

            // Calculate bounds of all positions
            Vector3 min = positions[0];
            Vector3 max = positions[0];

            foreach (var pos in positions)
            {
                min.x = Mathf.Min(min.x, pos.x);
                min.z = Mathf.Min(min.z, pos.z);
                max.x = Mathf.Max(max.x, pos.x);
                max.z = Mathf.Max(max.z, pos.z);
            }

            // Calculate center
            Vector3 center = new Vector3(
                (min.x + max.x) / 2f,
                transform.position.y,
                (min.z + max.z) / 2f
            );

            // Calculate required size
            float width = max.x - min.x + padding * 2f;
            float height = max.z - min.z + padding * 2f;

            // For orthographic camera, size is half the vertical size
            // Account for aspect ratio
            float aspectRatio = cam.aspect;
            float requiredSizeForWidth = width / (2f * aspectRatio);
            float requiredSizeForHeight = height / 2f;

            float requiredSize = Mathf.Max(requiredSizeForWidth, requiredSizeForHeight);
            requiredSize = Mathf.Clamp(requiredSize, minZoom, maxZoom);

            // Apply
            targetPosition = center;
            transform.position = center;
            targetZoom = requiredSize;
            cam.orthographicSize = requiredSize;

            // Update pan bounds
            currentPanBounds = Mathf.Max(currentPanBounds, width / 2f + padding, height / 2f + padding);

            Debug.Log($"CameraController: Fit to map - center: {center}, size: {requiredSize}, bounds: {currentPanBounds}");
        }
    }
}