using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 必要なコンポーネントの列記
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]

public class AnimationManager : MonoBehaviour
{
    public PlayerAnimState playerAnimState;                         // プレイヤーのアニメーターステート

    private Animator anim;                                          // キャラにアタッチされるアニメーターへの参照
    private Rigidbody rb;
    private CapsuleCollider col;                                    // キャラクターコントローラ（カプセルコライダ）の参照
    private AnimatorStateInfo currentAnimState;                     // BaseLayerで使われる、アニメーターの現在の状態の参照

    public float animSpeed = 1.5f;                                  // アニメーション再生速度設定
    public float lookSmoother = 3.0f;                               // a smoothing setting for camera motion
    public bool useCurves = true;                                   // Mecanimでカーブ調整を使うか設定する
                                                                    // このスイッチが入っていないとカーブは使われない
    public float useCurvesHeight = 0.5f;                            // カーブ補正の有効高さ（地面をすり抜けやすい時には大きくする）

    // キャラクターコントローラ用パラメータ
    public float speed = 3.0f;                                      // 移動速度
    public float thrust = 3.0f;                                     // ジャンプ力
    public float locomotionThreshold = 0.05f;                             // Locomotion移行のしきい値

    // キャラクターコントローラ(カプセルコライダ)の移動量
    private Vector3 velocity;
    // CapsuleColliderで設定されているコライダのHeight、Centerの初期値を収める変数
    private float orgColHight;
    private Vector3 orgVectColCenter;

    private GameObject mainCamera;                                // メインカメラへの参照



    //Planeに触れているか判定するため
    private bool isGround;

    // 初期化
    void Start()
    {
        anim = GetComponent<Animator>();                            // Animatorコンポーネントを取得する
        anim.speed = animSpeed;                                     // Animatorのモーション再生速度に animSpeedを設定する

        col = GetComponent<CapsuleCollider>();                      // CapsuleColliderコンポーネントを取得する(カプセル型コリジョン)
        rb = GetComponent<Rigidbody>();

        mainCamera = GameObject.FindWithTag("MainCamera");          //メインカメラを取得する

        // CapsuleColliderコンポーネントのHeight、Centerの初期値を保存する
        orgColHight = col.height;
        orgVectColCenter = col.center;
    }

    // 以下、メイン処理.リジッドボディと絡めるので、FixedUpdate内で処理を行う.
    void FixedUpdate()
    {
        currentAnimState = anim.GetCurrentAnimatorStateInfo(0);     // 参照用のステート変数にBase Layer (0)の現在のステートを設定する

        float h = Input.GetAxis("Horizontal");                      // 入力デバイスの水平軸をhで定義
        anim.SetFloat("Speed", h);                                  // Animator側で設定している"Speed"パラメータにhを渡す

        // キャラクターの移動処理
        if (h > locomotionThreshold || h < -locomotionThreshold)    // hがLocomotion移行のしきい値を超えている場合
        {
            anim.SetBool(PlayerAnimState.Locomotion, true);
            rb.velocity = new Vector3(speed * h, 0, 0);             // 移動速度と方向をベクトルに掛ける
            if (h > locomotionThreshold)                            // 右を向く
            {
                transform.rotation = Quaternion.Euler(0, 90, 0);
            }
            else if (h < -locomotionThreshold)                      // 左を向く
            {
                transform.rotation = Quaternion.Euler(0, 270, 0);
            }
        }
        else
        {
            anim.SetBool(PlayerAnimState.Locomotion, false);
        }

        currentAnimState = anim.GetCurrentAnimatorStateInfo(0);     // 参照用のステート変数にBaseLayer(0)の現在のステートを設定する
        rb.useGravity = true;                                       // ジャンプ中に重力を切るので、それ以外は重力の影響を受けるようにする

        if (Input.GetButtonDown("Jump"))
        {   // スペースキーを入力したら
            // ステート遷移中でなかったらジャンプできる
                if (!anim.IsInTransition(0))
                {
                rb.AddForce(Vector3.up * thrust, ForceMode.Impulse);
                    anim.SetBool("Jump", true);                     // Animatorにジャンプに切り替えるフラグを送る
                }
        }

        if (Input.GetButtonDown("Squat"))
        {   // Nキーを入力したら
            // アニメーションのステートがIdleの最中のみしゃがめる
            if (currentAnimState.fullPathHash == anim.EnumToHash(PlayerAnimState.Idle))
            {
                anim.SetBool(PlayerAnimState.Squat, true);          // Animatorにしゃがみに切り替えるフラグを送る
            }
            else if (currentAnimState.fullPathHash == anim.EnumToHash(PlayerAnimState.Squat))
            {
                anim.SetBool(PlayerAnimState.Squat, false);
            }
        }

        // 以下、Animatorの各ステート中での処理
        // Locomotion中
        // 現在のベースレイヤーがlocoStateの時
        if (currentAnimState.fullPathHash == anim.EnumToHash(PlayerAnimState.Locomotion))
        {
            //カーブでコライダ調整をしている時は、念のためにリセットする
            if (useCurves)
            {
                resetCollider();
            }
        }

        // JUMP中の処理
        // 現在のベースレイヤーがjumpStateの時
        else if (currentAnimState.fullPathHash == anim.EnumToHash(PlayerAnimState.Jump))
        {
            //cameraObject.SendMessage("setCameraPositionJumpView");  // ジャンプ中のカメラに変更
                                                                    // ステートがトランジション中でない場合
            if (!anim.IsInTransition(0))
            {
                // 以下、カーブ調整をする場合の処理
                if (useCurves)
                {
                    // 以下JUMP00アニメーションについているカーブJumpHeightとGravityControl
                    // JumpHeight:JUMP00でのジャンプの高さ（0〜1）
                    // GravityControl:1⇒ジャンプ中（重力無効）、0⇒重力有効
                    float jumpHeight = anim.GetFloat("JumpHeight");
                    float gravityControl = anim.GetFloat("GravityControl");
                    //if (gravityControl > 0)
                        //rb.useGravity = false;  //ジャンプ中の重力の影響を切る

                    // レイキャストをキャラクターのセンターから落とす
                    Ray ray = new Ray(transform.position + Vector3.up, -Vector3.up);
                    RaycastHit hitInfo = new RaycastHit();
                    // 高さが useCurvesHeight 以上ある時のみ、コライダーの高さと中心をJUMP00アニメーションについているカーブで調整する
                    if (Physics.Raycast(ray, out hitInfo))
                    {
                        if (hitInfo.distance > useCurvesHeight)
                        {
                            col.height = orgColHight - jumpHeight;          // 調整されたコライダーの高さ
                            float adjCenterY = orgVectColCenter.y + jumpHeight;
                            col.center = new Vector3(0, adjCenterY, 0); // 調整されたコライダーのセンター
                        }
                        else
                        {
                            // 閾値よりも低い時には初期値に戻す（念のため）                   
                            resetCollider();
                        }
                    }
                }
                // Jump bool値をリセットする（ループしないようにする）               
                anim.SetBool("Jump", false);
            }
        }
        // IDLE中の処理
        // 現在のベースレイヤーがidleStateの時
        else if (currentAnimState.fullPathHash == anim.EnumToHash(PlayerAnimState.Idle))
        {
            //カーブでコライダ調整をしている時は、念のためにリセットする
            if (useCurves)
            {
                resetCollider();
            }
            // スペースキーを入力したらRest状態になる
            if (Input.GetButtonDown("Jump"))
            {
                anim.SetBool("Rest", true);
            }
        }
        // REST中の処理
        // 現在のベースレイヤーがrestStateの時
        else if (currentAnimState.fullPathHash == anim.EnumToHash(PlayerAnimState.Squat))
        {
            //cameraObject.SendMessage("setCameraPositionFrontView");       // カメラを正面に切り替える
            // ステートが遷移中でない場合、Rest bool値をリセットする（ループしないようにする）
            if (!anim.IsInTransition(0))
            {
                anim.SetBool("Rest", false);
            }
        }
    }

    void OnGUI()
    {
        GUI.Box(new Rect(Screen.width - 260, 10, 250, 150), "Interaction");
        GUI.Label(new Rect(Screen.width - 245, 30, 250, 30), "Up/Down Arrow : Go Forwald/Go Back");
        GUI.Label(new Rect(Screen.width - 245, 50, 250, 30), "Left/Right Arrow : Turn Left/Turn Right");
        GUI.Label(new Rect(Screen.width - 245, 70, 250, 30), "Hit Space key while Running : Jump");
        GUI.Label(new Rect(Screen.width - 245, 90, 250, 30), "Hit Spase key while Stopping : Rest");
        GUI.Label(new Rect(Screen.width - 245, 110, 250, 30), "Left Control : Front Camera");
        GUI.Label(new Rect(Screen.width - 245, 130, 250, 30), "Alt : LookAt Camera");
    }


    // キャラクターのコライダーサイズのリセット関数
    void resetCollider()
    {
        // コンポーネントのHeight、Centerの初期値を戻す
        col.height = orgColHight;
        col.center = orgVectColCenter;
    }

    //void Update()
    //{
    //    //地面に触れている場合発動
    //    if (ground)
    //    {
    //        //上下左右のキーでの移動、向き、アニメーション
    //        if (Input.GetKey(KeyCode.RightArrow))
    //        {
    //            //移動(X軸、Y軸、Z軸）
    //            rb.velocity = new Vector3(speed, 0, 0);
    //            //向き(X軸、Y軸、Z軸）
    //            transform.rotation = Quaternion.Euler(0, 90, 0);
    //            //アニメーション
    //            anim.SetBool("Run", true);
    //        }
    //        else if (Input.GetKey(KeyCode.LeftArrow))
    //        {
    //            rb.velocity = new Vector3(-speed, 0, 0);
    //            transform.rotation = Quaternion.Euler(0, 270, 0);
    //            anim.SetBool("Run", true);
    //        }
    //        else if (Input.GetKey(KeyCode.UpArrow))
    //        {
    //            rb.velocity = new Vector3(0, 0, speed);
    //            transform.rotation = Quaternion.Euler(0, 0, 0);
    //            anim.SetBool("Run", true);
    //        }

    //        else if (Input.GetKey(KeyCode.DownArrow))
    //        {
    //            rb.velocity = new Vector3(0, 0, -speed);
    //            transform.rotation = Quaternion.Euler(0, 180, 0);
    //            anim.SetBool("Run", true);
    //        }
    //        //何もキーを押していない時はアニメーションをオフにする
    //        else
    //        {
    //            anim.SetBool("Run", false);
    //        }

    //        //スペースキーでジャンプする
    //        if (Input.GetKey(KeyCode.Space))
    //        {
    //            anim.SetBool("Jump", true);
    //            //上方向に向けて力を加える
    //            rb.AddForce(new Vector3(0, thrust, 0));
    //            ground = false;
    //        }
    //        else
    //        {
    //            anim.SetBool("Jump", false);
    //        }
    //    }
    //}

    // 別のCollider、今回はPlaneに触れているかどうかを判断する
    void OnCollisionStay(Collision c)
    {
        isGround = true;
    }
}