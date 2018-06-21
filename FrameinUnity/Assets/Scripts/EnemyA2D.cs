using UnityEngine;
using System.Collections;

public class EnemyA2D : MonoBehaviour
{

    Rigidbody2D rigidbody2D;
    public int speed = -3;
    public GameObject explosion;
    public GameObject item;

    public int attackPoint = 10;
    private HPGauge2D lifeScript;

    void Start()
    {
        rigidbody2D = GetComponent<Rigidbody2D>();
        lifeScript = GameObject.FindGameObjectWithTag("HP").GetComponent<HPGauge2D>();
    }

    void Update()
    {
        rigidbody2D.velocity = new Vector2(speed, rigidbody2D.velocity.y);
    }

    void OnTriggerEnter2D(Collider2D col)
    {
        if (col.tag == "Bullet")
        {
            Destroy(gameObject);
            Instantiate(explosion, transform.position, transform.rotation);

            //四分の一の確率で回復アイテムを落とす
            if (Random.Range(0, 4) == 0)
            {
                Instantiate(item, transform.position, transform.rotation);
            }
        }
    }

    void OnCollisionEnter2D(Collision2D col)
    {
        //UnityChanとぶつかった時
        if (col.gameObject.tag == "Player")
        {
            //LifeScriptのLifeDownメソッドを実行
            lifeScript.LifeDown(attackPoint);
        }
    }
}
