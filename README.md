# DesktopAudioController

Windows에서 사용자가 고른 출력 장치만 표시하고, 장치 마스터 볼륨과 해당 장치에서 실제로 소리를 내는 프로그램별 세션 볼륨을 빠르게 제어하는 WPF 데스크톱 유틸리티입니다.

[최신 릴리즈 바로가기](https://github.com/TailFox-Forge/DesktopAudioController/releases/latest)

## 기여 및 문의

본 프로젝트는 OpenAI Codex, Anthropic Claude와 함께 만들어가는 초기 단계 프로젝트입니다.

```text
- 버그 제보, 개선 제안, 문서 수정 제안은 GitHub Issues 또는 Pull Request 기준으로 받습니다.
- 재현 절차, 로그, 사용 환경을 함께 남기면 확인 속도가 빨라집니다.
- 배포 zip, 종료 로그, 장치/세션 재현 정보가 있으면 오디오 이슈 분석이 수월합니다.
- 공개 이슈에 로그를 올릴 때는 첨부 전 내용을 한 번 확인하고, DxDiag / CPU-Z 원본 텍스트는 그대로 올리지 않는 것을 권장합니다.
```

## 현재 상태

```text
기준 버전: v0.13.15
개발 단계: Phase 13 안정성 개선 + Phase 14 비정상 종료 감지 및 자동 업데이트 검증
Public 전환: 완료
런타임: .NET 8 / WPF / Windows 전용
배포 형태: win-x64 self-contained single-file exe zip
릴리즈 페이지: https://github.com/TailFox-Forge/DesktopAudioController/releases
최근 확인 사항:
- 저장소 Public 전환 완료 및 부팅 직후 제한 모드 자동 복구 흐름 반영
- startup degraded mode / atomic settings save / current log rollover 반영
- 비정상 종료 감지 및 다음 실행 안내 팝업 1차 반영
- win-x64 publish 산출물을 single-file exe zip 기준으로 고정
- 프로그램 이름 자동 개선 / 사용자 지정 이름 저장 복원 반영
- Inactive 세션 상태 표기 및 "재생 중인 앱만 보기" 옵션 반영
- X 버튼 종료 시 OnExit dispose 단계 로그 및 종료 hang 대응 반영
- 설정창에서 로그 폴더 바로 열기 지원
- 공개 첨부용 로그에서 장치/세션 식별자와 로컬 경로 요약 처리
- 앱 시작 시 로그 7일 / 30개 / 100MB 기준 자동 정리
- 실행 파일 및 창 아이콘 커스텀 에셋 적용
- 업데이트 방식은 zip 다운로드 후 압축 해제 / 덮어쓰기 기준 유지
- v0.13.10부터 앱 내 업데이트 버튼으로 zip 다운로드 / sha256 검증 / 덮어쓰기 적용 자동화
- 기본 업데이트 채널은 stable only, prerelease는 opt-in 설정으로 유지
- v1.0.0 출고 기준 / 회귀 검증 체크리스트 문서화 완료
```

## 프로그램 동작 방식

### 1. 앱 시작

```text
- 시작 시 settings.json을 로드합니다.
- 설정 파일이 손상되어 있으면 기존 파일을 settings.json.bak로 백업하고 기본값으로 복구합니다.
- 최초 실행이거나 표시할 장치를 아직 고르지 않았으면 설정창을 먼저 엽니다.
- RunAtWindowsStartup가 켜져 있으면 시작 시 자동 실행 레지스트리 상태를 다시 맞춥니다.
- 로그 파일은 앱 시작과 함께 초기화됩니다.
```

### 2. 메인 화면

```text
- 설정에서 선택한 출력 장치만 메인 화면에 표시합니다.
- ShowOnlyConnectedDevices가 켜져 있으면 연결된 장치만 보여줍니다.
- 각 장치 카드에서 마스터 볼륨, 음소거, 기본 출력 장치 전환을 수행할 수 있습니다.
- 기본 장치는 화면 갱신 시 자동으로 눈에 띄게 반영됩니다.
```

### 3. 장치 / 세션 제어

```text
- 장치 마스터 볼륨 변경은 약 80ms debounce 후 실제 오디오 서비스에 반영됩니다.
- 장치 음소거는 즉시 반영됩니다.
- 장치를 펼치면 해당 장치에 연결된 프로그램 세션 목록을 표시합니다.
- 기본값으로 Active / Inactive 세션을 함께 보여주고, 무음 상태 세션은 `현재 재생 중 아님`으로 구분합니다.
- 설정에서 `재생 중인 앱만 보기`를 켜면 Active 세션만 표시합니다.
- 프로그램 세션에서도 볼륨 / 음소거 / 이름 변경을 각각 제어할 수 있습니다.
- 세션 볼륨 변경 역시 약 80ms debounce 후 실제 오디오 서비스에 반영됩니다.
- 이름이 같은 프로그램이 여러 개면 경로나 세션 힌트로 표시명을 구분합니다.
- 시스템 사운드 세션은 옵션으로 포함하거나 숨길 수 있습니다.
```

### 4. 프로그램별 설정 저장 / 복원

```text
- 프로그램별 볼륨 / 음소거 상태와 사용자 지정 이름을 저장합니다.
- 저장 키는 가능한 경우 실행 파일 경로 기반으로 잡고, 필요하면 세션 경로 또는 표시명으로 보정합니다.
- 세션 슬라이더를 짧게 여러 번 움직여도 설정 파일 저장은 마지막 입력 기준 약 400ms 뒤 1회만 수행됩니다.
- 설정창을 열기 직전과 앱 종료 직전에는 pending 저장을 즉시 flush합니다.
- 살아 있는 세션이 다시 보이면 저장된 프로그램 설정과 이름을 자동 복원합니다.
```

### 5. 자동 갱신

```text
- 오디오 장치 / 세션 / 상태 변경 이벤트를 받아 화면을 자동 갱신합니다.
- 상태 변화만 있으면 부분 갱신으로 처리하고, 장치 구조 변화가 있으면 전체 갱신으로 전환합니다.
- 앱 아이콘과 프로세스 메타데이터는 캐시를 사용해 반복 조회 비용을 줄입니다.
- 세션 IconPath, 실행 파일 경로, `app.exe,-123` 형태 리소스 경로를 기준으로 아이콘을 읽습니다.
- GitHub 릴리즈 확인은 백그라운드에서 수행하며 UI를 멈추지 않도록 짧은 timeout과 예외 무시 정책을 사용합니다.
- 기본값은 정식(stable) 릴리즈만 확인하며, 설정에서 원하면 프리릴리즈까지 함께 안내할 수 있습니다.
- 새 버전이 있으면 상단에 최신 버전 안내를 표시하고, zip 다운로드 링크를 바로 여는 업데이트 버튼을 노출합니다.
- 실제 적용은 자동 패치가 아니라 zip 다운로드 후 기존 실행 폴더 덮어쓰기 방식입니다.
```

### 6. 트레이 동작

```text
- MinimizeToTray가 켜져 있으면 창 닫기 시 종료 대신 트레이로 숨깁니다.
- 트레이 메뉴에서 창 열기, 설정 열기, 새로고침, 기본 장치 빠른 전환, 장치 음소거 토글, 종료를 실행할 수 있습니다.
- v0.8.3부터 트레이 종료는 Shutdown 단독 호출이 아니라 메인 창 Close 경로를 타도록 변경했습니다.
- 이 경로에서 종료 직전 pending 설정을 flush하고 WPF 종료 파이프라인을 정상적으로 통과합니다.
```

## 설정 항목

```text
- 표시할 출력 장치 선택
- 시작 시 최소화
- Windows 시작 시 자동 실행
- 트레이로 최소화
- 연결된 장치만 표시
- 재생 중인 앱만 보기
- 시스템 사운드 세션 표시
- 프리릴리즈 업데이트 포함 여부
```

## 저장 파일과 로그

설정 파일:

```text
%LocalAppData%\DesktopAudioController\settings.json
%LocalAppData%\DesktopAudioController\settings.json.bak
```

진단 로그:

```text
%LocalAppData%\DesktopAudioController\logs\DesktopAudioController-YYYYMMDD.log
```

로그 보관 정책:

```text
- 날짜별 로그 파일 1개 유지
- 앱 시작 시 7일 초과 로그 삭제
- 앱 시작 시 최근 30개까지만 유지
- 앱 시작 시 총 로그 용량 100MB 상한 유지
```

공개 로그 업로드 주의:

```text
- 설정창의 `로그 폴더 열기` 버튼으로 바로 로그 폴더를 열 수 있습니다.
- 현재 로그는 deviceId / sessionId / 로컬 파일 경로를 그대로 남기지 않고 요약 형태로 기록합니다.
- 그래도 공개 이슈 첨부 전에는 로그 내용을 한 번 확인하는 것을 권장합니다.
```

로그에서 바로 확인할 수 있는 대표 항목:

```text
- 설정 로드 / 저장 경로
- 프로그램 설정 저장 완료 count=...
- 저장된 프로그램 설정 복원
- 로그 폴더 열기 성공 / 실패
- 트레이 종료 요청 처리
- OnExit 시작 / 장치 Dispose / 세션 Dispose / 알림 Dispose / OnExit 완료
```

## 기술 스택

```text
- C#
- .NET 8
- WPF
- Windows Forms NotifyIcon
- Windows Core Audio API + NAudio
- PolicyConfig COM interop
```

## 사용 가능한 환경

```text
- Windows 10 x64 / Windows 11 x64
- win-x64 self-contained 배포본 기준으로 별도 .NET 런타임 설치 없이 실행 가능
- 일반 사용자 권한에서 실행 가능
- 인터넷 연결은 업데이트 확인 / 릴리즈 페이지 이동 / zip 다운로드 시에만 필요
- 다중 출력 장치, USB 오디오 장치, 가상 오디오 장치 환경에서 사용 가능
```

현재 미검증 또는 비지원 범위:

```text
- macOS / Linux
- Windows ARM64 네이티브 배포
- Windows Server 계열 환경
```

## 개발 / 검증 환경

아래 사양은 개발자가 빌드, 게시, 실사용 테스트에 사용한 기준 시스템입니다. 최소/권장 사양이 아닙니다.

### 환경 A - 데스크톱

| 항목 | 사양 |
| --- | --- |
| OS | Windows 11 Pro 64-bit, Build 26200 |
| 개발 도구 | Visual Studio 2026 Community |
| 대상 런타임 | .NET 8 / WPF / Windows Forms |
| CPU | AMD Ryzen 7 7800X3D, 8코어 16스레드 |
| 메모리 | DDR5 96GB |
| GPU | NVIDIA GeForce RTX 4080 SUPER, VRAM 16GB |
| DirectX | DirectX 12 |
| 주 모니터 | 2560 x 1440, 180Hz |
| 보조 모니터 | 1920 x 1080, 240Hz |

### 환경 B - 노트북

| 항목 | 사양 |
| --- | --- |
| 모델 | ASUS TUF Gaming A18 FA808UP |
| OS | Windows 11 Pro 64-bit, Build 26200 |
| 개발 도구 | Visual Studio 2026 Community |
| 대상 런타임 | .NET 8 / WPF / Windows Forms |
| CPU | AMD Ryzen 7 260 w/ Radeon 780M Graphics, 8코어 16스레드 |
| 메모리 | 32GB RAM |
| dGPU | NVIDIA GeForce RTX 5070 Laptop GPU, VRAM 8GB |
| iGPU | AMD Radeon 780M Graphics |
| DirectX | DirectX 12 |
| 내장 디스플레이 | 1920 x 1200, 144Hz |
| 외부 디스플레이 테스트 | 1920 x 1080, 60Hz |

보관 정책:

```text
- CPU-Z / DxDiag 원본에는 PC 이름, 장치 식별자 등 민감할 수 있는 항목이 포함될 수 있습니다.
- 저장소에는 원본 텍스트를 보관하지 않고, 공개 가능한 요약 사양만 기록합니다.
```

## 최소 사양 / 권장 사양

대략적인 기준:

```text
최소 사양
- OS: Windows 10 x64
- CPU: 2코어 이상
- 메모리: 4GB RAM
- 저장공간: 200MB 이상 여유 공간
- 기타: 사용 가능한 출력 장치 1개 이상

권장 사양
- OS: Windows 11 x64 또는 Windows 10 22H2 이상
- CPU: 4코어 이상
- 메모리: 8GB RAM 이상
- 저장공간: SSD 기준 500MB 이상 여유 공간
- 기타: 다중 출력 장치 또는 USB 오디오 장치 사용 환경
```

## 프로젝트 구조

```text
DesktopAudioController/
  README.md                                                   -> 프로젝트 개요, 실행 방식, 배포/업데이트, 현재 단계 설명
  LICENSE                                                     -> 라이선스
  docs/
    design.md                                                 -> 설계 배경, 구현 방향, 단계별 설계 메모
    regression-checklist.md                                   -> 수동 회귀 검증 체크리스트
    release-notes-template.md                                 -> 릴리즈 노트 작성 템플릿
    v1-release-criteria.md                                    -> v1.0.0 출고 기준
    images/
      main-window-example.png                                 -> 메인 화면 예시 이미지 자산
  scripts/
    publish-win-x64.sh                                        -> win-x64 self-contained single-file exe zip 생성 스크립트
  src/
    DesktopAudioController/
      DesktopAudioController.csproj                           -> 앱 타깃 프레임워크, 버전, 패키지 참조 정의
      app.manifest                                             -> Windows 실행 권한 / DPI / 호환성 매니페스트
      App.xaml                                                 -> WPF 애플리케이션 리소스 진입점
      App.xaml.cs                                              -> 앱 시작, 서비스 생성, 메인 창 초기화, 종료 Dispose 순서 제어
      Infrastructure/
        ObservableObject.cs                                    -> MVVM 바인딩용 INotifyPropertyChanged 기반 클래스
      Models/
        AppSettings.cs                                         -> settings.json 저장 스키마
        AudioDeviceInfo.cs                                     -> 출력 장치 1개에 대한 상태 모델
        AudioSessionInfo.cs                                    -> 오디오 세션 1개에 대한 상태 모델
        ProcessMetadataInfo.cs                                 -> PID / 실행 파일 / 표시명 캐시 모델
        ProgramAudioPreference.cs                              -> 프로그램별 볼륨 / 음소거 / 사용자 지정 이름 저장 모델
      Services/
        AppLog.cs                                              -> 로그 초기화 및 Info/Warn/Error 기록 유틸리티
        AudioNotificationChangedEventArgs.cs                   -> 오디오 변경 종류(State/Topology) 이벤트 인자
        CachedAppIconService.cs                                -> 앱 아이콘 로딩 및 캐시
        CachedProcessMetadataService.cs                        -> 프로세스명 / exe 경로 / 파일 메타데이터 캐시
        GitHubReleaseUpdateCheckService.cs                     -> GitHub Releases 조회 및 최신 버전 판정
        IAudioDeviceCatalogService.cs                          -> 출력 장치 조회/제어 서비스 인터페이스
        IAudioNotificationService.cs                           -> 오디오 이벤트 구독 서비스 인터페이스
        IAudioSessionService.cs                                -> 세션 조회/볼륨/음소거 제어 인터페이스
        IAppIconService.cs                                     -> 아이콘 조회 서비스 인터페이스
        IProcessMetadataCacheService.cs                        -> 프로세스 메타데이터 캐시 인터페이스
        ISettingsService.cs                                    -> 설정 로드/저장 서비스 인터페이스
        IStartupLaunchService.cs                               -> 자동 실행 등록 서비스 인터페이스
        IUpdateCheckService.cs                                 -> 업데이트 확인 서비스 인터페이스
        NativeAudioDeviceCatalogService.cs                     -> Core Audio 기반 장치 열거 / 마스터 볼륨 / 기본 장치 변경
        NativeAudioNotificationService.cs                      -> Core Audio 이벤트 구독 / 해제 / 종료 시 Dispose 처리
        NativeAudioSessionService.cs                           -> 장치별 세션 열거, 세션 볼륨/음소거, 세션 이름 보정
        PlaceholderAudioDeviceCatalogService.cs                -> UI 골격 점검용 가짜 장치 서비스
        ProgramAudioPreferenceStore.cs                         -> 프로그램 저장 키 생성, 설정 저장/복원 적용
        RegistryStartupLaunchService.cs                        -> Windows 시작 시 자동 실행 레지스트리 동기화
        SettingsPersistenceException.cs                        -> 설정 저장/복구 예외 타입
        SettingsService.cs                                     -> settings.json 로드/저장, 백업, 복구 처리
        StartupRegistrationException.cs                        -> 자동 실행 등록 실패 예외 타입
        UpdateCheckResult.cs                                   -> 업데이트 확인 결과 모델
      ViewModels/
        AudioDeviceSelectionViewModel.cs                       -> 설정창의 장치 선택 행 상태
        AudioSessionViewModel.cs                               -> 프로그램 세션 행 상태, 볼륨/음소거/이름 변경 바인딩
        MainViewModel.cs                                       -> 메인 화면 데이터 로드, 자동 갱신, 저장 설정 복원, 필터 적용
        SettingsViewModel.cs                                   -> 설정창 데이터 바인딩 및 저장 로직
        VisibleDeviceViewModel.cs                              -> 메인 화면 장치 카드 상태와 장치 제어 바인딩
      Views/
        MainWindow.xaml                                        -> 메인 화면 레이아웃
        MainWindow.xaml.cs                                     -> 트레이 메뉴, 새로고침, 업데이트 안내, 창 이벤트 브리지
        RenameProgramWindow.xaml                               -> 사용자 지정 이름 입력 팝업 레이아웃
        RenameProgramWindow.xaml.cs                            -> 이름 변경 저장 / 자동 이름 복원 처리
        SettingsWindow.xaml                                    -> 설정창 레이아웃
        SettingsWindow.xaml.cs                                 -> 설정 저장 / 취소 / 닫기 처리
    DesktopAudioController.Updater/
      DesktopAudioController.Updater.csproj                    -> portable zip 덮어쓰기 updater 프로젝트
      Program.cs                                               -> 본 앱 종료 대기, zip 압축 해제, 파일 교체, 재실행 처리
  tests/
    DesktopAudioController.Tests/
      DesktopAudioController.Tests.csproj                      -> xUnit 테스트 프로젝트
      GitHubReleaseUpdateCheckServiceTests.cs                  -> 릴리즈 파싱, 버전 비교, zip 자산 선택 테스트
      ProgramAudioPreferenceStoreTests.cs                      -> 프로그램 설정 저장 키, 저장/복원 로직 테스트
      SettingsServiceTests.cs                                  -> 설정 round-trip, 기본값, 손상 복구 테스트
```

생성 산출물:

```text
artifacts/release/                                             -> publish 스크립트 실행 시 생성되는 배포 산출물 폴더
```

## 빌드

Windows 또는 Windows 타겟팅이 가능한 환경에서 실행합니다.

```bash
dotnet restore src/DesktopAudioController/DesktopAudioController.csproj -p:EnableWindowsTargeting=true
dotnet build src/DesktopAudioController/DesktopAudioController.csproj -p:EnableWindowsTargeting=true
```

최근 기준 빌드 검증:

```text
Build succeeded.
0 Warning(s)
0 Error(s)
```

## 배포

win-x64 self-contained single-file zip 생성 스크립트:

```bash
bash scripts/publish-win-x64.sh v0.13.15-local
```

스크립트가 수행하는 작업:

```text
1. 본 앱 / updater RID restore
2. 본 앱 / updater Release publish
3. updater exe를 본 앱 publish 폴더에 포함
4. zip 패키지 생성
5. sha256 체크섬 생성
```

산출물 경로:

```text
artifacts/release/win-x64/<version>/publish/
artifacts/release/packages/DesktopAudioController-<version>-win-x64.zip
artifacts/release/packages/DesktopAudioController-<version>-win-x64.zip.sha256
```

zip 내부 기준:

```text
DesktopAudioController/
  DesktopAudioController.exe
  DesktopAudioController.Updater.exe
```

업데이트 방식:

```text
1. 앱의 업데이트 버튼 클릭
2. 새 zip 자동 다운로드 및 sha256 검증
3. updater 프로세스가 본 앱 종료를 기다린 뒤 기존 실행 폴더에 파일 덮어쓰기
4. 새 버전 앱 재실행 시도
5. settings.json / logs 는 %LocalAppData%\DesktopAudioController 경로에 유지
```

## 문서 / 릴리즈

- [설계 문서](docs/design.md)
- [v1.0.0 출고 기준](docs/v1-release-criteria.md)
- [회귀 검증 체크리스트](docs/regression-checklist.md)
- [릴리즈 노트 템플릿](docs/release-notes-template.md)
- [GitHub Releases](https://github.com/TailFox-Forge/DesktopAudioController/releases)

## 라이선스

```text
MIT License
```

자세한 내용은 [LICENSE](LICENSE)를 따릅니다.

## 현재 알려진 제한

```text
- 일부 UWP/Store 앱 세션은 @{Package?...} 형태 리소스 URI를 사용해 현재 아이콘이 비표시될 수 있습니다.
- 이 경로는 별도 Shell API 처리 범위로 분리되어 있습니다.
```

## 업데이트 내역

<details open>
<summary><strong>v0.13.15 / 실행 로그 빌드 식별자 추가</strong></summary>

```text
- 앱 시작 로그에 현재 앱 버전과 빌드 sourceRevision 출력
- 릴리즈 publish 시 현재 git hash를 어셈블리 메타데이터로 주입
- 로컬 빌드처럼 hash를 알 수 없는 경우 sourceRevision=unknown으로 표시
```

</details>

<details>
<summary><strong>v0.13.14 / 중첩 zip 구조 및 업데이트 확인 간격 조정</strong></summary>

```text
- 릴리즈 zip 내부 구조를 DesktopAudioController/ 하위 폴더형으로 변경
- 수동 압축 해제 시 DesktopAudioController 폴더째 기존 위치로 옮길 수 있게 정리
- "업데이트 확인" 버튼 연타 제한을 30초에서 5초로 완화
```

</details>

<details>
<summary><strong>v0.13.13 / 실행 중 업데이트 재확인</strong></summary>

```text
- 상단에 "업데이트 확인" 버튼 추가
- 앱을 재시작하지 않아도 GitHub 릴리즈를 수동으로 다시 확인 가능
- 수동 확인 연타를 막기 위해 30초 throttle 적용
- 앱이 켜져 있는 동안 6시간 간격으로 백그라운드 업데이트 재확인
```

</details>

<details>
<summary><strong>v0.13.12 / updater 창 숨김 및 중첩 패키지 대응 준비</strong></summary>

```text
- DesktopAudioController.Updater.exe를 콘솔 앱에서 창 없는 WinExe로 변경
- 업데이트 적용 중 잠깐 나타나던 CMD 창 제거
- updater가 기존 루트형 zip과 DesktopAudioController/ 하위 폴더형 zip을 모두 처리하도록 보강
- v0.13.11 자동 업데이트 호환을 위해 이번 릴리즈 zip 구조는 기존 루트형 유지
```

</details>

<details>
<summary><strong>v0.13.11 / 자동 업데이트 검증 릴리즈</strong></summary>

```text
- v0.13.10 자동 업데이트 기능의 실사용 검증을 위한 후속 릴리즈
- 앱/업데이터 버전만 v0.13.11로 상향
- 릴리즈 zip에 DesktopAudioController.Updater.exe 포함 유지
```

</details>

<details>
<summary><strong>v0.13.10 / portable zip 자동 업데이트</strong></summary>

```text
- 업데이트 버튼 클릭 시 최신 zip과 sha256 파일을 자동 다운로드하도록 변경
- 다운로드한 zip의 sha256을 검증한 뒤 별도 updater 프로세스로 기존 실행 폴더에 덮어쓰기
- 실행 중인 본 앱이 자기 exe를 직접 덮어쓰지 않도록 DesktopAudioController.Updater.exe 추가
- 업데이트 적용 후 새 버전 앱 재실행 시도
- 실패 시 수동 다운로드 페이지로 이동할 수 있는 fallback 유지
- 릴리즈 zip에 DesktopAudioController.Updater.exe 포함
```

</details>

<details>
<summary><strong>v0.13.9 / 오디오 상태 이벤트 장치 재열거 억제</strong></summary>

```text
- 단순 오디오 State 이벤트에서 장치 전체 worker probe를 실행하지 않도록 변경
- 프로그램별 볼륨 패널이 열린 경우에만 State 이벤트에서 세션 목록을 부분 갱신
- Topology 이벤트는 실제 장치 구성 변경으로 보고 기존처럼 전체 장치 재열거 유지
```

</details>

<details>
<summary><strong>v0.13.8 / 프로그램 세션 자동 새로고침 및 종료 감지 보강</strong></summary>

```text
- 프로그램별 볼륨 패널을 열 때 즉시 세션 목록을 새로고침
- 프로그램별 볼륨 패널이 열린 동안에만 세션 목록을 주기 갱신
- 세션 자동 갱신에서는 장치 전체 재열거를 수행하지 않도록 제한
- 기본 장치의 프로그램별 볼륨 패널이 자동으로 펼쳐지지 않도록 변경
- Windows 종료/로그오프 시 정상 종료 상태를 먼저 기록해 다음 부팅의 비정상 종료 오탐 완화
```

</details>

<details>
<summary><strong>v0.13.7 / 부팅 직후 오디오 열거 워커 분리</strong></summary>

```text
- 부팅 직후 Core Audio 장치 열거를 별도 워커 프로세스로 분리
- 최근 정상 장치 스냅샷으로 UI를 즉시 표시하고 백그라운드에서 장치 목록을 갱신
- 앱 전체 자동 재시작 없이 워커 timeout/kill 후 같은 프로세스에서 재시도하도록 변경
- Core Audio 열거 단계별 진단 로그와 probe 워커 경로 로그 마스킹 보강
```

</details>

<details>
<summary><strong>v0.13.6 / 설정창 로딩 타임아웃 핫픽스</strong></summary>

```text
- 상태 부분 새로고침이 장치 열거를 점유 중일 때 설정창 로딩도 함께 막히던 경합 완화
- 장치 열거가 이미 진행 중이면 최근 정상 장치 스냅샷을 재사용하도록 변경
- 설정창과 상태 새로고침이 같은 Core Audio 재열거를 중복으로 시작하지 않도록 보강
```

</details>

<details>
<summary><strong>v0.13.5 / 트레이 종료 후 프로세스 잔류 핫픽스</strong></summary>

```text
- 트레이 아이콘 종료 후 프로세스가 남는 종료 hang 경로 수정
- NativeAudioNotificationService dispose 중 오디오 콜백 lock 대기 타임아웃 추가
- 종료 중 새 notification/state callback 처리를 중단해 OnExit 정리 완료 보장 강화
```

</details>

<details>
<summary><strong>v0.13.4 / 부팅 직후 제한 모드 자동 복구 및 설정창 높이 조정</strong></summary>

```text
- 부팅 직후 초기 장치 로드가 지연되면 15초 뒤 자동 재시작 복구 수행
- 자동 재시작 전에 상단 경고 문구와 트레이 안내 표시
- 제한 모드 수동 새로고침 시 앱 재기동 기반 복구로 전환
- 설정창 높이를 화면 작업 영역 안으로 제한해 하단 버튼 가림 방지
```

</details>

<details>
<summary><strong>v0.13.3 / 설정창 및 메인 화면 정렬 보정</strong></summary>

```text
- v0.13.3 릴리즈 게시 완료
- 설정창 장치 목록의 "연결됨" 상태 문구 우측 정렬
- 설정창 자동 실행 안내 문구 간격을 다른 옵션과 동일하게 정렬
- 메인 화면 장치 카드의 "기본 장치" 상태와 버튼 영역 우측 정렬
```

</details>

<details>
<summary><strong>v0.13.2 / 자동실행 초기 로드 타임아웃 대응</strong></summary>

```text
- v0.13.2 릴리즈 준비
- 재부팅 후 자동실행에서 초기 장치 로드가 멈춰 UI 없이 프로세스만 남는 문제 대응
- 시작 시 초기 장치 로드를 비동기 + 타임아웃으로 제한
- 타임아웃 시 제한 모드로 전환해 창/트레이 생성 경로 유지
- 타임아웃된 백그라운드 초기 로드가 이후 UI를 덮어쓰지 않도록 무효화 처리
```

</details>

<details>
<summary><strong>v0.13.1 / 단일 인스턴스 및 재실행 위치 원복 핫픽스</strong></summary>

```text
- v0.13.1 릴리즈 게시 완료
- 단일 인스턴스 가드 추가
- 재실행 시 기존 인스턴스 활성화 요청 반영
- 재실행 시 최초 위치(주 모니터 우상단) 원복 반영
- GitHub 이슈 본문 로그 경로 마스킹 반영
```

</details>

<details>
<summary><strong>v0.13.0 / 안정성 개선 + 비정상 종료 감지 1차</strong></summary>

```text
- v0.13.0 릴리즈 게시 완료
- settings.json atomic save 반영
- 현재 로그 파일 5MB 롤오버 반영
- 시작 실패 시 degraded mode 진입 반영
- 트레이 아이콘을 커스텀 아이콘으로 통일
- 업데이트/외부 열기 실패 가시화 보강
- 이전 실행 비정상 종료 감지 및 다음 실행 안내 팝업 1차 반영
```

</details>

<details>
<summary><strong>v0.12.2 / 아이콘 경로 보정</strong></summary>

```text
- v0.12.2 릴리즈 게시 완료
- WPF 창 아이콘 경로를 pack URI로 보정
- 커스텀 앱 아이콘이 포함된 게시용 EXE 재배포
```

</details>

<details>
<summary><strong>v0.12.1 / Phase 12 유지보수</strong></summary>

```text
- v0.12.1 릴리즈 게시 완료
- 실행 파일 / 창 아이콘 커스텀 에셋 추가
- 세션 행 `음소거` 체크박스/텍스트 수직 정렬 보정
- 설정창 크기 정책을 메인 UI와 동일 기준으로 정리
- 앱 시작 시 로그 7일 / 30개 / 100MB 자동 정리 반영
```

</details>

<details>
<summary><strong>v0.12.0 / Phase 12 시작</strong></summary>

```text
- 저장소 Public 전환 완료
- win-x64 self-contained single-file exe zip 배포 기준 확정
- README 구조/배포/환경/사양 정보 최신화
- v0.12.0 릴리즈 게시 완료
- 설정창 `로그 폴더 열기` 버튼 추가
- 공개 첨부용 로그 민감정보 요약 처리
- 앱 시작 시 로그 7일 / 30개 / 100MB 자동 정리 추가
- P12-1 배포 형식 단일화 기준 반영 완료
- P12-2 Public 전환 완료
- P12-3 로그 첨부 편의 및 공개 로그 간소화 반영 완료
- P12-4 v0.12.0 테스트용 릴리즈 게시 완료
```

</details>

<details>
<summary><strong>Phase 11 마감</strong></summary>

```text
- stable only 업데이트 채널 정책 정리
- v1.0.0 출고 기준 문서화
- 회귀 검증 체크리스트 문서화
- non-UWP 프로그램 아이콘 로딩 개선
- 보호 프로세스 세션 오판 제거
- 세션 이름 자동 개선 및 사용자 지정 이름 저장/복원
- Inactive 상태 표시 및 "재생 중인 앱만 보기" 옵션 추가
- X 버튼 종료 hang 대응 및 OnExit dispose 단계 로그 보강
- P11-1 릴리즈 채널 정책 1차 구현 완료
- P11-2 v1.0.0 출고 기준 정의 완료
- P11-3 Public 전환 전 배포/접근 조건 정리 완료
- P11-4 회귀 검증 체크리스트 문서화 완료
```

</details>

<details>
<summary><strong>v0.10.2</strong></summary>

```text
- prerelease opt-in 설정 추가
- GitHub Releases 기반 새 버전 안내 개선
- zip 덮어쓰기 업데이트 흐름 정리
- Public 전환 전 배포/접근 조건 점검
```

</details>

<details>
<summary><strong>v0.10.1</strong></summary>

```text
- 오디오 변경 새로고침 큐 안정화
- settings 저장 churn 감소
- 업데이트 / 설정 / 빈 상태 UX 정리
- P10-1 업데이트 체계 고도화 완료
- P10-2 장시간 안정화 1차 완료
- P10-3 UX 마감 완료
```

</details>

<details>
<summary><strong>v0.8.3</strong></summary>

```text
- 트레이 종료가 메인 창 Close 경로를 타도록 정리
- 종료 직전 pending 설정 flush 경로 반영
- LICENSE 반영 및 Public 전환 준비
- 자동 테스트 추가
- portable zip 배포 기준 확정
```

</details>
