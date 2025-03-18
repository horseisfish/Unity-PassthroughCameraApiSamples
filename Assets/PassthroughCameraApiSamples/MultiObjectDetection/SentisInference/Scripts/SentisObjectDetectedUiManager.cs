// Copyright (c) Meta Platforms, Inc. and affiliates.

using System;
using System.Collections;
using System.Collections.Generic;
using Meta.XR.Samples;
using UnityEngine;
using UnityEngine.Assertions;

namespace PassthroughCameraSamples.MultiObjectDetection
{
    [MetaCodeSample("PassthroughCameraApiSamples-MultiObjectDetection")]
    public class SentisObjectDetectedUiManager : MonoBehaviour
    {
        [SerializeField] private WebCamTextureManager m_webCamTextureManager;
        private PassthroughCameraEye CameraEye => m_webCamTextureManager.Eye;
        private Vector2Int CameraResolution => m_webCamTextureManager.RequestedResolution;
        [SerializeField] private GameObject m_detectionCanvas;
        [SerializeField] private float m_canvasDistance = 1f;
        
        [Header("Canvas Marker References")]
        [SerializeField] private GameObject m_canvasMarkerPrefab; // Marker prefab for the canvas
        [SerializeField] private Color m_canvasMarkerColor = new Color(0.2f, 0.8f, 1.0f, 0.7f); // Color for canvas markers
        [SerializeField] private float m_markerDepthOffset = 0.1f; // Distance to place markers in front of canvas
        
        private SentisInferenceUiManager m_inferenceUiManager;
        private EnvironmentRayCastSampleManager m_environmentRaycast;
        private List<GameObject> m_canvasMarkers = new List<GameObject>();
        private bool m_markersEnabled = true;

        private IEnumerator Start()
        {
            if (m_webCamTextureManager == null)
            {
                Debug.LogError($"PCA: {nameof(m_webCamTextureManager)} field is required "
                            + $"for the component {nameof(SentisObjectDetectedUiManager)} to operate properly");
                enabled = false;
                yield break;
            }

            // Make sure the manager is disabled in scene and enable it only when the required permissions have been granted
            Assert.IsFalse(m_webCamTextureManager.enabled);
            while (PassthroughCameraPermissions.HasCameraPermission != true)
            {
                yield return null;
            }

            // Set the 'requestedResolution' and enable the manager
            m_webCamTextureManager.RequestedResolution = PassthroughCameraUtils.GetCameraIntrinsics(CameraEye).Resolution;
            m_webCamTextureManager.enabled = true;

            var cameraCanvasRectTransform = m_detectionCanvas.GetComponentInChildren<RectTransform>();
            var leftSidePointInCamera = PassthroughCameraUtils.ScreenPointToRayInCamera(CameraEye, new Vector2Int(0, CameraResolution.y / 2));
            var rightSidePointInCamera = PassthroughCameraUtils.ScreenPointToRayInCamera(CameraEye, new Vector2Int(CameraResolution.x, CameraResolution.y / 2));
            var horizontalFoVDegrees = Vector3.Angle(leftSidePointInCamera.direction, rightSidePointInCamera.direction);
            var horizontalFoVRadians = horizontalFoVDegrees / 180 * Math.PI;
            var newCanvasWidthInMeters = 2 * m_canvasDistance * Math.Tan(horizontalFoVRadians / 2);
            var localScale = (float)(newCanvasWidthInMeters / cameraCanvasRectTransform.sizeDelta.x);
            cameraCanvasRectTransform.localScale = new Vector3(localScale, localScale, localScale);
            
            // Find necessary components
            m_inferenceUiManager = FindObjectOfType<SentisInferenceUiManager>();
            m_environmentRaycast = FindObjectOfType<EnvironmentRayCastSampleManager>();
        }

        private void Update()
        {
            var cameraPose = PassthroughCameraUtils.GetCameraPoseInWorld(CameraEye);
            // Position the canvas in front of the camera
            m_detectionCanvas.transform.position = cameraPose.position + cameraPose.rotation * Vector3.forward * m_canvasDistance;
            m_detectionCanvas.transform.rotation = Quaternion.Euler(0, cameraPose.rotation.eulerAngles.y, 0);
            
            // Update canvas markers if enabled and canvas is active
            if (m_markersEnabled && m_detectionCanvas.activeSelf && m_inferenceUiManager != null)
            {
                UpdateCanvasMarkers();
            }
        }
        
        private void OnDestroy()
        {
            // Clean up markers
            ClearCanvasMarkers();
        }
        
        /// <summary>
        /// Updates 3D markers to align with detection boxes on the canvas
        /// </summary>
        private void UpdateCanvasMarkers()
        {
            // Ensure we have the necessary components
            if (m_canvasMarkerPrefab == null || m_inferenceUiManager == null)
                return;
                
            // Clear existing markers
            ClearCanvasMarkers();
            
            // Get current detection boxes from the inference manager
            var boxes = m_inferenceUiManager.BoxDrawn;
            if (boxes == null || boxes.Count == 0)
                return;
                
            // Create new markers for each detection box
            foreach (var box in boxes)
            {
                CreateCanvasMarker(box.WorldPos, box.ClassName);
            }
        }
        
        /// <summary>
        /// Creates a 3D marker for a detection on the canvas
        /// </summary>
        private void CreateCanvasMarker(Vector3 worldPos, string className)
        {
            // Determine marker position - either using environment raycast or based on canvas position
            Vector3 position;
            Quaternion rotation = Quaternion.identity;
            
            if (m_environmentRaycast != null)
            {
                var worldTransform = m_environmentRaycast.PlaceGameObject(worldPos);
                position = worldTransform.position;
                rotation = worldTransform.rotation;
            }
            else
            {
                // If no environment data, position slightly in front of the canvas
                var cameraPose = PassthroughCameraUtils.GetCameraPoseInWorld(CameraEye);
                Vector3 dirToPos = (worldPos - cameraPose.position).normalized;
                position = worldPos + dirToPos * m_markerDepthOffset;
            }
            
            // Create marker
            GameObject marker = Instantiate(m_canvasMarkerPrefab, position, rotation);
            m_canvasMarkers.Add(marker);
            
            // Set marker properties
            DetectionSpawnMarkerAnim markerAnim = marker.GetComponent<DetectionSpawnMarkerAnim>();
            if (markerAnim != null)
            {
                markerAnim.SetYoloClassName(className);
                markerAnim.SetTemporaryAppearance(true, m_canvasMarkerColor);
            }
        }
        
        /// <summary>
        /// Clears all canvas markers
        /// </summary>
        private void ClearCanvasMarkers()
        {
            foreach (var marker in m_canvasMarkers)
            {
                if (marker != null)
                {
                    Destroy(marker);
                }
            }
            m_canvasMarkers.Clear();
        }

        /// <summary>
        /// Hides the detection canvas
        /// </summary>
        public void HideDetectionCanvas()
        {
            if (m_detectionCanvas != null)
            {
                m_detectionCanvas.SetActive(false);
                ClearCanvasMarkers(); // Clear markers when canvas is hidden
            }
        }

        /// <summary>
        /// Shows the detection canvas
        /// </summary>
        public void ShowDetectionCanvas()
        {
            if (m_detectionCanvas != null)
            {
                m_detectionCanvas.SetActive(true);
            }
        }
        
        /// <summary>
        /// Enables or disables 3D markers on the canvas
        /// </summary>
        public void SetCanvasMarkersEnabled(bool enabled)
        {
            m_markersEnabled = enabled;
            if (!enabled)
            {
                ClearCanvasMarkers();
            }
        }

        /// <summary>
        /// Completely disables the detection canvas and its functionality
        /// </summary>
        public void DisableDetectionCanvasCompletely()
        {
            if (m_detectionCanvas != null)
            {
                m_detectionCanvas.SetActive(false);
                ClearCanvasMarkers();
                
                // Disable the Canvas component to stop rendering
                Canvas canvas = m_detectionCanvas.GetComponent<Canvas>();
                if (canvas != null)
                {
                    canvas.enabled = false;
                }
            }
        }
    }
}
