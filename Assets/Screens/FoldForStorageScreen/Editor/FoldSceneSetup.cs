using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using TMPro;
using Lean.Gui;

[InitializeOnLoad]
public static class FoldSceneSetup
{
    private const string FoldScenePath = "Assets/Screens/FoldForStorageScreen/FoldForStorage.unity";
    private const string SelectScenePath = "Assets/Screens/TutorialSelectScreen/TutorialSelectScreen.unity";
    private const string GhostMatPath = "Assets/Materials/M_TutorialGhost.mat";
    private const string InputActionsPath = "Assets/InputSystem_Actions.inputactions";
    private const string FontPath = "Assets/Screens/StartScreen/Fonts/MavenPro-VariableFont_wght SDF.asset";

    static FoldSceneSetup()
    {
        EditorApplication.delayCall += AutoCheck;
    }

    private static void AutoCheck()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.path == FoldScenePath)
        {
            if (GameObject.Find("FoldTutorialCanvas") == null || GameObject.Find("TutorialController") == null)
            {
                ExecuteFoldSceneSetup();
            }
        }
    }

    [MenuItem("Tools/Setup Fold For Storage Scene")]
    public static void ExecuteFoldSceneSetup()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.path != FoldScenePath)
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                EditorSceneManager.OpenScene(FoldScenePath);
            }
            else
            {
                Debug.LogWarning("[FoldSceneSetup] Open cancelled.");
                return;
            }
        }

        Debug.Log("[FoldSceneSetup] Configuring Fold For Storage scene...");

        // 1. Locate TutorialRigRoot
        GameObject rigRootGo = GameObject.Find("TutorialRigRoot");
        if (rigRootGo == null)
        {
            Debug.LogError("[FoldSceneSetup] TutorialRigRoot not found!");
            return;
        }

        // Configure TutorialPlayer on TutorialRigRoot
        TutorialPlayer player = rigRootGo.GetComponent<TutorialPlayer>();
        if (player == null) player = rigRootGo.AddComponent<TutorialPlayer>();
        player.rigRoot = rigRootGo.transform;
        player.animator = rigRootGo.GetComponent<Animator>();
        player.placement = TutorialPlayer.PlacementMode.DroneAnchored;
        player.autoPlay = false;
        player.loop = true;
        player.speed = 1f;

        // Configure TutorialGhostSkin on TutorialRigRoot
        TutorialGhostSkin ghostSkin = rigRootGo.GetComponent<TutorialGhostSkin>();
        if (ghostSkin == null) ghostSkin = rigRootGo.AddComponent<TutorialGhostSkin>();
        ghostSkin.hologramMaterial = AssetDatabase.LoadAssetAtPath<Material>(GhostMatPath);
        ghostSkin.ghostRoots = new List<Transform>();
        string[] rootNames = { "Ghost_HandRight", "Ghost_HandLeft", "Ghost_Arm_BackRight", "Ghost_Drone", "Ghost_Battery_Upper" };
        foreach (var rName in rootNames)
        {
            Transform t = rigRootGo.transform.Find(rName);
            if (t != null) ghostSkin.ghostRoots.Add(t);
        }
        ghostSkin.applyOnAwake = true;
        ghostSkin.skipHands = false;
        ghostSkin.ApplySkin();

        // 2. Camera and Lighting
        SetupCameraAndLighting();

        // 3. EventSystem with InputSystemUIInputModule
        SetupEventSystem();

        // 4. Setup TutorialController & Manager
        FoldTutorialManager manager = SetupTutorialController(player);

        // 5. Setup Canvas UI
        SetupCanvasUI(manager);

        // 6. Populate default 26 steps
        manager.PopulateDefaultSteps();

        // 7. Save Scene
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();

        Debug.Log("[FoldSceneSetup] Fold For Storage scene successfully setup and saved!");
    }

    [MenuItem("Tools/Wire Tutorial Select Screen Buttons")]
    public static void ExecuteWireTutorialSelect()
    {
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            EditorSceneManager.OpenScene(SelectScenePath);
        }
        else
        {
            return;
        }

        GameObject canvasGo = GameObject.Find("Canvas");
        if (canvasGo == null)
        {
            Debug.LogError("[FoldSceneSetup] Canvas not found in TutorialSelectScreen!");
            return;
        }

        TutorialSelectController ctrl = canvasGo.GetComponent<TutorialSelectController>();
        if (ctrl == null) ctrl = canvasGo.AddComponent<TutorialSelectController>();

        LeanButton[] buttons = UnityEngine.Object.FindObjectsByType<LeanButton>(FindObjectsSortMode.None);
        int wiredCount = 0;
        foreach (var btn in buttons)
        {
            TMP_Text tmp = btn.GetComponentInChildren<TMP_Text>();
            Text txt = btn.GetComponentInChildren<Text>();
            string btnText = tmp != null ? tmp.text : (txt != null ? txt.text : "");

            if (btnText.Contains("FOLD") && btnText.Contains("STORAGE"))
            {
                // Clear and add persistent call to OpenFoldForStorage
                SerializedObject so = new SerializedObject(btn);
                SerializedProperty prop = so.FindProperty("onClick");
                so.ApplyModifiedProperties();

                // Use UnityEventTools
                UnityEventTools.RemovePersistentListener(btn.OnClick, ctrl.OpenFoldForStorage);
                UnityEventTools.AddPersistentListener(btn.OnClick, ctrl.OpenFoldForStorage);
                wiredCount++;
                Debug.Log("[FoldSceneSetup] Wired 'FOLD FOR STORAGE' button!");
            }
            else if (btnText.Contains("DEPLOY") && btnText.Contains("FLIGHT"))
            {
                UnityEventTools.RemovePersistentListener(btn.OnClick, ctrl.OpenDeployForFlight);
                UnityEventTools.AddPersistentListener(btn.OnClick, ctrl.OpenDeployForFlight);
                wiredCount++;
                Debug.Log("[FoldSceneSetup] Wired 'DEPLOY FOR FLIGHT' button!");
            }
        }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        Debug.Log($"[FoldSceneSetup] Wired {wiredCount} buttons in TutorialSelectScreen and saved!");
    }

    private static void SetupCameraAndLighting()
    {
        Camera[] cams = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
        foreach (var c in cams)
        {
            Undo.DestroyObjectImmediate(c.gameObject);
        }

        GameObject camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        Camera cam = camGo.AddComponent<Camera>();
        camGo.AddComponent<AudioListener>();

        cam.transform.position = new Vector3(0f, 1.2f, -2.5f);
        cam.transform.rotation = Quaternion.Euler(25f, 0f, 0f);
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.10f, 0.12f, 0.16f, 1f);
        cam.fieldOfView = 55f;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 100f;

        Light[] lights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
        foreach (var l in lights)
        {
            Undo.DestroyObjectImmediate(l.gameObject);
        }

        GameObject lightGo = new GameObject("Directional Light");
        Light light = lightGo.AddComponent<Light>();
        light.type = LightType.Directional;
        light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        light.color = new Color(1f, 0.96f, 0.9f);
        light.intensity = 1.2f;
    }

    private static void SetupEventSystem()
    {
        EventSystem[] esList = UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None);
        foreach (var es in esList)
        {
            Undo.DestroyObjectImmediate(es.gameObject);
        }

        GameObject esGo = new GameObject("EventSystem");
        esGo.AddComponent<EventSystem>();
        var module = esGo.AddComponent<InputSystemUIInputModule>();
        var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
        if (actions != null)
        {
            module.actionsAsset = actions;
        }
    }

    private static FoldTutorialManager SetupTutorialController(TutorialPlayer player)
    {
        GameObject ctrlGo = GameObject.Find("TutorialController");
        if (ctrlGo != null)
        {
            Undo.DestroyObjectImmediate(ctrlGo);
        }

        ctrlGo = new GameObject("TutorialController");
        FoldTutorialManager manager = ctrlGo.AddComponent<FoldTutorialManager>();

        SerializedObject so = new SerializedObject(manager);
        so.FindProperty("player").objectReferenceValue = player;
        so.FindProperty("selectScreenSceneName").stringValue = "TutorialSelectScreen";
        so.ApplyModifiedProperties();

        return manager;
    }

    private static void SetupCanvasUI(FoldTutorialManager manager)
    {
        GameObject oldCanvas = GameObject.Find("FoldTutorialCanvas");
        if (oldCanvas != null)
        {
            Undo.DestroyObjectImmediate(oldCanvas);
        }

        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

        // Canvas
        GameObject canvasGo = new GameObject("FoldTutorialCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        // --- TOP PANEL (Header) ---
        GameObject topPanel = CreateUIObject("TopHeaderPanel", canvasGo.transform);
        RectTransform topRt = topPanel.GetComponent<RectTransform>();
        SetStretch(topRt, 0f, 0.82f, 1f, 1f, 0f, 0f, 0f, 0f);
        Image topBg = topPanel.AddComponent<Image>();
        topBg.color = new Color(0.06f, 0.08f, 0.12f, 0.92f);

        // Back Button
        Button backBtn = CreateButton("BackButton", topPanel.transform, "BACK", font, 20);
        RectTransform bRt = backBtn.GetComponent<RectTransform>();
        SetAnchor(bRt, 0f, 0.5f, 0f, 0.5f, new Vector2(90, 0), new Vector2(130, 48));
        SetButtonColor(backBtn, new Color(0.25f, 0.28f, 0.35f, 1f));
        UnityEventTools.AddPersistentListener(backBtn.onClick, manager.BackToTutorialSelect);

        // Step Counter Text
        TextMeshProUGUI stepCountText = CreateText("StepCounterText", topPanel.transform, "STEP 1 / 26", font, 20, TextAlignmentOptions.Center);
        RectTransform scRt = stepCountText.GetComponent<RectTransform>();
        SetAnchor(scRt, 0.5f, 0.85f, 0.5f, 0.85f, Vector2.zero, new Vector2(400, 30));
        stepCountText.color = new Color(0.35f, 0.75f, 1f, 1f);

        // Title Text
        TextMeshProUGUI titleText = CreateText("TitleText", topPanel.transform, "Release the battery", font, 28, TextAlignmentOptions.Center);
        RectTransform tRt = titleText.GetComponent<RectTransform>();
        SetAnchor(tRt, 0.5f, 0.55f, 0.5f, 0.55f, Vector2.zero, new Vector2(1000, 42));
        titleText.fontStyle = FontStyles.Bold;
        titleText.color = Color.white;

        // Instruction Text
        TextMeshProUGUI instText = CreateText("InstructionText", topPanel.transform, "Grab the battery handle and rotate it until the latch releases.", font, 19, TextAlignmentOptions.Center);
        RectTransform iRt = instText.GetComponent<RectTransform>();
        SetAnchor(iRt, 0.5f, 0.22f, 0.5f, 0.22f, Vector2.zero, new Vector2(1200, 36));
        instText.color = new Color(0.85f, 0.88f, 0.92f, 1f);

        // --- BOTTOM PANEL (Controls) ---
        GameObject botPanel = CreateUIObject("BottomControlsPanel", canvasGo.transform);
        RectTransform botRt = botPanel.GetComponent<RectTransform>();
        SetStretch(botRt, 0f, 0f, 1f, 0.18f, 0f, 0f, 0f, 0f);
        Image botBg = botPanel.AddComponent<Image>();
        botBg.color = new Color(0.06f, 0.08f, 0.12f, 0.92f);

        // Timeline Slider
        GameObject sliderGo = CreateSlider("TimelineSlider", botPanel.transform);
        Slider slider = sliderGo.GetComponent<Slider>();
        RectTransform sRt = sliderGo.GetComponent<RectTransform>();
        SetAnchor(sRt, 0.5f, 0.72f, 0.5f, 0.72f, Vector2.zero, new Vector2(1400, 30));

        EventTrigger trigger = sliderGo.AddComponent<EventTrigger>();
        AddEventTrigger(trigger, EventTriggerType.PointerDown, manager.OnSliderPointerDown);
        AddEventTrigger(trigger, EventTriggerType.PointerUp, manager.OnSliderPointerUp);

        // Buttons Row
        GameObject btnRow = CreateUIObject("ButtonRow", botPanel.transform);
        RectTransform brRt = btnRow.GetComponent<RectTransform>();
        SetAnchor(brRt, 0.5f, 0.32f, 0.5f, 0.32f, Vector2.zero, new Vector2(850, 56));

        HorizontalLayoutGroup hlg = btnRow.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 30f;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;

        Button prevBtn = CreateButton("PrevButton", btnRow.transform, "PREV", font, 22);
        SetRectSize(prevBtn.GetComponent<RectTransform>(), 170, 52);
        SetButtonColor(prevBtn, new Color(0.18f, 0.45f, 0.8f, 1f));
        UnityEventTools.AddPersistentListener(prevBtn.onClick, manager.PreviousStep);

        Button playPauseBtn = CreateButton("PlayPauseButton", btnRow.transform, "PLAY", font, 22);
        SetRectSize(playPauseBtn.GetComponent<RectTransform>(), 170, 52);
        SetButtonColor(playPauseBtn, new Color(0.2f, 0.65f, 0.35f, 1f));
        UnityEventTools.AddPersistentListener(playPauseBtn.onClick, manager.TogglePlayPause);
        TextMeshProUGUI playPauseText = playPauseBtn.GetComponentInChildren<TextMeshProUGUI>();

        Button replayBtn = CreateButton("ReplayButton", btnRow.transform, "REPLAY", font, 22);
        SetRectSize(replayBtn.GetComponent<RectTransform>(), 170, 52);
        SetButtonColor(replayBtn, new Color(0.3f, 0.35f, 0.45f, 1f));
        UnityEventTools.AddPersistentListener(replayBtn.onClick, manager.ReplayCurrentStep);

        Button nextBtn = CreateButton("NextButton", btnRow.transform, "NEXT", font, 22);
        SetRectSize(nextBtn.GetComponent<RectTransform>(), 170, 52);
        SetButtonColor(nextBtn, new Color(0.18f, 0.45f, 0.8f, 1f));
        UnityEventTools.AddPersistentListener(nextBtn.onClick, manager.NextStep);

        // Bind all properties on manager
        SerializedObject so = new SerializedObject(manager);
        so.FindProperty("titleText").objectReferenceValue = titleText;
        so.FindProperty("instructionText").objectReferenceValue = instText;
        so.FindProperty("stepCounterText").objectReferenceValue = stepCountText;
        so.FindProperty("playPauseButtonText").objectReferenceValue = playPauseText;
        so.FindProperty("timelineSlider").objectReferenceValue = slider;
        so.FindProperty("prevButton").objectReferenceValue = prevBtn;
        so.FindProperty("nextButton").objectReferenceValue = nextBtn;
        so.ApplyModifiedProperties();
    }

    private static GameObject CreateUIObject(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    private static TextMeshProUGUI CreateText(string name, Transform parent, string content, TMP_FontAsset font, float size, TextAlignmentOptions align)
    {
        GameObject go = CreateUIObject(name, parent);
        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = content;
        if (font != null) tmp.font = font;
        tmp.fontSize = size;
        tmp.alignment = align;
        tmp.raycastTarget = false;
        return tmp;
    }

    private static Button CreateButton(string name, Transform parent, string label, TMP_FontAsset font, float fontSize)
    {
        GameObject go = CreateUIObject(name, parent);
        Image img = go.AddComponent<Image>();
        img.color = new Color(0.2f, 0.2f, 0.25f, 1f);

        Button btn = go.AddComponent<Button>();
        btn.targetGraphic = img;

        TextMeshProUGUI txt = CreateText("Label", go.transform, label, font, fontSize, TextAlignmentOptions.Center);
        RectTransform trt = txt.GetComponent<RectTransform>();
        SetStretch(trt, 0f, 0f, 1f, 1f, 0f, 0f, 0f, 0f);

        return btn;
    }

    private static GameObject CreateSlider(string name, Transform parent)
    {
        GameObject sliderGo = CreateUIObject(name, parent);
        Slider slider = sliderGo.AddComponent<Slider>();

        GameObject bgGo = CreateUIObject("Background", sliderGo.transform);
        Image bgImg = bgGo.AddComponent<Image>();
        bgImg.color = new Color(0.18f, 0.2f, 0.25f, 1f);
        SetStretch(bgGo.GetComponent<RectTransform>(), 0f, 0.25f, 1f, 0.75f, 0f, 0f, 0f, 0f);

        GameObject fillArea = CreateUIObject("Fill Area", sliderGo.transform);
        SetStretch(fillArea.GetComponent<RectTransform>(), 0f, 0.25f, 1f, 0.75f, 5f, 0f, -5f, 0f);

        GameObject fill = CreateUIObject("Fill", fillArea.transform);
        Image fillImg = fill.AddComponent<Image>();
        fillImg.color = new Color(0.3f, 0.7f, 1f, 1f);
        SetStretch(fill.GetComponent<RectTransform>(), 0f, 0f, 0f, 1f, 0f, 0f, 0f, 0f);

        GameObject handleArea = CreateUIObject("Handle Slide Area", sliderGo.transform);
        SetStretch(handleArea.GetComponent<RectTransform>(), 0f, 0f, 1f, 1f, 10f, 0f, -10f, 0f);

        GameObject handle = CreateUIObject("Handle", handleArea.transform);
        Image handleImg = handle.AddComponent<Image>();
        handleImg.color = Color.white;
        RectTransform hRt = handle.GetComponent<RectTransform>();
        hRt.sizeDelta = new Vector2(28, 28);

        slider.fillRect = fill.GetComponent<RectTransform>();
        slider.handleRect = hRt;
        slider.targetGraphic = handleImg;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 1f;

        return sliderGo;
    }

    private static void AddEventTrigger(EventTrigger trigger, EventTriggerType type, UnityEngine.Events.UnityAction action)
    {
        EventTrigger.Entry entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener((data) => action.Invoke());
        trigger.triggers.Add(entry);
    }

    private static void SetStretch(RectTransform rt, float minX, float minY, float maxX, float maxY, float left, float bottom, float right, float top)
    {
        rt.anchorMin = new Vector2(minX, minY);
        rt.anchorMax = new Vector2(maxX, maxY);
        rt.offsetMin = new Vector2(left, bottom);
        rt.offsetMax = new Vector2(right, top);
    }

    private static void SetAnchor(RectTransform rt, float minX, float minY, float maxX, float maxY, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = new Vector2(minX, minY);
        rt.anchorMax = new Vector2(maxX, maxY);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    private static void SetRectSize(RectTransform rt, float width, float height)
    {
        rt.sizeDelta = new Vector2(width, height);
    }

    private static void SetButtonColor(Button btn, Color normal)
    {
        ColorBlock cb = btn.colors;
        cb.normalColor = normal;
        cb.highlightedColor = normal * 1.15f;
        cb.pressedColor = normal * 0.85f;
        cb.selectedColor = normal;
        btn.colors = cb;
    }
}
