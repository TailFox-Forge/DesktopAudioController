# DesktopAudioController 릴리즈 노트 템플릿

## 버전 정보

```text
버전:
태그:
배포 일시:
대상 런타임: win-x64 self-contained / single-file
기준 커밋:
```

## 이번 배포에 포함된 핵심 변경

```text
1.
2.
3.
```

## 사용 방법

```text
1. zip 파일 압축 해제
2. DesktopAudioController.exe 실행
3. 업데이트 시 새 zip 내용을 기존 실행 폴더에 덮어쓰기
4. 최초 실행 시 설정창에서 표시할 장치 선택
```

## 검증 기준

```text
dotnet build 0 Warning / 0 Error
publish 스크립트 실행 성공
zip / sha256 생성 확인
```

## 알려진 제한 사항

```text
1. installer는 제공하지 않음
2. 자동 업데이트 기능은 아직 없음
3. 관리자 권한 또는 보안 정책에 따라 기본 출력 장치 변경이 제한될 수 있음
```
