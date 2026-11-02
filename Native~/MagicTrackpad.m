// Forwards trackpad scroll and pinch to managed code, which decides whether
// Unity still gets to see them.
#import <Cocoa/Cocoa.h>
#import <objc/runtime.h>

// Return 1 to consume the event (Unity never sees it), 0 to let it through.
// mods: 1 = shift, 2 = option, 4 = command.
typedef int (*ScrollCb)(float dx, float dy, int momentum, int mods);
typedef int (*PinchCb)(float magnification);

static id gMonitor;
static int gDebug;
static PinchCb gOnPinch;
static Method gMagnify; // -[GUIRenderView magnifyWithEvent:]
static IMP gMagnifyOrig;

// Bump with any change to the exports or callback signatures. Unity keeps the
// dylib loaded across domain reloads, so managed must check it before Init.
int MagicTrackpad_Version(void) { return 5; }

void MagicTrackpad_SetDebug(int on) { gDebug = on; }

void MagicTrackpad_Shutdown(void) {
    if (gMonitor) { [NSEvent removeMonitor:gMonitor]; gMonitor = nil; }
    if (gMagnify) { method_setImplementation(gMagnify, gMagnifyOrig); gMagnify = NULL; }
    gOnPinch = NULL;
}

// Unity's panel maximize (WindowLayout.MaximizeGestureHandler) is fed from this
// view method and an NSEvent monitor does not stop it, so replace the method.
static void HookMagnify(void) {
    Method m = class_getInstanceMethod(objc_getClass("GUIRenderView"), @selector(magnifyWithEvent:));
    if (!m) { NSLog(@"[MagicTrackpad] GUIRenderView magnifyWithEvent: not found, pinch unchanged"); return; }
    IMP orig = method_getImplementation(m);
    IMP hook = imp_implementationWithBlock(^(id view, NSEvent *e) {
        if (gDebug) NSLog(@"[MagicTrackpad] magnify %f", e.magnification);
        if (gOnPinch && gOnPinch((float)e.magnification)) return;
        ((void (*)(id, SEL, NSEvent *))orig)(view, @selector(magnifyWithEvent:), e);
    });
    gMagnifyOrig = method_setImplementation(m, hook);
    gMagnify = m;
}

void MagicTrackpad_Init(ScrollCb onScroll, PinchCb onPinch) {
    MagicTrackpad_Shutdown();
    gOnPinch = onPinch;
    HookMagnify();
    gMonitor = [NSEvent addLocalMonitorForEventsMatchingMask:NSEventMaskScrollWheel handler:^NSEvent *(NSEvent *e) {
        if (gDebug) NSLog(@"[MagicTrackpad] scroll precise=%d phase=%lu momentum=%lu", e.hasPreciseScrollingDeltas,
                          (unsigned long)e.phase, (unsigned long)e.momentumPhase);
        // Smoothing tools make wheel mice report precise deltas, but only
        // real touch scrolling carries a gesture phase.
        BOOL touch = e.phase != NSEventPhaseNone || e.momentumPhase != NSEventPhaseNone;
        if (!e.hasPreciseScrollingDeltas || !touch) return e;
        int mods = ((e.modifierFlags & NSEventModifierFlagShift) ? 1 : 0)
                 | ((e.modifierFlags & NSEventModifierFlagOption) ? 2 : 0)
                 | ((e.modifierFlags & NSEventModifierFlagCommand) ? 4 : 0);
        return onScroll((float)e.scrollingDeltaX, (float)e.scrollingDeltaY,
                        e.momentumPhase != NSEventPhaseNone, mods) ? nil : e;
    }];
}
