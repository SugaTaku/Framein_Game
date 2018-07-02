using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityStandardAssets.CrossPlatformInput;

// 必要なコンポーネントの列記
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]

public class AnimationManager : MonoBehaviour
{
    private Animator anim;                                                      // キャラにアタッチされるアニメーターへの参照
    private Rigidbody rb;
    private CapsuleCollider col;                                                // キャラのカプセルコライダの参照

    private AnimatorStateInfo currentAnimState;                                 // BaseLayerで使われる、アニメーターの現在の状態の参照

    public float animSpeed = 1.5f;                                              // アニメーション再生速度設定
    public float lookSmoother = 3.0f;                                           // a smoothing setting for camera motion
    public bool useCurves = true;                                               // Mecanimでカーブ調整を使うか設定する
                                                                                // このスイッチが入っていないとカーブは使われない
    public float useCurvesHeight = 0.5f;                                        // カーブ補正の有効高さ（地面をすり抜けやすい時には大きくする）

    [Serializable]
    public class MovementSettings
    {
        // プレイヤー操作用パラメータ
        public float speed = 3.0f;                                              // 移動速度
        public float jumpForce = 30f;                                           // ジャンプ力
        public float locoThresh = 0.05f;                                        // 歩行のしきい値(inputの入力量)

        public AnimationCurve SlopeCurve = new AnimationCurve
            (new Keyframe(-90.0f, 1.0f),
             new Keyframe(0.0f, 1.0f),
             new Keyframe(90.0f, 0.0f));
        
        public float targetSpeed;

#if !MOBILE_INPUT
        public float sneakRate = 0.15f;                                         // 忍び歩く際のinputに対する代入値
        public float walkRate = 0.5f;                                           // 歩く際のinputに対する代入値
#endif
    }

    [Serializable]
    public class AdvancedSettings
    {
        public float groundThresh = 0.01f;                                      // 接地判定のしきい値(コリジョンから地面までの距離)
        public float slowDownRate = 20f;                                        // rate at which the controller comes to a stop when there is no input
        public bool airControl;                                                 // can the user control the direction that is being moved in the air
        public float castOffset;                                                // SphereCastのサイズオフセット(0.1がデフォルト)
    }

    public MovementSettings movSet = new MovementSettings();
    //public MouseLook mouseLook = new MouseLook();
    public AdvancedSettings advSet = new AdvancedSettings();

    public Vector3 velocity;                                                    // プレイヤーの移動量

    private float m_YRotation;
    private Vector3 locoMove;
    private Vector3 groundNormal;

    private bool Jump, Squat;
    public bool isJumping, isGrounded;

    // CapsuleColliderで設定されているコライダのHeight、Centerの初期値を収める変数
    private float orgColHight;
    private Vector3 orgVectColCenter;

    private GameObject mainCamera;                                              // メインカメラへの参照

    public Vector3 Velocity { get { return velocity; } }

    // 初期化
    void Awake()
    {
        anim = GetComponent<Animator>();                                        // Animatorコンポーネントを取得する
        anim.speed = animSpeed;                                                 // Animatorのモーション再生速度に animSpeedを設定する

        col = GetComponent<CapsuleCollider>();                                  // CapsuleColliderコンポーネントを取得する(カプセル型コリジョン)
        rb = GetComponent<Rigidbody>();

        if (Camera.main != null)
            mainCamera = Camera.main.gameObject;                                // メインカメラを取得する

        // CapsuleColliderコンポーネントのHeight、Centerの初期値を保存する
        orgColHight = col.height;
        orgVectColCenter = col.center;

        velocity = rb.velocity;                                                 // 移動量の初期化
    }

    private void Update()
    {
        //RotateView();

        if (!Jump && !Squat)
        {   // スペースキーを入力したら
            // ステート遷移中でなかったらジャンプできる
            //if (!anim.IsInTransition(0))
            //{
            Jump = CrossPlatformInputManager.GetButtonDown("Jump");
            anim.SetBool(PlayerAnimState.Jump, Jump);                           // Animatorにジャンプに切り替えるフラグを送る
            //}
        }

        //if (!Squat)
        //{   // スペースキーを入力したら
            // ステート遷移中でなかったらジャンプできる
            //if (!anim.IsInTransition(0))
            //{
            Squat = CrossPlatformInputManager.GetButton("Squat");
            anim.SetBool(PlayerAnimState.Squat, Squat);                         // Animatorにジャンプに切り替えるフラグを送る
            //}
        //}


    }

    // 以下、メイン処理.リジッドボディと絡めるので、FixedUpdate内で処理を行う.
    void FixedUpdate()
    {
        velocity = rb.velocity; 
        currentAnimState = anim.GetCurrentAnimatorStateInfo(0);                 // 参照用のステート変数にBaseLayer(0)の現在のステートを設定する

        CheckGrounded();                                                        // 接地判定
        float input = GetInput();                                               // 入力取得

        // inputがしきい値を超えている、かつしゃがんでいない場合
        if (Mathf.Abs(input) > movSet.locoThresh && !Squat)
        {
            locoMove = Vector3.ProjectOnPlane(mainCamera.transform.right * input, groundNormal).normalized;
            // 地面の傾きと入力に沿った正規化ベクトルを返す
            
            locoMove *= movSet.targetSpeed;
            // targetSpeedを反映

            // プレイヤーの速度がtargetSpeedを超えていなければ
            if (Mathf.Abs(rb.velocity.x) < movSet.targetSpeed)
            {
                rb.AddForce(locoMove * SlopeMultiplier(), ForceMode.Impulse);
                // 地面の傾きを考慮した力を加える
            }

            transform.rotation = Quaternion.Euler(0, 90 * Mathf.Sign(input), 0);
            // 方向転換(inputの正負を取得して掛ける)
        }

        if (isGrounded)
        {
            rb.drag = 5f;

            if (Jump)
            {
                rb.drag = 0f;
                rb.velocity = new Vector3(rb.velocity.x, 0f, 0f);
                rb.AddForce(new Vector3(0f, movSet.jumpForce, 0f), ForceMode.Impulse);
                isJumping = true;
            }

            if (!isJumping && Mathf.Abs(input) < movSet.locoThresh && rb.velocity.magnitude < 1f)
            {
                rb.Sleep();
            }
        }
        else
        {
            rb.drag = 0f;
        }
        Jump = false;

        //rb.useGravity = true;                                       // ジャンプ中に重力を切るので、それ以外は重力の影響を受けるようにする

        //if (Input.GetButtonDown("Squat"))
        //{   // Nキーを入力したら
        //    // アニメーションのステートがIdleの最中のみしゃがめる
        //    if (currentAnimState.fullPathHash == anim.EnumToHash(PlayerAnimState.Idle))
        //    {
        //        anim.SetBool(PlayerAnimState.Squat, true);          // Animatorにしゃがみに切り替えるフラグを送る
        //    }
        //    else if (currentAnimState.fullPathHash == anim.EnumToHash(PlayerAnimState.Squat))
        //    {
        //        anim.SetBool(PlayerAnimState.Squat, false);
        //    }
        //}

        //// 以下、Animatorの各ステート中での処理
        //// Locomotion中
        //// 現在のベースレイヤーがlocoStateの時
        ////if (currentAnimState.fullPathHash == anim.EnumToHash(PlayerAnimState.Locomotion))
        ////{
        ////    //カーブでコライダ調整をしている時は、念のためにリセットする
        ////    if (useCurves)
        ////    {
        ////        resetCollider();
        ////    }
        ////}

        //// JUMP中の処理
        //// 現在のベースレイヤーがjumpStateの時
        //else if (currentAnimState.fullPathHash == anim.EnumToHash(PlayerAnimState.Jump))
        //{
        //    //cameraObject.SendMessage("setCameraPositionJumpView");  // ジャンプ中のカメラに変更
        //                                                            // ステートがトランジション中でない場合
        //    if (!anim.IsInTransition(0))
        //    {
        //        // 以下、カーブ調整をする場合の処理
        //        if (useCurves)
        //        {
        //            // 以下JUMP00アニメーションについているカーブJumpHeightとGravityControl
        //            // JumpHeight:JUMP00でのジャンプの高さ（0〜1）
        //            // GravityControl:1⇒ジャンプ中（重力無効）、0⇒重力有効
        //            float jumpHeight = anim.GetFloat("JumpHeight");
        //            float gravityControl = anim.GetFloat("GravityControl");
        //            //if (gravityControl > 0)
        //                //rb.useGravity = false;  //ジャンプ中の重力の影響を切る

        //            // レイキャストをキャラクターのセンターから落とす
        //            Ray ray = new Ray(transform.position + Vector3.up, -Vector3.up);
        //            RaycastHit hitInfo = new RaycastHit();
        //            // 高さが useCurvesHeight 以上ある時のみ、コライダーの高さと中心をJUMP00アニメーションについているカーブで調整する
        //            if (Physics.Raycast(ray, out hitInfo))
        //            {
        //                if (hitInfo.distance > useCurvesHeight)
        //                {
        //                    col.height = orgColHight - jumpHeight;          // 調整されたコライダーの高さ
        //                    float adjCenterY = orgVectColCenter.y + jumpHeight;
        //                    col.center = new Vector3(0, adjCenterY, 0); // 調整されたコライダーのセンター
        //                }
        //                else
        //                {
        //                    // 閾値よりも低い時には初期値に戻す（念のため）                   
        //                    resetCollider();
        //                }
        //            }
        //        }
        //        // Jump bool値をリセットする（ループしないようにする）               
        //        anim.SetBool("Jump", false);
        //    }
        //}
        //// IDLE中の処理
        //// 現在のベースレイヤーがidleStateの時
        //else if (currentAnimState.fullPathHash == anim.EnumToHash(PlayerAnimState.Idle))
        //{
        //    //カーブでコライダ調整をしている時は、念のためにリセットする
        //    if (useCurves)
        //    {
        //        resetCollider();
        //    }
        //    // スペースキーを入力したらRest状態になる
        //    if (Input.GetButtonDown("Jump"))
        //    {
        //        anim.SetBool("Rest", true);
        //    }
        //}
        //// REST中の処理
        //// 現在のベースレイヤーがrestStateの時
        //else if (currentAnimState.fullPathHash == anim.EnumToHash(PlayerAnimState.Squat))
        //{
        //    //cameraObject.SendMessage("setCameraPositionFrontView");       // カメラを正面に切り替える
        //    // ステートが遷移中でない場合、Rest bool値をリセットする（ループしないようにする）
        //    if (!anim.IsInTransition(0))
        //    {
        //        anim.SetBool("Rest", false);
        //    }
        //}
    }

    void OnGUI()
    {
        GUI.Box(new Rect(Screen.width - 260, 10, 250, 150), "Interaction");
        GUI.Label(new Rect(Screen.width - 245, 30, 250, 30), "A/Dキー : 左右移動");
        GUI.Label(new Rect(Screen.width - 245, 50, 250, 30), "走りながら右Shiftキー : 走る");
        GUI.Label(new Rect(Screen.width - 245, 70, 250, 30), "走りながらCキー : 忍び歩く");
        GUI.Label(new Rect(Screen.width - 245, 90, 250, 30), "WもしくはSpaceキー : ジャンプする");
        GUI.Label(new Rect(Screen.width - 245, 110, 250, 30), "Sキー : しゃがむ");
        //GUI.Label(new Rect(Screen.width - 245, 130, 250, 30), "Alt : LookAt Camera");
    }


    // キャラクターのコライダーサイズのリセット関数
    //void resetCollider()
    //{
    //    // コンポーネントのHeight、Centerの初期値を戻す
    //    col.height = orgColHight;
    //    col.center = orgVectColCenter;
    //}

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

    private float SlopeMultiplier()
    {
        float angle = Vector3.Angle(groundNormal, Vector3.up);
        return movSet.SlopeCurve.Evaluate(angle);
    }

    private float GetInput()
    {
        float input = Input.GetAxis("Horizontal");                  // 水平入力をinputとして定義

        if (Mathf.Abs(input) > movSet.locoThresh)
        {
            // PC操作時の入力分岐
#if !MOBILE_INPUT
            if (Input.GetButton("Sneak"))
                input *= movSet.sneakRate;                          // 忍び歩く
            else if (!Input.GetButton("Run"))
                input *= movSet.walkRate;                           // 歩く
#endif

            anim.SetBool(PlayerAnimState.Locomotion, true);
        }
        else
            anim.SetBool(PlayerAnimState.Locomotion, false);

        movSet.targetSpeed = movSet.speed * Mathf.Abs(input);       // targetSpeedを設定
        anim.SetFloat("Speed", input);                              // Animatorの"Speed"パラメータにinputを渡す

        return input;
    }

    // 接地判定
    private void CheckGrounded()
    {
        bool isPrevGrounded = isGrounded;
        float castRadius = col.radius * (1.0f - advSet.castOffset);
        RaycastHit hitInfo;

        // 足元にSphereを飛ばし接地判定
        if (Physics.SphereCast(transform.position + Vector3.up * castRadius, castRadius, Vector3.down, out hitInfo,
                               castRadius + advSet.groundThresh, Physics.AllLayers, QueryTriggerInteraction.Ignore))
        {
            isGrounded = true;
            groundNormal = hitInfo.normal;
            //anim.applyRootMotion = true;
        }
        else
        {
            isGrounded = false;
            groundNormal = Vector3.up;
            //anim.applyRootMotion = false;
        }
        if (!isPrevGrounded && isGrounded && isJumping)
        {
            isJumping = false;
        }
    }
}