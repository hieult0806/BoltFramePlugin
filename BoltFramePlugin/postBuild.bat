@echo off
REM Path to Revit executable
set revit_path="C:\Program Files\Autodesk\Revit 2025\Revit.exe"

REM Path to the default project
set project_path="D:\GameDevelopments\BoltFramePlugin\Bed.rvt"

REM Start Revit without blocking
start /B "" %revit_path% %project_path%

REM Optional: Echo message to indicate Revit is starting
echo Revit is starting with project: %project_path%

REM Exit the batch file to signal Visual Studio that the task is done
exit