@echo off
REM Double-click this to build the two release zips into dist\.
REM
REM The first version of this file redirected with /dev/null instead of nul,
REM which is Unix, not Windows. The redirect failed and printed
REM "The system cannot find the path specified." before the script ran.
REM The build itself was unaffected, but the message was alarming and wrong.
REM pushd rather than cd /d as well, because %~dp0 ends in a backslash and
REM that escapes the closing quote.
pushd "%~dp0"

where py >nul 2>&1
if %errorlevel%==0 goto runpy
where python >nul 2>&1
if %errorlevel%==0 goto runpython
goto nopython

:runpy
py tools\pack.py
goto done

:runpython
python tools\pack.py
goto done

:nopython
echo.
echo Python was not found on this machine.
echo Install it from python.org, tick "Add python.exe to PATH" during setup,
echo then double-click this file again.

:done
popd
echo.
echo ----------------------------------------------------------------
echo Finished. Read the two file lists above before uploading anything.
echo ----------------------------------------------------------------
pause
