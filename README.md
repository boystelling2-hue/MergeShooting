# MergeShooting

Unity 6.3 LTS (6000.3.27f1) 기반 합성 디펜스 프로토타입입니다.

## 현재 구현
- 유닛 90종, 제작식 135개, 적 14종과 테마 시너지 데이터
- 중복 없이 최대 5종의 출전 덱 검증
- 약점 1.5배(초안), 일반 1배, 강인성 0.5배 판정
- SampleScene의 MergeGameDatabase에 JSON 연결

전투 성급, 장비, 공용 성장, 스킬 및 웨이브는 데이터 구조만 준비된 상태입니다.
개별 캐릭터 영구 레벨업은 없습니다. 적별 강인성 배정은 밸런스 초안입니다.

## 실행 및 검증
Unity Hub에서 이 폴더를 추가하고 지정 버전으로 여세요.
Assets/Scenes/SampleScene.unity를 연 뒤 Play로 데이터 로드를 확인합니다.
Tools > MergeTD > Verify V2 Database 메뉴에서 데이터와 주요 규칙을 검증할 수 있습니다.
아직 플레이 가능한 전투 화면은 없습니다.

## 데이터 편집
Design/MergeTD_Package의 Excel은 기존 유닛과 약점, 시너지, 추천 덱 원본입니다.
추가 규칙과 강인성은 같은 폴더의 Data/main_loop_config.json이 기준입니다.
Excel의 과거 4명 표기 대신 JSON 설정의 최대 5종 규칙을 사용합니다.

프로젝트 루트에서 Python 3으로 실행하면 Unity 데이터가 갱신됩니다.

```powershell
python Design/MergeTD_Package/Tools/export_workbook.py Design/MergeTD_Package/MergeTD_Design.xlsx Assets/Data/game_data.json
```

Unity가 생성하는 Library, Temp, Logs와 사용자 설정은 버전 관리에서 제외합니다.
