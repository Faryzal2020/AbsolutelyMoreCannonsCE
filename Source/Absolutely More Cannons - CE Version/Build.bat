@echo off
echo Building Turret Barrel Animation Mod...

REM Set the RimWorld path (adjust this if your installation is different)
set RimWorldPath=G:\SteamLibrary\steamapps\common\RimWorld

REM Navigate to the source directory
cd "G:\SteamLibrary\steamapps\common\RimWorld\Mods\(Dev) Absolutely More Cannons - CE Version 1.6\Source\Absolutely More Cannons - CE Version"

REM Create output directory if it doesn't exist
if not exist "..\..\Assemblies" mkdir "..\..\Assemblies"

REM Build using CSC compiler directly (bypasses MSBuild/NuGet issues)
REM Note: HarmonyPatches.cs uses runtime type loading, so we exclude it from compilation
echo Building project...
"C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe" /target:library /out:"..\..\Assemblies\AbsolutelyMoreCannons.dll" /reference:"%RimWorldPath%\RimWorldWin64_Data\Managed\Assembly-CSharp.dll" /reference:"%RimWorldPath%\RimWorldWin64_Data\Managed\UnityEngine.dll" /reference:"%RimWorldPath%\RimWorldWin64_Data\Managed\UnityEngine.CoreModule.dll" /reference:"C:\Windows\Microsoft.NET\Framework\v4.0.30319\System.dll" /reference:"C:\Windows\Microsoft.NET\Framework\v4.0.30319\System.Core.dll" /reference:"C:\Windows\Microsoft.NET\Framework\v4.0.30319\System.Xml.dll" /reference:"C:\Windows\Microsoft.NET\Framework\v4.0.30319\System.Xml.Linq.dll" /optimize+ /debug- Main.cs TurretBarrelExtension.cs CompProperties_TurretBarrel.cs CompTurretBarrel.cs

echo.
if %ERRORLEVEL% EQU 0 (
    echo Build successful! AbsolutelyMoreCannons.dll created in Assemblies folder.
    echo The dll is ready for use in RimWorld!
) else (
    echo Build failed! Check the error messages above.
    echo Make sure RimWorldPath is set correctly: %RimWorldPath%
)
echo.
pause
