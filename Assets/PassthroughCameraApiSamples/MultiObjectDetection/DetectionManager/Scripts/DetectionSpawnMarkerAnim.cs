// Copyright (c) Meta Platforms, Inc. and affiliates.

using Meta.XR.Samples;
using UnityEngine;

namespace PassthroughCameraSamples.MultiObjectDetection
{
    [MetaCodeSample("PassthroughCameraApiSamples-MultiObjectDetection")]
    public class DetectionSpawnMarkerAnim : MonoBehaviour
    {
        [SerializeField] private Vector3 m_anglesSpeed = new(20.0f, 40.0f, 60.0f);
        [SerializeField] private Transform m_model;
        [SerializeField] private TextMesh m_textModel;
        [SerializeField] private Transform m_textEntity;
        [SerializeField] private Renderer[] m_renderers;

        private Vector3 m_angles;
        private OVRCameraRig m_camera;
        private Color m_originalColor;
        private bool m_isTemporary = false;

        private void Awake()
        {
            if (m_renderers != null && m_renderers.Length > 0 && m_renderers[0] != null)
            {
                m_originalColor = m_renderers[0].material.color;
            }
        }

        private void Update()
        {
            m_angles.x = AddAngle(m_angles.x, m_anglesSpeed.x * Time.deltaTime);
            m_angles.y = AddAngle(m_angles.y, m_anglesSpeed.y * Time.deltaTime);
            m_angles.z = AddAngle(m_angles.z, m_anglesSpeed.z * Time.deltaTime);

            m_model.rotation = Quaternion.Euler(m_angles);

            if (!m_camera)
            {
                m_camera = FindAnyObjectByType<OVRCameraRig>();
            }
            else
            {
                m_textEntity.gameObject.transform.LookAt(m_camera.centerEyeAnchor);
            }
        }

        private float AddAngle(float value, float toAdd)
        {
            value += toAdd;
            if (value > 360.0f)
            {
                value -= 360.0f;
            }

            if (value < 0.0f)
            {
                value = 360.0f - value;
            }

            return value;
        }

        public void SetYoloClassName(string name)
        {
            m_textModel.text = name;
        }

        public string GetYoloClassName()
        {
            return m_textModel.text;
        }
        
        public void SetTemporaryAppearance(bool isTemporary, Color? temporaryColor = null)
        {
            m_isTemporary = isTemporary;
            
            if (m_renderers != null)
            {
                foreach (var renderer in m_renderers)
                {
                    if (renderer != null)
                    {
                        if (isTemporary && temporaryColor.HasValue)
                        {
                            renderer.material.color = temporaryColor.Value;
                        }
                        else
                        {
                            renderer.material.color = m_originalColor;
                        }
                    }
                }
            }
            
            if (m_textModel != null)
            {
                if (isTemporary)
                {
                    m_textModel.color = Color.cyan;
                    m_textModel.text = "Tracking: " + m_textModel.text;
                }
                else
                {
                    m_textModel.color = Color.white;
                }
            }
        }
    }
}
