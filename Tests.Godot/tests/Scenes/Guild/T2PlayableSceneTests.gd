extends "res://addons/gdUnit4/src/GdUnitTestSuite.gd"

var _bus: Node
var _previous_bus: Node
var _previous_bus_original_name: String
var _previous_bus_original_process_mode: Node.ProcessMode
var _event_types: Array[String] = []
var _manifest_event_json: String = ""
var _catalog_event_json: String = ""
var _bus_cb: Callable

func before() -> void:
    _bus = preload("res://Game.Godot/Adapters/EventBusAdapter.cs").new()
    _bus.name = "EventBus"

    var data_store := get_node_or_null("/root/DataStore")
    if data_store == null:
        data_store = preload("res://Game.Godot/Adapters/DataStoreAdapter.cs").new()
        data_store.name = "DataStore"
        get_tree().get_root().add_child(auto_free(data_store))

    _previous_bus = get_node_or_null("/root/EventBus")
    if _previous_bus != null:
        _previous_bus_original_name = _previous_bus.name
        _previous_bus_original_process_mode = _previous_bus.process_mode
        _previous_bus.process_mode = Node.ProcessMode.PROCESS_MODE_DISABLED
        _previous_bus.name = "_EventBusBackup_%s" % str(_previous_bus.get_instance_id())

    get_tree().get_root().add_child(auto_free(_bus))

    _bus_cb = Callable(self, "_on_domain_event")
    if _bus != null and _bus.has_signal("DomainEventEmitted"):
        if not _bus.is_connected("DomainEventEmitted", _bus_cb):
            _bus.connect("DomainEventEmitted", _bus_cb)

    OS.set_environment("SECURITY_TEST_MODE", "1")

func after() -> void:
    if _bus != null and is_instance_valid(_bus):
        if _bus_cb.is_valid() and _bus.has_signal("DomainEventEmitted") and _bus.is_connected("DomainEventEmitted", _bus_cb):
            _bus.disconnect("DomainEventEmitted", _bus_cb)
        _bus.name = "_EventBusTemp"
    _bus = null

    if _previous_bus != null and is_instance_valid(_previous_bus):
        _previous_bus.name = _previous_bus_original_name
        _previous_bus.process_mode = _previous_bus_original_process_mode
    _previous_bus = null
    _previous_bus_original_name = ""
    _previous_bus_original_process_mode = Node.ProcessMode.PROCESS_MODE_INHERIT
    _event_types = []
    _manifest_event_json = ""
    _catalog_event_json = ""

func after_test() -> void:
    # Allow queued frees from auto_free() to be processed before orphan detection.
    await get_tree().process_frame
    await get_tree().process_frame

func _on_domain_event(type: String, _source: String, data_json: String, _id: String, _spec: String, _ct: String, _ts: String) -> void:
    _event_types.append(str(type))
    if str(type) == "core.content.manifest.loaded":
        _manifest_event_json = str(data_json)
    elif str(type) == "core.event_catalog.loaded":
        _catalog_event_json = str(data_json)

func _wait_for_event_type(wanted: String, frames: int = 240) -> bool:
    for _i in range(frames):
        for t in _event_types:
            if t == wanted:
                return true
        await get_tree().process_frame
    return false

# ACC:T27.6
# ACC:T27.7
# ACC:T29.2
# ACC:T29.3
# ACC:T29.4
# ACC:T32.4
func test_t2_playable_loop_scene_skeleton() -> void:
    var scene := preload("res://Game.Godot/Scenes/Main.tscn").instantiate()
    add_child(scene)
    auto_free(scene)
    await get_tree().process_frame
    assert_bool(scene.visible).is_true()

    var ok_manifest := await _wait_for_event_type("core.content.manifest.loaded", 120)
    assert_bool(ok_manifest).is_true()
    assert_str(_manifest_event_json).contains("manifestId")
    assert_str(_manifest_event_json).contains("entryCount")

    var ok_catalog := await _wait_for_event_type("core.event_catalog.loaded", 120)
    assert_bool(ok_catalog).is_true()
    assert_str(_catalog_event_json).contains("catalogId")
    assert_str(_catalog_event_json).contains("eventDefinitionCount")

    assert_bool(scene.has_node("MainMenu")).is_true()
    var menu: Control = scene.get_node("MainMenu")
    assert_bool(menu.visible).is_true()
    assert_int(menu.mouse_filter).is_equal(Control.MOUSE_FILTER_STOP)
    assert_bool(menu.has_node("VBox/BtnPlay")).is_true()
    var btn_play: Button = menu.get_node("VBox/BtnPlay")
    assert_bool(btn_play.disabled).is_false()
