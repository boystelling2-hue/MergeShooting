#!/usr/bin/env python3
"""Excel -> Unity JSON. Python 3 standard library only; no Excel installation required."""
import argparse
import csv
import itertools
import json
import math
import posixpath
import re
import sys
import zipfile
from pathlib import Path
import xml.etree.ElementTree as ET

NS = {'s': 'http://schemas.openxmlformats.org/spreadsheetml/2006/main'}
REL = 'http://schemas.openxmlformats.org/officeDocument/2006/relationships'
THEMES = {'궁정','신성','비전','자연','공학','암영','북방','용맹'}
ELEMENTS = {'물리','화염','냉기','번개','신성','암흑','자연'}
COUNT_FIELDS = ['knight','archer','buffer','healer','mage']

def require(condition, message):
    if not condition:
        raise ValueError(message)

def read_sheets(path):
    with zipfile.ZipFile(path) as z:
        strings = []
        if 'xl/sharedStrings.xml' in z.namelist():
            for si in ET.fromstring(z.read('xl/sharedStrings.xml')).findall('s:si', NS):
                strings.append(''.join(si.itertext()))
        rels = {r.attrib['Id']:r.attrib['Target'] for r in ET.fromstring(z.read('xl/_rels/workbook.xml.rels'))}
        result = {}
        for sh in ET.fromstring(z.read('xl/workbook.xml')).findall('s:sheets/s:sheet', NS):
            target = rels[sh.attrib['{' + REL + '}id']]
            part = target.lstrip('/') if target.startswith('/') else posixpath.normpath('xl/' + target)
            cells, formula_cells = {}, set()
            for c in ET.fromstring(z.read(part)).findall('.//s:sheetData/s:row/s:c', NS):
                address, kind = c.attrib['r'], c.attrib.get('t','n')
                if c.find('s:f', NS) is not None:
                    formula_cells.add(address)
                v = c.find('s:v', NS)
                if kind == 's':
                    value = strings[int(v.text)] if v is not None and v.text else ''
                elif kind == 'inlineStr':
                    value = ''.join(c.find('s:is', NS).itertext())
                elif v is None or v.text is None:
                    value = ''
                elif kind in ('str','e','d'):
                    value = v.text
                elif kind == 'b':
                    value = v.text == '1'
                else:
                    value = float(v.text)
                    if value.is_integer():
                        value = int(value)
                cells[address] = value
            result[sh.attrib['name']] = (cells, formula_cells)
        return result

def col(index):
    s = ''
    while index:
        index, rem = divmod(index-1,26)
        s = chr(65+rem) + s
    return s

def records(sheets, name):
    require(name in sheets, '필수 시트 없음: ' + name)
    cells, formulas = sheets[name]
    headers = []
    n = 1
    while cells.get(col(n)+'5','') != '':
        headers.append(str(cells[col(n)+'5']))
        n += 1
    require(headers, name + ': 5행 머리글 없음')
    require(len(headers)==len(set(headers)), name + ': 중복 머리글')
    maxrow = max((int(re.search(r'\d+$',a).group()) for a in cells), default=5)
    rows = []
    for row in range(6,maxrow+1):
        if cells.get('A'+str(row),'') == '':
            continue
        record = {}
        for i, header in enumerate(headers,1):
            address = col(i)+str(row)
            # R열의 기본 재료 수는 검토용 계산식이다. 데이터는 실제 구성 수로 계산한다.
            if header != '기본 재료 수':
                require(address not in formulas, f'{name}!{address}: 데이터 필드 수식은 지원하지 않음. 값을 입력하세요.')
            record[header] = cells.get(address,'')
        rows.append(record)
    return rows

def text(r, key):
    require(key in r, '필수 열 없음: '+key)
    value = str(r[key]).strip()
    require(value, '빈 필드: '+key)
    return value

def integer(value, label):
    require(isinstance(value,(int,float)) and not isinstance(value,bool) and math.isfinite(value) and int(value)==value and value>=0, label+': 0 이상 정수 필요')
    return int(value)

def counts(u):
    return tuple(u[k] for k in COUNT_FIELDS)

def export(path):
    sheets = read_sheets(path)
    units = []
    for sheet in ['기본_2티어','3티어_70종']:
        for r in records(sheets,sheet):
            c = tuple(integer(r.get(k,''),k) for k in ['기사 수','궁수 수','버퍼 수','힐러 수','마법사 수'])
            total = sum(c)
            require(total in (1,2,4), '기본 재료 수 합계는 1,2,4만 가능')
            tier = {1:1,2:2,4:3}[total]
            require((sheet=='3티어_70종')==(tier==3), '유닛 티어와 시트 위치 불일치')
            uid = text(r,'유닛 ID')
            require(uid==f'U{tier}_'+''.join(map(str,c)), uid+': ID와 직업 수 불일치')
            key = '_'.join(map(str,c))
            require(text(r,'구성 키')==key, uid+': 구성 키 불일치')
            tags = [text(r,'테마 1'),text(r,'테마 2')]
            require(len(set(tags))==2 and all(t in THEMES for t in tags),uid+': 테마 오류')
            element = text(r,'공격속성')
            require(element in ELEMENTS,uid+': 공격속성 오류')
            u = dict(unitId=uid,name=text(r,'유닛명'),tier=tier,**dict(zip(COUNT_FIELDS,c)),compositionKey=key,role=text(r,'역할'),themeTags=tags,attackElement=element,skillId=text(r,'능력 ID'),skillName=text(r,'능력명'),skillDescription=text(r,'능력 설명'),limitation=text(r,'약점·제약'),buildHint=text(r,'추천 빌드'),balanceStatus='능력 설계 초안·전투 수치 미설정')
            units.append(u)
    by_id = {u['unitId']:u for u in units}
    by_counts = {counts(u):u for u in units}
    require(len(by_id)==len(by_counts)==len(units)==90,'90종 구성 또는 ID 중복 오류')
    require(len({u['skillId'] for u in units})==90,'능력 ID 중복')
    for n,tier in [(1,1),(2,2),(4,3)]:
        for combo in itertools.combinations_with_replacement(range(5),n):
            c=tuple(combo.count(i) for i in range(5))
            require(c in by_counts and by_counts[c]['tier']==tier,'기본 직업 조합 누락: '+str(c))
    recipes = []
    seen_outputs = set()
    for tier in (1,2):
        pool = sorted((u for u in units if u['tier']==tier),key=lambda u:u['unitId'])
        for a,b in itertools.combinations_with_replacement(pool,2):
            c = tuple(x+y for x,y in zip(counts(a),counts(b)))
            out = by_counts[c]
            recipes.append(dict(recipeId=f"R_{a['unitId']}_{b['unitId']}",inputA=a['unitId'],inputB=b['unitId'],resultId=out['unitId'],resultTier=out['tier'],shopHint=out['unitId'] not in seen_outputs))
            seen_outputs.add(out['unitId'])
    require(len(recipes)==135,'합성식 수 오류')
    synergies = []
    for r in records(sheets,'키워드시너지'):
        tag = text(r,'키워드')
        require(tag in THEMES,'시너지 키워드 오류')
        thresholds=[integer(r['1단계 인원'],'1단계 인원'),integer(r['2단계 인원'],'2단계 인원')]
        require(thresholds==[2,4],'시너지 기준은 이 초안에서 2/4로 고정')
        vals=[r['1단계 증가율'],r['2단계 증가율']]
        require(all(isinstance(v,(int,float)) and math.isfinite(v) and v>=0 for v in vals),'시너지 배율은 0 이상 숫자 필요')
        synergies.append(dict(tag=tag,name=text(r,'시너지명'),effectId=text(r,'효과 ID'),thresholds=thresholds,value2=vals[0],value4=vals[1],description=text(r,'적용 규칙')))
    require(len(synergies)==8 and {s['tag'] for s in synergies}==THEMES,'시너지 8종 누락 또는 중복')
    monsters=[]
    for r in records(sheets,'몬스터약점'):
        weak=text(r,'약점속성').split('|')
        require(all(e in ELEMENTS for e in weak) and len(weak)==len(set(weak)),'몬스터 약점 오류')
        monsters.append(dict(monsterId=text(r,'몬스터 ID'),name=text(r,'이름'),rank=text(r,'등급'),weakElements=weak,theme=text(r,'테마'),behavior=text(r,'행동'),buildHint=text(r,'추천 대응 빌드')))
    require(len(monsters)==len({m['monsterId'] for m in monsters}) and monsters,'몬스터 ID 중복 또는 빈 데이터')
    builds=[]
    for r in records(sheets,'추천빌드'):
        ids=[str(r.get(f'유닛 {i} ID','')).strip() for i in range(1,6)]
        ids=[i for i in ids if i]
        require(1 <= len(ids) <= 5 and len(ids)==len(set(ids)), '추천 덱은 중복 없이 1~5종 필요')
        require(all(i in by_id for i in ids),'추천 빌드에 없는 유닛 ID 포함')
        builds.append(dict(buildId=text(r,'빌드 ID'),name=text(r,'빌드명'),unitIds=ids,plan=text(r,'운용'),strongAgainst=text(r,'유리한 적'),weakAgainst=text(r,'취약 상황'),note=text(r,'교체·속성 메모')))
    require(len(builds)==len({b['buildId'] for b in builds}),'빌드 ID 중복')
    settings=sheets['설계요약'][0]
    weak_multiplier=settings.get('E13')
    require(isinstance(weak_multiplier,(int,float)) and math.isfinite(weak_multiplier) and weak_multiplier>=1,'설계요약 E13: 약점 배율은 1 이상 숫자 필요')
    formation=integer(settings.get('E14'),'편성 인원')
    require(formation in (4,5),'기존 엑셀의 편성 표기는 4 또는 5만 허용. 실제 상한은 v2 설정의 5종 사용')
    rules=dict(schemaVersion=1,weaknessMultiplier=weak_multiplier,formationSizeDraft=formation,synergyCountMode='distinctUnitId',themeStackMode='highestThreshold',sameSkillStackMode='highestOnlyUnlessSpecified',allDamagingHitsUseUnitElement=True)
    result = dict(schemaVersion=2,units=units,recipes=recipes,synergies=synergies,monsters=monsters,builds=builds,rules=rules)
    config_path = Path(__file__).resolve().parent.parent/'Data'/'main_loop_config.json'
    config = json.loads(config_path.read_text(encoding='utf-8'))
    require(config.get('schemaVersion')==2, 'v2 메인 루프 설정 필요')
    result['rules'].pop('formationSizeDraft',None)
    result['rules'].update(config['rules'])
    result['rules']['schemaVersion']=2
    require(result['rules']['maxDeckUnitTypes']==5, '출전 덱 최대 5종 필요')
    require(result['rules']['resistanceMultiplier']==0.5, '강인성 배율은 0.5')
    require(result['rules']['personalUnitLevelsEnabled'] is False, '개별 영구 레벨 사용 불가')
    require(result['rules']['synergySource'] in ('unconfigured','deck','deployed'), '시너지 기준 오류')
    overrides={m['monsterId']:m for m in config['monsterOverrides']}
    require(len(overrides)==len(config['monsterOverrides']) and set(overrides)=={m['monsterId'] for m in monsters}, '강인성 설정 ID 누락/중복')
    for m in monsters:
        override=overrides[m['monsterId']]
        resist=override['resistElements']
        require(isinstance(resist,list) and all(e in ELEMENTS for e in resist) and len(set(resist))==len(resist), '강인성 속성 오류')
        require(not set(resist)&set(m['weakElements']), '약점과 강인성 중복 불가')
        m.update(resistElements=resist,resistanceStatus=override['resistanceStatus'])
    for key in ('battleMerge','equipment','sharedGrowth','combatStats','skills','stages','summoning','battleLimits','pendingDecisions'):
        result[key]=config[key]
    # These tables are deliberately unavailable until their complete gameplay rules are implemented.
    require(not result['battleMerge']['configured'] and not result['summoning']['configured'] and not result['battleLimits']['configured'], '미구현 규칙을 configured=true로 활성화할 수 없음')
    require(all(not result[k] for k in ('equipment','sharedGrowth','combatStats','skills','stages')), '예약 테이블은 현재 비워 두세요. 실행기 구현 후 스키마 확장 필요')
    return result

def main():
    ap=argparse.ArgumentParser(description=__doc__)
    ap.add_argument('workbook',type=Path)
    ap.add_argument('output',type=Path)
    args=ap.parse_args()
    try:
        data=export(args.workbook)
        args.output.parent.mkdir(parents=True,exist_ok=True)
        temp=args.output.with_suffix(args.output.suffix+'.tmp')
        temp.write_text(json.dumps(data,ensure_ascii=False,indent=2),encoding='utf-8')
        temp.replace(args.output)
        # CSV snapshots are refreshed alongside JSON so they cannot silently stay stale.
        for name,objects in [('units',data['units']),('recipes',data['recipes']),('monsters',data['monsters'])]:
            fields=list(objects[0])
            with (args.output.parent/(name+'.csv')).open('w',encoding='utf-8-sig',newline='') as f:
                w=csv.DictWriter(f,fieldnames=fields);w.writeheader()
                for obj in objects:
                    w.writerow({k:'|'.join(map(str,v)) if isinstance(v,list) else v for k,v in obj.items()})
        print(f"Exported {len(data['units'])} units, {len(data['recipes'])} recipes, {len(data['monsters'])} monsters -> {args.output}")
        return 0
    except (ValueError,KeyError,zipfile.BadZipFile,ET.ParseError,OSError) as e:
        print('Export failed: '+str(e),file=sys.stderr)
        return 1

if __name__=='__main__':
    sys.exit(main())
