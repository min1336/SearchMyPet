#import <Foundation/Foundation.h>
#import <cmath>
#import <cstdio>
#import <cstdlib>

#import <ARKit/ARKit.h>

static AVCaptureDevice *testCaptureDevice;

@implementation ARConfiguration

+ (AVCaptureDevice *)configurableCaptureDeviceForPrimaryCamera
{
    return nil;
}

@end


@implementation ARWorldTrackingConfiguration

+ (AVCaptureDevice *)configurableCaptureDeviceForPrimaryCamera
{
    return testCaptureDevice;
}

@end

@implementation AVCaptureDevice

- (BOOL)lockForConfiguration:(NSError **)error
{
    return YES;
}

- (void)unlockForConfiguration
{
}

- (void)rampToVideoZoomFactor:(CGFloat)factor withRate:(float)rate
{
    self.videoZoomFactor = factor;
}

@end

extern "C" bool SearchMyPetARCameraLensGetRange(float *minimum, float *maximum, float *current);
extern "C" bool SearchMyPetARCameraLensRampTo(float requestedZoom, float rate, float *appliedZoom);

static void ExpectNear(const char *label, float actual, float expected)
{
    if (std::fabs(actual - expected) <= 0.001f)
    {
        return;
    }

    std::fprintf(stderr, "FAIL %s: expected %.3f, got %.3f\n", label, expected, actual);
    std::exit(1);
}

int main()
{
    testCaptureDevice = [AVCaptureDevice new];
    testCaptureDevice.minAvailableVideoZoomFactor = 1.0;
    testCaptureDevice.maxAvailableVideoZoomFactor = 4.0;
    testCaptureDevice.videoZoomFactor = 1.0;
    testCaptureDevice.displayVideoZoomFactorMultiplier = 0.5;

    float minimum = 0.0f;
    float maximum = 0.0f;
    float current = 0.0f;
    if (!SearchMyPetARCameraLensGetRange(&minimum, &maximum, &current))
    {
        std::fprintf(stderr, "FAIL range query unexpectedly returned false\n");
        return 1;
    }

    // A raw range of 1...4 with Apple's 0.5 display multiplier must be
    // presented to the user as 0.5x...2x.
    ExpectNear("minimum display zoom", minimum, 0.5f);
    ExpectNear("maximum display zoom", maximum, 2.0f);
    ExpectNear("current display zoom", current, 0.5f);

    float applied = 0.0f;
    if (!SearchMyPetARCameraLensRampTo(2.0f, 5.0f, &applied))
    {
        std::fprintf(stderr, "FAIL zoom ramp unexpectedly returned false\n");
        return 1;
    }

    // Selecting the UI's 2x stop must convert back to raw factor 4 before
    // configuring AVCaptureDevice, then report 2x to Unity.
    ExpectNear("raw ramp factor", (float)testCaptureDevice.videoZoomFactor, 4.0f);
    ExpectNear("applied display zoom", applied, 2.0f);

    std::printf("PASS camera lens display/raw zoom conversion\n");
    return 0;
}
