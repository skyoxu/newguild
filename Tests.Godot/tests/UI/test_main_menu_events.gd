extends "res://addons/gdUnit4/src/GdUnitTestSuite.gd"

var _bus: Node
var _received := false
var _etype := ""

func before() -> void:
    _bus = get_node_or_null("/root/EventBus")
    assert_object(_bus).is_not_null()
    var cb := Callable(self, "_on_evt")
    if not _bus.is_connected("DomainEventEmitted", cb):
        _bus.connect("DomainEventEmitted", cb)

func after() -> void:
    if _bus == null:
        return
    var cb := Callable(self, "_on_evt")
    if _bus.is_connected("DomainEventEmitted", cb):
        _bus.disconnect("DomainEventEmitted", cb)

func _on_evt(type, _source, _data_json, _id, _spec, _ct, _ts) -> void:
    _received = true
    _etype = str(type)

func test_main_menu_emits_start() -> void:
    _received = false
    var menu = preload("res://Game.Godot/Scenes/UI/MainMenu.tscn").instantiate()
    add_child(auto_free(menu))
    await get_tree().process_frame
    var btn = menu.get_node("VBox/BtnPlay")
    btn.emit_signal("pressed")
    await get_tree().process_frame
    assert_bool(_received).is_true()
    assert_str(_etype).is_equal("ui.menu.start")

func test_main_menu_emits_guild() -> void:
    _received = false
    var menu = preload("res://Game.Godot/Scenes/UI/MainMenu.tscn").instantiate()
    add_child(auto_free(menu))
    await get_tree().process_frame
    var btn = menu.get_node("VBox/BtnGuild")
    btn.emit_signal("pressed")
    await get_tree().process_frame
    assert_bool(_received).is_true()
    assert_str(_etype).is_equal("ui.menu.guild")
