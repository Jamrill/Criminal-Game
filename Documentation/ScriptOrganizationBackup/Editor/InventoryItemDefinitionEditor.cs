using JuegoCriminal.Inventory;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(InventoryItemDefinition), true)]
public sealed class InventoryItemDefinitionEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawPropertiesExcluding(serializedObject, "m_Script", "occupiedCells");

        SerializedProperty width = serializedObject.FindProperty("gridWidth");
        SerializedProperty height = serializedObject.FindProperty("gridHeight");
        SerializedProperty cells = serializedObject.FindProperty("occupiedCells");
        int gridWidth = Mathf.Max(1, width.intValue);
        int gridHeight = Mathf.Max(1, height.intValue);
        int requiredSize = gridWidth * gridHeight;

        if (cells.arraySize != requiredSize)
        {
            int previousSize = cells.arraySize;
            cells.arraySize = requiredSize;
            for (int i = previousSize; i < requiredSize; i++)
                cells.GetArrayElementAtIndex(i).boolValue = true;
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Occupied Cells", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Activo = el objeto ocupa esa casilla. La primera fila es la parte superior.", MessageType.Info);

        for (int y = 0; y < gridHeight; y++)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            for (int x = 0; x < gridWidth; x++)
            {
                SerializedProperty cell = cells.GetArrayElementAtIndex(y * gridWidth + x);
                cell.boolValue = GUILayout.Toggle(cell.boolValue, GUIContent.none, "Button", GUILayout.Width(28f), GUILayout.Height(28f));
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        serializedObject.ApplyModifiedProperties();
    }
}
