#import <AVFoundation/AVFoundation.h>

@interface ARConfiguration : NSObject

@property(class, nonatomic, nullable, readonly) AVCaptureDevice *configurableCaptureDeviceForPrimaryCamera;

@end

@interface ARWorldTrackingConfiguration : ARConfiguration

@end
