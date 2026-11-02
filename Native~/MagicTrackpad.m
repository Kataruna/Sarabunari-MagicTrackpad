// Forwards trackpad scroll and pinch to managed code, which decides whether
// Unity still gets to see them.
#import <Cocoa/Cocoa.h>
#import <objc/runtime.h>

// Return 1 to consume the event (Unity never sees it), 0 to let it through.
// mods: 1 = shift, 2 = option, 4 = command, 8 = control.
typedef int (*ScrollCb)(float dx, float dy, int momentum, int mods);
typedef int (*PinchCb)(float magnification);

static id gMonitor;
static int gDebug;
static PinchCb gOnPinch;
static Method gSendEvent; // -[EditorApplicationPrincipalClass sendEvent:]
static IMP gSendEventOrig;

// Bump with any change to the exports or callback signatures. Unity keeps the
// dylib loaded across domain reloads, so managed must check it before Init.
int MagicTrackpad_Version(void) { return 8; }

void MagicTrackpad_SetDebug(int on) { gDebug = on; }

void MagicTrackpad_Shutdown(void) {
    if (gMonitor) { [NSEvent removeMonitor:gMonitor]; gMonitor = nil; }
    if (gSendEvent) { method_setImplementation(gSendEvent, gSendEventOrig); gSendEvent = NULL; }
    gOnPinch = NULL;
}

// Unity's panel maximize (WindowLayout.MaximizeGestureHandler) runs from
// CallGlobalInputEvent inside its app delegate's sendEvent:, which the principal
// class calls before [super sendEvent:]. NSEvent monitors and view methods only
// run after that, so a pinch has to be taken here, at the very top.
static void HookSendEvent(void) {
    Method m = class_getInstanceMethod(objc_getClass("EditorApplicationPrincipalClass"), @selector(sendEvent:));
    if (!m) { NSLog(@"[MagicTrackpad] EditorApplicationPrincipalClass sendEvent: not found, pinch unchanged"); return; }
    IMP orig = method_getImplementation(m);
    IMP hook = imp_implementationWithBlock(^(id app, NSEvent *e) {
        if (e.type == NSEventTypeMagnify && gOnPinch) {
            int consumed = gOnPinch((float)e.magnification);
            if (gDebug) NSLog(@"[MagicTrackpad] magnify %f consumed=%d", e.magnification, consumed);
            if (consumed) return;
        }
        ((void (*)(id, SEL, NSEvent *))orig)(app, @selector(sendEvent:), e);
    });
    gSendEventOrig = method_setImplementation(m, hook);
    gSendEvent = m;
}

void MagicTrackpad_Init(ScrollCb onScroll, PinchCb onPinch) {
    MagicTrackpad_Shutdown();
    gOnPinch = onPinch;
    HookSendEvent();
    gMonitor = [NSEvent addLocalMonitorForEventsMatchingMask:NSEventMaskScrollWheel handler:^NSEvent *(NSEvent *e) {
        if (gDebug) NSLog(@"[MagicTrackpad] scroll precise=%d phase=%lu momentum=%lu", e.hasPreciseScrollingDeltas,
                          (unsigned long)e.phase, (unsigned long)e.momentumPhase);
        // Smoothing tools make wheel mice report precise deltas, but only
        // real touch scrolling carries a gesture phase.
        BOOL touch = e.phase != NSEventPhaseNone || e.momentumPhase != NSEventPhaseNone;
        if (!e.hasPreciseScrollingDeltas || !touch) return e;
        int mods = ((e.modifierFlags & NSEventModifierFlagShift) ? 1 : 0)
                 | ((e.modifierFlags & NSEventModifierFlagOption) ? 2 : 0)
                 | ((e.modifierFlags & NSEventModifierFlagCommand) ? 4 : 0)
                 | ((e.modifierFlags & NSEventModifierFlagControl) ? 8 : 0);
        return onScroll((float)e.scrollingDeltaX, (float)e.scrollingDeltaY,
                        e.momentumPhase != NSEventPhaseNone, mods) ? nil : e;
    }];
}
