using UnityEngine;
using UnityEditor;

/// <summary>
/// Custom property drawer for StepCameraPose.
/// Provides one-click SceneView capture, Main Camera capture, and interactive previewing right in the Inspector.
/// </summary>
[CustomPropertyDrawer(typeof(StepCameraPose))]
public class StepCameraPoseDrawer : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        SerializedProperty enabledProp = property.FindPropertyRelative("enabled");
        if (!enabledProp.boolValue)
        {
            return EditorGUIUtility.singleLineHeight + 6f;
        }

        float height = EditorGUIUtility.singleLineHeight * 4.4f + 16f;
        if (property.isExpanded)
        {
            height += EditorGUIUtility.singleLineHeight * 6f + 12f;
        }
        return height;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        SerializedProperty enabledProp = property.FindPropertyRelative("enabled");
        SerializedProperty posProp = property.FindPropertyRelative("position");
        SerializedProperty rotProp = property.FindPropertyRelative("rotationEuler");
        SerializedProperty pivotProp = property.FindPropertyRelative("pivot");
        SerializedProperty distProp = property.FindPropertyRelative("distance");
        SerializedProperty pitchProp = property.FindPropertyRelative("pitch");
        SerializedProperty yawProp = property.FindPropertyRelative("yaw");
        SerializedProperty fovProp = property.FindPropertyRelative("fieldOfView");

        Rect lineRect = new Rect(position.x, position.y + 2f, position.width, EditorGUIUtility.singleLineHeight);

        // Header with toggle
        EditorGUI.BeginChangeCheck();
        bool isEnabled = EditorGUI.ToggleLeft(
            new Rect(lineRect.x, lineRect.y, 220f, lineRect.height),
            new GUIContent(" Use Custom Camera Position", "When enabled, this step uses its own custom camera starting pose."),
            enabledProp.boolValue,
            EditorStyles.boldLabel
        );
        if (EditorGUI.EndChangeCheck())
        {
            enabledProp.boolValue = isEnabled;
            if (isEnabled && distProp.floatValue <= 0.01f)
            {
                CapturePoseFromScene(property);
            }
        }

        if (!enabledProp.boolValue)
        {
            Rect hintRect = new Rect(lineRect.x + 230f, lineRect.y, lineRect.width - 230f, lineRect.height);
            GUIStyle dimStyle = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(0.6f, 0.6f, 0.6f) } };
            EditorGUI.LabelField(hintRect, "(Using Scene Default Camera)", dimStyle);
            EditorGUI.EndProperty();
            return;
        }

        lineRect.y += EditorGUIUtility.singleLineHeight + 3f;

        // Status / Summary line
        string summary = $"Pos: ({posProp.vector3Value.x:F1}, {posProp.vector3Value.y:F1}, {posProp.vector3Value.z:F1}) | Rot: ({pitchProp.floatValue:F0}°, {yawProp.floatValue:F0}°) | Dist: {distProp.floatValue:F1}m";
        Rect boxRect = new Rect(position.x + 4f, lineRect.y, position.width - 8f, EditorGUIUtility.singleLineHeight * 1.3f);
        GUI.Box(boxRect, GUIContent.none, EditorStyles.helpBox);
        Rect labelRect = new Rect(boxRect.x + 6f, boxRect.y + 2f, boxRect.width - 12f, EditorGUIUtility.singleLineHeight);
        GUIStyle greenStyle = new GUIStyle(EditorStyles.miniBoldLabel) { normal = { textColor = new Color(0.2f, 0.85f, 0.4f) } };
        EditorGUI.LabelField(labelRect, summary, greenStyle);

        lineRect.y += EditorGUIUtility.singleLineHeight * 1.4f + 3f;

        // Action buttons: [Capture View] [Capture Cam] [Preview] [Clear]
        float btnWidth = (position.width - 20f) / 4f;
        Rect b1 = new Rect(position.x + 4f, lineRect.y, btnWidth - 2f, EditorGUIUtility.singleLineHeight + 2f);
        Rect b2 = new Rect(b1.xMax + 4f, lineRect.y, btnWidth - 2f, EditorGUIUtility.singleLineHeight + 2f);
        Rect b3 = new Rect(b2.xMax + 4f, lineRect.y, btnWidth - 2f, EditorGUIUtility.singleLineHeight + 2f);
        Rect b4 = new Rect(b3.xMax + 4f, lineRect.y, btnWidth - 2f, EditorGUIUtility.singleLineHeight + 2f);

        if (GUI.Button(b1, new GUIContent("Capture View", "Capture current camera view from SceneView into this step")))
        {
            CapturePoseFromScene(property);
        }
        if (GUI.Button(b2, new GUIContent("Capture Cam", "Capture camera view from Main Camera into this step")))
        {
            CapturePoseFromMainCamera(property);
        }
        if (GUI.Button(b3, new GUIContent("Preview", "Preview this camera pose in SceneView and Game view")))
        {
            PreviewPose(property);
        }
        if (GUI.Button(b4, new GUIContent("Clear", "Disable custom camera pose and revert to scene default")))
        {
            enabledProp.boolValue = false;
            property.serializedObject.ApplyModifiedProperties();
        }

        lineRect.y += EditorGUIUtility.singleLineHeight + 6f;

        // Foldout for manual coordinates
        property.isExpanded = EditorGUI.Foldout(
            new Rect(position.x + 4f, lineRect.y, position.width - 8f, EditorGUIUtility.singleLineHeight),
            property.isExpanded,
            "Manual Coordinates / Fine Tuning",
            true
        );

        if (property.isExpanded)
        {
            EditorGUI.indentLevel++;
            lineRect.y += EditorGUIUtility.singleLineHeight + 2f;
            EditorGUI.PropertyField(new Rect(position.x, lineRect.y, position.width, EditorGUIUtility.singleLineHeight), posProp, new GUIContent("Position"));
            lineRect.y += EditorGUIUtility.singleLineHeight + 2f;
            EditorGUI.PropertyField(new Rect(position.x, lineRect.y, position.width, EditorGUIUtility.singleLineHeight), rotProp, new GUIContent("Rotation Euler"));
            lineRect.y += EditorGUIUtility.singleLineHeight + 2f;
            EditorGUI.PropertyField(new Rect(position.x, lineRect.y, position.width, EditorGUIUtility.singleLineHeight), pivotProp, new GUIContent("Pivot"));
            lineRect.y += EditorGUIUtility.singleLineHeight + 2f;
            EditorGUI.PropertyField(new Rect(position.x, lineRect.y, position.width, EditorGUIUtility.singleLineHeight), distProp, new GUIContent("Distance"));
            lineRect.y += EditorGUIUtility.singleLineHeight + 2f;
            EditorGUI.PropertyField(new Rect(position.x, lineRect.y, position.width, EditorGUIUtility.singleLineHeight), fovProp, new GUIContent("Field of View"));

            // Keep pitch & yaw in sync with rotationEuler
            pitchProp.floatValue = StepCameraPose.NormalizeAngle(rotProp.vector3Value.x);
            yawProp.floatValue = StepCameraPose.NormalizeAngle(rotProp.vector3Value.y);

            EditorGUI.indentLevel--;
        }

        EditorGUI.EndProperty();
    }

    private static void CapturePoseFromScene(SerializedProperty property)
    {
        var camCtrl = UnityEngine.Object.FindFirstObjectByType<ModelCameraController>();
        Transform targetPivot = camCtrl != null ? camCtrl.TargetPivotTransform : null;
        StepCameraPose pose = StepCameraPose.CreateFromSceneView(targetPivot);
        ApplyPoseToProperty(property, pose);
    }

    private static void CapturePoseFromMainCamera(SerializedProperty property)
    {
        var camCtrl = UnityEngine.Object.FindFirstObjectByType<ModelCameraController>();
        Camera cam = camCtrl != null ? camCtrl.GetComponent<Camera>() : Camera.main;
        Transform targetPivot = camCtrl != null ? camCtrl.TargetPivotTransform : null;
        StepCameraPose pose = StepCameraPose.CreateFromCamera(cam, targetPivot);
        ApplyPoseToProperty(property, pose);
    }

    private static void ApplyPoseToProperty(SerializedProperty property, StepCameraPose pose)
    {
        property.FindPropertyRelative("enabled").boolValue = true;
        property.FindPropertyRelative("position").vector3Value = pose.position;
        property.FindPropertyRelative("rotationEuler").vector3Value = pose.rotationEuler;
        property.FindPropertyRelative("pivot").vector3Value = pose.pivot;
        property.FindPropertyRelative("distance").floatValue = pose.distance;
        property.FindPropertyRelative("pitch").floatValue = pose.pitch;
        property.FindPropertyRelative("yaw").floatValue = pose.yaw;
        property.FindPropertyRelative("fieldOfView").floatValue = pose.fieldOfView;
        property.serializedObject.ApplyModifiedProperties();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
    }

    private static void PreviewPose(SerializedProperty property)
    {
        StepCameraPose pose = new StepCameraPose
        {
            enabled = true,
            position = property.FindPropertyRelative("position").vector3Value,
            rotationEuler = property.FindPropertyRelative("rotationEuler").vector3Value,
            pivot = property.FindPropertyRelative("pivot").vector3Value,
            distance = property.FindPropertyRelative("distance").floatValue,
            pitch = property.FindPropertyRelative("pitch").floatValue,
            yaw = property.FindPropertyRelative("yaw").floatValue,
            fieldOfView = property.FindPropertyRelative("fieldOfView").floatValue
        };
        pose.ApplyPreviewInEditor();
    }
}
