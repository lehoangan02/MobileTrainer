using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(StepCameraAnchor))]
public class StepCameraAnchorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        StepCameraAnchor anchor = (StepCameraAnchor)target;

        EditorGUILayout.Space(4f);
        using (new EditorGUILayout.VerticalScope(GUI.skin.box))
        {
            EditorGUILayout.LabelField("🎥 3D Step Camera Anchor", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Drag & rotate this GameObject directly in the 3D Scene View.\n" +
                "Unity's Camera Preview is active in the bottom-right corner of the Scene View while selected!",
                MessageType.Info
            );

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
                    SceneView sv = SceneView.lastActiveSceneView;
                    if (sv != null)
                    {
                        sv.Frame(new Bounds(anchor.transform.position, Vector3.one * 1.5f), false);
                    }
                }
            }
        }

        serializedObject.ApplyModifiedProperties();
    }
}
