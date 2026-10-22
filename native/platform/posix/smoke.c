/*
 * Headless smoke harness for the native iBaye build.
 *
 * It boots the C engine on a worker thread, feeds a fixed key sequence
 * (title -> new game -> period -> ruler -> map), and dumps the framebuffer
 * as PPM files.  This is the reference integration for the future
 * GDExtension wrapper:
 *
 *   - engine runs on its own thread and blocks in GuiGetMsg()
 *   - the presentation layer is called at those block points, when a frame
 *     is complete (never mid-draw)
 *   - input is injected with GuiPushMsg()
 *
 * Usage:
 *   baye_smoke <lib> <fonts> <saves> <outdir>
 *
 * The defaults are only convenient values for a checkout; the run script
 * always passes absolute paths.
 */
#include <errno.h>
#include <pthread.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <sys/stat.h>
#include <unistd.h>

#include "inc/dictsys.h"
#include "baye/comm.h"
#include "baye/consdef.h"

FAR U8 GamConInit(void);
FAR void GamBaYeEng(void);
FAR void GamSetResourcePath(const U8 *datPath, const U8 *fontDir);
FAR void GamSetDataDir(const U8 *dataDir);
FAR void GamSetLcdFlushCallback(void (*cb)(char *buffer));
FAR void SysFlushLcdNow(void);
void GuiSetMsgWaitCallback(void (*cb)(void));

static volatile int g_frames = 0;
static volatile int g_safe_flush = 0;
static pthread_mutex_t g_frame_lock = PTHREAD_MUTEX_INITIALIZER;
static unsigned char g_last[2048 * 2048 * 4];
static const char *g_outdir = "build/frames";

static void dump_frame(const char *path)
{
    int w = g_screenWidth * g_scale;
    int h = g_screenHeight * g_scale;
    if (w <= 0 || h <= 0 || w > 2048 || h > 2048)
        return;

    FILE *f = fopen(path, "wb");
    if (!f)
        return;
    fprintf(f, "P6\n%d %d\n255\n", w, h);
    for (int i = 0; i < w * h; ++i) {
        const unsigned char *p = &g_last[i * 4];
        unsigned char rgb[3] = { p[2], p[1], p[0] };
        fwrite(rgb, 1, 3, f);
    }
    fclose(f);
}

static void flush_frame(char *buffer)
{
    /* The timer-driven flush can run while the engine is drawing.  Only
       save frames that were captured from the host-wait callback, where
       the current frame is known to be complete. */
    if (!g_safe_flush)
        return;

    int w = g_screenWidth * g_scale;
    int h = g_screenHeight * g_scale;
    if (w <= 0 || h <= 0 || w > 2048 || h > 2048)
        return;

    int len = w * h * 4;
    pthread_mutex_lock(&g_frame_lock);
    memcpy(g_last, buffer, len);
    g_frames++;
    int n = g_frames;
    pthread_mutex_unlock(&g_frame_lock);

    if (n <= 5 || n % 50 == 0) {
        char path[512];
        snprintf(path, sizeof(path), "%s/f%05d.ppm", g_outdir, n);
        dump_frame(path);
        printf("[smoke] frame %d %dx%d -> %s\n", n, w, h, path);
        fflush(stdout);
    }
}

static void on_msg_wait(void)
{
    g_safe_flush = 1;
    SysFlushLcdNow();
    g_safe_flush = 0;
}

static void send_key(int key)
{
    MsgType msg;
    memset(&msg, 0, sizeof(msg));
    msg.type = VM_CHAR_FUN;
    msg.param = (U16)key;
    GuiPushMsg(&msg);
    printf("[smoke] key 0x%02x\n", key);
    fflush(stdout);
}

static void *engine_main(void *arg)
{
    (void)arg;
    GamBaYeEng();
    return NULL;
}

int main(int argc, char **argv)
{
    setvbuf(stdout, NULL, _IONBF, 0);

    const char *lib = argc > 1 ? argv[1] : "libs/SGBY.lib";
    const char *fonts = argc > 2 ? argv[2] : "assets/fonts";
    const char *saves = argc > 3 ? argv[3] : "build/saves";
    g_outdir = argc > 4 ? argv[4] : "build/frames";

    if (mkdir(g_outdir, 0755) != 0 && errno != EEXIST) {
        fprintf(stderr, "cannot create output dir %s: %s\n", g_outdir, strerror(errno));
        return 1;
    }

    printf("[smoke] lib=%s\n[smoke] fonts=%s\n[smoke] saves=%s\n[smoke] out=%s\n",
           lib, fonts, saves, g_outdir);

    GamSetResourcePath((const U8 *)lib, (const U8 *)fonts);
    GamSetDataDir((const U8 *)saves);
    GamSetLcdFlushCallback(flush_frame);

    /* The H5 shell sets the logical screen before the engine initializes.
       SGBY's lib script does the same on the web build (240x144 at scale 2
       -> a 480x288 framebuffer).  The original game area is 160x96 inside
       that canvas. */
    g_screenWidth = 240;
    g_screenHeight = 144;

    if (GamConInit()) {
        fprintf(stderr, "[smoke] GamConInit failed\n");
        return 1;
    }
    printf("[smoke] logical screen %ux%u scale=%u\n",
           g_screenWidth, g_screenHeight, g_scale);

    GuiSetMsgWaitCallback(on_msg_wait);

    pthread_t th;
    if (pthread_create(&th, NULL, engine_main, NULL) != 0) {
        fprintf(stderr, "[smoke] cannot start engine thread\n");
        return 1;
    }

    /* Skip the opening movie, pick the first period, first ruler, enter map. */
    usleep(3000 * 1000);
    send_key(VK_ENTER);
    usleep(800 * 1000);
    send_key(VK_ENTER);
    usleep(800 * 1000);
    send_key(VK_DOWN);
    usleep(200 * 1000);
    send_key(VK_ENTER);
    usleep(800 * 1000);
    send_key(VK_ENTER);

    sleep(8);

    char final_path[512];
    snprintf(final_path, sizeof(final_path), "%s/final.ppm", g_outdir);
    dump_frame(final_path);
    printf("[smoke] final -> %s\n[smoke] done, frames=%d\n", final_path, g_frames);
    fflush(stdout);

    /* GamBaYeEng never returns; leave the process while the engine thread
     * is still parked. */
    _exit(0);
}
