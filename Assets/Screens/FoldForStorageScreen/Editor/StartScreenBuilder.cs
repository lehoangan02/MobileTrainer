using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using Lean.Gui;

public static class StartScreenBuilder
{
    private const string StartScenePath = "Assets/Screens/StartScreen/StartScreen.unity";
    private const string CircleSpriteGuid = "7c5922f9fb70b6046851f338315c81ad"; // Round50.png
    private const string FontGuid = "d2c8e8d82fdfb4d60b5a60af277fd26e";

    [MenuItem("Tools/Configure Start Screen UI", priority = 25)]
    public static void ConfigureStartScreen()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        string previousPath = activeScene.path;
        bool switchedScene = false;

        if (activeScene.path != StartScenePath)
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                activeScene = EditorSceneManager.OpenScene(StartScenePath);
                switchedScene = true;
            }
            else
            {
                Debug.LogWarning("[StartScreenBuilder] Cancelled opening StartScreen.");
                return;
            }
        }

        GameObject canvasGo = GameObject.Find("Canvas");
        if (canvasGo == null)
        {
            Debug.LogError("[StartScreenBuilder] Canvas GameObject not found!");
            return;
        }

        Transform canvasTrans = canvasGo.transform;

        // Clean up any stale or duplicate Settings Modals
        for (int i = canvasTrans.childCount - 1; i >= 0; i--)
        {
            Transform child = canvasTrans.GetChild(i);
            if (child.name.StartsWith("Settings Modal"))
            {
                Undo.DestroyObjectImmediate(child.gameObject);
            }
        }

        Transform startBtnTrans = null;
        Transform aboutBtnTrans = null;
        Transform settingsBtnTrans = null;
        Transform aboutModalTrans = null;
        Transform fadeOverlayTrans = null;
        Transform bgImgTrans = null;

        List<Transform> extraButtons = new List<Transform>();

        for (int i = 0; i < canvasTrans.childCount; i++)
        {
            Transform child = canvasTrans.GetChild(i);
            string childName = child.name;

            if (childName.Contains("Modal"))
            {
                aboutModalTrans = child;
            }
            else if (childName.Contains("Fade Overlay"))
            {
                fadeOverlayTrans = child;
            }
            else if (childName == "Image" && child.GetComponent<LeanButton>() == null)
            {
                bgImgTrans = child;
            }
            else if (child.GetComponent<LeanButton>() != null)
            {
                RectTransform rt = child.GetComponent<RectTransform>();
                float y = rt.anchoredPosition.y;

                if (childName.StartsWith("Start") || Mathf.Abs(y - 200f) < 40f)
                {
                    if (startBtnTrans == null)
                        startBtnTrans = child;
                    else
                        extraButtons.Add(child);
                }
                else if (childName.StartsWith("About") || Mathf.Abs(y - (-200f)) < 40f)
                {
                    if (aboutBtnTrans == null)
                        aboutBtnTrans = child;
                    else
                        extraButtons.Add(child);
                }
                else if (childName.StartsWith("Settings") || childName.StartsWith("Open Window") || Mathf.Abs(y) < 40f)
                {
                    // Check if it's the about button (has onClick to modal)
                    LeanButton lb = child.GetComponent<LeanButton>();
                    SerializedObject so = new SerializedObject(lb);
                    var calls = so.FindProperty("onClick.m_PersistentCalls.m_Calls");
                    bool hasModalCall = false;
                    if (calls != null && calls.arraySize > 0)
                    {
                        for (int c = 0; c < calls.arraySize; c++)
                        {
                            var targetProp = calls.GetArrayElementAtIndex(c).FindPropertyRelative("m_Target");
                            if (targetProp != null && targetProp.objectReferenceValue != null && targetProp.objectReferenceValue.name.Contains("Modal"))
                            {
                                hasModalCall = true;
                                break;
                            }
                        }
                    }

                    if (hasModalCall && aboutBtnTrans == null)
                    {
                        aboutBtnTrans = child;
                    }
                    else if (settingsBtnTrans == null)
                    {
                        settingsBtnTrans = child;
                    }
                    else
                    {
                        extraButtons.Add(child);
                    }
                }
                else
                {
                    extraButtons.Add(child);
                }
            }
        }

        // Destroy any duplicate/extra buttons found
        foreach (var extra in extraButtons)
        {
            Debug.Log($"[StartScreenBuilder] Removing redundant button: {extra.name}");
            Undo.DestroyObjectImmediate(extra.gameObject);
        }

        if (settingsBtnTrans == null && aboutBtnTrans != null)
        {
            GameObject newBtn = Object.Instantiate(aboutBtnTrans.gameObject, canvasTrans);
            settingsBtnTrans = newBtn.transform;
            Undo.RegisterCreatedObjectUndo(newBtn, "Create Settings Button");
        }

        if (aboutBtnTrans == null && startBtnTrans != null)
        {
            GameObject newBtn = Object.Instantiate(startBtnTrans.gameObject, canvasTrans);
            aboutBtnTrans = newBtn.transform;
            Undo.RegisterCreatedObjectUndo(newBtn, "Create About Button");
        }

        Font fontAsset = AssetDatabase.LoadAssetAtPath<Font>(AssetDatabase.GUIDToAssetPath(FontGuid));
        Sprite circleSprite = AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath(CircleSpriteGuid));

        // 1. Configure START Button (x: 325, y: 200)
        if (startBtnTrans != null)
        {
            startBtnTrans.name = "Start Button (LeanButton)";
            RectTransform rt = startBtnTrans.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0.5f);
            rt.anchorMax = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(325f, 200f);
            rt.sizeDelta = new Vector2(250f, 100f);

            Text label = startBtnTrans.GetComponentInChildren<Text>(true);
            if (label != null)
            {
                label.text = "START";
                EditorUtility.SetDirty(label);
            }

            if (fadeOverlayTrans != null)
            {
                LeanButton lb = startBtnTrans.GetComponent<LeanButton>();
                SerializedObject so = new SerializedObject(lb);
                var calls = so.FindProperty("onClick.m_PersistentCalls.m_Calls");
                calls.ClearArray();
                calls.arraySize = 1;
                var call = calls.GetArrayElementAtIndex(0);
                call.FindPropertyRelative("m_Target").objectReferenceValue = fadeOverlayTrans.gameObject;
                call.FindPropertyRelative("m_TargetAssemblyTypeName").stringValue = typeof(GameObject).AssemblyQualifiedName;
                call.FindPropertyRelative("m_MethodName").stringValue = "SendMessage";
                call.FindPropertyRelative("m_Mode").enumValueIndex = 5; // String
                call.FindPropertyRelative("m_Arguments.m_StringArgument").stringValue = "BeginTransitions";
                call.FindPropertyRelative("m_CallState").enumValueIndex = 2; // RuntimeOnly
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(lb);
            }
            EditorUtility.SetDirty(startBtnTrans.gameObject);
        }

        // 2. Configure SETTINGS Button (x: 325, y: 0)
        if (settingsBtnTrans != null)
        {
            settingsBtnTrans.name = "Settings Button (LeanButton)";
            RectTransform rt = settingsBtnTrans.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0.5f);
            rt.anchorMax = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(325f, 0f);
            rt.sizeDelta = new Vector2(250f, 100f);

            Text label = settingsBtnTrans.GetComponentInChildren<Text>(true);
            if (label != null)
            {
                label.text = "SETTINGS";
                EditorUtility.SetDirty(label);
            }
            EditorUtility.SetDirty(settingsBtnTrans.gameObject);
        }

        // 3. Configure ABOUT Button (x: 325, y: -200)
        if (aboutBtnTrans != null)
        {
            aboutBtnTrans.name = "About Button (LeanButton)";
            RectTransform rt = aboutBtnTrans.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0.5f);
            rt.anchorMax = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(325f, -200f);
            rt.sizeDelta = new Vector2(250f, 100f);

            Text label = aboutBtnTrans.GetComponentInChildren<Text>(true);
            if (label != null)
            {
                label.text = "ABOUT";
                EditorUtility.SetDirty(label);
            }

            if (aboutModalTrans != null)
            {
                LeanWindow aboutWindow = aboutModalTrans.GetComponent<LeanWindow>();
                LeanButton lb = aboutBtnTrans.GetComponent<LeanButton>();
                SerializedObject so = new SerializedObject(lb);
                var calls = so.FindProperty("onClick.m_PersistentCalls.m_Calls");
                calls.ClearArray();
                calls.arraySize = 1;
                var call = calls.GetArrayElementAtIndex(0);
                call.FindPropertyRelative("m_Target").objectReferenceValue = aboutWindow;
                call.FindPropertyRelative("m_TargetAssemblyTypeName").stringValue = typeof(LeanWindow).AssemblyQualifiedName;
                call.FindPropertyRelative("m_MethodName").stringValue = "TurnOn";
                call.FindPropertyRelative("m_Mode").enumValueIndex = 1; // Void
                call.FindPropertyRelative("m_CallState").enumValueIndex = 2; // RuntimeOnly
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(lb);
            }
            EditorUtility.SetDirty(aboutBtnTrans.gameObject);
        }

        // 4. Create Settings Modal (LeanWindow)
        GameObject settingsModalGo = Object.Instantiate(aboutModalTrans.gameObject, canvasTrans);
        settingsModalGo.name = "Settings Modal (LeanWindow)";
        Undo.RegisterCreatedObjectUndo(settingsModalGo, "Create Settings Modal");

        LeanWindow settingsWindow = settingsModalGo.GetComponent<LeanWindow>();
        if (settingsWindow != null)
        {
            settingsWindow.TurnOff();
        }

        Transform bgTrans = settingsModalGo.transform.Find("Background (LeanButton)");
        if (bgTrans != null)
        {
            LeanButton bgBtn = bgTrans.GetComponent<LeanButton>();
            SerializedObject so = new SerializedObject(bgBtn);
            var calls = so.FindProperty("onClick.m_PersistentCalls.m_Calls");
            calls.ClearArray();
            calls.arraySize = 1;
            var call = calls.GetArrayElementAtIndex(0);
            call.FindPropertyRelative("m_Target").objectReferenceValue = settingsWindow;
            call.FindPropertyRelative("m_TargetAssemblyTypeName").stringValue = typeof(LeanWindow).AssemblyQualifiedName;
            call.FindPropertyRelative("m_MethodName").stringValue = "TurnOff";
            call.FindPropertyRelative("m_Mode").enumValueIndex = 1; // Void
            call.FindPropertyRelative("m_CallState").enumValueIndex = 2; // RuntimeOnly
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(bgBtn);
        }

        Transform panelTrans = settingsModalGo.transform.Find("Panel");
        if (panelTrans != null)
        {
            for (int i = panelTrans.childCount - 1; i >= 0; i--)
            {
                Transform child = panelTrans.GetChild(i);
                if (child.name == "Image" || child.name == "Text")
                {
                    Undo.DestroyObjectImmediate(child.gameObject);
                }
            }

            Transform circleTrans = panelTrans.Find("Circle");
            if (circleTrans != null)
            {
                LeanButton circleBtn = circleTrans.GetComponent<LeanButton>();
                if (circleBtn == null) circleBtn = circleTrans.gameObject.AddComponent<LeanButton>();
                SerializedObject so = new SerializedObject(circleBtn);
                var calls = so.FindProperty("onClick.m_PersistentCalls.m_Calls");
                calls.ClearArray();
                calls.arraySize = 1;
                var call = calls.GetArrayElementAtIndex(0);
                call.FindPropertyRelative("m_Target").objectReferenceValue = settingsWindow;
                call.FindPropertyRelative("m_TargetAssemblyTypeName").stringValue = typeof(LeanWindow).AssemblyQualifiedName;
                call.FindPropertyRelative("m_MethodName").stringValue = "TurnOff";
                call.FindPropertyRelative("m_Mode").enumValueIndex = 1; // Void
                call.FindPropertyRelative("m_CallState").enumValueIndex = 2; // RuntimeOnly
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(circleBtn);
            }

            GameObject titleGo = new GameObject("Title Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            titleGo.transform.SetParent(panelTrans, false);
            RectTransform titleRt = titleGo.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0.5f, 0.5f);
            titleRt.anchorMax = new Vector2(0.5f, 0.5f);
            titleRt.anchoredPosition = new Vector2(0f, 190f);
            titleRt.sizeDelta = new Vector2(800f, 80f);

            Text titleText = titleGo.GetComponent<Text>();
            titleText.font = fontAsset;
            titleText.text = "DRONE THEME";
            titleText.fontSize = 52;
            titleText.fontStyle = FontStyle.Bold;
            titleText.color = new Color(0.12f, 0.16f, 0.23f, 1f);
            titleText.alignment = TextAnchor.MiddleCenter;

            GameObject radioGroupGo = new GameObject("Theme Radio Group", typeof(RectTransform), typeof(DroneThemeSettings));
            radioGroupGo.transform.SetParent(panelTrans, false);
            RectTransform radioGroupRt = radioGroupGo.GetComponent<RectTransform>();
            radioGroupRt.anchorMin = new Vector2(0.5f, 0.5f);
            radioGroupRt.anchorMax = new Vector2(0.5f, 0.5f);
            radioGroupRt.anchoredPosition = new Vector2(0f, -40f);
            radioGroupRt.sizeDelta = new Vector2(880f, 320f);

            DroneThemeSettings themeSettings = radioGroupGo.GetComponent<DroneThemeSettings>();
            themeSettings.options = new DroneThemeSettings.ThemeRadioOption[3];

            string[] themeNames = new string[]
            {
                "Version 1: Cyan Ghost (Red Highlight)",
                "Version 2: Carbon Fiber (Yellow Highlight)",
                "Version 3: Carbon Fiber (Cyan Highlight)"
            };

            float[] yOffsets = new float[] { 90f, 0f, -90f };

            for (int i = 0; i < 3; i++)
            {
                GameObject optGo = new GameObject($"Option_{i + 1}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LeanButton), typeof(LeanToggle));
                optGo.transform.SetParent(radioGroupGo.transform, false);

                RectTransform optRt = optGo.GetComponent<RectTransform>();
                optRt.anchorMin = new Vector2(0.5f, 0.5f);
                optRt.anchorMax = new Vector2(0.5f, 0.5f);
                optRt.anchoredPosition = new Vector2(0f, yOffsets[i]);
                optRt.sizeDelta = new Vector2(850f, 70f);

                Image optBg = optGo.GetComponent<Image>();
                optBg.sprite = circleSprite;
                optBg.type = Image.Type.Sliced;
                optBg.color = new Color(0.92f, 0.94f, 0.96f, 0.6f);

                LeanButton optBtn = optGo.GetComponent<LeanButton>();
                LeanToggle optToggle = optGo.GetComponent<LeanToggle>();
                optToggle.TurnOffSiblings = true;

                GameObject ringGo = new GameObject("Outer Ring", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                ringGo.transform.SetParent(optGo.transform, false);
                RectTransform ringRt = ringGo.GetComponent<RectTransform>();
                ringRt.anchorMin = new Vector2(0f, 0.5f);
                ringRt.anchorMax = new Vector2(0f, 0.5f);
                ringRt.anchoredPosition = new Vector2(50f, 0f);
                ringRt.sizeDelta = new Vector2(36f, 36f);
                Image ringImg = ringGo.GetComponent<Image>();
                ringImg.sprite = circleSprite;
                ringImg.color = new Color(0.70f, 0.74f, 0.78f, 1f);

                GameObject dotGo = new GameObject("Inner Dot", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                dotGo.transform.SetParent(ringGo.transform, false);
                RectTransform dotRt = dotGo.GetComponent<RectTransform>();
                dotRt.anchorMin = new Vector2(0.5f, 0.5f);
                dotRt.anchorMax = new Vector2(0.5f, 0.5f);
                dotRt.anchoredPosition = Vector2.zero;
                dotRt.sizeDelta = new Vector2(22f, 22f);
                Image dotImg = dotGo.GetComponent<Image>();
                dotImg.sprite = circleSprite;
                dotImg.color = (i == 0) ? new Color(0.12f, 0.58f, 0.95f, 1f) : new Color(0f, 0f, 0f, 0f);

                GameObject labelGo = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                labelGo.transform.SetParent(optGo.transform, false);
                RectTransform labelRt = labelGo.GetComponent<RectTransform>();
                labelRt.anchorMin = new Vector2(0f, 0.5f);
                labelRt.anchorMax = new Vector2(1f, 0.5f);
                labelRt.pivot = new Vector2(0f, 0.5f);
                labelRt.anchoredPosition = new Vector2(90f, 0f);
                labelRt.sizeDelta = new Vector2(-110f, 50f);

                Text labelText = labelGo.GetComponent<Text>();
                labelText.font = fontAsset;
                labelText.text = themeNames[i];
                labelText.fontSize = 32;
                labelText.fontStyle = FontStyle.Bold;
                labelText.color = new Color(0.10f, 0.15f, 0.22f, 1f);
                labelText.alignment = TextAnchor.MiddleLeft;

                var radioOption = new DroneThemeSettings.ThemeRadioOption
                {
                    themeName = themeNames[i],
                    toggle = optToggle,
                    button = optBtn,
                    innerDot = dotImg,
                    outerRing = ringImg,
                    label = labelText
                };
                themeSettings.options[i] = radioOption;

                int themeIndex = i;
                SerializedObject soOptBtn = new SerializedObject(optBtn);
                var optCalls = soOptBtn.FindProperty("onClick.m_PersistentCalls.m_Calls");
                optCalls.ClearArray();
                optCalls.arraySize = 1;
                var optCall = optCalls.GetArrayElementAtIndex(0);
                optCall.FindPropertyRelative("m_Target").objectReferenceValue = themeSettings;
                optCall.FindPropertyRelative("m_TargetAssemblyTypeName").stringValue = typeof(DroneThemeSettings).AssemblyQualifiedName;
                optCall.FindPropertyRelative("m_MethodName").stringValue = "SelectTheme";
                optCall.FindPropertyRelative("m_Mode").enumValueIndex = 3; // Int
                optCall.FindPropertyRelative("m_Arguments.m_IntArgument").intValue = themeIndex;
                optCall.FindPropertyRelative("m_CallState").enumValueIndex = 2; // RuntimeOnly
                soOptBtn.ApplyModifiedProperties();
            }

            EditorUtility.SetDirty(themeSettings);
        }

        // Wire SETTINGS Button onClick to Settings Modal TurnOn
        if (settingsBtnTrans != null)
        {
            LeanButton lb = settingsBtnTrans.GetComponent<LeanButton>();
            SerializedObject so = new SerializedObject(lb);
            var calls = so.FindProperty("onClick.m_PersistentCalls.m_Calls");
            calls.ClearArray();
            calls.arraySize = 1;
            var call = calls.GetArrayElementAtIndex(0);
            call.FindPropertyRelative("m_Target").objectReferenceValue = settingsWindow;
            call.FindPropertyRelative("m_TargetAssemblyTypeName").stringValue = typeof(LeanWindow).AssemblyQualifiedName;
            call.FindPropertyRelative("m_MethodName").stringValue = "TurnOn";
            call.FindPropertyRelative("m_Mode").enumValueIndex = 1; // Void
            call.FindPropertyRelative("m_CallState").enumValueIndex = 2; // RuntimeOnly
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(lb);
        }

        // 5. Enforce Precise Canvas Sibling Order (Draw Order & Overlay Coverage)
        // 0: Wallpaper Background Image
        // 1: Start Button
        // 2: Settings Button
        // 3: About Button
        // 4: Modal (About)
        // 5: Settings Modal (Drone Theme)
        // 6: Fade Overlay (TOPMOST: Covers all buttons and modals during scene transitions!)
        if (bgImgTrans != null) bgImgTrans.SetSiblingIndex(0);
        if (startBtnTrans != null) startBtnTrans.SetSiblingIndex(1);
        if (settingsBtnTrans != null) settingsBtnTrans.SetSiblingIndex(2);
        if (aboutBtnTrans != null) aboutBtnTrans.SetSiblingIndex(3);
        if (aboutModalTrans != null) aboutModalTrans.SetSiblingIndex(4);
        if (settingsModalGo != null) settingsModalGo.transform.SetSiblingIndex(5);
        if (fadeOverlayTrans != null) fadeOverlayTrans.SetAsLastSibling();

        EditorSceneManager.MarkSceneDirty(canvasGo.scene);
        EditorSceneManager.SaveScene(canvasGo.scene);

        Debug.Log("<color=green><b>[StartScreenBuilder]</b></color> Successfully configured Canvas hierarchy, buttons, and verified Fade Overlay is topmost!");

        if (switchedScene && !string.IsNullOrEmpty(previousPath))
        {
            EditorSceneManager.OpenScene(previousPath);
        }
    }
}
