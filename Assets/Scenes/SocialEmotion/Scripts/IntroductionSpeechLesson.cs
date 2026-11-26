using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.IO;
using TMPro;
using UnityEngine;
using Debug = UnityEngine.Debug;

[DisallowMultipleComponent]
public class IntroductionSpeechLesson : MonoBehaviour
{
    [Serializable]
    private class Question
    {
        public AudioClip prompt;
        [InspectorName("人工设置的问题"), TextArea(1, 3)] public string manuallyIssue;
        [InspectorName("Success")] public List<FeedbackEntry> feedback = new List<FeedbackEntry>();
    }

    [SerializeField] private TMP_Text stepScore;
    [SerializeField] private AudioSource promptSource;
    [InspectorName("Arrival Success"), SerializeField] private List<FeedbackEntry> arrivalFeedback = new List<FeedbackEntry>();
    [SerializeField] private Question nameQuestion = new Question();
    [SerializeField] private Question ageQuestion = new Question();
    [SerializeField] private Question playQuestion = new Question();

    public const float SilenceTimeoutSeconds = 20f;
    [SerializeField, HideInInspector] private bool questionsInitialized;

    private void OnValidate()
    {
        if (questionsInitialized) return;
        nameQuestion.manuallyIssue = "你叫什么名字呀？";
        ageQuestion.manuallyIssue = "你今年几岁啦？";
        playQuestion.manuallyIssue = "你喜欢玩什么呀？";
        questionsInitialized = true;
    }

    [Serializable]
    private class ListenRequest
    {
        public string manually_issue;
        public float silence_timeout = SilenceTimeoutSeconds;
    }

    [Serializable]
    private class FeedbackEntry
    {
        public Transform target;
        private static readonly int SuccessTrigger = Animator.StringToHash("TriggerSuccess");
        public AudioClip voice;
        public AudioSource audioSource;
        [Min(0f)] public float duration = 2f;

        [NonSerialized] private Quaternion originalRotation;
        [NonSerialized] private bool turned;
        [NonSerialized] public float finishTime;

        public void Begin(Transform player)
        {
            Animator animator = target != null ? target.GetComponentInChildren<Animator>() : null;
            if (target != null && player != null)
            {
                originalRotation = target.rotation;
                Vector3 direction = player.position - target.position;
                direction.y = 0f;
                turned = direction.sqrMagnitude > 0.0001f;
                if (turned) target.rotation = Quaternion.LookRotation(direction);
            }
            if (animator != null) animator.SetTrigger(SuccessTrigger);
            AudioSource source = audioSource != null ? audioSource : Camera.main?.GetComponent<AudioSource>();
            if (voice != null && source != null) source.PlayOneShot(voice);
            finishTime = Time.time + Mathf.Max(duration, voice != null ? voice.length : 0f);
        }

        public void Restore()
        {
            if (turned && target != null) target.rotation = originalRotation;
            turned = false;
        }
    }

    private readonly List<FeedbackEntry> activeEntries = new List<FeedbackEntry>();
    private Process worker;
    private ConcurrentQueue<string> messages;
    private Coroutine routine;
    private float modelLoadStarted;
    private string answer;
    private int points;
    public bool Ready { get; private set; }

    public void StartSession()
    {
        if (worker != null) return;
        SetPoints(points);
        var root = Path.GetFullPath(Path.Combine(Application.dataPath, "../Tools/TTS"));
        var queue = new ConcurrentQueue<string>();
        messages = queue;
        try
        {
            worker = new Process { StartInfo = new ProcessStartInfo {
                FileName = Path.Combine(root, ".venv/Scripts/python.exe"),
                Arguments = "-B -X utf8 -u ASR/lesson_worker.py",
                WorkingDirectory = root, UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true
            }};
            worker.OutputDataReceived += (_, e) => {
                if (e.Data != null && e.Data.StartsWith("LUMINA ", StringComparison.Ordinal))
                    queue.Enqueue(e.Data.Substring(7));
            };
            worker.Start();
            worker.BeginOutputReadLine();
            worker.BeginErrorReadLine();
        }
        catch (Exception ex)
        {
            Debug.LogError("无法启动第一关语音：" + ex.GetType().Name, this);
            CloseWorker();
        }
    }

    private void Update()
    {
        while (messages != null && messages.TryDequeue(out string status))
        {
            if (status == "KEY_REQUIRED") Debug.Log("第一关语音：请在 Lumina 弹窗输入方舟 Key。", this);
            else if (status == "LOADING")
            {
                modelLoadStarted = Time.realtimeSinceStartup;
                Debug.Log("SenseVoiceSmall 开始加载：正在初始化 Python 识别库。", this);
            }
            else if (status == "LOADING_WEIGHTS")
                Debug.Log("SenseVoiceSmall 识别库已就绪，开始加载本地模型权重。", this);
            else if (status == "READY")
            {
                Ready = true;
                Debug.Log($"SenseVoiceSmall 加载完成，语音识别已就绪（耗时 {Time.realtimeSinceStartup - modelLoadStarted:F1} 秒）。", this);
            }
            else if (status.StartsWith("FAULT") || status == "CANCELLED")
            {
                Debug.LogWarning("第一关语音暂停：" + status + "。修正配置后调用 RetryVoice。", this);
                CloseWorker();
            }
            else if (status.StartsWith("ARK ", StringComparison.Ordinal))
                Debug.Log(status.Substring(4), this);
            else answer = status;
        }
        if (worker != null && worker.HasExited)
        {
            Debug.LogWarning("语音进程已退出。可调用 RetryVoice 重试。", this);
            CloseWorker();
        }
    }

    [ContextMenu("重试语音连接（运行时）")]
    public void RetryVoice() { if (Application.isPlaying && isActiveAndEnabled && points < 4) StartSession(); }

    public void Run(Transform player, Action reminded, Action completed)
    {
        if (routine == null) routine = StartCoroutine(RunSteps(player, reminded, completed));
    }

    private IEnumerator RunSteps(Transform player, Action reminded, Action completed)
    {
        if (points == 0)
        {
            SetPoints(1);
            yield return Feedback(arrivalFeedback, player);
        }

        Question[] questions = { nameQuestion, ageQuestion, playQuestion };
        while (points < 4)
        {
            int index = points - 1;
            Question question = questions[index];
            if (string.IsNullOrWhiteSpace(question.manuallyIssue))
            {
                Debug.LogError($"第一关第 {index + 1} 题缺少人工设置的问题，请在 Inspector 填写。", this);
                yield return new WaitUntil(() => !string.IsNullOrWhiteSpace(question.manuallyIssue));
            }
            yield return PlayPrompt(question.prompt);
            if (!Ready)
            {
                // Re-cue the child once recording is available after a slow start or retry.
                yield return new WaitUntil(() => Ready);
                yield return PlayPrompt(question.prompt);
            }

            answer = null;
            try
            {
                var request = new ListenRequest { manually_issue = question.manuallyIssue };
                byte[] line = Encoding.UTF8.GetBytes(JsonUtility.ToJson(request) + "\n");
                var input = worker.StandardInput.BaseStream;
                input.Write(line, 0, line.Length);
                input.Flush();
            }
            catch (Exception ex)
            {
                Debug.LogWarning("语音连接中断：" + ex.GetType().Name, this);
                CloseWorker();
            }
            while (Ready && answer == null) yield return null;
            if (!Ready) continue;
            if (answer != "YES")
            {
                reminded();
                continue;
            }

            SetPoints(points + 1);
            yield return Feedback(question.feedback, player);
        }
        routine = null;
        CloseWorker();
        completed();
    }

    private IEnumerator PlayPrompt(AudioClip clip)
    {
        if (clip == null || promptSource == null) yield break;
        promptSource.clip = clip;
        promptSource.Play();
        while (promptSource.isPlaying) yield return null;
    }

    private IEnumerator Feedback(List<FeedbackEntry> entries, Transform player)
    {
        if (entries == null) yield break;
        foreach (var entry in entries)
        {
            if (entry == null) continue;
            entry.Begin(player);
            activeEntries.Add(entry);
        }
        while (activeEntries.Count > 0)
        {
            yield return null;
            for (int i = activeEntries.Count - 1; i >= 0; i--)
            {
                if (Time.time < activeEntries[i].finishTime) continue;
                activeEntries[i].Restore();
                activeEntries.RemoveAt(i);
            }
        }
    }

    private void SetPoints(int value)
    {
        points = value;
        if (stepScore != null) stepScore.text = value + "/4";
    }

    public void EndSession()
    {
        if (routine != null) StopCoroutine(routine);
        routine = null;
        if (promptSource != null) promptSource.Stop();
        for (int i = activeEntries.Count - 1; i >= 0; i--) activeEntries[i].Restore();
        activeEntries.Clear();
        CloseWorker();
        SetPoints(0);
    }

    private void CloseWorker()
    {
        Ready = false;
        messages = null;
        if (worker == null) return;
        try { worker.StandardInput.Close(); }
        catch (InvalidOperationException) { }
        catch (IOException) { }
        worker.Dispose();
        worker = null;
    }

    private void OnDisable() { EndSession(); }
}
