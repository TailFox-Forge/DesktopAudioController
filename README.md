# DesktopAudioController

Windows에서 **사용자가 선택한 출력 장치만 표시**하고,  
그 장치들에 대해서만 빠르게 볼륨 조절 / 음소거 / 기본 출력 장치 전환을 할 수 있게 만드는 데스크톱 유틸리티입니다.

## 현재 상태

```text
단계: 설계 완료, 구현 시작 전
목표: MVP 설계 문서 확정 후 WPF 프로젝트 스캐폴딩
```

## 핵심 문제

기존 Windows 오디오 UI나 EarTrumpet 계열 도구는 모든 장치를 한 번에 많이 보여주는 경우가 있어,
실제로 자주 쓰는 장치 몇 개만 관리하고 싶은 사용자에게는 가독성이 떨어질 수 있습니다.

이 프로젝트는 아래를 해결하는 데 초점을 둡니다.

- **원하는 출력 장치만 UI에 표시**
- **초기 실행 시 설정창에서 표시 장치 선택**
- **설정창에서 나중에 언제든 변경**
- **선택한 장치에 대해서만 볼륨 조절 UI 제공**
- **기본 출력 장치 전환 기능**

## 기술 선택

현재 기준 권장안:

```text
- 언어: C#
- 런타임: .NET 8
- UI: WPF
- 오디오 제어:
  - 장치 열거/볼륨: Windows Core Audio API
  - 기본 장치 전환: PolicyConfig COM interop
```

WPF를 먼저 선택한 이유:

- Windows 시스템 유틸리티 성격과 잘 맞음
- 트레이 앱, 설정창, COM/Core Audio 연동이 안정적임
- WinUI 3보다 MVP 구현 속도가 빠름

## 배포 전략

현재 기준 권장:

```text
- MVP / 내부 테스트: zip 배포
- 정식 내부 배포: installer 추가
```

주의:

- `exe 하나만` 배포할지
- `publish 폴더 전체를 zip`으로 줄지는 다릅니다.

1차는 보통 아래가 안전합니다.

```text
dotnet publish 결과물 전체 zip 배포
```

자동 실행, 바로가기, 제거, 업데이트 관리가 필요해지면
그때 installer를 붙이는 게 맞습니다.

## 문서

- [설계 문서](docs/design.md)

## 계획된 저장소 구조

```text
DesktopAudioController/
  docs/
    design.md
  src/
    DesktopAudioController/
  tests/
    DesktopAudioController.Tests/
```

## 1차 MVP 범위

```text
1. 최초 실행 시 설정창 표시
2. 출력 장치 목록 조회
3. 선택한 장치만 메인 UI에 표시
4. 장치별 볼륨/음소거
5. 기본 출력 장치 변경
6. 설정 저장/재로딩
7. 시스템 트레이 최소화
```
