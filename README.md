# DesktopAudioController

Windows에서 사용자가 고른 출력 장치만 표시하고, 장치 마스터 볼륨과 해당 장치에서 실제로 소리를 내는 프로그램별 세션 볼륨을 빠르게 제어하는 WPF 데스크톱 유틸리티입니다.

## 현재 상태

```text
기준 버전: v0.12.0
개발 단계: Phase 12 진행 중
Public 전환: 보류
런타임: .NET 8 / WPF / Windows 전용
배포 형태: win-x64 self-contained single-file exe zip
릴리즈 페이지: https://github.com/TailFox-Forge/DesktopAudioController/releases
최근 확인 사항:
- Phase 11 범위 종료 및 Phase 12 시작
- win-x64 publish 산출물을 single-file exe zip 기준으로 고정
- 프로그램 이름 자동 개선 / 사용자 지정 이름 저장 복원 반영
- Inactive 세션 상태 표기 및 "재생 중인 앱만 보기" 옵션 반영
- X 버튼 종료 시 OnExit dispose 단계 로그 및 종료 hang 대응 반영
- 업데이트 방식은 zip 다운로드 후 압축 해제 / 덮어쓰기 기준 유지
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

로그에서 바로 확인할 수 있는 대표 항목:

```text
- 설정 로드 / 저장 경로
- 프로그램 설정 저장 완료 count=...
- 저장된 프로그램 설정 복원
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

## 프로젝트 구조

```text
DesktopAudioController/
  docs/
    design.md
    release-notes-template.md
  scripts/
    publish-win-x64.sh
  src/
    DesktopAudioController/
      Models/
      Services/
      ViewModels/
      Views/
      App.xaml.cs
      DesktopAudioController.csproj
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
bash scripts/publish-win-x64.sh v0.12.0-local
```

스크립트가 수행하는 작업:

```text
1. RID restore
2. Release publish
3. single-file exe 1개 산출물 생성
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
DesktopAudioController.exe
```

업데이트 방식:

```text
1. 새 zip 다운로드
2. 압축 해제
3. 기존 실행 폴더에 파일 덮어쓰기
4. settings.json / logs 는 %LocalAppData%\DesktopAudioController 경로에 유지
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

## 진행 현황

### Phase 9 완료 항목

```text
1. LICENSE 반영 및 Public 전환 준비
2. 자동 테스트 추가
3. portable zip 배포 기준 확정
```

### Phase 10 진행 현황

```text
1. P10-1 업데이트 체계 고도화 완료
2. P10-2 장시간 안정화 1차 완료
3. P10-3 UX 마감 완료
```

### Phase 11 완료 현황

```text
1. P11-1 릴리즈 채널 정책 1차 구현 완료
2. P11-2 v1.0.0 출고 기준 정의 완료
3. P11-3 Public 전환 전 배포/접근 조건 정리 완료
4. P11-4 회귀 검증 체크리스트 문서화 완료
5. 세션 이름 개선 / Option C / 종료 hang 대응 / single-file exe zip 기준 정리 후 Phase 11 종료
```

### Phase 12 진행 현황

```text
1. P12-1 배포 형식 단일화 기준 반영 완료
```
