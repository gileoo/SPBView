echo %1
cd bin\Debug

powershell rm -r -fo .\%1\

mkdir %1

xcopy *.dll %1
xcopy Accord.dll.config %1
xcopy ExperimentSetup.conf %1
xcopy EyeTargets.conf %1
xcopy InputData.conf %1
xcopy Media.conf %1
xcopy SPBView.exe %1
xcopy SPBView.exe.config %1

powershell Compress-Archive -Path %1 %1_Debug.zip

move %1_Debug.zip ..\..\..\bin

cd ..
cd Release

powershell rm -r -fo .\%1\

mkdir %1

xcopy *.dll %1
xcopy Accord.dll.config %1
xcopy ExperimentSetup.conf %1
xcopy EyeTargets.conf %1
xcopy InputData.conf %1
xcopy Media.conf %1
xcopy SPBView.exe %1
xcopy SPBView.exe.config %1

powershell Compress-Archive -Path %1 %1_Release.zip

move %1_Release.zip ..\..\..\bin

