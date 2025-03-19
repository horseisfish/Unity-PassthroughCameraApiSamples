using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Meta.XR.Samples;
using UnityEngine.Events;

namespace PassthroughCameraSamples.MultiObjectDetection
{
    [MetaCodeSample("PassthroughCameraApiSamples-MultiObjectDetection")]
public class ObjectTrackingMoving : MonoBehaviour
{

    [Header("Detection manager references")]
    [SerializeField] private DetectionManager m_detectionManager;

    [Header("Controls configuration")]
    [SerializeField] private OVRInput.RawButton m_actionButton = OVRInput.RawButton.B;

    [Header("Placement configureation")]
        [SerializeField] private GameObject m_spwanMarker;
        [SerializeField] private EnvironmentRayCastSampleManager m_environmentRaycast;
        [SerializeField] private float m_spawnDistance = 0.25f;
        [SerializeField] private float m_detectionDistance = 0.35f;

    [Header("Sentis inference ref")]
        [SerializeField] private SentisInferenceRunManager m_runInference;
        [SerializeField] private SentisInferenceUiManager m_uiInference;

    public UnityEvent<int> OnObjectsIdentified;

    private List<GameObject> m_spwanedEntities = new();
    
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if(m_detectionManager.m_isStarted){
            if(OVRInput.GetUp(m_actionButton)){
                SpwanCurrentDetectedObjects();
            }
            foreach (var box in m_uiInference.BoxDrawn)
        {
            UpdateMakerPosition(); 
        }
        }
    }

    /// <summary>
    /// Update position for 3d markers
    /// </summary>
    /// 
    private void UpdateMakerPosition(){
        foreach (var box in m_uiInference.BoxDrawn)
        {
        // Get the real transform using DepthApi
        var markerTransform = m_environmentRaycast.PlaceGameObject(box.WorldPos);
            foreach (var e in m_spwanedEntities)
            {
                var markerClass = e.GetComponent<DetectionSpawnMarkerAnim>();
                if (markerClass)
                {
                    var dist = Vector3.Distance(e.transform.position, markerTransform.position);
                    // Check if the new object is close to the detected object
                    if (dist > m_detectionDistance && markerClass.GetYoloClassName() == box.ClassName)
                    {
                        // Update the marker position
                        e.transform.SetPositionAndRotation(markerTransform.position, markerTransform.rotation);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Spwan 3d markers for the detected objects
    /// </summary>
    private void SpwanCurrentDetectedObjects()
    {
        var count = 0;
        foreach (var box in m_uiInference.BoxDrawn)
        {
            if (PlaceMarkerUsingEnvironmentRaycast(box.WorldPos, box.ClassName))
            {
                UpdateMakerPosition();
            }
            }
            
        }

    private bool PlaceMarkerUsingEnvironmentRaycast(Vector3 boxWorldPos, string className)
        {
            // Get the real transform using DepthApi
            var markerTransform = m_environmentRaycast.PlaceGameObject(boxWorldPos);

            // Check if you spanwed the same object before
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
            }
            

            return !existMarker;
        }
}

}
