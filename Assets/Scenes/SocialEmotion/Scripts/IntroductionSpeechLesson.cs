using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
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
        public TaskNpcSuccessFeedback success;
    }

    [SerializeField] private TMP_Text stepScore;
    [SerializeField] private AudioSource promptSource;
    [SerializeField] private TaskNpcSuccessFeedback arrivalSuccess;
    [SerializeField] private Question nameQuestion = new Question();
    [SerializeField] private Question ageQuestion = new Question();
    [SerializeField] private Question playQuestion = new Question();

    private Process worker;
    private ConcurrentQueue<string> messages;
    private Coroutine routine;
    private TaskNpcSuccessFeedback playingFeedback;
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
            if (status == "READY") Ready = true;
            else if (status.StartsWith("FAULT") || status == "CANCELLED")
            {
                Debug.LogWarning("第一关语音暂停：" + status + "。修正配置后调用 RetryVoice。", this);
                CloseWorker();
            }
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

    public void Run(Transform player, float interval, Action reminded, Action completed)
    {
        if (routine == null) routine = StartCoroutine(RunSteps(player, interval, reminded, completed));
    }

    private IEnumerator RunSteps(Transform player, float interval, Action reminded, Action completed)
    {
        if (points == 0)
        {
            SetPoints(1);
            yield return Feedback(arrivalSuccess, player);
        }

        Question[] questions = { nameQuestion, ageQuestion, playQuestion };
        while (points < 4)
        {
            while (!Ready) yield return null;
            int index = points - 1;
            Question question = questions[index];
            if (question.prompt != null && promptSource != null)
            {
                promptSource.clip = question.prompt;
                promptSource.Play();
                while (promptSource.isPlaying) yield return null;
            }

            answer = null;
            try
            {
                worker.StandardInput.WriteLine(index + " " + interval.ToString(CultureInfo.InvariantCulture));
                worker.StandardInput.Flush();
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
            yield return Feedback(question.success, player);
        }
        routine = null;
        CloseWorker();
        completed();
    }

    private IEnumerator Feedback(TaskNpcSuccessFeedback feedback, Transform player)
    {
        if (feedback == null) yield break;
        playingFeedback = feedback;
        feedback.PlayFeedback(player, null, true);
        while (feedback.IsPlaying) yield return null;
        playingFeedback = null;
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
        if (playingFeedback != null) playingFeedback.StopFeedback(false);
        playingFeedback = null;
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
