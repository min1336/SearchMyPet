#import <ARKit/ARKit.h>
#import <AVFoundation/AVFoundation.h>

static AVCaptureDevice *SearchMyPetPrimaryARCaptureDevice(void)
{
    if (@available(iOS 16.0, *))
    {
        return ARWorldTrackingConfiguration.configurableCaptureDeviceForPrimaryCamera;
    }

    return nil;
}

static CGFloat SearchMyPetDisplayZoomMultiplier(AVCaptureDevice *device)
{
    if (@available(iOS 18.0, *))
    {
        const CGFloat multiplier = device.displayVideoZoomFactorMultiplier;
        return multiplier > 0.0 ? multiplier : 1.0;
    }

    return 1.0;
}

extern "C" bool SearchMyPetARCameraLensGetRange(float *minimum, float *maximum, float *current)
{
    AVCaptureDevice *device = SearchMyPetPrimaryARCaptureDevice();
    if (device == nil || minimum == nil || maximum == nil || current == nil)
    {
        return false;
    }

    const CGFloat displayMultiplier = SearchMyPetDisplayZoomMultiplier(device);
    *minimum = device.minAvailableVideoZoomFactor * displayMultiplier;
    *maximum = device.maxAvailableVideoZoomFactor * displayMultiplier;
    *current = device.videoZoomFactor * displayMultiplier;
    return true;
}

extern "C" bool SearchMyPetARCameraLensRampTo(float requestedZoom, float rate, float *appliedZoom)
{
    AVCaptureDevice *device = SearchMyPetPrimaryARCaptureDevice();
    if (device == nil || appliedZoom == nil)
    {
        return false;
    }

    NSError *error = nil;
    if (![device lockForConfiguration:&error])
    {
        return false;
    }

    const CGFloat displayMultiplier = SearchMyPetDisplayZoomMultiplier(device);
    const CGFloat requestedRawZoom = (CGFloat)requestedZoom / displayMultiplier;
    const CGFloat minimumRawZoom = device.minAvailableVideoZoomFactor;
    const CGFloat maximumRawZoom = device.maxAvailableVideoZoomFactor;
    const CGFloat clampedRawZoom = MAX(minimumRawZoom, MIN(requestedRawZoom, maximumRawZoom));
    [device rampToVideoZoomFactor:clampedRawZoom withRate:MAX(0.1f, rate)];
    [device unlockForConfiguration];
    *appliedZoom = clampedRawZoom * displayMultiplier;
    return true;
}
