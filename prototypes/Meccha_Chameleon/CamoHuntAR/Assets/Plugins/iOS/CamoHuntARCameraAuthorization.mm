#import <AVFoundation/AVFoundation.h>

extern "C" int CamoHuntARCameraAuthorizationStatus()
{
    return (int)[AVCaptureDevice authorizationStatusForMediaType:AVMediaTypeVideo];
}
