using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AcqAPC
{
    class CleanupHelper
    {
        private static LinkedList<Action> cleanupActions = new LinkedList<Action>();

        public static void AddCleanupAction(Action action, String description)
        {
            // cleanup must happen in reverse order, so we add to the front.
            cleanupActions.AddFirst(() => {
                Console.WriteLine(description);
                action();
            });
        }

        public static void performCleanup()
        {
            foreach (var action in cleanupActions)
            {
                action();
            }
        }
    }

    /**
     * Customized Subclass of fg_apc_data for passing
     * custom data to the APC callback.
     */
    class MyApcData : fg_apc_data
    {
        public Fg_Struct fg;
        public uint port;
        public dma_mem mem;
        public uint displayid;

        public MyApcData(Fg_Struct fg, uint port, dma_mem mem, uint displayid)
            : base()
        {
            this.fg = fg;
            this.port = port;
            this.mem = mem;
            this.displayid = displayid;
        }
    }
    internal class Program
    {
        static Fg_Struct fg = null;
        static dma_mem memHandle = null;
        static uint camPort = (uint)(SiSoCsRt.PORT_A);
        static int nrOfPicturesToGrab = 1000;

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
            siso_board_type boardType;
            int i = 0;

            int maxNrOfboards = 10;
            int nrOfBoardsFound = 0;
            int nrOfBoardsPresent = getNrOfBoards();
            int maxBoardIndex = -1;

            for (i = 0; i < Math.Max(maxNrOfboards, nrOfBoardsPresent); i++)
            {
                string boardName;
                bool skipIndex = false;
                boardType = (siso_board_type)SiSoCsRt.Fg_getBoardType(i);
                switch (boardType)
                {
                    case siso_board_type.PN_MICROENABLE5_MARATHON_ACL:
                        boardName = "microEnable 5 marathon ACL";
                        break;
                    case siso_board_type.PN_MICROENABLE5_MARATHON_VCL:
                        boardName = "microEnable 5 marathon VCL";
                        break;
                    case siso_board_type.PN_MICROENABLE5_MARATHON_VCLx:
                        boardName = "microEnable 5 marathon VCLx";
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
                throw new Exception("No Boards found!");
            }

            Console.Write("\n=====================================\n\nPlease choose a board[0-{0}]: ", maxBoardIndex);

            int userInput = -1;
            bool retry = false;
            do
            {
                string inputBuffer = Console.ReadLine();

                try
                {
                    userInput = int.Parse(inputBuffer);
                    retry = (userInput < 0 || userInput > maxBoardIndex);
                }
                catch (Exception ex)
                {
                    retry = true;
                }

                if (retry)
                {
                    Console.Write("Invalid selection, retry[0-{0}]: ", maxBoardIndex);
                }
            } while (retry);

            return userInput;
        }

        private static string selectAppletForBoard(int boardId)
        {
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

            return appletName;
        }


        /**
         * APC Callback function.
         * Must have same signature as SiSoCallback.Fg_ApcFuncDelegate
         */
        static int apcCallback(uint imgNr, fg_apc_data userData)
        {
            MyApcData data = (MyApcData)userData;
            SiSoCsRt.DrawBuffer((int)(data.displayid), SiSoCsRt.Fg_getImagePtrEx(data.fg, (int)(imgNr), data.port, data.mem), (int)(imgNr), "");

            return 0;
        }

        /**
         * Create a message that includes the internal error description for an FG error code.
         */
        static String createFgErrorMessage(int fgResult, String actionDescription)
        {
            return String.Format("{0}: FAILED\n  Error Code: {1}\n  Error Description: {2}",
                    actionDescription, fgResult, SiSoCsRt.Fg_getErrorDescription(fgResult));
        }

        /**
         * Checks if a fg result code is an error and either loggs a success message
         * or throws an exception with a proper error message.
         */
        static void checkFgResultOrThrow(int fgResult, String actionDescription)
        {
            if (fgResult == SiSoCsRt.FG_OK)
            {
                Console.WriteLine("{0}: ok", actionDescription);
            }
            else
            {
                throw new Exception(createFgErrorMessage(fgResult, actionDescription));
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

        static void init()
        {
            int result = SiSoCsRt.Fg_InitLibraries(null);
            checkFgResultOrThrow(result, "Init libraries");
            CleanupHelper.AddCleanupAction(SiSoCsRt.Fg_FreeLibraries, "Free Libraries");

            int boardId = selectBoardDialog();
            int nbBuffers = 4;
            uint width = 512;
            uint height = 512;
            int samplePerPixel = 1;
            uint bytePerSample = 1;
            bool isSlave = false;
            bool useCameraSimulator = true;

            if (boardId < 0)
            {
                throw new Exception("Invalid Board ID.");
            }

            string applet;
            applet = selectAppletForBoard(boardId);

            fg = SiSoCsRt.Fg_InitEx(applet, (uint)(boardId), isSlave ? 1 : 0);
            checkFgResultOrThrow(fg, "Init FG Handle");
            CleanupHelper.AddCleanupAction(() => SiSoCsRt.Fg_FreeGrabber(fg), "Free FG Handle");

            uint imageSize = (uint)(bytePerSample * samplePerPixel * width * height);

            // Create Display
            int displayId = SiSoCsRt.CreateDisplay((uint)(8 * bytePerSample * samplePerPixel), width, height);
            SiSoCsRt.SetBufferWidth(displayId, width, height);
            CleanupHelper.AddCleanupAction(() => SiSoCsRt.CloseDisplay(displayId), "Close Display");

            // Allocate DMA
            uint totalBufferSize = (uint)(width * height * samplePerPixel * bytePerSample * nbBuffers);
            memHandle = SiSoCsRt.Fg_AllocMemEx(fg, totalBufferSize, nbBuffers);
            if (memHandle == null)
            {
                // Will throw internally
                checkFgResultOrThrow(fg, "Allocate DMA Memory");
            }
            CleanupHelper.AddCleanupAction(() => SiSoCsRt.Fg_FreeMemEx(fg, memHandle), "Free DMA Memory");

            // Set Applet Parameters
            /* Image width of the acquisition window. */
            result = SiSoCsRt.Fg_setParameterWithUInt(fg, SiSoCsRt.FG_WIDTH, width, camPort);
            checkFgResultOrThrow(result, "Set parameter FG_WIDTH");

            /* Image height of the acquisition window. */
            result = SiSoCsRt.Fg_setParameterWithUInt(fg, SiSoCsRt.FG_HEIGHT, height, camPort);
            checkFgResultOrThrow(result, "Set parameter FG_HEIGHT");

            int bitAlignment = SiSoCsRt.FG_LEFT_ALIGNED;
            result = SiSoCsRt.Fg_setParameterWithInt(fg, SiSoCsRt.FG_BITALIGNMENT, bitAlignment, camPort);
            if (result != SiSoCsRt.FG_OK)
            {
                // Continue despite the failure, since the bit alignment is not vital for this example and
                // not all boards support this property.
                Console.WriteLine(createFgErrorMessage(result, "Set parameter FG_BITALIGNMENT"));
            }

            if (useCameraSimulator)
            {
                result = SiSoCsRt.Fg_setParameterWithInt(fg, SiSoCsRt.FG_GEN_ENABLE, (int)(FgImageSourceTypes.FG_GENERATOR), camPort);
                checkFgResultOrThrow(result, "Enable Camera Simulator");
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


            /* Register acquisition call back */

            // Define FgApcControl instance to handle the callback
            FgApcControl apcCtrl = new FgApcControl(5, (uint)(Fg_Apc_Flag.FG_APC_DEFAULTS));
            fg_apc_data gApcData = new MyApcData(fg, (uint)(camPort), memHandle, (uint)(displayId));
            apcCtrl.setApcCallbackFunction(apcCallback, gApcData);

            // Register the FgApcControl instance to the Fg_Struct instance
            result = SiSoCsRt.Fg_registerApcHandler(fg, camPort, apcCtrl, FgApcControlFlags.FG_APC_CONTROL_BASIC);
            checkFgResultOrThrow(result, "Register APC Handler");
            CleanupHelper.AddCleanupAction(() => SiSoCsRt.Fg_registerApcHandler(fg, camPort, null, FgApcControlFlags.FG_APC_CONTROL_BASIC), "Unregister APC Handler");
        }

        static void startAcquisition()
        {
            // Start acquisition
            int result = SiSoCsRt.Fg_AcquireEx(fg, camPort, nrOfPicturesToGrab, SiSoCsRt.ACQ_STANDARD, memHandle);
            checkFgResultOrThrow(result, "Start Acquisition");
            CleanupHelper.AddCleanupAction(() => SiSoCsRt.Fg_stopAcquireEx(fg, camPort, memHandle, unchecked((int)FgStopAcquireFlags.STOP_SYNC)), "Stop Acquisition");
        }

        static void Main(string[] args)
        {
            try
            {
                init();
                startAcquisition();

                Thread.Sleep(5000);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                while (ex.InnerException != null)
                {
                    ex = ex.InnerException;
                    Console.WriteLine(ex.Message);
                }
            }
            finally
            {
                CleanupHelper.performCleanup();
            }

            Console.WriteLine("Press any key to quit...");
            Console.ReadKey(true);

            Console.WriteLine("Exited.");
        }
    }
}
