using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.Video;
using UnityEngine.UIElements;
using UnityEngine.UI;

[System.Serializable]
public class StepData
{
    public string text;
    public string ar_model;
}
[System.Serializable]
public class DiagnosisResponse
{
    public string problem;
    public StepData[] steps;
    public static DiagnosisResponse LastDiagnosis;
}

public class AppManager : MonoBehaviour
{
    public string backendUrl;
    public UIDocument uiDocument;
    public RawImage videoBackground;
    public VideoPlayer videoPlayer;
    private UnityEngine.UIElements.VisualElement progressPanel;
    private UnityEngine.UIElements.ProgressBar progressBar;
    private UnityEngine.UIElements.Label statusText;
    private UnityEngine.UIElements.Button selectVideoButton;
    private UnityEngine.UIElements.Button recordAudioButton;
    private UnityEngine.UIElements.Button selectAudioButton;
    private UnityEngine.UIElements.Button findSolutionButton;
    private UnityEngine.UIElements.Label videoStatusText;
    private UnityEngine.UIElements.Label audioStatusText;
    private string videoPath;
    private string audioPath;
    private string tempAudioFileName = "temp_audio.wav";
    private UnityEngine.UIElements.VisualElement root;

    void Start()
    {
        root = uiDocument.rootVisualElement;
        progressPanel = root.Q<UnityEngine.UIElements.VisualElement>("progress-panel");
        progressBar = root.Q<UnityEngine.UIElements.ProgressBar>("progress-bar");
        statusText = root.Q<UnityEngine.UIElements.Label>("status-label");
        selectVideoButton = root.Q<UnityEngine.UIElements.Button>("select-video-button");
        recordAudioButton = root.Q<UnityEngine.UIElements.Button>("record-audio-button");
        selectAudioButton = root.Q<UnityEngine.UIElements.Button>("select-audio-button");
        findSolutionButton = root.Q<UnityEngine.UIElements.Button>("find-solution-button");
        videoStatusText = root.Q<UnityEngine.UIElements.Label>("video-status-label");
        audioStatusText = root.Q<UnityEngine.UIElements.Label>("audio-status-label");
        progressPanel.style.display = DisplayStyle.None;
        selectVideoButton.clicked += OnSelectVideoButtonPressed;
        recordAudioButton.clicked += OnRecordAudioPressed;
        selectAudioButton.clicked += OnSelectAudioPressed;
        findSolutionButton.clicked += OnFindSolutionPressed;

        StartCoroutine(PlayBackgroundVideo());
    }

    IEnumerator PlayBackgroundVideo()
    {
        videoPlayer.Prepare();
        while (!videoPlayer.isPrepared)
            yield return null;

        videoBackground.texture = videoPlayer.texture;
        videoPlayer.Play();
    }
    public void OnSelectVideoButtonPressed()
    {
        NativeGallery.GetVideoFromGallery((path) =>
        {
            if (path != null)
            {
                videoPath = path;
                videoStatusText.text = "Video Selected!";
                videoStatusText.style.color = Color.green;
                CheckIfReady();
            }
        }, "Select a Machine Video");
    }
    public void OnRecordAudioPressed()
    {
        StartCoroutine(RecordAudio());
    }

    IEnumerator RecordAudio()
    {
        audioStatusText.text = "Recording... Speak now!";
        recordAudioButton.SetEnabled(false);
        selectAudioButton.SetEnabled(false);

        AudioClip clip = Microphone.Start(null, false, 5, 44100);
        yield return new WaitForSeconds(5);
        Microphone.End(null);

        audioPath = Path.Combine(Application.persistentDataPath, tempAudioFileName);
        SavWav.Save(audioPath, clip);

        audioStatusText.text = "Problem Recorded!";
        audioStatusText.style.color = Color.green;
        recordAudioButton.SetEnabled(true);
        selectAudioButton.SetEnabled(true);
        CheckIfReady();
    }
    public void OnSelectAudioPressed()
    {
        NativeGallery.GetAudioFromGallery((path) =>
        {
            if (path != null)
            {
                audioPath = path;
                audioStatusText.text = "Audio File Selected!";
                audioStatusText.style.color = Color.green;
                CheckIfReady();
            }
        }, "Select an Audio File");
    }
    void CheckIfReady()
    {
        if (!string.IsNullOrEmpty(videoPath) && !string.IsNullOrEmpty(audioPath))
        {
            findSolutionButton.pickingMode = PickingMode.Position;
            findSolutionButton.text = "Find Solution!";
        }
    }
    public void OnFindSolutionPressed()
    {
        StartCoroutine(UploadFiles(videoPath, audioPath));
    }

    IEnumerator UploadFiles(string videoPath, string audioPath)
    {
        progressPanel.style.display = DisplayStyle.Flex;
        progressBar.value = 0;
        statusText.text = "Starting analysis...";

        string[] statusMessages = new string[] {
            "Analyzing the problem...",
            "Detecting machine model...",
            "Problem found. Cross-referencing solutions...",
            "Generating repair steps..."
        };

        float startTime = Time.time;
        float minWaitTime = 5.0f;
        byte[] videoData = File.ReadAllBytes(videoPath);
        byte[] audioData = File.ReadAllBytes(audioPath);

        List<IMultipartFormSection> formData = new List<IMultipartFormSection>();
        string audioFileName = Path.GetFileName(audioPath);
        string audioMimeType = (audioFileName.EndsWith(".mp3")) ? "audio/mpeg" : "audio/wav";

        formData.Add(new MultipartFormFileSection("video", videoData, Path.GetFileName(videoPath), "video/mp4"));
        formData.Add(new MultipartFormFileSection("audio", audioData, audioFileName, audioMimeType));
        using (UnityWebRequest www = UnityWebRequest.Post(backendUrl, formData))
        {
            var asyncOperation = www.SendWebRequest();
            float timer = 0f;
            int statusIndex = 0;
            while (timer < minWaitTime || !asyncOperation.isDone)
            {
                timer += Time.deltaTime;
                float displayPercent = Mathf.Clamp01(timer / minWaitTime) * 90f;
                progressBar.value = displayPercent;
                int newIndex = Mathf.FloorToInt((timer / minWaitTime) * statusMessages.Length);
                newIndex = Mathf.Clamp(newIndex, 0, statusMessages.Length - 1);

                if (newIndex != statusIndex && newIndex < statusMessages.Length)
                {
                    statusIndex = newIndex;
                    statusText.text = statusMessages[statusIndex];
                }

                yield return null;
            }
            if (www.result == UnityWebRequest.Result.Success)
            {
                statusText.text = "Solution Found!";
                progressBar.value = 100;
                Debug.Log("Success! Server response: " + www.downloadHandler.text);
                DiagnosisResponse diagnosis = JsonUtility.FromJson<DiagnosisResponse>(www.downloadHandler.text);
                DiagnosisResponse.LastDiagnosis = diagnosis;

                yield return new WaitForSeconds(1.0f);
                SceneManager.LoadScene("SolutionScene");
            }
            else
            {
                statusText.text = "Error! " + www.error;
                Debug.LogError("Error from server: " + www.error);
                yield return new WaitForSeconds(3.0f);
                progressPanel.style.display = DisplayStyle.None;
            }
        }
    }
}