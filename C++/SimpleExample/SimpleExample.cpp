#include <stdio.h>

#include "board_and_dll_chooser.h"

#include <basler_fg.h>

int main(int argc, char* argv[], char* envp[])
{
    Fg_Struct *fg                   = NULL;
    int camPort                     = PORT_A;
    frameindex_t nrOfPicturesToGrab = 1000;
    frameindex_t nbBuffers          = 4;
    unsigned int width              = 512;
    unsigned int height             = 512;
    int samplePerPixel              = 1;
    // Number of channels
    size_t bytePerSample            = 1;

    // initialize libraries
    int status = Fg_InitLibraries(nullptr);
    if (status != FG_OK) {
        fprintf(stderr, "Failed to initialize libraries\n");
        return FG_ERROR;
    }

    int boardNr   = selectBoardDialog();
    char * applet = selectAppletDialog(boardNr);
    if (applet == NULL) {
        fprintf(stderr, "error in selectAppletDialog\n");
        return FG_ERROR;
    }

    fg = Fg_Init(applet, boardNr);
    free(applet);
    if (fg == NULL) {
        fprintf(stderr, "error in Fg_Init: %s\n", Fg_getLastErrorDescription(NULL));
        return FG_ERROR;
    }

    int dispId0 = CreateDisplay((unsigned int) (8 * bytePerSample * samplePerPixel), width, height);
    SetBufferWidth(dispId0, width, height);

    /*Calculate buffer size (careful to avoid integer arithmetic overflows!) and allocate memory.*/
    size_t totalBufferSize = (size_t) width * height * samplePerPixel * bytePerSample * nbBuffers;
    dma_mem *memHandle = Fg_AllocMemEx(fg, totalBufferSize, nbBuffers);
    if (memHandle == NULL) {
        fprintf(stderr, "error in Fg_AllocMemEx: %s\n", Fg_getLastErrorDescription(fg));
        CloseDisplay(dispId0);
        Fg_FreeGrabber(fg);
        return FG_ERROR;
    }
    unsigned int cameraSim = FG_CAMERASIMULATOR;
    if (Fg_setParameter(fg, FG_CAMERASIMULATOR_ENABLE, &cameraSim, camPort) < 0) {
        fprintf(stderr, "Fg_setParameter(FG_CAMERASIMULATOR_ENABLE) failed: %s\n", Fg_getLastErrorDescription(fg));
        Fg_FreeMemEx(fg, memHandle);
        CloseDisplay(dispId0);
        Fg_FreeGrabber(fg);
        return FG_ERROR;
    }

    if (Fg_setParameter(fg, FG_CAMERASIMULATOR_WIDTH, &width, camPort) < 0) {
        fprintf(stderr, "Fg_setParameter(FG_CAMERASIMULATOR_WIDTH) failed: %s\n", Fg_getLastErrorDescription(fg));
        Fg_FreeMemEx(fg, memHandle);
        CloseDisplay(dispId0);
        Fg_FreeGrabber(fg);
        return FG_ERROR;
    }

    if (Fg_setParameter(fg, FG_CAMERASIMULATOR_HEIGHT, &height, camPort) < 0) {
        fprintf(stderr, "Fg_setParameter(FG_CAMERASIMULATOR_WIDTH) failed: %s\n", Fg_getLastErrorDescription(fg));
        Fg_FreeMemEx(fg, memHandle);
        CloseDisplay(dispId0);
        Fg_FreeGrabber(fg);
        return FG_ERROR;
    }

    /*Image width of the acquisition window.*/
    if (Fg_setParameter(fg, FG_WIDTH, &width, camPort) < 0) {
        fprintf(stderr, "Fg_setParameter(FG_WIDTH) failed: %s\n", Fg_getLastErrorDescription(fg));
        Fg_FreeMemEx(fg, memHandle);
        CloseDisplay(dispId0);
        Fg_FreeGrabber(fg);
        return FG_ERROR;
    }

    /*Image height of the acquisition window.*/
    if (Fg_setParameter(fg, FG_HEIGHT, &height, camPort) < 0) {
        fprintf(stderr, "Fg_setParameter(FG_HEIGHT) failed: %s\n", Fg_getLastErrorDescription(fg));
        Fg_FreeMemEx(fg, memHandle);
        CloseDisplay(dispId0);
        Fg_FreeGrabber(fg);
        return FG_ERROR;
    }


    int bitAlignment = FG_LEFT_ALIGNED;
    if (Fg_setParameter(fg, FG_BITALIGNMENT, &bitAlignment, camPort) < 0) {
        fprintf(stderr, "Fg_setParameter(FG_BITALIGNMENT) failed: %s\n", Fg_getLastErrorDescription(fg));
        Fg_FreeMemEx(fg, memHandle);
        CloseDisplay(dispId0);
        Fg_FreeGrabber(fg);
        return FG_ERROR;
    }

    if ((Fg_AcquireEx(fg, camPort, nrOfPicturesToGrab, ACQ_STANDARD, memHandle)) < 0) {
        fprintf(stderr, "Fg_AcquireEx() failed: %s\n", Fg_getLastErrorDescription(fg));
        Fg_FreeMemEx(fg, memHandle);
        CloseDisplay(dispId0);
        Fg_FreeGrabber(fg);
        return FG_ERROR;
    }

    frameindex_t last_pic_nr = 0;
    frameindex_t cur_pic_nr;
    int timeout = 4;
    while ((cur_pic_nr = Fg_getLastPicNumberBlockingEx(fg, last_pic_nr + 1, camPort, timeout, memHandle)) < nrOfPicturesToGrab) {
        if (cur_pic_nr < 0) {
            fprintf(stderr, "Fg_getLastPicNumberBlockingEx(%li) failed: %s\n", (long)last_pic_nr + 1, Fg_getLastErrorDescription(fg));
            Fg_stopAcquire(fg, camPort);
            Fg_FreeMemEx(fg, memHandle);
            CloseDisplay(dispId0);
            Fg_FreeGrabber(fg);
            return FG_ERROR;
        }
        last_pic_nr = cur_pic_nr;
        DrawBuffer(dispId0, Fg_getImagePtrEx(fg, last_pic_nr, camPort, memHandle), static_cast<int>(last_pic_nr), "");
    }

    CloseDisplay(dispId0);
    Fg_stopAcquire(fg, camPort);
    Fg_FreeMemEx(fg, memHandle);
    Fg_FreeGrabber(fg);

    Fg_FreeLibraries();

    return FG_OK;
}
