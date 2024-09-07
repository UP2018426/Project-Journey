using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class FPSCounter : MonoBehaviour
{
    private TextMeshProUGUI fpsText;
    private void Start()
    {
        fpsText = GetComponent<TextMeshProUGUI>();
    }

    void Update()
    {
        fpsText.text = "FPS: " + (int)(1f / Time.unscaledDeltaTime);
    }
}
