using UnityEngine;
using Scripts.Input;
using Scripts.Character;

namespace Scripts.StateMachine.Evasive
{
    public class SlidingState : PlayerStateBase
    {
        private float slideTimer;
        private Vector3 slideDirection;
        private const float MinSlopeAngleForDownhillSlide = 4.0f;

        public SlidingState(PlayerCharacter character, PlayerStateMachine stateMachine, InputReader inputReader) 
            : base(character, stateMachine, inputReader)
        {
        }

        public override void Enter()
        {
            character.SetStanceDimensions(MovementData.CrouchingHeight, MovementData.CrouchingCenter);
            slideTimer = MovementData.SlideDuration;

            Vector2 moveInput = inputReader.CurrentMoveInput;
            slideDirection = character.CalculateWorldMovementDirection(moveInput);

            if (slideDirection.sqrMagnitude < 0.01f)
            {
                slideDirection = character.transform.forward;
            }

            slideDirection.y = 0f;
            slideDirection.Normalize();

            // Si ya iniciamos el deslizamiento en una pendiente descendente, alineamos la dirección con la bajada
            if (IsOnDownhillSlope(out Vector3 downhillDir, out _))
            {
                slideDirection = downhillDir;
            }

            character.SetHorizontalVelocity(slideDirection * MovementData.SlideInitialSpeed);
        }

        public override void Execute()
        {
            bool isDownhill = IsOnDownhillSlope(out _, out _);

            if (isDownhill)
            {
                // Mientras haya pendiente negativa (bajada), se mantiene el temporizador al máximo
                // para que el personaje continúe deslizándose indefinidamente hasta que acabe la pendiente
                slideTimer = MovementData.SlideDuration;
            }
            else
            {
                // En terreno plano o subida, el tiempo transcurre normalmente
                slideTimer -= Time.deltaTime;
            }

            // Salir del slide si se agota el tiempo o si en plano ya perdió prácticamente toda la inercia
            bool hasStoppedOnFlat = !isDownhill && character.CurrentVelocity.sqrMagnitude < 0.25f && slideTimer < (MovementData.SlideDuration * 0.5f);

            if (slideTimer <= 0f || hasStoppedOnFlat)
            {
                if (inputReader.CurrentMoveInput.sqrMagnitude > 0.01f)
                {
                    stateMachine.ChangeState(character.CrouchingState);
                }
                else
                {
                    stateMachine.ChangeState(character.IdleState);
                }
            }
        }

        public override void FixedExecute()
        {
            bool isDownhill = IsOnDownhillSlope(out Vector3 downhillDir, out float slopeAngle);

            if (isDownhill)
            {
                // Reorientar suavemente la trayectoria del deslizamiento hacia la caída natural de la pendiente
                slideDirection = Vector3.Slerp(slideDirection, downhillDir, Time.fixedDeltaTime * 4.0f);
                character.RotateTowards(slideDirection, MovementData.RotationSmoothTime);

                // La inclinación de la bajada impulsa al personaje: a mayor pendiente, mayor velocidad
                float slopeBonusSpeed = Mathf.Clamp((slopeAngle / 45.0f) * 5.0f, 0f, 8.0f);
                float targetSpeed = MovementData.SlideInitialSpeed + slopeBonusSpeed;

                // Acelerar cuesta abajo manteniendo impulso
                character.AccelerateTowards(slideDirection, targetSpeed, MovementData.Acceleration * 1.5f);
            }
            else
            {
                // En terreno plano, desacelera naturalmente
                character.Decelerate(MovementData.Deceleration * 0.75f);
            }

            character.ApplyGravity();
            character.MoveWithCurrentVelocity();
        }

        /// <summary>
        /// Determina si el personaje está sobre una superficie con pendiente descendente (bajada)
        /// orientada en la dirección en la que se está deslizando.
        /// </summary>
        private bool IsOnDownhillSlope(out Vector3 downhillDirection, out float slopeAngle)
        {
            downhillDirection = Vector3.zero;
            slopeAngle = 0f;

            if (!character.IsGrounded()) return false;

            slopeAngle = character.GroundAngle;
            if (slopeAngle < MinSlopeAngleForDownhillSlide) return false;

            Vector3 normal = character.GroundNormal;
            // Proyección del vector gravedad/caída sobre el plano de la superficie
            Vector3 slopeDown = Vector3.ProjectOnPlane(Vector3.down, normal);

            if (slopeDown.sqrMagnitude < 0.001f) return false;

            downhillDirection = slopeDown.normalized;

            // Evaluar si el vector de deslizamiento coincide con la dirección de la bajada
            Vector3 currentFacing = slideDirection.sqrMagnitude > 0.01f ? slideDirection : character.transform.forward;
            float dot = Vector3.Dot(currentFacing.normalized, downhillDirection);

            // dot > 0.1 indica avance hacia abajo de la pendiente (evita deslizarse cuesta arriba)
            return dot > 0.1f;
        }
    }
}
