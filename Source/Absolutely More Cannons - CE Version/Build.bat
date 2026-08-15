@echo off
echo Building Absolutely More Cannons - CE Version...

dotnet build "Absolutely More Cannons - CE Version.csproj" -c Release

echo.
if %ERRORLEVEL% EQU 0 (
    echo Build successful! AbsolutelyMoreCannons.dll created in Assemblies folder.
) else (
    echo Build failed! Check the error messages above.
)
echo.
pause
