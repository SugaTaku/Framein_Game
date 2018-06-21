using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AnimationManager : MonoBehaviour
{

    Rigidbody rb;
    //移動スピード
    public float speed = 2f;
    //ジャンプ力
    public float thrust = 100;
    //Animatorを入れる変数
    private Animator animator;
    //Planeに触れているか判定するため
    bool ground;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        //UnityちゃんのAnimatorにアクセスする
        animator = GetComponent<Animator>();
    }

    void Update()
    {
        //地面に触れている場合発動
        if (ground)
        {
            //上下左右のキーでの移動、向き、アニメーション
            if (Input.GetKey(KeyCode.RightArrow))
            {
                //移動(X軸、Y軸、Z軸）
                rb.velocity = new Vector3(speed, 0, 0);
                //向き(X軸、Y軸、Z軸）
                transform.rotation = Quaternion.Euler(0, 90, 0);
                //アニメーション
                animator.SetBool("Run", true);
            }
            else if (Input.GetKey(KeyCode.LeftArrow))
            {
                rb.velocity = new Vector3(-speed, 0, 0);
                transform.rotation = Quaternion.Euler(0, 270, 0);
                animator.SetBool("Run", true);
            }
            else if (Input.GetKey(KeyCode.UpArrow))
            {
                rb.velocity = new Vector3(0, 0, speed);
                transform.rotation = Quaternion.Euler(0, 0, 0);
                animator.SetBool("Run", true);
            }

            else if (Input.GetKey(KeyCode.DownArrow))
            {
                rb.velocity = new Vector3(0, 0, -speed);
                transform.rotation = Quaternion.Euler(0, 180, 0);
                animator.SetBool("Run", true);
            }
            //何もキーを押していない時はアニメーションをオフにする
            else
            {
                animator.SetBool("Run", false);
            }

            //スペースキーでジャンプする
            if (Input.GetKey(KeyCode.Space))
            {
                animator.SetBool("Jump", true);
                //上方向に向けて力を加える
                rb.AddForce(new Vector3(0, thrust, 0));
                ground = false;
            }
            else
            {
                animator.SetBool("Jump", false);
            }
        }
    }

    //別のCollider、今回はPlaneに触れているかどうかを判断する
    void OnCollisionStay(Collision col)
    {
        ground = true;
    }
}