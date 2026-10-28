using System.IO;
using Lean.Gui;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>
/// Rebuilds the UI of the RoomScanLibrary and RoomScanDetail scenes (and the row prefab), and puts the
/// HomeHub header inside the safe area. The scenes start as copies of the shared screen chrome
/// (background, header with Back, fade overlay); everything else lives under a generated "RoomScanUI"
/// root, so the build can be re-run safely.
///
/// Look matches the tutorial screens: dark navy panels with a blue accent, Maven Pro, and the shared
/// yellow LeanGUI "Tap Button" prefab with bold capitals.
/// </summary>
public static class RoomScanSceneBuilder
{
    private const string LibraryScene = "Assets/Screens/RoomScanScreen/RoomScanLibrary.unity";
    private const string DetailScene = "Assets/Screens/RoomScanScreen/RoomScanDetail.unity";
    private const string HomeHubScene = "Assets/Screens/HomeHubScreen/HomeHub.unity";
    private const string RowPrefabPath = "Assets/Screens/RoomScanScreen/ScanRow.prefab";
    private const string ButtonPrefabPath = "Assets/Screens/TutorialSelectScreen/Buttons/Tap Button (LeanButton).prefab";
    private const string UiRoot = "RoomScanUI";
    private const string HeaderSafe = "SafeContent";

    // palette taken from the tutorial screens
    private static readonly Color Yellow = new Color(0.957f, 0.922f, 0.322f);
    private static readonly Color YellowDown = new Color(0.764f, 0.722f, 0f);
    private static readonly Color Red = new Color(0.93f, 0.36f, 0.33f);
    private static readonly Color RedDown = new Color(0.70f, 0.22f, 0.20f);
    private static readonly Color Ink = new Color(0.07f, 0.08f, 0.11f);
    private static readonly Color ScreenBg = new Color(0.09f, 0.11f, 0.16f);
    private static readonly Color DarkPanel = new Color(0.06f, 0.08f, 0.12f, 0.92f);
    private static readonly Color Card = new Color(0.13f, 0.16f, 0.22f);
    private static readonly Color CardDown = new Color(0.17f, 0.21f, 0.28f);
    private static readonly Color Field = new Color(0.18f, 0.20f, 0.25f);
    private static readonly Color Accent = new Color(0.35f, 0.75f, 1f);
    private static readonly Color Body = new Color(0.85f, 0.88f, 0.92f);
    private static readonly Color Muted = new Color(0.60f, 0.66f, 0.74f);

    private static Sprite round10, round50, shadowSprite;
    private static TMP_FontAsset font;
    private static GameObject buttonPrefab;

    // the two button sizes used on the tutorial screens: header (BACK / RESET) and main controls (PREV / NEXT / PLAY)
    private static readonly Vector2 HeaderButton = new Vector2(180f, 60f);
    private static readonly Vector2 MainButton = new Vector2(270f, 70f);

    private enum Kind { Yellow, Red, Card }

    [MenuItem("Tools/Build Room Scan Screens", priority = 30)]
    public static void BuildAll()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        LoadAssets();

        if (!File.Exists(DetailScene))
        {
            AssetDatabase.CopyAsset(LibraryScene, DetailScene);
        }

        BuildRowPrefab();
        BuildLibrary();
        BuildDetail();
        SafeAreaHomeHub();

        AssetDatabase.SaveAssets();
        Debug.Log("[RoomScanSceneBuilder] Room scan screens rebuilt.");
    }

    private static void LoadAssets()
    {
        round10 = FindSprite("Round10");
        round50 = FindSprite("Round50");
        shadowSprite = FindSprite("Round10_Blur10");
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Screens/StartScreen/Fonts/MavenPro-VariableFont_wght SDF.asset");
        buttonPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ButtonPrefabPath);
    }

    private static Sprite FindSprite(string name)
    {
        foreach (string guid in AssetDatabase.FindAssets(name + " t:Sprite"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (o is Sprite s && s.name == name) return s;
            }
        }
        Debug.LogWarning("[RoomScanSceneBuilder] Sprite not found: " + name);
        return null;
    }

    // ------------------------------------------------------------------ scenes

    /// <summary>Moves the header's children into a container that stays inside the safe area (left/right).</summary>
    private static Transform WrapHeader(Transform header)
    {
        Transform existing = header.Find(HeaderSafe);
        if (existing != null) return existing;

        RectTransform safe = NewRect(HeaderSafe, header);
        Stretch(safe);
        var fitter = safe.gameObject.AddComponent<SafeAreaFitter>();
        var so = new SerializedObject(fitter);
        so.FindProperty("horizontalOnly").boolValue = true;
        so.ApplyModifiedPropertiesWithoutUndo();

        for (int i = header.childCount - 1; i >= 0; i--)
        {
            Transform c = header.GetChild(i);
            if (c != safe) c.SetParent(safe, false);
        }
        return safe;
    }

    /// <summary>
    /// The copied Back button still calls TutorialSelectController.BackToStartScreen, which these screens
    /// no longer have. Point it at the SceneLoader's back stack instead.
    /// </summary>
    private static void WireBack(Transform headerContent, Transform canvas)
    {
        Transform back = headerContent.Find("Back Button (LeanButton)");
        var loader = canvas.GetComponentInChildren<SceneLoader>(true);
        if (back == null || loader == null)
        {
            Debug.LogWarning("[RoomScanSceneBuilder] Back button or SceneLoader missing; Back not wired.");
            return;
        }
        LeanButton button = back.GetComponent<LeanButton>();
        var so = new SerializedObject(button);
        so.FindProperty("onClick.m_PersistentCalls.m_Calls").ClearArray();
        so.ApplyModifiedPropertiesWithoutUndo();
        UnityEventTools.AddVoidPersistentListener(button.OnClick, loader.GoBack);
        EditorUtility.SetDirty(button);
    }

    private static void SafeAreaHomeHub()
    {
        Scene scene = EditorSceneManager.OpenScene(HomeHubScene, OpenSceneMode.Single);
        GameObject canvas = GameObject.Find("Canvas");
        Transform headerContent = WrapHeader(canvas.transform.Find("TopHeaderPanel"));
        WireBack(headerContent, canvas.transform);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static Transform OpenAndClean(string scenePath, string title, out Scene scene, out Transform headerContent, out Transform safeRoot)
    {
        scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        GameObject canvas = GameObject.Find("Canvas");
        Transform old = canvas.transform.Find(UiRoot);
        if (old != null) Object.DestroyImmediate(old.gameObject);
        foreach (var c in canvas.GetComponents<MonoBehaviour>())
        {
            if (c is RoomScanLibraryController || c is RoomScanDetailController) Object.DestroyImmediate(c);
        }
        Transform t = canvas.transform.Find("Grid Panel");
        if (t != null) Object.DestroyImmediate(t.gameObject);

        // dark screen background like the tutorial screens (replaces the photo)
        Image bg = canvas.transform.Find("Image").GetComponent<Image>();
        bg.sprite = null;
        bg.color = ScreenBg;

        Transform header = canvas.transform.Find("TopHeaderPanel");
        headerContent = WrapHeader(header);
        // drop header buttons generated by an earlier build (Record / Delete); Back and the title stay
        for (int i = headerContent.childCount - 1; i >= 0; i--)
        {
            string n = headerContent.GetChild(i).name;
            if (n != "TitleText" && !n.StartsWith("Back Button")) Object.DestroyImmediate(headerContent.GetChild(i).gameObject);
        }
        headerContent.Find("TitleText").GetComponent<TMP_Text>().text = title;
        WireBack(headerContent, canvas.transform);

        RectTransform root = NewRect(UiRoot, canvas.transform);
        Stretch(root);
        // Keep generated UI below the fade overlay (last sibling).
        Transform fade = canvas.transform.Find("Fade Overlay");
        root.SetSiblingIndex(fade != null ? fade.GetSiblingIndex() : canvas.transform.childCount - 1);

        RectTransform safe = NewRect("Safe", root);
        Stretch(safe);
        safe.gameObject.AddComponent<SafeAreaFitter>();
        safeRoot = safe;
        return root;
    }

    private static void BuildLibrary()
    {
        Transform root = OpenAndClean(LibraryScene, "Room Scans", out Scene scene, out Transform headerContent, out Transform safe);
        GameObject canvas = root.parent.gameObject;
        ScanRowView rowPrefab = AssetDatabase.LoadAssetAtPath<ScanRowView>(RowPrefabPath);

        LeanButton record = MakeButton("Record Button", headerContent, "REC", Kind.Yellow);
        RectTransform rrt = (RectTransform)record.transform;
        rrt.anchorMin = rrt.anchorMax = new Vector2(1f, 0.5f);
        rrt.pivot = new Vector2(0.5f, 0.5f);
        rrt.anchoredPosition = new Vector2(-180f, 0f); // same slot as RESET on the tutorial screens
        rrt.sizeDelta = HeaderButton;
        Image dot = NewImage("Dot", record.transform.Find("Cap"), new Color(0.88f, 0.12f, 0.12f), round50);
        RectTransform drt = dot.rectTransform;
        drt.anchorMin = drt.anchorMax = drt.pivot = new Vector2(0f, 0.5f);
        drt.anchoredPosition = new Vector2(20f, 0f);
        drt.sizeDelta = new Vector2(22f, 22f);
        dot.raycastTarget = false;
        ((RectTransform)record.transform.Find("Cap/Text")).anchoredPosition = new Vector2(24f, 0f);

        // list
        RectTransform listArea = NewRect("ListArea", safe);
        listArea.anchorMin = new Vector2(0f, 0f);
        listArea.anchorMax = new Vector2(1f, 0.88f);
        listArea.offsetMin = new Vector2(40f, 30f);
        listArea.offsetMax = new Vector2(-40f, -24f);

        ScrollRect scroll = NewScroll("Scroll", listArea, out RectTransform content);
        var vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 22f;
        vlg.padding = new RectOffset(0, 12, 6, 24);
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        TMP_Text empty = NewText("EmptyState", listArea, "No scans yet.\nTap REC to capture your first room.", 28, Muted, TextAlignmentOptions.Center, false);
        Stretch(empty.rectTransform);
        empty.raycastTarget = false;

        // row menu
        GameObject menuLayer = NewDim("MenuLayer", root, out Button menuBackdrop);
        Image menuPanel = NewImage("Panel", menuLayer.transform, new Color(0.11f, 0.14f, 0.20f, 1f), round10);
        Center(menuPanel.rectTransform, new Vector2(720f, 500f));
        var menuV = menuPanel.gameObject.AddComponent<VerticalLayoutGroup>();
        menuV.padding = new RectOffset(40, 40, 30, 30);
        menuV.spacing = 14f;
        menuV.childControlWidth = false; menuV.childControlHeight = true;
        menuV.childForceExpandWidth = false; menuV.childForceExpandHeight = false;
        menuV.childAlignment = TextAnchor.UpperCenter;
        TMP_Text menuTitle = NewText("Title", menuPanel.transform, "Scan", 28, Color.white, TextAlignmentOptions.Center, true);
        menuTitle.overflowMode = TextOverflowModes.Ellipsis;
        ((RectTransform)menuTitle.transform).sizeDelta = new Vector2(640f, 60f);
        AddLayout(menuTitle.gameObject, 60f);
        LeanButton mOpen = MakeButton("Open", menuPanel.transform, "OPEN", Kind.Yellow); FixedSize(mOpen, MainButton); AddLayout(mOpen.gameObject, MainButton.y);
        LeanButton mShare = MakeButton("Share", menuPanel.transform, "SHARE", Kind.Yellow); FixedSize(mShare, MainButton); AddLayout(mShare.gameObject, MainButton.y);
        LeanButton mSend = MakeButton("Send", menuPanel.transform, "SEND", Kind.Yellow); FixedSize(mSend, MainButton); AddLayout(mSend.gameObject, MainButton.y);
        LeanButton mDelete = MakeButton("Delete", menuPanel.transform, "DELETE", Kind.Red); FixedSize(mDelete, MainButton); AddLayout(mDelete.gameObject, MainButton.y);

        GameObject confirmLayer = NewConfirm(root, out TMP_Text confirmText, out LeanButton yes, out LeanButton no);
        GameObject toast = NewToast(safe, out TMP_Text toastText);

        var ctrl = canvas.AddComponent<RoomScanLibraryController>();
        var so = new SerializedObject(ctrl);
        so.FindProperty("listContent").objectReferenceValue = content;
        so.FindProperty("rowPrefab").objectReferenceValue = rowPrefab;
        so.FindProperty("scrollRect").objectReferenceValue = scroll;
        so.FindProperty("emptyState").objectReferenceValue = empty.gameObject;
        so.FindProperty("recordButton").objectReferenceValue = record;
        so.FindProperty("menuLayer").objectReferenceValue = menuLayer;
        so.FindProperty("menuTitle").objectReferenceValue = menuTitle;
        so.FindProperty("menuBackdrop").objectReferenceValue = menuBackdrop;
        so.FindProperty("menuOpen").objectReferenceValue = mOpen;
        so.FindProperty("menuShare").objectReferenceValue = mShare;
        so.FindProperty("menuSend").objectReferenceValue = mSend;
        so.FindProperty("menuSendLabel").objectReferenceValue = mSend.GetComponentInChildren<Text>();
        so.FindProperty("menuDelete").objectReferenceValue = mDelete;
        so.FindProperty("confirmLayer").objectReferenceValue = confirmLayer;
        so.FindProperty("confirmText").objectReferenceValue = confirmText;
        so.FindProperty("confirmYes").objectReferenceValue = yes;
        so.FindProperty("confirmNo").objectReferenceValue = no;
        so.FindProperty("toast").objectReferenceValue = toast;
        so.FindProperty("toastText").objectReferenceValue = toastText;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static void BuildDetail()
    {
        Transform root = OpenAndClean(DetailScene, "Scan", out Scene scene, out Transform headerContent, out Transform safe);
        GameObject canvas = root.parent.gameObject;
        TMP_Text headerTitle = headerContent.Find("TitleText").GetComponent<TMP_Text>();
        headerTitle.overflowMode = TextOverflowModes.Ellipsis;

        // left: video
        RectTransform left = NewRect("VideoColumn", safe);
        left.anchorMin = new Vector2(0f, 0f); left.anchorMax = new Vector2(0.58f, 0.88f);
        left.offsetMin = new Vector2(40f, 30f); left.offsetMax = new Vector2(-20f, -24f);

        Image videoBg = NewImage("VideoBackground", left, new Color(0.03f, 0.04f, 0.06f, 1f), round10);
        RectTransform vbrt = videoBg.rectTransform;
        vbrt.anchorMin = new Vector2(0f, 0f); vbrt.anchorMax = new Vector2(1f, 1f);
        vbrt.offsetMin = new Vector2(0f, 120f); vbrt.offsetMax = Vector2.zero;

        RectTransform videoHolder = NewRect("VideoHolder", vbrt);
        Stretch(videoHolder);
        videoHolder.offsetMin = new Vector2(8f, 8f); videoHolder.offsetMax = new Vector2(-8f, -8f);
        var fitter = new GameObject("Video", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage), typeof(AspectRatioFitter));
        fitter.transform.SetParent(videoHolder, false);
        RawImage video = fitter.GetComponent<RawImage>();
        video.color = new Color(0.03f, 0.04f, 0.06f);
        video.raycastTarget = false;
        RectTransform vrt = video.rectTransform;
        vrt.anchorMin = vrt.anchorMax = vrt.pivot = new Vector2(0.5f, 0.5f);
        var aspect = fitter.GetComponent<AspectRatioFitter>();
        aspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        aspect.aspectRatio = 16f / 9f;

        var vp = new GameObject("VideoPlayer", typeof(VideoPlayer), typeof(AudioSource));
        vp.transform.SetParent(root, false);
        vp.GetComponent<VideoPlayer>().playOnAwake = false;
        vp.GetComponent<AudioSource>().playOnAwake = false;

        // controls bar, like the tutorial's BottomControlsPanel
        RectTransform controls = NewRect("Controls", left);
        controls.anchorMin = new Vector2(0f, 0f); controls.anchorMax = new Vector2(1f, 0f);
        controls.pivot = new Vector2(0.5f, 0f);
        controls.sizeDelta = new Vector2(0f, 100f);
        Image controlsBg = NewImage("ControlsPanel", controls, DarkPanel, round10);
        Stretch(controlsBg.rectTransform);
        LeanButton play = MakeButton("Play Button", controls, "PLAY", Kind.Yellow);
        RectTransform prt = (RectTransform)play.transform;
        prt.anchorMin = prt.anchorMax = prt.pivot = new Vector2(0f, 0.5f);
        prt.anchoredPosition = new Vector2(24f, 0f);
        prt.sizeDelta = MainButton;
        TMP_Text time = NewText("Time", controls, "0:00 / 0:00", 22, Accent, TextAlignmentOptions.Right, true);
        RectTransform trt = time.rectTransform;
        trt.anchorMin = trt.anchorMax = trt.pivot = new Vector2(1f, 0.5f);
        trt.anchoredPosition = new Vector2(-24f, 0f);
        trt.sizeDelta = new Vector2(200f, 50f);
        Slider seek = NewSlider("Seek", controls);
        RectTransform srt = (RectTransform)seek.transform;
        srt.anchorMin = new Vector2(0f, 0.5f); srt.anchorMax = new Vector2(1f, 0.5f);
        srt.offsetMin = new Vector2(330f, -35f); srt.offsetMax = new Vector2(-250f, 35f);

        // right: info
        Image info = NewImage("InfoPanel", safe, DarkPanel, round10);
        RectTransform irt = info.rectTransform;
        irt.anchorMin = new Vector2(0.58f, 0f); irt.anchorMax = new Vector2(1f, 0.88f);
        irt.offsetMin = new Vector2(20f, 30f); irt.offsetMax = new Vector2(-40f, -24f);
        var v = info.gameObject.AddComponent<VerticalLayoutGroup>();
        v.padding = new RectOffset(30, 30, 24, 24);
        v.spacing = 10f;
        v.childControlWidth = v.childControlHeight = true;
        v.childForceExpandWidth = true; v.childForceExpandHeight = false;

        AddLayout(NewText("NameLabel", info.transform, "NAME", 22, Accent, TextAlignmentOptions.Left, true).gameObject, 32f);
        TMP_InputField nameInput = NewInput("NameInput", info.transform, "Scan name", false);
        AddLayout(nameInput.gameObject, 68f);
        AddLayout(NewText("NotesLabel", info.transform, "NOTES", 22, Accent, TextAlignmentOptions.Left, true).gameObject, 32f);
        TMP_InputField notesInput = NewInput("NotesInput", info.transform, "Add notes about this room", true);
        AddLayout(notesInput.gameObject, 120f);
        TMP_Text infoText = NewText("InfoText", info.transform, "", 22, Body, TextAlignmentOptions.Left, false);
        AddLayout(infoText.gameObject, 90f);

        RectTransform statusRow = NewRect("StatusRow", info.transform);
        AddLayout(statusRow.gameObject, 54f);
        Image pill = NewImage("StatusPill", statusRow, Color.gray, round10);
        Stretch(pill.rectTransform);
        TMP_Text statusText = NewText("StatusText", pill.transform, "LOCAL", 22, Ink, TextAlignmentOptions.Center, true);
        Stretch(statusText.rectTransform);
        statusText.enableAutoSizing = true; statusText.fontSizeMin = 14; statusText.fontSizeMax = 22;
        statusText.textWrappingMode = TextWrappingModes.NoWrap;
        RectTransform progressFill = NewProgress("Progress", info.transform);

        RectTransform buttons = NewButtonRow("Buttons", info.transform);
        LeanButton send = MakeButton("Send Button", buttons, "SEND", Kind.Yellow); FixedSize(send, MainButton);
        LeanButton share = MakeButton("Share Button", buttons, "SHARE", Kind.Yellow); FixedSize(share, MainButton);
        RectTransform buttons2 = NewButtonRow("Buttons2", info.transform);
        LeanButton delete = MakeButton("Delete Button", buttons2, "DELETE", Kind.Red); FixedSize(delete, MainButton);

        GameObject confirmLayer = NewConfirm(root, out TMP_Text confirmText, out LeanButton yes, out LeanButton no);
        GameObject toast = NewToast(safe, out TMP_Text toastText);

        var ctrl = canvas.AddComponent<RoomScanDetailController>();
        var so = new SerializedObject(ctrl);
        so.FindProperty("headerTitle").objectReferenceValue = headerTitle;
        so.FindProperty("deleteButton").objectReferenceValue = delete;
        so.FindProperty("videoImage").objectReferenceValue = video;
        so.FindProperty("videoAspect").objectReferenceValue = aspect;
        so.FindProperty("player").objectReferenceValue = vp.GetComponent<VideoPlayer>();
        so.FindProperty("audioSource").objectReferenceValue = vp.GetComponent<AudioSource>();
        so.FindProperty("playButton").objectReferenceValue = play;
        so.FindProperty("playLabel").objectReferenceValue = play.GetComponentInChildren<Text>();
        so.FindProperty("seekSlider").objectReferenceValue = seek;
        so.FindProperty("timeLabel").objectReferenceValue = time;
        so.FindProperty("nameInput").objectReferenceValue = nameInput;
        so.FindProperty("notesInput").objectReferenceValue = notesInput;
        so.FindProperty("infoText").objectReferenceValue = infoText;
        so.FindProperty("statusText").objectReferenceValue = statusText;
        so.FindProperty("statusPill").objectReferenceValue = pill;
        so.FindProperty("progressFill").objectReferenceValue = progressFill;
        so.FindProperty("sendButton").objectReferenceValue = send;
        so.FindProperty("sendLabel").objectReferenceValue = send.GetComponentInChildren<Text>();
        so.FindProperty("shareButton").objectReferenceValue = share;
        so.FindProperty("confirmLayer").objectReferenceValue = confirmLayer;
        so.FindProperty("confirmText").objectReferenceValue = confirmText;
        so.FindProperty("confirmYes").objectReferenceValue = yes;
        so.FindProperty("confirmNo").objectReferenceValue = no;
        so.FindProperty("toast").objectReferenceValue = toast;
        so.FindProperty("toastText").objectReferenceValue = toastText;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    // ------------------------------------------------------------------ row prefab

    private static void BuildRowPrefab()
    {
        // The row is the shared button prefab re-skinned as a card, so it presses like every other control.
        LeanButton openBtn = MakeButton("ScanRow", null, "", Kind.Card);
        GameObject go = openBtn.gameObject;
        Transform cap = go.transform.Find("Cap");
        Object.DestroyImmediate(cap.Find("Text").gameObject);
        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = 150f; le.minHeight = 150f; le.flexibleHeight = 0f;

        RawImage thumb = new GameObject("Thumb", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage)).GetComponent<RawImage>();
        thumb.transform.SetParent(cap, false);
        thumb.raycastTarget = false;
        RectTransform trt = thumb.rectTransform;
        trt.anchorMin = trt.anchorMax = trt.pivot = new Vector2(0f, 0.5f);
        trt.anchoredPosition = new Vector2(18f, 0f);
        trt.sizeDelta = new Vector2(196f, 110f);

        TMP_Text title = NewText("Title", cap, "Title", 28, Color.white, TextAlignmentOptions.BottomLeft, true);
        title.overflowMode = TextOverflowModes.Ellipsis; title.textWrappingMode = TextWrappingModes.NoWrap;
        RectTransform ttr = title.rectTransform;
        ttr.anchorMin = new Vector2(0f, 0.5f); ttr.anchorMax = new Vector2(1f, 1f);
        ttr.offsetMin = new Vector2(240f, 0f); ttr.offsetMax = new Vector2(-420f, -14f);

        TMP_Text sub = NewText("Sub", cap, "Sub", 19, Muted, TextAlignmentOptions.TopLeft, false);
        sub.overflowMode = TextOverflowModes.Ellipsis; sub.textWrappingMode = TextWrappingModes.NoWrap;
        RectTransform srt = sub.rectTransform;
        srt.anchorMin = new Vector2(0f, 0f); srt.anchorMax = new Vector2(1f, 0.5f);
        srt.offsetMin = new Vector2(240f, 18f); srt.offsetMax = new Vector2(-420f, 0f);

        Image pill = NewImage("StatusPill", cap, Color.gray, round10);
        pill.raycastTarget = false;
        RectTransform prt = pill.rectTransform;
        prt.anchorMin = prt.anchorMax = prt.pivot = new Vector2(1f, 0.5f);
        prt.anchoredPosition = new Vector2(-130f, 0f);
        prt.sizeDelta = new Vector2(270f, 54f);
        TMP_Text status = NewText("Text", pill.transform, "LOCAL", 22, Ink, TextAlignmentOptions.Center, true);
        Stretch(status.rectTransform);
        status.rectTransform.offsetMin = new Vector2(8f, 0f);
        status.rectTransform.offsetMax = new Vector2(-8f, 0f);
        status.enableAutoSizing = true; status.fontSizeMin = 12; status.fontSizeMax = 22;
        status.textWrappingMode = TextWrappingModes.NoWrap;

        LeanButton menu = MakeButton("MenuButton", cap, "...", Kind.Yellow);
        RectTransform mrt = (RectTransform)menu.transform;
        mrt.anchorMin = mrt.anchorMax = mrt.pivot = new Vector2(1f, 0.5f);
        mrt.anchoredPosition = new Vector2(-22f, 0f);
        mrt.sizeDelta = new Vector2(MainButton.y, MainButton.y);

        // upload progress strip along the bottom edge of the card
        Image track = NewImage("Progress", cap, Field, null);
        track.raycastTarget = false;
        RectTransform track_rt = track.rectTransform;
        track_rt.anchorMin = new Vector2(0f, 0f); track_rt.anchorMax = new Vector2(1f, 0f);
        track_rt.pivot = new Vector2(0.5f, 0f);
        track_rt.sizeDelta = new Vector2(-36f, 8f);
        track_rt.anchoredPosition = new Vector2(0f, 8f);
        Image fill = NewImage("Fill", track.transform, Accent, null);
        fill.raycastTarget = false;
        RectTransform frt = fill.rectTransform;
        frt.anchorMin = Vector2.zero; frt.anchorMax = new Vector2(0f, 1f);
        frt.offsetMin = frt.offsetMax = Vector2.zero;

        var view = go.AddComponent<ScanRowView>();
        var so = new SerializedObject(view);
        so.FindProperty("openButton").objectReferenceValue = openBtn;
        so.FindProperty("menuButton").objectReferenceValue = menu;
        so.FindProperty("thumb").objectReferenceValue = thumb;
        so.FindProperty("titleText").objectReferenceValue = title;
        so.FindProperty("subText").objectReferenceValue = sub;
        so.FindProperty("statusText").objectReferenceValue = status;
        so.FindProperty("statusPill").objectReferenceValue = pill;
        so.FindProperty("progressFill").objectReferenceValue = frt;
        so.ApplyModifiedPropertiesWithoutUndo();

        // standalone prefab, not a variant of the shared button prefab
        PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        PrefabUtility.SaveAsPrefabAsset(go, RowPrefabPath);
        Object.DestroyImmediate(go);
    }

    // ------------------------------------------------------------------ widgets

    private static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private static Image NewImage(string name, Transform parent, Color color, Sprite sprite)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = color;
        img.sprite = sprite;
        if (sprite != null) img.type = Image.Type.Sliced;
        return img;
    }

    private static TMP_Text NewText(string name, Transform parent, string text, float size, Color color, TextAlignmentOptions align, bool bold)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<TextMeshProUGUI>();
        t.font = font;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
        t.raycastTarget = false;
        return t;
    }

    /// <summary>Instantiates the shared yellow LeanGUI "Tap Button" prefab (shadow, cap, bold capitals).</summary>
    private static LeanButton MakeButton(string name, Transform parent, string text, Kind kind)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(buttonPrefab);
        if (parent != null) go.transform.SetParent(parent, false);
        go.name = name;

        Color normal = kind == Kind.Red ? Red : kind == Kind.Card ? Card : Yellow;
        Color down = kind == Kind.Red ? RedDown : kind == Kind.Card ? CardDown : YellowDown;

        Image cap = go.transform.Find("Cap").GetComponent<Image>();
        cap.color = normal;
        foreach (var c in go.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (c.GetType().Name != "LeanGraphicColor") continue;
            var so = new SerializedObject(c);
            SerializedProperty value = so.FindProperty("Data.Value");
            if (value == null) continue;
            value.colorValue = c.transform.name.Contains("Down") ? down : normal;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // label keeps the prefab's font, size (64) and bold caps, exactly like BACK / RESET on the tutorial screens
        cap.transform.Find("Text").GetComponent<Text>().text = text;

        var theme = go.AddComponent<ButtonTheme>();
        var tso = new SerializedObject(theme);
        tso.FindProperty("cap").objectReferenceValue = cap;
        tso.FindProperty("normal").colorValue = normal;
        tso.ApplyModifiedPropertiesWithoutUndo();

        return go.GetComponent<LeanButton>();
    }

    private static ScrollRect NewScroll(string name, Transform parent, out RectTransform content)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(ScrollRect), typeof(RectMask2D));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
        Stretch((RectTransform)go.transform);
        content = NewRect("Content", go.transform);
        content.anchorMin = new Vector2(0f, 1f); content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.sizeDelta = Vector2.zero;
        var sr = go.GetComponent<ScrollRect>();
        sr.content = content;
        sr.viewport = (RectTransform)go.transform;
        sr.horizontal = false; sr.vertical = true;
        sr.movementType = ScrollRect.MovementType.Elastic;
        sr.scrollSensitivity = 40f;
        return sr;
    }

    /// <summary>Matches the tutorial timeline: dark track, blue fill, white block handle, generous touch area.</summary>
    private static Slider NewSlider(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Slider));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f); // touch hit area

        Image bg = NewImage("Background", go.transform, Field, null);
        bg.raycastTarget = false;
        bg.rectTransform.anchorMin = new Vector2(0f, 0.5f); bg.rectTransform.anchorMax = new Vector2(1f, 0.5f);
        bg.rectTransform.sizeDelta = new Vector2(0f, 15f);
        RectTransform fillArea = NewRect("Fill Area", go.transform);
        fillArea.anchorMin = new Vector2(0f, 0.5f); fillArea.anchorMax = new Vector2(1f, 0.5f);
        fillArea.sizeDelta = new Vector2(-28f, 15f);
        Image fill = NewImage("Fill", fillArea, Accent, null);
        fill.raycastTarget = false;
        fill.rectTransform.anchorMin = Vector2.zero; fill.rectTransform.anchorMax = new Vector2(0f, 1f);
        fill.rectTransform.sizeDelta = new Vector2(14f, 0f);
        RectTransform handleArea = NewRect("Handle Slide Area", go.transform);
        Stretch(handleArea);
        handleArea.offsetMin = new Vector2(14f, 0f); handleArea.offsetMax = new Vector2(-14f, 0f);
        Image handle = NewImage("Handle", handleArea, Color.white, null);
        handle.rectTransform.sizeDelta = new Vector2(28f, 0f);
        handle.rectTransform.anchorMin = new Vector2(0f, 0.15f); handle.rectTransform.anchorMax = new Vector2(0f, 0.85f);
        var s = go.GetComponent<Slider>();
        s.fillRect = fill.rectTransform;
        s.handleRect = handle.rectTransform;
        s.targetGraphic = handle;
        s.direction = Slider.Direction.LeftToRight;
        s.transition = Selectable.Transition.None;
        s.minValue = 0f; s.maxValue = 1f; s.value = 0f;
        return s;
    }

    private static RectTransform NewProgress(string name, Transform parent)
    {
        Image track = NewImage(name, parent, Field, null);
        AddLayout(track.gameObject, 14f);
        Image fill = NewImage("Fill", track.transform, Accent, null);
        RectTransform frt = fill.rectTransform;
        frt.anchorMin = Vector2.zero; frt.anchorMax = new Vector2(0f, 1f);
        frt.offsetMin = frt.offsetMax = Vector2.zero;
        return frt;
    }

    private static TMP_InputField NewInput(string name, Transform parent, string placeholder, bool multiline)
    {
        Image bg = NewImage(name, parent, Field, round10);
        var input = bg.gameObject.AddComponent<TMP_InputField>();
        RectTransform area = NewRect("Text Area", bg.transform);
        Stretch(area);
        area.offsetMin = new Vector2(16f, 8f); area.offsetMax = new Vector2(-16f, -8f);
        area.gameObject.AddComponent<RectMask2D>();
        TextAlignmentOptions align = multiline ? TextAlignmentOptions.TopLeft : TextAlignmentOptions.Left;
        TMP_Text ph = NewText("Placeholder", area, placeholder, 24, new Color(1f, 1f, 1f, 0.35f), align, false);
        ph.fontStyle = FontStyles.Italic;
        Stretch(ph.rectTransform);
        TMP_Text txt = NewText("Text", area, "", 24, Color.white, align, false);
        txt.raycastTarget = true;
        Stretch(txt.rectTransform);
        input.textViewport = area;
        input.textComponent = txt;
        input.placeholder = ph;
        input.targetGraphic = bg;
        input.lineType = multiline ? TMP_InputField.LineType.MultiLineNewline : TMP_InputField.LineType.SingleLine;
        input.characterLimit = multiline ? 500 : 60;
        input.pointSize = 24;
        input.shouldHideMobileInput = true;
        input.onFocusSelectAll = false; // select-all on focus hides the caret
        input.caretBlinkRate = 0.85f;
        input.caretWidth = 3;
        input.customCaretColor = true;
        input.caretColor = Accent;
        input.selectionColor = new Color(Accent.r, Accent.g, Accent.b, 0.35f);
        return input;
    }

    private static GameObject NewDim(string name, Transform parent, out Button backdrop)
    {
        Image dim = NewImage(name, parent, new Color(0f, 0f, 0f, 0.62f), null);
        Stretch(dim.rectTransform);
        backdrop = dim.gameObject.AddComponent<Button>();
        backdrop.targetGraphic = dim;
        backdrop.transition = Selectable.Transition.None;
        return dim.gameObject;
    }

    private static GameObject NewConfirm(Transform root, out TMP_Text message, out LeanButton yes, out LeanButton no)
    {
        GameObject layer = NewDim("ConfirmLayer", root, out _);
        Image panel = NewImage("Panel", layer.transform, new Color(0.11f, 0.14f, 0.20f, 1f), round10);
        Center(panel.rectTransform, new Vector2(860f, 420f));
        message = NewText("Message", panel.transform, "Delete?", 28, Body, TextAlignmentOptions.Center, false);
        RectTransform mrt = message.rectTransform;
        mrt.anchorMin = new Vector2(0f, 0.38f); mrt.anchorMax = Vector2.one;
        mrt.offsetMin = new Vector2(40f, 0f); mrt.offsetMax = new Vector2(-40f, -30f);
        no = MakeButton("Cancel", panel.transform, "CANCEL", Kind.Yellow);
        yes = MakeButton("Delete", panel.transform, "DELETE", Kind.Red);
        RectTransform nrt = (RectTransform)no.transform, yrt = (RectTransform)yes.transform;
        nrt.anchorMin = nrt.anchorMax = new Vector2(0.27f, 0.2f); nrt.sizeDelta = MainButton;
        yrt.anchorMin = yrt.anchorMax = new Vector2(0.73f, 0.2f); yrt.sizeDelta = MainButton;
        return layer;
    }

    private static GameObject NewToast(Transform parent, out TMP_Text text)
    {
        Image pill = NewImage("Toast", parent, new Color(0.06f, 0.08f, 0.12f, 0.96f), round10);
        pill.raycastTarget = false;
        RectTransform rt = pill.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 60f);
        rt.sizeDelta = new Vector2(900f, 76f);
        text = NewText("Text", pill.transform, "", 22, Accent, TextAlignmentOptions.Center, true);
        Stretch(text.rectTransform);
        text.overflowMode = TextOverflowModes.Ellipsis;
        return pill.gameObject;
    }

    private static void FixedSize(LeanButton b, Vector2 size)
    {
        ((RectTransform)b.transform).sizeDelta = size;
    }

    /// <summary>A left-aligned row of fixed-size buttons inside a vertical layout.</summary>
    private static RectTransform NewButtonRow(string name, Transform parent)
    {
        RectTransform row = NewRect(name, parent);
        AddLayout(row.gameObject, MainButton.y + 12f);
        var h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        h.spacing = 24f;
        h.childAlignment = TextAnchor.MiddleLeft;
        h.childControlWidth = h.childControlHeight = false;
        h.childForceExpandWidth = h.childForceExpandHeight = false;
        return row;
    }

    private static void AddLayout(GameObject go, float preferredHeight)
    {
        var le = go.GetComponent<LayoutElement>();
        if (le == null) le = go.AddComponent<LayoutElement>();
        le.preferredHeight = preferredHeight;
        le.minHeight = preferredHeight;
        le.flexibleHeight = 0f;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    private static void Center(RectTransform rt, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = size;
    }
}
