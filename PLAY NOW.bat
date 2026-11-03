@echo off
title Lunar Smart Adaptive Habitat - PLAY NOW
cd /d "%~dp0"
rem 1) game is next to this file (extracted zip)
if exist "LunarHabitat.exe" ( start "" "LunarHabitat.exe" & exit /b )
rem 2) repo folder: look in the usual build location
if exist "Builds\Windows\LunarHabitat.exe" ( start "" "Builds\Windows\LunarHabitat.exe" & exit /b )
if exist "..\official projects for nasa\unity 3\Builds\Windows\LunarHabitat.exe" ( start "" "..\official projects for nasa\unity 3\Builds\Windows\LunarHabitat.exe" & exit /b )
rem 3) nothing here yet: open the download page
echo No game build next to this file yet - opening the download page...
start "" "https://github.com/SuperCoolJai/NASA-HUNCH-3D-Print-Kits/releases/latest"
echo Download LunarHabitat_Windows.zip, Extract All, then double-click PLAY NOW.bat inside it.
pause
