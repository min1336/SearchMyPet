#import <MapKit/MapKit.h>
#import <UIKit/UIKit.h>

extern UIViewController *UnityGetGLViewController(void);

static MKMapView *SearchMyPetMapView;

extern "C" void SMPMapSetVisible(int visible)
{
    dispatch_async(dispatch_get_main_queue(), ^{
        UIView *unityView = UnityGetGLViewController().view;
        if (visible != 0)
        {
            if (SearchMyPetMapView == nil)
            {
                SearchMyPetMapView = [[MKMapView alloc] initWithFrame:CGRectZero];
                SearchMyPetMapView.mapType = MKMapTypeMutedStandard;
                SearchMyPetMapView.showsCompass = YES;
                SearchMyPetMapView.showsScale = YES;
                SearchMyPetMapView.showsUserLocation = YES;
                SearchMyPetMapView.userTrackingMode = MKUserTrackingModeFollow;
            }

            UIEdgeInsets safe = unityView.safeAreaInsets;
            CGFloat tabHeight = safe.bottom + 76.0;
            SearchMyPetMapView.frame = CGRectMake(
                0.0,
                0.0,
                unityView.bounds.size.width,
                MAX(0.0, unityView.bounds.size.height - tabHeight));
            SearchMyPetMapView.autoresizingMask = UIViewAutoresizingFlexibleWidth | UIViewAutoresizingFlexibleHeight;
            [unityView addSubview:SearchMyPetMapView];
        }
        else
        {
            [SearchMyPetMapView removeFromSuperview];
        }
    });
}
