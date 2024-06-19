@echo off
cd /d "%~dp0"  REM Change to the directory of the batch file

REM Replace placeholders with actual paths and arguments
set elevationRaster="%1"
set carve_path="%2"
set outletsLayer="%3"
set outputPath="%4"
set targetSize="%5"
set modelType="%6"

REM Execute your C# executable with arguments
%7 "%elevationRaster%" "%carve_path%" "%outletsLayer%" "%outputPath%" "%targetSize%" "%modelType%"

pause  REM Pause to keep the window open