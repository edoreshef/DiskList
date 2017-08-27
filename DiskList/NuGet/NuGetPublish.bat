@echo off

rem choose version
for /F "delims=" %%i in ('dir *.nupkg /b /aa-h /t:c /on') do set mostRecent=%%i
echo Choose version name (most packange: %mostRecent%):
set /P version=Version number (as x.x.x): 

echo.
nuget pack ..\DiskList.csproj -Properties Configuration=Release -Version %version%

echo.
set /P c=Do you want to publish package [Y/N]?
if /I "%c%" EQU "Y" goto :pushPackage
goto :exit

:pushPackage
nuget push DiskList.%version%.nupkg 4c773282-a1b5-4672-bfc2-ee604a08c58d -Source https://www.nuget.org/api/v2/package

:exit