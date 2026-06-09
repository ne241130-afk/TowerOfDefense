using UnityEngine;

[CreateAssetMenu(fileName = "SummonDefinition", menuName = "TowerOfDefense/Summon Definition")]
public class SummonDefinition : ScriptableObject
{
    [SerializeField] private string displayName;
    [SerializeField] private SummonType summonType;
    [SerializeField] private GameObject prefab;
    [SerializeField] private Sprite icon;
    [SerializeField] private int cost = 50;

    public string DisplayName => displayName;
    public SummonType Type => summonType;
    public GameObject Prefab => prefab;
    public Sprite Icon => icon;
    public int Cost => cost;
}
