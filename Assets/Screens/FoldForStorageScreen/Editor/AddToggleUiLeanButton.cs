#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Lean.Gui;

public static class AddToggleUiLeanButton
{
    private const string PrefabPath = "Assets/Screens/FoldForStorageScreen/Buttons/Tap Button (LeanButton).prefab";

    [MenuItem("Tools/MobileTrainer/Setup Toggle UI LeanButton in Both Scenes")]
    public static void SetupBothScenes()
    {
        SetupScene("Assets/Screens/FoldForStorageScreen/FoldForStorage.unity", "FoldTutorialCanvas", true);
        SetupScene("Assets/Screens/DeployForFlightScreen/DeployForFlight.unity", "DeployTutorialCanvas", false);
        AssetDatabase.SaveAssets();
        Debug.Log("<color=green><b>[SetupToggleUi]</b> Successfully configured Toggle UI LeanButton in both FoldForStorage and DeployForFlight scenes!</color>");
    }

    private static void SetupScene(string scenePath, string canvasName, bool isFold)
    {
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        if (!scene.IsValid())
        {
            Debug.LogError("Failed to open scene: " + scenePath);
            return;
        }

        GameObject canvasGo = GameObject.Find(canvasName);
        if (canvasGo == null)
        {
            Debug.LogError("Canvas not found: " + canvasName + " in " + scenePath);
            return;
        }

        // Find or instantiate button
        Transform existingBtn = canvasGo.transform.Find("Toggle UI Button (LeanButton)");
        GameObject btnGo;
        if (existingBtn != null)
        {
            btnGo = existingBtn.gameObject;
        }
        else
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                Debug.LogError("Prefab not found at: " + PrefabPath);
                return;
            }

            btnGo = (GameObject)PrefabUtility.InstantiatePrefab(prefab, canvasGo.transform);
            btnGo.name = "Toggle UI Button (LeanButton)";
            PrefabUtility.UnpackPrefabInstance(btnGo, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        }

        // Place right before Fade Overlay if present
        Transform fadeOverlay = canvasGo.transform.Find("Fade Overlay");
        if (fadeOverlay != null)
        {
            btnGo.transform.SetSiblingIndex(fadeOverlay.GetSiblingIndex());
        }

        // Configure RectTransform
        RectTransform rt = btnGo.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 0.5f);
        rt.anchorMax = new Vector2(1f, 0.5f);
        rt.pivot = new Vector2(1f, 0.5f);
        rt.anchoredPosition = new Vector2(-20f, 0f);
        rt.sizeDelta = new Vector2(190f, 60f);

        // Configure Text
        Transform capTrans = btnGo.transform.Find("Cap");
        Text textComp = capTrans != null ? capTrans.Find("Text")?.GetComponent<Text>() : null;
        if (textComp != null)
        {
            textComp.text = "HIDE UI";
            textComp.fontSize = 38;
            textComp.alignment = TextAnchor.MiddleCenter;
            textComp.horizontalOverflow = HorizontalWrapMode.Overflow;
            textComp.verticalOverflow = VerticalWrapMode.Overflow;
            textComp.color = Color.black;
            EditorUtility.SetDirty(textComp);
        }

        // Setup LeanButton
        LeanButton leanBtn = btnGo.GetComponent<LeanButton>();
        if (leanBtn == null)
        {
            leanBtn = btnGo.AddComponent<LeanButton>();
        }

        // Find Panels
        GameObject topHeader = canvasGo.transform.Find("TopHeaderPanel")?.gameObject;
        GameObject bottomControls = canvasGo.transform.Find("BottomControlsPanel")?.gameObject;

        if (isFold)
        {
            FoldTutorialManager mgr = Object.FindFirstObjectByType<FoldTutorialManager>();
            if (mgr != null)
            {
                // Clear persistent calls and wire ToggleUiVisibility
                int count = leanBtn.OnClick.GetPersistentEventCount();
                for (int i = count - 1; i >= 0; i--)
                {
                    UnityEditor.Events.UnityEventTools.RemovePersistentListener(leanBtn.OnClick, i);
                }
                UnityEditor.Events.UnityEventTools.AddVoidPersistentListener(leanBtn.OnClick, mgr.ToggleUiVisibility);

                // Set serialized properties on FoldTutorialManager
                SerializedObject so = new SerializedObject(mgr);
                so.Update();
                var pTop = so.FindProperty("topHeaderPanel");
                if (pTop != null) pTop.objectReferenceValue = topHeader;
                var pBot = so.FindProperty("bottomControlsPanel");
                if (pBot != null) pBot.objectReferenceValue = bottomControls;
                var pBtn = so.FindProperty("toggleUiLeanButton");
                if (pBtn != null) pBtn.objectReferenceValue = leanBtn;
                var pTxt = so.FindProperty("toggleUiButtonLegacyText");
                if (pTxt != null) pTxt.objectReferenceValue = textComp;
                var pVis = so.FindProperty("isUiVisible");
                if (pVis != null) pVis.boolValue = true;
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(mgr);
            }
        }
        else
        {
            DeployTutorialManager mgr = Object.FindFirstObjectByType<DeployTutorialManager>();
            if (mgr != null)
            {
                // Clear persistent calls and wire ToggleUiVisibility
                int count = leanBtn.OnClick.GetPersistentEventCount();
                for (int i = count - 1; i >= 0; i--)
                {
                    UnityEditor.Events.UnityEventTools.RemovePersistentListener(leanBtn.OnClick, i);
                }
                UnityEditor.Events.UnityEventTools.AddVoidPersistentListener(leanBtn.OnClick, mgr.ToggleUiVisibility);

                // Set serialized properties on DeployTutorialManager
                SerializedObject so = new SerializedObject(mgr);
                so.Update();
                var pTop = so.FindProperty("topHeaderPanel");
                if (pTop != null) pTop.objectReferenceValue = topHeader;
                var pBot = so.FindProperty("bottomControlsPanel");
                if (pBot != null) pBot.objectReferenceValue = bottomControls;
                var pBtn = so.FindProperty("toggleUiLeanButton");
                if (pBtn != null) pBtn.objectReferenceValue = leanBtn;
                var pTxt = so.FindProperty("toggleUiButtonLegacyText");
                if (pTxt != null) pTxt.objectReferenceValue = textComp;
                var pVis = so.FindProperty("isUiVisible");
                if (pVis != null) pVis.boolValue = true;
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(mgr);
            }
        }

        EditorUtility.SetDirty(btnGo);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[SetupToggleUi] Scene setup complete and saved: " + scenePath);
    }
}
#endif
