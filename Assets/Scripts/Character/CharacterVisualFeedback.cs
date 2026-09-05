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

        [Header("Attack Charge Visuals (Model & HUD)")]
        [SerializeField] private Color chargeStartColor = new Color(1.0f, 0.75f, 0.15f); // Ámbar dorado
        [SerializeField] private Color chargeMaxColor = new Color(1.0f, 0.12f, 0.05f);   // Rojo fuego intenso
        [SerializeField] private Color chargePulseColor = new Color(1.0f, 0.95f, 0.35f); // Destello oro
        [SerializeField] private float maxChargePulseSpeed = 14.0f;
        [SerializeField] private bool showChargeHUD = true;

        private MaterialPropertyBlock propertyBlock;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private Color targetColor;
        private Color currentColor;
        private Coroutine flashCoroutine;

        private GUIStyle labelStyle;

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
                playerCharacter.AttackReleasedEvent += HandleAttackReleased;
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
                playerCharacter.AttackReleasedEvent -= HandleAttackReleased;
                playerCharacter.InteractTriggeredEvent -= HandleInteractTriggered;
            }
        }

        private void Update()
        {
            // 1. Feedback visual directo en el modelo durante la carga de ataque
            if (playerCharacter != null && playerCharacter.IsChargingAttack)
            {
                float ratio = playerCharacter.AttackChargeRatio;
                if (ratio >= 0.999f)
                {
                    // Pulsación energética de alta velocidad al estar al 100%
                    float pulse = Mathf.PingPong(Time.time * maxChargePulseSpeed, 1f);
                    currentColor = Color.Lerp(chargeMaxColor, chargePulseColor, pulse);
                }
                else
                {
                    // Gradiente progresivo de ámbar a rojo intenso conforme carga
                    currentColor = Color.Lerp(chargeStartColor, chargeMaxColor, ratio);
                }

                ApplyColorToRenderer(currentColor);
                return;
            }

            // 2. Feedback normal de locomoción / destello cuando no está cargando
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
            TriggerActionFlash(attackFlashColor, flashDuration);
        }

        private void HandleAttackReleased(float chargeRatio)
        {
            // A mayor nivel de carga, mayor intensidad y duración del destello de liberación
            float dynamicDuration = Mathf.Lerp(flashDuration, flashDuration * 1.8f, chargeRatio);
            Color dynamicColor = Color.Lerp(attackFlashColor, Color.white, chargeRatio * 0.45f);
            TriggerActionFlash(dynamicColor, dynamicDuration);
        }

        private void HandleInteractTriggered()
        {
            TriggerActionFlash(interactFlashColor, flashDuration);
        }

        private void TriggerActionFlash(Color flashColor, float duration)
        {
            if (flashCoroutine != null)
            {
                StopCoroutine(flashCoroutine);
            }

            flashCoroutine = StartCoroutine(FlashColorRoutine(flashColor, duration));
        }

        private IEnumerator FlashColorRoutine(Color flashColor, float duration)
        {
            ApplyColorToRenderer(flashColor);
            yield return new WaitForSeconds(duration);

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

        #region HUD Charge Bar Visualization

        private void OnGUI()
        {
            if (!showChargeHUD || playerCharacter == null || !playerCharacter.IsChargingAttack)
            {
                return;
            }

            float ratio = playerCharacter.AttackChargeRatio;
            bool isMaxCharge = ratio >= 0.999f;

            // Dimensiones y posicionamiento de la barra de carga en pantalla
            float barWidth = 280f;
            float barHeight = 18f;
            float posX = (Screen.width - barWidth) * 0.5f;
            float posY = Screen.height * 0.74f;

            Rect backgroundRect = new Rect(posX - 4f, posY - 24f, barWidth + 8f, barHeight + 30f);
            Rect barOuterRect = new Rect(posX, posY, barWidth, barHeight);
            Rect barFillRect = new Rect(posX + 2f, posY + 2f, (barWidth - 4f) * ratio, barHeight - 4f);

            // 1. Fondo oscuro translúcido estilo HUD moderno
            DrawRect(backgroundRect, new Color(0.06f, 0.07f, 0.10f, 0.88f));

            // 2. Borde exterior (dorado pulsante si está a carga máxima)
            Color borderColor = isMaxCharge 
                ? Color.Lerp(Color.yellow, Color.white, Mathf.PingPong(Time.time * 10f, 1f))
                : new Color(0.35f, 0.40f, 0.50f, 0.8f);
            DrawBorder(barOuterRect, borderColor, 2f);

            // 3. Relleno dinámico de la barra
            Color fillColor = isMaxCharge 
                ? Color.Lerp(chargeMaxColor, chargePulseColor, Mathf.PingPong(Time.time * maxChargePulseSpeed, 1f))
                : Color.Lerp(chargeStartColor, chargeMaxColor, ratio);
            DrawRect(barFillRect, fillColor);

            // 4. Texto informativo sobre la barra
            if (labelStyle == null)
            {
                labelStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 13,
                    fontStyle = FontStyle.Bold
                };
            }

            Rect textRect = new Rect(posX, posY - 22f, barWidth, 20f);
            if (isMaxCharge)
            {
                labelStyle.normal.textColor = Color.Lerp(Color.yellow, Color.white, Mathf.PingPong(Time.time * 8f, 1f));
                GUI.Label(textRect, "★ ¡CARGA MÁXIMA COMPLETA! ★", labelStyle);
            }
            else
            {
                labelStyle.normal.textColor = Color.white;
                GUI.Label(textRect, $"⚡ CARGANDO ATAQUE... {(ratio * 100f):F0}%", labelStyle);
            }
        }

        private void DrawRect(Rect rect, Color color)
        {
            Color prev = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = prev;
        }

        private void DrawBorder(Rect rect, Color color, float thickness = 2f)
        {
            DrawRect(new Rect(rect.x, rect.y, rect.width, thickness), color);
            DrawRect(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
            DrawRect(new Rect(rect.x, rect.y, thickness, rect.height), color);
            DrawRect(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
        }

        #endregion
    }
}
