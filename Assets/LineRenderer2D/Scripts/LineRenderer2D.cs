// Copyright 2020 Alejandro Villalba Avila
//
// Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), 
// to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, 
// and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, 
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER 
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS 
// IN THE SOFTWARE.

//using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Core.Rendering
{
    /// <summary>
    /// 
    /// </summary>
    public class LineRenderer2D : MonoBehaviour
    {
        //[FoldoutGroup("Settings")]
        public Vector2 PointA;

        //[FoldoutGroup("Settings")]
        public Vector2 PointB;
    
        public Color LineColorA
        {
            get
            {
                return m_LineColorA;
            }
        }

        public Color LineColorB
        {
            get
            {
                return m_LineColorB;
            }
        }

        public Camera CurrentCamera
        {
            get
            {
                return m_camera;
            }

            set
            {
                m_camera = value;
                int screenHeight = (int)m_camera.orthographicSize * 2;
                m_pixelsPerUnit = m_camera.pixelHeight / screenHeight;
            }
        }

        [Tooltip("When enabled, the position of the points is sent to the GPU once per frame without having to apply the changes.")]
        //[FoldoutGroup("Settings")]
        [SerializeField]
        protected bool m_AutoApplyPositionChanges;

        [Tooltip("The sprite renderer that defines the area of the screen where the line is to be drawn.")]
        //[FoldoutGroup("Settings")]
        [SerializeField]
        protected SpriteRenderer m_Renderer;

        [Tooltip("The color of the line at the endpoint A. If the color of the point B is different, the line will be filled with a color gradient.")]
        //[OnValueChanged("SetColorA")]
        //[FoldoutGroup("Settings")]
        [SerializeField]
        protected Color m_LineColorA = Color.red;

        [Tooltip("The color of the line at the endpoint B. If the color of the point A is different, the line will be filled with a color gradient.")]
        //[OnValueChanged("SetColorB")]
        //[FoldoutGroup("Settings")]
        [SerializeField]
        protected Color m_LineColorB = Color.red;

        [Tooltip("The width of every point in the line, in pixels.")]
        //[OnValueChanged("SetThickness")]
        //[FoldoutGroup("Settings")]
        [SerializeField]
        protected float m_LineThickness = 4.0f;

        [Tooltip("The length of the dots of the line. Use big numbers to draw a continuous line.")]
        //[OnValueChanged("SetDottedLineLength")]
        //[FoldoutGroup("Settings")]
        [SerializeField]
        protected float m_DottedLineLength = 99999.0f;

        [Tooltip("The offset of the dots of the line.")]
        //[OnValueChanged("SetDottedLineOffset")]
        //[FoldoutGroup("Settings")]
        [SerializeField]
        protected float m_DottedLineOffset = 0.0f;

        [Tooltip("Whan enabled, the world position of the parent is added to the position of the points which is assumed to be local.")]
        //[FoldoutGroup("Settings")]
        [SerializeField]
        protected bool m_PositionsAreLocalSpace = false;

        [Tooltip("Enable this when the line is drawn using the vectorial technique.")]
        //[FoldoutGroup("Settings")]
        [SerializeField]
        protected bool m_UsesVectorialLineMaterial;

        [Tooltip("The detected pixels per Unity spacial unit.")]
        //[FoldoutGroup("Status")]
        //[ShowInInspector]
        //[ReadOnly]
        protected int m_pixelsPerUnit;

        [SerializeField]
        protected Camera m_camera;

        protected bool m_isPositionsDirty = false;

        protected virtual void Start()
        {
            CurrentCamera = m_camera;

            ApplyPointPositionChanges();
            SetColorA(m_LineColorA);
            SetColorB(m_LineColorB);
            SetThickness(m_LineThickness);
        }

        protected virtual void LateUpdate()
        {
            if (m_camera != null &&
               (m_isPositionsDirty || m_AutoApplyPositionChanges))
            {
                m_isPositionsDirty = false;
                SendPointPositionsToGPU();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="newLineColor"></param>
        public void SetColorA(Color newLineColor)
        {
            m_LineColorA = newLineColor;

            if (Application.isPlaying)
            {
                m_Renderer.material.SetColor("_LineColorA", m_LineColorA);
            }
            else
            {
                m_Renderer.sharedMaterial.SetColor("_LineColorA", m_LineColorA);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="newLineColor"></param>
        public void SetColorB(Color newLineColor)
        {
            m_LineColorB = newLineColor;

            if(Application.isPlaying)
            {
                m_Renderer.material.SetColor("_LineColorB", m_LineColorB);
            }
            else
            {
                m_Renderer.sharedMaterial.SetColor("_LineColorB", m_LineColorB);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="newLineColor"></param>
        public void SetThickness(float newThickness)
        {
            m_LineThickness = newThickness;

            if (Application.isPlaying)
            {
                m_Renderer.material.SetFloat("_Thickness", m_LineThickness);
            }
            else
            {
                m_Renderer.sharedMaterial.SetFloat("_Thickness", m_LineThickness);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="newDottedLineLength"></param>
        public void SetDottedLineLength(float newDottedLineLength)
        {
            m_DottedLineLength = newDottedLineLength;

            if (Application.isPlaying)
            {
                m_Renderer.material.SetFloat("_DottedLineLength", m_DottedLineLength);
            }
            else
            {
                m_Renderer.sharedMaterial.SetFloat("_DottedLineLength", m_DottedLineLength);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="newDottedLineOffset"></param>
        public void SetDottedLineOffset(float newDottedLineOffset)
        {
            m_DottedLineOffset = newDottedLineOffset;

            if (Application.isPlaying)
            {
                m_Renderer.material.SetFloat("_DottedLineOffset", m_DottedLineOffset);
            }
            else
            {
                m_Renderer.sharedMaterial.SetFloat("_DottedLineOffset", m_DottedLineOffset);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="newTiling"></param>
        /// <param name="newOffset"></param>
        public void SetTextureTilingAndOffset(Vector2 newTiling, Vector2 newOffset)
        {
            m_Renderer.material.SetVector("_MainTex_ST", new Vector4(newTiling.x, newTiling.y, newOffset.x, newOffset.y));
        }

        /// <summary>
        /// 
        /// </summary>
        public void ApplyPointPositionChanges()
        {
            m_isPositionsDirty = true;
        }

        protected virtual void SendPointPositionsToGPU()
        {
            Vector2 parentPosition = Vector2.zero;

            if(m_PositionsAreLocalSpace && transform.parent != null)
            {
                parentPosition = transform.parent.position;
            }

            if(m_UsesVectorialLineMaterial)
            {
                m_Renderer.material.SetVector("_PointA", (Vector2)m_camera.WorldToScreenPoint(PointA + parentPosition));
                m_Renderer.material.SetVector("_PointB", (Vector2)m_camera.WorldToScreenPoint(PointB + parentPosition));

                Vector2 origin = m_camera.WorldToScreenPoint(Vector2.zero);
                origin = new Vector2(Mathf.Round(origin.x), Mathf.Round(origin.y));
                m_Renderer.material.SetVector("_Origin", origin);
            }
            else
            {
                m_Renderer.material.SetVector("_PointA", PointA + parentPosition);
                m_Renderer.material.SetVector("_PointB", PointB + parentPosition);
            }

            RefreshSpriteTransform();
        }

        protected virtual void RefreshSpriteTransform()
        {
            Vector2 parentPosition = Vector2.zero;

            if (m_PositionsAreLocalSpace && transform.parent != null)
            {
                parentPosition = transform.parent.position;
            }

            // This avoids the pixel blocks of the line to be cut-off
            float MINIMUM_THICKNESS = m_LineThickness / m_pixelsPerUnit;

            Vector4 worldBounds = Calculate2DWorldBoundingBox(PointA, PointB);
            worldBounds.x -= MINIMUM_THICKNESS;
            worldBounds.y += MINIMUM_THICKNESS;
            worldBounds.z += 2 * MINIMUM_THICKNESS;
            worldBounds.w += 2 * MINIMUM_THICKNESS;
            transform.localScale = new Vector2(worldBounds.z, worldBounds.w);
            transform.position = new Vector2(worldBounds.x + parentPosition.x, worldBounds.y + parentPosition.y);
            transform.rotation = Quaternion.identity;

            if (transform.parent != null)
            {
                Vector3 parentScale = transform.parent.localScale;
                transform.localScale = new Vector3(transform.lossyScale.x / parentScale.x, transform.lossyScale.y / parentScale.y, 1.0f);
            }
        }

        private static Vector4 Calculate2DWorldBoundingBox(Vector2 pointA, Vector2 pointB)
        {
            Vector4 bounds = new Vector4(float.MaxValue, -float.MaxValue, -float.MaxValue, float.MaxValue);

            bounds.x = bounds.x > pointA.x ? pointA.x
                                           : bounds.x;
            bounds.y = bounds.y < pointA.y ? pointA.y
                                           : bounds.y;
            bounds.z = bounds.z < pointA.x ? pointA.x
                                           : bounds.z;
            bounds.w = bounds.w > pointA.y ? pointA.y
                                           : bounds.w;
            bounds.x = bounds.x > pointB.x ? pointB.x
                                           : bounds.x;
            bounds.y = bounds.y < pointB.y ? pointB.y
                                           : bounds.y;
            bounds.z = bounds.z < pointB.x ? pointB.x
                                           : bounds.z;
            bounds.w = bounds.w > pointB.y ? pointB.y
                                           : bounds.w;

            bounds.z -= bounds.x;
            bounds.w = bounds.y - bounds.w;

            return bounds;
        }

#if UNITY_EDITOR

        private void OnDrawGizmos()
        {
            Vector2 parentPosition = Vector2.zero;

            if (m_PositionsAreLocalSpace && transform.parent != null)
            {
                parentPosition = transform.parent.position;
            }

            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(PointA + parentPosition, Vector2.up * 5.0f);
            Gizmos.DrawRay(PointA + parentPosition, Vector2.right * 5.0f);
            Gizmos.DrawRay(PointB + parentPosition, Vector2.up * 5.0f);
            Gizmos.DrawRay(PointB + parentPosition, Vector2.right * 5.0f);

            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(PointA + parentPosition, PointB + parentPosition);
        }

#endif

    }
}