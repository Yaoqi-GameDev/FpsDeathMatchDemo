using UnityEngine;

namespace AssetBundleFramework
{
    public class AutoUnload : MonoBehaviour
    {
        public IResource resource { get; set; }

        private void OnDestroy()
        {
            if (resource == null)
                return;

            ResourceManager.instance.Unload(resource);
            resource = null;
        }
    }
}
