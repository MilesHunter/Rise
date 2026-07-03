using UnityEngine;

namespace Rise
{
    public sealed class RestPoint : MonoBehaviour
    {
        [SerializeField] private RestPointType restType = RestPointType.ShortRest;
        [SerializeField] private string promptText = "Press F to rest";
        [SerializeField] private string detailText = "Recover stamina";

        public RestPointType RestType => restType;
        public string PromptText => promptText;
        public string DetailText => detailText;

        public void Configure(RestPointType type, string prompt, string detail)
        {
            restType = type;
            promptText = prompt;
            detailText = detail;
        }
    }
}
