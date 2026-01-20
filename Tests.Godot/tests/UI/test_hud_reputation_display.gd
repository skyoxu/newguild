extends "res://addons/gdUnit4/src/GdUnitTestSuite.gd"

var _bus: Node
var _previous_bus: Node
var _previous_bus_original_name: String
var _previous_bus_original_process_mode: int

func before() -> void:
    _bus = preload("res://Game.Godot/Adapters/EventBusAdapter.cs").new()
    _bus.name = "EventBus"

    _previous_bus = get_node_or_null("/root/EventBus")
    if _previous_bus != null:
        # Keep it in-tree to avoid orphan nodes, but move it out of the way.
        _previous_bus_original_name = _previous_bus.name
        _previous_bus_original_process_mode = _previous_bus.process_mode
        _previous_bus.process_mode = Node.PROCESS_MODE_DISABLED
        _previous_bus.name = "_EventBusBackup_%s" % str(_previous_bus.get_instance_id())

    get_tree().get_root().add_child(auto_free(_bus))

func after() -> void:
    if _bus != null and is_instance_valid(_bus):
        # Avoid name collision when restoring _previous_bus.
        _bus.name = "_EventBusTemp"
    _bus = null

    if _previous_bus != null and is_instance_valid(_previous_bus):
        _previous_bus.name = _previous_bus_original_name
        _previous_bus.process_mode = _previous_bus_original_process_mode
    _previous_bus = null
    _previous_bus_original_name = ""
    _previous_bus_original_process_mode = Node.PROCESS_MODE_INHERIT


# ACC:T19.4
func test_hud_has_reputation_display_and_updates_on_event() -> void:
    var hud := preload("res://Game.Godot/Scenes/UI/HUD.tscn").instantiate()
    add_child(auto_free(hud))
    await get_tree().process_frame

    # Red-first expectation: a visible HUD node exists for reputation display.
    # Implementation should add this node and update it from core.reputation.changed.
    var label_path := "TopBar/HBox/ReputationLabel"
    assert_bool(hud.has_node(label_path)).is_true()

    var label: Label = hud.get_node(label_path)
    assert_bool(label.text != "").is_true()

    _bus.PublishSimple("core.reputation.changed", "ut", "{\"guildId\":\"g1\",\"oldValue\":0,\"newValue\":42,\"reason\":\"test\",\"changedAt\":\"2026-01-01T00:00:00Z\"}")
    await get_tree().process_frame
    await get_tree().process_frame

    assert_str(label.text).contains("42")
