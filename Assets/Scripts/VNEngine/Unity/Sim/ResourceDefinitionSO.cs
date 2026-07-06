using UnityEngine;

namespace VNEngine.Unity
{
    [CreateAssetMenu(fileName = "Resource", menuName = "VNEngine/Sim/Resource Definition")]
    public sealed class ResourceDefinitionSO : ScriptableObject
    {
        public string id;
        public string displayName;
        public int startValue;

        public ResourceDef ToDef() => new ResourceDef(id, displayName, startValue);
    }
}
