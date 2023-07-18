using System;
using System.Collections.Generic;
using System.Text;
/*
 * Simple Example for the usage of Basler Framegrabber C# SDK
 */
namespace MyFirstSDK
{
    class CleanupHelper
    {
        private static List<Action> cleanupActions = new List<Action>();

        public static void AddCleanupAction(Action action, String logMessage)
        {
            cleanupActions.Add(() => { action(); Console.WriteLine(logMessage); });
        }

        public static void PerformCleanup()
        {
            for (int i = cleanupActions.Count - 1; i >= 0; --i)
            {
                cleanupActions[i]();
            }
            cleanupActions.Clear();
        }
    }
    internal class Program
    {
        static void checkFgResultOrThrow(int result, String actionDescription)
        {
            if (result == SiSoCsRt.FG_OK)
            {
                Console.WriteLine("{0}: OK", actionDescription);
            }
            else if (result > 0)
            {
                Console.WriteLine("{0}: OK, Result = {1}", actionDescription, result);
            }
            else
            {
                throw new Exception(String.Format("{0}: FAILED\n  errcode: {1}\n  description: {2}", actionDescription, result, SiSoCsRt.Fg_getErrorDescription(result)));
            }
        }

        static void checkFgOrThrow(Fg_Struct fg, String actionDescription)
        {
            int errorCode = SiSoCsRt.Fg_getLastErrorNumber(fg);
            checkFgResultOrThrow(errorCode, actionDescription);
        }

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
            int minBoardIndex = -1;

            for (i = 0; i < maxNrOfboards; i++)
            {
                string boardName;
                bool skipIndex = false;
                boardType = SiSoCsRt.Fg_getBoardType(i);
                switch ((siso_board_type)(boardType))
                {
                    case siso_board_type.PN_MICROENABLE5_LIGHTBRIDGE_VCL:
                        boardName = "LightBridge VCL";
                        break;
                    case siso_board_type.PN_MICROENABLE5_LIGHTBRIDGE_ACL:
                        boardName = "LightBridge ACL";
                        break;
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
                    if (minBoardIndex == -1) minBoardIndex = i;
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

            Console.Write("\n=====================================\n\nPlease choose a board[{0}-{1}]: ", minBoardIndex, maxBoardIndex);
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
        static void Main(string[] args)
        {
            try
            {
                SiSoCsRt.Fg_InitLibraries(null);
                checkFgOrThrow(null, "Init Libraries");
                CleanupHelper.AddCleanupAction(() => SiSoCsRt.Fg_FreeLibraries(), "Free Libraries");

                Fg_Struct fg = null;
                int boardId = selectBoardDialog();
                uint camPort = (uint)(SiSoCsRt.PORT_A);
                int nrOfPicturesToGrab = 50;
                int nbBuffers = 4;
                uint width = 512;
                uint height = 512;
                int samplePerPixel = 1;
                uint bytePerSample = 1;
                bool isSlave = false;
                bool useCameraSimulator = true;

                if (boardId < 0)
                {
                    return;
                }

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

                    fg = SiSoCsRt.Fg_InitEx(appletName, (uint)(boardId), isSlave ? 1 : 0);
                    checkFgOrThrow(fg, "Init Framegrabber");
                    CleanupHelper.AddCleanupAction(() => SiSoCsRt.Fg_FreeGrabber(fg), "Free Framegrabber");
                }
                uint imageSize = (uint)(bytePerSample * samplePerPixel * width * height);

                // Create Display
                int displayId = SiSoCsRt.CreateDisplay((uint)(8 * bytePerSample * samplePerPixel), width, height);
                checkFgResultOrThrow(displayId, "Create Display");
                CleanupHelper.AddCleanupAction(() => SiSoCsRt.CloseDisplay(displayId), "Close Display");

                SiSoCsRt.SetBufferWidth(displayId, width, height);

                // Allocate DMA
                uint totalBufferSize = (uint)(width * height * samplePerPixel * bytePerSample * nbBuffers);
                dma_mem memHandle = SiSoCsRt.Fg_AllocMemEx(fg, totalBufferSize, nbBuffers);
                checkFgOrThrow(fg, "Allocate DMA Memory");
                CleanupHelper.AddCleanupAction(() => SiSoCsRt.Fg_FreeMemEx(fg, memHandle), "Free DMA Memory");

                // Set Applet Parameters
                /* Image width of the acquisition window. */
                int result = SiSoCsRt.Fg_setParameterWithUInt(fg, SiSoCsRt.FG_WIDTH, width, camPort);
                checkFgResultOrThrow(result, String.Format("Set Parameter 'FG_WITH' to {0}", width));

                /* Image height of the acquisition window. */
                result = SiSoCsRt.Fg_setParameterWithUInt(fg, SiSoCsRt.FG_HEIGHT, height, camPort);
                checkFgResultOrThrow(result, String.Format("Set Parameter 'FG_HEIGHT' to {0}", height));

                result = SiSoCsRt.Fg_setParameterWithInt(fg, SiSoCsRt.FG_BITALIGNMENT, SiSoCsRt.FG_LEFT_ALIGNED, camPort);
                if (result < 0)
                {
                    Console.WriteLine("Set Parameter 'FG_BITALIGNMENT' to FG_LEFT_ALIGNED failed: {0}\n", SiSoCsRt.Fg_getLastErrorDescription(fg));
                }

                if (useCameraSimulator)
                {
                    result = SiSoCsRt.Fg_setParameterWithInt(fg, SiSoCsRt.FG_GEN_ENABLE, (int)(FgImageSourceTypes.FG_GENERATOR), camPort);
                    checkFgResultOrThrow(result, "Enable Generator");
                }

                /* Reading back parameters */
                #region Read back parameters
                {
                    int oWidth = 0;
                    int oHeight = 0;
                    int oBitAlignment = 0;
                    int oGenEnabled = 0;
                    int oGenRoll = 0;
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
                #endregion

                // Start acquisition
                result = SiSoCsRt.Fg_AcquireEx(fg, camPort, nrOfPicturesToGrab, SiSoCsRt.ACQ_STANDARD, memHandle);
                checkFgResultOrThrow(result, "Start Acquisition");
                CleanupHelper.AddCleanupAction(() => SiSoCsRt.Fg_stopAcquireEx(fg, camPort, memHandle, unchecked((int)FgStopAcquireFlags.STOP_SYNC)), "Stop Acquisition");

                long lastPicNr = 0;
                long currentPicNumber = 0;
                int timeout = 100;
                while ((currentPicNumber = SiSoCsRt.Fg_getLastPicNumberBlockingEx(fg, lastPicNr + 1, camPort, timeout, memHandle)) < nrOfPicturesToGrab)
                {
                    if (currentPicNumber < 0)
                    {
                        throw new Exception("Failed to get last pic number");
                    }
                    lastPicNr = currentPicNumber;
                    SiSoCsRt.DrawBuffer(displayId, SiSoCsRt.Fg_getImagePtrEx(fg, lastPicNr, camPort, memHandle), (int)lastPicNr, "");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception: {0}", e.Message);
            }
            finally
            {
                // Clean up
                CleanupHelper.PerformCleanup();

                Console.WriteLine("Done. Press any key to quit...");
                Console.ReadKey(true);
            }
        }
    }
}
