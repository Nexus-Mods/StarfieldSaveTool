@echo off

echo The full path of the file is: %1

REM Run the command line application with the file path as an argument
"%~dp0StarfieldSaveTool.exe" %1 --output-json-file --output-raw-file

REM Pause the output to keep the command prompt open
pause