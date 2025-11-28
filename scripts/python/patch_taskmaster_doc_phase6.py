#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Append a small note about Node/Web ecosystem being optional.

In newguild, the primary toolchain is Taskmaster + Python scripts +
dotnet/xUnit + GdUnit4. Any Node/Vitest/Playwright references in the
workflow document are optional and mainly for projects that add
Web/Electron frontends.

This script appends a short clarification section at the end of
`task-master-superclaude-integration.md` if it is not present yet.

Run once via:
  py -3 scripts/python/patch_taskmaster_doc_phase6.py
"""

from __future__ import annotations

from pathlib import Path


NOTE_HEADER = "## 10. Node / Web 鏂囧寲閰嶇疆璇存槑锛堟彃浠ュ彲閫夛級"

NOTE_BODY = (
    "\n\n" + NOTE_HEADER + "\n\n"
    "- 绗竴娆℃澶囨鎻愮ず锛氬湪 newguild 妯℃澘涓紝**涓婚宸ュ叿閰嶇疆**"
    "鏄浘 `.taskmaster/tasks/*.json` 鍜屼笓鐢ㄧ殑 Python 鑴氭湰銆佸苟閫氬父鍚勪綅杩涜 dotnet/xUnit 鍜孷dUnit4"
    "娴嬭瘯锛屾敮鎸乼I 涓殑鍩虹璐ㄩ噺闂ㄧ銆?\n"
    "- 鏂囨。涓湁鍑ºode锛孲npm`銆乵laywright MCP 绛夌浉鍏虫寚褰曪紝**榛樿閮戒负鍗曡瘝"
    " Web/Electron 瀛愰」鐩範渚嬶紙鐩爣鍖呮嫭 HTML5 Web 鐗堟湰鎴栬€呰寖鍥翠腑鍖呰锛夈€?\n"
    "- 濡傛灉褰撳墠椤圭洰浠€涔堣繕娌℃坊鍔犳湁閲嶅垎鐨凱eb/Electron 瀛愰」鐩紝鍙互**涓存椂"
    "鎵€鏈?Node/Vitest/Playwright 浠ｇ爜鍜屾爲浣撳寘鍚潵锛屼笉褰卞搷 Godot + C# 妯℃澘鐨勫熀纭€閰嶇疆鍜屼换鍔°€?\n"
    "- 濡傛灉鍚庨潰鎻愪緵鐨勫垎鏋愭祴璇曟ā鍧楋紝闇€瑕佸鍔犲湴鏂归」鐩浆鍙戜负 Web/Electron 鐗堟湰锛岃繕闇€瑕佺敤"
    " Node/Vitest/Playwright 杩涜鍏朵粬 CI 闆嗘垚锛屽緢閫傜敤鏂囨。鍙互浣跨敤鏈鍏跺彛鏍囧噯鍋氫负鍓嶇疆锛屽悓鏃朵繚鎸侀渶缁堟牎楠屾潯浠跺拰 ADR"
    " 涓殑鍥為摼涓€鑷达紝闃叉宸ュ叿閰嶇疆鍜屽熀纭€宸ュ叿鍚屾銆?\n"
)


def main() -> None:
    doc_path = Path("docs/workflows/task-master-superclaude-integration.md")
    text = doc_path.read_text(encoding="utf-8")

    if NOTE_HEADER in text:
        print("[patch_taskmaster_doc_phase6] note already present, no changes made")
        return

    text = text.rstrip() + NOTE_BODY + "\n"
    doc_path.write_text(text, encoding="utf-8")
    print("[patch_taskmaster_doc_phase6] appended Node/Web optional note section")


if __name__ == "__main__":  # pragma: no cover - tiny helper
    main()

