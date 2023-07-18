using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Events
{
    /**
     * Helper for handling resources that require cleanup.
     */
    class CleanupHelper
    {
        private static LinkedList<Action> cleanupActions = new LinkedList<Action>();

        public static void AddCleanupAction(Action action, String message)
        {
            // Add at the front to perform cleanup in reverse order.
            cleanupActions.AddFirst(() => { action(); Console.WriteLine(message); });
        }

        public static void PerformCleanup()
        {
            foreach (var action in cleanupActions)
            {
                action();
            }
        }
    }
    internal class Program
    {
        #region User Interface
        public static int getNrOfBoards()
        {
            int nrOfBoards = 0;
            byte[] buffer = new byte[256];
            uint buflen = 256;
            buffer[0] = 0;

            if (SiSoCsRt.Fg_getSystemInformation(null, Fg_Info_Selector.INFO_NR_OF_BOARDS, FgProperty.PROP_ID_VALUE, 0, buffer, ref buflen) == SiSoCsRt.FG_OK)
            {
                nrOfBoards = int.Parse(Encoding.ASCII.GetString(buffer));
            }
            return nrOfBoards;
        }

        public static int selectBoardDialog()
        {
            int boardType;
            int i = 0;

            int maxNrOfboards = 10;
            int nrOfBoardsFound = 0;
            int nrOfBoardsPresent = getNrOfBoards();
            int maxBoardIndex = -1;

            for (i = 0; i < maxNrOfboards; i++)
            {
                string boardName;
                bool skipIndex = false;
                boardType = SiSoCsRt.Fg_getBoardType(i);
                switch ((siso_board_type)(boardType))
                {
                    case siso_board_type.PN_MICROENABLE5_MARATHON_ACL:
                        boardName = "microEnable 5 marathon ACL";
                        break;
                    case siso_board_type.PN_MICROENABLE5_MARATHON_VCL:
                        boardName = "microEnable 5 marathon VCL";
                        break;
                    case siso_board_type.PN_MICROENABLE5_MARATHON_ACX_SP:
                        boardName = "microEnable 5 marathon ACX SP";
                        break;
                    case siso_board_type.PN_MICROENABLE5_MARATHON_ACX_DP:
                        boardName = "microEnable 5 marathon ACX DP";
                        break;
                    case siso_board_type.PN_MICROENABLE5_MARATHON_ACX_QP:
                        boardName = "microEnable 5 marathon ACX QP";
                        break;
                    case siso_board_type.PN_MICROENABLE5_MARATHON_VCX_QP:
                        boardName = "microEnable 5 marathon VCX QP";
                        break;
                    case siso_board_type.PN_MICROENABLE5_MARATHON_VF2_DP:
                        boardName = "microEnable 5 marathon VF2";
                        break;
                    case siso_board_type.PN_MICROENABLE6_IMAWORX_CXP12_QUAD:
                        boardName = "microenable 6 imaWorx CXP12 QUAD";
                        break;
                    case siso_board_type.PN_MICROENABLE6_CXP12_IC_1C:
                        boardName = "microenable 6 CXP12 IC 1C";
                        break;
                    case siso_board_type.PN_MICROENABLE6_CXP12_IC_2C:
                        boardName = "microenable 6 CXP12 IC 2C";
                        break;
                    case siso_board_type.PN_MICROENABLE6_CXP12_IC_4C:
                        boardName = "microenable CXP12 IC 4C";
                        break;
                    default:
                        boardName = "Unknown / Unsupported Board";
                        skipIndex = true;
                        break;
                }
                if (!skipIndex)
                {
                    Console.WriteLine("Board ID {0}: {1} 0x{2:X}", i, boardName, boardType);
                    nrOfBoardsFound++;
                    maxBoardIndex = i;
                }
                if (nrOfBoardsFound >= nrOfBoardsPresent)
                {
                    break;// all boards are scanned
                }
            }


            if (nrOfBoardsFound <= 0)
            {
                Console.Write("No Boards found!");
                return -1;
            }

            Console.Write("\n=====================================\n\nPlease choose a board[0-{0}]: ", maxBoardIndex);
            //fflush(stdout);
            int userInput = -1;
            do
            {
                string inputBuffer = Console.ReadLine();
                userInput = int.Parse(inputBuffer);
                if (userInput > maxBoardIndex)
                {
                    Console.Write("Invalid selection, retry[0-{0}]: ", maxBoardIndex);
                }
            } while (userInput > maxBoardIndex);

            return userInput;
        }
        #endregion

        #region Event Handling

        /**
         * Custom data to be passed to the event callbacks.
         */
        class EventData : SiSoUserContext
        {
            public Fg_Struct Fg
            {
                get;
                private set;
            }

            public EventData(Fg_Struct fg)
            {
                Fg = fg;
            }
        }

        private static int getEventIdxFromFlag(ulong eventFlag)
        {
            for (int bitPos = 0; bitPos < sizeof(ulong) * 8; ++bitPos)
            {
                if ((eventFlag & (1ul << bitPos)) != 0)
                {
                    return bitPos;
                }
            }

            return -1;
        }

        /**
         * Prints information about every event in an event mask.
         */
        private static void printEvents(ulong events, fg_event_info info, EventData eventData)
        {
            for (int bitPos = 0; bitPos < sizeof(ulong) * 8; ++bitPos)
            {
                ulong eventFlag = 1ul << bitPos;
                if ((events & eventFlag) != 0)
                {
                    // The event index is its bit position in the mask.
                    uint eventIdx = (uint)bitPos;

                    FgEventNotifiers notifiers = (FgEventNotifiers)info.getNotify(eventIdx);
                    Console.WriteLine("EventName: {0}\n  Notifiers: {1}", SiSoCsRt.Fg_getEventName(eventData.Fg, eventFlag), notifiers);
                    if ((notifiers & FgEventNotifiers.FG_EVENT_NOTIFY_TIMESTAMP) == FgEventNotifiers.FG_EVENT_NOTIFY_TIMESTAMP)
                    {
                        Console.WriteLine("  Timestamp: {0}", info.getTimestamp(eventIdx));
                    }
                }
            }
        }

        /**
         * Handles all events with payloud.
         * Multiple events can be handled in one call.
         */
        static int handleEventWithoutPayload(ulong events, SiSoUserContext data, fg_event_info info)
        {
            Console.WriteLine("Handling EventMask 0x{0:X}", events);

            // cast should always succeed...
            EventData eventData = data as EventData;
            if (eventData == null)
            {
                Console.WriteLine("No event data...");
                return 0;
            }

            printEvents(events, info, eventData);

            return 0;
        }

        /**
         * Handles an overflow event.
         * Only one event per call.
         */
        static int handleOverflowEvent(ulong events, SiSoUserContext data, fg_event_info info)
        {
            Console.WriteLine("Handling Overflow event 0x{0:X}", events);

            // cast should always succeed...
            EventData eventData = data as EventData;
            if (eventData == null)
            {
                Console.WriteLine("No event data...");
                return 0;
            }

            printEvents(events, info, eventData);

            int eventIdx = getEventIdxFromFlag(events);
            if (eventIdx < 0)
            {
                return 0;
            }

            FgEventNotifiers notifiers = (FgEventNotifiers)info.getNotify((uint)eventIdx);
            bool isPayloadPresent = ((notifiers & FgEventNotifiers.FG_EVENT_NOTIFY_PAYLOAD) == FgEventNotifiers.FG_EVENT_NOTIFY_PAYLOAD);

            // data should contain 3 bytes:
            //   - frame Id (2 bytes)
            //   - overflow mask (1 byte)
            if (isPayloadPresent && info.getDataLength() == 3)
            {
                ushort[] overflowData = info.getData();

                uint frameId = overflowData[0] + ((uint)overflowData[1] << 16);
                Console.WriteLine("  Frame ID: {0}", frameId);

                ushort overflowMask = overflowData[2];
                if ((overflowMask & 1) != 0)
                {
                    // Bit 0: frame corrupted
                    Console.WriteLine("  Frame corrupted");
                }
                else if ((overflowMask & 2) != 0)
                {
                    // Bit 1: frame lost
                    Console.WriteLine("  Frame lost");

                    if ((overflowMask & 4) != 0)
                    {
                        // Bit 2: loss occurred before
                        Console.WriteLine("     ... again");
                    }
                }
                else if ((overflowMask & 4) != 0)
                {
                    Console.WriteLine("  Frame OK");
                }
            }

            return 0;
        }

        #endregion

        #region Error Handling

        /**
         * Create a message that includes the internal error description for an FG error code.
         */
        static String createFgResultMessage(int fgResult, String actionDescription)
        {
            if (fgResult == SiSoCsRt.FG_OK)
            {
                return String.Format("{0}: ok", actionDescription);
            }
            else if (fgResult > 0)
            {
                return String.Format("{0}: ok (result = {1})", actionDescription, fgResult);
            }
            else
            {
                return String.Format("{0}: FAILED\n  Error Code: {1}\n  Error Description: {2}",
                        actionDescription, fgResult, SiSoCsRt.Fg_getErrorDescription(fgResult));
            }
        }

        /**
         * Checks if a fg result code is an error and either loggs a success message
         * or throws an exception with a proper error message.
         */
        static void checkFgResultOrThrow(int fgResult, String actionDescription)
        {
            if (fgResult >= 0)
            {
                Console.WriteLine(createFgResultMessage(fgResult, actionDescription));
            }
            else
            {
                throw new Exception(createFgResultMessage(fgResult, actionDescription));
            }
        }

        /**
         * Overload
         */
        static void checkFgResultOrThrow(Fg_Struct fg, String actionDescription)
        {
            int errorCode = SiSoCsRt.Fg_getLastErrorNumber(fg);
            checkFgResultOrThrow(errorCode, actionDescription);
        }

        #endregion  
        static void Main(string[] args)
        {
            try
            {
                int result = SiSoCsRt.Fg_InitLibraries(null);
                checkFgResultOrThrow(result, "Init Libraries");
                CleanupHelper.AddCleanupAction(SiSoCsRt.Fg_FreeLibraries, "Free Libraries");

                int boardId = selectBoardDialog();
                if (boardId < 0)
                {
                    throw new Exception("No board selected");
                }

                uint camPort = (uint)(SiSoCsRt.PORT_A);
                int nrOfPicturesToGrab = 5;
                int nbBuffers = 4;
                uint width = 512;
                uint height = 512;
                bool isSlave = false;
                bool useCameraSimulator = true;
                double simulatorFramerate = 10;

                // uncomment the following to produce Overflow Events
                // ( + make sure to use an RGB or Bayer Applet )
                //width = 1024;
                //height = 1024;
                //simulatorFramerate = 2000;

                #region init

                int err = 0;
                var iter = SiSoCsRt.Fg_getAppletIterator(boardId, FgAppletIteratorSource.FG_AIS_FILESYSTEM, (int)FgAppletIteratorFlags.FG_AF_IS_LOADABLE, out err);
                string appletName;

                Console.WriteLine();
                if (err == 0)
                {
                    throw new Exception("No Applets found!");
                }
                int i = 0;
                if (err > 0)
                {
                    Console.WriteLine("Found " + err + " Applet(s): ");
                    while (err > 0)
                    {
                        var iterItem = SiSoCsRt.Fg_getAppletIteratorItem(iter, i);
                        if (iterItem == null) { break; }
                        appletName = SiSoCsRt.Fg_getAppletStringProperty(iterItem, FgAppletStringProperty.FG_AP_STRING_APPLET_NAME);
                        Console.WriteLine(i + ": " + appletName + "\n");
                        i += 1;
                        err -= 1;
                    }
                }
                int maxAppletIndex = i - 1;
                Console.WriteLine("=====================================\n\nPlease choose an Applet [0-" + maxAppletIndex + "]: ");
                int userInput = -1;
                do
                {
                    string inputBuffer = Console.ReadLine();
                    userInput = int.Parse(inputBuffer);
                    if (userInput > maxAppletIndex)
                    {
                        Console.Write("Invalid selection, retry[0-{0}]: ", maxAppletIndex);
                    }
                } while (userInput > maxAppletIndex);

                var item = SiSoCsRt.Fg_getAppletIteratorItem(iter, userInput);
                appletName = SiSoCsRt.Fg_getAppletStringProperty(item, FgAppletStringProperty.FG_AP_STRING_APPLET_NAME);
                SiSoCsRt.Fg_freeAppletIterator(iter);

                Console.WriteLine("Using Applet '{0}'", appletName);

                Fg_Struct fg = SiSoCsRt.Fg_InitEx(appletName, (uint)(boardId), isSlave ? 1 : 0);
                checkFgResultOrThrow(fg, "Init FG Handle");
                CleanupHelper.AddCleanupAction(() => SiSoCsRt.Fg_FreeGrabber(fg), "Free FG Handle");

                // Set Applet Parameters
                /* Image width of the acquisition window. */
                result = SiSoCsRt.Fg_setParameterWithUInt(fg, SiSoCsRt.FG_WIDTH, width, camPort);
                checkFgResultOrThrow(result, "Set Parameter FG_WIDTH");

                /* Image height of the acquisition window. */
                result = SiSoCsRt.Fg_setParameterWithUInt(fg, SiSoCsRt.FG_HEIGHT, height, camPort);
                checkFgResultOrThrow(result, "Set Parameter FG_HEIGHT");

                int bitAlignment = SiSoCsRt.FG_LEFT_ALIGNED;
                result = SiSoCsRt.Fg_setParameterWithInt(fg, SiSoCsRt.FG_BITALIGNMENT, bitAlignment, camPort);
                if (result != SiSoCsRt.FG_OK)
                {
                    Console.WriteLine("Fg_setParameter(FG_BITALIGNMENT) failed: {0}\n", SiSoCsRt.Fg_getLastErrorDescription(fg));
                }

                if (useCameraSimulator)
                {
                    result = SiSoCsRt.Fg_setParameterWithInt(fg, SiSoCsRt.FG_GEN_ENABLE, (int)(FgImageSourceTypes.FG_GENERATOR), camPort);
                    checkFgResultOrThrow(result, "Enabled Camera Simulator");

                    result = SiSoCsRt.Fg_setParameterWithDouble(fg, SiSoCsRt.FG_CAMERASIMULATOR_FRAMERATE, simulatorFramerate, camPort);
                    Console.WriteLine(createFgResultMessage(result, "Set Camera Simulator Framerate to " + simulatorFramerate));
                }

                /* Reading back parameters */
                {
                    int oWidth = 0;
                    int oHeight = 0;
                    int oBitAlignment = 0;
                    int oGenEnabled = 0;
                    string oString = "";
                    if (SiSoCsRt.Fg_getParameterWithInt(fg, SiSoCsRt.FG_WIDTH, out oWidth, camPort) == 0)
                    {
                        Console.WriteLine("Width = {0}", oWidth);
                    }
                    if (SiSoCsRt.Fg_getParameterWithInt(fg, SiSoCsRt.FG_HEIGHT, out oHeight, camPort) == 0)
                    {
                        Console.WriteLine("Height = {0}", oHeight);
                    }
                    if (SiSoCsRt.Fg_getParameterWithString(fg, SiSoCsRt.FG_HAP_FILE, out oString, camPort) == 0)
                    {
                        Console.WriteLine("Hap File = {0}", oString);
                    }
                    if (SiSoCsRt.Fg_getParameterWithInt(fg, SiSoCsRt.FG_BITALIGNMENT, out oBitAlignment, camPort) == 0)
                    {
                        string align;
                        if (oBitAlignment == SiSoCsRt.FG_LEFT_ALIGNED)
                        {
                            align = "Left Aligned";
                        }
                        else if (oBitAlignment == SiSoCsRt.FG_RIGHT_ALIGNED)
                        {
                            align = "Right Aligned";
                        }
                        else
                        {
                            align = "Unknown";
                        }
                        Console.WriteLine("Bit Alignment = {0}", align);
                    }
                    if (SiSoCsRt.Fg_getParameterWithInt(fg, SiSoCsRt.FG_GEN_ENABLE, out oGenEnabled, camPort) == 0)
                    {
                        string gen;
                        if (oGenEnabled == (int)(FgImageSourceTypes.FG_GENERATOR))
                        {
                            gen = "enabled";
                        }
                        else
                        {
                            gen = "disabled";
                        }
                        Console.WriteLine("Generator: {0}", gen);
                    }
                }

                int format;
                result = SiSoCsRt.Fg_getParameterWithInt(fg, SiSoCsRt.FG_FORMAT, out format, camPort);
                checkFgResultOrThrow(result, "Get Parameter FG_FORMAT");

                int bitsPerPixel = SiSoCsRt.Fg_getBitsPerPixel(format);
                checkFgResultOrThrow(bitsPerPixel, "Get Bits per Pixel");

                // Allocate DMA
                uint totalBufferSize = (uint)SiSoCsRt.IoCalculateBufferSize((int)width, (int)height, bitsPerPixel) * (uint)nbBuffers;
                dma_mem memHandle = SiSoCsRt.Fg_AllocMemEx(fg, totalBufferSize, nbBuffers);
                if (memHandle == null)
                {
                    checkFgResultOrThrow(fg, "Allocate DMA Memory");
                }
                CleanupHelper.AddCleanupAction(() => SiSoCsRt.Fg_FreeMemEx(fg, memHandle), "Free DMA Memory");

                #endregion

                // Note:
                // Since the applet is not known at compile time, the following is a generic implementation
                // to create an event mask for all events without payload. If an event handler should 
                // be registered for a known event of a known Applet, the procedure would simply be:
                // 
                //   ulong eventMask = SiSoCsRt.Fg_getEventMask(fg, eventName);
                //   eventMask |= SiSoCsRt.Fg_getEventMask(fg, anotherEventName);
                //   ...
                // 
                //   SiSoCsRt.Fg_registerEventCallback(fg, eventMask, eventHandlerDelegate, new EventData(fg), (uint)FgEventControlFlags.FG_EVENT_DEFAULT_FLAGS, new fg_event_info());
                //   SiSoCsRt.Fg_activateEvents(fg, eventMask, 1);

                #region Event mask for all non-payload events

                // Create a mask of all events without payload.
                // These may all be handled by a single handler.
                uint maskEventsWithoutPayload = 0;

                int eventCount = SiSoCsRt.Fg_getEventCount(fg);

                int shift = 0;
                int eventsFound = 0;
                while (eventsFound < eventCount)
                {
                    uint eventFlag = (uint)(1ul << shift);
                    String eventName = SiSoCsRt.Fg_getEventName(fg, eventFlag);
                    if (eventName != null)
                    {
                        ++eventsFound;
                        Console.WriteLine("Found event: '{0}' (0x{1:X})", (eventName != null) ? eventName : "null", eventFlag);

                        // Only use non-payload events for simplicity
                        if (SiSoCsRt.Fg_getEventPayload(fg, eventFlag) == 0)
                        {
                            Console.WriteLine("No Payload. Add callback.");
                            maskEventsWithoutPayload |= eventFlag;
                        }
                    }
                    ++shift;
                }

                #endregion

                // Register the callback for the events without payload.
                result = SiSoCsRt.Fg_registerEventCallback(fg, maskEventsWithoutPayload, handleEventWithoutPayload, new SiSoUserContext(),
                                                                (uint)FgEventControlFlags.FG_EVENT_DEFAULT_FLAGS, new fg_event_info());
                checkFgResultOrThrow(result, "Register Callback for Non-Payload Events");
                CleanupHelper.AddCleanupAction(
                    () => SiSoCsRt.Fg_registerEventCallback(fg, maskEventsWithoutPayload, null, null, (uint)FgEventControlFlags.FG_EVENT_DEFAULT_FLAGS, null),
                    "Unregister Callback for Non-Payload Events");

                result = SiSoCsRt.Fg_activateEvents(fg, maskEventsWithoutPayload, 1);
                checkFgResultOrThrow(result, "Activate Non-Payload Events");

                // Try to register another handler for the overflow event. This event is not available on all boards.
                // The overflow event carries payload and therefore it requires it's invidual handler.
                String overflowEventName = "FG_OVERFLOW_CAM0";
                uint overflowEventFlag = (uint)SiSoCsRt.Fg_getEventMask(fg, overflowEventName);
                result = SiSoCsRt.Fg_registerEventCallback(fg, overflowEventFlag, handleOverflowEvent, new SiSoUserContext(),
                                                            (uint)FgEventControlFlags.FG_EVENT_DEFAULT_FLAGS, new fg_event_info());
                if (result == 0)
                {
                    CleanupHelper.AddCleanupAction(
                    () => SiSoCsRt.Fg_registerEventCallback(fg, overflowEventFlag, null, null, (uint)FgEventControlFlags.FG_EVENT_DEFAULT_FLAGS, null),
                    "Unregister Callback for Overflow Event");
                    result = SiSoCsRt.Fg_activateEvents(fg, overflowEventFlag, 1);
                    checkFgResultOrThrow(result, "Activate Overflow Event");
                }

                // Create Display
                int displayId = SiSoCsRt.CreateDisplay((uint)bitsPerPixel, width, height);
                checkFgResultOrThrow(displayId, "Create Display");
                CleanupHelper.AddCleanupAction(() => SiSoCsRt.CloseDisplay(displayId), "Close Display");

                SiSoCsRt.SetBufferWidth(displayId, width, height);

                // Start acquisition
                Console.WriteLine("Press any key to start aqcuisition...");
                Console.ReadKey(true);
                result = SiSoCsRt.Fg_AcquireEx(fg, camPort, SiSoCsRt.GRAB_INFINITE, SiSoCsRt.ACQ_STANDARD, memHandle);
                checkFgResultOrThrow(result, "Start Acquisition");
                CleanupHelper.AddCleanupAction(() => SiSoCsRt.Fg_stopAcquireEx(fg, camPort, memHandle, unchecked((int)FgStopAcquireFlags.STOP_SYNC)), "Stop Acquisition");

                // Dipslay images
                for (int imageCount = 0; imageCount < nrOfPicturesToGrab; ++imageCount)
                {
                    int latestImage = (int)SiSoCsRt.Fg_getLastPicNumberBlockingEx(fg, -1, camPort, 1, memHandle);
                    if (latestImage > 0)
                    {
                        Console.WriteLine("[{0}] Image {1}", imageCount, latestImage);
                        SisoImage image = SiSoCsRt.Fg_getImagePtrEx(fg, latestImage, camPort, memHandle);
                        SiSoCsRt.DrawBuffer(displayId, image, latestImage, "");
                    }
                    else
                    {
                        Console.WriteLine("[{0}] ERROR: {1}", imageCount, SiSoCsRt.Fg_getLastErrorDescription(fg));
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
            finally
            {
                CleanupHelper.PerformCleanup();

                Console.WriteLine("Finished. Press any key to quit...");
                Console.ReadKey(true);
            }
        }
    }
}
