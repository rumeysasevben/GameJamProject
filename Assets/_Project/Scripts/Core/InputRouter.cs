using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// The only place the game reads the mouse and keyboard.
///
/// Polled once a frame by <see cref="NightController"/> rather than running its
/// own Update, so the whole frame happens in a guaranteed order: input, then
/// aim, then movement, then collision. Nothing can read a cursor position that
/// is a frame stale.
///
/// Left click is a short signal, right click a long one. On WebGL the right
/// click must not open the browser menu — that is handled in the WebGL
/// template's index.html, not here, and without it the game is unplayable.
/// </summary>
public class InputRouter : MonoBehaviour
{
    /// <summary>Raised on a left click.</summary>
    public event Action OnShort;

    /// <summary>Raised on a right click.</summary>
    public event Action OnLong;

    /// <summary>Raised on Escape.</summary>
    public event Action OnPause;

    private Camera cam;

    /// <summary>
    /// The cursor in world units, z zeroed. Refreshed by <see cref="Poll"/>;
    /// holds its last value if the mouse is unavailable.
    /// </summary>
    public Vector2 CursorWorld { get; private set; }

    /// <summary>
    /// Reads this frame's input and raises whatever happened. Call once, first
    /// thing in the frame.
    /// </summary>
    public void Poll()
    {
        Mouse mouse = Mouse.current;
        if (mouse != null)
        {
            CursorWorld = ScreenToWorld(mouse.position.ReadValue());

            // A click on a button — the pause button in the corner — is meant
            // for the button, not the ships.
            bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

            if (mouse.leftButton.wasPressedThisFrame && !overUi)
            {
                OnShort?.Invoke();
            }

            if (mouse.rightButton.wasPressedThisFrame && !overUi)
            {
                OnLong?.Invoke();
            }
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
        {
            OnPause?.Invoke();
        }
    }

    /// <summary>
    /// Screen pixels to world units. The camera is orthographic, so the z fed
    /// in does not affect the answer and is simply discarded.
    /// </summary>
    private Vector2 ScreenToWorld(Vector2 screenPosition)
    {
        // Camera.main can be null for a frame after a scene load; keep the last
        // known cursor rather than reporting the origin, which would swing the
        // beam across the screen.
        if (cam == null)
        {
            cam = Camera.main;
            if (cam == null)
            {
                return CursorWorld;
            }
        }

        return cam.ScreenToWorldPoint(screenPosition);
    }
}
