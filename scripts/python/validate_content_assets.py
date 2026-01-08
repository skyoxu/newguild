#!/usr/bin/env python3
"""
Validate content assets (JSON) and write CI artifacts under logs/ci/<date>/.

This validator intentionally uses only the Python standard library.

Hard rules:
- All *.json under `Game.Godot/Assets/Data/**` must be parseable UTF-8 JSON.
- Base content pack files must exist and follow minimal schema rules:
  - `Game.Godot/Assets/Data/content/base/manifest.json`
  - `Game.Godot/Assets/Data/content/base/guild_events.json`
  - `Game.Godot/Assets/Data/content/base/tuning.json`

Outputs:
- logs/ci/<YYYY-MM-DD>/content-validation/report.json
- logs/ci/<YYYY-MM-DD>/content-validation/report.txt
"""

from __future__ import annotations

import datetime as dt
import json
import re
from pathlib import Path
from typing import Any


REPO_ROOT = Path(__file__).resolve().parents[2]
DATA_ROOT = REPO_ROOT / "Game.Godot" / "Assets" / "Data"

BASE_MANIFEST = "Game.Godot/Assets/Data/content/base/manifest.json"
BASE_EVENTS = "Game.Godot/Assets/Data/content/base/guild_events.json"
BASE_TUNING = "Game.Godot/Assets/Data/content/base/tuning.json"
BASE_MEMBER_ARCHETYPES = "Game.Godot/Assets/Data/content/base/member_archetypes.json"
BASE_NPC_GUILDS = "Game.Godot/Assets/Data/content/base/npc_guilds.json"
BASE_RECRUIT_OFFERS = "Game.Godot/Assets/Data/content/base/recruit_offers.json"
BASE_RAID_ENCOUNTERS = "Game.Godot/Assets/Data/content/base/raid_encounters.json"
BASE_TACTICS = "Game.Godot/Assets/Data/content/base/tactics.json"
BASE_MEDIA_BEATS = "Game.Godot/Assets/Data/content/base/media_beats.json"
BASE_SOCIAL_INTERACTIONS = "Game.Godot/Assets/Data/content/base/social_interactions.json"


CONTENT_ID_RE = re.compile(r"^(Base|DLC1)_[A-Za-z0-9]+(?:_[A-Za-z0-9]+)*$")


def _today_ci_dir() -> Path:
    out_dir = REPO_ROOT / "logs" / "ci" / dt.date.today().isoformat() / "content-validation"
    out_dir.mkdir(parents=True, exist_ok=True)
    return out_dir


def _read_json(path: Path) -> tuple[Any | None, str | None]:
    try:
        text = path.read_text(encoding="utf-8")
    except UnicodeDecodeError as exc:
        return None, f"non_utf8: {exc}"
    except OSError as exc:
        return None, f"io_error: {exc}"
    try:
        return json.loads(text), None
    except Exception as exc:
        return None, f"json_parse_error: {exc}"


def _ensure_str(obj: dict[str, Any], key: str, issues: list[str]) -> str | None:
    v = obj.get(key)
    if not isinstance(v, str) or not v.strip():
        issues.append(f"{key}_invalid")
        return None
    return v.strip()


def _ensure_int(obj: dict[str, Any], key: str, issues: list[str], min_value: int | None = None) -> int | None:
    v = obj.get(key)
    if not isinstance(v, int):
        issues.append(f"{key}_invalid")
        return None
    if min_value is not None and v < min_value:
        issues.append(f"{key}_too_small")
        return None
    return v


def _ensure_num(obj: dict[str, Any], key: str, issues: list[str], lo: float | None = None, hi: float | None = None) -> float | None:
    v = obj.get(key)
    if not isinstance(v, (int, float)):
        issues.append(f"{key}_invalid")
        return None
    x = float(v)
    if lo is not None and x < lo:
        issues.append(f"{key}_too_small")
        return None
    if hi is not None and x > hi:
        issues.append(f"{key}_too_large")
        return None
    return x


def _validate_base_manifest(obj: Any) -> list[str]:
    issues: list[str] = []
    if not isinstance(obj, dict):
        return ["root_not_object"]
    _ensure_str(obj, "contentVersion", issues)
    pack_id = _ensure_str(obj, "packId", issues)
    if pack_id is not None and pack_id != "Base":
        issues.append("packId_expected_Base")
    prefix = _ensure_str(obj, "idNamespacePrefix", issues)
    if prefix is not None and prefix != "Base_":
        issues.append("idNamespacePrefix_expected_Base_")
    files = obj.get("files")
    if not isinstance(files, list) or not files:
        issues.append("files_missing_or_empty")
    else:
        required = {"guild_events.json", "tuning.json"}
        got = {str(x) for x in files}
        missing = sorted(required - got)
        if missing:
            issues.append("files_missing_required:" + ",".join(missing))
    return issues


def _validate_condition(cond: Any) -> list[str]:
    issues: list[str] = []
    if not isinstance(cond, dict):
        return ["condition_not_object"]
    t = cond.get("type")
    if not isinstance(t, str) or not t.strip():
        issues.append("type_invalid")
        return issues
    t = t.strip()
    if t == "RandomRoll":
        chance = cond.get("chance")
        if not isinstance(chance, (int, float)):
            issues.append("chance_invalid")
        else:
            c = float(chance)
            if c < 0.0 or c > 1.0:
                issues.append("chance_out_of_range_0_1")
    elif t in {"WeekAtLeast", "GuildMemberCountAtLeast", "GuildReputationAtLeast", "HasRoleCountAtLeast"}:
        v = cond.get("value")
        if not isinstance(v, int):
            issues.append("value_invalid")
    elif t == "FlagEquals":
        ref = cond.get("refId")
        if not isinstance(ref, str) or not ref.strip():
            issues.append("refId_invalid")
        val = cond.get("value")
        if not isinstance(val, (str, int, bool)):
            issues.append("value_invalid")
    else:
        issues.append("type_unknown:" + t)
    return issues


def _validate_effect(effect: Any) -> list[str]:
    issues: list[str] = []
    if not isinstance(effect, dict):
        return ["effect_not_object"]
    t = effect.get("type")
    if not isinstance(t, str) or not t.strip():
        issues.append("type_invalid")
        return issues
    t = t.strip()
    if t in {"AdjustGuildReputation", "AdjustMemberMorale"}:
        if "amount" not in effect or not isinstance(effect.get("amount"), int):
            issues.append("amount_invalid")
    elif t in {"AddMember", "RemoveMember", "ChangeMemberRole"}:
        if "refId" not in effect or not isinstance(effect.get("refId"), str):
            issues.append("refId_invalid")
    elif t == "AddResource":
        if "refId" not in effect or not isinstance(effect.get("refId"), str):
            issues.append("refId_invalid")
        if "amount" not in effect or not isinstance(effect.get("amount"), int):
            issues.append("amount_invalid")
    elif t == "QueueMail":
        if "refId" not in effect or not isinstance(effect.get("refId"), str):
            issues.append("refId_invalid")
    elif t == "ScheduleRaid":
        if "refId" not in effect or not isinstance(effect.get("refId"), str):
            issues.append("refId_invalid")
    elif t == "SetFlag":
        if "refId" not in effect or not isinstance(effect.get("refId"), str):
            issues.append("refId_invalid")
        if "value" not in effect:
            issues.append("value_missing")
    else:
        issues.append("type_unknown:" + t)
    return issues


def _validate_base_guild_events(obj: Any) -> list[str]:
    issues: list[str] = []
    if not isinstance(obj, dict):
        return ["root_not_object"]
    _ensure_str(obj, "contentVersion", issues)
    evs = obj.get("guildEvents")
    if not isinstance(evs, list) or not evs:
        issues.append("guildEvents_missing_or_empty")
        return issues
    for i, e in enumerate(evs):
        if not isinstance(e, dict):
            issues.append(f"guildEvents[{i}]_not_object")
            continue
        cid = e.get("id")
        if not isinstance(cid, str) or not cid.strip() or not CONTENT_ID_RE.match(cid.strip()):
            issues.append(f"guildEvents[{i}].id_invalid")
        if not isinstance(e.get("nameKey"), str) or not str(e.get("nameKey")).strip():
            issues.append(f"guildEvents[{i}].nameKey_invalid")
        if not isinstance(e.get("category"), str) or not str(e.get("category")).strip():
            issues.append(f"guildEvents[{i}].category_invalid")
        if not isinstance(e.get("weight"), int) or e.get("weight") < 0:
            issues.append(f"guildEvents[{i}].weight_invalid")
        if "cooldownWeeks" in e and (not isinstance(e.get("cooldownWeeks"), int) or e.get("cooldownWeeks") < 0):
            issues.append(f"guildEvents[{i}].cooldownWeeks_invalid")
        conds = e.get("conditions")
        if not isinstance(conds, list):
            issues.append(f"guildEvents[{i}].conditions_not_list")
        else:
            for j, c in enumerate(conds):
                for ci in _validate_condition(c):
                    issues.append(f"guildEvents[{i}].conditions[{j}].{ci}")
        effs = e.get("effects")
        if not isinstance(effs, list):
            issues.append(f"guildEvents[{i}].effects_not_list")
        else:
            for j, c in enumerate(effs):
                for ci in _validate_effect(c):
                    issues.append(f"guildEvents[{i}].effects[{j}].{ci}")
    return issues


def _validate_member_archetypes(obj: Any) -> list[str]:
    issues: list[str] = []
    if not isinstance(obj, dict):
        return ["root_not_object"]
    _ensure_str(obj, "contentVersion", issues)
    items = obj.get("memberArchetypes")
    if not isinstance(items, list) or not items:
        issues.append("memberArchetypes_missing_or_empty")
        return issues
    for i, it in enumerate(items):
        if not isinstance(it, dict):
            issues.append(f"memberArchetypes[{i}]_not_object")
            continue
        cid = it.get("id")
        if not isinstance(cid, str) or not cid.strip() or not CONTENT_ID_RE.match(cid.strip()):
            issues.append(f"memberArchetypes[{i}].id_invalid")
        if not isinstance(it.get("nameKey"), str) or not str(it.get("nameKey")).strip():
            issues.append(f"memberArchetypes[{i}].nameKey_invalid")
        role = it.get("role")
        if role not in {"Tank", "Healer", "DPS"}:
            issues.append(f"memberArchetypes[{i}].role_invalid")
        tags = it.get("personalityTags")
        if tags is not None and not isinstance(tags, list):
            issues.append(f"memberArchetypes[{i}].personalityTags_invalid")
        ratings = it.get("baseRatings")
        if ratings is not None and not isinstance(ratings, dict):
            issues.append(f"memberArchetypes[{i}].baseRatings_invalid")
    return issues


def _validate_tactics(obj: Any) -> list[str]:
    issues: list[str] = []
    if not isinstance(obj, dict):
        return ["root_not_object"]
    _ensure_str(obj, "contentVersion", issues)
    items = obj.get("tactics")
    if not isinstance(items, list) or not items:
        issues.append("tactics_missing_or_empty")
        return issues
    for i, it in enumerate(items):
        if not isinstance(it, dict):
            issues.append(f"tactics[{i}]_not_object")
            continue
        cid = it.get("id")
        if not isinstance(cid, str) or not cid.strip() or not CONTENT_ID_RE.match(cid.strip()):
            issues.append(f"tactics[{i}].id_invalid")
        if not isinstance(it.get("nameKey"), str) or not str(it.get("nameKey")).strip():
            issues.append(f"tactics[{i}].nameKey_invalid")
        mods = it.get("modifiers")
        if mods is not None and not isinstance(mods, dict):
            issues.append(f"tactics[{i}].modifiers_invalid")
    return issues


def _validate_npc_guilds(obj: Any) -> list[str]:
    issues: list[str] = []
    if not isinstance(obj, dict):
        return ["root_not_object"]
    _ensure_str(obj, "contentVersion", issues)
    items = obj.get("npcGuildArchetypes")
    if not isinstance(items, list) or not items:
        issues.append("npcGuildArchetypes_missing_or_empty")
        return issues
    for i, it in enumerate(items):
        if not isinstance(it, dict):
            issues.append(f"npcGuildArchetypes[{i}]_not_object")
            continue
        cid = it.get("id")
        if not isinstance(cid, str) or not cid.strip() or not CONTENT_ID_RE.match(cid.strip()):
            issues.append(f"npcGuildArchetypes[{i}].id_invalid")
        if not isinstance(it.get("nameKey"), str) or not str(it.get("nameKey")).strip():
            issues.append(f"npcGuildArchetypes[{i}].nameKey_invalid")
        prefs = it.get("recruitmentPreferences")
        if not isinstance(prefs, dict):
            issues.append(f"npcGuildArchetypes[{i}].recruitmentPreferences_invalid")
        else:
            for k in ("tank", "healer", "dps"):
                if k in prefs and isinstance(prefs[k], (int, float)):
                    v = float(prefs[k])
                    if v < 0.0 or v > 1.0:
                        issues.append(f"npcGuildArchetypes[{i}].recruitmentPreferences.{k}_out_of_range_0_1")
        tactics = it.get("tacticPreferences")
        if tactics is not None and not isinstance(tactics, list):
            issues.append(f"npcGuildArchetypes[{i}].tacticPreferences_invalid")
    return issues


def _validate_range_int(obj: Any, path: str, issues: list[str]) -> None:
    if not isinstance(obj, dict):
        issues.append(f"{path}_invalid")
        return
    mn = obj.get("min")
    mx = obj.get("max")
    if not isinstance(mn, int) or not isinstance(mx, int):
        issues.append(f"{path}.minmax_invalid")
        return
    if mn > mx:
        issues.append(f"{path}.min_greater_than_max")


def _validate_raid_encounters(obj: Any) -> list[str]:
    issues: list[str] = []
    if not isinstance(obj, dict):
        return ["root_not_object"]
    _ensure_str(obj, "contentVersion", issues)
    items = obj.get("raidEncounters")
    if not isinstance(items, list) or not items:
        issues.append("raidEncounters_missing_or_empty")
        return issues
    for i, it in enumerate(items):
        if not isinstance(it, dict):
            issues.append(f"raidEncounters[{i}]_not_object")
            continue
        cid = it.get("id")
        if not isinstance(cid, str) or not cid.strip() or not CONTENT_ID_RE.match(cid.strip()):
            issues.append(f"raidEncounters[{i}].id_invalid")
        if not isinstance(it.get("nameKey"), str) or not str(it.get("nameKey")).strip():
            issues.append(f"raidEncounters[{i}].nameKey_invalid")
        rr = it.get("recommendedRoleRatio")
        if rr is not None and not isinstance(rr, dict):
            issues.append(f"raidEncounters[{i}].recommendedRoleRatio_invalid")
        for rk in ("successReputationDelta", "failMoraleDelta"):
            rv = it.get(rk)
            if rv is not None:
                _validate_range_int(rv, f"raidEncounters[{i}].{rk}", issues)
    return issues


def _validate_media_beats(obj: Any) -> list[str]:
    issues: list[str] = []
    if not isinstance(obj, dict):
        return ["root_not_object"]
    _ensure_str(obj, "contentVersion", issues)
    items = obj.get("mediaBeats")
    if not isinstance(items, list) or not items:
        issues.append("mediaBeats_missing_or_empty")
        return issues
    for i, it in enumerate(items):
        if not isinstance(it, dict):
            issues.append(f"mediaBeats[{i}]_not_object")
            continue
        cid = it.get("id")
        if not isinstance(cid, str) or not cid.strip() or not CONTENT_ID_RE.match(cid.strip()):
            issues.append(f"mediaBeats[{i}].id_invalid")
        if not isinstance(it.get("nameKey"), str) or not str(it.get("nameKey")).strip():
            issues.append(f"mediaBeats[{i}].nameKey_invalid")
        if not isinstance(it.get("weight"), int) or it.get("weight") < 0:
            issues.append(f"mediaBeats[{i}].weight_invalid")
        _validate_range_int(it.get("reputationDelta"), f"mediaBeats[{i}].reputationDelta", issues)
    return issues


def _validate_social_interactions(obj: Any) -> list[str]:
    issues: list[str] = []
    if not isinstance(obj, dict):
        return ["root_not_object"]
    _ensure_str(obj, "contentVersion", issues)
    items = obj.get("socialInteractions")
    if not isinstance(items, list) or not items:
        issues.append("socialInteractions_missing_or_empty")
        return issues
    for i, it in enumerate(items):
        if not isinstance(it, dict):
            issues.append(f"socialInteractions[{i}]_not_object")
            continue
        cid = it.get("id")
        if not isinstance(cid, str) or not cid.strip() or not CONTENT_ID_RE.match(cid.strip()):
            issues.append(f"socialInteractions[{i}].id_invalid")
        if not isinstance(it.get("nameKey"), str) or not str(it.get("nameKey")).strip():
            issues.append(f"socialInteractions[{i}].nameKey_invalid")
        if not isinstance(it.get("weight"), int) or it.get("weight") < 0:
            issues.append(f"socialInteractions[{i}].weight_invalid")
        effs = it.get("effects")
        if effs is not None:
            if not isinstance(effs, list):
                issues.append(f"socialInteractions[{i}].effects_not_list")
            else:
                for j, e in enumerate(effs):
                    for ei in _validate_effect(e):
                        issues.append(f"socialInteractions[{i}].effects[{j}].{ei}")
    return issues


def _validate_recruit_offers(obj: Any) -> list[str]:
    issues: list[str] = []
    if not isinstance(obj, dict):
        return ["root_not_object"]
    _ensure_str(obj, "contentVersion", issues)
    items = obj.get("recruitOffers")
    if not isinstance(items, list) or not items:
        issues.append("recruitOffers_missing_or_empty")
        return issues
    for i, it in enumerate(items):
        if not isinstance(it, dict):
            issues.append(f"recruitOffers[{i}]_not_object")
            continue
        cid = it.get("id")
        if not isinstance(cid, str) or not cid.strip() or not CONTENT_ID_RE.match(cid.strip()):
            issues.append(f"recruitOffers[{i}].id_invalid")
        if not isinstance(it.get("nameKey"), str) or not str(it.get("nameKey")).strip():
            issues.append(f"recruitOffers[{i}].nameKey_invalid")
        role = it.get("role")
        if role not in {"Tank", "Healer", "DPS"}:
            issues.append(f"recruitOffers[{i}].role_invalid")
        if not isinstance(it.get("difficultyTier"), int) or it.get("difficultyTier") < 1:
            issues.append(f"recruitOffers[{i}].difficultyTier_invalid")
        _ensure_num(it, "baseSuccessChance", issues, lo=0.0, hi=1.0)
        for k in ("effectsOnSuccess", "effectsOnFail"):
            v = it.get(k)
            if v is not None and not isinstance(v, list):
                issues.append(f"recruitOffers[{i}].{k}_not_list")
            elif isinstance(v, list):
                for j, e in enumerate(v):
                    for ei in _validate_effect(e):
                        issues.append(f"recruitOffers[{i}].{k}[{j}].{ei}")
    return issues


def _validate_ratio_map(obj: Any, key: str, issues: list[str]) -> None:
    v = obj.get(key)
    if not isinstance(v, dict):
        issues.append(f"{key}_invalid")
        return
    for k in ("tank", "healer", "dps"):
        if k not in v:
            issues.append(f"{key}.missing:{k}")
            continue
        if not isinstance(v[k], (int, float)):
            issues.append(f"{key}.{k}_invalid")
            continue
        x = float(v[k])
        if x < 0.0 or x > 1.0:
            issues.append(f"{key}.{k}_out_of_range_0_1")


def _validate_base_tuning(obj: Any) -> list[str]:
    issues: list[str] = []
    if not isinstance(obj, dict):
        return ["root_not_object"]
    _ensure_str(obj, "contentVersion", issues)

    g = obj.get("global")
    if not isinstance(g, dict):
        issues.append("global_invalid")
    else:
        _ensure_num(g, "uiRefreshSeconds", issues, lo=0.05, hi=2.0)
        if "percentAsDecimal" not in g or not isinstance(g.get("percentAsDecimal"), bool):
            issues.append("global.percentAsDecimal_invalid")

    turn = obj.get("turn")
    if not isinstance(turn, dict):
        issues.append("turn_invalid")
    else:
        _ensure_int(turn, "weeksPerTurn", issues, min_value=1)
        ff = turn.get("fastForward")
        if not isinstance(ff, dict):
            issues.append("turn.fastForward_invalid")
        else:
            _ensure_int(ff, "maxWeeks", issues, min_value=1)
            _ensure_int(ff, "weekChunk", issues, min_value=1)

    events = obj.get("events")
    if not isinstance(events, dict):
        issues.append("events_invalid")
    else:
        _ensure_int(events, "defaultWeight", issues, min_value=0)
        _ensure_int(events, "defaultCooldownWeeks", issues, min_value=0)

    recruitment = obj.get("recruitment")
    if not isinstance(recruitment, dict):
        issues.append("recruitment_invalid")
    else:
        _ensure_num(recruitment, "baseSuccessChance", issues, lo=0.0, hi=1.0)
        _ensure_int(recruitment, "negotiationMaxRounds", issues, min_value=1)
        _ensure_int(recruitment, "cooldownWeeks", issues, min_value=0)

    roster = obj.get("roster")
    if not isinstance(roster, dict):
        issues.append("roster_invalid")
    else:
        _ensure_int(roster, "maxMembersSoft", issues, min_value=1)
        _validate_ratio_map(roster, "roleRatioTargets", issues)

    ai = obj.get("ai")
    if not isinstance(ai, dict):
        issues.append("ai_invalid")
    else:
        _ensure_int(ai, "actionBudgetPerTurn", issues, min_value=0)

    raid = obj.get("raid")
    if not isinstance(raid, dict):
        issues.append("raid_invalid")
    else:
        _ensure_num(raid, "baseSuccessChance", issues, lo=0.0, hi=1.0)
        _validate_range_int(raid.get("rewardReputation"), "raid.rewardReputation", issues)
        _validate_range_int(raid.get("penaltyMorale"), "raid.penaltyMorale", issues)

    media = obj.get("media")
    if not isinstance(media, dict):
        issues.append("media_invalid")
    else:
        _validate_range_int(media.get("postsPerTurn"), "media.postsPerTurn", issues)
        _validate_range_int(media.get("reputationDeltaPerBeat"), "media.reputationDeltaPerBeat", issues)

    return issues


def main() -> int:
    out_dir = _today_ci_dir()

    report: dict[str, Any] = {
        "ts": dt.datetime.now().replace(microsecond=0).isoformat(),
        "data_root": str(DATA_ROOT),
        "files": [],
        "status": "ok",
        "errors": 0,
    }

    if not DATA_ROOT.exists():
        report["status"] = "fail"
        report["errors"] += 1
        report["files"].append({"path": str(DATA_ROOT), "error": "missing_data_root"})
    else:
        json_files = sorted([p for p in DATA_ROOT.rglob("*.json") if p.is_file()])
        for p in json_files:
            obj, err = _read_json(p)
            rel = str(p.relative_to(REPO_ROOT)).replace("\\", "/")
            entry: dict[str, Any] = {"path": rel, "status": "ok", "issues": []}
            if err is not None:
                entry["status"] = "fail"
                entry["issues"].append(err)
                report["errors"] += 1
                report["files"].append(entry)
                continue

            # Targeted validations for base pack
            if rel == BASE_MANIFEST:
                issues = _validate_base_manifest(obj)
                if issues:
                    entry["status"] = "fail"
                    entry["issues"].extend(issues)
                    report["errors"] += 1
            if rel == BASE_EVENTS:
                issues = _validate_base_guild_events(obj)
                if issues:
                    entry["status"] = "fail"
                    entry["issues"].extend(issues)
                    report["errors"] += 1
            if rel == BASE_TUNING:
                issues = _validate_base_tuning(obj)
                if issues:
                    entry["status"] = "fail"
                    entry["issues"].extend(issues)
                    report["errors"] += 1
            if rel == BASE_MEMBER_ARCHETYPES:
                issues = _validate_member_archetypes(obj)
                if issues:
                    entry["status"] = "fail"
                    entry["issues"].extend(issues)
                    report["errors"] += 1
            if rel == BASE_NPC_GUILDS:
                issues = _validate_npc_guilds(obj)
                if issues:
                    entry["status"] = "fail"
                    entry["issues"].extend(issues)
                    report["errors"] += 1
            if rel == BASE_RECRUIT_OFFERS:
                issues = _validate_recruit_offers(obj)
                if issues:
                    entry["status"] = "fail"
                    entry["issues"].extend(issues)
                    report["errors"] += 1
            if rel == BASE_RAID_ENCOUNTERS:
                issues = _validate_raid_encounters(obj)
                if issues:
                    entry["status"] = "fail"
                    entry["issues"].extend(issues)
                    report["errors"] += 1
            if rel == BASE_TACTICS:
                issues = _validate_tactics(obj)
                if issues:
                    entry["status"] = "fail"
                    entry["issues"].extend(issues)
                    report["errors"] += 1
            if rel == BASE_MEDIA_BEATS:
                issues = _validate_media_beats(obj)
                if issues:
                    entry["status"] = "fail"
                    entry["issues"].extend(issues)
                    report["errors"] += 1
            if rel == BASE_SOCIAL_INTERACTIONS:
                issues = _validate_social_interactions(obj)
                if issues:
                    entry["status"] = "fail"
                    entry["issues"].extend(issues)
                    report["errors"] += 1

            report["files"].append(entry)

        # Ensure required files exist (hard requirement)
        required = {
            BASE_MANIFEST,
            BASE_EVENTS,
            BASE_TUNING,
            BASE_MEMBER_ARCHETYPES,
            BASE_NPC_GUILDS,
            BASE_RECRUIT_OFFERS,
            BASE_RAID_ENCOUNTERS,
            BASE_TACTICS,
            BASE_MEDIA_BEATS,
            BASE_SOCIAL_INTERACTIONS,
        }
        present = {f["path"] for f in report["files"] if isinstance(f, dict) and "path" in f}
        missing = sorted(required - present)
        for m in missing:
            report["errors"] += 1
            report["files"].append({"path": m, "status": "fail", "issues": ["missing_required_file"]})

    if report["errors"] > 0:
        report["status"] = "fail"

    out_json = out_dir / "report.json"
    out_txt = out_dir / "report.txt"
    out_json.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")

    lines = [
        f"ts={report['ts']}",
        f"status={report['status']}",
        f"errors={report['errors']}",
        f"data_root={report['data_root']}",
        "",
    ]
    for f in report["files"]:
        if f.get("status") == "fail":
            lines.append(f"FAIL {f['path']}: {', '.join(f.get('issues', []))}")
    out_txt.write_text("\n".join(lines) + "\n", encoding="utf-8")

    print(f"[REPORT] {out_json}")
    print(f"[REPORT] {out_txt}")
    print(f"[{ 'OK' if report['status']=='ok' else 'FAIL' }] content_validation errors={report['errors']}")
    return 0 if report["status"] == "ok" else 1


if __name__ == "__main__":
    raise SystemExit(main())
