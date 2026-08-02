using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AssetBundleFramework
{
    internal abstract class ABundleAsync : ABundle
    {
        internal abstract bool Update();
    }
}