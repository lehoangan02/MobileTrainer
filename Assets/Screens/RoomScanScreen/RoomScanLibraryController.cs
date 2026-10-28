using System.Collections;
using System.Collections.Generic;
using Lean.Gui;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Lists recorded room scans and offers record / open / share / send / delete.</summary>
public class RoomScanLibraryController : MonoBehaviour
{
    [Header("List")]
    [SerializeField] private RectTransform listContent;
    [SerializeField] private ScanRowView rowPrefab;
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private GameObject emptyState;
    [SerializeField] private LeanButton recordButton;

    [Header("Row menu")]
    [SerializeField] private GameObject menuLayer;
    [SerializeField] private TMP_Text menuTitle;
    [SerializeField] private Button menuBackdrop;
    [SerializeField] private LeanButton menuOpen;
    [SerializeField] private LeanButton menuShare;
    [SerializeField] private LeanButton menuSend;
    [SerializeField] private Text menuSendLabel;
    [SerializeField] private LeanButton menuDelete;

    [Header("Delete confirmation")]
    [SerializeField] private GameObject confirmLayer;
    [SerializeField] private TMP_Text confirmText;
    [SerializeField] private LeanButton confirmYes;
    [SerializeField] private LeanButton confirmNo;

    [Header("Toast")]
    [SerializeField] private GameObject toast;
    [SerializeField] private TMP_Text toastText;

    private readonly List<ScanRowView> rows = new List<ScanRowView>();
    private string menuScanId;
    private Coroutine toastRoutine;

    private void Awake()
    {
        recordButton.OnClick.AddListener(OnRecord);
        menuBackdrop.onClick.AddListener(CloseMenu);
        menuOpen.OnClick.AddListener(() => { string id = menuScanId; CloseMenu(); OpenDetail(id); });
        menuShare.OnClick.AddListener(OnMenuShare);
        menuSend.OnClick.AddListener(OnMenuSend);
        menuDelete.OnClick.AddListener(OnMenuDelete);
        confirmYes.OnClick.AddListener(OnConfirmDelete);
        confirmNo.OnClick.AddListener(() => confirmLayer.SetActive(false));
        menuLayer.SetActive(false);
        confirmLayer.SetActive(false);
        toast.SetActive(false);
    }

    private void OnEnable()
    {
        ScanRepository.Changed += Refresh;
        SceneLoader.BackHandler = HandleBack;
        Refresh();
    }

    private void OnDisable()
    {
        ScanRepository.Changed -= Refresh;
        if (SceneLoader.BackHandler == (System.Func<bool>)HandleBack) SceneLoader.BackHandler = null;
    }

    private bool HandleBack()
    {
        if (confirmLayer.activeSelf) { confirmLayer.SetActive(false); return true; }
        if (menuLayer.activeSelf) { CloseMenu(); return true; }
        return false;
    }

    private void Refresh()
    {
        IReadOnlyList<ScanRecord> items = ScanRepository.All;
        emptyState.SetActive(items.Count == 0);

        bool sameOrder = rows.Count == items.Count;
        for (int i = 0; sameOrder && i < items.Count; i++)
        {
            sameOrder = rows[i] != null && rows[i].ScanId == items[i].id;
        }

        if (!sameOrder)
        {
            foreach (var row in rows) if (row != null) Destroy(row.gameObject);
            rows.Clear();
            foreach (var rec in items)
            {
                ScanRowView row = Instantiate(rowPrefab, listContent);
                string id = rec.id;
                row.OpenButton.OnClick.AddListener(() => OpenDetail(id));
                row.MenuButton.OnClick.AddListener(() => OpenMenu(id));
                rows.Add(row);
            }
        }

        for (int i = 0; i < items.Count; i++) rows[i].Bind(items[i]);

        if (menuLayer.activeSelf) RefreshMenu();
    }

    private void OnRecord()
    {
        ButtonTheme.SetInteractable(recordButton, false);
        ScanServices.Instance.RecordNew((rec, error) =>
        {
            if (this == null) return;
            ButtonTheme.SetInteractable(recordButton, true);
            if (rec != null)
            {
                scrollRect.verticalNormalizedPosition = 1f;
                ShowToast("Scan saved");
            }
            else if (error != null)
            {
                ShowToast(error);
            }
        });
    }

    private void OpenDetail(string id)
    {
        ScanSession.SelectedId = id;
        var loader = FindFirstObjectByType<SceneLoader>();
        if (loader != null) loader.LoadScene("RoomScanDetail");
        else SceneManager.LoadScene("RoomScanDetail");
    }

    private void OpenMenu(string id)
    {
        menuScanId = id;
        menuLayer.SetActive(true);
        RefreshMenu();
    }

    private void RefreshMenu()
    {
        ScanRecord r = ScanRepository.Get(menuScanId);
        if (r == null) { CloseMenu(); return; }
        menuTitle.text = r.title;
        bool busy = r.status == ScanStatus.Queued || r.status == ScanStatus.Uploading || r.status == ScanStatus.Processing;
        ButtonTheme.SetInteractable(menuSend, !busy && r.status != ScanStatus.Ready);
        menuSendLabel.text = r.status == ScanStatus.Failed ? "RETRY"
            : r.status == ScanStatus.Ready ? "SENT" : "SEND";
    }

    private void CloseMenu()
    {
        menuLayer.SetActive(false);
        menuScanId = null;
    }

    private void OnMenuShare()
    {
        ScanRecord r = ScanRepository.Get(menuScanId);
        CloseMenu();
        if (r != null && !ScanServices.Instance.Share(r)) ShowToast("Sharing is not available");
    }

    private void OnMenuSend()
    {
        ScanRecord r = ScanRepository.Get(menuScanId);
        if (r != null) ScanServices.Instance.StartUpload(r);
        CloseMenu();
    }

    private void OnMenuDelete()
    {
        ScanRecord r = ScanRepository.Get(menuScanId);
        if (r == null) return;
        confirmText.text = "Delete \"" + r.title + "\"?\nThe video will be removed from this device.";
        confirmLayer.SetActive(true);
    }

    private void OnConfirmDelete()
    {
        string id = menuScanId;
        confirmLayer.SetActive(false);
        CloseMenu();
        if (id != null) ScanServices.Instance.DeleteScan(id);
    }

    private void ShowToast(string message)
    {
        toastText.text = message;
        toast.SetActive(true);
        if (toastRoutine != null) StopCoroutine(toastRoutine);
        toastRoutine = StartCoroutine(HideToastLater());
    }

    private IEnumerator HideToastLater()
    {
        yield return new WaitForSecondsRealtime(3f);
        toast.SetActive(false);
    }
}
