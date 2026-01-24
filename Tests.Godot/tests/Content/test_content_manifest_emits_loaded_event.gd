extends "res://addons/gdUnit4/src/GdUnitTestSuite.gd"

const EVENT_TYPE: String = "core.content.manifest.loaded"

var _got := false
var _source := ""
var _payload = null

func before() -> void:
	_got = false
	_source = ""
	_payload = null
	var bus = get_node_or_null("/root/EventBus")
	assert_object(bus).is_not_null()
	bus.connect("DomainEventEmitted", Callable(self, "_on_evt"))

func after() -> void:
	var bus = get_node_or_null("/root/EventBus")
	if bus == null:
		return
	var callable := Callable(self, "_on_evt")
	if bus.is_connected("DomainEventEmitted", callable):
		bus.disconnect("DomainEventEmitted", callable)
	await get_tree().process_frame
	await get_tree().process_frame

func _on_evt(type, source, data_json, _id, _spec, _ct, _ts) -> void:
	if str(type) != EVENT_TYPE:
		return
	_got = true
	_source = str(source)
	_payload = JSON.parse_string(str(data_json))

# ACC:T27.10
func test_main_scene_emits_manifest_loaded_event_on_startup() -> void:
	var main := preload("res://Game.Godot/Scenes/Main.tscn").instantiate()
	add_child(main)
	auto_free(main)
	await get_tree().process_frame
	assert_bool(_got).is_true()
	assert_str(_source).is_equal("ContentManifestBootstrapper")
	assert_bool(typeof(_payload) == TYPE_DICTIONARY).is_true()
	var dict := _payload as Dictionary
	assert_str(str(dict.get("manifestId", ""))).is_not_empty()
	assert_str(str(dict.get("schemaVersion", ""))).is_not_empty()
	assert_int(int(dict.get("entryCount", 0))).is_greater(0)

