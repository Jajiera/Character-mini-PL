using UnityEngine;

namespace Scripts.Data
{
    [CreateAssetMenu(fileName = "NewCharacterProfile", menuName = "Character/Data/Character Profile")]
    public class CharacterDataSO : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string characterId = "character_default";
        [SerializeField] private string characterDisplayName = "Standard Operative";
        [TextArea(2, 4)]
        [SerializeField] private string characterDescription = "Balanced operative capable of versatile locomotion and tactical maneuvers.";

        [Header("Visual Blueprint")]
        [SerializeField] private GameObject visualPrefab;
        [SerializeField] private RuntimeAnimatorController animatorController;
        [SerializeField] private Avatar characterAvatar;

        [Header("Movement & Physics Configuration")]
        [SerializeField] private MovementDataSO movementParameters;

        [Header("Combat & Tactical Attributes")]
        [SerializeField] private float maxHealth = 100.0f;
        [SerializeField] private float maxStamina = 100.0f;

        // Public getters for read-only access (Flyweight)
        public string CharacterId => characterId;
        public string CharacterDisplayName => characterDisplayName;
        public string CharacterDescription => characterDescription;

        public GameObject VisualPrefab => visualPrefab;
        public RuntimeAnimatorController AnimatorController => animatorController;
        public Avatar CharacterAvatar => characterAvatar;

        public MovementDataSO MovementParameters => movementParameters;
        public float MaxHealth => maxHealth;
        public float MaxStamina => maxStamina;
    }
}
