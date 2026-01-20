extends "res://addons/gdUnit4/src/GdUnitTestSuite.gd"

const HUD_SCENE_PATH: String = "res://Game.Godot/Scenes/UI/HUD.tscn"

var _bus: Node = null
var _bus_cb: Callable = Callable()
var _types: Array[String] = []
var _prev_enable_playable: String = ""

func _on_evt(type, _source, _data_json, _id, _specVersion, _dataContentType, _timestampIso) -> void:
	_types.append(str(type))

func _count_type(types: Array[String], wanted: String) -> int:
	var c := 0
	for t in types:
		if t == wanted:
			c += 1
	return c

func _wait_for_type_count(types: Array[String], wanted: String, min_count: int, frames: int = 120) -> bool:
	for _i in range(frames):
		if _count_type(types, wanted) >= min_count:
			return true
		await get_tree().process_frame
	return false

func before() -> void:
	_prev_enable_playable = OS.get_environment("GD_ENABLE_PLAYABLE")
	OS.set_environment("GD_ENABLE_PLAYABLE", "1")
	assert_bool(OS.get_environment("GD_ENABLE_PLAYABLE") == "1").is_true()

	var existing = get_node_or_null("/root/EventBus")
	if existing != null:
		assert_bool(existing.has_signal("DomainEventEmitted")).is_true()
		return

	var __bus = preload("res://Game.Godot/Adapters/EventBusAdapter.cs").new()
	__bus.name = "EventBus"
	get_tree().get_root().add_child(auto_free(__bus))

func after() -> void:
	if _bus != null and _bus_cb.is_valid() and _bus.is_connected("DomainEventEmitted", _bus_cb):
		_bus.disconnect("DomainEventEmitted", _bus_cb)
	_bus = null
	_bus_cb = Callable()
	OS.set_environment("GD_ENABLE_PLAYABLE", _prev_enable_playable)

# ACC:T19.3
func test_hud_can_trigger_media_beat_demo_and_is_observable() -> void:
	var bus = get_node_or_null("/root/EventBus")
	assert_object(bus).is_not_null()
	if bus == null:
		return

	_types.clear()
	var types := _types

	_bus = bus
	_bus_cb = Callable(self, "_on_evt")
	if not bus.is_connected("DomainEventEmitted", _bus_cb):
		bus.connect("DomainEventEmitted", _bus_cb)

	var hud_scene := preload(HUD_SCENE_PATH).instantiate()
	get_tree().get_root().add_child(auto_free(hud_scene))
	await get_tree().process_frame

	assert_bool(hud_scene.has_method("TriggerMediaBeatDemo")).is_true()
	assert_bool(hud_scene.has_node("TopBar/HBox/MediaBeatLabel")).is_true()

	var media_label: Label = hud_scene.get_node("TopBar/HBox/MediaBeatLabel")
	var before_text := str(media_label.text)

	hud_scene.TriggerMediaBeatDemo()

	var ok = await _wait_for_type_count(types, "core.media.beat.triggered", 1)
	assert_bool(ok).is_true()
	if not ok:
		return

	await get_tree().process_frame
	assert_bool(str(media_label.text) != before_text).is_true()

