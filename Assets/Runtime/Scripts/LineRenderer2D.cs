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
    public class LineRenderer2D : MonoBehaviour
    {
        [Tooltip("The position of the endpoint A. Its world position depends on whether the Local Space options is enabled.")]
        public Vector2 PointA;

        [Tooltip("The position of the endpoint B. Its world position depends on whether the Local Space options is enabled.")]
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

        [Tooltip("When enabled, the position of the points is sent to the GPU once per frame without having to apply the changes.")]
        [SerializeField]
        protected bool m_AutoApplyPositionChanges;

        [Tooltip("The sprite renderer that defines the area of the screen where the line is to be drawn.")]
        [SerializeField]
        protected SpriteRenderer m_Renderer;

        [Tooltip("The color of the line at the endpoint A. If the color of the point B is different, the line will be filled with a color gradient.")]
        [SerializeField]
        protected Color m_LineColorA = Color.red;

        [Tooltip("The color of the line at the endpoint B. If the color of the point A is different, the line will be filled with a color gradient.")]
        [SerializeField]
        protected Color m_LineColorB = Color.red;

        [Tooltip("The color used in the area of the quad that is not filled with the line.")]
        [SerializeField]
        protected Color m_BackgroundColor = Color.clear;

        [Tooltip("The width of every point in the line, in pixels.")]
        [Min(0.0001f)]
        [SerializeField]
        protected float m_LineThickness = 4.0f;

        [Tooltip("The length of the dots of the line. Use big numbers to draw a continuous line.")]
        [SerializeField]
        protected float m_DottedLineLength = 99999.0f;

        [Tooltip("The offset of the dots of the line.")]
        [SerializeField]
        protected float m_DottedLineOffset = 0.0f;

        [Tooltip("When enabled, the world position of the parent is added to the position of the points which is assumed to be local.")]
        [SerializeField]
        protected bool m_PositionsAreLocalSpace = false;

        [Tooltip("Enable this when the line is drawn using the vectorial technique.")]
        [SerializeField]
        protected bool m_UsesVectorialLineMaterial;

        [Tooltip("A texture to draw on the line.")]
        [SerializeField]
        protected Texture2D m_LineTexture;

        [Tooltip("The tiling settings for the line texture.")]
        [SerializeField]
        protected Vector2 m_LineTextureTiling = Vector2.one;

        [Tooltip("The offset settings for the line texture.")]
        [SerializeField]
        protected Vector2 m_LineTextureOffset = Vector2.zero;

        [Tooltip("The camera used for calculating the actual thickness on screen.")]
        [SerializeField]
        protected Camera m_Camera;

        protected int PixelsPerUnit
        {
            get
            {
#if UNITY_EDITOR

                if(!Application.isPlaying && m_editorCamera != null)
                {
                    return m_editorCamera.pixelHeight / ((int)m_editorCamera.orthographicSize * 2);
                }

#endif
                
                return m_pixelsPerUnit;
            }
        }

        protected int m_pixelsPerUnit;
        protected bool m_isPositionsDirty = false;
        protected MaterialPropertyBlock m_materialPropertyBlock;

#if UNITY_EDITOR

        protected Camera m_editorCamera;

#endif

        protected static class ShaderParams
        {
            public static int LineColorA = Shader.PropertyToID("_LineColorA");
            public static int LineColorB = Shader.PropertyToID("_LineColorB");
            public static int LineColor = Shader.PropertyToID("_LineColor");
            public static int Thickness = Shader.PropertyToID("_Thickness");
            public static int DottedLineLength = Shader.PropertyToID("_DottedLineLength");
            public static int DottedLineOffset = Shader.PropertyToID("_DottedLineOffset");
            public static int BackgroundColor = Shader.PropertyToID("_BackgroundColor");
            public static int LineTexture = Shader.PropertyToID("_LineTexture");
            public static int LineTexture_ST = Shader.PropertyToID("_LineTexture_ST");
            public static int Origin = Shader.PropertyToID("_Origin");
            public static int PointA = Shader.PropertyToID("_PointA");
            public static int PointB = Shader.PropertyToID("_PointB");
        }

        protected virtual void Start()
        {
            if(!Application.isPlaying)
            {
                return;
            }

            int screenHeight = (int)CurrentCamera.orthographicSize * 2;
            m_pixelsPerUnit = CurrentCamera.pixelHeight / screenHeight;

            ApplyPointPositionChanges();

            if (CurrentCamera != null)
            {
                RefreshMaterial();
            }
        }

        protected virtual void OnEnable()
        {
            if(m_materialPropertyBlock == null)
            {
                m_materialPropertyBlock = new MaterialPropertyBlock();
            }

            if(CurrentCamera != null)
            {
                RefreshMaterial();
            }
        }

        protected virtual void LateUpdate()
        {
            if(CurrentCamera != null && 
               (m_isPositionsDirty || m_AutoApplyPositionChanges))
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

            if(m_UsesVectorialLineMaterial && !Application.isPlaying)
            {
                SendPointPositionsToGPU();
            }
        }

#endif

        protected virtual void RefreshMaterial()
        {
            m_Renderer.GetPropertyBlock(m_materialPropertyBlock);

            m_materialPropertyBlock.SetColor(ShaderParams.LineColorA, m_LineColorA);
            m_materialPropertyBlock.SetColor(ShaderParams.LineColorB, m_LineColorB);
            m_materialPropertyBlock.SetFloat(ShaderParams.Thickness, m_LineThickness);
            m_materialPropertyBlock.SetFloat(ShaderParams.DottedLineLength, m_DottedLineLength);
            m_materialPropertyBlock.SetFloat(ShaderParams.DottedLineOffset, m_DottedLineOffset);
            m_materialPropertyBlock.SetColor(ShaderParams.BackgroundColor, m_BackgroundColor);
            m_materialPropertyBlock.SetColor(ShaderParams.LineColor, m_LineColorA);

            if (m_LineTexture != null)
            {
                m_materialPropertyBlock.SetTexture(ShaderParams.LineTexture, m_LineTexture);
            }
            else
            {
                m_materialPropertyBlock.SetTexture(ShaderParams.LineTexture, Texture2D.whiteTexture);
            }

            m_materialPropertyBlock.SetVector(ShaderParams.LineTexture_ST, new Vector4(m_LineTextureTiling.x, m_LineTextureTiling.y, m_LineTextureOffset.x, m_LineTextureOffset.y));

            if(!Application.isPlaying || m_AutoApplyPositionChanges)
            {
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

            m_materialPropertyBlock.SetColor(ShaderParams.LineColorA, m_LineColorA);
            m_materialPropertyBlock.SetColor(ShaderParams.LineColor, m_LineColorA);
            m_Renderer.SetPropertyBlock(m_materialPropertyBlock);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="newLineColor"></param>
        public void SetColorB(Color newLineColor)
        {
            m_LineColorB = newLineColor;

            m_materialPropertyBlock.SetColor(ShaderParams.LineColorB, m_LineColorB);
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
        /// 
        /// </summary>
        /// <param name="newDottedLineLength"></param>
        public void SetDottedLineLength(float newDottedLineLength)
        {
            m_DottedLineLength = newDottedLineLength;

            m_materialPropertyBlock.SetFloat(ShaderParams.DottedLineLength, m_DottedLineLength);
            m_Renderer.SetPropertyBlock(m_materialPropertyBlock);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="newDottedLineOffset"></param>
        public void SetDottedLineOffset(float newDottedLineOffset)
        {
            m_DottedLineOffset = newDottedLineOffset;

            m_materialPropertyBlock.SetFloat(ShaderParams.DottedLineOffset, m_DottedLineOffset);
            m_Renderer.SetPropertyBlock(m_materialPropertyBlock);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="newTiling"></param>
        /// <param name="newOffset"></param>
        public void SetTextureTilingAndOffset(Vector2 newTiling, Vector2 newOffset)
        {
            m_materialPropertyBlock.SetVector(ShaderParams.LineTexture_ST, new Vector4(newTiling.x, newTiling.y, newOffset.x, newOffset.y));
            m_Renderer.SetPropertyBlock(m_materialPropertyBlock);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="newBackgroundColor"></param>
        public void SetBackgroundColor(Color newBackgroundColor)
        {
            m_BackgroundColor = newBackgroundColor;

            m_materialPropertyBlock.SetColor(ShaderParams.BackgroundColor, m_BackgroundColor);
            m_Renderer.SetPropertyBlock(m_materialPropertyBlock);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="newTexture"></param>
        public void SetLineTexture(Texture2D newTexture)
        {
            m_LineTexture = newTexture;

            if (m_LineTexture != null)
            {
                m_materialPropertyBlock.SetTexture(ShaderParams.LineTexture, m_LineTexture);
            }
            else
            {
                m_materialPropertyBlock.SetTexture(ShaderParams.LineTexture, Texture2D.whiteTexture);
            }

            m_Renderer.SetPropertyBlock(m_materialPropertyBlock);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="newLineTextureTiling"></param>
        public void SetLineTextureTiling(Vector2 newLineTextureTiling)
        {
            m_LineTextureTiling = newLineTextureTiling;

            SetTextureTilingAndOffset(m_LineTextureTiling, m_LineTextureOffset);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="newLineTextureOffset"></param>
        public void SetLineTextureOffset(Vector2 newLineTextureOffset)
        {
            m_LineTextureOffset = newLineTextureOffset;

            SetTextureTilingAndOffset(m_LineTextureTiling, m_LineTextureOffset);
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
                m_materialPropertyBlock.SetVector(ShaderParams.PointA, (Vector2)CurrentCamera.WorldToScreenPoint(PointA + parentPosition));
                m_materialPropertyBlock.SetVector(ShaderParams.PointB, (Vector2)CurrentCamera.WorldToScreenPoint(PointB + parentPosition));

                Vector2 origin = CurrentCamera.WorldToScreenPoint(Vector2.zero);

#if UNITY_EDITOR

                // This allows to draw the line on the scene view at the proper position
                if(!Application.isPlaying && m_editorCamera != null)
                {
                    origin = m_editorCamera.WorldToScreenPoint(Vector2.zero);

                    m_materialPropertyBlock.SetVector(ShaderParams.PointA, (Vector2)m_editorCamera.WorldToScreenPoint(PointA + parentPosition));
                    m_materialPropertyBlock.SetVector(ShaderParams.PointB, (Vector2)m_editorCamera.WorldToScreenPoint(PointB + parentPosition));
                }

#endif

                origin = new Vector2(Mathf.Round(origin.x), Mathf.Round(origin.y));
                m_materialPropertyBlock.SetVector(ShaderParams.Origin, origin);
            }
            else
            {
                m_materialPropertyBlock.SetVector(ShaderParams.PointA, PointA + parentPosition);
                m_materialPropertyBlock.SetVector(ShaderParams.PointB, PointB + parentPosition);
            }

            m_Renderer.SetPropertyBlock(m_materialPropertyBlock);

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
            float MINIMUM_THICKNESS = PixelsPerUnit == 0 ? m_LineThickness 
                                                         : m_LineThickness / PixelsPerUnit;

            Vector4 worldBounds = Calculate2DWorldBoundingBox(PointA, PointB);
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
            Gizmos.DrawRay(PointA + parentPosition, Vector2.up);
            Gizmos.DrawRay(PointA + parentPosition, Vector2.right);
            Gizmos.DrawRay(PointB + parentPosition, Vector2.up);
            Gizmos.DrawRay(PointB + parentPosition, Vector2.right);

            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(PointA + parentPosition, PointB + parentPosition);
        }

        [CustomEditor(typeof(LineRenderer2D))]
        protected class LineRenderer2DEditor : UnityEditor.Editor
        {
            private static class Texts
            {
                public static GUIContent PixelsPerUnit = new GUIContent("Pixels per unit: ", "The detected pixels per Unity spacial unit.");
                public static GUIContent PointA = new GUIContent("A");
                public static GUIContent PointB = new GUIContent("B");
            }
            
            protected SerializedProperty m_pointA;
            protected SerializedProperty m_pointB;
            protected SerializedProperty m_lineColorA;
            protected SerializedProperty m_lineColorB;
            protected SerializedProperty m_lineThickness;
            protected SerializedProperty m_dottedLineLength;
            protected SerializedProperty m_dottedLineOffset;
            protected SerializedProperty m_backgroundColor;
            protected SerializedProperty m_lineTexture;
            protected SerializedProperty m_lineTextureTiling;
            protected SerializedProperty m_lineTextureOffset;
            protected SerializedProperty m_positionsAreLocalSpace;
            protected SerializedProperty m_autoApplyPositionChanges;
            protected SerializedProperty m_usesVectorialLineMaterial;
            protected SerializedProperty m_camera;
            protected SerializedProperty m_renderer;

            protected void OnEnable()
            {
                m_pointA = serializedObject.FindProperty("PointA");
                m_pointB = serializedObject.FindProperty("PointB");
                m_lineColorA = serializedObject.FindProperty("m_LineColorA");
                m_lineColorB = serializedObject.FindProperty("m_LineColorB");
                m_lineThickness = serializedObject.FindProperty("m_LineThickness");
                m_dottedLineLength = serializedObject.FindProperty("m_DottedLineLength");
                m_dottedLineOffset = serializedObject.FindProperty("m_DottedLineOffset");
                m_backgroundColor = serializedObject.FindProperty("m_BackgroundColor");
                m_lineTexture = serializedObject.FindProperty("m_LineTexture");
                m_lineTextureTiling = serializedObject.FindProperty("m_LineTextureTiling");
                m_lineTextureOffset = serializedObject.FindProperty("m_LineTextureOffset");
                m_positionsAreLocalSpace = serializedObject.FindProperty("m_PositionsAreLocalSpace");
                m_autoApplyPositionChanges = serializedObject.FindProperty("m_AutoApplyPositionChanges");
                m_usesVectorialLineMaterial = serializedObject.FindProperty("m_UsesVectorialLineMaterial");
                m_camera = serializedObject.FindProperty("m_Camera");
                m_renderer = serializedObject.FindProperty("m_Renderer");
            }

            public override void OnInspectorGUI()
            {
                EditorGUILayout.BeginVertical();
                {
                    EditorGUI.BeginChangeCheck();
                    {
                        EditorGUILayout.PropertyField(m_pointA);
                        EditorGUILayout.PropertyField(m_pointB);
                        EditorGUILayout.PropertyField(m_lineColorA);
                        EditorGUILayout.PropertyField(m_lineColorB);
                        EditorGUILayout.PropertyField(m_lineThickness);
                        EditorGUILayout.PropertyField(m_dottedLineLength);
                        EditorGUILayout.PropertyField(m_dottedLineOffset);
                        EditorGUILayout.PropertyField(m_backgroundColor);
                        EditorGUILayout.PropertyField(m_lineTexture);
                        EditorGUILayout.PropertyField(m_lineTextureTiling);
                        EditorGUILayout.PropertyField(m_lineTextureOffset);
                        EditorGUILayout.PropertyField(m_positionsAreLocalSpace);
                        EditorGUILayout.PropertyField(m_autoApplyPositionChanges);
                        EditorGUILayout.PropertyField(m_usesVectorialLineMaterial);
                        EditorGUILayout.PropertyField(m_camera);
                        EditorGUILayout.PropertyField(m_renderer);
                        EditorGUILayout.LabelField(new GUIContent(Texts.PixelsPerUnit.text + (target as LineRenderer2D).PixelsPerUnit, Texts.PixelsPerUnit.tooltip));
                    }
                    if(EditorGUI.EndChangeCheck())
                    {
                        serializedObject.ApplyModifiedProperties();

                        (target as LineRenderer2D).RefreshMaterial();
                    }
                }
                EditorGUILayout.EndVertical();
            }

            protected void OnSceneGUI()
            {
                float handleSize = HandleUtility.GetHandleSize(Vector3.zero) * 0.1f;

                Vector2 pointA;
                Vector2 pointB;

                EditorGUI.BeginChangeCheck();
                {
                    pointA = Handles.PositionHandle(m_pointA.vector2Value, Quaternion.identity);
                    pointB = Handles.PositionHandle(m_pointB.vector2Value, Quaternion.identity);

                    Handles.Label(pointA + Vector2.down * handleSize, Texts.PointA);
                    Handles.Label(pointB + Vector2.down * handleSize, Texts.PointB);
                }
                if(EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(target, "Line point moved");

                    LineRenderer2D lineRenderer = target as LineRenderer2D;
                    lineRenderer.PointA = pointA;
                    lineRenderer.PointB = pointB;

                    serializedObject.Update();
                }
            }
        }

#endif

    }
}