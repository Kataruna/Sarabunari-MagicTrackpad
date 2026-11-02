// Local NSEvent monitor: forwards trackpad scroll/pinch to managed code and
// swallows the event when the managed side says it handled it.
#import <Cocoa/Cocoa.h>

// Return 1 to consume the event (Unity never sees it), 0 to let it through.
// mods: 1 = shift, 2 = option. type is the NSEventType of a non-scroll gesture.
typedef int (*ScrollCb)(float dx, float dy, int momentum, int mods);
typedef int (*PinchCb)(int type, float magnification);

static id gMonitor;
static int gDebug;

void MagicTrackpad_SetDebug(int on) { gDebug = on; }

void MagicTrackpad_Shutdown(void) {
    if (gMonitor) { [NSEvent removeMonitor:gMonitor]; gMonitor = nil; }
}

void MagicTrackpad_Init(ScrollCb onScroll, PinchCb onPinch) {
    MagicTrackpad_Shutdown();
    NSEventMask mask = NSEventMaskScrollWheel | NSEventMaskMagnify | NSEventMaskSmartMagnify |
        NSEventMaskRotate | NSEventMaskSwipe | NSEventMaskGesture |
        NSEventMaskBeginGesture | NSEventMaskEndGesture;
    gMonitor = [NSEvent addLocalMonitorForEventsMatchingMask:mask handler:^NSEvent *(NSEvent *e) {
        if (gDebug) NSLog(@"[MagicTrackpad] type=%lu subtype=%d", (unsigned long)e.type, (int)e.subtype);
        if (e.type == NSEventTypeScrollWheel) {
            if (!e.hasPreciseScrollingDeltas) return e; // wheel mouse: leave alone
            int mods = ((e.modifierFlags & NSEventModifierFlagShift) ? 1 : 0)
                     | ((e.modifierFlags & NSEventModifierFlagOption) ? 2 : 0);
            return onScroll((float)e.scrollingDeltaX, (float)e.scrollingDeltaY,
                            e.momentumPhase != NSEventPhaseNone, mods) ? nil : e;
        }
        // Magnify, smart magnify, rotate, swipe and the raw gesture stream:
        // Unity's panel-maximize can hang off any of them, so managed decides per type.
        float mag = e.type == NSEventTypeMagnify ? (float)e.magnification : 0;
        return onPinch((int)e.type, mag) ? nil : e;
    }];
}
