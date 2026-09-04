using UnityEngine;
using Scripts.Core;

namespace Scripts.Interaction
{
    [RequireComponent(typeof(Collider))]
    public class CollectibleCube : MonoBehaviour, IInteractable
    {
        [Header("Item Configuration")]
        [SerializeField] private string itemName = "Cubo Coleccionable";
        [SerializeField] private Renderer targetRenderer;

        [Header("Proximity Highlight Colors")]
        [SerializeField] private Color normalColor = new Color(0.7f, 0.7f, 0.7f);
        [SerializeField] private Color highlightColor = new Color(1.0f, 0.85f, 0.1f);
        [SerializeField] private float pulseSpeed = 4.0f;

        private MaterialPropertyBlock propertyBlock;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private bool isPlayerNearby;

        private void Awake()
        {
            if (targetRenderer == null)
            {
                targetRenderer = GetComponent<Renderer>();
            }

            propertyBlock = new MaterialPropertyBlock();
            ApplyColor(normalColor);
        }

        private void Update()
        {
            if (isPlayerNearby)
            {
                // Pulsate gently while in interaction range
                float t = (Mathf.Sin(Time.time * pulseSpeed) + 1.0f) * 0.5f;
                Color current = Color.Lerp(highlightColor, Color.white, t * 0.35f);
                ApplyColor(current);
            }
        }

        public bool CanInteract(GameObject interactor)
        {
            return true;
        }

        public void Interact(GameObject interactor)
        {
            Debug.Log($"<color=green>[Interacción]</color> ¡Objeto recolectado: <b>{itemName}</b>!");
            
            // Trigger feedback or spawn VFX if needed
            // Destroy upon collection as specified
            Destroy(gameObject);
        }

        public string GetInteractionPrompt()
        {
            return $"Recolectar {itemName} [E]";
        }

        public void OnFocusGained()
        {
            isPlayerNearby = true;
            ApplyColor(highlightColor);
        }

        public void OnFocusLost()
        {
            isPlayerNearby = false;
            ApplyColor(normalColor);
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
