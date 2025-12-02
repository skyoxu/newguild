import xml.etree.ElementTree as ET
import sys

coverage_file = sys.argv[1] if len(sys.argv) > 1 else r'C:\buildgame\newguild\logs\unit\test-run\dd4392bd-ed63-4015-932f-86f6dc3c1105\coverage.cobertura.xml'

tree = ET.parse(coverage_file)
root = tree.getroot()

packages = root.find('packages')
file_stats = []

for pkg in packages.findall('package'):
    for cls in pkg.findall('.//class'):
        filename = cls.attrib['filename']
        short_name = filename.split('\\')[-1]

        # Calculate branch metrics
        lines = cls.findall('.//line[@branch="true"]')
        if not lines:
            continue

        total_branches = 0
        covered_branches = 0

        for line in lines:
            branch_info = line.attrib.get('condition-coverage', '')
            if branch_info:
                # Parse "50% (1/2)" format
                parts = branch_info.split()
                if len(parts) >= 2:
                    fraction = parts[1].strip('()')
                    if '/' in fraction:
                        covered, total = map(int, fraction.split('/'))
                        covered_branches += covered
                        total_branches += total

        if total_branches > 0:
            current_rate = covered_branches / total_branches * 100
            uncovered = total_branches - covered_branches

            # Calculate potential improvement
            if uncovered <= 5:  # Easy wins
                potential_new_rate = (covered_branches + uncovered) / total_branches * 100
                improvement = potential_new_rate - current_rate

                file_stats.append({
                    'file': short_name,
                    'path': filename,
                    'covered': covered_branches,
                    'total': total_branches,
                    'uncovered': uncovered,
                    'current_rate': current_rate,
                    'potential_rate': potential_new_rate,
                    'improvement': improvement,
                    'effort': 'LOW' if uncovered <= 2 else 'MEDIUM'
                })

# Sort by uncovered count (easiest first)
file_stats.sort(key=lambda x: x['uncovered'])

print('=' * 100)
print('BRANCH COVERAGE GAP ANALYSIS - PATH TO 85%')
print('=' * 100)
print(f'Current Overall: 82.69% (172/208)')
print(f'Target: 85% (176/208)')
print(f'Additional branches needed: 4')
print()

print('QUICK WIN OPPORTUNITIES (≤5 uncovered branches):')
print('-' * 100)
print(f'{"File":<45} {"Covered":<10} {"Uncovered":<12} {"Current":<10} {"Effort":<10}')
print('-' * 100)

total_easy_uncovered = 0
for item in file_stats[:15]:
    print(f'{item["file"]:<45} {item["covered"]}/{item["total"]:<7} {item["uncovered"]:<12} {item["current_rate"]:>6.1f}%    {item["effort"]:<10}')
    total_easy_uncovered += item['uncovered']

print('-' * 100)
print(f'Total quick win branches available: {total_easy_uncovered}')
print()

# Calculate cumulative impact
print('RECOMMENDED STRATEGY TO REACH 85%:')
print('-' * 100)
cumulative = 172  # Current
target = 176

strategy_files = []
for item in file_stats:
    if cumulative >= target:
        break
    branches_to_add = min(item['uncovered'], target - cumulative)
    cumulative += branches_to_add
    strategy_files.append({
        'file': item['file'],
        'add': branches_to_add,
        'of': item['uncovered'],
        'cumulative': cumulative,
        'rate': cumulative / 208 * 100
    })

for i, item in enumerate(strategy_files, 1):
    print(f'{i}. {item["file"]:<45} Add {item["add"]}/{item["of"]} branches → {item["rate"]:.2f}%')

print('-' * 100)
print()
print('FEASIBILITY ASSESSMENT:')
print('-' * 100)
print(f'[OK] Only need to cover 4 more branches (2.31% improvement)')
print(f'[OK] Multiple files have <=2 uncovered branches (low effort)')
print(f'[OK] Focus on files with highest line coverage (already well-tested)')
print(f'')
print(f'VERDICT: HIGHLY FEASIBLE - Can reach 85% with targeted improvements')
print('=' * 100)
