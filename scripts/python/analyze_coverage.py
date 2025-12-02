import xml.etree.ElementTree as ET
import sys

coverage_file = sys.argv[1] if len(sys.argv) > 1 else r'C:\buildgame\newguild\logs\unit\test-run\dd4392bd-ed63-4015-932f-86f6dc3c1105\coverage.cobertura.xml'

tree = ET.parse(coverage_file)
root = tree.getroot()

# Overall metrics
line_rate = float(root.attrib['line-rate']) * 100
branch_rate = float(root.attrib['branch-rate']) * 100
lines_covered = int(root.attrib['lines-covered'])
lines_valid = int(root.attrib['lines-valid'])
branches_covered = int(root.attrib['branches-covered'])
branches_valid = int(root.attrib['branches-valid'])

print('=' * 80)
print('COVERAGE SUMMARY')
print('=' * 80)
print(f'Line Coverage:   {line_rate:6.2f}% ({lines_covered}/{lines_valid})')
print(f'Branch Coverage: {branch_rate:6.2f}% ({branches_covered}/{branches_valid})')
print()

# Files with low branch coverage
packages = root.find('packages')
low_branch_files = []

for pkg in packages.findall('package'):
    for cls in pkg.findall('.//class'):
        filename = cls.attrib['filename']
        short_name = filename.split('\\')[-1]
        line_rate_file = float(cls.attrib['line-rate']) * 100
        branch_rate_file = float(cls.attrib.get('branch-rate', '0')) * 100

        if branch_rate_file < 90 and branch_rate_file > 0:
            low_branch_files.append({
                'file': short_name,
                'line': line_rate_file,
                'branch': branch_rate_file,
                'path': filename
            })

if low_branch_files:
    print('FILES WITH BRANCH COVERAGE < 90%:')
    print('-' * 80)
    print(f'{"File":<50} {"Line %":>10} {"Branch %":>10}')
    print('-' * 80)
    for item in sorted(low_branch_files, key=lambda x: x['branch']):
        print(f'{item["file"]:<50} {item["line"]:>9.2f}% {item["branch"]:>9.2f}%')
    print()

# Quality gate result
print('=' * 80)
print('QUALITY GATE RESULT (90% threshold)')
print('=' * 80)
print(f'Line Coverage:   {"PASS" if line_rate >= 90 else "FAIL"}')
print(f'Branch Coverage: {"PASS" if branch_rate >= 90 else "FAIL"}')
print(f'Overall:         {"PASS" if line_rate >= 90 and branch_rate >= 90 else "FAIL"}')
print('=' * 80)

sys.exit(0 if line_rate >= 90 and branch_rate >= 90 else 1)
