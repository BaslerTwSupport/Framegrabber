/// <summary>
/// Add ..\Basler\FramegrabberSDK\lib\SiSoCsInterface.dll to reference.
/// </summary>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FramegrabberSDK
{
    
    public class SiSo
    {
        public static int InitLibraries()
        {
            var ret = SiSoCsRt.Fg_InitLibraries(null);
            if (ret != SiSoCsRt.FG_OK)
            {
                Console.WriteLine("Failed to initialize libraries!");
                return SiSoCsRt.FG_ENV_NOT_SET;
            }
            return ret;
        }
        /// <summary>
        /// starting with RT 5.2.
        /// </summary>
        /// <returns></returns>
        public static int GetNumOfBoards()
        {
            var buffer = new byte[256];
            uint buflen = Convert.ToUInt32(buffer.Length);
            
            if (SiSoCsRt.Fg_getSystemInformation(null, Fg_Info_Selector.INFO_NR_OF_BOARDS, FgProperty.PROP_ID_VALUE, 0, buffer, ref buflen) == SiSoCsRt.FG_OK)
            {
                return Convert.ToInt32(Encoding.Default.GetString(buffer));
            }
            return 0;
        }
        public static string[] GetBoardsName()
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
                if(boardType == 0)
                    continue;
                if (boardType > 0)
                {
                    var boardName = SiSoCsRt.Fg_getBoardNameByType(boardType, 0);
                    Console.WriteLine($"{i}: {boardName} {boardType}");
                    boardsName.Add(boardName);
                    nrOfBoardsFound++;
                    maxBoardIndex = i;
                }
                if(nrOfBoardsFound >= nrOfBoardsPresent)
                    break;  // all boards are scanned
            }
            if (nrOfBoardsFound <= 0)
            {
                Console.WriteLine("No supported Boards found!\n");
                return null;
            }

            return boardsName.ToArray();
        }
        public static string[] GetApplets(int boardNr)
        {
            var ai = SiSoCsRt.Fg_getAppletIterator(boardNr, FgAppletIteratorSource.FG_AIS_FILESYSTEM, Convert.ToInt32(FgAppletIteratorFlags.FG_AF_IS_LOADABLE), out int errorCode);
            if(errorCode <= 0)
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
        public enum ResetStatus
        {
            Off = 0,
            On = 1
        }
        public enum GpioType
        {
            PushPull = 0,
            OpenDrain = 1
        }
        private Fg_Struct _fg = null;
        private string _fileName;
        private int _boardIndex;
        private int _width;
        private int _height;
        private int _channels;
        private int _bufferSize;
        private int _bufferSize2;
        public long _fameId;
        private dma_mem _pmem;
        private dma_mem _pmem2;
        private SgcBoardHandle _bh;
        private SgcCameraHandle _chPortA;
        private string[] _paramsName;
        private int[] _paramsID;
        public int Width => _width;
        public int Height => _height;
        public Dictionary<string, int> ParamsNameAndId { get; }
        public string[] ParamsName => _paramsName;
        public int[] ParamsID => _paramsID;
        public long FrameId => _fameId;
        /// <summary>
        /// Hap file name  file<br/>
        /// Framegrabber card index file<br/>
        /// Camera image width<br/>
        /// Camera image height<br/>
        /// Pixel format chanels: Mono or bayer is 1. RGB is 3. RGBA is 4<br/>
        /// </summary>
        /// <param name="fileName">gggg</param>
        /// <param name="boardIndex"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <param name="channels"></param>
        /// <exception cref="Exception"></exception>
        /// <exception cref="NullReferenceException"></exception>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public SiSo(string fileName, int boardIndex, int width, int height, int channels)
        {
            if(InitLibraries() != SiSoCsRt.FG_OK)
            {
                throw new Exception("Failed to initialize libraries!");
            }
            if (string.IsNullOrEmpty(fileName) || string.IsNullOrWhiteSpace(fileName))
            {
                throw new NullReferenceException("Hap file can't be null or white space!");
            }
            if (width <= 0 || height <= 0)
            {
                throw new ArgumentOutOfRangeException("Image width or height must greater than 0!");
            }
            _fileName = fileName;
            _width = width;
            _height = height;
            _boardIndex = boardIndex;
            _channels = channels;
            _pmem = null;
            _bh = null;
            ParamsNameAndId = new Dictionary<string, int>();
        }
        /// <summary>
        /// API initialize
        /// </summary>
        /// <exception cref="NullReferenceException"></exception>
        /// <exception cref="Exception"></exception>
        public void Initialize()
        {
            SiSoCsRt.Fg_FreeLibraries();
            _fg = SiSoCsRt.Fg_InitEx(_fileName, (uint)_boardIndex, 0);
            if (_fg == null)
            {
                throw new NullReferenceException(SiSoCsRt.Fg_getLastErrorDescription(_fg));
            }

            var num = SiSoCsRt.Fg_getNrOfParameter(_fg);
            for (int i = 0; i < num; i++)
            {
                //ParamsNameAndId.Add(SiSoCsRt.Fg_getParameterName(_fg, i), SiSoCsRt.Fg_getParameterId(_fg, i));
            }
            _paramsName = ParamsNameAndId.Keys.ToArray();
            _paramsID = ParamsNameAndId.Values.ToArray();
            _bufferSize = _width * _height * _channels;
            var mem_bufs = 4;
            _pmem = SiSoCsRt.Fg_AllocMemEx(_fg, (ulong)(_bufferSize * mem_bufs), mem_bufs);
            if (_pmem == null)
            {
                SiSoCsRt.Fg_FreeGrabber(_fg);
                _fg = null;
                throw new Exception($"Error in Fg_AllocMemEx(): {SiSoCsRt.Fg_getLastErrorDescription(_fg)}");
            }
            _bufferSize2 = _width * _height;
            _pmem2 = SiSoCsRt.Fg_AllocMemEx(_fg, (ulong)(_bufferSize2 * mem_bufs), mem_bufs);
            if (_pmem2 == null)
            {
                SiSoCsRt.Fg_FreeGrabber(_fg);
                _fg = null;
                throw new Exception($"Error in Fg_AllocMemEx(): {SiSoCsRt.Fg_getLastErrorDescription(_fg)}");
            }
        }
        /// <summary>
        /// Board and camera initialize
        /// </summary>
        /// <exception cref="Exception"></exception>
        /// <exception cref="ArgumentException"></exception>
        public void BoardInitialize()
        {
            _bh = SiSoCsRt.Sgc_initBoard(_fg, 0, out var ret);
            if (ret < 0)
            {
                FreeMemExGrabber();
                throw new Exception($"Error in Sgc_initBoard(): {SiSoCsRt.Sgc_getErrorDescription(ret)} ({ret})");
            }

            ret = SiSoCsRt.Sgc_scanPorts(_bh, 0x0F, 5000, SiSoCsRt.CXP_SPEED_3125);
            if (ret < 0)
            {
                FreeBoardMemExGrabber();
                throw new Exception($"Error in Sgc_scanPorts(): {SiSoCsRt.Sgc_getErrorDescription(ret)} ({ret})");
            }

            _chPortA = SiSoCsRt.Sgc_getCamera(_bh, SiSoCsRt.PORT_A, out ret);
            if (ret < 0)
            {
                FreeBoardMemExGrabber();
                throw new Exception($"Error in Sgc_getCamera(): {SiSoCsRt.Sgc_getErrorDescription(ret)} ({ret})");
            }

            ret = SiSoCsRt.Sgc_connectCamera(_chPortA);
            if (ret < 0)
            {
                FreeBoardMemExGrabber();
                throw new Exception($"Error in Sgc_connectCamera(): {SiSoCsRt.Sgc_getErrorDescription(ret)} ({ret})");
            }

            ret = SiSoCsRt.Sgc_setIntegerValue(_chPortA, "Width", _width);
            if (ret < 0)
            {
                throw new ArgumentException($"Port A: Error in Sgc_setIntegerValue(): \"Width\" {SiSoCsRt.Sgc_getErrorDescription(ret)} ({ret})");
            }

            ret = SiSoCsRt.Sgc_setIntegerValue(_chPortA, "Height", _height);
            if (ret < 0)
            {
                throw new ArgumentException($"Port A: Error in Sgc_setIntegerValue(): \"Height\" {SiSoCsRt.Sgc_getErrorDescription(ret)} ({ret})");
            }

            ret = SiSoCsRt.Fg_AcquireEx(_fg, 0, SiSoCsRt.GRAB_INFINITE, SiSoCsRt.ACQ_STANDARD, _pmem);
            if (ret < 0)
            {
                SiSoCsRt.Sgc_disconnectCamera(_chPortA);
                FreeBoardMemExGrabber();
                throw new Exception($"Error in Fg_AcquireEx() for channel 0: {SiSoCsRt.Fg_getErrorDescription(ret)}");
            }
            ret = SiSoCsRt.Fg_AcquireEx(_fg, 1, SiSoCsRt.GRAB_INFINITE, SiSoCsRt.ACQ_STANDARD, _pmem2);
            if (ret < 0)
            {
                SiSoCsRt.Sgc_disconnectCamera(_chPortA);
                FreeBoardMemExGrabber();
                throw new Exception($"Error in Fg_AcquireEx() for channel 0: {SiSoCsRt.Fg_getErrorDescription(ret)}");
            }
        }
        /// <summary>
        /// Camera start acquisition
        /// </summary>
        /// <exception cref="Exception"></exception>
        public void StartAcquisition()
        {
            var ret = SiSoCsRt.Sgc_startAcquisition(_chPortA, Convert.ToUInt16(true));
            if (ret < 0)
            {
                SiSoCsRt.Fg_stopAcquireEx(_fg, 0, _pmem, 0);
                SiSoCsRt.Sgc_disconnectCamera(_chPortA);
                SiSoCsRt.Sgc_freeBoard(_bh);
                _bh = null;
                //SiSoCsRt.CloseDisplay(displayId);
                SiSoCsRt.Fg_FreeMemEx(_fg, _pmem);
                _pmem = null;
                SiSoCsRt.Fg_FreeGrabber(_fg);
                _fg = null;
                Console.ReadKey();
                throw new Exception($"Error in Fg_AcquireEx() for channel 0: {SiSoCsRt.Fg_getErrorDescription(ret)}");
            }
        }
        /// <summary>
        /// Camera stop acquisition
        /// </summary>
        /// <exception cref="Exception"></exception>
        public void StopAcquisition()
        {
            var ret = SiSoCsRt.Sgc_stopAcquisition(_chPortA, Convert.ToUInt16(true));
            if (ret < 0)
            {
                Console.WriteLine($"Port A: Error in Sgc_stopAcquisition(): {SiSoCsRt.Fg_getErrorDescription(ret)} ({ret})");
            }
        }
        public byte[] GetImage(uint i = 0)
        {
            var pmem = i == 0 ? _pmem : _pmem2;
            var bufferSize = i == 0 ? _bufferSize : _bufferSize2;
            _fameId = SiSoCsRt.Fg_getLastPicNumberBlockingEx(_fg, _fameId + 1, i, 5, pmem);
            if (_fameId < 0)
            {
                throw new Exception($"Error in Fg_getLastPicNumberBlockingEx(): {SiSoCsRt.Fg_getLastErrorDescription(_fg)} ({_fameId})");
            }
            var img = SiSoCsRt.Fg_getImagePtrEx(_fg, _fameId, i, pmem);
            if (img == null)
            {
                StopAcquireEx();
                DisconnectCamera();
                FreeBoardMemExGrabber();
                throw new Exception($"Error in Fg_getLastPicNumberBlockingEx(): {SiSoCsRt.Fg_getLastErrorDescription(_fg)} ({_fameId})");
            }
            return img.toByteArray(Convert.ToUInt32(bufferSize));
        }
        public void ReleaseAll()
        {
            StopAcquisition();
            StopAcquireEx();
            DisconnectCamera();
            FreeBoardMemExGrabber();
        }
        #region Parameter
        public void SetParameter(string name, int value)
        {
            var Id = SiSoCsRt.Fg_getParameterIdByName(_fg, name);
            if (Id <= 0)
            {
                Console.WriteLine($"Error in Fg_getParameterIdByName({name}): {SiSoCsRt.Fg_getLastErrorDescription(_fg)} ({Id})");
                Console.ReadKey();
            }
            var ret = SiSoCsRt.Fg_setParameterWithInt(_fg, Id, value, 0);
            if (ret < 0)
            {
                Console.WriteLine($"Error in Fg_setParameterWithUInt(): {SiSoCsRt.Fg_getLastErrorDescription(_fg)} ({ret})");
            }
        }
        public void SetParameter(string name, uint value)
        {
            var Id = SiSoCsRt.Fg_getParameterIdByName(_fg, name);
            if (Id <= 0)
            {
                Console.WriteLine($"Error in Fg_getParameterIdByName({name}): {SiSoCsRt.Fg_getLastErrorDescription(_fg)} ({Id})");
                Console.ReadKey();
            }
            var ret = SiSoCsRt.Fg_setParameterWithUInt(_fg, Id, value, 0);
            if (ret < 0)
            {
                Console.WriteLine($"Error in Fg_setParameterWithUInt(): {SiSoCsRt.Fg_getLastErrorDescription(_fg)} ({ret})");
            }
        }
        public void SetParameter(string name, long value)
        {
            var Id = SiSoCsRt.Fg_getParameterIdByName(_fg, name);
            if (Id <= 0)
            {
                Console.WriteLine($"Error in Fg_getParameterIdByName({name}): {SiSoCsRt.Fg_getLastErrorDescription(_fg)} ({Id})");
                Console.ReadKey();
            }
            var ret = SiSoCsRt.Fg_setParameterWithLong(_fg, Id, value, 0);
            if (ret < 0)
            {
                Console.WriteLine($"Error in Fg_setParameterWithUInt(): {SiSoCsRt.Fg_getLastErrorDescription(_fg)} ({ret})");
            }
        }
        public void SetParameter(string name, ulong value)
        {
            var Id = SiSoCsRt.Fg_getParameterIdByName(_fg, name);
            if (Id <= 0)
            {
                Console.WriteLine($"Error in Fg_getParameterIdByName({name}): {SiSoCsRt.Fg_getLastErrorDescription(_fg)} ({Id})");
                Console.ReadKey();
            }
            var ret = SiSoCsRt.Fg_setParameterWithULong(_fg, Id, value, 0);
            if (ret < 0)
            {
                Console.WriteLine($"Error in Fg_setParameterWithUInt(): {SiSoCsRt.Fg_getLastErrorDescription(_fg)} ({ret})");
            }
        }
        public void SetParameter(string name, double value)
        {
            var Id = SiSoCsRt.Fg_getParameterIdByName(_fg, name);
            if (Id <= 0)
            {
                Console.WriteLine($"Error in Fg_getParameterIdByName({name}): {SiSoCsRt.Fg_getLastErrorDescription(_fg)} ({Id})");
                Console.ReadKey();
            }
            var ret = SiSoCsRt.Fg_setParameterWithDouble(_fg, Id, value, 0);
            if (ret < 0)
            {
                Console.WriteLine($"Error in Fg_setParameterWithUInt(): {SiSoCsRt.Fg_getLastErrorDescription(_fg)} ({ret})");
            }
        }
        public void GetParameterWithUInt(string name)
        {
            var Id = SiSoCsRt.Fg_getParameterIdByName(_fg, name);
            if (Id <= 0)
            {
                Console.WriteLine($"Error in Fg_getParameterIdByName({name}): {SiSoCsRt.Fg_getLastErrorDescription(_fg)} ({Id})");
                Console.ReadKey();
            }
            var ret = SiSoCsRt.Fg_getParameterWithUInt(_fg, Id, out var value, 0);
            if (ret < 0)
            {
                Console.WriteLine($"Error in Fg_getParameterWithUInt(): {SiSoCsRt.Fg_getLastErrorDescription(_fg)} ({ret}) ({Id})");
            }
            else
            {
                Console.WriteLine($"{name} = {value}");
            }
        }
        public void GetParameterWithString(string name)
        {
            var Id = SiSoCsRt.Fg_getParameterIdByName(_fg, name);
            if (Id <= 0)
            {
                Console.WriteLine($"Error in Fg_getParameterIdByName({name}): {SiSoCsRt.Fg_getLastErrorDescription(_fg)} ({Id})");
                Console.ReadKey();
            }

            var ret = SiSoCsRt.Fg_getParameterWithString(_fg, Id, out var value, 0);
            if (ret < 0)
            {
                Console.WriteLine($"Error in Fg_getParameterWithUInt(): {SiSoCsRt.Fg_getLastErrorDescription(_fg)} ({ret}) ({Id})");
            }
            else
            {
                Console.WriteLine($"{name} = {value}");
            }
        }
        public void GetParameterWithULong(string name)
        {
            var Id = SiSoCsRt.Fg_getParameterIdByName(_fg, name);
            if (Id <= 0)
            {
                Console.WriteLine($"Error in Fg_getParameterIdByName({name}): {SiSoCsRt.Fg_getLastErrorDescription(_fg)} ({Id})");
                Console.ReadKey();
            }
            var ret = SiSoCsRt.Fg_getParameterWithULong(_fg, Id, out var value, 0);
            if (ret < 0)
            {
                Console.WriteLine($"Error in Fg_getParameterWithUInt(): {SiSoCsRt.Fg_getLastErrorDescription(_fg)} ({ret}) ({Id})");
            }
            else
            {
                Console.WriteLine($"{name} = {value}");
            }
        }
        public void GetParameterWithDobule(string name)
        {
            var Id = SiSoCsRt.Fg_getParameterIdByName(_fg, name);
            if (Id <= 0)
            {
                Console.WriteLine($"Error in Fg_getParameterIdByName({name}): {SiSoCsRt.Fg_getLastErrorDescription(_fg)} ({Id})");
                Console.ReadKey();
            }
            var ret = SiSoCsRt.Fg_getParameterWithDouble(_fg, Id, out var value, 0);
            if (ret < 0)
            {
                Console.WriteLine($"Error in Fg_getParameterWithUInt(): {SiSoCsRt.Fg_getLastErrorDescription(_fg)} ({ret}) ({Id})");
            }
            else
            {
                Console.WriteLine($"{name} = {value}");
            }
        }
        #endregion
        private void StopAcquireEx()
        {
            var ret = SiSoCsRt.Fg_stopAcquireEx(_fg, 0, _pmem, 0);
            if (ret < 0)
            {
                Console.WriteLine($"Error in Fg_stopAcquireEx(): {SiSoCsRt.Fg_getLastErrorDescription(_fg)} ({ret})");
            }
        }
        private void DisconnectCamera()
        {
            var ret = SiSoCsRt.Sgc_disconnectCamera(_chPortA);
            if (ret < 0)
            {
                Console.WriteLine($"Port A: Error in Sgc_disconnectCamera(): {SiSoCsRt.Sgc_getErrorDescription(ret)} ({ret})");
            }
        }
        private void FreeMemExGrabber()
        {
            FreeMemEx();
            FreeGrabber();
        }

        private void FreeBoardMemExGrabber()
        {
            FreeBoard();
            FreeMemEx();
            FreeGrabber();
        }

        private void FreeBoard()
        {
            SiSoCsRt.Sgc_freeBoard(_bh);
            _bh = null;
        }

        private void FreeMemEx()
        {
            var ret = SiSoCsRt.Fg_FreeMemEx(_fg, _pmem);
            if (ret < 0)
            {
                Console.WriteLine($"Error in Fg_FreeMemEx(): {SiSoCsRt.Fg_getLastErrorDescription(_fg)} ({ret})");
            }
            _pmem = null;
        }

        private void FreeGrabber()
        {
            var ret = SiSoCsRt.Fg_FreeGrabber(_fg);
            if (ret < 0)
            {
                Console.WriteLine($"Error in Fg_FreeGrabber(): {SiSoCsRt.Fg_getLastErrorDescription(_fg)} ({ret})");
            }
            _fg = null;
        }
    }
}
