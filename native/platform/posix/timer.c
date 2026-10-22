// POSIX timer backend for the native iBaye build.
// Mirrors platform/js/timer.c: interval unit is 10ms, two general timers
// plus one dedicated LCD-flush timer.
#include <pthread.h>
#include <time.h>
#include <string.h>
#include "timer.h"

typedef void (*gtimer_cb)(void);

typedef struct {
    int active;
    int interval;   // in 10ms units
    int elapsed;    // in ms
    gtimer_cb cb;
} gtimer;

static gtimer timers[3];
static pthread_t timer_thread;
static pthread_mutex_t timer_lock = PTHREAD_MUTEX_INITIALIZER;
static int timer_started = 0;

static void *timer_loop(void *arg)
{
    (void)arg;
    struct timespec tick = {0, 5 * 1000 * 1000}; // 5ms
    for (;;) {
        nanosleep(&tick, NULL);
        pthread_mutex_lock(&timer_lock);
        for (int i = 0; i < 3; ++i) {
            gtimer *t = &timers[i];
            if (!t->active || !t->cb)
                continue;
            t->elapsed += 5;
            int need = t->interval * 10;
            if (need <= 0)
                need = 10;
            if (t->elapsed >= need) {
                t->elapsed = 0;
                gtimer_cb cb = t->cb;
                pthread_mutex_unlock(&timer_lock);
                cb();
                pthread_mutex_lock(&timer_lock);
            }
        }
        pthread_mutex_unlock(&timer_lock);
    }
    return NULL;
}

void gam_timer_init(void)
{
    if (timer_started)
        return;
    timer_started = 1;
    memset(timers, 0, sizeof(timers));
    pthread_create(&timer_thread, NULL, timer_loop, NULL);
}

void gam_timer_set_callback(U8 n, void (*cb)(void))
{
    if (n > 2) return;
    pthread_mutex_lock(&timer_lock);
    timers[n].cb = cb;
    pthread_mutex_unlock(&timer_lock);
}

int gam_timer_open(U8 n, int interval)
{
    if (n > 2) return 0;
    pthread_mutex_lock(&timer_lock);
    int prev = timers[n].active ? timers[n].interval : 0;
    if (interval == 0) {
        timers[n].active = 0;
        timers[n].elapsed = 0;
    } else {
        timers[n].interval = interval;
        timers[n].elapsed = 0;
        timers[n].active = 1;
    }
    pthread_mutex_unlock(&timer_lock);
    return prev;
}

void gam_timer_close(U8 n)
{
    gam_timer_open(n, 0);
}

U8 gam_check_timer_on(U8 n)
{
    if (n > 2) return 0;
    return (U8)timers[n].active;
}

int gam_timer_interval(U8 n)
{
    if (n > 2) return 0;
    return timers[n].interval;
}

void gam_timer_set_interval(U8 n, int interval)
{
    if (n > 2) return;
    pthread_mutex_lock(&timer_lock);
    timers[n].interval = interval;
    pthread_mutex_unlock(&timer_lock);
}

void gam_timer2_open(int interval, void (*callback)(void))
{
    pthread_mutex_lock(&timer_lock);
    timers[2].interval = interval;
    timers[2].cb = callback;
    timers[2].elapsed = 0;
    timers[2].active = 1;
    pthread_mutex_unlock(&timer_lock);
}
