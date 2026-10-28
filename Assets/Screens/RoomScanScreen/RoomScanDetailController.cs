using System.Collections;
using Lean.Gui;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>Plays one scan and lets the user edit its info, send it to the server, share or delete it.</summary>
public class RoomScanDetailController : MonoBehaviour
{
    [Header("Header")]
    [SerializeField] private TMP_Text headerTitle;
    [SerializeField] private LeanButton deleteButton;

    [Header("Player")]
    [SerializeField] private RawImage videoImage;
    [SerializeField] private AspectRatioFitter videoAspect;
    [SerializeField] private VideoPlayer player;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private LeanButton playButton;
    [SerializeField] private Text playLabel;
    [SerializeField] private Slider seekSlider;
    [SerializeField] private TMP_Text timeLabel;

    [Header("Info")]
    [SerializeField] private TMP_InputField nameInput;
    [SerializeField] private TMP_InputField notesInput;
    [SerializeField] private TMP_Text infoText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private Image statusPill;
    [SerializeField] private RectTransform progressFill;
    [SerializeField] private LeanButton sendButton;
    [SerializeField] private Text sendLabel;
    [SerializeField] private LeanButton shareButton;

    [Header("Delete confirmation")]
    [SerializeField] private GameObject confirmLayer;
    [SerializeField] private TMP_Text confirmText;
    [SerializeField] private LeanButton confirmYes;
    [SerializeField] private LeanButton confirmNo;

    [Header("Toast")]
    [SerializeField] private GameObject toast;
    [SerializeField] private TMP_Text toastText;

    private ScanRecord record;
    private RenderTexture videoTexture;
    private Texture2D posterTexture;
    private bool prepared;
    private bool deleted;

    private void Awake()
    {
        record = ScanRepository.Get(ScanSession.SelectedId);
        confirmLayer.SetActive(false);
        toast.SetActive(false);

        deleteButton.OnClick.AddListener(() => { confirmText.text = "Delete \"" + record.title + "\"?\nThe video will be removed from this device."; confirmLayer.SetActive(true); });
        confirmNo.OnClick.AddListener(() => confirmLayer.SetActive(false));
        confirmYes.OnClick.AddListener(OnDelete);
        playButton.OnClick.AddListener(TogglePlay);
        seekSlider.onValueChanged.AddListener(OnSeek);
        nameInput.onEndEdit.AddListener(_ => SaveInfo());
        notesInput.onEndEdit.AddListener(_ => SaveInfo());
        sendButton.OnClick.AddListener(OnSend);
        shareButton.OnClick.AddListener(OnShare);
    }

    private void Start()
    {
        if (record == null)
        {
            // Nothing to show (e.g. opened directly in the editor): go back to the list.
            var loader = FindFirstObjectByType<SceneLoader>();
            if (loader != null) loader.GoBack();
            return;
        }

        headerTitle.text = record.title;
        nameInput.SetTextWithoutNotify(record.title);
        notesInput.SetTextWithoutNotify(record.notes);
        LoadPoster();
        SetupPlayer();
        RefreshStatus();
    }

    private void OnEnable()
    {
        ScanRepository.Changed += RefreshStatus;
        SceneLoader.BackHandler = HandleBack;
    }

    private void OnDisable()
    {
        ScanRepository.Changed -= RefreshStatus;
        if (SceneLoader.BackHandler == (System.Func<bool>)HandleBack) SceneLoader.BackHandler = null;
        SaveInfo();
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused) SaveInfo();
    }

    private void OnDestroy()
    {
        if (videoTexture != null) { videoTexture.Release(); Destroy(videoTexture); }
        if (posterTexture != null) Destroy(posterTexture);
    }

    private bool HandleBack()
    {
        if (confirmLayer.activeSelf) { confirmLayer.SetActive(false); return true; }
        return false;
    }

    // ---------------------------------------------------------------- player

    private void LoadPoster()
    {
        string thumb = ScanRepository.ThumbPath(record);
        if (string.IsNullOrEmpty(thumb) || !System.IO.File.Exists(thumb))
        {
            videoImage.color = new Color(0.08f, 0.09f, 0.12f);
            return;
        }
        var tex = new Texture2D(2, 2, TextureFormat.RGB24, false);
        if (tex.LoadImage(System.IO.File.ReadAllBytes(thumb)))
        {
            posterTexture = tex;
            videoImage.texture = tex;
            videoImage.color = Color.white;
            videoAspect.aspectRatio = (float)tex.width / tex.height;
        }
        else
        {
            Destroy(tex);
        }
    }

    private void SetupPlayer()
    {
        player.playOnAwake = false;
        player.source = VideoSource.Url;
        player.url = "file://" + ScanRepository.VideoPath(record);
        player.audioOutputMode = VideoAudioOutputMode.AudioSource;
        player.SetTargetAudioSource(0, audioSource);
        player.renderMode = VideoRenderMode.RenderTexture;
        player.isLooping = false;
        player.prepareCompleted += OnPrepared;
        player.errorReceived += (p, msg) => ShowToast("Cannot play video: " + msg);
        player.loopPointReached += _ => { playLabel.text = "PLAY"; };
        player.Prepare();
        ButtonTheme.SetInteractable(playButton, false);
        UpdateTimeUI(0f, record.durationSec);
    }

    private void OnPrepared(VideoPlayer p)
    {
        prepared = true;
        ButtonTheme.SetInteractable(playButton, true);
        int w = (int)p.width, h = (int)p.height;
        if (w > 0 && h > 0)
        {
            videoTexture = new RenderTexture(w, h, 0);
            p.targetTexture = videoTexture;
            videoAspect.aspectRatio = (float)w / h;
        }
        if (record.durationSec <= 0f && p.length > 0)
        {
            record.durationSec = (float)p.length;
            ScanRepository.Update(record);
            RefreshStatus();
        }
    }

    private void TogglePlay()
    {
        if (!prepared) return;
        if (player.isPlaying)
        {
            player.Pause();
            playLabel.text = "PLAY";
        }
        else
        {
            if (videoTexture != null) videoImage.texture = videoTexture;
            videoImage.color = Color.white;
            player.Play();
            playLabel.text = "PAUSE";
        }
    }

    private void OnSeek(float v)
    {
        if (!prepared || player.length <= 0) return;
        if (videoTexture != null && !player.isPlaying) videoImage.texture = videoTexture;
        player.time = v * player.length;
    }

    private void Update()
    {
        if (!prepared || record == null) return;
        double len = player.length > 0 ? player.length : record.durationSec;
        if (len <= 0) return;
        if (player.isPlaying) seekSlider.SetValueWithoutNotify((float)(player.time / len));
        UpdateTimeUI((float)player.time, (float)len);
    }

    private void UpdateTimeUI(float cur, float total)
    {
        timeLabel.text = ScanFormat.Duration(cur) + " / " + ScanFormat.Duration(total);
    }

    // ---------------------------------------------------------------- info + actions

    private void SaveInfo()
    {
        if (record == null || deleted) return;
        string title = nameInput.text.Trim();
        if (title.Length == 0) title = record.title;
        string notes = notesInput.text;
        if (title == record.title && notes == record.notes) return;
        record.title = title;
        record.notes = notes;
        headerTitle.text = title;
        nameInput.SetTextWithoutNotify(title);
        ScanRepository.Update(record);
    }

    private void RefreshStatus()
    {
        if (record == null || deleted) return;
        string dur = record.durationSec > 0 ? ScanFormat.Duration(record.durationSec) : "--:--";
        infoText.text = ScanFormat.Date(record.CreatedLocal) + "\n" + dur + "   |   " + ScanFormat.Size(record.sizeBytes);
        if (record.status == ScanStatus.Failed && !string.IsNullOrEmpty(record.statusMessage)) infoText.text += "\n" + record.statusMessage;
        statusText.text = ScanFormat.Status(record).ToUpperInvariant();
        statusPill.color = ScanRowView.StatusColor(record.status);

        bool uploading = record.status == ScanStatus.Uploading;
        progressFill.transform.parent.gameObject.SetActive(uploading);
        if (uploading) progressFill.anchorMax = new Vector2(Mathf.Clamp01(record.uploadProgress), 1f);

        bool busy = record.status == ScanStatus.Queued || uploading || record.status == ScanStatus.Processing;
        ButtonTheme.SetInteractable(sendButton, !busy && record.status != ScanStatus.Ready);
        sendLabel.text = record.status == ScanStatus.Failed ? "RETRY"
            : record.status == ScanStatus.Ready ? "SENT" : "SEND";
    }

    private void OnSend()
    {
        SaveInfo();
        ScanServices.Instance.StartUpload(record);
    }

    private void OnShare()
    {
        SaveInfo();
        if (!ScanServices.Instance.Share(record)) ShowToast("Sharing is not available");
    }

    private void OnDelete()
    {
        deleted = true;
        confirmLayer.SetActive(false);
        player.Stop();
        ScanServices.Instance.DeleteScan(record.id);
        ScanSession.SelectedId = null;
        var loader = FindFirstObjectByType<SceneLoader>();
        if (loader != null) loader.GoBack();
        else SceneManager.LoadScene("RoomScanLibrary");
    }

    private void ShowToast(string message)
    {
        toastText.text = message;
        toast.SetActive(true);
        StopAllCoroutines();
        StartCoroutine(HideToastLater());
    }

    private IEnumerator HideToastLater()
    {
        yield return new WaitForSecondsRealtime(3f);
        toast.SetActive(false);
    }
}
