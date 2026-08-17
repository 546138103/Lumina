using System.Collections;
using System.Collections.Generic;
using StarterAssets;
using UnityEngine;
using UnityEngine.Events;

public enum SocialLessonType
{
    GreetingWave,
    InitiateSpeech,
    WaitThenRespond
}

public enum SocialLessonTaskState
{
    Unavailable,
    Inactive,
    Active,
    Preparing,
    SocialActive,
    NpcSpeaking,
    ResponseWindow,
    CompletionPending,
    Completed
}

public class SocialLessonTaskController : TaskZoneController
{
    [Header("关卡配置")]
    [SerializeField] private SocialLessonType lessonType;
    [Range(0, 3)]
    [SerializeField] private int stageIndex;
    [Min(0.1f)]
    [SerializeField] private float preparationSeconds = 2f;
    [Min(1f)]
    [SerializeField] private float idleGuidanceIntervalSeconds = 20f;

    [Header("共享系统")]
    [SerializeField] private LevelTaskUIController uiController;
    [SerializeField] private PoseControlModeManager modeManager;
    [SerializeField] private PoseSocialActionRecognizer actionRecognizer;

    [Header("进入大圈提示")]
    [SerializeField] private AudioClip taskAreaEntryVoice;
    [SerializeField] private AudioSource taskAudioSource;
    [Min(0f)]
    [SerializeField] private float taskAreaEntryVoiceCooldownSeconds = 3f;

    [Header("第三关：NPC 说话 / 回应循环")]
    [SerializeField] private Animator conversationNpcAnimator;
    [SerializeField] private Transform conversationNpcTransform;
    [SerializeField] private string conversationSpeakingState;
    [SerializeField] private string conversationDefaultState;
    [SerializeField] private AudioClip conversationVoice;
    [SerializeField] private AudioSource conversationAudioSource;
    [Min(0f)]
    [SerializeField] private float conversationFallbackSeconds = 2f;
    [Min(0.1f)]
    [SerializeField] private float responseWindowSeconds = 5f;

    [Header("提示语音（可选）")]
    [SerializeField] private AudioClip idleGuidanceVoice;
    [SerializeField] private AudioClip respondedTooEarlyVoice;
    [Min(0f)]
    [SerializeField] private float respondedTooEarlyCooldownSeconds = 3f;

    [Header("任务消息")]
    public UnityEvent onTaskAreaEntered = new UnityEvent();
    public UnityEvent onTaskAreaExited = new UnityEvent();
    public TaskFloatUnityEvent onPreparationProgress = new TaskFloatUnityEvent();
    public UnityEvent onSocialInteractionStarted = new UnityEvent();
    public UnityEvent onIdleNeedsGuidance = new UnityEvent();
    public UnityEvent onRespondedTooEarly = new UnityEvent();
    public TaskCompletionMethodUnityEvent onCompletionDetected =
        new TaskCompletionMethodUnityEvent();
    public UnityEvent onTaskCompleted = new UnityEvent();
    public UnityEvent onTaskAbandoned = new UnityEvent();

    public SocialLessonTaskState CurrentState { get; private set; } =
        SocialLessonTaskState.Inactive;
    public TaskCompletionMethod CompletionMethod { get; private set; } =
        TaskCompletionMethod.None;
    public float PreparationProgressNormalized { get; private set; }

    private readonly List<TaskZone> registeredZones = new List<TaskZone>();
    private StarterAssetsInputs activePlayer;
    private TaskZone preparationZone;
    private float preparationTimer;
    private float idleGuidanceTimer;
    private float lastTaskAreaEntryVoiceTime = -Mathf.Infinity;
    private float lastTooEarlyFeedbackTime = -Mathf.Infinity;
    private Coroutine conversationRoutine;
    private Quaternion conversationNpcOriginalRotation;
    private bool hasConversationNpcOriginalRotation;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
        if (actionRecognizer != null)
        {
            actionRecognizer.onSocialActionDetected.AddListener(
                HandleSocialAction);
        }
    }

    private void OnDisable()
    {
        if (actionRecognizer != null)
        {
            actionRecognizer.onSocialActionDetected.RemoveListener(
                HandleSocialAction);
        }

        StopConversationLoop();
    }

    private void Update()
    {
        if (!IsTaskAvailable || activePlayer == null)
        {
            return;
        }

        if (CurrentState == SocialLessonTaskState.Preparing)
        {
            TickPreparation();
            return;
        }

        if (CurrentState == SocialLessonTaskState.Active)
        {
            TryBeginPreparationFromOccupiedActionArea();
            return;
        }

        if (CurrentState == SocialLessonTaskState.SocialActive &&
            lessonType != SocialLessonType.WaitThenRespond)
        {
            TickIdleGuidance();
        }
    }

    public override void RegisterZone(TaskZone zone)
    {
        if (zone != null && !registeredZones.Contains(zone))
        {
            registeredZones.Add(zone);
        }
    }

    public override void UnregisterZone(TaskZone zone)
    {
        registeredZones.Remove(zone);
        if (zone == preparationZone)
        {
            CancelPreparation();
        }
    }

    public override void NotifyZoneEntered(
        TaskZone zone,
        StarterAssetsInputs player)
    {
        if (!IsTaskAvailable || zone == null || player == null ||
            CurrentState == SocialLessonTaskState.CompletionPending ||
            CurrentState == SocialLessonTaskState.Completed)
        {
            return;
        }

        if (zone.Role == TaskZoneRole.TaskArea)
        {
            if (activePlayer == null)
            {
                activePlayer = player;
            }

            if (activePlayer == player &&
                (CurrentState == SocialLessonTaskState.Inactive ||
                 CurrentState == SocialLessonTaskState.Unavailable))
            {
                SetState(SocialLessonTaskState.Active);
                uiController?.ShowTaskUi(stageIndex, true);
                PlayTaskAreaEntryVoice();
                onTaskAreaEntered?.Invoke();
            }

            return;
        }

        if (activePlayer == player &&
            CurrentState == SocialLessonTaskState.Active)
        {
            BeginPreparation(zone);
        }
    }

    public override void NotifyZoneExited(
        TaskZone zone,
        StarterAssetsInputs player)
    {
        if (zone == null || player == null || player != activePlayer)
        {
            return;
        }

        if (zone.Role == TaskZoneRole.ActionArea)
        {
            if (CurrentState == SocialLessonTaskState.Preparing &&
                zone == preparationZone)
            {
                CancelPreparation();
            }

            // 社交模式与移动模式严格互斥。进入社交模式后角色不能移动，
            // 因此不设计“社交阶段走出小圈”的任务分支。
            return;
        }

        if (IsPlayerInsideTaskArea() || IsInSocialPhase())
        {
            return;
        }

        ResetRuntimeState(true);
    }

    public void NotifySpeechDetected()
    {
        HandlePlayerResponse(PlayerResponseKind.Speech);
    }

    public override void CompleteCurrentTaskByTeacher()
    {
        if (!IsTaskAvailable ||
            CurrentState == SocialLessonTaskState.CompletionPending ||
            CurrentState == SocialLessonTaskState.Completed)
        {
            return;
        }

        BeginCompletion(TaskCompletionMethod.Teacher);
    }

    public override void CancelCurrentInteractionByTeacher()
    {
        if (!IsTaskAvailable ||
            CurrentState == SocialLessonTaskState.Completed)
        {
            return;
        }

        StopConversationLoop();
        modeManager?.SetMovementMode();
        uiController?.ShowTeachingUi(
            stageIndex,
            TaskTeachingUiState.Hidden);
        ResetPreparationProgress();
        idleGuidanceTimer = 0f;

        SetState(activePlayer != null && IsPlayerInsideTaskArea()
            ? SocialLessonTaskState.Active
            : SocialLessonTaskState.Inactive);
    }

    private void HandleSocialAction(
        SocialIntent intent,
        ChildHandSide childHand,
        AvatarHandSide avatarHand,
        float confidence)
    {
        if (intent == SocialIntent.WaveInvite)
        {
            HandlePlayerResponse(PlayerResponseKind.Wave);
        }
    }

    private void HandlePlayerResponse(PlayerResponseKind response)
    {
        if (!IsTaskAvailable ||
            CurrentState == SocialLessonTaskState.CompletionPending ||
            CurrentState == SocialLessonTaskState.Completed)
        {
            return;
        }

        if (lessonType == SocialLessonType.WaitThenRespond)
        {
            if (CurrentState == SocialLessonTaskState.NpcSpeaking)
            {
                RaiseRespondedTooEarly();
                return;
            }

            if (CurrentState == SocialLessonTaskState.ResponseWindow)
            {
                BeginCompletion(TaskCompletionMethod.TargetAction);
            }

            return;
        }

        if (CurrentState != SocialLessonTaskState.SocialActive)
        {
            return;
        }

        bool isTarget =
            (lessonType == SocialLessonType.GreetingWave &&
             response == PlayerResponseKind.Wave) ||
            (lessonType == SocialLessonType.InitiateSpeech &&
             response == PlayerResponseKind.Speech);

        BeginCompletion(isTarget
            ? TaskCompletionMethod.TargetAction
            : TaskCompletionMethod.AlternativeAction);
    }

    private void BeginPreparation(TaskZone zone)
    {
        preparationZone = zone;
        preparationTimer = 0f;
        SetPreparationProgress(0f);
        SetState(SocialLessonTaskState.Preparing);
    }

    private void TryBeginPreparationFromOccupiedActionArea()
    {
        for (int i = 0; i < registeredZones.Count; i++)
        {
            TaskZone zone = registeredZones[i];
            if (zone != null &&
                zone.Role == TaskZoneRole.ActionArea &&
                zone.IsOccupiedBy(activePlayer))
            {
                BeginPreparation(zone);
                return;
            }
        }
    }

    private void TickPreparation()
    {
        if (preparationZone == null ||
            !preparationZone.IsOccupiedBy(activePlayer))
        {
            CancelPreparation();
            return;
        }

        preparationTimer += Time.deltaTime;
        float progress = Mathf.Clamp01(
            preparationTimer / Mathf.Max(0.1f, preparationSeconds));
        SetPreparationProgress(progress);

        if (progress >= 1f)
        {
            BeginSocialInteraction();
        }
    }

    private void CancelPreparation()
    {
        preparationZone = null;
        ResetPreparationProgress();
        if (IsTaskAvailable &&
            CurrentState == SocialLessonTaskState.Preparing)
        {
            SetState(SocialLessonTaskState.Active);
        }
    }

    private void BeginSocialInteraction()
    {
        preparationZone = null;
        ResetPreparationProgress();
        idleGuidanceTimer = 0f;
        modeManager?.SetSocialInteractionMode();
        onSocialInteractionStarted?.Invoke();

        if (lessonType == SocialLessonType.WaitThenRespond)
        {
            StartConversationLoop();
        }
        else
        {
            SetState(SocialLessonTaskState.SocialActive);
            uiController?.ShowTeachingUi(
                stageIndex,
                TaskTeachingUiState.Default);
        }
    }

    private void TickIdleGuidance()
    {
        idleGuidanceTimer += Time.deltaTime;
        if (idleGuidanceTimer < idleGuidanceIntervalSeconds)
        {
            return;
        }

        idleGuidanceTimer = 0f;
        PlayVoice(idleGuidanceVoice);
        onIdleNeedsGuidance?.Invoke();
    }

    private void StartConversationLoop()
    {
        StopConversationLoop();
        CaptureAndFaceConversationNpc();
        conversationRoutine = StartCoroutine(ConversationLoop());
    }

    private IEnumerator ConversationLoop()
    {
        while (IsTaskAvailable &&
               CurrentState != SocialLessonTaskState.CompletionPending &&
               CurrentState != SocialLessonTaskState.Completed)
        {
            SetState(SocialLessonTaskState.NpcSpeaking);
            uiController?.ShowTeachingUi(
                stageIndex,
                TaskTeachingUiState.Wait);
            PlayConversationAnimation(conversationSpeakingState);
            PlayVoice(conversationVoice, conversationAudioSource);

            float speakingSeconds = conversationVoice != null
                ? conversationVoice.length
                : conversationFallbackSeconds;
            if (speakingSeconds > 0f)
            {
                yield return new WaitForSeconds(speakingSeconds);
            }

            if (CurrentState == SocialLessonTaskState.CompletionPending ||
                CurrentState == SocialLessonTaskState.Completed)
            {
                break;
            }

            PlayConversationAnimation(conversationDefaultState);
            SetState(SocialLessonTaskState.ResponseWindow);
            uiController?.ShowTeachingUi(
                stageIndex,
                TaskTeachingUiState.Respond);

            float elapsed = 0f;
            while (elapsed < responseWindowSeconds &&
                   CurrentState == SocialLessonTaskState.ResponseWindow)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        conversationRoutine = null;
    }

    private void StopConversationLoop()
    {
        if (conversationRoutine != null)
        {
            StopCoroutine(conversationRoutine);
            conversationRoutine = null;
        }

        if (conversationAudioSource != null &&
            conversationAudioSource.isPlaying)
        {
            conversationAudioSource.Stop();
        }

        RestoreConversationNpc();
    }

    private void RaiseRespondedTooEarly()
    {
        if (Time.time - lastTooEarlyFeedbackTime <
            respondedTooEarlyCooldownSeconds)
        {
            return;
        }

        lastTooEarlyFeedbackTime = Time.time;
        PlayVoice(respondedTooEarlyVoice);
        onRespondedTooEarly?.Invoke();
    }

    private void BeginCompletion(TaskCompletionMethod method)
    {
        CompletionMethod = method;
        StopConversationLoop();
        ResetPreparationProgress();
        SetState(SocialLessonTaskState.CompletionPending);
        onCompletionDetected?.Invoke(method);
        RequestSequenceCompletion(method);
    }

    private void SetPreparationProgress(float progress)
    {
        PreparationProgressNormalized = Mathf.Clamp01(progress);
        uiController?.SetProgress(PreparationProgressNormalized);
        onPreparationProgress?.Invoke(PreparationProgressNormalized);
    }

    private void ResetPreparationProgress()
    {
        preparationTimer = 0f;
        SetPreparationProgress(0f);
    }

    private void ResetRuntimeState(bool dispatchAbandoned)
    {
        StopConversationLoop();
        modeManager?.SetMovementMode();
        uiController?.HideStage(stageIndex);
        ResetPreparationProgress();

        activePlayer = null;
        preparationZone = null;
        idleGuidanceTimer = 0f;
        CompletionMethod = TaskCompletionMethod.None;
        SetState(IsTaskAvailable
            ? SocialLessonTaskState.Inactive
            : SocialLessonTaskState.Unavailable);

        if (dispatchAbandoned)
        {
            onTaskAreaExited?.Invoke();
            onTaskAbandoned?.Invoke();
            NotifySequenceTaskAbandoned();
        }
    }

    protected override void OnTaskAvailabilityChanged(bool available)
    {
        if (!available &&
            CurrentState != SocialLessonTaskState.Completed)
        {
            ResetRuntimeState(false);
            SetState(SocialLessonTaskState.Unavailable);
        }
        else if (available &&
                 CurrentState == SocialLessonTaskState.Unavailable)
        {
            SetState(SocialLessonTaskState.Inactive);
        }
    }

    protected override void OnSequenceCompletedRestored()
    {
        StopConversationLoop();
        activePlayer = null;
        uiController?.HideStage(stageIndex);
        uiController?.SetProgress(0f);
        SetState(SocialLessonTaskState.Completed);
    }

    protected override void OnSequenceCompletionFinalized()
    {
        StopConversationLoop();
        modeManager?.SetMovementMode();
        uiController?.HideStage(stageIndex);
        uiController?.SetProgress(0f);
        SetState(SocialLessonTaskState.Completed);
        onTaskCompleted?.Invoke();
    }

    protected override void OnSequenceReset()
    {
        ResetRuntimeState(false);
    }

    private bool IsPlayerInsideTaskArea()
    {
        for (int i = 0; i < registeredZones.Count; i++)
        {
            TaskZone zone = registeredZones[i];
            if (zone != null &&
                zone.Role == TaskZoneRole.TaskArea &&
                zone.IsOccupiedBy(activePlayer))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsInSocialPhase()
    {
        return CurrentState == SocialLessonTaskState.SocialActive ||
               CurrentState == SocialLessonTaskState.NpcSpeaking ||
               CurrentState == SocialLessonTaskState.ResponseWindow ||
               CurrentState == SocialLessonTaskState.CompletionPending;
    }

    private void ResolveReferences()
    {
        if (modeManager == null)
        {
            modeManager = FindObjectOfType<PoseControlModeManager>();
        }

        if (actionRecognizer == null)
        {
            actionRecognizer = FindObjectOfType<PoseSocialActionRecognizer>();
        }

        if (uiController == null)
        {
            uiController = FindObjectOfType<LevelTaskUIController>(true);
        }
    }

    private void CaptureAndFaceConversationNpc()
    {
        Transform npc = ResolveConversationNpc();
        if (npc == null)
        {
            return;
        }

        conversationNpcOriginalRotation = npc.rotation;
        hasConversationNpcOriginalRotation = true;

        Transform player = activePlayer != null ? activePlayer.transform : null;
        if (player == null)
        {
            return;
        }

        Vector3 direction = player.position - npc.position;
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.0001f)
        {
            npc.rotation = Quaternion.LookRotation(direction.normalized);
        }
    }

    private void RestoreConversationNpc()
    {
        PlayConversationAnimation(conversationDefaultState);

        Transform npc = ResolveConversationNpc();
        if (hasConversationNpcOriginalRotation && npc != null)
        {
            npc.rotation = conversationNpcOriginalRotation;
        }

        hasConversationNpcOriginalRotation = false;
    }

    private Transform ResolveConversationNpc()
    {
        return conversationNpcTransform != null
            ? conversationNpcTransform
            : conversationNpcAnimator != null
                ? conversationNpcAnimator.transform
                : null;
    }

    private void PlayConversationAnimation(string stateName)
    {
        if (conversationNpcAnimator != null &&
            !string.IsNullOrWhiteSpace(stateName))
        {
            conversationNpcAnimator.Play(stateName, 0, 0f);
        }
    }

    private void PlayVoice(AudioClip clip, AudioSource preferredSource = null)
    {
        if (clip == null)
        {
            return;
        }

        AudioSource source = preferredSource;
        if (source == null && Camera.main != null)
        {
            source = Camera.main.GetComponent<AudioSource>();
        }

        source?.PlayOneShot(clip);
    }

    private void PlayTaskAreaEntryVoice()
    {
        float now = Time.unscaledTime;
        if (now - lastTaskAreaEntryVoiceTime <
            taskAreaEntryVoiceCooldownSeconds)
        {
            return;
        }

        lastTaskAreaEntryVoiceTime = now;
        PlayVoice(taskAreaEntryVoice, taskAudioSource);
    }

    private void SetState(SocialLessonTaskState state)
    {
        if (CurrentState == state)
        {
            return;
        }

        CurrentState = state;
        Debug.Log(
            $"[SocialLessonTask] Stage {stageIndex + 1} ({lessonType})：{state}",
            this);
    }

    private enum PlayerResponseKind
    {
        Wave,
        Speech
    }
}
