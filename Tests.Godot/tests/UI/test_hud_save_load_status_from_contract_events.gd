extends "res://addons/gdUnit4/src/GdUnitTestSuite.gd"

var _bus: Node
var _types: Array[String] = []

func before() -> void:
    _types.clear()
    _bus = _ensure_event_bus()

    var cb := Callable(self, "_on_domain_event")
    if not _bus.is_connected("DomainEventEmitted", cb):
        _bus.connect("DomainEventEmitted", cb)

func _on_domain_event(type, _source, _data_json, _id, _spec, _ct, _ts) -> void:
    _types.append(str(type))

func _ensure_event_bus() -> Node:
    var existing := get_node_or_null("/root/EventBus")
    if existing != null:
        return existing

    assert_bool(ResourceLoader.exists("res://Game.Godot/Adapters/EventBusAdapter.cs")).is_true()
    var bus := preload("res://Game.Godot/Adapters/EventBusAdapter.cs").new()
    bus.name = "EventBus"
    get_tree().get_root().add_child(auto_free(bus))
    return bus

func _publish_contract_event(event_type: String, data_json: String = "{}") -> void:
    if _bus == null:
        return
    if _bus.has_method("PublishSimple"):
        _bus.PublishSimple(event_type, "ut", data_json)

func _spawn_hud() -> Node:
    assert_bool(ResourceLoader.exists("res://Game.Godot/Scenes/UI/HUD.tscn")).is_true()
    var hud := preload("res://Game.Godot/Scenes/UI/HUD.tscn").instantiate()
    add_child(auto_free(hud))
    await get_tree().process_frame
    return hud

# ACC:T26.3
func test_contract_event_types_can_be_observed_via_event_bus_signal() -> void:
    _types.clear()

    _publish_contract_event("core.save.requested", "{\"saveId\":\"ut\",\"requestedAt\":\"2026-01-01T00:00:00Z\"}")
    _publish_contract_event("core.save.completed", "{\"saveId\":\"ut\",\"completedAt\":\"2026-01-01T00:00:01Z\"}")
    _publish_contract_event("core.load.requested", "{\"saveId\":\"ut\",\"requestedAt\":\"2026-01-01T00:00:02Z\"}")
    _publish_contract_event("core.load.completed", "{\"saveId\":\"ut\",\"completedAt\":\"2026-01-01T00:00:03Z\"}")

    await get_tree().process_frame

    var save_req_i := _types.find("core.save.requested")
    var save_ok_i := _types.find("core.save.completed")
    var load_req_i := _types.find("core.load.requested")
    var load_ok_i := _types.find("core.load.completed")

    assert_int(save_req_i).is_greater_equal(0)
    assert_int(save_ok_i).is_greater_equal(0)
    assert_int(load_req_i).is_greater_equal(0)
    assert_int(load_ok_i).is_greater_equal(0)

func test_hud_can_be_instantiated_and_remains_stable_on_save_load_contract_events() -> void:
    var hud := await _spawn_hud()

    _publish_contract_event("core.save.requested", "{\"saveId\":\"ut\"}")
    _publish_contract_event("core.load.completed", "{\"saveId\":\"ut\"}")

    await get_tree().process_frame

    assert_bool(hud.is_inside_tree()).is_true()

    var score_label := hud.get_node_or_null("TopBar/HBox/ScoreLabel") as Label
    var health_label := hud.get_node_or_null("TopBar/HBox/HealthLabel") as Label

    assert_bool(score_label != null).is_true()
    assert_bool(health_label != null).is_true()
    assert_bool(score_label.text.length() > 0).is_true()
    assert_bool(health_label.text.length() > 0).is_true()
