using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 各セルに現在どの動物がいるかを管理するシングルトン。
/// 経路探索時にこれを参照することで、動物同士が同じマスに重なるのを防ぐ。
/// </summary>
public class AnimalOccupancyMap : MonoBehaviour
{
    public static AnimalOccupancyMap Instance { get; private set; }

    private readonly Dictionary<Vector3Int, AnimalController> occupied =
        new Dictionary<Vector3Int, AnimalController>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void SetOccupied(Vector3Int cell, AnimalController animal)
    {
        occupied[cell] = animal;
    }

    public void ClearOccupied(Vector3Int cell, AnimalController animal)
    {
        if (occupied.TryGetValue(cell, out var current) && current == animal)
        {
            occupied.Remove(cell);
        }
    }

    /// <summary>
    /// 自分以外の動物がこのマスにいるかどうか。
    /// </summary>
    public bool IsOccupiedByOther(Vector3Int cell, AnimalController self)
    {
        return occupied.TryGetValue(cell, out var current) && current != self;
    }

    /// <summary>
    /// 誰でもいいので、このマスに動物がいるかどうか。スポナーの重複生成防止などに使う。
    /// </summary>
    public bool IsOccupied(Vector3Int cell)
    {
        return occupied.ContainsKey(cell);
    }
}
