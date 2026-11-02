// Local NSEvent monitor: forwards trackpad scroll/pinch to managed code and
// swallows the event when the managed side says it handled it.
#import <Cocoa/Cocoa.h>

// Return 1 to consume the event (Unity never sees it), 0 to let it through.
typedef int (*ScrollCb)(float dx, float dy, int momentum, int shift);
typedef int (*PinchCb)(float magnification);

static id gMonitor;

void MagicTrackpad_Shutdown(void) {
    if (gMonitor) { [NSEvent removeMonitor:gMonitor]; gMonitor = nil; }
}

void MagicTrackpad_Init(ScrollCb onScroll, PinchCb onPinch) {
    MagicTrackpad_Shutdown();
    NSEventMask mask = NSEventMaskScrollWheel | NSEventMaskMagnify | NSEventMaskSmartMagnify;
    gMonitor = [NSEvent addLocalMonitorForEventsMatchingMask:mask handler:^NSEvent *(NSEvent *e) {
        switch (e.type) {
            case NSEventTypeScrollWheel:
                if (!e.hasPreciseScrollingDeltas) return e; // wheel mouse: leave alone
                return onScroll((float)e.scrollingDeltaX, (float)e.scrollingDeltaY,
                                e.momentumPhase != NSEventPhaseNone,
                                (e.modifierFlags & NSEventModifierFlagShift) != 0) ? nil : e;
            case NSEventTypeMagnify:
                return onPinch((float)e.magnification) ? nil : e;
            case NSEventTypeSmartMagnify: // two-finger double tap
                return onPinch(0) ? nil : e;
            default:
                return e;
        }
    }];
}
