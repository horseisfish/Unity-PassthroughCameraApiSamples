// Copyright (c) Meta Platforms, Inc. and affiliates.

using System.Collections;
using System.Collections.Generic;
using Meta.XR.Samples;
using UnityEngine;
using UnityEngine.Events;

namespace PassthroughCameraSamples.MultiObjectDetection
{
    [MetaCodeSample("PassthroughCameraApiSamples-MultiObjectDetection")]
    public class DetectionManagerMod : MonoBehaviour
    {
        [SerializeField] private WebCamTextureManager m_webCamTextureManager;

        [Header("Controls configuration")]
        [SerializeField] private OVRInput.RawButton m_actionButton = OVRInput.RawButton.A;

        [Header("Ui references")]
        [SerializeField] private DetectionUiMenuManager m_uiMenuManager;

        [Header("Placement configureation")]
        [SerializeField] private GameObject m_spwanMarker;
        [SerializeField] private EnvironmentRayCastSampleManager m_environmentRaycast;
        [SerializeField] private float m_spawnDistance = 0.25f;
        [SerializeField] private AudioSource m_placeSound;

        [Header("Sentis inference ref")]
        [SerializeField] private SentisInferenceRunManager m_runInference;
        [SerializeField] private SentisInferenceUiManager m_uiInference;

        [Header("UI Canvas Reference")]
        [SerializeField] private SentisObjectDetectedUiManager m_sentisObjectDetectedUiManager;

        [Header("Temporary Markers")]
        [SerializeField] private GameObject m_temporaryMarkerPrefab; // Temporary marker prefab
        [SerializeField] private Color m_temporaryMarkerColor = new Color(0.7f, 0.7f, 1.0f, 0.7f); // Different color for temporary markers

        [Space(10)]
        public UnityEvent<int> OnObjectsIdentified;

        private bool m_isPaused = true;
        private List<GameObject> m_spwanedEntities = new();
        private bool m_isStarted = false;
        private bool m_isSentisReady = false;
        private float m_delayPauseBackTime = 0;

        // Dictionary to track objects and their markers
        private Dictionary<string, TrackedObject> m_trackedObjects = new Dictionary<string, TrackedObject>();
        
        // Structure to hold tracking data
        private class TrackedObject
        {
            public GameObject Marker;
            public string ClassName;
            public Vector3 LastPosition;
            public bool UpdatedThisFrame;
        }

        private Dictionary<string, GameObject> m_temporaryMarkers = new Dictionary<string, GameObject>();
        private bool m_showingTemporaryMarkers = true;

        #region Unity Functions
        private void Awake() => OVRManager.display.RecenteredPose += CleanMarkersCallBack;

        private IEnumerator Start()
        {
            // Wait until Sentis model is loaded
            var sentisInference = FindObjectOfType<SentisInferenceRunManager>();
            while (!sentisInference.IsModelLoaded)
            {
                yield return null;
            }
            m_isSentisReady = true;
            
            // Do NOT hide detection canvas initially - we want to show 2D boxes first
            // Remove the DisableDetectionCanvasCompletely call
        }

        private void Update()
        {
            // Get the WebCamTexture CPU image
            var hasWebCamTextureData = m_webCamTextureManager.WebCamTexture != null;

            if (!m_isStarted)
            {
                // Manage the Initial Ui Menu
                if (hasWebCamTextureData && m_isSentisReady)
                {
                    m_uiMenuManager.OnInitialMenu(m_environmentRaycast.HasScenePermission());
                    m_isStarted = true;
                }
            }
            else
            {
                // Press A button to spawn 3d markers
                if (OVRInput.GetUp(m_actionButton) && m_delayPauseBackTime <= 0)
                {
                    SpwanCurrentDetectedObjects();
                }
                // Cooldown for the A button after return from the pause menu
                m_delayPauseBackTime -= Time.deltaTime;
                if (m_delayPauseBackTime <= 0)
                {
                    m_delayPauseBackTime = 0;
                }
                
                // Mark all tracked objects as not updated this frame
                foreach (var trackedObj in m_trackedObjects.Values)
                {
                    trackedObj.UpdatedThisFrame = false;
                }
                
                // Update positions of all currently detected objects
                if (!m_isPaused && hasWebCamTextureData && !m_runInference.IsRunning())
                {
                    UpdateTrackedObjectPositions();
                    
                    // If we're showing temporary markers, update them
                    if (m_showingTemporaryMarkers)
                    {
                        UpdateTemporaryMarkers();
                    }
                }
                
                // Remove objects that weren't updated for a while (optional)
                // RemoveStaleTrackedObjects();
            }

            // Not start a sentis inference if the app is paused or we don't have a valid WebCamTexture
            if (m_isPaused || !hasWebCamTextureData)
            {
                if (m_isPaused)
                {
                    // Set the delay time for the A button to return from the pause menu
                    m_delayPauseBackTime = 0.1f;
                }
                return;
            }

            // Run a new inference when the current inference finishes
            if (!m_runInference.IsRunning())
            {
                m_runInference.RunInference(m_webCamTextureManager.WebCamTexture);
            }
        }
        #endregion

        #region Marker Functions
        /// <summary>
        /// Clean 3d markers when the tracking space is re-centered.
        /// </summary>
        private void CleanMarkersCallBack()
        {
            foreach (var e in m_spwanedEntities)
            {
                Destroy(e, 0.1f);
            }
            m_spwanedEntities.Clear();
            m_trackedObjects.Clear(); // Also clear the tracked objects
            
            // Clear temporary markers too
            CleanTemporaryMarkers();
            
            // Re-enable temporary markers
            m_showingTemporaryMarkers = true;
            
            OnObjectsIdentified?.Invoke(-1);
            
            // Hide detection canvas if it was showing
            if (m_sentisObjectDetectedUiManager != null)
            {
                m_sentisObjectDetectedUiManager.HideDetectionCanvas();
            }
        }
        
        /// <summary>
        /// Update positions of all tracked objects based on current detection results
        /// </summary>
        private void UpdateTrackedObjectPositions()
        {
            foreach (var box in m_uiInference.BoxDrawn)
            {
                UpdateMarkerPosition(box.WorldPos, box.ClassName);
            }
        }
        
        /// <summary>
        /// Update the position of a marker or create a new one if not exists
        /// </summary>
        private void UpdateMarkerPosition(Vector3 boxWorldPos, string className)
        {
            var key = $"{className}_{boxWorldPos.ToString("F1")}";
            
            // Get the real transform using DepthApi
            var newPosition = m_environmentRaycast.PlaceGameObject(boxWorldPos).position;
            
            if (m_trackedObjects.TryGetValue(key, out var trackedObj))
            {
                // Update existing tracked object
                if (trackedObj.Marker != null)
                {
                    trackedObj.Marker.transform.position = newPosition;
                    trackedObj.LastPosition = newPosition;
                    trackedObj.UpdatedThisFrame = true;
                }
            }
        }
        
        /// <summary>
        /// Update temporary markers that follow detected objects
        /// </summary>
        private void UpdateTemporaryMarkers()
        {
            // Only proceed if we've pressed the action button once
            if (!m_showingTemporaryMarkers)
                return;
            
            // Mark all temporary markers as not updated
            Dictionary<string, bool> markerUpdated = new Dictionary<string, bool>();
            foreach (var key in m_temporaryMarkers.Keys)
            {
                markerUpdated[key] = false;
            }
            
            // Update or create temporary markers for each detected object
            foreach (var box in m_uiInference.BoxDrawn)
            {
                var markerTransform = m_environmentRaycast.PlaceGameObject(box.WorldPos);
                string key = $"{box.ClassName}_{box.WorldPos.ToString("F1")}";
                
                if (m_temporaryMarkers.TryGetValue(key, out GameObject tempMarker))
                {
                    // Update existing temporary marker
                    tempMarker.transform.position = markerTransform.position;
                    markerUpdated[key] = true;
                }
                else
                {
                    // Create new temporary marker
                    CreateTemporaryMarker(key, box.ClassName, markerTransform);
                    markerUpdated[key] = true;
                }
            }
            
            // Remove any temporary markers that weren't updated
            List<string> keysToRemove = new List<string>();
            foreach (var entry in markerUpdated)
            {
                if (!entry.Value)
                {
                    keysToRemove.Add(entry.Key);
                }
            }
            
            foreach (var key in keysToRemove)
            {
                if (m_temporaryMarkers.TryGetValue(key, out GameObject marker))
                {
                    Destroy(marker);
                    m_temporaryMarkers.Remove(key);
                }
            }
        }
        
        /// <summary>
        /// Create a temporary marker for a detected object
        /// </summary>
        private void CreateTemporaryMarker(string key, string className, Transform transform)
        {
            GameObject prefab = m_temporaryMarkerPrefab ?? m_spwanMarker;
            
            GameObject tempMarker = Instantiate(prefab);
            tempMarker.transform.position = transform.position;
            tempMarker.transform.rotation = transform.rotation;
            
            // Set class name in marker
            DetectionSpawnMarkerAnim markerAnim = tempMarker.GetComponent<DetectionSpawnMarkerAnim>();
            if (markerAnim != null)
            {
                markerAnim.SetYoloClassName(className);
                
                // Set different appearance for temporary markers
                if (m_temporaryMarkerPrefab == null) // Only if using same prefab
                {
                    markerAnim.SetTemporaryAppearance(true, m_temporaryMarkerColor);
                }
            }
            
            m_temporaryMarkers[key] = tempMarker;
        }
        
        /// <summary>
        /// Clean up all temporary markers
        /// </summary>
        private void CleanTemporaryMarkers()
        {
            foreach (var marker in m_temporaryMarkers.Values)
            {
                Destroy(marker);
            }
            m_temporaryMarkers.Clear();
        }
        
        /// <summary>
        /// Spwan 3d markers for the detected objects
        /// </summary>
        private void SpwanCurrentDetectedObjects()
        {
            // Switch from 2D boxes to 3D markers - hide the 2D canvas when markers are placed
            if (m_sentisObjectDetectedUiManager != null)
            {
                m_sentisObjectDetectedUiManager.HideDetectionCanvas();
            }
            
            // Show and update 3D markers
            m_showingTemporaryMarkers = true;
            
            m_placeSound.Play();
            var count = 0;
            foreach (var box in m_uiInference.BoxDrawn)
            {
                if (PlaceMarkerUsingEnvironmentRaycast(box.WorldPos, box.ClassName))
                {
                    count++;
                }
            }
            OnObjectsIdentified?.Invoke(count);
        }

        /// <summary>
        /// Place a marker using the environment raycast
        /// </summary>
        private bool PlaceMarkerUsingEnvironmentRaycast(Vector3 boxWorldPos, string className)
        {
            // Get the real transform using DepthApi
            var markerTransform = m_environmentRaycast.PlaceGameObject(boxWorldPos);
            var key = $"{className}_{boxWorldPos.ToString("F1")}";

            // Check if you spawned the same object before
            var existMarker = false;
            foreach (var e in m_spwanedEntities)
            {
                var markerClass = e.GetComponent<DetectionSpawnMarkerAnim>();
                if (markerClass)
                {
                    var dist = Vector3.Distance(e.transform.position, markerTransform.position);
                    if (dist < m_spawnDistance && markerClass.GetYoloClassName() == className)
                    {
                        existMarker = true;
                        break;
                    }
                }
            }

            if (!existMarker)
            {
                // spawn a visual marker
                var eMarker = Instantiate(m_spwanMarker);
                m_spwanedEntities.Add(eMarker);

                // Update marker transform with the real world transform
                eMarker.transform.SetPositionAndRotation(markerTransform.position, markerTransform.rotation);
                eMarker.GetComponent<DetectionSpawnMarkerAnim>().SetYoloClassName(className);
                
                // Add to tracked objects for continuous updates
                m_trackedObjects[key] = new TrackedObject
                {
                    Marker = eMarker,
                    ClassName = className,
                    LastPosition = markerTransform.position,
                    UpdatedThisFrame = true
                };
            }
            else if (m_trackedObjects.TryGetValue(key, out var trackedObj))
            {
                // Update existing tracked object
                trackedObj.UpdatedThisFrame = true;
            }

            return !existMarker;
        }
        
        /// <summary>
        /// Optional: Remove objects that haven't been updated for some time
        /// </summary>
        private void RemoveStaleTrackedObjects()
        {
            List<string> keysToRemove = new List<string>();
            
            foreach (var kvp in m_trackedObjects)
            {
                if (!kvp.Value.UpdatedThisFrame)
                {
                    keysToRemove.Add(kvp.Key);
                    if (kvp.Value.Marker != null)
                    {
                        m_spwanedEntities.Remove(kvp.Value.Marker);
                        Destroy(kvp.Value.Marker);
                    }
                }
            }
            
            foreach (var key in keysToRemove)
            {
                m_trackedObjects.Remove(key);
            }
        }
        #endregion

        #region Public Functions
        /// <summary>
        /// Pause the detection logic when the pause menu is active
        /// </summary>
        public void OnPause(bool pause)
        {
            m_isPaused = pause;
            
            // When unpausing, re-enable temporary markers and hide canvas
            if (!pause)
            {
                m_showingTemporaryMarkers = true;
                
                // Make sure canvas stays hidden
                if (m_sentisObjectDetectedUiManager != null)
                {
                    m_sentisObjectDetectedUiManager.HideDetectionCanvas();
                }
            }
        }
        #endregion
    }
}
