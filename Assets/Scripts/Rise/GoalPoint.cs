using UnityEngine;

namespace Rise
{
    public sealed class GoalPoint : MonoBehaviour
    {
        [SerializeField] private string promptText = "Summit reached";

        public string PromptText => promptText;
    }
}
