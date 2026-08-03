using UnityEditor;
using UnityEngine;

/// <summary>
/// Scene View上でマウス位置に対応するセル座標(Vector3Int)を表示するエディタ拡張。
///
/// 使い方:
///   1. このファイルを Assets/Editor フォルダに配置する(なければEditorフォルダを作成)
///   2. Unity EditorのScene View内でマウスを動かすと、カーソル付近にセル座標が表示される
///   3. 左クリックすると、そのセル座標がConsoleにログ出力される
///
/// FieldGridConfigがシーンにあればそのGridを使い、なければシーン内の最初のGridを自動で使う。
/// </summary>
[InitializeOnLoad]
public static class GridCellPicker
{
    static GridCellPicker()
    {
        SceneView.duringSceneGui += OnSceneGUI;
    }

    private static void OnSceneGUI(SceneView sceneView)
    {
        Grid grid = FieldGridConfig.Instance != null
            ? FieldGridConfig.Instance.grid
            : Object.FindFirstObjectByType<Grid>();

        if (grid == null) return;

        Event e = Event.current;

        // マウスのスクリーン座標をワールド座標(z=0平面)に変換
        Vector3 mouseWorldPos = HandleUtility.GUIPointToWorldRay(e.mousePosition).origin;
        mouseWorldPos.z = 0f;

        Vector3Int cell = grid.WorldToCell(mouseWorldPos);

        // カーソル位置付近にセル座標をラベル表示
        Handles.BeginGUI();
        GUI.Label(new Rect(e.mousePosition.x + 12, e.mousePosition.y - 8, 160, 20),
            $"Cell: {cell}", EditorStyles.helpBox);
        Handles.EndGUI();

        // 左クリックでConsoleに出力(Altキーを押しながらの回転操作は除外)
        if (e.type == EventType.MouseDown && e.button == 0 && !e.alt)
        {
            Debug.Log($"[GridCellPicker] クリックしたセル座標: {cell}");
        }

        sceneView.Repaint();
    }
}
