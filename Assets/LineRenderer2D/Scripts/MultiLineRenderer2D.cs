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
using System.Collections.Generic;
using UnityEngine;

namespace Game.Core.Rendering
{
    /// <summary>
    /// 
    /// </summary>
    public class MultiLineRenderer2D : MonoBehaviour
    {
        [Tooltip("The position of every point in the line.")]
        //[FoldoutGroup("Settings")]
        public List<Vector2> Points = new List<Vector2>();

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

        [Tooltip("The sprite renderer that defines the area of the screen where the line is to be drawn.")]
        //[FoldoutGroup("Settings")]
        [SerializeField]
        protected SpriteRenderer m_Renderer;

        [Tooltip("The color of the line.")]
        //[OnValueChanged("SetColor")]
        //[FoldoutGroup("Settings")]
        [SerializeField]
        protected Color m_LineColor = Color.red;

        [Tooltip("The width of every point in the line, in pixels.")]
        //[OnValueChanged("SetThickness")]
        //[FoldoutGroup("Settings")]
        [SerializeField]
        protected float m_LineThickness = 4.0f;

        [Tooltip("The maximum points the line can contain. I cannot be changed afterwards.")]
        //[FoldoutGroup("Settings")]
        [SerializeField]
        protected int m_MaxPoints = 80;

        [Tooltip("When enabled, the position of the points is sent to the GPU once per frame without having to apply the changes.")]
        //[FoldoutGroup("Settings")]
        [SerializeField]
        protected bool m_AutoApplyPositionChanges;

        [Tooltip("Whan enabled, the world position of the parent is added to the position of the points which is assumed to be local.")]
        //[FoldoutGroup("Settings")]
        [SerializeField]
        protected bool m_PositionsAreLocalSpace = false;

        [Tooltip("The actual amount of points in the line.")]
        //[FoldoutGroup("Status")]
        //[ShowInInspector]
        //[ReadOnly]
        protected int m_pointsCount;

        [Tooltip("The amount of packages that contain 2 points each.")]
        //[FoldoutGroup("Status")]
        //[ShowInInspector]
        //[ReadOnly]
        protected int m_packedPointsCount;

        [Tooltip("The detected pixels per Unity spacial unit.")]
        //[FoldoutGroup("Status")]
        //[ShowInInspector]
        //[ReadOnly]
        protected int m_pixelsPerUnit;

        [SerializeField]
        protected Camera m_camera;

        protected Texture2D m_packedPointsTexture;
        protected Color[] m_packedPoints;
        protected bool m_isPositionsDirty = false;
        protected bool m_isLayoutDirty = false;

        protected virtual void Start()
        {
            CurrentCamera = m_camera;

            ApplyLayoutChanges();
            ApplyPointPositionChanges();
            SetColor(m_LineColor);
            SetThickness(m_LineThickness);
        }

        protected virtual void LateUpdate()
        {
            if (m_camera == null)
            {
                return;
            }

            if (m_isLayoutDirty)
            {
                m_isLayoutDirty = false;
                SendLayoutToGPU();
            }

            if(m_isPositionsDirty || m_AutoApplyPositionChanges)
            {
                m_isPositionsDirty = false;
                SendPointPositionsToGPU();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="newLineColor"></param>
        public void SetColor(Color newLineColor)
        {
            m_LineColor = newLineColor;

            if (Application.isPlaying)
            {
                m_Renderer.material.SetColor("_LineColor", m_LineColor);
            }
            else
            {
                m_Renderer.sharedMaterial.SetColor("_LineColor", m_LineColor);
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
        /// Marks the layout of the line (the number of points) as dirty, so the new values will be sent to the GPU right before the line is rendered again.
        /// </summary>
        public void ApplyLayoutChanges()
        {
            m_isLayoutDirty = true;
        }

        /// <summary>
        /// Marks the positions of the points as dirty, so the new values will be sent to the GPU right before the line is rendered again.
        /// </summary>
        public void ApplyPointPositionChanges()
        {
            m_isPositionsDirty = true;
        }

        protected void SendLayoutToGPU()
        {
            m_pointsCount = Mathf.Min(Points.Count, m_MaxPoints);
            m_packedPointsCount = Mathf.CeilToInt(m_pointsCount * 0.5f);

            if (m_packedPoints == null || m_packedPoints.Length < m_packedPointsCount)
            {
                m_packedPoints = new Color[m_packedPointsCount];
            }

            if(m_packedPointsTexture == null || m_packedPointsTexture.width < m_packedPointsCount)
            {
                m_packedPointsTexture = new Texture2D(m_packedPointsCount, 1, TextureFormat.RGBAFloat, false, true);
            }

            m_Renderer.material.SetTexture("_PackedPoints", m_packedPointsTexture);
            m_Renderer.material.SetFloat("_PointsCount", m_pointsCount);
            m_Renderer.material.SetFloat("_PackedPointsCount", m_packedPointsCount);
        }

        protected virtual void SendPointPositionsToGPU()
        {
            if (Points.Count == 0)
            {
                return;
            }

            RefreshSpriteTransform();

            Vector2 parentPosition = Vector2.zero;

            if (m_PositionsAreLocalSpace && transform.parent != null)
            {
                parentPosition = transform.parent.position;
            }

            // Packs the points in colors that contain 2 points each
            for (int j = 0; j < m_packedPointsCount; ++j)
            {
                m_packedPoints[j].r = Points[j * 2].x + parentPosition.x;
                m_packedPoints[j].g = Points[j * 2].y + parentPosition.y;

                if (j * 2 + 1 >= m_pointsCount)
                {
                    break;
                }

                m_packedPoints[j].b = Points[j * 2 + 1].x + parentPosition.x;
                m_packedPoints[j].a = Points[j * 2 + 1].y + parentPosition.y;
            }

            m_packedPointsTexture.SetPixels(0, 0, m_packedPointsCount, 1, m_packedPoints);
            m_packedPointsTexture.Apply();
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

            Vector4 worldBounds = Calculate2DWorldBoundingBox(Points);
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

        private static Vector4 Calculate2DWorldBoundingBox(List<Vector2> points)
        {
            Vector4 bounds = new Vector4(float.MaxValue, -float.MaxValue, -float.MaxValue, float.MaxValue);

            for (int i = 0; i < points.Count; ++i)
            {
                bounds.x = bounds.x > points[i].x ? points[i].x 
                                                  : bounds.x;
                bounds.y = bounds.y < points[i].y ? points[i].y
                                                  : bounds.y;
                bounds.z = bounds.z < points[i].x ? points[i].x
                                                  : bounds.z;
                bounds.w = bounds.w > points[i].y ? points[i].y
                                                  : bounds.w;
            }

            bounds.z -= bounds.x;
            bounds.w = bounds.y - bounds.w;

            return bounds;
        }

#if UNITY_EDITOR

        protected virtual void OnDrawGizmos()
        {
            Vector2 parentPosition = Vector2.zero;

            if (m_PositionsAreLocalSpace && transform.parent != null)
            {
                parentPosition = transform.parent.position;
            }

            for (int i = 0; i < Points.Count; ++i)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawRay(Points[i] + parentPosition, Vector2.up * 5.0f);
                Gizmos.DrawRay(Points[i] + parentPosition, Vector2.right * 5.0f);

                if (i < Points.Count - 1)
                {
                    Gizmos.color = Color.magenta;
                    Gizmos.DrawLine(Points[i] + parentPosition, Points[i + 1] + parentPosition);
                }
            }
        }

#endif

    }
}