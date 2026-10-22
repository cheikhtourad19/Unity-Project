using System.Runtime.InteropServices;

namespace HeritageXR
{
    public enum CameraAuthorization { NotDetermined = 0, Restricted = 1, Denied = 2, Authorized = 3 }

    public static class CameraPermission
    {
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] static extern int HeritageCameraAuthorizationStatus();
        [DllImport("__Internal")] static extern void HeritageRequestCameraAuthorization();
        [DllImport("__Internal")] static extern void HeritageOpenAppSettings();
        public static CameraAuthorization Status => (CameraAuthorization)HeritageCameraAuthorizationStatus();
        public static void Request() => HeritageRequestCameraAuthorization();
        public static void OpenSettings() => HeritageOpenAppSettings();
#else
        public static CameraAuthorization Status => CameraAuthorization.NotDetermined;
        public static void Request() { }
        public static void OpenSettings() { }
#endif
    }
}
