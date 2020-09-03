@echo off
setlocal
set THIS_NAME=%~nx0
set THIS_DIR=%~dp0
if "%1"=="-v" (
    shift
    echo on
)
IF %THIS_DIR:~-1%==\ SET THIS_DIR=%THIS_DIR:~0,-1%
set PROJECT_NAME=
set PROJECT_NAME=%~nx1
if not defined PROJECT_NAME (
    echo>&2 Missing or invalid project path specification!
    exit /b 1
)
set DEFAULT_GIT_USER_EMAIL=atifaziz@users.noreply.github.com
set /p GIT_USER_EMAIL=Enter e-mail address for commits (default = %DEFAULT_GIT_USER_EMAIL%):
if not defined GIT_USER_EMAIL set GIT_USER_EMAIL=%DEFAULT_GIT_USER_EMAIL%
git clone "%THIS_DIR%" %1 || goto :EOF
pushd %1
    rd /s /q .git                                               ^
 && git init                                                    ^
 && git config user.name "Atif Aziz"                            ^
 && git config user.email "%GIT_USER_EMAIL%"                    ^
 && dotnet new sln                                              ^
 && dotnet new classlib -o src -n "%PROJECT_NAME%"              ^
 && dotnet new nunit -o tests -n "%PROJECT_NAME%.Tests"         ^
 && dotnet sln add src                                          ^
 && dotnet sln add tests                                        ^
 && dotnet add tests reference src                              ^
 && dotnet test tests                                           ^
 && del README.md                                               ^
 && del "%THIS_NAME%"                                           ^
 && git add .                                                   ^
 && git add --chmod +x *.sh
popd
endlocal
if not %ERRORLEVEL%==0 goto :EOF
echo ................................................
echo .########..########....###....########..##....##
echo .##.....##.##.........##.##...##.....##..##..##.
echo .##.....##.##........##...##..##.....##...####..
echo .########..######...##.....##.##.....##....##...
echo .##...##...##.......#########.##.....##....##...
echo .##....##..##.......##.....##.##.....##....##...
echo .##.....##.########.##.....##.########.....##...
echo ................................................
echo May the Force be with you!
cd /d %1
