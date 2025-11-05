@echo off
tasklist /fi "imagename eq Revit.exe" | findstr /i "Revit.exe"
if %errorlevel% equ 0 (
    taskkill /f /im Revit.exe
    echo "Revit.exe has been terminated."
) else (
    echo "Revit.exe is not running."
)