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
    private Animator anim;                                          // キャラにアタッチされるアニメーターへの参照
    private Rigidbody rb;
    private CapsuleCollider col;                                    // キャラのカプセルコライダの参照

    public PlayerAnimState playerAnimState;                         // プレイヤーのアニメーターステート
    private AnimatorStateInfo currentAnimState;                     // BaseLayerで使われる、アニメーターの現在の状態の参照

    public float animSpeed = 1.5f;                                  // アニメーション再生速度設定
    public float lookSmoother = 3.0f;                               // a smoothing setting for camera motion
    public bool useCurves = true;                                   // Mecanimでカーブ調整を使うか設定する
                                                                    // このスイッチが入っていないとカーブは使われない
    public float useCurvesHeight = 0.5f;                            // カーブ補正の有効高さ（地面をすり抜けやすい時には大きくする）

    [Serializable]
    public class MovementSettings
    {
        // プレイヤー操作用パラメータ
        public float speed = 3.0f;                                  // 移動速度
        public float jumpForce = 3.0f;                              // ジャンプ力
        public float locoThresh = 0.05f;                            // 歩行のしきい値(inputの入力量)

        public float JumpForce = 30f;
        public AnimationCurve SlopeCurveModifier = new AnimationCurve(new Keyframe(-90.0f, 1.0f), new Keyframe(0.0f, 1.0f), new Keyframe(90.0f, 0.0f));
        [HideInInspector] public float CurrentTargetSpeed = 8f;

#if !MOBILE_INPUT
        public float sneakRate = 0.25f;                             // 忍び歩く際のinputに対する代入値
        public float walkRate = 0.5f;                               // 歩く際のinputに対する代入値
#endif
    }

    [Serializable]
    public class AdvancedSettings
    {
        public float groundThresh = 0.01f;                          // 接地判定のしきい値(コリジョンから地面までの距離)
        public float stickToGroundHelperDistance = 0.5f; // stops the character
        public float slowDownRate = 20f; // rate at which the controller comes to a stop when there is no input
        public bool airControl; // can the user control the direction that is being moved in the air
        [Tooltip("set it to 0.1 or more if you get stuck in wall")]
        public float shellOffset; //reduce the radius by that ratio to avoid getting stuck in wall (a value of 0.1f is nice)
    }

    public MovementSettings movSet = new MovementSettings();
    //public MouseLook mouseLook = new MouseLook();
    public AdvancedSettings advSet = new AdvancedSettings();

    private Vector3 velocity;                                       // プレイヤーの移動量

    private float m_YRotation;
    private Vector3 locoMove;
    private Vector3 groundNormal;
    private bool m_Jump, m_PreviouslyGrounded, m_Jumping, isGrounded;

    // CapsuleColliderで設定されているコライダのHeight、Centerの初期値を収める変数
    private float orgColHight;
    private Vector3 orgVectColCenter;

    private GameObject mainCamera;                                // メインカメラへの参照

    public Vector3 Velocity { get { return velocity; } }

    // 初期化
    void Awake()
    {
        anim = GetComponent<Animator>();                            // Animatorコンポーネントを取得する
        anim.speed = animSpeed;                                     // Animatorのモーション再生速度に animSpeedを設定する

        col = GetComponent<CapsuleCollider>();                      // CapsuleColliderコンポーネントを取得する(カプセル型コリジョン)
        rb = GetComponent<Rigidbody>();

        mainCamera = GameObject.FindWithTag("MainCamera");          //メインカメラを取得する

        // CapsuleColliderコンポーネントのHeight、Centerの初期値を保存する
        orgColHight = col.height;
        orgVectColCenter = col.center;

        velocity = Vector3.zero;                                    // 移動量の初期化
    }

    private void Update()
    {
        //RotateView();

        if (CrossPlatformInputManager.GetButtonDown("Jump") && !m_Jump)
        {
            m_Jump = true;
        }
    }

    // 以下、メイン処理.リジッドボディと絡めるので、FixedUpdate内で処理を行う.
    void FixedUpdate()
    {
        currentAnimState = anim.GetCurrentAnimatorStateInfo(0);     // 参照用のステート変数にBaseLayer(0)の現在のステートを設定する

        CheckGrounded();                                            // 接地判定
        float input = GetInput();                                   // 入力取得

        // キャラクターの移動処理
        if (Mathf.Abs(input) > movSet.locoThresh)                   // inputがしきい値を超えている場合
        {
            anim.SetBool(PlayerAnimState.Locomotion, true);

            locoMove = Vector3.ProjectOnPlane(locoMove, groundNormal);
            rb.velocity = new Vector3(movSet.speed * input, 0, 0);  // 移動速度と方向をベクトルに掛ける

            transform.rotation = Quaternion.Euler
                (0, 90 * Mathf.Sign(input), 0);                     // 方向転換(inputの正負を取得して掛ける)
        }
        else
        {
            anim.SetBool(PlayerAnimState.Locomotion, false);
        }

        // always move along the camera forward as it is the direction that it being aimed at
        Vector3 desiredMove = cam.transform.forward * input.y + cam.transform.right * input.x;
        desiredMove = Vector3.ProjectOnPlane(desiredMove, m_GroundContactNormal).normalized;

        desiredMove.x = desiredMove.x * movementSettings.CurrentTargetSpeed;
        desiredMove.z = desiredMove.z * movementSettings.CurrentTargetSpeed;
        desiredMove.y = desiredMove.y * movementSettings.CurrentTargetSpeed;
        if (m_RigidBody.velocity.sqrMagnitude <
            (movementSettings.CurrentTargetSpeed * movementSettings.CurrentTargetSpeed))
        {
            m_RigidBody.AddForce(desiredMove * SlopeMultiplier(), ForceMode.Impulse);
        }

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



        if ((Mathf.Abs(input) > float.Epsilon || Mathf.Abs(input) > float.Epsilon) && (advancedSettings.airControl || m_IsGrounded))
        {
            // always move along the camera forward as it is the direction that it being aimed at
            Vector3 desiredMove = cam.transform.forward * input.y + cam.transform.right * input.x;
            desiredMove = Vector3.ProjectOnPlane(desiredMove, m_GroundContactNormal).normalized;

            desiredMove.x = desiredMove.x * movementSettings.CurrentTargetSpeed;
            desiredMove.z = desiredMove.z * movementSettings.CurrentTargetSpeed;
            desiredMove.y = desiredMove.y * movementSettings.CurrentTargetSpeed;
            if (m_RigidBody.velocity.sqrMagnitude <
                (movementSettings.CurrentTargetSpeed * movementSettings.CurrentTargetSpeed))
            {
                m_RigidBody.AddForce(desiredMove * SlopeMultiplier(), ForceMode.Impulse);
            }
        }

        if (m_IsGrounded)
        {
            m_RigidBody.drag = 5f;

            if (m_Jump)
            {
                m_RigidBody.drag = 0f;
                m_RigidBody.velocity = new Vector3(m_RigidBody.velocity.x, 0f, m_RigidBody.velocity.z);
                m_RigidBody.AddForce(new Vector3(0f, movementSettings.JumpForce, 0f), ForceMode.Impulse);
                m_Jumping = true;
            }

            if (!m_Jumping && Mathf.Abs(input.x) < float.Epsilon && Mathf.Abs(input.y) < float.Epsilon && m_RigidBody.velocity.magnitude < 1f)
            {
                m_RigidBody.Sleep();
            }
        }
        else
        {
            m_RigidBody.drag = 0f;
            if (m_PreviouslyGrounded && !m_Jumping)
            {
                StickToGroundHelper();
            }
        }
        m_Jump = false;
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
        isGrounded = true;
    }

    private float GetInput()
    {
        float input = Input.GetAxis("Horizontal");                  // 水平入力をinputとして定義

        // PC操作時の入力分岐
#if !MOBILE_INPUT
        if (Mathf.Abs(input) > movSet.locoThresh)
        {
            if (Input.GetButton("Sneak"))
                input = movSet.sneakRate;                           // 忍び歩く
            
            if (Input.GetButton("Walk"))
                input = movSet.walkRate;                            // 歩く
        }
#endif
        anim.SetFloat("Speed", input);                              // Animatorの"Speed"パラメータにinputを渡す

        return input;
    }

    private void CheckGrounded()
    {
        m_PreviouslyGrounded = isGrounded;
        RaycastHit hitInfo;
        if (Physics.SphereCast(transform.position, col.radius * (1.0f - advSet.shellOffset), Vector3.down, out hitInfo,
                               ((col.height / 2f) - col.radius) + advSet.groundThresh, Physics.AllLayers, QueryTriggerInteraction.Ignore))
        {
            isGrounded = true;
            m_GroundContactNormal = hitInfo.normal;
        }
        else
        {
            m_IsGrounded = false;
            m_GroundContactNormal = Vector3.up;
        }
        if (!m_PreviouslyGrounded && m_IsGrounded && m_Jumping)
        {
            m_Jumping = false;
        }
    }
}