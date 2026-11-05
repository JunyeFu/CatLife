"""Prepare anonymous historical model/template pairs. Does not call a model or invent scores."""
import argparse
import csv
import json
from pathlib import Path
import random
import re
import statistics


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--input', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    rng = random.Random(20261105)
    inputs = sorted((args.input / 'cases').glob('A*.json'))
    rng.shuffle(inputs)
    key, review, checks, results = [], [], [], []
    for index, path in enumerate(inputs, 1):
        case = json.loads(path.read_text(encoding='utf-8-sig'))
        result = json.loads((args.input / 'results' / path.name).read_text(encoding='utf-8-sig'))
        content = json.loads(result['content'])
        fields = [content.get(k, '') for k in ('gentleReminder', 'returnSuggestion', 'catReactionReason')]
        variants = [('cloud', '温和提醒：' + fields[0] + '\n下一小步：' + fields[1] + '\n猫咪回应：' + fields[2]),
                    ('local', case['localTemplate'])]
        rng.shuffle(variants)
        context = f"合成场景；完成{case['seconds']}秒；"
        context += '行为统计未采集' if case['behaviorStatsDisabled'] else f"触控{case['touches']}次，后台{case['backgrounds']}次，前台稳定{case['stability']}%（仅应用活动代理量）"
        for slot, (source, text) in enumerate(variants):
            entry = f"P{index:02d}{chr(65 + slot)}"
            review.append({'id': entry, 'context': context, 'response': text, 'reviewer': '',
                           'relevance_0_2': '', 'actionable_0_2': '', 'no_overreach_0_2': '',
                           'no_invented_task_0_2': '', 'reaction_reasonable_0_2': '', 'notes': ''})
            key.append({'id': entry, 'case': case['id'], 'source': source})
        checks.append({'case': case['id'], 'status': result['status'], 'threeFieldsPresent': all(fields),
                       'actionAllowlisted': content.get('recommendedLocalAction') in ('none', 'quiet_idle', 'soft_roam', 'social_response'),
                       'concreteNextStep': bool(re.search('打开|写下|第一行|未完成|材料|下一步', fields[1])),
                       'rawUtf16Lengths': [len(s.encode('utf-16-le')) // 2 for s in fields],
                       'displayLimitUtf16': 24})
        results.append(result)
    with (args.output / 'blind-review.csv').open('w', encoding='utf-8-sig', newline='') as stream:
        writer = csv.DictWriter(stream, fieldnames=list(review[0])); writer.writeheader(); writer.writerows(review)
    (args.output / 'answer-key-private.json').write_text(json.dumps(key, ensure_ascii=False, indent=2), encoding='utf-8')
    summary = {'syntheticCases': len(results), 'newRequests': 0, 'humanRatings': 0,
               'http200': sum(r['status'] == 200 for r in results),
               'meanMs': statistics.mean(r['elapsedMs'] for r in results), 'maxMs': max(r['elapsedMs'] for r in results),
               'returnedTotalTokens': sum(r.get('usage', {}).get('total_tokens', 0) for r in results),
               'actionDistribution': {}, 'checks': checks, 'userBenefitGate': 'open-no-human-evidence'}
    for result in results:
        action = json.loads(result['content']).get('recommendedLocalAction')
        summary['actionDistribution'][action] = summary['actionDistribution'].get(action, 0) + 1
    (args.output / 'evaluation-summary.json').write_text(json.dumps(summary, ensure_ascii=False, indent=2), encoding='utf-8')
    (args.output / 'README.md').write_text('# 历史响应匿名对照\n\n'
        '12个合成聚合场景，24条建议；来源及顺序已随机化。填写blind-review.csv，不读取answer-key-private.json。\n'
        '每项0=不满足、1=部分、2=满足；相关性、具体下一步、无越界、无虚构任务、动作理由合理性分别评分。\n'
        '比较实际显示内容，模板与三段云端建议的格式差异仍可能透露来源，这是本对照的限制。\n'
        '未填写评分，不代表人工盲评完成；不能据此声称坚持率或学习效果提高。\n'
        '请求和响应来自2026-10-09历史真实调用，本轮零新增请求；样本少，不报告生产P95或统计显著。\n', encoding='utf-8')
    print(json.dumps({k: v for k, v in summary.items() if k != 'checks'}, ensure_ascii=False))


if __name__ == '__main__':
    main()
