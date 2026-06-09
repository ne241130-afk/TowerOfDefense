using UnityEngine;
using TMPro; //テキスト
using UnityEngine.SceneManagement;

public class TowerHealth : MonoBehaviour
{
    [SerializeField] private int maxHp = 10;
    [SerializeField] private TMP_Text hpText;
    [SerializeField] private string gameOverSceneName = "GameOverScene";

    private int hp;
    public bool IsDestroyed => hp <= 0;

    private void Start()
    {
        hp = maxHp; //HP初期値マックス
        UpdateHpText();
    }

    public void TakeDamage(int damage)
    {
        //すでに壊れてるなら何もしない
        if (hp <= 0)
        {
            return;
        }

        hp -= damage;
        //大きい方を返す
        hp = Mathf.Max(hp, 0);
        
        UpdateHpText();
        Debug.Log("Tower HP: " + hp);

        if (hp <= 0)
        {
            //いったん倒れましたの報告
            Debug.Log("Towerが崩壊した。");
            SceneManager.LoadScene(gameOverSceneName);
        }
    }

    private void UpdateHpText()
    {
        hpText.text = "Tower HP: " + hp;
    }

}
