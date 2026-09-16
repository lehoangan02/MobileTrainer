using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 3D visual anchor for tutorial step camera positions.
/// Place or drag this object anywhere in 3D space to position the camera for a step.
/// Selecting this GameObject in the Unity Editor automatically shows Unity's built-in
/// Camera Preview window in the bottom-right corner of the Scene View.
/// </summary>
[ExecuteInEditMode]
[SelectionBase]
[DisallowMultipleComponent]
public class StepCameraAnchor : MonoBehaviour
{
    [Header("Step Information")]
    [Tooltip("Zero-based index of the step this anchor is assigned to.")]
    public int stepIndex = 0;

    [Tooltip("Title of the step for easy reference in the scene.")]
    public string stepTitle = "";

    [Header("Orientation & Aiming")]
    [Tooltip("Target in the scene to aim towards (e.g. Ghost_Drone or specific part).")]
    public Transform lookAtTarget;

    [Tooltip("When enabled, the anchor automatically points towards the lookAtTarget in edit mode.")]
    public bool autoLookAtTarget = false;

    [Range(10f, 120f)]
    public float fieldOfView = 55f;

    [Header("Visual Gizmo Settings")]
    public Color gizmoColor = new Color(0.2f, 0.85f, 0.4f, 0.9f);
    public float frustumLength = 1.2f;

    [SerializeField, HideInInspector]
    private Camera previewCamera;

    public Camera PreviewCamera => previewCamera;

    private void Awake()
    {
        EnsurePreviewCamera();
    }

    private void OnValidate()
    {
        EnsurePreviewCamera();
    }

    /// <summary>
    /// Ensures a disabled Camera component exists on this GameObject.
    /// This causes Unity's Scene View to automatically pop up its native Camera Preview overlay
    /// whenever this GameObject is selected in the Editor.
    /// </summary>
    public void EnsurePreviewCamera()
    {
        if (previewCamera == null)
        {
            previewCamera = GetComponent<Camera>();
        }
        if (previewCamera == null)
        {
            previewCamera = gameObject.AddComponent<Camera>();
        }

        // Always keep disabled at runtime so it never renders as an extra camera in-game
        previewCamera.enabled = false;
        previewCamera.fieldOfView = fieldOfView;
        previewCamera.nearClipPlane = 0.05f;
        previewCamera.farClipPlane = 100f;
    }

    private void Update()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            if (previewCamera != null && Mathf.Abs(previewCamera.fieldOfView - fieldOfView) > 0.01f)
            {
                previewCamera.fieldOfView = fieldOfView;
            }

            if (autoLookAtTarget && lookAtTarget != null)
            {
                transform.LookAt(lookAtTarget.position);
            }
        }
#endif
    }

    /// <summary>
    /// Converts this anchor's current 3D position and rotation into a StepCameraPose.
    /// </summary>
    public StepCameraPose ToPose(Transform pivotTarget = null)
    {
        Transform target = pivotTarget != null ? pivotTarget : lookAtTarget;
        return StepCameraPose.CreateFromTransform(transform, target, fieldOfView);
    }

    /// <summary>
    /// Snaps this 3D anchor to the current SceneView camera position and rotation.
    /// </summary>
    public void AlignToSceneView()
    {
#if UNITY_EDITOR
        var sv = SceneView.lastActiveSceneView;
        if (sv != null)
        {
            Undo.RecordObject(transform, "Align Camera Anchor to SceneView");
            transform.position = sv.camera.transform.position;
            transform.rotation = sv.camera.transform.rotation;
            fieldOfView = sv.camera.fieldOfView;
            if (previewCamera != null) previewCamera.fieldOfView = fieldOfView;
            EditorUtility.SetDirty(gameObject);
        }
#endif
    }

    /// <summary>
    /// Rotates this anchor to face the lookAtTarget directly.
    /// </summary>
    public void LookAtTarget()
    {
        if (lookAtTarget != null)
        {
#if UNITY_EDITOR
            Undo.RecordObject(transform, "Look At Target");
#endif
            transform.LookAt(lookAtTarget.position);
#if UNITY_EDITOR
            EditorUtility.SetDirty(gameObject);
#endif
        }
    }

    private void OnDrawGizmos()
    {
        DrawGizmoInternal(false);
    }

    private void OnDrawGizmosSelected()
    {
        DrawGizmoInternal(true);
    }

    private void DrawGizmoInternal(bool selected)
    {
        Color c = selected ? Color.yellow : gizmoColor;
        Gizmos.color = c;

        Vector3 pos = transform.position;
        Quaternion rot = transform.rotation;
        Vector3 fwd = transform.forward;
        Vector3 up = transform.up;
        Vector3 right = transform.right;

        // Draw camera body wireframe
        Matrix4x4 oldMatrix = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(pos, rot, Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(0.18f, 0.12f, 0.22f));

        // Draw camera lens cone
        Vector3 lensPos = new Vector3(0f, 0f, 0.14f);
        Gizmos.DrawWireSphere(lensPos, 0.05f);

        // Draw view frustum pyramid
        float fovRad = fieldOfView * 0.5f * Mathf.Deg2Rad;
        float aspect = 16f / 9f;
        float halfH = Mathf.Tan(fovRad) * frustumLength;
        float halfW = halfH * aspect;

        Vector3 f1 = new Vector3(-halfW,  halfH, frustumLength);
        Vector3 f2 = new Vector3( halfW,  halfH, frustumLength);
        Vector3 f3 = new Vector3( halfW, -halfH, frustumLength);
        Vector3 f4 = new Vector3(-halfW, -halfH, frustumLength);

        Gizmos.DrawLine(Vector3.zero, f1);
        Gizmos.DrawLine(Vector3.zero, f2);
        Gizmos.DrawLine(Vector3.zero, f3);
        Gizmos.DrawLine(Vector3.zero, f4);

        Gizmos.DrawLine(f1, f2);
        Gizmos.DrawLine(f2, f3);
        Gizmos.DrawLine(f3, f4);
        Gizmos.DrawLine(f4, f1);

        // Top triangle indicator (which way is up)
        Vector3 topCenter = (f1 + f2) * 0.5f;
        Vector3 topArrow = topCenter + Vector3.up * 0.08f;
        Gizmos.DrawLine(f1, topArrow);
        Gizmos.DrawLine(f2, topArrow);

        Gizmos.matrix = oldMatrix;

        // Draw line to target pivot
        if (lookAtTarget != null)
        {
            Gizmos.color = new Color(c.r, c.g, c.b, selected ? 0.8f : 0.35f);
            Gizmos.DrawLine(pos, lookAtTarget.position);
            Gizmos.DrawWireSphere(lookAtTarget.position, 0.06f);
        }

#if UNITY_EDITOR
        // Draw 3D label in SceneView
        string labelText = string.IsNullOrEmpty(stepTitle)
            ? $"📷 Step {stepIndex + 1}"
            : $"📷 Step {stepIndex + 1}: {stepTitle}";

        GUIStyle labelStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = selected ? Color.yellow : Color.white }
        };

        Handles.Label(pos + up * 0.25f, labelText, labelStyle);
#endif
    }
}
