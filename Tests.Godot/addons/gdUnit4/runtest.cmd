@echo off
setlocal enabledelayedexpansion

:: Initialize variables
set "godot_binary="
set "filtered_args="

:: Process all arguments
set "i=0"
:parse_args
if "%~1"=="" goto end_parse_args

if "%~1"=="--godot_binary" (
    set "godot_binary=%~2"
    shift
    shift
) else (
    set "filtered_args=!filtered_args! %~1"
    shift
)
goto parse_args
:end_parse_args

:: If --godot_binary wasn't provided, fallback to environment variable
if "!godot_binary!"=="" (
    set "godot_binary=%GODOT_BIN%"
)

:: Check if we have a godot_binary value from any source
if "!godot_binary!"=="" (
    echo Godot binary path is not specified.
    echo Please either:
    echo   - Set the environment variable: set GODOT_BIN=C:\path\to\godot.exe
    echo   - Or use the --godot_binary argument: --godot_binary C:\path\to\godot.exe
    exit /b 1
)

:: Check if the Godot binary exists
if not exist "!godot_binary!" (
    echo Error: The specified Godot binary '!godot_binary!' does not exist.
    exit /b 1
)

:: Get Godot version and check if it's a mono build
for /f "tokens=*" %%i in ('"!godot_binary!" --version') do set GODOT_VERSION=%%i
echo !GODOT_VERSION! | findstr /I "mono" >nul
if !errorlevel! equ 0 (
    echo Godot .NET detected
    echo Compiling c# classes ... Please Wait
    dotnet build --debug
    echo done !errorlevel!
)

:: Optional: Redirect user:// to a custom filesystem directory (env: GODOT_USERDIR/GODOT_USER_DIR).
:: IMPORTANT: This must be a real filesystem path, NOT a Godot virtual path like user://.
set "userdir_args="
set "user_dir="
if not "%GODOT_USERDIR%"=="" set "user_dir=%GODOT_USERDIR%"
if "!user_dir!"=="" if not "%GODOT_USER_DIR%"=="" set "user_dir=%GODOT_USER_DIR%"
set "user_dir_flag=%GODOT_USERDIR_FLAG%"
if /I "!user_dir_flag!"=="auto" set "user_dir_flag="

if not "!user_dir!"=="" (
    echo !user_dir! | findstr /I /B "user:// res://" >nul
    if !errorlevel! equ 0 (
        echo Error: GODOT_USERDIR must be a filesystem path, not a Godot virtual path like user://
        echo Current value: !user_dir!
        exit /b 1
    )

    echo !user_dir! | findstr ":" >nul
    if !errorlevel! equ 0 (
        echo !user_dir! | findstr /R /C:"^[A-Za-z]:[\\/]" >nul
        if !errorlevel! neq 0 (
            echo Error: GODOT_USERDIR contains ':' but is not an absolute Windows drive path (expected like C:\path\to\dir)
            echo Current value: !user_dir!
            exit /b 1
        )
    )

    if "!user_dir_flag!"=="" (
        call :detect_userdir_flag
    )
    if "!user_dir_flag!"=="" (
        echo Warning: Could not detect userdir flag from Godot --help; running without user:// redirection.
    ) else (
        set "userdir_args=!user_dir_flag! \"!user_dir!\""
        echo Using user:// redirection: !user_dir_flag! !user_dir!
    )
)

:: Run the tests with the filtered arguments
"!godot_binary!" !userdir_args! --path . -s -d res://addons/gdUnit4/bin/GdUnitCmdTool.gd !filtered_args!
set exit_code=%ERRORLEVEL%
echo Run tests ends with %exit_code%

:: Run the copy log command
"!godot_binary!" !userdir_args! --headless --path . --quiet -s res://addons/gdUnit4/bin/GdUnitCopyLog.gd !filtered_args! > nul
set exit_code2=%ERRORLEVEL%
exit /b %exit_code%

:detect_userdir_flag
set "user_dir_flag="
"!godot_binary!" --help 2>&1 | findstr /I /C:"--user-data-dir" >nul
if !errorlevel! equ 0 (
    set "user_dir_flag=--user-data-dir"
    goto :eof
)
"!godot_binary!" --help 2>&1 | findstr /I /C:"--user-dir" >nul
if !errorlevel! equ 0 (
    set "user_dir_flag=--user-dir"
    goto :eof
)
"!godot_binary!" --help 2>&1 | findstr /I /C:"--userdir" >nul
if !errorlevel! equ 0 (
    set "user_dir_flag=--userdir"
    goto :eof
)
goto :eof
