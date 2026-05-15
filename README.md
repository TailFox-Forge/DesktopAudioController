# DesktopAudioController

Windows에서 **사용자가 고른 출력 장치만 표시**하고,
그 장치의 **마스터 볼륨 / 음소거 / 기본 출력 장치 전환**과
**해당 장치에서 실제로 소리를 내는 프로그램별 세션 볼륨 / 음소거**까지 빠르게 제어하는 WPF 데스크톱 유틸리티입니다.

## 현재 상태

```text
단계: Phase 6 완료 + Phase 7-1 배포 자동화 완료
상태: 핵심 기능 구현, 안정화, 배포 자동화, 빌드 검증 완료
빌드 검증: dotnet build 0 Warning / 0 Error
배포 형태: 임시 zip 배포 우선, installer는 추후
최신 GitHub 프리릴리즈: v0.7.3-preview2
최신 로컬 배포 검증: v0.7.3-preview2
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
15. Windows 시작 시 자동 실행 옵션
16. 기본 장치 변경 실패 시 원인 분류 / 재시도 / 설정 fallback
17. 시스템 사운드 세션 표시 옵션
18. 트레이에서 기본 장치 빠른 전환 / 장치 음소거 토글
19. 상태 변경 시 부분 갱신, 토폴로지 변경 시 전체 갱신
20. 아이콘 실패 캐시 / 만료 정리
21. PID 기준 프로세스 메타데이터 캐시
22. 세션 아이콘 비동기 로딩
23. 오디오 변경 이벤트 큐 스레드 안전화
24. 설정 저장 실패 시 사용자 안내 및 창 유지
25. 캐시 히트 시 아이콘 비동기 로딩 중복 예약 차단
26. 동일 exe 아이콘 로딩 in-flight dedupe
27. 설정 파일 손상 시 .bak 백업 및 1회 경고
28. 메인 화면 현재 앱 버전 표시
29. GitHub 릴리즈 페이지 열기 버튼
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
- Windows 시작 시 자동 실행 여부
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

배포 자동화 스크립트:

```bash
bash scripts/publish-win-x64.sh v0.7.3-preview2-local
```

스크립트가 수행하는 작업:

```text
1. Release publish
2. win-x64 self-contained / single-file 산출물 생성
3. zip 패키지 생성
4. sha256 체크섬 생성
```

생성 경로:

```text
artifacts/release/win-x64/<version>/publish/
artifacts/release/packages/DesktopAudioController-<version>-win-x64.zip
artifacts/release/packages/DesktopAudioController-<version>-win-x64.zip.sha256
```

릴리즈 노트 작성 템플릿:

- [docs/release-notes-template.md](docs/release-notes-template.md)

현재 게시된 프리릴리즈:

- [v0.7.3-preview2](https://github.com/TailFox-Forge/DesktopAudioController/releases/tag/v0.7.3-preview2)
- [v0.6.0-preview2](https://github.com/TailFox-Forge/DesktopAudioController/releases/tag/v0.6.0-preview2)
- [v0.3.0-preview1](https://github.com/TailFox-Forge/DesktopAudioController/releases/tag/v0.3.0-preview1)

## 문서

- [설계 문서](docs/design.md)

## Phase 4 완료 내역

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

## Phase 5 완료 내역

```text
Phase 5-1 실패 캐시 및 만료 정리 완료
Phase 5-2 프로세스 메타데이터 캐시 분리 완료
Phase 5-3 아이콘 비동기 로딩 완료
```

반영 효과:

```text
- 캐시가 무한정 쌓이지 않게 정리 정책 추가
- 반복 실패 경로에 대한 재시도 간격 제어
- 대량 세션 환경에서 아이콘/메타데이터 조회 부하 감소
- UI 첫 렌더링 시 아이콘 조회로 인한 체감 지연 감소
```

## Phase 6 완료 내역

```text
Phase 6-1 오디오 변경 이벤트 큐 스레드 안전화 완료
Phase 6-2 설정 저장 실패 UX 처리 완료
Phase 6-3 아이콘 비동기 로딩 중복 예약 제거 완료
Phase 6-4 동일 exe 아이콘 로딩 in-flight dedupe 완료
Phase 6-5 설정 파일 손상 백업 및 1회 경고 완료
```

반영 효과:

```text
- 오디오 이벤트가 동시에 들어와도 중복 Dispatcher 예약이 줄어듦
- 설정 저장 실패 시 원인과 경로를 사용자에게 명확히 안내
- 캐시 히트 상황에서 불필요한 아이콘 로딩 작업 제거
- 같은 exe 아이콘을 동시에 여러 번 읽는 낭비 제거
- 손상된 설정 파일을 조용히 덮어쓰지 않고 복구 사실을 사용자에게 알림
```

## Phase 7 진행 내역

```text
Phase 7-1 배포 자동화 완료
Phase 7-3 1단계 앱 버전 표시와 릴리즈 버튼 완료
Phase 7-3 2단계 백그라운드 최신 버전 감지와 안내 완료
```

반영 내용:

```text
- publish / zip / sha256 생성 스크립트 추가
- 버전별 배포 산출물 경로 표준화
- 릴리즈 노트 템플릿 추가
- 현재 빌드 버전을 메인 화면에 표시
- GitHub 릴리즈 페이지를 여는 버튼 추가
- 앱 시작 후 GitHub 릴리즈 기준 백그라운드 업데이트 확인
- 인터넷이 없거나 응답이 느릴 때도 UI가 멈추지 않도록 짧은 timeout과 예외 무시 적용
```

## 남은 고도화 후보

### Phase 7 후보

#### 7-2. 설치 체계 정리

배경:

```text
현재 배포는 self-contained single-file zip까지 정리되어 있고,
설치/제거/바로가기 생성 같은 사용자 설치 절차는 아직 없습니다.
```

추가 목표:

```text
- MSI 또는 installer 도입
- 설치 경로 / 바로가기 / 제거 절차 정리
- 내부 배포 표준 절차 수립
```

기대 효과:

```text
- 일반 사용자 배포 편의성 향상
- 운영/지원 절차 단순화
```

#### 7-3. 업데이트 체계 검토

배경:

```text
현재는 새 버전이 나와도 사용자가 zip을 다시 받아 교체해야 합니다.
```

추가 목표:

```text
- 1단계: 앱 내 버전 표시 / 릴리즈 페이지 열기 완료
- 2단계: 최신 버전 감지와 새 버전 안내 완료
- 3단계: installer 도입 시 auto-update 연계 검토
```

기대 효과:

```text
- 사용자 업데이트 부담 감소
- 버전 불일치 운영 리스크 감소
```
