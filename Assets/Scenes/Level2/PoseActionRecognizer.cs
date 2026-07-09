using System;
using UnityEngine;

public class PoseActionRecognizer : MonoBehaviour
{
    // 自动寻找场景里的 PipeServer。一般保持 true，除非你想手动指定某个 PipeServer。
    private const bool AutoFindPipeServer = true;

    // 是否在 Console 输出识别到的 SocialIntent。调试时 true，正式演示嫌刷屏可以改 false。
    private const bool LogDetectedIntent = true;

    // 同一个动作的冷却时间，避免一次动作连续刷很多次日志/事件。
    // 调小：同一动作可以更频繁触发；调大：同一动作触发更克制。
    private const float SameIntentCooldown = 1f;

    // 是否开启举手检测。你现在主要测挥手，所以这里可以保持 false。
    private const bool DetectRaiseHand = false;

    // 举手高度余量。公式：wrist.y > shoulder.y + RaiseHandVerticalMargin。
    // 调小：更容易识别举手；调大：必须举得更高才识别。
    private const float RaiseHandVerticalMargin = 0.05f;

    // 举手保持时间。调小：更灵敏但更容易误触发；调大：更稳定但反应慢。
    private const float RaiseHandHoldSeconds = 1f;

    // 是否开启挥手邀请检测。
    private const bool DetectWaveInvite = true;

    // 单次有效左右移动的最小横向距离。公式：abs(currentRelativeX - lastRelativeX) >= WaveMinHorizontalDelta。
    // 调小：挥手更灵敏，小幅摆动也能识别；调大：需要更大幅度挥手，误触发更少。
    private const float WaveMinHorizontalDelta = 0.08f;

    // 需要检测到几次“方向反转”。1 表示左->右或右->左即可；2 表示左->右->左才触发。
    // 你反馈不灵敏，所以先用 1；如果误触发多，再改回 2。
    private const int WaveRequiredDirectionChanges = 2;

    // 挥手检测时间窗，单位秒。只有在这个时间内完成方向反转，才算挥手。
    // 调大：慢一点的挥手也能识别；调小：动作必须更快更干脆。
    private const float WaveWindowSeconds = 1.25f;

    // 手腕允许低于肩膀的高度容差。公式：wrist.y > shoulder.y - WaveShoulderVerticalTolerance。
    // 调大：手不用举得太高也能挥手；调小：必须接近肩膀高度，误触发更少。
    private const float WaveShoulderVerticalTolerance = 0.25f;

    // 是否开启等待区域检测。需要配置 waitZone 后再打开。
    private const bool DetectWaitInZone = false;

    // 在等待区域内需要停留多久才触发 WaitInZone。
    private const float WaitHoldSeconds = 1.5f;

    // 是否开启面向 NPC 检测。需要配置 npcTarget 后再打开。
    private const bool DetectFaceAndAttend = false;

    // 面向 NPC 需要持续多久才触发 FaceAndAttend。
    private const float FaceHoldSeconds = 1f;

    // 面向 NPC 的点积阈值。公式：Dot(bodyForward, toNpc) >= FaceDotThreshold。
    // 越接近 1 要求越正对；越接近 0 越宽松。
    private const float FaceDotThreshold = 0.6f;

    // 如果发现“背对 NPC 反而触发”，说明估计方向反了，把这里改 true。
    private const bool InvertEstimatedBodyForward = false;

    [Header("Pose Source")]
    [SerializeField] private PipeServer pipeServer;
    public SocialIntentUnityEvent onIntentDetected = new SocialIntentUnityEvent();

    [Header("Wait In Zone References")]
    [SerializeField] private Transform waitSubject;
    [SerializeField] private Collider waitZone;

    [Header("Face And Attend References")]
    [SerializeField] private Transform npcTarget;

    public SocialIntent CurrentIntent { get; private set; } = SocialIntent.None;
    public float CurrentConfidence { get; private set; }

    private float leftRaiseTimer;
    private float rightRaiseTimer;
    private float waitTimer;
    private float faceTimer;

    private HandWaveState leftWave = new HandWaveState();
    private HandWaveState rightWave = new HandWaveState();

    private SocialIntent lastEmittedIntent = SocialIntent.None;
    private float lastEmitTime = -999f;

    private void Awake()
    {
        if (pipeServer == null && AutoFindPipeServer)
        {
            pipeServer = FindObjectOfType<PipeServer>();
        }
    }

    private void Update()
    {
        if (pipeServer == null)
        {
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

        if (DetectFaceAndAttend)
        {
            TickFaceAndAttend();
        }
    }

    private void TickRaiseHand()
    {
        // 举手检测原理：
        // 1. 分别比较同侧手腕和肩膀的世界坐标高度。
        // 2. 判定公式：wrist.y > shoulder.y + RaiseHandVerticalMargin。
        // 3. RaiseHandVerticalMargin 用来留出安全距离，避免手腕刚好贴近肩膀时误判。
        // 4. 条件需要连续保持 RaiseHandHoldSeconds 秒，避免单帧抖动触发。
        // 5. confidence = holdTime / RaiseHandHoldSeconds，限制在 0-1。
        bool leftRaised = IsWristAboveShoulder(Landmark.LEFT_WRIST, Landmark.LEFT_SHOULDER, RaiseHandVerticalMargin);
        bool rightRaised = IsWristAboveShoulder(Landmark.RIGHT_WRIST, Landmark.RIGHT_SHOULDER, RaiseHandVerticalMargin);

        leftRaiseTimer = UpdateHoldTimer(leftRaiseTimer, leftRaised);
        rightRaiseTimer = UpdateHoldTimer(rightRaiseTimer, rightRaised);

        if (leftRaiseTimer >= RaiseHandHoldSeconds || rightRaiseTimer >= RaiseHandHoldSeconds)
        {
            float confidence = Mathf.Clamp01(Mathf.Max(leftRaiseTimer, rightRaiseTimer) / RaiseHandHoldSeconds);
            EmitIntent(SocialIntent.RaiseHand, confidence);

            leftRaiseTimer = 0f;
            rightRaiseTimer = 0f;
        }
    }

    private void TickWaveInvite()
    {
        // 挥手邀请检测入口：
        // 1. 先要求手腕在肩膀附近或肩膀上方，避免把自然垂手摆动误判成挥手。
        // 2. 准备条件公式：wrist.y > shoulder.y - WaveShoulderVerticalTolerance。
        // 3. 左右手分别进入 TickWaveHand，任何一只手满足挥手规则就触发 WaveInvite。
        bool leftReady = IsWristNearOrAboveShoulder(
            Landmark.LEFT_WRIST, Landmark.LEFT_SHOULDER, WaveShoulderVerticalTolerance);
        bool rightReady = IsWristNearOrAboveShoulder(
            Landmark.RIGHT_WRIST, Landmark.RIGHT_SHOULDER, WaveShoulderVerticalTolerance);

        if (TickWaveHand(leftWave, Landmark.LEFT_WRIST, Landmark.LEFT_SHOULDER, leftReady) ||
            TickWaveHand(rightWave, Landmark.RIGHT_WRIST, Landmark.RIGHT_SHOULDER, rightReady))
        {
            EmitIntent(SocialIntent.WaveInvite, 1f);
        }
    }

    private bool TickWaveHand(HandWaveState state, Landmark wristMark, Landmark shoulderMark, bool handReady)
    {
        // 单手挥手检测原理：
        // 1. 使用相对横向位移，避免身体整体左右移动造成误判。
        //    relativeX = wrist.x - shoulder.x
        // 2. 当前帧横向变化：
        //    delta = relativeX - lastRelativeX
        // 3. 当 abs(delta) >= WaveMinHorizontalDelta 时，认为手发生了一次有效左右移动。
        // 4. direction = Sign(delta)，即 +1 表示向一侧移动，-1 表示向另一侧移动。
        // 5. 如果 direction 和上一次有效方向相反，则 directionChanges + 1。
        // 6. 在 WaveWindowSeconds 时间窗内，方向反转次数达到 WaveRequiredDirectionChanges，判定为挥手。
        // 调参建议：误触发太多就增大 WaveMinHorizontalDelta 或 WaveRequiredDirectionChanges；
        // 检测太迟钝就减小 WaveMinHorizontalDelta 或增大 WaveWindowSeconds。
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
            Debug.Log($"HandWave: direction change detected. Total changes: {state.DirectionChanges}");
        }

        if (direction != 0)
        {
            state.LastDirection = direction;
            state.LastRelativeX = relativeX;
        }

        if (state.DirectionChanges >= WaveRequiredDirectionChanges)
        {
            state.Reset();
            return true;
        }

        return false;
    }

    private void TickWaitInZone()
    {
        // 等待区域检测原理：
        // 1. 默认使用虚拟髋部 virtualHip 作为人体中心，也可以通过 waitSubject 指定其他对象。
        // 2. 使用 Collider.ClosestPoint 判断 subject 是否位于 waitZone 内。
        // 3. 内部判定公式：
        //    closest = waitZone.ClosestPoint(subject.position)
        //    inside = (closest - subject.position).sqrMagnitude < epsilon
        // 4. inside 需要连续保持 WaitHoldSeconds 秒，才触发 WaitInZone。
        // 5. 这个规则适合“站到排队空位并等待”的社交任务。
        Transform subject = waitSubject != null ? waitSubject : pipeServer.GetVirtualHip();
        bool inZone = subject != null && waitZone != null && IsInsideCollider(waitZone, subject.position);

        waitTimer = UpdateHoldTimer(waitTimer, inZone);

        if (waitTimer >= WaitHoldSeconds)
        {
            EmitIntent(SocialIntent.WaitInZone, Mathf.Clamp01(waitTimer / WaitHoldSeconds));
            waitTimer = 0f;
        }
    }

    private void TickFaceAndAttend()
    {
        // 面向并关注 NPC 检测原理：
        // 1. 先用肩膀轴和脊柱轴估计身体朝向 bodyForward。
        // 2. 再计算身体中心指向 NPC 的方向 toNpc。
        // 3. 使用点积判断朝向接近程度：dot = Dot(normalize(bodyForward), normalize(toNpc))。
        // 4. 判定公式：dot >= FaceDotThreshold。
        // 5. FaceDotThreshold 越接近 1，要求越正对 NPC；越接近 0，允许侧身。
        // 6. 条件需要连续保持 FaceHoldSeconds 秒，才触发 FaceAndAttend。
        // 7. 如果发现正反方向相反，把 InvertEstimatedBodyForward 改为 true。
        if (npcTarget == null)
        {
            faceTimer = 0f;
            return;
        }

        Vector3 bodyForward = EstimateBodyForward();
        Vector3 bodyCenter = pipeServer.GetVirtualNeck().position;
        Vector3 toNpc = npcTarget.position - bodyCenter;
        toNpc.y = 0f;

        bool facing = bodyForward.sqrMagnitude > 0.0001f
            && toNpc.sqrMagnitude > 0.0001f
            && Vector3.Dot(bodyForward.normalized, toNpc.normalized) >= FaceDotThreshold;

        faceTimer = UpdateHoldTimer(faceTimer, facing);

        if (faceTimer >= FaceHoldSeconds)
        {
            EmitIntent(SocialIntent.FaceAndAttend, Mathf.Clamp01(faceTimer / FaceHoldSeconds));
            faceTimer = 0f;
        }
    }

    private bool IsWristAboveShoulder(Landmark wristMark, Landmark shoulderMark, float margin)
    {
        // 举手基础公式：wrist.y > shoulder.y + margin。
        // margin 是高度余量，用于抵消关键点抖动和轻微耸肩。
        return GetPosition(wristMark).y > GetPosition(shoulderMark).y + margin;
    }

    private bool IsWristNearOrAboveShoulder(Landmark wristMark, Landmark shoulderMark, float tolerance)
    {
        // 挥手准备公式：wrist.y > shoulder.y - tolerance。
        // tolerance 允许手腕略低于肩膀，适合儿童挥手幅度较小的情况。
        return GetPosition(wristMark).y > GetPosition(shoulderMark).y - tolerance;
    }

    private Vector3 EstimateBodyForward()
    {
        // 身体朝向估计：
        // shoulderAxis = rightShoulder - leftShoulder，表示左右肩连线。
        // spineAxis = shoulderCenter - hipCenter，表示躯干向上方向。
        // forward = Cross(shoulderAxis, spineAxis)，用叉乘估计人体正面方向。
        // 注意：不同镜像/坐标设置可能导致 forward 反向，可用 InvertEstimatedBodyForward 修正。
        Vector3 leftShoulder = GetPosition(Landmark.LEFT_SHOULDER);
        Vector3 rightShoulder = GetPosition(Landmark.RIGHT_SHOULDER);
        Vector3 leftHip = GetPosition(Landmark.LEFT_HIP);
        Vector3 rightHip = GetPosition(Landmark.RIGHT_HIP);

        Vector3 shoulderAxis = rightShoulder - leftShoulder;
        Vector3 spineAxis = ((leftShoulder + rightShoulder) * 0.5f) - ((leftHip + rightHip) * 0.5f);
        Vector3 forward = Vector3.Cross(shoulderAxis, spineAxis);
        if (InvertEstimatedBodyForward)
        {
            forward = -forward;
        }
        forward.y = 0f;
        return forward;
    }

    private Vector3 GetPosition(Landmark mark)
    {
        return pipeServer.GetLandmark(mark).position;
    }

    private float UpdateHoldTimer(float timer, bool condition)
    {
        // 持续时间滤波：condition 为真时累加 Time.deltaTime；一旦为假就清零。
        // 这样可以避免单帧抖动直接触发动作。
        return condition ? timer + Time.deltaTime : 0f;
    }

    private bool IsInsideCollider(Collider zone, Vector3 point)
    {
        // Collider 内部判断：Unity 的 ClosestPoint 对内部点会返回点本身或非常接近的位置。
        // 因此用距离平方接近 0 判断 point 是否在 Collider 内。
        Vector3 closest = zone.ClosestPoint(point);
        return (closest - point).sqrMagnitude < 0.0001f;
    }

    private void EmitIntent(SocialIntent intent, float confidence)
    {
        if (intent == lastEmittedIntent && Time.time - lastEmitTime < SameIntentCooldown)
        {
            return;
        }

        CurrentIntent = intent;
        CurrentConfidence = confidence;
        lastEmittedIntent = intent;
        lastEmitTime = Time.time;

        if (LogDetectedIntent)
        {
            Debug.Log($"SocialIntent detected: {intent} ({confidence:0.00})");
        }

        onIntentDetected?.Invoke(intent);
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
