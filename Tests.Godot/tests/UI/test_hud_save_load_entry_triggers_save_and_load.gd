extends "res://addons/gdUnit4/src/GdUnitTestSuite.gd"

var _bus: Node
var _store: Node
var _types: Array[String] = []

func before() -> void:
    _types.clear()
    _bus = _ensure_event_bus()
    _store = _ensure_data_store()

    var cb := Callable(self, "_on_evt")
    if not _bus.is_connected("DomainEventEmitted", cb):
        _bus.connect("DomainEventEmitted", cb)

func after() -> void:
    var ds = get_node_or_null("/root/DataStore")
    if ds != null and ds.has_method("DeleteSync"):
        ds.DeleteSync("demo_save")

func _on_evt(type, _source, _data_json, _id, _spec, _ct, _ts) -> void:
    _types.append(str(type))

func _ensure_event_bus() -> Node:
    var existing = get_node_or_null("/root/EventBus")
    if existing != null:
        return existing
    var bus = preload("res://Game.Godot/Adapters/EventBusAdapter.cs").new()
    bus.name = "EventBus"
    get_tree().get_root().add_child(auto_free(bus))
    return bus

func _ensure_data_store() -> Node:
    var existing = get_node_or_null("/root/DataStore")
    if existing != null:
        return existing
    var store = preload("res://Game.Godot/Adapters/DataStoreAdapter.cs").new()
    store.name = "DataStore"
    get_tree().get_root().add_child(auto_free(store))
    return store

func _open_main() -> Node:
    var main = preload("res://Game.Godot/Scenes/Main.tscn").instantiate()
    add_child(auto_free(main))
    await get_tree().process_frame
    return main

# ACC:T26.1
func test_save_load_entry_emits_save_then_load_requested_events() -> void:
    _types.clear()
    var main = await _open_main()
    var btn: Button = main.get_node("VBox/SaveLoadBtn")
    btn.emit_signal("pressed")
    await get_tree().process_frame
    await get_tree().process_frame

    var save_i := _types.find("core.save.requested")
    var load_i := _types.find("core.load.requested")
    assert_int(save_i).is_greater_equal(0)
    assert_int(load_i).is_greater_equal(0)
    assert_bool(save_i < load_i).is_true()

func test_save_load_entry_updates_debug_label_with_loaded_payload() -> void:
    var main = await _open_main()
    var btn: Button = main.get_node("VBox/SaveLoadBtn")
    var out: Label = main.get_node("VBox/Output")
    btn.emit_signal("pressed")
    await get_tree().process_frame
    assert_str(out.text).contains("Loaded:")
