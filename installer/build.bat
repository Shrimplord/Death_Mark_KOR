@echo off
rem 사인 한글패치 인스톨러 빌드
rem ※ 이 파일은 CP949(ANSI)로 저장해야 한다. cmd가 배치 파일을 시스템 OEM
rem    코드페이지로 읽기 때문에, UTF-8로 저장하면 /out: 의 한글 파일명이 깨져
rem    "파일 이름이 너무 길거나 잘못되었습니다"(CS2021) 오류가 난다.
rem 윈도우에 기본 포함된 .NET Framework 컴파일러만 쓴다. 별도 설치 불필요.
rem 코드는 전부 C# 5 문법이라 구형 csc.exe로도 빌드된다.

setlocal
set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
if not exist "%CSC%" (
  echo [오류] csc.exe 를 찾을 수 없습니다: %CSC%
  echo .NET Framework 4 이상이 설치돼 있어야 합니다.
  pause
  exit /b 1
)

"%CSC%" /nologo /target:exe /platform:anycpu /optimize+ ^
  /out:"패치 적용.exe" ^
  /win32manifest:app.manifest ^
  /reference:System.dll ^
  DmPatch.cs Manifest.cs UiDat.cs Program.cs

if errorlevel 1 (
  echo.
  echo [실패] 빌드 오류
  pause
  exit /b 1
)

echo.
echo 빌드 완료: "패치 적용.exe"
pause
