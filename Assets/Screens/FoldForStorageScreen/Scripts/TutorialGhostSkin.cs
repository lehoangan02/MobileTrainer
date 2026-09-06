using System.Collections.Generic;
using UnityEngine;

public class TutorialGhostSkin : MonoBehaviour
{
    [Header("Settings")]
    public Material hologramMaterial;
    public List<Transform> ghostRoots = new List<Transform>();
    public bool applyOnAwake = true;
    public bool skipHands = false;
    public bool debugLog = false;

    void Awake()
    {
        if (applyOnAwake)
        {
            ApplySkin();
        }
    }

    [ContextMenu("Apply Skin")]
    public void ApplySkin()
    {
        if (hologramMaterial == null)
        {
            if (debugLog) Debug.LogWarning("[TutorialGhostSkin] No hologram material assigned.");
            return;
        }

        List<Transform> targets = (ghostRoots != null && ghostRoots.Count > 0) ? ghostRoots : new List<Transform> { transform };

        int count = 0;
        foreach (var root in targets)
        {
            if (root == null) continue;

            if (skipHands && (root.name.Contains("Hand") || root.name.Contains("XRHand")))
                continue;

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                if (skipHands && (r.name.Contains("Hand") || (r.transform.parent != null && r.transform.parent.name.Contains("Hand"))))
                    continue;

                Material[] mats = new Material[r.sharedMaterials.Length > 0 ? r.sharedMaterials.Length : 1];
                for (int i = 0; i < mats.Length; i++)
                {
                    mats[i] = hologramMaterial;
                }
                r.sharedMaterials = mats;
                count++;
            }
        }

        if (debugLog) Debug.Log($"[TutorialGhostSkin] Applied ghost skin to {count} renderers.");
    }
}
