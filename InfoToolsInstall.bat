@echo off
chcp 1251

REM ���� � ������, ��������
set revit2018path=%appdata%\Autodesk\Revit\Addins\2018
set revit2019path=%appdata%\Autodesk\Revit\Addins\2019
set autodeskBundlePath= %appdata%\Autodesk\ApplicationPlugins

REM ������� ������ ����� � �����
del /q %revit2018path%\RevitInfoTools.addin >nul 2>nul
rd %revit2018path%\RevitInfoTools /s /q >nul 2>nul
del /q %revit2019path%\RevitInfoTools.addin >nul 2>nul
rd %revit2019path%\RevitInfoTools /s /q >nul 2>nul
rd %autodeskBundlePath%\Civil3DInfoTools.bundle /s /q >nul 2>nul

REM ���������� ����� ������
xcopy Revit2018Addin /E /I /F /Y %revit2018path%\ &&  (
		echo ���������� Revit2018. �������
		) || (
		echo ������ ��� ���������� ������� ��� REVIT2018. �������� �� ������ REVIT2018
		goto RETURN
		)

xcopy Revit2019Addin /E /I /F /Y %revit2019path%\ &&  (
		echo ���������� Revit2019. �������
		) || (
		echo ������ ��� ���������� ������� ��� REVIT2019. �������� �� ������ REVIT2019
		goto RETURN
		)

xcopy Civil3DInfoTools.bundle /E /I /F /Y %autodeskBundlePath%\Civil3DInfoTools.bundle\ &&  (
		echo ���������� Civil3D. �������
		) || (
		echo ������ ��� ���������� ������� ��� CIVIL3D. �������� �� ������ CIVIL3D
		goto ABORTED
		)





echo ��������� (����������) ��������� �������
pause 

:ABORTED
	echo ��������� (����������) �� ���������
	pause