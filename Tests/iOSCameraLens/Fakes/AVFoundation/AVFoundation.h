#import <Foundation/Foundation.h>

@interface AVCaptureDevice : NSObject

@property(nonatomic) CGFloat minAvailableVideoZoomFactor;
@property(nonatomic) CGFloat maxAvailableVideoZoomFactor;
@property(nonatomic) CGFloat videoZoomFactor;
@property(nonatomic) CGFloat displayVideoZoomFactorMultiplier;

- (BOOL)lockForConfiguration:(NSError **)error;
- (void)unlockForConfiguration;
- (void)rampToVideoZoomFactor:(CGFloat)factor withRate:(float)rate;

@end
