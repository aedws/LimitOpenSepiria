# 소원 분수·재능·과일 꼬치 제한 해제 (LimitOpenSepiria)

[세피리아(Sephiria)](https://store.steampowered.com/app/2436940) Steam 창작마당용 DLL 모드입니다.
[공식 모드 제작 가이드](https://github.com/team-horay/sephiria_modding)의 패키지 형식(`metadata.json` + `dll` 모듈)을 따릅니다.

## 기능

### 소원 분수

소원 분수 화면의 희귀도 버튼 줄(즐겨찾기 옆)에 **용량 해제**, **모든 아이템**, **확장 +6**, **확장 +12** 버튼이 생깁니다. 누르면 바로 켜지고 꺼지며 화면이 다시 구성됩니다. 같은 설정이 게임 옵션 화면의 모드 페이지에도 있습니다.

| 설정 | 기본값 | 동작 |
|---|---|---|
| 용량 해제 | 켜짐 | 아이템이 차지하는 용량(희귀도별 3~6칸)을 0으로 만들어 용량 계산을 무효화합니다. 선택 화면과 런 시작 시 지급 검사에 함께 적용됩니다. 켜져 있는 동안 아이콘 아래 용량 표시는 등급색 원 1개로 그려지고, "주머니 용량" 표시는 숨겨집니다. |
| 모든 아이템 | 켜짐 | 아래 "풀리는 제한"의 아티팩트를 모두 선택할 수 있게 합니다. |
| └ 아티팩트 외 종류도 포함 (옵션 화면) | 꺼짐 | 포션·석판·음식 등 아티팩트가 아닌 아이템까지 목록에 넣습니다. |
| └ 숨김·비활성·테스트용 아이템도 포함 (옵션 화면) | 꺼짐 | 게임이 숨겨 둔 내부용 아이템까지 목록에 넣습니다. |

### 인벤토리 확장

소원 분수 화면의 **확장 +6**, **확장 +12** 버튼으로 인벤토리 칸을 늘립니다. 둘 중 하나만 켜지고, 켜진 버튼을 다시 누르면 꺼집니다. 기본은 꺼짐이며, 옵션 화면의 모드 페이지에서도 고를 수 있습니다.

- 버튼을 누르는 즉시 적용됩니다. 끄거나 줄이면 넘치는 아이템은 게임이 빈칸으로 옮기거나 바닥에 떨어뜨립니다.
- 레벨업·재능 등 게임이 주는 칸에 더해지는 방식입니다.

### 재능 포인트와 과일 꼬치

재능 탭과 과일 꼬치 탭의 왼쪽 위에 조절 버튼이 생깁니다. 같은 설정이 게임 옵션 화면의 모드 페이지에도 있습니다.

```
재능 탭        [-10] [-1] [재능 최대 140] [+1] [+10]
               [레벨 20 상한: 적용 중]
과일 꼬치 탭   [감소·금지 제한: 해제됨]
```

| 설정 | 기본값 | 범위 | 동작 |
|---|---|---|---|
| 재능 포인트 최대치 | 140 | 게임 기본~140 | 재능 포인트 최대치를 설정값으로 올립니다. 재능 탭의 남은 포인트에 바로 반영됩니다. 가운데 버튼은 게임 기본값과 140을 오갑니다. 최대치를 이미 쓴 포인트보다 낮추면 재능이 초기화됩니다. |
| 재능별 레벨 상한(20) 해제 | 꺼짐 | 켬/끔 | 재능 하나에 20 넘게 찍을 수 있게 합니다. 효과는 레벨에 비례해 계속 늘어납니다. 끄면 다음에 그 재능을 조정할 때 20으로 돌아옵니다. |
| 과일 꼬치 감소·금지 제한 해제 | 켜짐 | 켬/끔 | 드랍률 감소·등장 금지(마이너스 과일)를 고를 수 있는 개수 제한을 없앱니다. 마이너스 과일은 과일 꼬치 개수에 포함되지 않습니다. 드랍률 증가(플러스 과일)의 개수 제한과, 카테고리 하나에 겹칠 수 있는 횟수는 게임 그대로입니다. |

콘텐츠를 새로 추가하지 않고, 기존 제한만 풉니다.

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
  Settings.cs                 설정값, 옵션 화면 등록
  FountainPatches.cs          소원 분수 패치
  FountainUi.cs               소원 분수 화면의 모드 버튼, 용량 표시
  ProgressionPatches.cs       재능 포인트, 재능 레벨 상한, 과일 꼬치 감소·금지 제한 패치
  InventoryPatches.cs         인벤토리 확장
  ProgressionUi.cs            재능 탭·과일 꼬치 탭의 조절 버튼
  ScrollWrap.cs               넘치는 목록을 스크롤 영역으로 감싸기
  GameReflection.cs           게임 내부 타입 접근(리플렉션)
stubs/                        컴파일 전용 스텁 (배포물에 포함되지 않음)
```

## 알아둘 점

- 소원 분수 자체가 열려 있어야 합니다(분수 용량이 1 이상). 이 모드는 분수를 해금해 주지 않습니다.
- **프리셋**: 프리셋을 불러올 때 게임이 "해금하지 않은 아티팩트"를 선택에서 빼고 오류로 표시합니다. 미해금 아이템은 분수에서 직접 고른 상태로 런을 시작하세요. 프리셋 화면에서는 미해금 아이템이 "?" 아이콘으로 보입니다.
- 인벤토리 칸 수는 그대로입니다. 인벤토리에 들어가지 않을 만큼 고르면 초과분이 어떻게 처리되는지는 확인하지 못했습니다.
- 용량 제한 해제를 끈 상태에서 이터널 아티팩트의 용량은 게임 기본 계산대로 3칸입니다.
- 재능 포인트 최대치와 레벨 상한은 호스트(솔로 포함)가 정해 모든 참가자에게 적용됩니다. 참가자 화면에는 재능 탭의 조절 버튼이 보이지 않습니다.
- 감소·금지를 많이 골라 과일 꼬치 화면의 "아티팩트 등장 확률 보너스" 목록이 길어지면, 넘치는 부분은 스크롤 영역(오른쪽의 스크롤 막대, 마우스 휠)으로 넘겨 봅니다. 가운데 꼬치와 프리셋 화면의 요약에는 스크롤이 없습니다.
- 인벤토리 확장은 호스트(솔로 포함)의 설정이 모든 참가자에게 적용됩니다. 게임의 인벤토리 화면이 7줄(42칸)을 넘는 크기를 보기 좋게 배치하는지는 확인하지 못했습니다.
- 과일 꼬치 화면의 "선택 수 / 최대" 숫자는 마이너스 과일을 고를 때마다 양쪽이 함께 늘어납니다. 제한을 다시 켜면 초과분은 게임의 오류 표시대로 적용되지 않습니다.
- 멀티플레이에서는 지급·적용 검사가 호스트에서 돌기 때문에 호스트에도 이 모드가 필요합니다(`everyone`으로 선언).

## 동작 원리

공식 `HorayModAPI`에는 소원 분수에 개입하는 이벤트가 없어, 가이드가 허용하는 Harmony 패치를 씁니다. 대상은 다음과 같습니다.

| 대상 | 패치 | 하는 일 |
|---|---|---|
| `UI_DimensionPocketPanel.GetCapacity` | Postfix | 결과를 0으로. UI와 서버 지급(`PlayerSpawner.AddDimensionPocketItemsOnServer`)이 모두 이 함수로 용량을 계산합니다. |
| `UI_DimensionVaultItemIcon.SetCost` | Prefix/Finalizer | 아이콘 아래 용량 표시를 그리는 동안만 용량을 1로 돌려줘 원 1개로 그려지게 합니다. |
| `UI_DimensionPocketPanel.Open` | Prefix | 넘어온 선택 목록의 사본에 `ItemDatabase`의 나머지 아이템을 덧붙입니다. |
| `UI_DimensionPocketPanel.OnOpened` | Prefix/Postfix/Finalizer | 현재 설정에 맞는 아이템만 남기고, 목록을 구성하는 동안만 걸러내기 조건을 풀었다가 되돌립니다. 모드 버튼과 용량 표시를 맞춥니다. |
| `UI_DimensionPocketPanel.HandleItemClick` | Prefix/Finalizer | 클릭을 처리하는 동안만 같은 조건을 풀었다가 되돌립니다. |
| `PlayerAvatar.NetworkmaxPassivePoint` (setter) | Postfix | 게임이 재능 포인트 최대치를 바꿀 때마다 보너스를 다시 맞춥니다(서버). |
| `PlayerAvatar.AddPassiveStatOnServer` | Prefix | 재능 레벨 상한(`PassiveEntity.maxLevel`)을 설정에 맞춰 둡니다. 원래 값은 기억했다가 되돌립니다. |
| `UI_PassivePanel.OnOpened`, `UI_FruitSkewerPanel.OnOpened` | Postfix | 탭 안의 조절 버튼을 만들고 표시를 맞춥니다. 버튼은 게임이 모드용으로 공개한 `UIWidgetFactory`로 만듭니다. |
| `UI_FruitSkewerPanel.MaxSelectedFruitCount` | Prefix/Postfix/Finalizer | 최대 개수를 "게임 값 + 고른 마이너스 과일 수"로 돌려줍니다. |
| `UI_FruitSkewerPanel.AddFruit` | Prefix/Finalizer | 마이너스 과일을 추가하는 동안 한 칸을 더 열어 줍니다. |
| `UI_FruitSkewerPanel.RefreshCategoryButtonLockState` | Prefix/Postfix/Finalizer | 개수가 가득 차도 마이너스 버튼은 누를 수 있게 되돌립니다. |
| `DungeonManager.LoadStageAndMove`, `UI_PresetPanel.UpdateFruitSkewerInfo` + `KeywordDatabase.GetConstValue` | Prefix/Finalizer + Postfix | 런 시작 시 서버 적용과 프리셋 표시가 과일을 세는 동안만 개수 상수를 크게 돌려줘, 고른 과일이 모두 적용되게 합니다. |

아이템 데이터는 메서드가 끝나면 원래 값으로 복원되므로 분수 밖(상점, 보상, 도감)에는 영향이 없습니다.
게임 내부 타입은 이름으로 찾아 접근하므로, 게임 업데이트로 대상이 바뀌면 해당 기능만 꺼지고 로그에 경고가 남습니다.
게임 1.1.x의 `Assembly-CSharp.dll`을 기준으로 작성했습니다.

## 두 번째 모드: 서리단조 칼날 : 초승달 식 (개발 중)

`frostforged/src`와 `package/frostforged-crescent/`에 있는 별도 모드입니다. 축사의 검집에 특화된 카타나 강화 무기를 추가합니다.

```bash
dotnet build frostforged/src/FrostforgedCrescent.csproj -c Release
```

- 기본 카타나의 무기 프리팹을 복제해 강화 무기로 등록하고, 무기 효과 컴포넌트(`CrescentAddon`)를 붙입니다.
- 이 무기를 들고 있으면 축사의 검집(`Charm_AirSlash`)이 충전 여부와 관계없이 기본 공격마다 발동합니다.
- `package/frostforged-crescent/Sprites/`: `icon.png`은 무기 아이콘, `katana.png`·`katana_sheathed.png`(10x29)는 손에 든 칼과 납도 상태의 칼, `scabbard.png`는 허리 칼집(위치는 `scabbard.txt`)입니다.
- 특수 공격은 납도이며, 납도 상태에서는 이동 속도가 60% 증가하고 납도 공격이 축사의 검집과 볼루스파(조준 방향)를 발동시킵니다.
- 아티팩트 7종(서리칼날, 서리한 : II형, 악보 : 북풍, 혈석 펜던트, 혈석 십자가, 블루비틀 전용인 서리 바람의 날개깃과 일번창 : 에린다누아)과 '생명' 콤보를 추가합니다. 등록은 `ItemContent.cs`, 효과 처리는 `ItemEffects.cs`, 아이콘은 `Sprites/Items/`(생성 스크립트 `frostforged/art/items.py`)입니다.
- 먹구름 콤보 개선(`DarkCloudBoost.cs`): 먹구름이 0이어도 콤보(2)가 켜져 있으면 번개가 50% 피해로 발동하고, 이때 번개·소모 이펙트는 절반 크기로 나옵니다(이펙트 축소는 호스트 본인 화면만).
- 무기 `서리기수 : 돌격창 블루비틀`(`LanceFactory.cs`, `LanceAddon.cs`): 돌격창 기오로이드의 프리팹을 복제해 창격을 쓰고, 전용 창 그림(`Sprites/lance.png`, 생성 스크립트 `frostforged/art/lance.py`)을 씁니다. 백상의 봉의 강화 무기입니다. 창격 3·4·5타 반복은 `LanceMotion.cs`(동작 위치를 모드 폴더 `lance_motion.txt` 에 기록), 약화된 볼루스파는 `WeakSpear.cs`, 얼음 궤적은 `LanceFx.cs`, 우클릭 패링과 돌파 찌르기는 `LanceParry.cs`, 손에 든 그림 조정은 `Sprites/lance.txt` 입니다.
- 봉의 새 갈래 `만변창 : 4기사`(2단계)와 그 3단계 `서리기수 : 돌격창 블루비틀`·`화염기수`·`뇌격기수`·`종말기수`: 다섯 무기 모두 `LanceFactory.cs` 가 기오로이드의 프리팹으로 만들고, 공용 능력(공격 속도, 창격 3·4·5타, 패링·퓨리·돌파)은 `LanceAddon.cs`·`LanceMotion.cs`·`LanceParry.cs`, 기수별 고유 효과는 `Riders.cs`, 숫자 달린 상태 표시는 `StatusHud.cs` 입니다. 창 그림은 `frostforged/art/lance.py` 가 배색만 바꿔 만듭니다.
- 우주 속성(`Cosmic.cs`): 전용 능력치 '우주 속성 피해', 상태이상 무지·허무, 콤보 `일식`·`태극`, 아티팩트 16종(`ItemContent.cs`). 태극 발동형(참월 흑·백, 섬멸 청, 명멸 적)과 무지·허무의 이펙트는 `Sprites/Fx/` 의 스프라이트 애니메이션을 `FxPlayer.cs`·`CosmicFx.cs` 가 재생합니다. `void.png`·`ignorance.png` 는 이미지 생성 API 로 만들어 `frostforged/art/fx_slice.py` 로 자른 것이고, 나머지는 `frostforged/art/fx.py` 가 그립니다.
- 아티팩트 수치는 0레벨이 1레벨의 절반(불이익·켜짐/꺼짐 효과는 1레벨과 같음)이고, 1레벨부터 설정한 값이 차례로 적용됩니다.
- 공격 모션을 첫 번째 베기로 고정하는 기능은 아직 없습니다. 0.1.0은 그 구현에 필요한 애니메이션 정보를 로그(`[FrostforgedCrescent]`)로 남깁니다.
