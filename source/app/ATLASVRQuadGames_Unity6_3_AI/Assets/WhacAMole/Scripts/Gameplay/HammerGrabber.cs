using System.Collections.Generic;
using UnityEngine;
using Whacamole.Controls;
using Whacamole.Core;

namespace Whacamole.Gameplay
{
    /// <summary>
    /// Lets each hand pick up a hammer with the grip button (the middle-finger button on the
    /// side of a Quest controller). Squeeze near a hammer to pick it up; let go to drop it,
    /// and it glides back to its stand.
    /// </summary>
    public class HammerGrabber : MonoBehaviour
    {
        [Tooltip("How close (m) the controller must be to a hammer to grab it.")]
        [SerializeField, Min(0.01f)] private float grabRadius = 0.25f;
        [Tooltip("Grip squeeze (0-1) needed to grab.")]
        [SerializeField, Range(0.05f, 1f)] private float pressThreshold = 0.55f;
        [Tooltip("Grip squeeze (0-1) below which the hammer is let go. Lower than the press value so it doesn't flicker.")]
        [SerializeField, Range(0f, 1f)] private float releaseThreshold = 0.35f;

        [Header("Haptics")]
        [SerializeField, Range(0f, 1f)] private float grabHapticAmplitude = 0.3f;
        [SerializeField, Min(0f)] private float grabHapticDuration = 0.05f;

        private readonly Hammer[] held = new Hammer[2];
        private readonly bool[] gripDown = new bool[2];

        private void Update()
        {
            InputManager input = InputManager.Instance;
            if (input == null) return;

            for (int i = 0; i < 2; i++)
            {
                Hand hand = (Hand)i;
                float grip = input.GetGrip(hand);
                bool down = gripDown[i] ? grip > releaseThreshold : grip >= pressThreshold;

                if (down && !gripDown[i]) TryGrab(hand, input);
                else if (!down && gripDown[i]) Release(hand);

                gripDown[i] = down;
            }
        }

        private void OnDisable()
        {
            Release(Hand.Left);
            Release(Hand.Right);
        }

        /// <summary>The hammer this hand is holding, or null.</summary>
        public Hammer GetHeld(Hand hand) => held[(int)hand];

        private void TryGrab(Hand hand, InputManager input)
        {
            if (held[(int)hand] != null) return;

            Transform anchor = input.GetControllerTransform(hand);
            if (anchor == null) return;

            Hammer best = null;
            float bestDist = grabRadius;
            foreach (Hammer h in AllHammers())
            {
                if (h == null || h.IsHeld) continue;
                float d = h.DistanceTo(anchor.position);
                if (d <= bestDist)
                {
                    bestDist = d;
                    best = h;
                }
            }

            if (best == null) return;

            best.Grab(anchor, hand);
            held[(int)hand] = best;
            input.SendHaptic(hand, grabHapticAmplitude, grabHapticDuration);
        }

        private void Release(Hand hand)
        {
            Hammer h = held[(int)hand];
            if (h == null) return;
            h.Release();
            held[(int)hand] = null;
        }

        private static IEnumerable<Hammer> AllHammers()
        {
            if (GameManager.Instance != null && GameManager.Instance.Hammers.Count > 0)
                return GameManager.Instance.Hammers;
            return FindObjectsByType<Hammer>(FindObjectsSortMode.None);
        }
    }
}
