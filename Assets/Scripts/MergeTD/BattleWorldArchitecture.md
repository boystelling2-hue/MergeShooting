# 전투 월드 / Canvas UI 분리

`Assets/Scenes/SampleScene.unity`에 실제 씬 오브젝트로 저장되어 있습니다.

- **Battle World**: 맵, 길, 입출구, 아군, 적, 투사체, 공격 궤적, 체력바, 레벨, 사거리. Canvas를 포함하지 않습니다.
- **Battle Camera**: 전장에 맞춘 Orthographic 카메라. MergeBattleWorld 레이어만 렌더링합니다.
- **Battle UI**: 생명력·재화·라운드, 구매 목록, 선택·강화 패널, 메뉴. 전장을 그리던 UI 칸과 Moving Actors Canvas는 제거했습니다.
- **Workshop UI**: 공방·인벤토리·합성·도감. 기존 Canvas 방식과 저장 구조를 유지합니다.

## 역할

- DefenseBattle: 전투 계산과 규칙. 기존 계산을 유지합니다.
- BattleScreen: 전투 진행과 HUD 연결. 월드 표시를 BattleWorldView에 위임합니다.
- BattleWorldView: SpriteRenderer 표시, 카메라 영역, 화면 좌표→배치 칸 변환, 마우스/터치 입력, UI 클릭 차단.
- WorldActorView: 개별 유닛의 아이콘·HP·레벨 표시.

논리 좌표 (x,y)는 월드 로컬 좌표 (x,-y)에 대응하며 한 칸은 1 Unity 단위입니다. 기존 18×11 배치 규칙과 경로를 유지합니다. Canvas의 빈 Battlefield RectTransform은 카메라 영역을 정하는 기준으로만 사용합니다.

## 씬 / 프리팹 편집

- Assets/Prefabs/DefenseWorld/TowerActor.prefab
- Assets/Prefabs/DefenseWorld/EnemyActor.prefab
- Assets/Prefabs/DefenseWorld/WorldSprites.mat

풀 오브젝트는 편집 시 씬에 배치됩니다. 플레이 중 GameObject나 UI 레이아웃을 생성하지 않습니다. 아군 198칸, 적 32개, 투사체 96개, 궤적 64개의 표시 용량은 기존 전투의 용량을 이어받습니다.

시작 화면은 공방이므로 Battle World는 저장 시 비활성화되어 있으며 전투 입장 시 켜집니다. Scene 뷰에서 월드를 살펴볼 때는 해당 루트를 활성화하면 됩니다. 실제 배치 위치는 전투 상태가 정하고, 프리팹으로 외형을 편집합니다. 확대·이동 카메라는 이번 단계에 추가하지 않았습니다.

Tools > MergeTD > Migrate Battle To World는 기존 공방/HUD를 보존하고 전장의 표시 계층만 다시 만듭니다. 재실행하면 월드 계층의 수동 수정은 교체되므로 새 외형은 프리팹 또는 authoring 소스에도 반영해야 합니다. Build Landscape UI 역시 씬을 다시 생성하는 개발 도구입니다.

## 검증

- Tools > MergeTD > Verify World Scene: Canvas와 월드 분리, 풀·머티리얼·씬 참조 확인.
- Play 모드에서 Tools > MergeTD > Verify World Runtime: 배치·강화·타겟·입력 차단·일시정지·복귀, 세 해상도 좌표, 매 프레임 이동 확인. 실제 인벤토리 변경 여부도 비교합니다.
- 기존 BTD 검증 / Visual Quality Repair도 월드 표시 구조를 사용하도록 갱신했습니다.

이번 단계는 전투 표시 구조의 이전입니다. 덱 저장, 제작법 목록, 연속 배치, 판매/이동, 라운드 자동 진행 설정 등 앞선 QA의 기획 변경은 별도 작업으로 남아 있습니다. 성능 향상의 실측치는 주장하지 않으며, 실제 게임 프레임률·모바일 터치·다수 적 부하는 대상 기기에서 추가 측정할 수 있습니다.
