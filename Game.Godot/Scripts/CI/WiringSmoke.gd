extends Node

const HUD_SCENE_PATH := "res://Game.Godot/Scenes/UI/HUD.tscn"
const GUILD_PANEL_SCENE_PATH := "res://Game.Godot/Scenes/UI/GuildPanel.tscn"

const EVT_GUILD_CREATED := "core.guild.created"
const EVT_GUILD_MEMBER_JOINED := "core.guild.member.joined"
const EVT_RECRUITMENT_OFFER_PRESENTED := "core.recruitment.offer.presented"
const EVT_RECRUITMENT_OFFER_RESOLVED := "core.recruitment.offer.resolved"
const EVT_REPUTATION_CHANGED := "core.reputation.changed"
const EVT_RAID_RESOLVED := "core.raid.resolved"
const EVT_MEDIA_BEAT_TRIGGERED := "core.media.beat.triggered"

const PATH_GUILD_NAME_INPUT := NodePath("Scroll/Margin/VBox/GuildInfo/GuildNameRow/GuildNameInput")
const PATH_CREATE_GUILD_BUTTON := NodePath("Scroll/Margin/VBox/Actions/CreateGuildButton")
const PATH_USER_ID_INPUT := NodePath("Scroll/Margin/VBox/RosterActions/UserIdRow/UserIdInput")
const PATH_JOIN_BUTTON := NodePath("Scroll/Margin/VBox/RosterActions/MemberActionsRow/JoinButton")
const PATH_MEMBERS_LIST := NodePath("Scroll/Margin/VBox/MembersList")
const PATH_CANDIDATE_ID_INPUT := NodePath("Scroll/Margin/VBox/RecruitmentSection/CandidateIdRow/CandidateIdInput")
const PATH_APPLY_BUTTON := NodePath("Scroll/Margin/VBox/RecruitmentSection/RecruitmentActionsRow/ApplyButton")
const PATH_APPROVE_BUTTON := NodePath("Scroll/Margin/VBox/RecruitmentSection/RecruitmentActionsRow/ApproveButton")
const PATH_OFFERS_LIST := NodePath("Scroll/Margin/VBox/RecruitmentSection/OffersList")

var _event_types: Array[String] = []

func _ready() -> void:
	call_deferred("_run")

func _on_evt(type: String, _source: String, _data_json: String, _id: String, _spec: String, _ct: String, _ts: String) -> void:
	_event_types.append(str(type))

func _count_type(wanted: String) -> int:
	var c := 0
	for t in _event_types:
		if t == wanted:
			c += 1
	return c

func _wait_for_type(wanted: String, min_count: int, frames: int = 240) -> bool:
	for _i in range(frames):
		if _count_type(wanted) >= min_count:
			return true
		await get_tree().process_frame
	return false

func _fail(reason: String) -> void:
	push_error("[WIRING_SMOKE_FAIL] " + reason)
	get_tree().quit(1)

func _require(cond: bool, reason: String) -> void:
	if not cond:
		_fail(reason)

func _run() -> void:
	print("[WIRING_SMOKE] start")

	var bus = get_node_or_null("/root/EventBus")
	_require(bus != null, "EventBus not found at /root/EventBus")
	_require(bus.has_signal("DomainEventEmitted"), "EventBus missing DomainEventEmitted signal")
	_require(bus.has_method("PublishSimple"), "EventBus missing PublishSimple(type, source, dataJson)")

	var cb := Callable(self, "_on_evt")
	if not bus.is_connected("DomainEventEmitted", cb):
		bus.connect("DomainEventEmitted", cb)

	var logger = get_node_or_null("/root/Logger")
	_require(logger != null, "Logger not found at /root/Logger")
	if logger.has_method("Info"):
		logger.Info("Wiring smoke: logger ok")

	# Ensure demo gates are enabled for headless wiring smoke (must be set via process env in CI).
	OS.set_environment("GD_ENABLE_PLAYABLE", "1")
	OS.set_environment("SECURITY_TEST_MODE", "1")

	_require(ResourceLoader.exists(HUD_SCENE_PATH), "HUD scene missing: " + HUD_SCENE_PATH)
	var hud = preload(HUD_SCENE_PATH).instantiate()
	get_tree().get_root().add_child(hud)
	await get_tree().process_frame

	_require(hud.has_node("TopBar/HBox/ReputationLabel"), "HUD missing ReputationLabel")
	_require(hud.has_node("TopBar/HBox/MediaBeatLabel"), "HUD missing MediaBeatLabel")
	var rep_label: Label = hud.get_node("TopBar/HBox/ReputationLabel")
	var media_label: Label = hud.get_node("TopBar/HBox/MediaBeatLabel")
	var rep_before := str(rep_label.text)
	var media_before := str(media_label.text)

	bus.PublishSimple(EVT_REPUTATION_CHANGED, "wiring", "{\"newValue\":42}")
	var ok_rep := await _wait_for_type(EVT_REPUTATION_CHANGED, 1, 120)
	_require(ok_rep, "Did not observe event: " + EVT_REPUTATION_CHANGED)
	await get_tree().process_frame
	_require(str(rep_label.text) != rep_before and str(rep_label.text).find("42") >= 0, "HUD reputation not updated/observable")

	_require(hud.has_method("TriggerRaidEncounterDemo"), "HUD missing TriggerRaidEncounterDemo()")
	hud.TriggerRaidEncounterDemo()
	var ok_raid := await _wait_for_type(EVT_RAID_RESOLVED, 1, 240)
	_require(ok_raid, "Did not observe event: " + EVT_RAID_RESOLVED)

	_require(hud.has_method("TriggerMediaBeatDemo"), "HUD missing TriggerMediaBeatDemo()")
	hud.TriggerMediaBeatDemo()
	var ok_media := await _wait_for_type(EVT_MEDIA_BEAT_TRIGGERED, 1, 240)
	_require(ok_media, "Did not observe event: " + EVT_MEDIA_BEAT_TRIGGERED)
	await get_tree().process_frame
	_require(str(media_label.text) != media_before, "HUD media beat not updated/observable")

	_require(ResourceLoader.exists(GUILD_PANEL_SCENE_PATH), "GuildPanel scene missing: " + GUILD_PANEL_SCENE_PATH)
	var panel = preload(GUILD_PANEL_SCENE_PATH).instantiate()
	get_tree().get_root().add_child(panel)
	await get_tree().process_frame

	_require(panel.has_node(PATH_GUILD_NAME_INPUT), "GuildPanel missing GuildNameInput")
	_require(panel.has_node(PATH_CREATE_GUILD_BUTTON), "GuildPanel missing CreateGuildButton")
	var guild_name_input: LineEdit = panel.get_node(PATH_GUILD_NAME_INPUT)
	guild_name_input.text = "WiringGuild"
	var create_btn: Button = panel.get_node(PATH_CREATE_GUILD_BUTTON)
	create_btn.pressed.emit()
	var ok_guild := await _wait_for_type(EVT_GUILD_CREATED, 1, 240)
	_require(ok_guild, "Did not observe event: " + EVT_GUILD_CREATED)

	_require(panel.has_node(PATH_USER_ID_INPUT), "GuildPanel missing UserIdInput")
	_require(panel.has_node(PATH_JOIN_BUTTON), "GuildPanel missing JoinButton")
	_require(panel.has_node(PATH_MEMBERS_LIST), "GuildPanel missing MembersList")
	var user_input: LineEdit = panel.get_node(PATH_USER_ID_INPUT)
	var join_btn: Button = panel.get_node(PATH_JOIN_BUTTON)
	var members_list: ItemList = panel.get_node(PATH_MEMBERS_LIST)
	user_input.text = "npc-0001"
	join_btn.pressed.emit()
	var ok_join := await _wait_for_type(EVT_GUILD_MEMBER_JOINED, 1, 240)
	_require(ok_join, "Did not observe event: " + EVT_GUILD_MEMBER_JOINED)
	await get_tree().process_frame
	_require(members_list.item_count >= 1, "GuildPanel members list not updated")

	_require(panel.has_node(PATH_CANDIDATE_ID_INPUT), "GuildPanel missing CandidateIdInput")
	_require(panel.has_node(PATH_APPLY_BUTTON), "GuildPanel missing ApplyButton")
	_require(panel.has_node(PATH_APPROVE_BUTTON), "GuildPanel missing ApproveButton")
	_require(panel.has_node(PATH_OFFERS_LIST), "GuildPanel missing OffersList")
	var candidate_input: LineEdit = panel.get_node(PATH_CANDIDATE_ID_INPUT)
	var apply_btn: Button = panel.get_node(PATH_APPLY_BUTTON)
	var approve_btn: Button = panel.get_node(PATH_APPROVE_BUTTON)
	var offers_list: ItemList = panel.get_node(PATH_OFFERS_LIST)
	candidate_input.text = "cand-0001"
	apply_btn.pressed.emit()
	var ok_offer := await _wait_for_type(EVT_RECRUITMENT_OFFER_PRESENTED, 1, 240)
	_require(ok_offer, "Did not observe event: " + EVT_RECRUITMENT_OFFER_PRESENTED)
	await get_tree().process_frame
	_require(offers_list.item_count >= 1, "Recruitment offers list not updated")
	offers_list.select(0)
	approve_btn.pressed.emit()
	var ok_resolved := await _wait_for_type(EVT_RECRUITMENT_OFFER_RESOLVED, 1, 240)
	_require(ok_resolved, "Did not observe event: " + EVT_RECRUITMENT_OFFER_RESOLVED)

	var ds = get_node_or_null("/root/DataStore")
	_require(ds != null, "DataStore not found at /root/DataStore")
	var key := "wiring_smoke_save"
	var json_payload := "{\"ts\":%d}" % int(Time.get_unix_time_from_system())
	var saved_ok := true
	if ds.has_method("TrySaveSync"):
		saved_ok = bool(ds.TrySaveSync(key, json_payload))
	elif ds.has_method("SaveSync"):
		ds.SaveSync(key, json_payload)
	else:
		saved_ok = false
	_require(saved_ok, "DataStore save failed")

	var loaded = null
	if ds.has_method("TryLoadSync"):
		loaded = ds.TryLoadSync(key)
	elif ds.has_method("LoadSync"):
		loaded = ds.LoadSync(key)
	_require(loaded != null, "DataStore load returned null")

	print("[WIRING_SMOKE_READY] Wiring smoke passed")
	get_tree().quit(0)

