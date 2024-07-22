@echo off
setlocal enabledelayedexpansion

:: Initialize an empty variable to hold the concatenated paths
set paths=

:: Loop through each file dropped onto the batch file
for %%f in (%*) do (
    echo Using file: %%f
    :: Concatenate the current file path to the variable with a semi-colon separator
    set paths=!paths!;%%f
)

:: Display the concatenated paths
echo paths=!paths!

REM Run the command line application with the file path as an argument
"%~dp0StarfieldSaveTool.exe" !paths! --output-json-file --output-raw-file

REM Pause the output to keep the command prompt open
pause