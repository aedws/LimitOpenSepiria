# 소원 분수 제한 해제 (LimitOpenSepiria)

[세피리아(Sephiria)](https://store.steampowered.com/app/2436940) Steam 창작마당용 DLL 모드입니다.
[공식 모드 제작 가이드](https://github.com/team-horay/sephiria_modding)의 패키지 형식(`metadata.json` + `dll` 모듈)을 따릅니다.

## 기능

게임 옵션 화면의 모드 페이지에 토글 4개가 추가됩니다. 토글은 재시작 없이, 다음에 소원 분수를 열 때 바로 반영됩니다.

| 토글 | 기본값 | 동작 |
|---|---|---|
| 소원 분수 용량 제한 해제 | 켜짐 | 아이템이 차지하는 용량(희귀도별 3~6칸)을 0으로 만들어 용량 계산을 무효화합니다. 선택 화면과 런 시작 시 지급 검사에 함께 적용됩니다. |
| 소원 분수에서 모든 아이템 선택 | 켜짐 | 아래 "풀리는 제한"의 아티팩트를 모두 선택할 수 있게 합니다. |
| └ 아티팩트 외 종류도 포함 | 꺼짐 | 포션·석판·음식 등 아티팩트가 아닌 아이템까지 목록에 넣습니다. |
| └ 숨김·비활성·테스트용 아이템도 포함 | 꺼짐 | 게임이 숨겨 둔 내부용 아이템까지 목록에 넣습니다. |

콘텐츠를 새로 추가하지 않고, 기존 소원 분수의 제한만 풉니다.

### 풀리는 제한

원래 소원 분수는 아래 아이템을 목록에서 빼거나 클릭을 막습니다. "모든 아이템 선택"을 켜면 전부 선택할 수 있습니다.

- 아직 해금하지 않은 아티팩트
- 툴팁에 "소원 분수에서 선택할 수 없음"이 붙은 아티팩트
- 보상으로 나오지 않는 아티팩트, 듀얼 아티팩트
- 이터널 희귀도 아티팩트
- 현재 무기와 맞지 않는 무기 전용 아티팩트

### 다른 모드와의 공존

- 아이템 목록은 분수를 열 때마다 게임의 `ItemDatabase`에서 읽습니다. **다른 모드가 추가한 아이템도 자동으로 포함**됩니다.
- 목록 패치는 가장 먼저 실행되므로, 다른 모드가 거는 필터(예: ModMaker의 "소원 분수 금지" 아이템)는 그대로 존중됩니다.
- 용량 패치는 가장 마지막에 결과만 덮어쓰므로, 다른 모드의 희귀도별 용량 변경(예: ModMaker 커스텀 희귀도)과 충돌하지 않습니다. 토글을 끄면 그 모드의 값이 그대로 쓰입니다.
- 세이브 데이터의 원본 목록은 수정하지 않고 사본에 덧붙입니다.

## 설치 (로컬 테스트)

1. `package/limit-open-sepiria/` 폴더를 통째로 `<게임 설치 폴더>/WorkshopLocal/` 아래에 복사합니다.
2. 게임을 실행하고 타이틀의 **스팀 창작마당** 패널 → "내가 만든 모드"에 보이는지 확인합니다.
3. 게임을 시작해 소원 분수를 열어 봅니다.

문제가 있으면 `%USERPROFILE%\AppData\LocalLow\TEAMHORAY\Sephiria\Player.log`에서 `[LimitOpenSepiria]`를 검색하세요.
패치가 걸린 대상과, 분수를 열 때 목록에 추가된 아이템 수가 기록됩니다.

## 빌드

.NET SDK 8 이상이 필요합니다. 결과물은 `package/limit-open-sepiria/bin/LimitOpenSepiria.dll`에 생깁니다.

```bash
dotnet build src/LimitOpenSepiria.csproj -c Release -p:GameDir="C:\Program Files (x86)\Steam\steamapps\common\Sephiria"
```

`GameDir`을 주면 실제 게임 DLL(`Sephiria_Data/Managed`)을 참조해 빌드합니다(권장).
생략하면 `stubs/`의 컴파일 전용 스텁(공식 API 레퍼런스에 공개된 시그니처만 옮긴 것, 게임 코드 없음)으로 빌드합니다.

```bash
dotnet build src/LimitOpenSepiria.csproj -c Release
```

## 창작마당 업로드

1. 올리기 전에 `metadata.json`의 `modId`·`author`를 확정합니다. **`modId`는 게시 후 바꾸면 안 됩니다.** 바꾼다면 `src/Entry.cs`의 `ModId`도 함께 바꾸세요.
2. 게임 타이틀의 **스팀 창작마당** 패널에서 모드를 선택하고 **창작마당에 올리기**를 누릅니다. DLL 모드이므로 자동으로 코드 검수에 접수됩니다.
3. 업로드 후 폴더에 생기는 `uploadticket.json`은 지우지 말고 이 저장소의 `package/limit-open-sepiria/`에 함께 커밋해 두세요.
4. 업데이트할 때마다 `metadata.json`의 `version`을 올립니다.

멀티플레이 설치 범위는 `everyone`(방 참가자 전원 필요)으로 선언했습니다.

## 구조

```
package/limit-open-sepiria/   창작마당 패키지 (WorkshopLocal에 복사하는 폴더)
  metadata.json
  thumbnail.png
  bin/LimitOpenSepiria.dll
src/                          모드 소스
  Entry.cs                    진입점 (WorkshopModBase)
  Settings.cs                 옵션 화면 토글
  FountainPatches.cs          Harmony 패치
  GameReflection.cs           게임 내부 타입 접근(리플렉션)
stubs/                        컴파일 전용 스텁 (배포물에 포함되지 않음)
```

## 알아둘 점

- 소원 분수 자체가 열려 있어야 합니다(분수 용량이 1 이상). 이 모드는 분수를 해금해 주지 않습니다.
- **프리셋**: 프리셋을 불러올 때 게임이 "해금하지 않은 아티팩트"를 선택에서 빼고 오류로 표시합니다. 미해금 아이템은 분수에서 직접 고른 상태로 런을 시작하세요. 프리셋 화면에서는 미해금 아이템이 "?" 아이콘으로 보입니다.
- 인벤토리 칸 수는 그대로입니다. 인벤토리에 들어가지 않을 만큼 고르면 초과분이 어떻게 처리되는지는 확인하지 못했습니다.
- 용량 제한 해제를 끈 상태에서 이터널 아티팩트의 용량은 게임 기본 계산대로 3칸입니다.
- 멀티플레이에서는 지급 검사가 호스트에서 돌기 때문에 호스트에도 이 모드가 필요합니다(`everyone`으로 선언).

## 동작 원리

공식 `HorayModAPI`에는 소원 분수에 개입하는 이벤트가 없어, 가이드가 허용하는 Harmony 패치를 씁니다. 대상은 `UI_DimensionPocketPanel`의 네 메서드입니다.

| 대상 | 패치 | 하는 일 |
|---|---|---|
| `GetCapacity` | Postfix | 결과를 0으로. UI와 서버 지급(`PlayerSpawner.AddDimensionPocketItemsOnServer`)이 모두 이 함수로 용량을 계산합니다. |
| `Open(…, cachedDimensionPocketItem, …)` | Prefix | 넘어온 선택 목록의 사본에 `ItemDatabase`의 나머지 아이템을 덧붙입니다. |
| `OnOpened` | Prefix/Postfix/Finalizer | 목록을 구성하는 동안만 아이템의 걸러내기 조건을 풀었다가 되돌리고, 무기 불일치 잠금을 풉니다. |
| `HandleItemClick` | Prefix/Finalizer | 클릭을 처리하는 동안만 같은 조건을 풀었다가 되돌립니다. |

아이템 데이터는 메서드가 끝나면 원래 값으로 복원되므로 분수 밖(상점, 보상, 도감)에는 영향이 없습니다.
게임 내부 타입은 이름으로 찾아 접근하므로, 게임 업데이트로 대상이 바뀌면 해당 기능만 꺼지고 로그에 경고가 남습니다.
게임 1.1.x의 `Assembly-CSharp.dll`을 기준으로 작성했습니다.
