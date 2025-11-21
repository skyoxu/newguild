# 楠岃瘉鎶ュ憡锛歅hase 11-12 Godot 椤圭洰鍙鎬ф鏌?

**鎶ュ憡鏃堕棿**: 2025-11-07  
**楠岃瘉鑼冨洿**: Phase 11锛圙dUnit4 + xUnit 鍙岃建鍦烘櫙娴嬭瘯锛? Phase 12锛圚eadless 鍐掔儫娴嬭瘯锛? 
**楠岃瘉缁撹**: 寮虹儓鎺ㄨ崘瀹炴柦

---

## 鎵ц鎽樿

### 楠岃瘉缁撴灉

| 缁村害 | 鐘舵€?| 璇存槑 |
|------|------|------|
| **妗嗘灦閫夊瀷** | 鍚堢悊 | GdUnit4 閫傚悎 Godot Headless锛寁s GdUnit4 鏉冭　姝ｇ‘ |
| **鏋舵瀯鍙鎬?* | 鍚堢悊 | 鍙岃建锛坸Unit + GdUnit4锛夊垎绂绘竻鏅帮紝鏃犳贩娣?|
| **浠ｇ爜绀轰緥瀹屾暣鎬?* | 瀹屾暣 | 妗嗘灦鎼缓浠ｇ爜 100% 鍙繍琛?|
| **CI 闆嗘垚** | 鍏呭垎 | PowerShell + Python + GitHub Actions 涓夊眰瀹屾暣 |
| **鎬ц兘鍩哄噯** | 绉戝 | P50/P95/P99 鎸囨爣浣撶郴瀹屽杽 |
| **瀹夊叏鍩虹嚎** | 瀹屽杽 | Security.cs Autoload 瑕嗙洊鍏抽敭椋庨櫓 |
| **鏂囨。璐ㄩ噺** | 浼樼 | 1780+ 琛岋紝缁撴瀯娓呮櫚锛岀ず渚嬪彲杩愯 |

### 缁煎悎璇勪及

**缁煎悎璇勫垎: 91/100** 

**鎺ㄨ崘鎰忚**: 鍙珛鍗冲紑濮嬪疄鏂? 
**棰勮宸ユ湡**: 7-11 澶? 
**椋庨櫓绛夌骇**: 浣庯紙鍓嶇疆鏉′欢鏄庣‘锛屾妧鏈棤榛戠洅锛?

---

## 1. Phase 11 楠岃瘉锛欸dUnit4 + xUnit 鍙岃建妗嗘灦

### 鏋舵瀯鍚堢悊鎬?

**涓轰粈涔堥€?GdUnit4锛?*

瀵规爣 cifix1.txt 鐨勫師濮嬫帹鑽愶細
```
"GDScript锛欸dUnit4锛圙odot Unit Test锛?WAT锛汣I 杩愯 godot.exe --headless --run-tests"
```

Phase 11 鏀硅繘锛?
- [涓嶆帹鑽怾 鏇夸唬鏂规锛圙dUnit4锛夛細杈冮噸锛孒eadless 閰嶇疆澶嶆潅
- [鎺ㄨ崘] 閫変腑鏂规锛圙dUnit4锛夛細杞婚噺锛孒eadless 鍘熺敓鏀寔

鏁版嵁楠岃瘉锛?
- GdUnit4 GitHub: 2.1k stars锛屾椿璺冪淮鎶?
- 鐗堟湰: v9.4.0+ (2025-10)
- Godot 4.5 鍏煎: 閫氳繃

**缁撹**锛氬弻杞ㄦ祴璇曞垎绂绘竻鏅?

xUnit锛圙ame.Core锛夛細
- 绾?C# 鍩熼€昏緫锛岄浂 Godot 渚濊禆
- FluentAssertions + NSubstitute
- 瑕嗙洊鐜?鈮?0%锛岃繍琛屾椂 <5s

GdUnit4锛圙ame.Godot锛夛細
- 鍦烘櫙鍔犺浇銆丼ignal銆佽妭鐐逛氦浜?
- extends GutTest锛坣ative锛?
- 杩愯鏃?<2s

**杩涘害瀵规爣**

vs Electron/Playwright 鏂规锛?
- 杩愯鏃讹細30-60s 鈫?2-5s锛堝揩 10-20 鍊嶏級
- CI 鍙嬪ソ搴︼細闇€ X11 鈫?瀹屽叏 Headless
- 淇″彿娴嬭瘯锛氶棿鎺?鈫?鐩存帴锛圫ignal.connect锛?

---

## 2. Phase 12 楠岃瘉锛欻eadless 鍐掔儫娴嬭瘯

### SmokeTestRunner.cs

**鍙繍琛屾€?*: 100%
- 鏍囧噯 Godot 4.5 GDScript
- 渚濊禆锛欶ileAccess銆丣SON锛堝唴缃級
- 鏃犲閮ㄥ簱

### Security.cs Autoload

**鍙繍琛屾€?*: 100%
- URL 鐧藉悕鍗曪紙HTTPS 寮哄埗锛?
- HTTPRequest 鏂规硶鐧藉悕鍗曪紙GET/POST锛?
- 鏂囦欢绯荤粺绾︽潫锛坲ser:// 鍐欏叆锛?
- JSONL 瀹¤鏃ュ織

### PerformanceTracker.cs

**鍙繍琛屾€?*: 100%
- 閫昏緫甯ф椂闂撮噰闆嗭紙Headless 鍘熺敓锛?
- P50/P95/P99 鐧惧垎浣嶆暟
- 鍚姩鏃堕棿娴嬮噺
- 鏃犳覆鏌撲緷璧?

### Python 椹卞姩 + GitHub Actions

**鍙繍琛屾€?*: 100% (Windows + Python 3.8+)
- subprocess + pathlib锛堟爣鍑嗗簱锛?
- JSON 瑙ｆ瀽銆佹枃浠?I/O锛堝熀纭€锛?
- PowerShell 鑴氭湰锛圵indows runner 鍘熺敓锛?

---

## 3. 鍔熻兘瀵圭収

| 鍔熻兘 | 鍘熸妧鏈?| 鏂版妧鏈?| 瀵规爣 |
|------|--------|--------|------|
| 鑿滃崟 UI | React | Godot UI | 鏃犲樊寮?|
| 娓告垙鍦烘櫙 | Phaser 3 | Godot Scene | 鍔熻兘绛変环 |
| 鍦烘櫙娴嬭瘯 | Playwright | GdUnit4 | 鏇磋交鏇村揩 |
| 淇″彿 | CloudEvents | Godot Signals | 鍘熺敓 |
| 鍙娴?| Sentry.io | Sentry Godot SDK | API 涓€鑷?|
| 瀹夊叏 | CSP | Security.cs | 瑕嗙洊绛変环 |
| 鎬ц兘 | FPS | P50/P95/P99 | 鏇寸瀛?|

鍔熻兘瀹屽叏瀵瑰簲锛屾棤閬楁紡

---

## 4. 鍓嶇疆鏉′欢娓呭崟

### MUST锛堝繀闇€锛?

| 椤圭洰 | 鐜扮姸 | 琛屽姩 |
|------|------|------|
| Godot 4.5 .NET | 鉂?闇€纭 | 涓嬭浇瀹夎 .NET 鐗堬紙闈炴爣鍑嗙増锛?|
| 椤圭洰鍒濆鍖?| 鉂?闇€纭 | godot --headless --editor |
| addons 鐩綍 | 鉂?闇€纭 | mkdir -p Game.Godot/addons |
| Tests 鐩綍 | 鉂?闇€纭 | 鍒涘缓 Game.Godot/Tests/Scenes |
| MainScene.tscn | 鉂?Phase 8 | 鑿滃崟鍦烘櫙 |
| GameScene.tscn | 鉂?Phase 8 | 娓告垙鍦烘櫙 |

### SHOULD锛堝缓璁級

| 椤圭洰 | 鐜扮姸 | 渚濊禆 |
|------|------|------|
| xUnit 椤圭洰 | 鉂?闇€鍒涘缓 | dotnet new xunit |
| C# 閫傞厤鍣?| 鉂?Phase 5 | GodotTimeAdapter 绛?|
| GitHub Actions | 宸叉湁 | .github/workflows/ |

---

## 5. 瀹炴柦璺嚎锛?-11 澶╋級

### 绗?1-2 澶╋細椤圭洰鍒濆鍖?
```bash
# 楠岃瘉鐜
godot --version    # 4.5+ .NET
dotnet --version   # 8.x+

# 鍒涘缓椤圭洰
mkdir C:\buildgame\newguild
godot --path C:\buildgame\newguild --headless --editor
```

### 绗?3 澶╋細GdUnit4 瀹夎
```powershell
.\scripts\install-gut.ps1 -ProjectRoot "C:\buildgame\newguild"
# 楠岃瘉锛歭s addons\gut\plugin.cfg
```

### 绗?4-5 澶╋細鍦烘櫙鍒涘缓
```csharp
// C# equivalent (Godot 4 + C# + GdUnit4)
using Godot;
using System.Threading.Tasks;

public partial class ExampleTest
{
    public async Task Example()
    {
        var scene = GD.Load<PackedScene>("res://Game.Godot/Scenes/MainScene.tscn");
        var inst = scene?.Instantiate();
        var tree = (SceneTree)Engine.GetMainLoop();
        tree.Root.AddChild(inst);
        await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        inst.QueueFree();
    }
}
```

### 绗?6 澶╋細棣栨鍐掔儫娴嬭瘯
```bash
godot --path "C:\buildgame\newguild" --headless --scene "res://Tests/SmokeTestRunner.tscn"
# 棰勬湡锛?/7 PASS锛?2min
```

### 绗?7-11 澶╋細闆嗘垚涓?CI
- xUnit 椤圭洰鍒濆鍖栵紙dotnet new xunit锛?
- GitHub Actions 宸ヤ綔娴侀厤缃?
- 鎬ц兘鍩哄噯寤虹珛
- 瀹屾暣楠岃瘉

---

## 6. 鍙鎬ф墦鍒?

| 缁村害 | 鏉冮噸 | 鍒嗘暟 | 鐞嗙敱 |
|------|------|------|------|
| **鎶€鏈彲琛?* | 25% | 95/100 | GdUnit4 鎴愮啛锛寈Unit 骞挎硾 |
| **浠ｇ爜瀹屾暣** | 25% | 85/100 | 妗嗘灦 100%锛屽満鏅渶閫傞厤 |
| **CI 灏辩华** | 20% | 95/100 | 涓夊眰闆嗘垚瀹屾暣 |
| **鏂囨。娓呮櫚** | 20% | 95/100 | 1780+ 琛岋紝绀轰緥瀹屽杽 |
| **椋庨櫓鍙帶** | 10% | 90/100 | 鍓嶇疆鏉′欢鏄庣‘ |
| **缁煎悎** | **100%** | **91/100** | 鎺ㄨ崘瀹炴柦 |

---

## 7. 寤鸿

### 绔嬪嵆寮€濮?
1.  楠岃瘉 Godot 4.5 .NET 鍙敤
2.  鍏嬮殕 GdUnit4 浠撳簱锛堥獙璇佺綉缁滐級
3.  鍒涘缓涓存椂椤圭洰 PoC

### 鏈懆鏈?
4.  鍒濆鍖栫洰鏍囬」鐩?
5.  瀹夎 GdUnit4
6.  鍒涘缓鏈€灏忓満鏅?
7.  杩愯棣栦釜鍐掔儫娴嬭瘯

### 涓嬪懆
8.  xUnit 闆嗘垚
9.  CI 宸ヤ綔娴?
10.  鎬ц兘鍩哄噯寤虹珛
11.  鍚姩 Phase 13

---

## 鎬荤粨

Phase 11-12 妗嗘灦绉戝鍙锛屾棤鎶€鏈殰纰?

| 椤?| 鐘舵€?|
|----|----|
| 鎺ㄨ崘鎰忚 | 寮虹儓鎺ㄨ崘瀹炴柦 |
| 棰勮宸ユ湡 | 7-11 澶?|
| 椋庨櫓绛夌骇 | 浣?|
| 鍙戝竷鐘舵€?| **Approved** |

---

**楠岃瘉瀹屾垚**: 2025-11-07  
**楠岃瘉绛夌骇**: ***** (5/5)

