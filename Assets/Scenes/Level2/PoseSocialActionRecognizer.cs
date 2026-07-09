using System;
using UnityEngine;
using UnityEngine.Events;

[Serializable]
public class PoseSocialActionUnityEvent : UnityEvent<SocialIntent, ChildHandSide, AvatarHandSide, float> { }

public class PoseSocialActionRecognizer : MonoBehaviour
{
    private const bool AutoFindPipeServer = true;
    private const bool AutoFindModeManager = true;
    private const bool LogDetectedIntent = true;

    // 同一个动作的冷却时间，避免一次动作连续刷屏或重复触发 NPC 反馈。
    private const float SameIntentCooldown = 0.8f;

    // 举手检测开关。第一版先启用 RaiseHand / WaveInvite / WaitInZone。
    private const bool DetectRaiseHand = true;

    // 举手高度余量。公式：wrist.y > shoulder.y + RaiseHandVerticalMargin。
    // 调小：更容易识别举手；调大：必须举得更高才识别。
    private const float RaiseHandVerticalMargin = 0.05f;

    // 举手保持时间。调小更灵敏，调大更稳定。
    private const float RaiseHandHoldSeconds = 0.35f;

    private const bool DetectWaveInvite = true;

    // 单次有效左右移动的最小横向距离。
    // 公式：abs((wrist.x - shoulder.x) - lastRelativeX) >= WaveMinHorizontalDelta。
    // 调小更灵敏；调大能减少把普通手部抖动误判成挥手。
    private const float WaveMinHorizontalDelta = 0.05f;

    // 方向反转次数。1 表示左->右或右->左即可触发，适合儿童动作幅度较小的测试。
    // 如果误触发太多，优先改成 2。
    private const int WaveRequiredDirectionChanges = 1;

    // 挥手时间窗。调大能容纳慢挥手；调小要求动作更快。
    private const float WaveWindowSeconds = 1.8f;

    // 手腕允许低于肩膀的高度容差。
    // 公式：wrist.y > shoulder.y - WaveShoulderVerticalTolerance。
    private const float WaveShoulderVerticalTolerance = 0.25f;

    private const bool DetectWaitInZone = true;

    // 在等待区域内连续停留多久触发 WaitInZone。
    private const float WaitHoldSeconds = 1.2f;

    [Header("Pose Source")]
    [SerializeField] private PipeServer pipeServer;
    [SerializeField] private PoseControlModeManager modeManager;

    [Header("Wait In Zone References")]
    [SerializeField] private Transform waitSubject;
    [SerializeField] private Collider waitZone;

    public PoseSocialActionUnityEvent onSocialActionDetected = new PoseSocialActionUnityEvent();

    public SocialIntent CurrentIntent { get; private set; } = SocialIntent.None;
    public ChildHandSide CurrentChildHand { get; private set; } = ChildHandSide.None;
    public AvatarHandSide CurrentAvatarHand { get; private set; } = AvatarHandSide.None;
    public float CurrentConfidence { get; private set; }

    private float leftRaiseTimer;
    private float rightRaiseTimer;
    private float waitTimer;
    private HandWaveState leftWave = new HandWaveState();
    private HandWaveState rightWave = new HandWaveState();

    private SocialIntent lastIntent = SocialIntent.None;
    private ChildHandSide lastChildHand = ChildHandSide.None;
    private float lastEmitTime = -999f;

    private void Awake()
    {
        if (pipeServer == null && AutoFindPipeServer)
        {
            pipeServer = FindObjectOfType<PipeServer>();
        }

        if (modeManager == null && AutoFindModeManager)
        {
            modeManager = FindObjectOfType<PoseControlModeManager>();
        }
    }

    private void Update()
    {
        if (pipeServer == null || !ShouldRecognizeSocialActions())
        {
            ResetTransientState();
            return;
        }

        if (DetectRaiseHand)
        {
            TickRaiseHand();
        }

        if (DetectWaveInvite)
        {
            TickWaveInvite();
        }

        if (DetectWaitInZone)
        {
            TickWaitInZone();
        }
    }

    private bool ShouldRecognizeSocialActions()
    {
        // 如果场景里还没挂 PoseControlModeManager，就允许单独测试本脚本。
        // 一旦挂了 modeManager，就只有 SocialInteraction 模式会识别社交动作。
        return modeManager == null || modeManager.CurrentMode == PoseControlMode.SocialInteraction;
    }

    private void TickRaiseHand()
    {
        // RaiseHand 检测原理：
        // 1. 分别比较儿童左右手腕和同侧肩膀的高度。
        // 2. 判定公式：wrist.y > shoulder.y + RaiseHandVerticalMargin。
        // 3. 条件连续保持 RaiseHandHoldSeconds 秒后触发，避免单帧抖动。
        // 4. 检测层记录儿童真实手侧；表现层用 MirrorToAvatarHand 做面对面镜像。
        //    儿童右手 -> 角色左手，儿童左手 -> 角色右手。
        bool leftRaised = IsWristAboveShoulder(Landmark.LEFT_WRIST, Landmark.LEFT_SHOULDER, RaiseHandVerticalMargin);
        bool rightRaised = IsWristAboveShoulder(Landmark.RIGHT_WRIST, Landmark.RIGHT_SHOULDER, RaiseHandVerticalMargin);

        leftRaiseTimer = UpdateHoldTimer(leftRaiseTimer, leftRaised);
        rightRaiseTimer = UpdateHoldTimer(rightRaiseTimer, rightRaised);

        if (leftRaiseTimer < RaiseHandHoldSeconds && rightRaiseTimer < RaiseHandHoldSeconds)
        {
            return;
        }

        ChildHandSide childHand = rightRaiseTimer >= leftRaiseTimer ? ChildHandSide.Right : ChildHandSide.Left;
        float holdTime = Mathf.Max(leftRaiseTimer, rightRaiseTimer);
        float confidence = Mathf.Clamp01(holdTime / RaiseHandHoldSeconds);
        EmitIntent(SocialIntent.RaiseHand, childHand, confidence);

        leftRaiseTimer = 0f;
        rightRaiseTimer = 0f;
    }

    private void TickWaveInvite()
    {
        // WaveInvite 检测入口：
        // 1. 先要求手腕在肩膀附近或肩膀上方，避免自然垂手摆动误触发。
        // 2. 准备条件公式：wrist.y > shoulder.y - WaveShoulderVerticalTolerance。
        // 3. 单手在时间窗内出现方向反转，触发邀请挥手。
        bool leftReady = IsWristNearOrAboveShoulder(
            Landmark.LEFT_WRIST, Landmark.LEFT_SHOULDER, WaveShoulderVerticalTolerance);
        bool rightReady = IsWristNearOrAboveShoulder(
            Landmark.RIGHT_WRIST, Landmark.RIGHT_SHOULDER, WaveShoulderVerticalTolerance);

        if (TickWaveHand(leftWave, Landmark.LEFT_WRIST, Landmark.LEFT_SHOULDER, leftReady))
        {
            EmitIntent(SocialIntent.WaveInvite, ChildHandSide.Left, 1f);
        }

        if (TickWaveHand(rightWave, Landmark.RIGHT_WRIST, Landmark.RIGHT_SHOULDER, rightReady))
        {
            EmitIntent(SocialIntent.WaveInvite, ChildHandSide.Right, 1f);
        }
    }

    private bool TickWaveHand(HandWaveState state, Landmark wristMark, Landmark shoulderMark, bool handReady)
    {
        // 单手挥手公式：
        // relativeX = wrist.x - shoulder.x
        // delta = relativeX - lastRelativeX
        // abs(delta) >= WaveMinHorizontalDelta 时，认为发生一次有效横向移动。
        // direction = Sign(delta)
        // direction 和上一次有效 direction 相反时，directionChanges + 1。
        // directionChanges >= WaveRequiredDirectionChanges 且发生在 WaveWindowSeconds 内，判定挥手。
        if (!handReady)
        {
            state.Reset();
            return false;
        }

        float now = Time.time;
        float relativeX = GetPosition(wristMark).x - GetPosition(shoulderMark).x;

        if (!state.HasSample || now - state.WindowStartTime > WaveWindowSeconds)
        {
            state.Reset(now, relativeX);
            return false;
        }

        float delta = relativeX - state.LastRelativeX;
        int direction = Mathf.Abs(delta) >= WaveMinHorizontalDelta ? Math.Sign(delta) : 0;

        if (direction != 0 && state.LastDirection != 0 && direction != state.LastDirection)
        {
            state.DirectionChanges++;
        }

        if (direction != 0)
        {
            state.LastDirection = direction;
            state.LastRelativeX = relativeX;
        }

        if (state.DirectionChanges < WaveRequiredDirectionChanges)
        {
            return false;
        }

        state.Reset();
        return true;
    }

    private void TickWaitInZone()
    {
        // WaitInZone 检测原理：
        // 1. 默认用 virtualHip 作为人体中心，也可以用 waitSubject 指定玩家或检测对象。
        // 2. 用 Collider.ClosestPoint 判断点是否在 waitZone 内。
        // 3. inside = (waitZone.ClosestPoint(point) - point).sqrMagnitude < epsilon。
        // 4. inside 连续保持 WaitHoldSeconds 秒后触发。
        Transform subject = waitSubject != null ? waitSubject : pipeServer.GetVirtualHip();
        bool inZone = subject != null && waitZone != null && IsInsideCollider(waitZone, subject.position);

        waitTimer = UpdateHoldTimer(waitTimer, inZone);

        if (waitTimer >= WaitHoldSeconds)
        {
            EmitIntent(SocialIntent.WaitInZone, ChildHandSide.None, Mathf.Clamp01(waitTimer / WaitHoldSeconds));
            waitTimer = 0f;
        }
    }

    private void EmitIntent(SocialIntent intent, ChildHandSide childHand, float confidence)
    {
        if (intent == lastIntent && childHand == lastChildHand && Time.time - lastEmitTime < SameIntentCooldown)
        {
            return;
        }

        AvatarHandSide avatarHand = MirrorToAvatarHand(childHand);

        CurrentIntent = intent;
        CurrentChildHand = childHand;
        CurrentAvatarHand = avatarHand;
        CurrentConfidence = confidence;

        lastIntent = intent;
        lastChildHand = childHand;
        lastEmitTime = Time.time;

        if (LogDetectedIntent)
        {
            Debug.Log($"Pose social action: {intent}, childHand={childHand}, avatarHand={avatarHand}, confidence={confidence:0.00}");
        }

        onSocialActionDetected?.Invoke(intent, childHand, avatarHand, confidence);
    }

    private AvatarHandSide MirrorToAvatarHand(ChildHandSide childHand)
    {
        // 面对面镜像规则：
        // 儿童举右手时，画面中的角色应该同步举左手；
        // 儿童举左手时，角色应该同步举右手。
        switch (childHand)
        {
            case ChildHandSide.Left:
                return AvatarHandSide.Right;
            case ChildHandSide.Right:
                return AvatarHandSide.Left;
            default:
                return AvatarHandSide.None;
        }
    }

    private bool IsWristAboveShoulder(Landmark wristMark, Landmark shoulderMark, float margin)
    {
        return GetPosition(wristMark).y > GetPosition(shoulderMark).y + margin;
    }

    private bool IsWristNearOrAboveShoulder(Landmark wristMark, Landmark shoulderMark, float tolerance)
    {
        return GetPosition(wristMark).y > GetPosition(shoulderMark).y - tolerance;
    }

    private Vector3 GetPosition(Landmark mark)
    {
        return pipeServer.GetLandmark(mark).position;
    }

    private float UpdateHoldTimer(float timer, bool condition)
    {
        return condition ? timer + Time.deltaTime : 0f;
    }

    private bool IsInsideCollider(Collider zone, Vector3 point)
    {
        Vector3 closest = zone.ClosestPoint(point);
        return (closest - point).sqrMagnitude < 0.0001f;
    }

    private void ResetTransientState()
    {
        leftRaiseTimer = 0f;
        rightRaiseTimer = 0f;
        waitTimer = 0f;
        leftWave.Reset();
        rightWave.Reset();
    }

    private class HandWaveState
    {
        public bool HasSample;
        public float WindowStartTime;
        public float LastRelativeX;
        public int LastDirection;
        public int DirectionChanges;

        public void Reset()
        {
            HasSample = false;
            WindowStartTime = 0f;
            LastRelativeX = 0f;
            LastDirection = 0;
            DirectionChanges = 0;
        }

        public void Reset(float time, float relativeX)
        {
            HasSample = true;
            WindowStartTime = time;
            LastRelativeX = relativeX;
            LastDirection = 0;
            DirectionChanges = 0;
        }
    }
}
