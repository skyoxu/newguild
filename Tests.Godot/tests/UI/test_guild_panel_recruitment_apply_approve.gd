extends "res://addons/gdUnit4/src/GdUnitTestSuite.gd"

# Scaffold-only GdUnit4 suite for T14 Recruitment UI flow.
# Goals:
# - Always compile/run safely.
# - Provide stable assertions on contract EventType strings.
# - Drive real UI flow (missing controls must fail).

var _bus: Node
var _session: Node
var _guild_manager: Node
var _logger: Node
var _emitted_types: Array[String] = []

func _new_csharp_node(script_path: String, node_name: String) -> Node:
	var s = load(script_path)
	if s == null or not s.has_method("new"):
		push_warning("SKIP: CSharpScript.new() unavailable: %s" % script_path)
		return null
	var n = s.new()
	n.name = node_name
	return n

func before() -> void:
	_emitted_types.clear()
	var suffix := str(Time.get_unix_time_from_system()) + "-" + str(randi() % 1000000)
	OS.set_environment("GD_GUILD_DB_PATH", "gdunit/gdunit-recruitment-" + suffix + ".db")
	OS.set_environment("SECURITY_TEST_MODE", "1")

	_bus = get_node_or_null("/root/EventBus")
	assert_object(_bus).is_not_null()
	var cb := Callable(self, "_on_evt")
	if not _bus.is_connected("DomainEventEmitted", cb):
		_bus.connect("DomainEventEmitted", cb)

	_session = _new_csharp_node("res://Game.Godot/Scripts/Autoload/PlayerSession.cs", "PlayerSession")
	assert_object(_session).is_not_null()
	get_tree().get_root().add_child(auto_free(_session))

	_guild_manager = _new_csharp_node("res://Game.Godot/Scripts/Autoload/GuildManager.cs", "GuildManager")
	assert_object(_guild_manager).is_not_null()
	get_tree().get_root().add_child(auto_free(_guild_manager))

	await get_tree().process_frame

func _on_evt(type: String, _source: String, _data_json: String, _id: String, _spec: String, _ct: String, _ts: String) -> void:
	_emitted_types.append(type)

func _contains_type_emitted(type: String) -> bool:
	for t in _emitted_types:
		if t == type:
			return true
	return false

func _await_until(pred: Callable, frames: int = 180) -> void:
	for _i in range(frames):
		if pred.call():
			return
		await get_tree().process_frame
	assert_bool(pred.call()).is_true()

func _read_res_text(path: String) -> String:
	if not ResourceLoader.exists(path):
		return ""
	var f := FileAccess.open(path, FileAccess.READ)
	if f == null:
		return ""
	var txt := f.get_as_text()
	f.close()
	return txt

func _guild_screen() -> Node:
	assert_bool(ResourceLoader.exists("res://Game.Godot/Scenes/Screens/GuildScreen.tscn")).is_true()
	var screen := preload("res://Game.Godot/Scenes/Screens/GuildScreen.tscn").instantiate()
	add_child(auto_free(screen))
	await get_tree().process_frame
	return screen

func _ensure_guild_created(panel: Node) -> void:
	var create_button: Button = panel.get_node_or_null("VBox/Actions/CreateGuildButton")
	var members_list: ItemList = panel.get_node_or_null("VBox/MembersList")
	assert_object(create_button).is_not_null()
	assert_object(members_list).is_not_null()
	if create_button.disabled:
		return
	create_button.pressed.emit()
	await _await_until(func() -> bool: return members_list.item_count >= 1 and create_button.disabled)

func _try_find_recruitment_controls(panel: Node) -> Dictionary:
	var root := panel.find_child("RecruitmentSection", true, false)
	assert_object(root).is_not_null()

	var apply_button := root.find_child("ApplyButton", true, false)
	assert_object(apply_button).is_not_null()

	var approve_button := root.find_child("ApproveButton", true, false)
	assert_object(approve_button).is_not_null()

	var reject_button := root.find_child("RejectButton", true, false)
	assert_object(reject_button).is_not_null()

	var candidate_input := root.find_child("CandidateIdInput", true, false)
	assert_object(candidate_input).is_not_null()

	var offer_id_input := root.find_child("OfferIdInput", true, false)
	assert_object(offer_id_input).is_not_null()
	var offers_list := root.find_child("OffersList", true, false)
	assert_object(offers_list).is_not_null()

	return {
		"root": root,
		"apply_button": apply_button,
		"approve_button": approve_button,
		"reject_button": reject_button,
		"candidate_input": candidate_input,
		"offer_id_input": offer_id_input,
		"offers_list": offers_list,
	}

func _parse_offer_id(text: String) -> String:
	var idx := text.find(" | ")
	if idx < 0:
		return text.strip_edges()
	return text.substr(0, idx).strip_edges()

# ACC:T14.3
func test_recruitment_apply_contract_and_optional_ui_smoke() -> void:
	var presented_cs := _read_res_text("res://Game.Core/Contracts/Recruitment/RecruitmentOfferPresented.cs")
	assert_bool(presented_cs.length() > 0).is_true()
	assert_str(presented_cs).contains("core.recruitment.offer.presented")

	var screen := await _guild_screen()
	var panel: Node = screen.get_node_or_null("Scroll/GuildPanel")
	assert_object(panel).is_not_null()

	await _ensure_guild_created(panel)

	var controls := _try_find_recruitment_controls(panel)
	var apply_button = controls.get("apply_button")
	assert_bool(apply_button is Button).is_true()
	assert_bool((apply_button as Button).disabled).is_false()

	var candidate_input = controls.get("candidate_input")
	assert_bool(candidate_input is LineEdit).is_true()
	(candidate_input as LineEdit).text = "u3"

	_emitted_types.clear()
	(apply_button as Button).pressed.emit()
	await _await_until(func() -> bool: return _contains_type_emitted("core.recruitment.offer.presented"))

# ACC:T14.4
func test_recruitment_approve_contract_and_optional_ui_smoke() -> void:
	var resolved_cs := _read_res_text("res://Game.Core/Contracts/Recruitment/RecruitmentOfferResolved.cs")
	assert_bool(resolved_cs.length() > 0).is_true()
	assert_str(resolved_cs).contains("core.recruitment.offer.resolved")

	var screen := await _guild_screen()
	var panel: Node = screen.get_node_or_null("Scroll/GuildPanel")
	assert_object(panel).is_not_null()

	await _ensure_guild_created(panel)

	var controls := _try_find_recruitment_controls(panel)
	var offers_list = controls.get("offers_list")
	assert_bool(offers_list is ItemList).is_true()

	var candidate_input = controls.get("candidate_input")
	assert_bool(candidate_input is LineEdit).is_true()
	(candidate_input as LineEdit).text = "u2"

	var apply_button = controls.get("apply_button")
	assert_bool(apply_button is Button).is_true()
	(apply_button as Button).pressed.emit()
	await _await_until(func() -> bool: return _contains_type_emitted("core.recruitment.offer.presented"))
	await _await_until(func() -> bool: return (offers_list as ItemList).item_count >= 1)

	var approve_button = controls.get("approve_button")
	assert_bool(approve_button is Button).is_true()
	assert_bool((approve_button as Button).disabled).is_false()

	var offer_id_input = controls.get("offer_id_input")
	assert_bool(offer_id_input is LineEdit).is_true()
	var first_text := (offers_list as ItemList).get_item_text(0)
	(offer_id_input as LineEdit).text = _parse_offer_id(first_text)

	_emitted_types.clear()
	(approve_button as Button).pressed.emit()
	await _await_until(func() -> bool: return _contains_type_emitted("core.recruitment.offer.resolved"))
	await _await_until(func() -> bool: return _contains_type_emitted("core.guild.member.joined"))
