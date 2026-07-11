using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;
using DrumKit.Pieces;
using DrumKit.Rhythm;

namespace DrumKit.Input
{
    /// <summary>
    /// Lets controller buttons stand in for the foot-operated pieces - the bass drum (kick
    /// pedal) and hi-hat (pedal) - which can't be struck with a hand-held mallet in VR.
    ///
    /// Each binding maps an Input System button action to a target DrumPiece. On a button
    /// press that piece's DrumPiece.TriggerPedalHit fires, which raises DrumPiece.OnStruck
    /// exactly like a physical strike does - so in rhythm mode the RhythmScorer judges,
    /// scores and combos a pedal press identically to a stick hit, while in a free-play
    /// mode (no scorer/registry) it simply plays the drum sound.
    ///
    /// The target piece is resolved either by a direct DrumPiece reference (works in any
    /// scene, no rhythm system needed - use this for Solo/free-play) or, if none is set, by
    /// looking DrumPieceId up in an optional RhythmPieceRegistry.
    /// </summary>
    public class DrumPedalInput : MonoBehaviour
    {
        [Serializable]
        public class PedalBinding
        {
            [Tooltip("Direct target piece for this button. Set this for scenes without a RhythmPieceRegistry (e.g. Solo). Takes priority over 'piece' below.")]
            public DrumPiece targetPiece;

            [Tooltip("Fallback: which foot-operated piece to play, resolved via the Registry when no direct Target Piece is set.")]
            public DrumPieceId piece = DrumPieceId.BassDrum;

            [Tooltip("Button action to listen to (e.g. a controller trigger, grip or face button). Its 'performed' callback fires one hit.")]
            public InputActionProperty action;

            [Tooltip("Hit strength (0..1) used to pick the sound layer and scale haptics when this pedal fires.")]
            [Range(0f, 1f)] public float intensity = 0.85f;

            [Tooltip("Optional controller to buzz when this pedal fires (usually the one whose button is bound). Leave empty for no haptics.")]
            public HapticImpulsePlayer haptics;

            [Tooltip("Ignore repeat presses arriving within this many seconds - guards against a single press firing twice.")]
            public float retriggerCooldown = 0.04f;

            [NonSerialized] public double LastFireTime = double.NegativeInfinity;
            [NonSerialized] public Action<InputAction.CallbackContext> Handler;
        }

        [Tooltip("Optional. Resolves a binding's DrumPieceId -> DrumPiece when no direct Target Piece is set. Only needed in rhythm scenes; leave empty in Solo/free-play and use each binding's Target Piece instead.")]
        [SerializeField] RhythmPieceRegistry registry;

        [Tooltip("Duration (seconds) of the haptic pulse at full intensity; scaled down for softer hits.")]
        [SerializeField] float maxHapticDuration = 0.06f;

        [Tooltip("Log binding/press diagnostics to the Console. Turn off once pedals are confirmed working.")]
        [SerializeField] bool logDebug = true;

        [SerializeField] List<PedalBinding> bindings = new();

        void OnEnable()
        {
            foreach (PedalBinding binding in bindings)
            {
                InputAction action = binding.action.action;
                if (action == null)
                {
                    if (logDebug)
                    {
                        Debug.LogWarning($"[DrumPedalInput] {binding.piece}: no action assigned.", this);
                    }
                    continue;
                }

                // A dedicated handler per binding - no ambiguity about which action fired.
                PedalBinding captured = binding;
                binding.Handler = ctx => Fire(captured, ctx);
                action.performed += binding.Handler;
                action.Enable();

                if (logDebug)
                {
                    Debug.Log($"[DrumPedalInput] bound {binding.piece} -> action '{action.name}' (type={action.type}, enabled={action.enabled}, controls={action.controls.Count}).", this);
                }
            }
        }

        void OnDisable()
        {
            foreach (PedalBinding binding in bindings)
            {
                InputAction action = binding.action.action;
                if (action != null && binding.Handler != null)
                {
                    // Only detach our handler - don't Disable(), the action may be shared with XRI.
                    action.performed -= binding.Handler;
                }

                binding.Handler = null;
            }
        }

        void Fire(PedalBinding binding, InputAction.CallbackContext context)
        {
            if (logDebug)
            {
                Debug.Log($"[DrumPedalInput] press received for {binding.piece} (control='{context.control?.path}').", this);
            }

            double now = Time.timeAsDouble;
            if (now - binding.LastFireTime < binding.retriggerCooldown)
            {
                return;
            }

            binding.LastFireTime = now;

            DrumPiece piece = ResolvePiece(binding);
            if (piece == null)
            {
                Debug.LogWarning($"[DrumPedalInput] no target piece for this binding: set its Target Piece directly, or assign a Registry that knows {binding.piece}.", this);
                return;
            }

            piece.TriggerPedalHit(binding.intensity);

            if (binding.haptics != null)
            {
                binding.haptics.SendHapticImpulse(binding.intensity, maxHapticDuration * binding.intensity);
            }
        }

        /// <summary>Direct Target Piece wins (needs no rhythm system); otherwise fall back to the optional registry lookup by id.</summary>
        DrumPiece ResolvePiece(PedalBinding binding)
        {
            if (binding.targetPiece != null)
            {
                return binding.targetPiece;
            }

            if (registry != null && registry.TryGetPiece(binding.piece, out DrumPiece piece))
            {
                return piece;
            }

            return null;
        }
    }
}
