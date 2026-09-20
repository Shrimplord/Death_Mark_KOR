# 사인 (死印 / Spirit Hunter: Death Mark) 비공식 한국어 패치

스팀 PC판 *Spirit Hunter: Death Mark* 의 비공식 한국어 패치입니다.

- 버전: 1.0 (2026-09-20)
- 제작: shrimplord89
- 번역 도움: qhsgkr0154, qjatndldi

## 내려받기

**[Death_Mark_KOR_V1.0.zip](https://github.com/Shrimplord/Death_Mark_KOR/releases/download/v1.0/Death_Mark_KOR_V1.0.zip)** (약 110 MB) · [모든 버전](https://github.com/Shrimplord/Death_Mark_KOR/releases)

| | |
|---|---|
| zip sha256 | 963fab03cf8577be4a57025f537515972ddeb2d30c171b8ab19ed763d652881d |
| `패치 적용.exe` sha256 | 6e6c15024f0fdba5d7f7ca06f9e4ecff58d4d4aaede7ae0e5eb2d3f8c31199d5 |
| VirusTotal (`패치 적용.exe`) | https://www.virustotal.com/gui/file/6e6c15024f0fdba5d7f7ca06f9e4ecff58d4d4aaede7ae0e5eb2d3f8c31199d5 |

`패치 적용.exe` 는 코드 서명이 없어 윈도우나 일부 백신이 경고를 띄울 수 있습니다. 소스는 `installer/` 에 전부 있습니다.

## 적용법

1. 스팀에서 게임 언어를 **English** 로 둡니다. 기본값이 영어입니다.
   (라이브러리에서 Spirit Hunter: Death Mark 우클릭 → 속성 → 일반 → 언어)
2. 게임 설치 폴더를 엽니다. (우클릭 → 관리 → 로컬 파일 보기)
3. zip 의 압축을 풀어 내용물을 모두 게임 설치 폴더에 붙여 넣습니다. **반드시 "모두 덮어쓰기"를 선택해 주세요.**
4. `패치 적용.exe` 를 실행합니다.
5. 게임을 실행합니다.

자세한 내용은 zip 안의 `읽어주세요.txt` 에 있습니다.

되돌리려면 스팀에서 "게임 파일 무결성 확인"을 실행하면 됩니다. 무결성 확인이나 게임 업데이트로 패치가 풀리면 위 절차를 처음부터 다시 해 주세요.

## 패치가 하는 일

- `resource\` 의 영문판 리소스 38개(스크립트, 폰트, 이미지 등)를 한국어판으로 교체합니다. 압축을 풀 때 덮어써집니다.
- `Death Mark.exe` 를 수정합니다. 시스템 문구 120항목가량, 이름 입력 화면의 한글 입력, 성/이름 표기 순서가 대상이며 원본은 `bak\` 폴더에 보관됩니다.
- `resource\ui.dat` 안의 UI 이미지 7장을 교체합니다.

인스톨러는 검사를 모두 통과한 뒤에만 게임 폴더를 건드립니다. 실패하면 게임 폴더에 `패치_로그.txt` 가 남습니다.

## 오역·오류 제보

madcow6208@gmail.com

- 오역·게임 내 오류: 해당 화면의 스크린샷을 첨부해 주세요.
- 설치 오류: 오류 화면 캡처와 `패치_로그.txt` 를 함께 보내 주세요.

## 저장소 구성

이 저장소에는 게임 파일이 없습니다. 수정된 게임 파일이 든 배포본은 Releases 의 zip 뿐입니다.

| 경로 | 내용 |
|---|---|
| `읽어주세요.txt` | 배포본에 동봉되는 안내문 |
| `installer/` | `패치 적용.exe` 소스 (C# 5 / .NET Framework 4.5 이상) |

`installer/build.bat` 을 실행하면 윈도우 기본 포함 컴파일러(`csc.exe`)로 `패치 적용.exe` 가 빌드됩니다. 이 컴파일러는 빌드할 때마다 바이너리가 조금씩 달라지므로, 직접 빌드한 exe 의 해시는 위 표와 다릅니다.

## 알림

개인이 만든 비공식 팬 패치이며 게임의 제작사·유통사와 무관합니다. 패치만으로는 게임을 실행할 수 없으며, 스팀 정품이 필요합니다.
