#import <AVFoundation/AVFoundation.h>
#import <UIKit/UIKit.h>

// Query iOS directly: Unity's WebCam permission API is disabled when its
// WebCam/Microphone modules are stripped from an ARKit-only player.
extern "C" int HeritageCameraAuthorizationStatus()
{
    return (int)[AVCaptureDevice authorizationStatusForMediaType:AVMediaTypeVideo];
}

extern "C" void HeritageRequestCameraAuthorization()
{
    if ([AVCaptureDevice authorizationStatusForMediaType:AVMediaTypeVideo] == AVAuthorizationStatusNotDetermined)
        [AVCaptureDevice requestAccessForMediaType:AVMediaTypeVideo completionHandler:^(BOOL granted) {}];
}

extern "C" void HeritageOpenAppSettings()
{
    dispatch_async(dispatch_get_main_queue(), ^{
        [[UIApplication sharedApplication] openURL:[NSURL URLWithString:UIApplicationOpenSettingsURLString]
                                          options:@{} completionHandler:nil];
    });
}
