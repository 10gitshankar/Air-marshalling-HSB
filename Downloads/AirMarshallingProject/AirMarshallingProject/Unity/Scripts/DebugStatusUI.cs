/*
  Air Marshalling Project — DebugStatusUI.cs

  Shows a live "LEFT: MOVING  RIGHT: STILL  Gesture: TURN_LEFT" text
  on screen — very useful while calibrating thresholds and during the
  live demo so the audience can see what's being detected.

  SETUP:
  1. In the Hierarchy: right-click > UI > Text - TextMeshPro
     (Unity will offer to import TMP Essentials the first time — accept it)
  2. Select the new Text object, add this script (DebugStatusUI) to it
  3. Drag your GestureManager (the object with GestureReceiver.cs) into
     the "Receiver" field in the Inspector
  4. Press Play — the text will update live
*/

using UnityEngine;
using TMPro;

public class DebugStatusUI : MonoBehaviour
{
    public GestureReceiver receiver;
    private TextMeshProUGUI label;

    void Start()
    {
        label = GetComponent<TextMeshProUGUI>();
    }

    void Update()
    {
        if (receiver == null || label == null) return;
        label.text = receiver.GetDebugStatus();
    }
}
