import xml.etree.ElementTree as ET
import sys

coverage_file = sys.argv[1]
tree = ET.parse(coverage_file)
root = tree.getroot()

pkg = root.find('.//package[@name="Game.Core"]')
if not pkg:
    print("Package Game.Core not found")
    sys.exit(1)

classes = pkg.find('classes')
print('Guild Domain Coverage (Game.Core.Domain):')
print('=' * 70)

guild_classes = [c for c in classes.findall('class') if 'Domain' in c.get('filename', '')]
if not guild_classes:
    print("No Domain classes found")
    sys.exit(0)

for cls in guild_classes:
    name = cls.get('name').split('.')[-1]
    line_rate = float(cls.get('line-rate', 0)) * 100
    branch_rate = float(cls.get('branch-rate', 0)) * 100

    lines = cls.find('lines')
    total_lines = len(lines.findall('line')) if lines else 0
    covered = sum(1 for l in lines.findall('line') if int(l.get('hits', 0)) > 0) if lines else 0

    print(f'{name:20s} Line={line_rate:5.1f}% ({covered:2d}/{total_lines:2d}) Branch={branch_rate:5.1f}%')

print('=' * 70)

# Calculate Domain-only coverage
domain_classes = [c for c in classes.findall('class') if 'Domain' in c.get('filename', '')]
if domain_classes:
    total_lines_all = sum(len(c.find('lines').findall('line')) if c.find('lines') else 0 for c in domain_classes)
    covered_lines_all = sum(sum(1 for l in c.find('lines').findall('line') if int(l.get('hits', 0)) > 0) if c.find('lines') else 0 for c in domain_classes)

    domain_line_rate = (covered_lines_all / total_lines_all * 100) if total_lines_all > 0 else 0

    print(f'Domain Total: {covered_lines_all}/{total_lines_all} lines = {domain_line_rate:.1f}%')
    print(f'Gate Status (≥90%): {'✓ PASS' if domain_line_rate >= 90 else '✗ FAIL'}')
