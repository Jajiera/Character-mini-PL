using UnityEngine;
using UnityEngine.Events;
using Scripts.Core;

namespace Scripts.Interaction
{
    [RequireComponent(typeof(Collider))]
    public class InteractiveSwitch : MonoBehaviour, IInteractable
    {
        [Header("Switch Configuration")]
        [SerializeField] private string switchName = "Interruptor";
        [SerializeField] private bool isActivated = false;
        [SerializeField] private Renderer targetRenderer;

        [Header("State Colors")]
        [SerializeField] private Color deactivatedColor = Color.red;
        [SerializeField] private Color activatedColor = Color.green;
        [SerializeField] private Color proximityHighlightColor = Color.yellow;

        [Header("Events")]
        [SerializeField] private UnityEvent<bool> onStateChanged;
        public event System.Action<bool> StateChangedEvent;

        private MaterialPropertyBlock propertyBlock;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private bool isPlayerNearby;

        public bool IsActivated => isActivated;

        private void Awake()
        {
            if (targetRenderer == null)
            {
                targetRenderer = GetComponent<Renderer>();
            }

            propertyBlock = new MaterialPropertyBlock();
            UpdateVisuals();
        }

        public bool CanInteract(GameObject interactor)
        {
            return true;
        }

        public void Interact(GameObject interactor)
        {
            isActivated = !isActivated;
            Debug.Log($"<color=cyan>[Interruptor]</color> <b>{switchName}</b> cambiado a: {(isActivated ? "<color=green>ACTIVADO</color>" : "<color=red>DESACTIVADO</color>")}");

            UpdateVisuals();
            onStateChanged?.Invoke(isActivated);
            StateChangedEvent?.Invoke(isActivated);
        }

        public string GetInteractionPrompt()
        {
            return $"{(isActivated ? "Desactivar" : "Activar")} {switchName} [E]";
        }

        public void OnFocusGained()
        {
            isPlayerNearby = true;
            ApplyColor(proximityHighlightColor);
        }

        public void OnFocusLost()
        {
            isPlayerNearby = false;
            UpdateVisuals();
        }

        private void UpdateVisuals()
        {
            Color targetColor = isPlayerNearby 
                ? proximityHighlightColor 
                : (isActivated ? activatedColor : deactivatedColor);

            ApplyColor(targetColor);
        }

        private void ApplyColor(Color color)
        {
            if (targetRenderer == null) return;

            targetRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(BaseColorId, color);
            propertyBlock.SetColor(ColorId, color);
            targetRenderer.SetPropertyBlock(propertyBlock);
        }
    }
}
