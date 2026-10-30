# 메인 루프 DB v2

출전 덱은 중복 없이 1~5종입니다. 전장 개체 수 제한과 다릅니다.
제작 티어(tier)는 영구 제작 단계입니다. 전투 성급(star)과 별개입니다.
캐릭터별 영구 레벨은 없으며 공용 성장 테이블을 예약했습니다.
약점은 기존 초안 1.5배, 일반 1배, 강인성은 확정 규칙 0.5배입니다.
14종 적의 강인성 배정은 작업 초안(draft)이며 밸런스 확정값이 아닙니다.

## 편집과 내보내기
기존 Excel은 유닛·제작·시너지·적 약점·추천 덱 편집용으로 유지했습니다.
Excel의 과거 4명 표기는 v2 최대 편성 규칙의 기준이 아닙니다.
Data/main_loop_config.json이 최대 5종·강인성·추가 시스템의 기준입니다.
추천 덱은 최대 5개 ID 열을 읽으며, 기존 4종 덱도 정상입니다.
python Tools/export_workbook.py MergeTD_Design.xlsx Data/game_data.json
내보낸 JSON을 Unity Assets/Data/game_data.json에 복사하세요.
구버전 내보내기 도구를 사용하면 안 됩니다. Backup_before_v2는 복구용입니다.

## API
TryResolveCraftMerge: 메인 영구 제작식 조회. 재료 차감·발견 저장은 별도입니다.
GetElementDamageMultiplier: 약점/일반/강인성 배율 조회.
IsValidDeck: 중복 없는 1~5종 및 존재하는 ID 검사.
GetActiveSynergies: 전달된 ID 목록의 종류 수로 시너지 계산(미리보기용).
GetBattleSynergies: synergySource가 deck/deployed로 결정된 후 사용합니다.
동일 유닛 여러 개체는 시너지 종류 수에 한 번만 포함합니다.
이전 API는 호환 래퍼로 남겼으며 새 API로 전환하세요.

## 아직 플레이 구현이 아닌 부분
battleMerge, summoning, battleLimits는 configured=false이며 미설정입니다.
equipment, sharedGrowth, combatStats, skills, stages는 빈 예약 테이블입니다.
관련 C# 데이터 타입만 추가했으며 실행기와 저장 시스템은 없습니다.
빈 테이블을 실제 구현 완료로 간주하지 마세요.
현재 버전은 미구현 규칙의 활성화나 예약 테이블의 레코드를 거부합니다.
구체적인 비용·성급·장비·스킬·웨이브를 확정한 뒤 검증기와 실행기를 함께 확장해야 합니다.
미정 사항은 pendingDecisions에 기록되어 있습니다.
