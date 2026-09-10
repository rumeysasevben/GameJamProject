using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// An on/off switch: a capsule track with a knob that slides across and
/// squashes a little on the way.
///
/// Unity's Toggle is a checkbox; the mock-up asks for a switch, and building
/// one on the checkbox would mean fighting its graphic swapping. This is the
/// whole of it instead. Animated on unscaled time, because it lives on the
/// pause panel.
/// </summary>
public class UIToggle : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private Image track;
    [SerializeField] private Image knob;

    [SerializeField] private Color trackOn = new Color(1f, 0.788f, 0.420f);
    [SerializeField] private Color trackOff = new Color(0.30f, 0.34f, 0.44f);
    [SerializeField] private Color knobOn = new Color(0.165f, 0.180f, 0.259f);
    [SerializeField] private Color knobOff = new Color(0.80f, 0.80f, 0.82f);

    [Tooltip("How far the knob moves either side of centre, in canvas pixels.")]
    [SerializeField] private float knobTravel = 16f;

    [SerializeField] private string clickSound = "ui_short";

    private bool value;
    private float position;

    /// <summary>Raised when the player flips the switch. Not raised by <see cref="SetWithoutNotify"/>.</summary>
    public event Action<bool> OnChanged;

    public bool Value => value;

    /// <summary>Shows <paramref name="isOn"/> without raising <see cref="OnChanged"/>.</summary>
    public void SetWithoutNotify(bool isOn, bool instant)
    {
        value = isOn;

        if (instant)
        {
            position = isOn ? 1f : 0f;
            Apply();
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
        {
            return;
        }

        value = !value;

        if (!string.IsNullOrEmpty(clickSound) && AudioManager.Instance != null)
        {
            AudioManager.Instance.Play(clickSound);
        }

        OnChanged?.Invoke(value);
    }

    private void Update()
    {
        position = Mathf.MoveTowards(position, value ? 1f : 0f, Time.unscaledDeltaTime * 6f);
        Apply();
    }

    private void Apply()
    {
        float eased = UIEase.OutCubic(position);

        if (track != null)
        {
            track.color = Color.Lerp(trackOff, trackOn, eased);
        }

        if (knob != null)
        {
            knob.color = Color.Lerp(knobOff, knobOn, eased);
            knob.rectTransform.anchoredPosition = new Vector2(Mathf.Lerp(-knobTravel, knobTravel, eased), 0f);

            // Stretched while it travels, round at either end.
            float squash = Mathf.Sin(position * Mathf.PI) * 0.18f;
            knob.rectTransform.localScale = new Vector3(1f + squash, 1f - squash * 0.5f, 1f);
        }
    }
}
