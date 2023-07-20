![FramegrabberSDK](doc/images/FramegrabberSDK_800x200.png "FramegrabberSDK")
# Framegrabber SDK
Framegrabber SDK sample codes
## Prerequisites
 * Framegrabber SDK version 5.10.
## C++
### Property
Add paths in C++ project property:
 * VC++ Directories
   * Include Directories
      * $(BASLER_FG_SDK_DIR)\SDKExamples\include;
      * $(BASLER_FG_SDK_DIR)\include
   * Library Directories
      * $(BASLER_FG_SDK_DIR)\lib\visualc

## C#
Add dll to project reference.
### Reference
 * SiSoCsInterface
   * Dll path: $(BASLER_FG_SDK_DIR)\bin\SiSoCsInterface.dll

## Python
### Installation
1. Install python. The support version is 3.7 ~ 3.10.
2. Install numpy by command prompt.
```console
python -m pip install --user numpy
```
4. Set environment variables. 
 * By python:
```python
import os
import sys
framegrabber_sdk_path = os.environ['BASLER_FG_SDK_DIR']
sys.path.insert(0, rf"{framegrabber_sdk_path}\bin")
sys.path.insert(0, rf"{framegrabber_sdk_path}\SDKWrapper\PythonWrapper\python310\lib")
```
 * By manual: Add paths to variable ```Path``` in environment variables. 
```
%BASLER_FG_SDK_DIR%\bin;%BASLER_FG_SDK_DIR%\SDKWrapper\PythonWrapper\python310\lib
```
### Reference
https://docs.baslerweb.com/frame-grabbers/python-wrapper#installation
