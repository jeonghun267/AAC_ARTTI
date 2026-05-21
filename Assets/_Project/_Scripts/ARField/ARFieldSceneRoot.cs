using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace Artti.ARField
{
    public class ARFieldSceneRoot : MonoBehaviour
    {
        private XROrigin _xrOrigin;
        private ARCameraManager _arCameraManager;
        private ARFieldNavigator _navigator;

        private void Awake()
        {
            _xrOrigin = FindFirstObjectByType<XROrigin>();
            _arCameraManager = FindFirstObjectByType<ARCameraManager>();
            _navigator = new ARFieldNavigator();
        }
    }
}
