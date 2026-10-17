@echo off
rem MusaicKontrol.exe'yi Windows'un yerlesik C# derleyicisiyle olusturur (ek kurulum gerekmez).
cd /d "%~dp0"
"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /codepage:65001 /optimize /target:winexe /out:MusaicKontrol.exe /r:System.dll /r:System.Core.dll /r:System.Xml.dll /r:System.Xml.Linq.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll MusaicKontrol.cs MusaicArayuz.cs
if errorlevel 1 (echo Derleme basarisiz. & pause & exit /b 1)
echo Tamam: MusaicKontrol.exe olusturuldu.
