using System.Collections;
using UnityEngine;
using Scripts.Core;
using Scripts.StateMachine;
using Scripts.StateMachine.Evasive;
using Scripts.StateMachine.Locomotion;
using Scripts.StateMachine.Tactical;

namespace Scripts.Character
{
    public class CharacterVisualFeedback : MonoBehaviour
    {
        [Header("Target References")]
        [SerializeField] private PlayerCharacter playerCharacter;
        [SerializeField] private PlayerStateMachine stateMachine;
        [SerializeField] private Renderer targetRenderer;

        [Header("Locomotion & Stance Colors")]
        [SerializeField] private Color idleColor = Color.white;
        [SerializeField] private Color walkingColor = new Color(0.35f, 0.75f, 1.0f);
        [SerializeField] private Color sprintingColor = new Color(0.0f, 0.35f, 1.0f);
        [SerializeField] private Color jumpingColor = new Color(0.1f, 1.0f, 0.75f);
        [SerializeField] private Color crouchingColor = new Color(1.0f, 0.85f, 0.15f);
        [SerializeField] private Color proneColor = new Color(0.85f, 0.45f, 0.1f);

        [Header("Evasive Colors")]
        [SerializeField] private Color slidingColor = new Color(0.75f, 0.15f, 1.0f);
        [SerializeField] private Color rollingColor = new Color(1.0f, 0.95f, 0.2f);

        [Header("Action Flash Colors")]
        [SerializeField] private Color attackFlashColor = Color.red;
        [SerializeField] private Color interactFlashColor = Color.green;
        [SerializeField] private float flashDuration = 0.2f;
        [SerializeField] private float colorTransitionSpeed = 10f;

        private MaterialPropertyBlock propertyBlock;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private Color targetColor;
        private Color currentColor;
        private Coroutine flashCoroutine;

        private void Awake()
        {
            if (playerCharacter == null)
            {
                playerCharacter = GetComponent<PlayerCharacter>();
            }

            if (stateMachine == null)
            {
                stateMachine = GetComponent<PlayerStateMachine>();
            }

            if (targetRenderer == null)
            {
                targetRenderer = GetComponentInChildren<Renderer>();
            }

            propertyBlock = new MaterialPropertyBlock();
            targetColor = idleColor;
            currentColor = idleColor;
            ApplyColorToRenderer(currentColor);
        }

        private void OnEnable()
        {
            if (stateMachine != null)
            {
                stateMachine.StateChangedEvent += HandleStateChanged;
            }

            if (playerCharacter != null)
            {
                playerCharacter.AttackTriggeredEvent += HandleAttackTriggered;
                playerCharacter.InteractTriggeredEvent += HandleInteractTriggered;
            }
        }

        private void OnDisable()
        {
            if (stateMachine != null)
            {
                stateMachine.StateChangedEvent -= HandleStateChanged;
            }

            if (playerCharacter != null)
            {
                playerCharacter.AttackTriggeredEvent -= HandleAttackTriggered;
                playerCharacter.InteractTriggeredEvent -= HandleInteractTriggered;
            }
        }

        private void Update()
        {
            if (flashCoroutine == null)
            {
                currentColor = Color.Lerp(currentColor, targetColor, Time.deltaTime * colorTransitionSpeed);
                ApplyColorToRenderer(currentColor);
            }
        }

        private void HandleStateChanged(IState newState)
        {
            switch (newState)
            {
                case SprintingState:
                    targetColor = sprintingColor;
                    break;
                case JumpingState:
                    targetColor = jumpingColor;
                    break;
                case WalkingState:
                    targetColor = walkingColor;
                    break;
                case CrouchingState:
                    targetColor = crouchingColor;
                    break;
                case ProneState:
                    targetColor = proneColor;
                    break;
                case SlidingState:
                    targetColor = slidingColor;
                    break;
                case RollingState:
                    targetColor = rollingColor;
                    break;
                case IdleState:
                default:
                    targetColor = idleColor;
                    break;
            }
        }

        private void HandleAttackTriggered()
        {
            TriggerActionFlash(attackFlashColor);
        }

        private void HandleInteractTriggered()
        {
            TriggerActionFlash(interactFlashColor);
        }

        private void TriggerActionFlash(Color flashColor)
        {
            if (flashCoroutine != null)
            {
                StopCoroutine(flashCoroutine);
            }

            flashCoroutine = StartCoroutine(FlashColorRoutine(flashColor));
        }

        private IEnumerator FlashColorRoutine(Color flashColor)
        {
            ApplyColorToRenderer(flashColor);
            yield return new WaitForSeconds(flashDuration);

            float elapsed = 0f;
            float returnDuration = 0.15f;
            while (elapsed < returnDuration)
            {
                elapsed += Time.deltaTime;
                currentColor = Color.Lerp(flashColor, targetColor, elapsed / returnDuration);
                ApplyColorToRenderer(currentColor);
                yield return null;
            }

            currentColor = targetColor;
            ApplyColorToRenderer(currentColor);
            flashCoroutine = null;
        }

        private void ApplyColorToRenderer(Color color)
        {
            if (propertyBlock == null)
            {
                propertyBlock = new MaterialPropertyBlock();
            }

            if (targetRenderer == null)
            {
                targetRenderer = GetComponentInChildren<Renderer>();
                if (targetRenderer == null) return;
            }

            targetRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(BaseColorId, color);
            propertyBlock.SetColor(ColorId, color);
            targetRenderer.SetPropertyBlock(propertyBlock);
        }
    }
}
