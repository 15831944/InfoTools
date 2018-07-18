@echo off
chcp 1251

REM оСРХ Й ОЮОЙЮЛ, ОКЮЦХМНБ
set revit2018path=%appdata%\Autodesk\Revit\Addins\2018
set revit2019path=%appdata%\Autodesk\Revit\Addins\2019
set autodeskBundlePath= %appdata%\Autodesk\ApplicationPlugins

REM сДЮКХРЭ ЯРЮПШЕ ТЮИКШ Х ОЮОЙХ
del /q %revit2018path%\RevitInfoTools.addin >nul 2>nul
rd %revit2018path%\RevitInfoTools /s /q >nul 2>nul
del /q %revit2019path%\RevitInfoTools.addin >nul 2>nul
rd %revit2019path%\RevitInfoTools /s /q >nul 2>nul
rd %autodeskBundlePath%\Civil3DInfoTools.bundle /s /q >nul 2>nul

REM оЕПЕОХЯЮРЭ МНБШЕ БЕПЯХХ
xcopy Revit2018Addin /E /I /F /Y %revit2018path%\ &&  (
		echo нАМНБКЕМХЕ Revit2018. сяоеьмн
		) || (
		echo ньхайю опх янупюмемхх окюцхмю дкъ REVIT2018. бнглнфмн ме гюйпшр REVIT2018
		goto RETURN
		)

xcopy Revit2019Addin /E /I /F /Y %revit2019path%\ &&  (
		echo нАМНБКЕМХЕ Revit2019. сяоеьмн
		) || (
		echo ньхайю опх янупюмемхх окюцхмю дкъ REVIT2019. бнглнфмн ме гюйпшр REVIT2019
		goto RETURN
		)

xcopy Civil3DInfoTools.bundle /E /I /F /Y %autodeskBundlePath%\Civil3DInfoTools.bundle\ &&  (
		echo нАМНБКЕМХЕ Civil3D. сяоеьмн
		) || (
		echo ньхайю опх янупюмемхх окюцхмю дкъ CIVIL3D. бнглнфмн ме гюйпшр CIVIL3D
		goto ABORTED
		)





echo сярюмнбйю (намнбкемхе) бшонкмемн сяоеьмн
pause 

:ABORTED
	echo сярюмнбйю (намнбкемхе) ме гюбепьемн
	pause