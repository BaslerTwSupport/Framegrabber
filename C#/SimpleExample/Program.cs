using System;
using System.Collections.Generic;
using System.Text;

namespace SimpleExample
{
    internal class Program
    {

        private static Fg_Struct _fg;
        private static dma_mem _memHandle;
        private static int _dispId0;
        private static string GetLastError()
        {
            return SiSoCsRt.Fg_getLastErrorDescription(_fg);
        }
        private static void FreeMemGrabberCloseDis(string errorMsg)
        {
            Console.WriteLine($"{errorMsg}: {GetLastError()}");
            SiSoCsRt.Fg_FreeMemEx(_fg, _memHandle);
            SiSoCsRt.Fg_FreeGrabber(_fg);
            SiSoCsRt.CloseDisplay(_dispId0);
        }
        static void Main(string[] args)
        {
            _fg = null;
            uint camPort = SiSoCsRt.PORT_A;
            long nrOfPicturesToGrab = 1000;
            uint nbBuffers = 4;
            uint width = 512;
            uint height = 512;
            uint samplePerPixel = 1;
            // Number of channels
            uint bytePerSample = 1;

            InitLibraries();
            var boardsName = GetBoardsName();
            if (boardsName.Length < 0)
            {
                Console.WriteLine($"Board not exits.");
                return;
            }
            var boardIndex = -1;

            do
            {
                try
                {
                    if (boardsName.Length == 1)
                    {
                        boardIndex = 0;
                        break;
                    }
                    Console.WriteLine($"Please choose a board by entering a number between 0 and {boardsName.Length - 1}:");
                    boardIndex = Convert.ToInt32(Console.ReadLine());
                }
                catch
                {
                    Console.WriteLine($"Invalid selection, retry [0-{boardsName.Length - 1}]: ");
                }
            }
            while (boardIndex < 0 || boardIndex >= boardsName.Length);

            var appletsName = GetApplets(boardIndex);
            Console.WriteLine($"Please choose an applet by entering a number between 0 and {appletsName.Length - 1}:");
            var appletIndex = -1;
            do
            {
                try
                {
                    appletIndex = Convert.ToInt32(Console.ReadLine());
                }
                catch
                {
                    Console.WriteLine($"Invalid selection, retry [0-{appletsName.Length - 1}]: ");
                }
            }
            while (appletIndex < 0 || appletIndex >= appletsName.Length);
            var applet = appletsName[appletIndex];
            _fg = SiSoCsRt.Fg_Init(applet, Convert.ToUInt32(boardIndex));
            if(_fg == null)
            {
                Console.WriteLine($"error in Fg_Init: {SiSoCsRt.Fg_getLastErrorDescription(null)}");
                return;
            }
            ulong bufferSize = width * height * bytePerSample;
            ulong totalBufferSize = bufferSize * samplePerPixel * nbBuffers;

            _dispId0 = SiSoCsRt.CreateDisplay(8 * bytePerSample * samplePerPixel, width, height);
            SiSoCsRt.SetBufferWidth(_dispId0, width, height);

            _memHandle = SiSoCsRt.Fg_AllocMemEx(_fg, totalBufferSize, nbBuffers);
            if(_memHandle == null)
            {
                Console.WriteLine($"error in Fg_AllocMemEx: {GetLastError()}");
                SiSoCsRt.Fg_FreeGrabber(_fg);
                SiSoCsRt.CloseDisplay(_dispId0);
                return;
            }

            if (SiSoCsRt.Fg_setParameterWithUInt(_fg, SiSoCsRt.FG_CAMERASIMULATOR_ENABLE, (uint)FgImageSourceTypes.FG_CAMERASIMULATOR , camPort) != SiSoCsRt.FG_OK)
            {
                FreeMemGrabberCloseDis("Fg_setParameterWithUInt(FG_CAMERASIMULATOR_ENABLE) failed");
                return;
            }

            if (SiSoCsRt.Fg_setParameterWithUInt(_fg, SiSoCsRt.FG_CAMERASIMULATOR_WIDTH, width, camPort) != SiSoCsRt.FG_OK)
            {
                FreeMemGrabberCloseDis("Fg_setParameterWithUInt(FG_CAMERASIMULATOR_WIDTH) failed");
                return;
            }

            if (SiSoCsRt.Fg_setParameterWithUInt(_fg, SiSoCsRt.FG_CAMERASIMULATOR_HEIGHT, height, camPort) != SiSoCsRt.FG_OK)
            {
                FreeMemGrabberCloseDis("Fg_setParameterWithUInt(FG_CAMERASIMULATOR_HEIGHT) failed");
                return;
            }

            if (SiSoCsRt.Fg_setParameterWithUInt(_fg, SiSoCsRt.FG_WIDTH, width, camPort) != SiSoCsRt.FG_OK)
            {
                FreeMemGrabberCloseDis("Fg_setParameterWithUInt(FG_WIDTH) failed");
                return;
            }
            if (SiSoCsRt.Fg_setParameterWithUInt(_fg, SiSoCsRt.FG_HEIGHT, height, camPort) != SiSoCsRt.FG_OK)
            {
                FreeMemGrabberCloseDis("Fg_setParameterWithUInt(FG_HEIGHT) failed");
                return;
            }
            if (SiSoCsRt.Fg_setParameterWithInt(_fg, SiSoCsRt.FG_BITALIGNMENT, SiSoCsRt.FG_LEFT_ALIGNED, camPort) != SiSoCsRt.FG_OK)
            {
                FreeMemGrabberCloseDis("Fg_setParameterWithInt(FG_BITALIGNMENT) failed");
                return;
            }
            if (SiSoCsRt.Fg_AcquireEx(_fg, camPort, nrOfPicturesToGrab, SiSoCsRt.ACQ_STANDARD, _memHandle) != SiSoCsRt.FG_OK)
            {
                FreeMemGrabberCloseDis("Fg_AcquireEx() failed");
                return;
            }
            long last_pic_nr = 0;
            long cur_pic_nr;
            int timeout = 4;
            while((cur_pic_nr = SiSoCsRt.Fg_getLastPicNumberBlockingEx(_fg, last_pic_nr + 1, camPort, timeout, _memHandle)) < nrOfPicturesToGrab)
            {
                if(cur_pic_nr < 0)
                {
                    Console.WriteLine($"Fg_getLastPicNumberBlockingEx(%li) failed: {SiSoCsRt.Fg_getLastErrorDescription(_fg)}");
                    SiSoCsRt.Fg_stopAcquire(_fg, camPort);
                    SiSoCsRt.Fg_FreeMemEx(_fg, _memHandle);
                    SiSoCsRt.Fg_FreeGrabber(_fg);
                    SiSoCsRt.CloseDisplay(_dispId0);
                    return;
                }
                last_pic_nr = cur_pic_nr;
                var img = SiSoCsRt.Fg_getImagePtrEx(_fg, last_pic_nr, camPort, _memHandle);
                SiSoCsRt.DrawBuffer(_dispId0, img, Convert.ToInt32(last_pic_nr), "");
                // Pylon image window 
                //ImageWindow.DisplayImage(0, img.toByteArray(Convert.ToUInt32(bufferSize)), PixelType.Mono8, (int)width, (int)height, 0, ImageOrientation.TopDown);
            }
        }
        private static int InitLibraries()
        {
            var ret = SiSoCsRt.Fg_InitLibraries(null);
            if (ret != SiSoCsRt.FG_OK)
            {
                Console.WriteLine("Failed to initialize libraries!");
                return SiSoCsRt.FG_ENV_NOT_SET;
            }
            return ret;
        }
        private static int GetNumOfBoards()
        {
            var buffer = new byte[256];
            uint buflen = Convert.ToUInt32(buffer.Length);

            if (SiSoCsRt.Fg_getSystemInformation(null, Fg_Info_Selector.INFO_NR_OF_BOARDS, FgProperty.PROP_ID_VALUE, 0, buffer, ref buflen) == SiSoCsRt.FG_OK)
            {
                return Convert.ToInt32(Encoding.Default.GetString(buffer));
            }
            return 0;
        }
        private static string[] GetBoardsName()
        {
            var boardType = 0;
            var maxNrOfboards = 32;  // use a constant no. of boards to query, when evaluations versions minor to RT 5.2
            var nrOfBoardsFound = 0;
            var nrOfBoardsPresent = GetNumOfBoards();
            var maxBoardIndex = -1;
            var boardsName = new List<string>();
            for (int i = 0; i < maxNrOfboards; i++)
            {
                boardType = SiSoCsRt.Fg_getBoardType(i);
                if (boardType == 0)
                    continue;
                if (boardType > 0)
                {
                    var boardName = SiSoCsRt.Fg_getBoardNameByType(boardType, 0);
                    Console.WriteLine($"{i}: {boardName} {boardType}");
                    boardsName.Add(boardName);
                    nrOfBoardsFound++;
                    maxBoardIndex = i;
                }
                if (nrOfBoardsFound >= nrOfBoardsPresent)
                    break;  // all boards are scanned
            }
            if (nrOfBoardsFound <= 0)
            {
                Console.WriteLine("No supported Boards found!\n");
                return null;
            }

            return boardsName.ToArray();
        }
        private static string[] GetApplets(int boardNr)
        {
            var ai = SiSoCsRt.Fg_getAppletIterator(boardNr, FgAppletIteratorSource.FG_AIS_FILESYSTEM, Convert.ToInt32(FgAppletIteratorFlags.FG_AF_IS_LOADABLE), out int errorCode);
            if (errorCode <= 0)
            {
                throw new Exception("No Applets found!");
            }
            var i = 0;
            var boardsName = new List<string>();
            while (errorCode > 0)
            {
                var aii = SiSoCsRt.Fg_getAppletIteratorItem(ai, i);
                if (aii == null)
                    break;
                var appletName = SiSoCsRt.Fg_getAppletStringProperty(aii, FgAppletStringProperty.FG_AP_STRING_APPLET_NAME);
                Console.WriteLine(i + ": " + appletName + "\n");
                boardsName.Add(appletName);
                i += 1;
                errorCode -= 1;
            }
            if (boardsName.Count == 0)
            {
                Console.WriteLine("No applets found for this board!\n");
                return null;
            }
            return boardsName.ToArray();
        }
    }
}
