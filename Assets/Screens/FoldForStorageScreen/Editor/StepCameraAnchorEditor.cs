using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(StepCameraAnchor))]
public class StepCameraAnchorEditor : Editor
{
    private void OnEnable()
    {
        StepCameraAnchor anchor = (StepCameraAnchor)target;
        if (anchor != null)
        {
            anchor.EnsurePreviewCamera();
        }
        HideCameraGizmoIcon();
    }

    private void OnDisable()
    {
        StepCameraAnchor anchor = (StepCameraAnchor)target;
        if (anchor != null)
        {
            anchor.RemovePreviewCamera();
        }
    }

    [InitializeOnLoadMethod]
    private static void HideCameraGizmoIcon()
    {
        try
        {
            var type = typeof(Editor).Assembly.GetType("UnityEditor.AnnotationUtility");
            var setIcon = type?.GetMethod("SetIconEnabled", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            setIcon?.Invoke(null, new object[] { 20, "", 0 }); // classID 20 = Camera
            var setDirty = type?.GetMethod("SetGizmosDirty", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            setDirty?.Invoke(null, null);
        }
        catch { }
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        StepCameraAnchor anchor = (StepCameraAnchor)target;

        var sv = SceneView.lastActiveSceneView;
        bool gizmosOff = sv != null && !sv.drawGizmos;

        EditorGUILayout.Space(4f);
        using (new EditorGUILayout.VerticalScope(GUI.skin.box))
        {
            EditorGUILayout.LabelField("🎥 3D Step Camera Anchor", EditorStyles.boldLabel);

            if (gizmosOff)
            {
                EditorGUILayout.HelpBox(
                    "⚠️ Scene View Gizmos are currently turned OFF!\n" +
                    "Click 'Turn ON Gizmos' below or click the 'Gizmos' button at the top-right of your Scene View window to see 3D camera wireframes.",
                    MessageType.Warning
                );
                if (GUILayout.Button("🔔 Turn ON Scene View Gizmos", GUILayout.Height(28)))
                {
                    if (sv != null)
                    {
                        sv.drawGizmos = true;
                        sv.Repaint();
                    }
                }
                EditorGUILayout.Space(4f);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "Drag & rotate this GameObject in the 3D Scene View.\n" +
                    "Unity's Camera Preview is active in the bottom-right corner of the Scene View while selected!",
                    MessageType.Info
                );
            }

            EditorGUILayout.Space(2f);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("stepIndex"), new GUIContent("Step Index"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("stepTitle"), new GUIContent("Step Title"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("lookAtTarget"), new GUIContent("Target Drone / Part"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("autoLookAtTarget"), new GUIContent("Auto-Aim at Target"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("fieldOfView"), new GUIContent("Field of View"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("gizmoColor"), new GUIContent("Gizmo Color"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("frustumLength"), new GUIContent("Frustum Visual Length"));

            EditorGUILayout.Space(6f);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("📸 Align to SceneView", "Snap this 3D anchor to current SceneView camera"), GUILayout.Height(26)))
                {
                    anchor.AlignToSceneView();
                }

                if (GUILayout.Button(new GUIContent("🎯 Aim at Target", "Rotate this anchor to face the target part directly"), GUILayout.Height(26)))
                {
                    anchor.LookAtTarget();
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("👁️ Preview Pose", "Preview this camera pose in SceneView and Game view"), GUILayout.Height(24)))
                {
                    StepCameraPose pose = anchor.ToPose();
                    pose.ApplyPreviewInEditor(anchor.PreviewCamera);
                }

                if (GUILayout.Button(new GUIContent("🔍 Frame in SceneView", "Focus SceneView onto this anchor"), GUILayout.Height(24)))
                {
                    if (sv != null)
                    {
                        sv.Frame(new Bounds(anchor.transform.position, Vector3.one * 1.5f), false);
                    }
                }
            }
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void OnSceneGUI()
    {
        StepCameraAnchor anchor = (StepCameraAnchor)target;
        if (anchor == null) return;

        var sv = SceneView.currentDrawingSceneView ?? SceneView.lastActiveSceneView;
        bool gizmosOff = sv != null && !sv.drawGizmos;

        // If Scene View Gizmos is toggled off, draw frustum via Handles as a fallback so it's visible
        if (gizmosOff)
        {
            Transform t = anchor.transform;
            DrawAnchorFrustumHandles(anchor, t.position, t.rotation);
        }
    }

    private static void DrawAnchorFrustumHandles(StepCameraAnchor anchor, Vector3 pos, Quaternion rot)
    {
        Handles.color = Color.yellow;

        float fov = anchor.fieldOfView > 10f ? anchor.fieldOfView : 55f;
        float fovRad = fov * 0.5f * Mathf.Deg2Rad;
        float aspect = 16f / 9f;
        float len = anchor.frustumLength > 0.2f ? anchor.frustumLength : 1.2f;
        float halfH = Mathf.Tan(fovRad) * len;
        float halfW = halfH * aspect;

        Vector3 f1 = pos + rot * new Vector3(-halfW,  halfH, len);
        Vector3 f2 = pos + rot * new Vector3( halfW,  halfH, len);
        Vector3 f3 = pos + rot * new Vector3( halfW, -halfH, len);
        Vector3 f4 = pos + rot * new Vector3(-halfW, -halfH, len);

        // Frustum pyramid edges
        Handles.DrawLine(pos, f1);
        Handles.DrawLine(pos, f2);
        Handles.DrawLine(pos, f3);
        Handles.DrawLine(pos, f4);

        // Far rect
        Handles.DrawLine(f1, f2);
        Handles.DrawLine(f2, f3);
        Handles.DrawLine(f3, f4);
        Handles.DrawLine(f4, f1);

        // Top triangle indicator (UP direction)
        Vector3 topCenter = (f1 + f2) * 0.5f;
        Vector3 topArrow = topCenter + (rot * Vector3.up) * 0.08f;
        Handles.DrawLine(f1, topArrow);
        Handles.DrawLine(f2, topArrow);

        // Camera body wire
        Matrix4x4 oldM = Handles.matrix;
        Handles.matrix = Matrix4x4.TRS(pos, rot, Vector3.one);
        Handles.DrawWireCube(Vector3.zero, new Vector3(0.18f, 0.12f, 0.22f));
        Handles.DrawWireDisc(new Vector3(0f, 0f, 0.12f), Vector3.forward, 0.05f);
        Handles.matrix = oldM;
    }
}
