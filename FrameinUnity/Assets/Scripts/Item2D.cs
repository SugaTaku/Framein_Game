using UnityEngine;
using System.Collections;

public class Item2D : MonoBehaviour
{

    public int healPoint = 20;
    public HPGauge2D lifeScript;

    void OnCollisionEnter2D(Collision2D col)
    {
        //ユニティちゃんと衝突した時
        if (col.gameObject.tag == "Player")
        {
            //LifeUpメソッドを呼び出す　引数はhealPoint
            lifeScript.LifeUp(healPoint);
            //アイテムを削除する
            Destroy(gameObject);
        }
    }
}