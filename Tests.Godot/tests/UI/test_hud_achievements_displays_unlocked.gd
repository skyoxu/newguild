extends "res://addons/gdUnit4/src/GdUnitTestSuite.gd"

const HUD_SCENE := "res://Game.Godot/Scenes/UI/HUD.tscn"
const EVENT_BUS_SCRIPT := "res://Game.Godot/Adapters/EventBusAdapter.cs"
const DATA_STORE_SCRIPT := "res://Game.Godot/Adapters/DataStoreAdapter.cs"
const ACHIEVEMENT_STATE_KEY := "achievement_state_t2-demo"

func _ensure_event_bus(root: Node) -> Node:
	var existing := root.get_node_or_null("EventBus")
	if existing != null and existing.has_method("PublishSimple"):
		return existing

	if existing != null:
		existing.queue_free()
		await get_tree().process_frame

	var script := load(EVENT_BUS_SCRIPT)
	assert_object(script).is_not_null()
	var bus = script.new()
	bus.name = "EventBus"
	root.add_child(auto_free(bus))
	await get_tree().process_frame
	return bus

func _reset_achievement_state() -> void:
	var data_store := get_tree().get_root().get_node_or_null("DataStore")
	if data_store != null and data_store.has_method("DeleteSync"):
		data_store.call("DeleteSync", ACHIEVEMENT_STATE_KEY)

func _ensure_data_store(root: Node) -> Node:
	var existing := root.get_node_or_null("DataStore")
	if existing != null:
		return existing
	var script := load(DATA_STORE_SCRIPT)
	assert_object(script).is_not_null()
	var store = script.new()
	store.name = "DataStore"
	root.add_child(auto_free(store))
	await get_tree().process_frame
	return store

func _extract_achievement_count(label_text: String) -> int:
	var idx := label_text.find(":")
	if idx < 0:
		return -1
	return int(label_text.substr(idx + 1).strip_edges())

# ACC:T36.1 ACC:T50.1 ACC:T50.4
func test_hud_achievements_label_updates_on_set() -> void:
	_reset_achievement_state()
	var root := get_tree().get_root()
	var bus := await _ensure_event_bus(root)
	await _ensure_data_store(root)

	var scene := preload(HUD_SCENE).instantiate()
	root.add_child(auto_free(scene))
	await get_tree().process_frame
	var label := scene.get_node_or_null("TopBar/HBox/AchievementsLabel")
	assert_object(label).is_not_null()
	var before_count := -1
	if label != null:
		before_count = _extract_achievement_count(label.text)

	var can_publish := bus.has_method("PublishSimple")
	if can_publish:
		bus.call("PublishSimple", "core.guild.created", "test", "{\"guildId\":\"g1\"}")
	await get_tree().process_frame
	assert_bool(can_publish).is_true()
	if label != null:
		assert_str(label.text).contains("Achievements")
		var after_count := _extract_achievement_count(label.text)
		assert_int(before_count).is_greater_equal(0)
		assert_int(after_count).is_greater_equal(before_count)
		assert_int(after_count).is_greater_equal(1)
	_reset_achievement_state()

# ACC:T50.4
func test_hud_achievements_persists_after_scene_recreate() -> void:
	_reset_achievement_state()
	var root := get_tree().get_root()
	var bus := await _ensure_event_bus(root)
	await _ensure_data_store(root)

	var first_scene := preload(HUD_SCENE).instantiate()
	root.add_child(auto_free(first_scene))
	await get_tree().process_frame
	bus.call("PublishSimple", "core.guild.created", "test", "{\"guildId\":\"g1\"}")
	await get_tree().process_frame

	var first_label := first_scene.get_node_or_null("TopBar/HBox/AchievementsLabel")
	assert_object(first_label).is_not_null()
	if first_label != null:
		assert_str(first_label.text).contains("1")

	first_scene.queue_free()
	await get_tree().process_frame

	var second_scene := preload(HUD_SCENE).instantiate()
	root.add_child(auto_free(second_scene))
	await get_tree().process_frame
	var second_label := second_scene.get_node_or_null("TopBar/HBox/AchievementsLabel")
	assert_object(second_label).is_not_null()
	if second_label != null:
		assert_str(second_label.text).contains("1")

	_reset_achievement_state()

# ACC:T36.3 ACC:T50.7
func test_hud_achievements_label_default_is_zero() -> void:
	var root := get_tree().get_root()
	await _ensure_data_store(root)
	var scene := preload(HUD_SCENE).instantiate()
	add_child(auto_free(scene))
	await get_tree().process_frame
	var label := scene.get_node_or_null("TopBar/HBox/AchievementsLabel")
	assert_object(label).is_not_null()
	if label != null:
		assert_str(label.text).contains("0")
