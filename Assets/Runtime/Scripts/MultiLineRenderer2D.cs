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

using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Game.Core.Rendering
{
    /// <summary>
    /// 
    /// </summary>
    [ExecuteAlways]
    public class MultiLineRenderer2D : MonoBehaviour
    {
        [Tooltip("The position of every point in the line. Its world position depends on whether the Local Space options is enabled.")]
        public List<Vector2> Points = new List<Vector2>();

        public Camera CurrentCamera
        {
            get
            {
                return m_Camera
#if UNITY_EDITOR
                                == null ? m_editorCamera : m_Camera
#endif
                       ;
            }

            set
            {
                m_Camera = value;
                int screenHeight = (int)m_Camera.orthographicSize * 2;
                m_pixelsPerUnit = m_Camera.pixelHeight / screenHeight;
            }
        }

        [Tooltip("The sprite renderer that defines the area of the screen where the line is to be drawn.")]
        [SerializeField]
        protected SpriteRenderer m_Renderer;

        [Tooltip("The color of the line.")]
        [SerializeField]
        protected Color m_LineColor = Color.red;

        [Tooltip("The color used in the area of the quad that is not filled with the line.")]
        [SerializeField]
        protected Color m_BackgroundColor = Color.clear;

        [Tooltip("The width of every point in the line, in pixels.")]
        [SerializeField]
        protected float m_LineThickness = 4.0f;

        [Tooltip("The maximum points the line can contain.")]
        [SerializeField]
        protected int m_MaxPoints = 80;

        [Tooltip("When enabled, the position of the points is sent to the GPU once per frame without having to apply the changes.")]
        [SerializeField]
        protected bool m_AutoApplyPositionChanges;

        [Tooltip("When enabled, the world position of the parent is added to the position of the points which is assumed to be local.")]
        [SerializeField]
        protected bool m_PositionsAreLocalSpace = false;

        [Tooltip("The camera used for calculating the actual thickness on screen.")]
        [SerializeField]
        protected Camera m_Camera;

        protected int PixelsPerUnit
        {
            get
            {
#if UNITY_EDITOR

                if (!Application.isPlaying && m_editorCamera != null)
                {
                    return Mathf.CeilToInt(m_editorCamera.pixelHeight / (m_editorCamera.orthographicSize * 2));
                }

#endif

                return m_pixelsPerUnit;
            }
        }

        protected int m_pointsCount;
        protected int m_packedPointsCount;
        protected int m_pixelsPerUnit;

        protected Texture2D m_packedPointsTexture;
        protected Color[] m_packedPoints;
        protected bool m_isPositionsDirty = false;
        protected bool m_isLayoutDirty = false;
        protected MaterialPropertyBlock m_materialPropertyBlock;

#if UNITY_EDITOR

        protected Camera m_editorCamera;

#endif

        protected static class ShaderParams
        {
            public static int LineColor = Shader.PropertyToID("_LineColor");
            public static int Thickness = Shader.PropertyToID("_Thickness");
            public static int BackgroundColor = Shader.PropertyToID("_BackgroundColor");
            public static int Origin = Shader.PropertyToID("_Origin");
            public static int PointsCount = Shader.PropertyToID("_PointsCount");
            public static int PackedPointsCount = Shader.PropertyToID("_PackedPointsCount");
            public static int PackedPoints = Shader.PropertyToID("_PackedPoints");
        }

        /// <summary>
        /// Establishes the maximum amount of points the line can render.
        /// </summary>
        /// <param name="maximum">The maximum amount of points that can be rendered by this line.</param>
        public void SetMaxPoints(int maximum)
        {
            m_MaxPoints = maximum;
            ApplyLayoutChanges();
        }

        protected virtual void Start()
        {
            if(!Application.isPlaying)
            {
                return;
            }

            int screenHeight = (int)CurrentCamera.orthographicSize * 2;
            m_pixelsPerUnit = CurrentCamera.pixelHeight / screenHeight;

            ApplyLayoutChanges();
            ApplyPointPositionChanges();

            if (CurrentCamera != null)
            {
                m_Renderer.GetPropertyBlock(m_materialPropertyBlock);
                RefreshMaterial();
            }
        }

        protected virtual void OnEnable()
        {
            if (m_materialPropertyBlock == null)
            {
                m_materialPropertyBlock = new MaterialPropertyBlock();
            }

            ApplyLayoutChanges();
            ApplyPointPositionChanges();

            if(CurrentCamera != null)
            {
                m_Renderer.GetPropertyBlock(m_materialPropertyBlock);
                RefreshMaterial();
            }
        }

        protected virtual void LateUpdate()
        {
            if(CurrentCamera == null)
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

#if UNITY_EDITOR

        protected virtual void OnRenderObject()
        {
            if (m_editorCamera == null && !Application.isPlaying)
            {
                Camera[] sceneCameras = SceneView.GetAllSceneCameras();

                if (sceneCameras.Length > 0)
                {
                    m_editorCamera = sceneCameras[0];
                    OnEnable();
                }
            }
        }

#endif

        /// <summary>
        /// 
        /// </summary>
        /// <param name="newLineColor"></param>
        public void SetColor(Color newLineColor)
        {
            m_LineColor = newLineColor;

            m_materialPropertyBlock.SetColor(ShaderParams.LineColor, m_LineColor);
            m_Renderer.SetPropertyBlock(m_materialPropertyBlock);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="newLineColor"></param>
        public void SetThickness(float newThickness)
        {
            m_LineThickness = newThickness;

            m_materialPropertyBlock.SetFloat(ShaderParams.Thickness, m_LineThickness);
            m_Renderer.SetPropertyBlock(m_materialPropertyBlock);
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

            m_materialPropertyBlock.SetTexture(ShaderParams.PackedPoints, m_packedPointsTexture);
            m_materialPropertyBlock.SetFloat(ShaderParams.PointsCount, m_pointsCount);
            m_materialPropertyBlock.SetFloat(ShaderParams.PackedPointsCount, m_packedPointsCount);
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
            float MINIMUM_THICKNESS = m_pixelsPerUnit == 0 ? m_LineThickness
                                                           : m_LineThickness / m_pixelsPerUnit;

            Vector4 worldBounds = Calculate2DWorldBoundingBox(Points);
            worldBounds.x -= MINIMUM_THICKNESS;
            worldBounds.y += MINIMUM_THICKNESS;
            worldBounds.z += 2 * MINIMUM_THICKNESS;
            worldBounds.w += 2 * MINIMUM_THICKNESS;
            transform.localScale = new Vector3(worldBounds.z, worldBounds.w, 1.0f);
            transform.position = new Vector2(worldBounds.x + parentPosition.x, worldBounds.y + parentPosition.y);
            transform.rotation = Quaternion.identity;

            if (transform.parent != null)
            {
                Vector3 parentScale = transform.parent.localScale;
                transform.localScale = new Vector3(transform.localScale.x / parentScale.x, transform.localScale.y / parentScale.y, 1.0f);
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

        protected virtual void RefreshMaterial()
        {
            m_materialPropertyBlock.SetColor(ShaderParams.LineColor, m_LineColor);
            m_materialPropertyBlock.SetFloat(ShaderParams.Thickness, m_LineThickness);
            m_materialPropertyBlock.SetColor(ShaderParams.BackgroundColor, m_BackgroundColor);

#if UNITY_EDITOR

            if(m_isLayoutDirty)
            {
                m_isLayoutDirty = false;
                SendLayoutToGPU();
            }

            if (m_isPositionsDirty && (!Application.isPlaying || m_AutoApplyPositionChanges))
            {
                m_isPositionsDirty = false;
                SendPointPositionsToGPU();
            }

#endif
            m_Renderer.SetPropertyBlock(m_materialPropertyBlock);

#if UNITY_EDITOR

            SceneView.RepaintAll();

#endif

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
                Gizmos.DrawRay(Points[i] + parentPosition, Vector2.up);
                Gizmos.DrawRay(Points[i] + parentPosition, Vector2.right);

                if (i < Points.Count - 1)
                {
                    Gizmos.color = Color.magenta;
                    Gizmos.DrawLine(Points[i] + parentPosition, Points[i + 1] + parentPosition);
                }
            }
        }

        [CustomEditor(typeof(MultiLineRenderer2D))]
        protected class MultiLineRenderer2DEditor : UnityEditor.Editor
        {
            private static class Texts
            {
                public static GUIContent PixelsPerUnit = new GUIContent("Pixels per unit: ", "The detected pixels per Unity spacial unit.");
                public static GUIContent PointsCount = new GUIContent("Points count: ", "The actual amount of points in the line.");
                public static GUIContent PackedPointsCount = new GUIContent("Packed points count: ", "The amount of packages that contain 2 points each.");
            }

            protected SerializedProperty m_points;
            protected SerializedProperty m_maxPoints;
            protected SerializedProperty m_lineThickness;
            protected SerializedProperty m_lineColor;
            protected SerializedProperty m_backgroundColor;
            protected SerializedProperty m_positionsAreLocalSpace;
            protected SerializedProperty m_autoApplyPositionChanges;
            protected SerializedProperty m_camera;
            protected SerializedProperty m_renderer;

            protected virtual void OnEnable()
            {
                m_points = serializedObject.FindProperty("Points");
                m_maxPoints = serializedObject.FindProperty("m_MaxPoints");
                m_lineThickness = serializedObject.FindProperty("m_LineThickness");
                m_lineColor = serializedObject.FindProperty("m_LineColor");
                m_backgroundColor = serializedObject.FindProperty("m_BackgroundColor");
                m_positionsAreLocalSpace = serializedObject.FindProperty("m_PositionsAreLocalSpace");
                m_autoApplyPositionChanges = serializedObject.FindProperty("m_AutoApplyPositionChanges");
                m_camera = serializedObject.FindProperty("m_Camera");
                m_renderer = serializedObject.FindProperty("m_Renderer");
            }

            public override void OnInspectorGUI()
            {
                EditorGUILayout.BeginVertical();
                {
                    bool hasChanges = false;

                    EditorGUI.BeginChangeCheck();
                    {
                        // Points
                        EditorGUILayout.PropertyField(m_points);
                    }
                    if (EditorGUI.EndChangeCheck())
                    {
                        (target as MultiLineRenderer2D).ApplyLayoutChanges();
                        (target as MultiLineRenderer2D).ApplyPointPositionChanges();
                        hasChanges = true;
                    }

                    EditorGUI.BeginChangeCheck();
                    {
                        // Max points
                        EditorGUILayout.PropertyField(m_maxPoints);
                    }
                    if (EditorGUI.EndChangeCheck())
                    {
                        (target as MultiLineRenderer2D).ApplyLayoutChanges();
                        hasChanges = true;
                    }

                    EditorGUI.BeginChangeCheck();
                    {
                        // Line thickness
                        EditorGUILayout.PropertyField(m_lineThickness);
                    }
                    if (EditorGUI.EndChangeCheck())
                    {
                        (target as MultiLineRenderer2D).ApplyPointPositionChanges();
                        hasChanges = true;
                    }

                    EditorGUI.BeginChangeCheck();
                    {
                        // Line color
                        EditorGUILayout.PropertyField(m_lineColor);
                        // Background color
                        EditorGUILayout.PropertyField(m_backgroundColor);
                    }
                    if (EditorGUI.EndChangeCheck())
                    {
                        hasChanges = true;
                    }

                    EditorGUI.BeginChangeCheck();
                    {
                        // Positions are local space
                        EditorGUILayout.PropertyField(m_positionsAreLocalSpace);
                    }
                    if (EditorGUI.EndChangeCheck())
                    {
                        (target as MultiLineRenderer2D).ApplyPointPositionChanges();
                        hasChanges = true;
                    }

                    EditorGUI.BeginChangeCheck();
                    {
                        DrawProperties();
                    }
                    if (EditorGUI.EndChangeCheck())
                    {
                        hasChanges = true;
                    }

                    if (hasChanges)
                    {
                        serializedObject.ApplyModifiedProperties();

                        (target as MultiLineRenderer2D).RefreshMaterial();
                    }
                }
                EditorGUILayout.EndVertical();
            }

            protected virtual void DrawProperties()
            {
                // Auto apply position changes
                EditorGUILayout.PropertyField(m_autoApplyPositionChanges);
                // Camera
                EditorGUILayout.PropertyField(m_camera);
                // Renderer
                EditorGUILayout.PropertyField(m_renderer);
                // Pixels per unit
                EditorGUILayout.LabelField(new GUIContent(Texts.PixelsPerUnit.text + (target as MultiLineRenderer2D).m_pixelsPerUnit, Texts.PixelsPerUnit.tooltip));
                // Points count
                EditorGUILayout.LabelField(new GUIContent(Texts.PointsCount.text + (target as MultiLineRenderer2D).m_pointsCount, Texts.PointsCount.tooltip));
                // Packed points count
                EditorGUILayout.LabelField(new GUIContent(Texts.PackedPointsCount.text + (target as MultiLineRenderer2D).m_packedPointsCount, Texts.PackedPointsCount.tooltip));
            }

            protected void OnSceneGUI()
            {
                float handleSize = HandleUtility.GetHandleSize(Vector3.zero) * 0.1f;
                Vector2 point;

                for (int i = 0; i < m_points.arraySize; ++i)
                {
                    EditorGUI.BeginChangeCheck();
                    {
                        point = Handles.PositionHandle(m_points.GetArrayElementAtIndex(i).vector2Value, Quaternion.identity);
                        Handles.Label(point + Vector2.down * handleSize, i.ToString());
                    }
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(target, "Line point moved");

                        MultiLineRenderer2D lineRenderer = target as MultiLineRenderer2D;
                        lineRenderer.Points[i] = point;

                        serializedObject.Update();
                    }
                }
            }
        }

#endif

    }
}