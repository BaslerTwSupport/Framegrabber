# IMPORT
import numpy as np
import time
import sys
import os
print(sys.path)
print('Importing SiSo Wrapper')
# It can set paths in environment variables by "sys.path".
# reference information https://docs.baslerweb.com/frame-grabbers/python-wrapper#installation
framegrabber_sdk_path = os.environ['BASLER_FG_SDK_DIR']
sys.path.insert(0, rf"{framegrabber_sdk_path}\bin")
sys.path.insert(0, rf"{framegrabber_sdk_path}\SDKWrapper\PythonWrapper\python310\lib")

print(sys.path)
try:
    #import SiSoPyInterface as s
    import SiSoPyInterface as s
except ImportError as e:
    raise ImportError(f'SiSo module not loaded successfully. {e}')

print('Runtime Version', s.Fg_getSWVersion())

# IMPORT additional modules

# for "s.getArrayFrom", to handle grabbed image as NumPy array
print('Importing NumPy', end='')
print('Version', np.__version__)

# DEFINITIONS

# returns count of available boards


def getNrOfBoards():
    nrOfBoards = 0
    (err, buffer, buflen) = s.Fg_getSystemInformation(None, s.INFO_NR_OF_BOARDS, s.PROP_ID_VALUE, 0)
    if (err == s.FG_OK):
        nrOfBoards = int(buffer)
    return nrOfBoards

# Lets the user select one of the available boards, returns the selected board, or -1 if nothing is selected


def selectBoardDialog():
    maxNrOfboards = 10
    nrOfBoardsFound = 0
    nrOfBoardsPresent = getNrOfBoards()
    maxBoardIndex = -1
    minBoardIndex = None

    if (nrOfBoardsPresent <= 0):
        print("No Boards found!")
        return -1

    print('Found', nrOfBoardsPresent, 'Board(s)')

    for i in range(0, maxNrOfboards):
        skipIndex = False
        boardType = s.Fg_getBoardType(i)
        if boardType == s.PN_MICROENABLE5AQ8CXP6B:
            boardName = "MicroEnable V AQ8-CXP"
        elif boardType == s.PN_MICROENABLE5VQ8CXP6B:
            boardName = "MicroEnable V VQ8-CXP"
        elif boardType == s.PN_MICROENABLE5VD8CL:
            boardName = "MicroEnable 5 VD8-CL"
        elif boardType == s.PN_MICROENABLE5AD8CL:
            boardName = "MicroEnable 5 AD8-CL"
        elif boardType == s.PN_MICROENABLE5AQ8CXP6D:
            boardName = "MicroEnable 5 AQ8-CXP6D"
        elif boardType == s.PN_MICROENABLE5VQ8CXP6D:
            boardName = "MicroEnable 5 VQ8-CXP6D"
        elif boardType == s.PN_MICROENABLE5AD8CLHSF2:
            boardName = "MicroEnable 5 AD8-CLHS-F2"
        elif boardType == s.PN_MICROENABLE5_LIGHTBRIDGE_ACL:
            boardName = "MicroEnable 5 LB-ACL"
        elif boardType == s.PN_MICROENABLE5_LIGHTBRIDGE_VCL:
            boardName = "MicroEnable 5 LB-VCL"
        elif boardType == s.PN_MICROENABLE5_MARATHON_ACL:
            boardName = "MicroEnable 5 MA-ACL"
        elif boardType == s.PN_MICROENABLE5_MARATHON_ACX_SP:
            boardName = "MicroEnable 5 MA-ACX-SP"
        elif boardType == s.PN_MICROENABLE5_MARATHON_ACX_DP:
            boardName = "MicroEnable 5 MA-ACX-DP"
        elif boardType == s.PN_MICROENABLE5_MARATHON_ACX_QP:
            boardName = "MicroEnable 5 MA-ACX-QP"
        elif boardType == s.PN_MICROENABLE5_MARATHON_AF2_DP:
            boardName = "MicroEnable 5 MA-AF2-DP"
        elif boardType == s.PN_MICROENABLE5_MARATHON_VCL:
            boardName = "MicroEnable 5 MA-VCL"
        elif boardType == s.PN_MICROENABLE5_MARATHON_VCX_QP:
            boardName = "MicroEnable 5 MA-VCX-QP"
        elif boardType == s.PN_MICROENABLE5_MARATHON_VF2_DP:
            boardName = "MicroEnable 5 MA-VF2-DP"
        elif boardType == s.PN_MICROENABLE6_IMAWORX_CXP12_QUAD:
            boardName = "imaWorx CXP-12 Quad"
        elif boardType == s.PN_MICROENABLE6_CXP12_IC_1C:
            boardName = "Microenable 6 CXP12 IC 1C"
        elif boardType == s.PN_MICROENABLE6_CXP12_IC_2C:
            boardName = "Microenable 6 CXP12 IC 2C"
        elif boardType == s.PN_MICROENABLE6_CXP12_IC_4C:
            boardName = "Microenable 6 CXP12 IC 4C"
        else:
            boardName = "Unknown / Unsupported Board"
            skipIndex = True

        if not skipIndex:
            sys.stdout.write("Board ID " + str(i) + ": " + boardName + " 0x" + format(boardType, '02X') + "\n")
            nrOfBoardsFound = nrOfBoardsFound + 1
            maxBoardIndex = i
            if minBoardIndex == None:
                minBoardIndex = i

        if nrOfBoardsFound >= nrOfBoardsPresent:
            break

        if nrOfBoardsFound < 0:
            break

    if nrOfBoardsFound <= 0:
        print("No Boards found!")
        return -1

    inStr = "=====================================\n\nPlease choose a board[{0}-{1}]: ".format(minBoardIndex, maxBoardIndex)
    userInput = input(inStr)

    while (not userInput.isdigit()) or (int(userInput) > maxBoardIndex):
        inStr = "Invalid selection, retry[{0}-{1}]: ".format(minBoardIndex, maxBoardIndex)
        userInput = input(inStr)

    return int(userInput)

# Lets the user select one of the available applets, and returns the selected applet


def selectAppletDialog(boardIndex):
    iter, err = s.Fg_getAppletIterator(boardIndex, s.FG_AIS_FILESYSTEM, s.FG_AF_IS_LOADABLE)
    appletName = None

    print()
    if err == 0:
        print("No Applets found!")
        return None

    if (err > 0):
        print('Found', err, 'Applet(s)')

        i = 0
        while True:
            item = s.Fg_getAppletIteratorItem(iter, i)
            if item == None:
                break
            appletName = s.Fg_getAppletStringProperty(item, s.FG_AP_STRING_APPLET_NAME)
            sys.stdout.write(str(i) + ": " + appletName + "\n")
            i += 1

        maxAppletIndex = i - 1
        inStr = "=====================================\n\nPlease choose an Applet [0-" + str(maxAppletIndex) + "]: "
        userInput = input(inStr)

        while (not userInput.isdigit()) or (int(userInput) > maxAppletIndex):
            inStr = "Invalid selection, retry[0-" + str(maxAppletIndex) + "]: "
            userInput = input(inStr)

        item = s.Fg_getAppletIteratorItem(iter, int(userInput))
        appletName = s.Fg_getAppletStringProperty(item, s.FG_AP_STRING_APPLET_NAME)

        s.Fg_freeAppletIterator(iter)

    return appletName

# Additional Class definition
# Data for APC Callback


class MyApcData:
    fg = None
    port = 0
    mem = None
    displayid = 0

    def __init__(self, fg, port, mem, displayid):
        self.fg = fg
        self.port = port
        self.mem = mem
        self.displayid = displayid


global_imgNr = -1
# Callback function definition


def apcCallback(imgNr, userData):
    s.DrawBuffer(userData.displayid, s.Fg_getImagePtrEx(userData.fg, imgNr, userData.port, userData.mem), imgNr, "")
    return 0


def main():
    # MAIN

    # Board and applet selection
    boardId = selectBoardDialog()
    #boardId = 0
    if boardId < 0:
        exit(1)

    # definition of resolution
    width = 512
    height = 512
    samplePerPixel = 1
    bytePerSample = 1
    isSlave = False
    useCameraSimulator = True
    camPort = s.PORT_A

    # number of buffers for acquisition
    nbBuffers = 10
    totalBufferSize = width * height * samplePerPixel * bytePerSample * nbBuffers

    # number of image to acquire
    nrOfPicturesToGrab = 10
    frameRate = 100

    # Get Loaded Applet
    applet = selectAppletDialog(boardId)
    if applet == None:
        exit(0)

    # INIT FRAMEGRABBER

    print('Initializing Board ..', end='')

    if isSlave:
        fg = s.Fg_InitEx(applet, boardId, 1)
    else:
        fg = s.Fg_InitEx(applet, boardId, 0)

    # error handling
    err = s.Fg_getLastErrorNumber(fg)
    mes = s.Fg_getErrorDescription(err)

    if err < 0:
        print("Error", err, ":", mes)
        sys.exit()
    else:
        print("ok")

    # allocating memory
    memHandle = s.Fg_AllocMemEx(fg, totalBufferSize, nbBuffers)

    # Set Applet Parameters
    err = s.Fg_setParameterWithInt(fg, s.FG_WIDTH, width, camPort)
    if (err < 0):
        print("Fg_setParameter(FG_WIDTH) failed: ", s.Fg_getLastErrorDescription(fg))
        s.Fg_FreeMemEx(fg, memHandle)
        s.Fg_FreeGrabber(fg)
        exit(err)

    err = s.Fg_setParameterWithInt(fg, s.FG_HEIGHT, height, camPort)
    if (err < 0):
        print("Fg_setParameter(FG_HEIGHT) failed: ", s.Fg_getLastErrorDescription(fg))
        s.Fg_FreeMemEx(fg, memHandle)
        s.Fg_FreeGrabber(fg)
        exit(err)

    err = s.Fg_setParameterWithInt(fg, s.FG_BITALIGNMENT, s.FG_LEFT_ALIGNED, camPort)
    if (err < 0):
        print("Fg_setParameter(FG_BITALIGNMENT) failed: ", s.Fg_getLastErrorDescription(fg))
        #s.Fg_FreeMemEx(fg, memHandle)
        # s.Fg_FreeGrabber(fg)
        # exit(err)

    if useCameraSimulator:
        # Start Generator
        print("Using camera simulator.")
        s.Fg_setParameterWithInt(fg, s.FG_GEN_ENABLE, s.FG_GENERATOR, camPort)
    #	s.Fg_setParameterWithInt(fg, s.FG_GEN_ROLL, 1, camPort)
    else:
        # Initialize CXP-Board
        err, sgc = s.Sgc_initBoard(fg, 0)
        # Scan for cameras
        s.Sgc_scanPorts(sgc, 0xf, 1000, 0)
        # Get first camera
        err, CameraHandle = s.Sgc_getCameraByIndex(sgc, 0)
        if (err != 0):
            print("No camera found, fallback to camera simulator.")
            useCameraSimulator = True
            s.Fg_setParameterWithInt(fg, s.FG_GEN_ENABLE, s.FG_CAMPORT, camPort)
        else:
            # Connect camera
            s.Sgc_connectCamera(CameraHandle)
            s.Sgc_setIntegerValue(CameraHandle, "Width", width)
            s.Sgc_setIntegerValue(CameraHandle, "Height", height)
            err, vendor = s.Sgc_getStringValue(CameraHandle, "DeviceVendorName")
            err, model = s.Sgc_getStringValue(CameraHandle, "DeviceModelName")
            print("Connected to camera: " + vendor + " " + model)

    # Read back settings
    (err, oWidth) = s.Fg_getParameterWithInt(fg, s.FG_WIDTH, camPort)
    if (err == 0):
        print('Width =', oWidth)
    (err, oHeight) = s.Fg_getParameterWithInt(fg, s.FG_HEIGHT, camPort)
    if (err == 0):
        print('Height =', oHeight)
    (err, oString) = s.Fg_getParameterWithString(fg, s.FG_HAP_FILE, camPort)
    if (err == 0):
        print('Hap File =', oString)

    # create a display window
    dispId0 = s.CreateDisplay(8 * bytePerSample * samplePerPixel, width, height)
    s.SetBufferWidth(dispId0, width, height)

    # Register acquisition call back

    # Define FgApcControl instance to handle the callback
    apcCtrl = s.FgApcControl(5, s.FG_APC_DEFAULTS)
    data = MyApcData(fg, camPort, memHandle, dispId0)
    s.setApcCallbackFunction(apcCtrl, apcCallback, data)
    # Register the FgApcControl instance to the Fg_Struct instance
    err = s.Fg_registerApcHandler(fg, camPort, apcCtrl, s.FG_APC_CONTROL_BASIC)
    if err != s.FG_OK:
        print("registering APC handler failed:", s.Fg_getErrorDescription(err))
        s.Fg_FreeMemEx(fg, memHandle)
        s.CloseDisplay(dispId0)
        s.Fg_FreeGrabber(fg)
        exit(err)

    # Start acquisition
    print("Acquisition started")

    err = s.Fg_AcquireEx(fg, camPort, nrOfPicturesToGrab, s.ACQ_STANDARD, memHandle)
    if (err != 0):
        print('Fg_AcquireEx() failed:', s.Fg_getLastErrorDescription(fg))
        s.Fg_FreeMemEx(fg, memHandle)
        s.CloseDisplay(dispId0)
        s.Fg_FreeGrabber(fg)
        exit(err)

    # Start camera
    if not useCameraSimulator:
        s.Sgc_startAcquisition(CameraHandle, 1)

    time.sleep(5)

    input("Acquisition stopped. Press <Enter> to continue.")

    s.CloseDisplay(dispId0)

    # Clean up
    if (fg != None):
        if not useCameraSimulator:
            s.Sgc_stopAcquisition(CameraHandle, 1)
        s.Fg_registerApcHandler(fg, camPort, None, s.FG_APC_CONTROL_BASIC)
        s.Fg_stopAcquire(fg, camPort)
        s.Fg_FreeMemEx(fg, memHandle)
        if not useCameraSimulator:
            s.Sgc_freeBoard(sgc)
        s.Fg_FreeGrabber(fg)

    print("Exited.")


if __name__ == "__main__":
    try:
        main()
    except Exception as e:
        print(str(e))
