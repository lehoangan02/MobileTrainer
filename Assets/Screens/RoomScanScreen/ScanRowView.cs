using System.IO;
using Lean.Gui;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>One entry in the room scan library list.</summary>
public class ScanRowView : MonoBehaviour
{
    [SerializeField] private LeanButton openButton;
    [SerializeField] private LeanButton menuButton;
    [SerializeField] private RawImage thumb;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text subText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private Image statusPill;
    [SerializeField] private RectTransform progressFill;

    private Texture2D thumbTexture;
    private string thumbLoadedFrom;
    private bool thumbInitialised;

    public string ScanId { get; private set; }
    public LeanButton OpenButton => openButton;
    public LeanButton MenuButton => menuButton;

    public void Bind(ScanRecord r)
    {
        ScanId = r.id;
        titleText.text = r.title;
        subText.text = ScanFormat.Summary(r);
        statusText.text = ScanFormat.Status(r).ToUpperInvariant();
        statusPill.color = StatusColor(r.status);

        bool showProgress = r.status == ScanStatus.Uploading;
        progressFill.transform.parent.gameObject.SetActive(showProgress);
        if (showProgress)
        {
            progressFill.anchorMax = new Vector2(Mathf.Clamp01(r.uploadProgress), 1f);
        }

        LoadThumb(ScanRepository.ThumbPath(r));
    }

    public static Color StatusColor(ScanStatus s)
    {
        switch (s)
        {
            case ScanStatus.Local: return new Color(0.78f, 0.80f, 0.84f);
            case ScanStatus.Queued:
            case ScanStatus.Uploading: return new Color(0.96f, 0.80f, 0.35f);
            case ScanStatus.Processing: return new Color(0.55f, 0.75f, 0.98f);
            case ScanStatus.Ready: return new Color(0.50f, 0.85f, 0.55f);
            default: return new Color(0.96f, 0.50f, 0.48f);
        }
    }

    private void LoadThumb(string path)
    {
        if (thumbInitialised && path == thumbLoadedFrom) return;
        thumbInitialised = true;
        thumbLoadedFrom = path;
        if (thumbTexture != null) { Destroy(thumbTexture); thumbTexture = null; }

        if (!string.IsNullOrEmpty(path) && File.Exists(path))
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGB24, false);
            if (tex.LoadImage(File.ReadAllBytes(path)))
            {
                thumbTexture = tex;
                thumb.texture = tex;
                thumb.color = Color.white;
                return;
            }
            Destroy(tex);
        }
        thumb.texture = null;
        thumb.color = new Color(0.12f, 0.14f, 0.18f);
    }

    private void OnDestroy()
    {
        if (thumbTexture != null) Destroy(thumbTexture);
    }
}
