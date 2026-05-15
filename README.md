# DesktopAudioController

Windows에서 **사용자가 고른 출력 장치만 표시**하고,
그 장치의 **마스터 볼륨 / 음소거 / 기본 출력 장치 전환**과
**해당 장치에서 실제로 소리를 내는 프로그램별 세션 볼륨 / 음소거**까지 빠르게 제어하는 WPF 데스크톱 유틸리티입니다.

## 현재 상태

```text
단계: Phase 4 완료
상태: 핵심 기능 구현 및 빌드 검증 완료
빌드 검증: dotnet build 0 Warning / 0 Error
배포 형태: 임시 zip 배포 우선, installer는 추후
```

## 현재 구현 범위

```text
1. 최초 실행 시 설정창 표시
2. 출력 장치 목록 조회
3. 선택한 장치만 메인 화면에 표시
4. 장치 마스터 볼륨 조절
5. 장치 음소거
6. 기본 출력 장치 변경
7. 장치 확장 시 앱 세션 목록 표시
8. 앱 세션별 볼륨 조절
9. 앱 세션별 음소거
10. 장치 / 볼륨 / 세션 이벤트 기반 자동 갱신
11. 세션 아이콘 표시
12. 트레이 최소화 / 복원 / 종료
13. PerMonitorV2 DPI 대응
14. 설정 저장 / 재로딩
15. 기본 장치 변경 실패 시 원인 분류 / 재시도 / 설정 fallback
16. 시스템 사운드 세션 표시 옵션
17. 트레이에서 기본 장치 빠른 전환 / 장치 음소거 토글
18. 상태 변경 시 부분 갱신, 토폴로지 변경 시 전체 갱신
```

## 핵심 UX

```text
초기 화면
- 사용자가 선택한 출력 장치만 카드 형태로 표시
- 각 장치의 마스터 볼륨과 음소거, 기본 장치 변경 제공

장치 확장
- 해당 장치에서 실제로 출력 중인 프로그램 목록 표시
- 프로그램별 볼륨과 음소거를 바로 조절

설정창
- 표시할 장치 선택
- 연결된 장치만 표시 여부
- 시스템 사운드 세션 표시 여부
- 트레이 최소화 여부
- 시작 시 최소화 여부

트레이 메뉴
- 창 열기
- 설정 열기
- 새로고침
- 기본 장치 빠른 전환
- 장치 음소거 토글
- 종료
```

## 기술 스택

```text
- 언어: C#
- 런타임: .NET 8
- UI: WPF
- 오디오 장치/세션 제어: Windows Core Audio API + NAudio
- 기본 장치 변경: PolicyConfig COM interop
- 트레이: Windows Forms NotifyIcon
```

## 현재 구조

```text
DesktopAudioController/
  docs/
    design.md
  src/
    DesktopAudioController/
      Models/
      Services/
      ViewModels/
      Views/
      app.manifest
```

## 빌드

Windows 또는 Windows 타겟팅이 가능한 환경에서 아래 순서로 검증합니다.

```bash
dotnet restore src/DesktopAudioController/DesktopAudioController.csproj -p:EnableWindowsTargeting=true
dotnet build src/DesktopAudioController/DesktopAudioController.csproj -p:EnableWindowsTargeting=true
```

현재 검증 기준:

```text
Build succeeded.
0 Warning(s)
0 Error(s)
```

## 임시 배포 방식

현 단계에서는 installer 대신 **publish 결과물 zip 배포**를 사용합니다.

권장 이유:

```text
- 설치 제거 로직을 아직 강제하지 않아도 됨
- 내부 테스트 배포 속도가 빠름
- 문제 발생 시 교체와 회수가 단순함
```

예시:

```bash
dotnet publish src/DesktopAudioController/DesktopAudioController.csproj \
  -c Release \
  -r win-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:EnableWindowsTargeting=true
```

그다음 publish 폴더 전체를 zip으로 묶어 배포합니다.

## 문서

- [설계 문서](docs/design.md)

## Phase 4 계획

```text
Phase 4-1 기본 장치 변경 실패 UX 개선 완료
Phase 4-2 세션 이벤트 반영 정교화 완료
Phase 4-3 System Sounds 필터 옵션 완료
Phase 4-4 트레이 메뉴 확장 완료
```

진행 원칙:

```text
- 항목별 커밋 분리
- 각 항목마다 빌드 검증
- 소스 주석 동시 반영
```

## 남은 고도화 후보

### 1. 세션 아이콘/메타데이터 캐싱 고도화

배경:

```text
현재는 실행 파일 경로 기반으로 아이콘을 캐싱합니다.
```

추가 목표:

```text
- 프로세스 종료/재시작 시 캐시 정리 정책 추가
- 아이콘 조회 실패 시 대체 아이콘 정책 정리
- 아이콘 조회 비동기화 여부 검토
```

기대 효과:

```text
- 대량 세션 환경에서 UI 부하 감소
- 아이콘 일관성 향상
```

### 2. 배포 고도화

배경:

```text
현재는 zip 임시 배포를 전제로 합니다.
```

추가 목표:

```text
- Release publish 자동화
- 릴리즈 노트 템플릿화
- 추후 installer / auto-update 검토
```

기대 효과:

```text
- 내부 배포 반복 작업 감소
- 버전별 회수/비교/복구가 쉬워짐
```
