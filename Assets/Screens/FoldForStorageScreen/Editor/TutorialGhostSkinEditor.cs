using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(TutorialGhostSkin))]
public class TutorialGhostSkinEditor : Editor
{
    public override void OnInspectorGUI()
    {
        TutorialGhostSkin skin = (TutorialGhostSkin)target;

        EditorGUILayout.Space(6);
        EditorGUILayout.BeginVertical("box");
        
        EditorGUILayout.LabelField("Theme Preset Quick-Switch", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Click any button below to instantly switch the drone skin & highlight material for different build versions. You can also press 1, 2, or 3 on your keyboard during Play mode.", MessageType.Info);

        EditorGUILayout.Space(4);

        // Highlight active theme button
        var current = skin.CurrentTheme;

        // Version 1 Button
        GUI.backgroundColor = (current == TutorialGhostSkin.SkinTheme.CyanGhost_RedHighlight) ? new Color(0.4f, 0.9f, 1f) : Color.white;
        if (GUILayout.Button("Version 1: Cyan Ghost + Red Highlight (Default)", GUILayout.Height(32)))
        {
            Undo.RecordObject(skin, "Switch to Version 1 Theme");
            skin.SetTheme(TutorialGhostSkin.SkinTheme.CyanGhost_RedHighlight);
            EditorUtility.SetDirty(skin);
        }

        // Version 2 Button
        GUI.backgroundColor = (current == TutorialGhostSkin.SkinTheme.CarbonFiber_YellowHighlight) ? new Color(1f, 0.9f, 0.3f) : Color.white;
        if (GUILayout.Button("Version 2: Carbon Fiber + Yellow Highlight", GUILayout.Height(32)))
        {
            Undo.RecordObject(skin, "Switch to Version 2 Theme");
            skin.SetTheme(TutorialGhostSkin.SkinTheme.CarbonFiber_YellowHighlight);
            EditorUtility.SetDirty(skin);
        }

        // Version 3 Button
        GUI.backgroundColor = (current == TutorialGhostSkin.SkinTheme.CarbonFiber_CyanHighlight) ? new Color(0.4f, 1f, 0.8f) : Color.white;
        if (GUILayout.Button("Version 3: Carbon Fiber + Cyan Highlight", GUILayout.Height(32)))
        {
            Undo.RecordObject(skin, "Switch to Version 3 Theme");
            skin.SetTheme(TutorialGhostSkin.SkinTheme.CarbonFiber_CyanHighlight);
            EditorUtility.SetDirty(skin);
        }

        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(8);

        // Draw standard fields
        DrawDefaultInspector();

        EditorGUILayout.Space(6);
        if (GUILayout.Button("Force Reapply Skin to Rig", GUILayout.Height(26)))
        {
            skin.ApplySkin();
        }
    }
}
