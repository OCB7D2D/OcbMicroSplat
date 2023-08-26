@echo off

call MC7D2D MicroSplat.dll /reference:"%PATH_7D2D_MANAGED%\Assembly-CSharp.dll" ^
  Harmony\*.cs Library\Configs\*.cs Library\Textures\*.cs Library\Utils\*.cs && ^
echo Successfully compiled MicroSplat.dll

pause