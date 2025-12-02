import xml.etree.ElementTree as ET

coverage_file = r'C:\buildgame\newguild\logs\unit\test-run\dd4392bd-ed63-4015-932f-86f6dc3c1105\coverage.cobertura.xml'

tree = ET.parse(coverage_file)
root = tree.getroot()

# Current state
branches_covered = int(root.attrib['branches-covered'])
branches_valid = int(root.attrib['branches-valid'])
current_rate = branches_covered / branches_valid * 100

# Target calculation
target_rate = 85.0
needed_covered = int(branches_valid * target_rate / 100)
additional_needed = needed_covered - branches_covered

print('=' * 80)
print('BRANCH COVERAGE: PATH TO 85% ANALYSIS')
print('=' * 80)
print()
print('CURRENT STATE:')
print(f'  Branches covered: {branches_covered}/{branches_valid}')
print(f'  Current rate: {current_rate:.2f}%')
print()
print('TARGET STATE (85%):')
print(f'  Branches needed: {needed_covered}/{branches_valid}')
print(f'  Additional branches: {additional_needed}')
print(f'  Improvement needed: {target_rate - current_rate:.2f}%')
print()

# Analyze files by branch coverage
packages = root.find('packages')
file_analysis = []

for pkg in packages.findall('package'):
    for cls in pkg.findall('.//class'):
        filename = cls.attrib['filename'].split('\\')[-1]
        line_rate = float(cls.attrib['line-rate']) * 100

        # Get branch info from lines
        branch_lines = cls.findall('.//line[@branch="true"]')
        if not branch_lines:
            continue

        file_branches_covered = 0
        file_branches_total = 0

        for line in branch_lines:
            # condition-coverage format: "50% (1/2)"
            cond_cov = line.attrib.get('condition-coverage', '')
            if '(' in cond_cov and ')' in cond_cov:
                fraction = cond_cov.split('(')[1].split(')')[0]
                if '/' in fraction:
                    covered, total = map(int, fraction.split('/'))
                    file_branches_covered += covered
                    file_branches_total += total

        if file_branches_total > 0:
            branch_rate = file_branches_covered / file_branches_total * 100
            uncovered = file_branches_total - file_branches_covered

            file_analysis.append({
                'file': filename,
                'line_rate': line_rate,
                'branch_covered': file_branches_covered,
                'branch_total': file_branches_total,
                'branch_rate': branch_rate,
                'uncovered': uncovered
            })

# Sort by uncovered count (easiest wins first)
file_analysis.sort(key=lambda x: x['uncovered'])

print('FILES WITH UNCOVERED BRANCHES (EASIEST FIRST):')
print('-' * 80)
print(f'{"File":<40} {"Line%":>7} {"Branch":>12} {"Uncov":>6} {"Priority":<10}')
print('-' * 80)

cumulative_gain = 0
recommended = []

for f in file_analysis:
    if f['uncovered'] <= 5:  # Quick wins
        priority = 'HIGH' if f['uncovered'] <= 2 else 'MEDIUM'
        print(f'{f["file"]:<40} {f["line_rate"]:>6.1f}% {f["branch_covered"]}/{f["branch_total"]:<7} {f["uncovered"]:>6} {priority:<10}')

        if cumulative_gain < additional_needed:
            branches_to_add = min(f['uncovered'], additional_needed - cumulative_gain)
            cumulative_gain += branches_to_add
            recommended.append({
                'file': f['file'],
                'add': branches_to_add,
                'total': f['uncovered']
            })

print('-' * 80)
print()

if recommended:
    print('RECOMMENDED APPROACH TO REACH 85%:')
    print('-' * 80)
    cumulative = branches_covered
    for i, rec in enumerate(recommended, 1):
        cumulative += rec['add']
        new_rate = cumulative / branches_valid * 100
        print(f'{i}. {rec["file"]:<40} Add {rec["add"]}/{rec["total"]} branches -> {new_rate:.2f}%')
    print('-' * 80)
    print()

print('FEASIBILITY ASSESSMENT:')
print('-' * 80)
print(f'[OK] Only {additional_needed} more branches needed (vs {branches_valid - branches_covered} total uncovered)')
print(f'[OK] That is {additional_needed/branches_covered*100:.1f}% increase over current coverage')
print(f'[OK] {len([f for f in file_analysis if f["uncovered"] <= 2])} files have <=2 uncovered branches')
print()

if additional_needed <= 4:
    print('VERDICT: HIGHLY FEASIBLE')
    print('  - Minimal effort required (1-2 hours)')
    print('  - Can focus on just a few strategic test cases')
    print('  - High probability of success')
elif additional_needed <= 10:
    print('VERDICT: FEASIBLE')
    print('  - Moderate effort required (half day)')
    print('  - Systematic approach to multiple files')
    print('  - Good probability of success')
else:
    print('VERDICT: CHALLENGING')
    print('  - Significant effort required (1-2 days)')
    print('  - May require deep analysis of complex branches')

print('=' * 80)
