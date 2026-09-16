using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

/// <summary>
/// Helper and custom inspector editors for configuring custom camera starting poses
/// for tutorial steps and scenes in Mobile Trainer.
/// </summary>
public static class TutorialStepCameraEditorHelper
{
    private static int selectedStepIndex = 0;
    private static bool showBatchTools = false;

    public static void DrawCameraSetupToolbar(SerializedObject so, SerializedProperty stepsProp, SerializedProperty defaultPoseProp, Transform rigRoot, ModelCameraController camCtrl)
    {
        if (so == null) return;

        EditorGUILayout.Space(6f);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 13,
                normal = { textColor = new Color(0.35f, 0.75f, 1f) }
            };
            EditorGUILayout.LabelField("🎥 Step Camera Setup Assistant", headerStyle);
            EditorGUILayout.LabelField("Easily configure custom camera starting poses for each step or the entire scene.", EditorStyles.miniLabel);
            EditorGUILayout.Space(4f);

            // 3D DRAG & DROP POSITIONING PROMINENT SECTION
            using (new EditorGUILayout.VerticalScope(GUI.skin.box))
            {
                EditorGUILayout.LabelField("🎯 3D Space Drag & Drop Positioning", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Create interactive 3D camera GameObjects in the scene hierarchy. You can drag and drop them in 3D space with live Camera Preview!", EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.Space(2f);
                Color oldBg = GUI.backgroundColor;
                GUI.backgroundColor = new Color(0.2f, 0.75f, 1f);
                if (GUILayout.Button(new GUIContent("🎥 Create / Sync 3D Camera Anchors in Hierarchy", "Creates 3D Camera Anchor GameObjects for each step under TutorialController/StepCameraAnchors so you can move them directly in 3D space with live preview"), GUILayout.Height(28)))
                {
                    Generate3DCameraAnchors(so, stepsProp, rigRoot, camCtrl);
                }
                GUI.backgroundColor = oldBg;
            }

            EditorGUILayout.Space(6f);

            // 1. SCENE DEFAULT CAMERA SECTION
            DrawSceneDefaultSection(so, defaultPoseProp, camCtrl);

            EditorGUILayout.Space(6f);

            // 2. STEP CAMERA NAVIGATOR SECTION
            DrawStepNavigatorSection(so, stepsProp, defaultPoseProp, rigRoot, camCtrl);

            EditorGUILayout.Space(4f);

            // 3. BATCH UTILITIES
            DrawBatchUtilities(so, stepsProp, camCtrl);
        }
        EditorGUILayout.Space(6f);
    }

    private static void DrawSceneDefaultSection(SerializedObject so, SerializedProperty defaultPoseProp, ModelCameraController camCtrl)
    {
        using (new EditorGUILayout.VerticalScope(GUI.skin.box))
        {
            EditorGUILayout.LabelField("Scene Default Camera Starting Pose", EditorStyles.boldLabel);

            SerializedProperty enabledProp = defaultPoseProp?.FindPropertyRelative("enabled");
            bool isSet = enabledProp != null && enabledProp.boolValue;

            if (isSet)
            {
                SerializedProperty posProp = defaultPoseProp.FindPropertyRelative("position");
                SerializedProperty distProp = defaultPoseProp.FindPropertyRelative("distance");
                SerializedProperty pitchProp = defaultPoseProp.FindPropertyRelative("pitch");
                SerializedProperty yawProp = defaultPoseProp.FindPropertyRelative("yaw");

                string sum = $"Pos: ({posProp.vector3Value.x:F1}, {posProp.vector3Value.y:F1}, {posProp.vector3Value.z:F1}) | Rot: ({pitchProp.floatValue:F0}°, {yawProp.floatValue:F0}°) | Dist: {distProp.floatValue:F1}m";
                GUIStyle greenLabel = new GUIStyle(EditorStyles.miniBoldLabel) { normal = { textColor = new Color(0.2f, 0.85f, 0.4f) } };
                EditorGUILayout.LabelField($"Active: {sum}", greenLabel);
            }
            else
            {
                GUIStyle greyLabel = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = Color.gray } };
                EditorGUILayout.LabelField("Not set explicitly (automatically uses initial camera transform on Awake)", greyLabel);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("Capture SceneView as Default", "Capture current SceneView orientation as scene default"), GUILayout.Height(24)))
                {
                    Undo.RecordObject(so.targetObject, "Capture Scene Default from SceneView");
                    Transform pTarget = camCtrl != null ? camCtrl.TargetPivotTransform : null;
                    StepCameraPose pose = StepCameraPose.CreateFromSceneView(pTarget);
                    ApplyPoseToSerializedProperty(defaultPoseProp, pose);
                    so.ApplyModifiedProperties();
                    if (camCtrl != null)
                    {
                        Undo.RecordObject(camCtrl, "Update CamCtrl Default Pose");
                        camCtrl.SceneDefaultPose = pose;
                        EditorUtility.SetDirty(camCtrl);
                    }
                    EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                    Debug.Log($"[TutorialStepCamera] Captured Scene Default Camera from SceneView: {pose.SummaryText}");
                }

                if (GUILayout.Button(new GUIContent("Capture MainCam as Default", "Capture Main Camera transform as scene default"), GUILayout.Height(24)))
                {
                    Undo.RecordObject(so.targetObject, "Capture Scene Default from Camera");
                    Camera cam = camCtrl != null ? camCtrl.GetComponent<Camera>() : Camera.main;
                    Transform pTarget = camCtrl != null ? camCtrl.TargetPivotTransform : null;
                    StepCameraPose pose = StepCameraPose.CreateFromCamera(cam, pTarget);
                    ApplyPoseToSerializedProperty(defaultPoseProp, pose);
                    so.ApplyModifiedProperties();
                    if (camCtrl != null)
                    {
                        Undo.RecordObject(camCtrl, "Update CamCtrl Default Pose");
                        camCtrl.SceneDefaultPose = pose;
                        EditorUtility.SetDirty(camCtrl);
                    }
                    EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                    Debug.Log($"[TutorialStepCamera] Captured Scene Default Camera from Main Camera: {pose.SummaryText}");
                }

                if (GUILayout.Button(new GUIContent("Preview Default", "Preview the scene default camera pose"), GUILayout.Height(24)))
                {
                    StepCameraPose pose = ExtractPoseFromProperty(defaultPoseProp);
                    pose.ApplyPreviewInEditor(camCtrl != null ? camCtrl.GetComponent<Camera>() : null);
                }
            }
        }
    }

    private static void DrawStepNavigatorSection(SerializedObject so, SerializedProperty stepsProp, SerializedProperty defaultPoseProp, Transform rigRoot, ModelCameraController camCtrl)
    {
        if (stepsProp == null || stepsProp.arraySize == 0)
        {
            EditorGUILayout.HelpBox("No steps found on this tutorial manager.", MessageType.Info);
            return;
        }

        int count = stepsProp.arraySize;
        selectedStepIndex = Mathf.Clamp(selectedStepIndex, 0, count - 1);

        SerializedProperty curStepProp = stepsProp.GetArrayElementAtIndex(selectedStepIndex);
        SerializedProperty titleProp = curStepProp.FindPropertyRelative("title");
        SerializedProperty poseProp = curStepProp.FindPropertyRelative("cameraPose");
        SerializedProperty poseEnabledProp = poseProp.FindPropertyRelative("enabled");
        SerializedProperty highlightPartsProp = curStepProp.FindPropertyRelative("highlightPartNames");

        string stepTitle = titleProp != null ? titleProp.stringValue : $"Step {selectedStepIndex + 1}";

        using (new EditorGUILayout.VerticalScope(GUI.skin.box))
        {
            // Navigation Row: [<] Step X of N: Title [>]
            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.enabled = selectedStepIndex > 0;
                if (GUILayout.Button("◄ Prev", GUILayout.Width(60), GUILayout.Height(22)))
                {
                    selectedStepIndex--;
                }
                GUI.enabled = true;

                EditorGUILayout.LabelField($"Step {selectedStepIndex + 1} / {count}:  \"{stepTitle}\"", EditorStyles.boldLabel);

                GUI.enabled = selectedStepIndex < count - 1;
                if (GUILayout.Button("Next ►", GUILayout.Width(60), GUILayout.Height(22)))
                {
                    selectedStepIndex++;
                }
                GUI.enabled = true;
            }

            // Slider for fast scrubbing through steps
            selectedStepIndex = EditorGUILayout.IntSlider(selectedStepIndex + 1, 1, count) - 1;

            // 3D Anchor field (Drag & drop from Scene or Hierarchy)
            SerializedProperty anchorProp = curStepProp.FindPropertyRelative("cameraAnchor");
            if (anchorProp != null)
            {
                EditorGUILayout.Space(2f);
                EditorGUILayout.PropertyField(anchorProp, new GUIContent("3D Camera Anchor (Drag & Drop)"));
                if (anchorProp.objectReferenceValue != null)
                {
                    Transform anchorT = (Transform)anchorProp.objectReferenceValue;
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUIStyle greenStyle = new GUIStyle(EditorStyles.miniBoldLabel) { normal = { textColor = new Color(0.2f, 0.85f, 0.4f) } };
                        EditorGUILayout.LabelField($"✔ Linked to 3D Anchor: {anchorT.name}", greenStyle);
                        if (GUILayout.Button(new GUIContent("🔍 Drag in 3D Space", "Selects this anchor in the Scene View so you can drag it with the 3D move tool and see the live Camera Preview!"), GUILayout.Width(140), GUILayout.Height(22)))
                        {
                            Selection.activeGameObject = anchorT.gameObject;
                            SceneView.lastActiveSceneView?.Frame(new Bounds(anchorT.position, Vector3.one * 1.5f), false);
                        }
                    }
                }
                else
                {
                    if (GUILayout.Button(new GUIContent("➕ Create 3D Anchor for this Step", "Creates a 3D camera anchor GameObject in the scene for this step"), GUILayout.Height(22)))
                    {
                        CreateSingleAnchorForStep(so, stepsProp, selectedStepIndex, rigRoot, camCtrl);
                    }
                }
            }

            // Step status
            bool hasCustom = (anchorProp != null && anchorProp.objectReferenceValue != null) || (poseEnabledProp != null && poseEnabledProp.boolValue);
            if (hasCustom)
            {
                SerializedProperty pPos = poseProp.FindPropertyRelative("position");
                SerializedProperty pDist = poseProp.FindPropertyRelative("distance");
                SerializedProperty pPitch = poseProp.FindPropertyRelative("pitch");
                SerializedProperty pYaw = poseProp.FindPropertyRelative("yaw");
                string sum = $"Pos: ({pPos.vector3Value.x:F1}, {pPos.vector3Value.y:F1}, {pPos.vector3Value.z:F1}) | Rot: ({pPitch.floatValue:F0}°, {pYaw.floatValue:F0}°) | Dist: {pDist.floatValue:F1}m";

                GUIStyle greenStyle = new GUIStyle(EditorStyles.miniBoldLabel) { normal = { textColor = new Color(0.2f, 0.85f, 0.4f) } };
                EditorGUILayout.LabelField($"✔ Active Camera: {sum}", greenStyle);
            }
            else
            {
                GUIStyle greyStyle = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(0.7f, 0.7f, 0.7f) } };
                EditorGUILayout.LabelField("○ Using Scene Default Camera (no custom override set for this step)", greyStyle);
            }

            EditorGUILayout.Space(2f);

            // Action Buttons
            using (new EditorGUILayout.HorizontalScope())
            {
                Color oldColor = GUI.backgroundColor;
                GUI.backgroundColor = new Color(0.3f, 0.8f, 0.4f);
                if (GUILayout.Button(new GUIContent($"📸 Capture SceneView to Step {selectedStepIndex + 1}", "Save current SceneView camera position to this step"), GUILayout.Height(26)))
                {
                    Undo.RecordObject(so.targetObject, $"Capture Step {selectedStepIndex + 1} Camera");
                    Transform pTarget = camCtrl != null ? camCtrl.TargetPivotTransform : null;
                    StepCameraPose pose = StepCameraPose.CreateFromSceneView(pTarget);
                    ApplyPoseToSerializedProperty(poseProp, pose);
                    so.ApplyModifiedProperties();
                    EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                    Debug.Log($"[TutorialStepCamera] Captured Step {selectedStepIndex + 1} camera: {pose.SummaryText}");
                }
                GUI.backgroundColor = oldColor;

                if (GUILayout.Button(new GUIContent("👁️ Preview Step Camera", "Preview this step's camera pose in SceneView and Game view"), GUILayout.Height(26)))
                {
                    StepCameraPose pose = hasCustom ? ExtractPoseFromProperty(poseProp) : ExtractPoseFromProperty(defaultPoseProp);
                    pose.ApplyPreviewInEditor(camCtrl != null ? camCtrl.GetComponent<Camera>() : null);
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("🎯 Frame Step Target in View", "Finds the active part highlighted in this step and frames it in the SceneView"), GUILayout.Height(22)))
                {
                    FrameStepHighlightTarget(rigRoot, highlightPartsProp);
                }

                if (hasCustom)
                {
                    if (GUILayout.Button(new GUIContent("Clear Step Camera", "Reverts this step to the scene default camera"), GUILayout.Height(22)))
                    {
                        Undo.RecordObject(so.targetObject, $"Clear Step {selectedStepIndex + 1} Camera");
                        poseEnabledProp.boolValue = false;
                        so.ApplyModifiedProperties();
                        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                    }
                }
            }
        }
    }

    private static void DrawBatchUtilities(SerializedObject so, SerializedProperty stepsProp, ModelCameraController camCtrl)
    {
        showBatchTools = EditorGUILayout.Foldout(showBatchTools, "Batch Camera Tools", true);
        if (!showBatchTools || stepsProp == null) return;

        using (new EditorGUILayout.VerticalScope(GUI.skin.box))
        {
            if (GUILayout.Button("Capture Current SceneView to ALL Steps"))
            {
                if (EditorUtility.DisplayDialog("Capture to ALL Steps?", "This will overwrite the camera starting pose for ALL steps with your current SceneView camera. Continue?", "Yes", "Cancel"))
                {
                    Undo.RecordObject(so.targetObject, "Capture SceneView to ALL Steps");
                    Transform pTarget = camCtrl != null ? camCtrl.TargetPivotTransform : null;
                    StepCameraPose pose = StepCameraPose.CreateFromSceneView(pTarget);
                    for (int i = 0; i < stepsProp.arraySize; i++)
                    {
                        SerializedProperty p = stepsProp.GetArrayElementAtIndex(i).FindPropertyRelative("cameraPose");
                        ApplyPoseToSerializedProperty(p, pose);
                    }
                    so.ApplyModifiedProperties();
                    EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                    Debug.Log($"[TutorialStepCamera] Applied camera pose to all {stepsProp.arraySize} steps.");
                }
            }

            if (GUILayout.Button("Clear ALL Custom Step Cameras (Revert all to Scene Default)"))
            {
                if (EditorUtility.DisplayDialog("Clear ALL Step Cameras?", "This will disable custom camera poses for ALL steps, causing all steps to use the scene default. Continue?", "Yes", "Cancel"))
                {
                    Undo.RecordObject(so.targetObject, "Clear ALL Step Cameras");
                    for (int i = 0; i < stepsProp.arraySize; i++)
                    {
                        SerializedProperty p = stepsProp.GetArrayElementAtIndex(i).FindPropertyRelative("cameraPose").FindPropertyRelative("enabled");
                        p.boolValue = false;
                    }
                    so.ApplyModifiedProperties();
                    EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                    Debug.Log($"[TutorialStepCamera] Cleared custom cameras on all {stepsProp.arraySize} steps.");
                }
            }

            if (GUILayout.Button("✨ Auto-Generate Smart Starting Cameras for ALL Steps"))
            {
                Transform rigRoot = null;
                var player = UnityEngine.Object.FindFirstObjectByType<TutorialPlayer>();
                if (player != null && player.rigRoot != null) rigRoot = player.rigRoot;

                GenerateSmartStepCameras(so, stepsProp, rigRoot);
            }
        }
    }

    public static void GenerateSmartStepCameras(SerializedObject so, SerializedProperty stepsProp, Transform rigRoot)
    {
        if (stepsProp == null || stepsProp.arraySize == 0) return;
        Undo.RecordObject(so.targetObject, "Generate Smart Step Cameras");

        Vector3 rigCenter = rigRoot != null ? rigRoot.position : Vector3.zero;

        for (int i = 0; i < stepsProp.arraySize; i++)
        {
            SerializedProperty stepProp = stepsProp.GetArrayElementAtIndex(i);
            SerializedProperty titleProp = stepProp.FindPropertyRelative("title");
            SerializedProperty highlightPartsProp = stepProp.FindPropertyRelative("highlightPartNames");
            SerializedProperty poseProp = stepProp.FindPropertyRelative("cameraPose");

            string title = titleProp != null ? titleProp.stringValue.ToLowerInvariant() : "";
            (float pitch, float yaw, float dist, Vector3 offset) = GetOptimalAngleForStep(title);

            Vector3 pivot = rigCenter + offset;
            if (rigRoot != null && highlightPartsProp != null && highlightPartsProp.arraySize > 0)
            {
                Bounds b = default;
                bool hasBounds = false;
                for (int p = 0; p < highlightPartsProp.arraySize; p++)
                {
                    string partName = highlightPartsProp.GetArrayElementAtIndex(p).stringValue;
                    Transform t = FindDeepChild(rigRoot, partName);
                    if (t != null)
                    {
                        var rends = t.GetComponentsInChildren<Renderer>();
                        foreach (var r in rends)
                        {
                            if (!hasBounds) { b = r.bounds; hasBounds = true; }
                            else { b.Encapsulate(r.bounds); }
                        }
                    }
                }
                if (hasBounds)
                {
                    pivot = b.center;
                }
            }

            Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 pos = pivot - rot * Vector3.forward * dist;
            StepCameraPose pose = new StepCameraPose(pos, new Vector3(pitch, yaw, 0f), pivot, dist, 55f);
            ApplyPoseToSerializedProperty(poseProp, pose);
        }

        so.ApplyModifiedProperties();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log($"[TutorialStepCamera] Successfully generated smart camera starting poses for all {stepsProp.arraySize} steps!");
    }

    private static (float pitch, float yaw, float dist, Vector3 offset) GetOptimalAngleForStep(string title)
    {
        float pitch = 25f;
        float yaw = 0f;
        float dist = 2.4f;
        Vector3 offset = Vector3.zero;

        if (title.Contains("battery"))
        {
            pitch = 38f; yaw = -20f; dist = 1.9f; offset = new Vector3(0f, 0.2f, 0f);
        }
        else if (title.Contains("fan blade"))
        {
            pitch = 50f; yaw = 0f; dist = 2.2f; offset = new Vector3(0f, 0.2f, 0f);
        }
        else if (title.Contains("right body"))
        {
            pitch = 22f; yaw = 65f; dist = 1.8f; offset = new Vector3(0.25f, 0f, 0f);
        }
        else if (title.Contains("left body"))
        {
            pitch = 22f; yaw = -65f; dist = 1.8f; offset = new Vector3(-0.25f, 0f, 0f);
        }
        else if (title.Contains("front-right") || title.Contains("front right"))
        {
            pitch = 25f; yaw = 45f; dist = 1.8f; offset = new Vector3(0.2f, 0f, 0.2f);
        }
        else if (title.Contains("front-left") || title.Contains("front left"))
        {
            pitch = 25f; yaw = -45f; dist = 1.8f; offset = new Vector3(-0.2f, 0f, 0.2f);
        }
        else if (title.Contains("front arms") || title.Contains("front arm"))
        {
            pitch = 28f; yaw = 0f; dist = 2.1f; offset = new Vector3(0f, 0f, 0.25f);
        }
        else if (title.Contains("back-right") || title.Contains("back right"))
        {
            pitch = 25f; yaw = 135f; dist = 1.8f; offset = new Vector3(0.2f, 0f, -0.2f);
        }
        else if (title.Contains("back-left") || title.Contains("back left"))
        {
            pitch = 25f; yaw = -135f; dist = 1.8f; offset = new Vector3(-0.2f, 0f, -0.2f);
        }
        else if (title.Contains("back arms") || title.Contains("back arm"))
        {
            pitch = 28f; yaw = 180f; dist = 2.1f; offset = new Vector3(0f, 0f, -0.25f);
        }
        else if (title.Contains("one landing gear") || title.Contains("landing gear 2") || title.Contains("landing_gear_2"))
        {
            pitch = 12f; yaw = -75f; dist = 1.9f; offset = new Vector3(-0.2f, -0.2f, 0f);
        }
        else if (title.Contains("other landing gear") || title.Contains("landing gear 1") || title.Contains("landing_gear_1"))
        {
            pitch = 12f; yaw = 75f; dist = 1.9f; offset = new Vector3(0.2f, -0.2f, 0f);
        }
        else if (title.Contains("both halves"))
        {
            pitch = 32f; yaw = 0f; dist = 2.5f; offset = Vector3.zero;
        }

        return (pitch, yaw, dist, offset);
    }

    [MenuItem("Tools/Mobile Trainer/Generate Smart Step Cameras for Both Scenes")]
    public static void GenerateSmartCamerasForBothScenes()
    {
        string foldScenePath = "Assets/Screens/FoldForStorageScreen/FoldForStorage.unity";
        string deployScenePath = "Assets/Screens/DeployForFlightScreen/DeployForFlight.unity";

        EditorSceneManager.SaveOpenScenes();
        {
            var foldScene = EditorSceneManager.OpenScene(foldScenePath);
            var foldMgr = UnityEngine.Object.FindFirstObjectByType<FoldTutorialManager>();
            if (foldMgr != null)
            {
                var player = UnityEngine.Object.FindFirstObjectByType<TutorialPlayer>();
                Transform rigRoot = player != null ? player.rigRoot : null;
                var camCtrl = UnityEngine.Object.FindFirstObjectByType<ModelCameraController>();
                Camera cam = camCtrl != null ? camCtrl.GetComponent<Camera>() : Camera.main;

                SerializedObject so = new SerializedObject(foldMgr);
                if (cam != null)
                {
                    StepCameraPose sceneDefault = StepCameraPose.CreateFromCamera(cam, rigRoot);
                    sceneDefault.enabled = true;
                    ApplyPoseToSerializedProperty(so.FindProperty("sceneDefaultPose"), sceneDefault);

                    if (camCtrl != null)
                    {
                        SerializedObject camSo = new SerializedObject(camCtrl);
                        ApplyPoseToSerializedProperty(camSo.FindProperty("sceneDefaultPose"), sceneDefault);
                        camSo.ApplyModifiedProperties();
                    }
                }

                GenerateSmartStepCameras(so, so.FindProperty("steps"), rigRoot);
                EditorSceneManager.MarkSceneDirty(foldScene);
                EditorSceneManager.SaveScene(foldScene);
                Debug.Log("[TutorialStepCamera] Fold scene smart step cameras generated and saved!");
            }

            var deployScene = EditorSceneManager.OpenScene(deployScenePath);
            var deployMgr = UnityEngine.Object.FindFirstObjectByType<DeployTutorialManager>();
            if (deployMgr != null)
            {
                var player = UnityEngine.Object.FindFirstObjectByType<TutorialPlayer>();
                Transform rigRoot = player != null ? player.rigRoot : null;
                var camCtrl = UnityEngine.Object.FindFirstObjectByType<ModelCameraController>();
                Camera cam = camCtrl != null ? camCtrl.GetComponent<Camera>() : Camera.main;

                SerializedObject so = new SerializedObject(deployMgr);
                if (cam != null)
                {
                    StepCameraPose sceneDefault = StepCameraPose.CreateFromCamera(cam, rigRoot);
                    sceneDefault.enabled = true;
                    ApplyPoseToSerializedProperty(so.FindProperty("sceneDefaultPose"), sceneDefault);

                    if (camCtrl != null)
                    {
                        SerializedObject camSo = new SerializedObject(camCtrl);
                        ApplyPoseToSerializedProperty(camSo.FindProperty("sceneDefaultPose"), sceneDefault);
                        camSo.ApplyModifiedProperties();
                    }
                }

                GenerateSmartStepCameras(so, so.FindProperty("steps"), rigRoot);
                EditorSceneManager.MarkSceneDirty(deployScene);
                EditorSceneManager.SaveScene(deployScene);
                Debug.Log("[TutorialStepCamera] Deploy scene smart step cameras generated and saved!");
            }
        }
    }

    [MenuItem("Tools/Mobile Trainer/Generate Smart Step Cameras for Active Scene")]
    public static void GenerateSmartCamerasForActiveScene()
    {
        var foldMgr = UnityEngine.Object.FindFirstObjectByType<FoldTutorialManager>();
        if (foldMgr != null)
        {
            SerializedObject so = new SerializedObject(foldMgr);
            var player = UnityEngine.Object.FindFirstObjectByType<TutorialPlayer>();
            GenerateSmartStepCameras(so, so.FindProperty("steps"), player != null ? player.rigRoot : null);
            return;
        }

        var deployMgr = UnityEngine.Object.FindFirstObjectByType<DeployTutorialManager>();
        if (deployMgr != null)
        {
            SerializedObject so = new SerializedObject(deployMgr);
            var player = UnityEngine.Object.FindFirstObjectByType<TutorialPlayer>();
            GenerateSmartStepCameras(so, so.FindProperty("steps"), player != null ? player.rigRoot : null);
            return;
        }

        Debug.LogWarning("[TutorialStepCamera] No FoldTutorialManager or DeployTutorialManager found in active scene.");
    }

    public static void FrameStepHighlightTarget(Transform rigRoot, SerializedProperty highlightPartsProp)
    {
        if (rigRoot == null || highlightPartsProp == null || highlightPartsProp.arraySize == 0)
        {
            var svFallback = SceneView.lastActiveSceneView;
            if (svFallback != null && rigRoot != null)
            {
                svFallback.Frame(new Bounds(rigRoot.position, Vector3.one * 1.5f), false);
            }
            return;
        }

        List<Renderer> renderers = new List<Renderer>();
        for (int i = 0; i < highlightPartsProp.arraySize; i++)
        {
            string partName = highlightPartsProp.GetArrayElementAtIndex(i).stringValue;
            Transform t = FindDeepChild(rigRoot, partName);
            if (t != null)
            {
                renderers.AddRange(t.GetComponentsInChildren<Renderer>());
            }
        }

        if (renderers.Count > 0)
        {
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Count; i++)
            {
                b.Encapsulate(renderers[i].bounds);
            }
            // Expand slightly for visual breathing room
            b.Expand(0.15f);

            var sv = SceneView.lastActiveSceneView;
            if (sv != null)
            {
                sv.Frame(b, false);
                sv.Repaint();
            }
        }
    }

    public static Transform FindDeepChild(Transform parent, string name)
    {
        if (parent == null) return null;
        if (parent.name.Equals(name, StringComparison.OrdinalIgnoreCase)) return parent;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform result = FindDeepChild(parent.GetChild(i), name);
            if (result != null) return result;
        }
        return null;
    }

    public static void ApplyPoseToSerializedProperty(SerializedProperty property, StepCameraPose pose)
    {
        if (property == null) return;
        property.FindPropertyRelative("enabled").boolValue = true;
        property.FindPropertyRelative("position").vector3Value = pose.position;
        property.FindPropertyRelative("rotationEuler").vector3Value = pose.rotationEuler;
        property.FindPropertyRelative("pivot").vector3Value = pose.pivot;
        property.FindPropertyRelative("distance").floatValue = pose.distance;
        property.FindPropertyRelative("pitch").floatValue = pose.pitch;
        property.FindPropertyRelative("yaw").floatValue = pose.yaw;
        property.FindPropertyRelative("fieldOfView").floatValue = pose.fieldOfView;
    }

    public static StepCameraPose ExtractPoseFromProperty(SerializedProperty property)
    {
        if (property == null) return default;
        return new StepCameraPose
        {
            enabled = property.FindPropertyRelative("enabled")?.boolValue ?? true,
            position = property.FindPropertyRelative("position")?.vector3Value ?? Vector3.zero,
            rotationEuler = property.FindPropertyRelative("rotationEuler")?.vector3Value ?? Vector3.zero,
            pivot = property.FindPropertyRelative("pivot")?.vector3Value ?? Vector3.zero,
            distance = property.FindPropertyRelative("distance")?.floatValue ?? 2.5f,
            pitch = property.FindPropertyRelative("pitch")?.floatValue ?? 25f,
            yaw = property.FindPropertyRelative("yaw")?.floatValue ?? 0f,
            fieldOfView = property.FindPropertyRelative("fieldOfView")?.floatValue ?? 55f
        };
    }

    public static void Generate3DCameraAnchors(SerializedObject so, SerializedProperty stepsProp, Transform rigRoot, ModelCameraController camCtrl)
    {
        if (stepsProp == null || stepsProp.arraySize == 0) return;
        var comp = so.targetObject as Component;
        if (comp == null) return;

        // Find or create parent container under TutorialController
        Transform parentContainer = comp.transform.Find("StepCameraAnchors");
        if (parentContainer == null)
        {
            var go = new GameObject("StepCameraAnchors");
            Undo.RegisterCreatedObjectUndo(go, "Create StepCameraAnchors");
            go.transform.SetParent(comp.transform, false);
            parentContainer = go.transform;
        }

        Transform targetPivot = camCtrl != null ? camCtrl.TargetPivotTransform : rigRoot;
        int count = stepsProp.arraySize;

        for (int i = 0; i < count; i++)
        {
            SerializedProperty stepProp = stepsProp.GetArrayElementAtIndex(i);
            SerializedProperty titleProp = stepProp.FindPropertyRelative("title");
            SerializedProperty poseProp = stepProp.FindPropertyRelative("cameraPose");
            SerializedProperty anchorProp = stepProp.FindPropertyRelative("cameraAnchor");

            string stepTitle = titleProp != null ? titleProp.stringValue : $"Step {i + 1}";
            string anchorName = $"Step_{i + 1:D2}_Anchor";

            Transform existingAnchor = parentContainer.Find(anchorName);
            GameObject anchorGo;
            if (existingAnchor == null)
            {
                anchorGo = new GameObject(anchorName);
                Undo.RegisterCreatedObjectUndo(anchorGo, "Create Step Camera Anchor");
                anchorGo.transform.SetParent(parentContainer, false);
            }
            else
            {
                anchorGo = existingAnchor.gameObject;
            }

            StepCameraAnchor anchorComp = anchorGo.GetComponent<StepCameraAnchor>();
            if (anchorComp == null) anchorComp = anchorGo.AddComponent<StepCameraAnchor>();

            anchorComp.stepIndex = i;
            anchorComp.stepTitle = stepTitle;
            anchorComp.lookAtTarget = targetPivot;

            StepCameraPose pose = ExtractPoseFromProperty(poseProp);
            if (!pose.enabled || pose.distance <= 0.05f)
            {
                Camera cam = camCtrl != null ? camCtrl.GetComponent<Camera>() : Camera.main;
                pose = StepCameraPose.CreateFromCamera(cam, targetPivot);
                pose.enabled = true;
                ApplyPoseToSerializedProperty(poseProp, pose);
            }

            anchorGo.transform.position = pose.position;
            anchorGo.transform.rotation = Quaternion.Euler(pose.pitch, pose.yaw, 0f);
            anchorComp.fieldOfView = pose.fieldOfView > 10f ? pose.fieldOfView : 55f;

            if (anchorProp != null)
            {
                anchorProp.objectReferenceValue = anchorGo.transform;
            }
        }

        so.ApplyModifiedProperties();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log($"[TutorialStepCamera] Successfully created & linked {count} 3D Camera Anchors under '{parentContainer.name}'! Click any anchor in 3D space to drag and preview.");
    }

    public static void CreateSingleAnchorForStep(SerializedObject so, SerializedProperty stepsProp, int stepIndex, Transform rigRoot, ModelCameraController camCtrl)
    {
        if (stepsProp == null || stepIndex < 0 || stepIndex >= stepsProp.arraySize) return;
        var comp = so.targetObject as Component;
        if (comp == null) return;

        Transform parentContainer = comp.transform.Find("StepCameraAnchors");
        if (parentContainer == null)
        {
            var go = new GameObject("StepCameraAnchors");
            Undo.RegisterCreatedObjectUndo(go, "Create StepCameraAnchors");
            go.transform.SetParent(comp.transform, false);
            parentContainer = go.transform;
        }

        SerializedProperty stepProp = stepsProp.GetArrayElementAtIndex(stepIndex);
        SerializedProperty titleProp = stepProp.FindPropertyRelative("title");
        SerializedProperty poseProp = stepProp.FindPropertyRelative("cameraPose");
        SerializedProperty anchorProp = stepProp.FindPropertyRelative("cameraAnchor");

        string stepTitle = titleProp != null ? titleProp.stringValue : $"Step {stepIndex + 1}";
        string anchorName = $"Step_{stepIndex + 1:D2}_Anchor";

        Transform existing = parentContainer.Find(anchorName);
        GameObject anchorGo;
        if (existing == null)
        {
            anchorGo = new GameObject(anchorName);
            Undo.RegisterCreatedObjectUndo(anchorGo, "Create Step Camera Anchor");
            anchorGo.transform.SetParent(parentContainer, false);
        }
        else
        {
            anchorGo = existing.gameObject;
        }

        StepCameraAnchor anchor = anchorGo.GetComponent<StepCameraAnchor>();
        if (anchor == null) anchor = anchorGo.AddComponent<StepCameraAnchor>();

        Transform targetPivot = camCtrl != null ? camCtrl.TargetPivotTransform : rigRoot;
        anchor.stepIndex = stepIndex;
        anchor.stepTitle = stepTitle;
        anchor.lookAtTarget = targetPivot;

        StepCameraPose pose = ExtractPoseFromProperty(poseProp);
        if (!pose.enabled || pose.distance <= 0.05f)
        {
            Camera cam = camCtrl != null ? camCtrl.GetComponent<Camera>() : Camera.main;
            pose = StepCameraPose.CreateFromCamera(cam, targetPivot);
            pose.enabled = true;
            ApplyPoseToSerializedProperty(poseProp, pose);
        }

        anchorGo.transform.position = pose.position;
        anchorGo.transform.rotation = Quaternion.Euler(pose.pitch, pose.yaw, 0f);
        anchor.fieldOfView = pose.fieldOfView > 10f ? pose.fieldOfView : 55f;

        if (anchorProp != null)
        {
            anchorProp.objectReferenceValue = anchorGo.transform;
        }

        so.ApplyModifiedProperties();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

        Selection.activeGameObject = anchorGo;
        SceneView.lastActiveSceneView?.Frame(new Bounds(anchorGo.transform.position, Vector3.one * 1.5f), false);
        Debug.Log($"[TutorialStepCamera] Created 3D camera anchor '{anchorName}' for Step {stepIndex + 1}. Drag it directly in 3D space!");
    }

    public static void DrawSceneViewGizmos(MonoBehaviour target, SerializedObject so, SerializedProperty stepsProp)
    {
        if (stepsProp == null || stepsProp.arraySize == 0) return;
        int index = Mathf.Clamp(selectedStepIndex, 0, stepsProp.arraySize - 1);
        SerializedProperty stepProp = stepsProp.GetArrayElementAtIndex(index);
        SerializedProperty titleProp = stepProp.FindPropertyRelative("title");
        SerializedProperty anchorProp = stepProp.FindPropertyRelative("cameraAnchor");
        SerializedProperty poseProp = stepProp.FindPropertyRelative("cameraPose");

        Transform anchor = anchorProp != null ? (Transform)anchorProp.objectReferenceValue : null;
        string stepTitle = titleProp != null ? titleProp.stringValue : $"Step {index + 1}";

        Vector3 currentPos;
        Quaternion currentRot;

        if (anchor != null)
        {
            currentPos = anchor.position;
            currentRot = anchor.rotation;
        }
        else
        {
            var pose = ExtractPoseFromProperty(poseProp);
            if (!pose.enabled) return;
            currentPos = pose.position;
            currentRot = Quaternion.Euler(pose.pitch, pose.yaw, 0f);
        }

        // Draw interactive 3D Position and Rotation handles in Scene View
        EditorGUI.BeginChangeCheck();
        Vector3 newPos = Handles.PositionHandle(currentPos, currentRot);
        Quaternion newRot = Handles.RotationHandle(currentRot, currentPos);
        if (EditorGUI.EndChangeCheck())
        {
            if (anchor != null)
            {
                Undo.RecordObject(anchor, "Move 3D Camera Anchor");
                anchor.position = newPos;
                anchor.rotation = newRot;
            }
            else
            {
                Undo.RecordObject(so.targetObject, "Move Step Camera");
                poseProp.FindPropertyRelative("position").vector3Value = newPos;
                Vector3 euler = newRot.eulerAngles;
                poseProp.FindPropertyRelative("pitch").floatValue = StepCameraPose.NormalizeAngle(euler.x);
                poseProp.FindPropertyRelative("yaw").floatValue = StepCameraPose.NormalizeAngle(euler.y);
                so.ApplyModifiedProperties();
            }
        }

        // Draw camera frustum wireframe
        DrawFrustumWire(currentPos, currentRot, 55f, 1.2f, new Color(0.2f, 0.85f, 1f, 0.8f));
    }

    public static void DrawFrustumWire(Vector3 pos, Quaternion rot, float fov, float length, Color color)
    {
        Handles.color = color;
        float fovRad = fov * 0.5f * Mathf.Deg2Rad;
        float aspect = 16f / 9f;
        float halfH = Mathf.Tan(fovRad) * length;
        float halfW = halfH * aspect;

        Vector3 f1 = pos + rot * new Vector3(-halfW,  halfH, length);
        Vector3 f2 = pos + rot * new Vector3( halfW,  halfH, length);
        Vector3 f3 = pos + rot * new Vector3( halfW, -halfH, length);
        Vector3 f4 = pos + rot * new Vector3(-halfW, -halfH, length);

        Handles.DrawLine(pos, f1);
        Handles.DrawLine(pos, f2);
        Handles.DrawLine(pos, f3);
        Handles.DrawLine(pos, f4);

        Handles.DrawLine(f1, f2);
        Handles.DrawLine(f2, f3);
        Handles.DrawLine(f3, f4);
        Handles.DrawLine(f4, f1);
    }

    [MenuItem("Tools/Mobile Trainer/Generate 3D Camera Anchors for Both Scenes")]
    public static void Generate3DCameraAnchorsForBothScenes()
    {
        string foldScenePath = "Assets/Screens/FoldForStorageScreen/FoldForStorage.unity";
        string deployScenePath = "Assets/Screens/DeployForFlightScreen/DeployForFlight.unity";

        EditorSceneManager.SaveOpenScenes();
        {
            var foldScene = EditorSceneManager.OpenScene(foldScenePath);
            var foldMgr = UnityEngine.Object.FindFirstObjectByType<FoldTutorialManager>();
            if (foldMgr != null)
            {
                var player = UnityEngine.Object.FindFirstObjectByType<TutorialPlayer>();
                var camCtrl = UnityEngine.Object.FindFirstObjectByType<ModelCameraController>();
                SerializedObject so = new SerializedObject(foldMgr);
                Generate3DCameraAnchors(so, so.FindProperty("steps"), player != null ? player.rigRoot : null, camCtrl);
                EditorSceneManager.MarkSceneDirty(foldScene);
                EditorSceneManager.SaveScene(foldScene);
            }

            var deployScene = EditorSceneManager.OpenScene(deployScenePath);
            var deployMgr = UnityEngine.Object.FindFirstObjectByType<DeployTutorialManager>();
            if (deployMgr != null)
            {
                var player = UnityEngine.Object.FindFirstObjectByType<TutorialPlayer>();
                var camCtrl = UnityEngine.Object.FindFirstObjectByType<ModelCameraController>();
                SerializedObject so = new SerializedObject(deployMgr);
                Generate3DCameraAnchors(so, so.FindProperty("steps"), player != null ? player.rigRoot : null, camCtrl);
                EditorSceneManager.MarkSceneDirty(deployScene);
                EditorSceneManager.SaveScene(deployScene);
            }
        }
    }

    [MenuItem("Tools/Mobile Trainer/Generate 3D Camera Anchors for Active Scene")]
    public static void Generate3DCameraAnchorsForActiveScene()
    {
        var foldMgr = UnityEngine.Object.FindFirstObjectByType<FoldTutorialManager>();
        if (foldMgr != null)
        {
            var player = UnityEngine.Object.FindFirstObjectByType<TutorialPlayer>();
            var camCtrl = UnityEngine.Object.FindFirstObjectByType<ModelCameraController>();
            SerializedObject so = new SerializedObject(foldMgr);
            Generate3DCameraAnchors(so, so.FindProperty("steps"), player != null ? player.rigRoot : null, camCtrl);
            return;
        }

        var deployMgr = UnityEngine.Object.FindFirstObjectByType<DeployTutorialManager>();
        if (deployMgr != null)
        {
            var player = UnityEngine.Object.FindFirstObjectByType<TutorialPlayer>();
            var camCtrl = UnityEngine.Object.FindFirstObjectByType<ModelCameraController>();
            SerializedObject so = new SerializedObject(deployMgr);
            Generate3DCameraAnchors(so, so.FindProperty("steps"), player != null ? player.rigRoot : null, camCtrl);
            return;
        }

        Debug.LogWarning("[TutorialStepCamera] No FoldTutorialManager or DeployTutorialManager found in active scene.");
    }
}

/// <summary>
/// Custom inspector for FoldTutorialManager.
/// </summary>
[CustomEditor(typeof(FoldTutorialManager))]
public class FoldTutorialManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        var manager = (FoldTutorialManager)target;
        var stepsProp = serializedObject.FindProperty("steps");
        var defaultPoseProp = serializedObject.FindProperty("sceneDefaultPose");

        Transform rigRoot = null;
        var player = manager.GetComponent<TutorialPlayer>() ?? UnityEngine.Object.FindFirstObjectByType<TutorialPlayer>();
        if (player != null && player.rigRoot != null) rigRoot = player.rigRoot;

        TutorialStepCameraEditorHelper.DrawCameraSetupToolbar(
            serializedObject,
            stepsProp,
            defaultPoseProp,
            rigRoot,
            manager.CameraController ?? UnityEngine.Object.FindFirstObjectByType<ModelCameraController>()
        );

        DrawDefaultInspector();

        serializedObject.ApplyModifiedProperties();
    }

    private void OnSceneGUI()
    {
        var manager = (FoldTutorialManager)target;
        var stepsProp = serializedObject.FindProperty("steps");
        TutorialStepCameraEditorHelper.DrawSceneViewGizmos(manager, serializedObject, stepsProp);
    }
}

/// <summary>
/// Custom inspector for DeployTutorialManager.
/// </summary>
[CustomEditor(typeof(DeployTutorialManager))]
public class DeployTutorialManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        var manager = (DeployTutorialManager)target;
        var stepsProp = serializedObject.FindProperty("steps");
        var defaultPoseProp = serializedObject.FindProperty("sceneDefaultPose");

        Transform rigRoot = null;
        var player = manager.GetComponent<TutorialPlayer>() ?? UnityEngine.Object.FindFirstObjectByType<TutorialPlayer>();
        if (player != null && player.rigRoot != null) rigRoot = player.rigRoot;

        TutorialStepCameraEditorHelper.DrawCameraSetupToolbar(
            serializedObject,
            stepsProp,
            defaultPoseProp,
            rigRoot,
            manager.CameraController ?? UnityEngine.Object.FindFirstObjectByType<ModelCameraController>()
        );

        DrawDefaultInspector();

        serializedObject.ApplyModifiedProperties();
    }

    private void OnSceneGUI()
    {
        var manager = (DeployTutorialManager)target;
        var stepsProp = serializedObject.FindProperty("steps");
        TutorialStepCameraEditorHelper.DrawSceneViewGizmos(manager, serializedObject, stepsProp);
    }
}

/// <summary>
/// Custom inspector for ModelCameraController.
/// </summary>
[CustomEditor(typeof(ModelCameraController))]
public class ModelCameraControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        var ctrl = (ModelCameraController)target;
        var defaultPoseProp = serializedObject.FindProperty("sceneDefaultPose");

        EditorGUILayout.Space(4f);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("🎥 Scene Default Camera Controls", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Capture SceneView as Default", GUILayout.Height(24)))
                {
                    ctrl.CaptureSceneViewToSceneDefault();
                }
                if (GUILayout.Button("Capture Camera as Default", GUILayout.Height(24)))
                {
                    ctrl.CaptureCurrentCameraToSceneDefault();
                }
                if (GUILayout.Button("Preview Default", GUILayout.Height(24)))
                {
                    ctrl.PreviewSceneDefaultPose();
                }
            }
        }
        EditorGUILayout.Space(4f);

        DrawDefaultInspector();

        serializedObject.ApplyModifiedProperties();
    }
}
