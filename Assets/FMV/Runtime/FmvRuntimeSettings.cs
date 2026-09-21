using UnityEngine;
using UnityEngine.AddressableAssets;

namespace FmvDemo
{
    [CreateAssetMenu(menuName = "FMV/Runtime Settings")]
    public sealed class FmvRuntimeSettings : ScriptableObject
    {
        public AssetReferenceT<FmvSequenceDefinition> sequence;
        [Min(0.01f)] public float loadTimeoutSeconds = 30;
        [Min(0.01f)] public float prepareTimeoutSeconds = 30;
        [Tooltip("프로필 도구가 설정합니다. Local은 원격 요청을 보내지 않습니다.")]
        public bool checkRemoteUpdates;
        public string profileName = "Local";
    }
}
