using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DemoGui : MonoBehaviour
{
    public GUIStyle guiStyle;

    float averageDeltaTime = 0f; //declare this variable outside Update
    float averageFps;
    string graphicsDeviceName;
    string resolutionText;

    void OnEnable()
    {
        averageDeltaTime = 0f;
        graphicsDeviceName = SystemInfo.graphicsDeviceName;
        resolutionText = $"{Screen.width}x{Screen.height}";
    }

    void Update()
    {
        averageDeltaTime += ((Time.deltaTime / Time.timeScale) - averageDeltaTime) * 0.03f;
        averageFps = (1F / averageDeltaTime);
    }

    void OnGUI()
    {
        GUILayout.Label($"Fps: {averageFps:F0}\nResolution: {resolutionText}\n{graphicsDeviceName}", guiStyle);
    }
}