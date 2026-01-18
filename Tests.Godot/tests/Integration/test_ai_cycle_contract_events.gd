extends "res://addons/gdUnit4/src/GdUnitTestSuite.gd"

var _event_types: Array[String] = []
var _ai_intent_types: Array[String] = []

func before() -> void:
    _event_types = []
    _ai_intent_types = []
    var bus = preload("res://Game.Godot/Adapters/EventBusAdapter.cs").new()
    bus.name = "EventBus"
    get_tree().get_root().add_child(auto_free(bus))

func _load_main() -> Node:
    var main = preload("res://Game.Godot/Scenes/Main.tscn").instantiate()
    get_tree().get_root().add_child(auto_free(main))
    await get_tree().process_frame
    return main

func _on_domain_event(type: String, _source: String, data_json: String, _id: String, _spec: String, _ct: String, _ts: String) -> void:
    _event_types.append(type)

    if type != "core.ai.intent.issued":
        return

    var payload = JSON.parse_string(data_json)
    if typeof(payload) != TYPE_DICTIONARY:
        return

    var intent_type = payload.get("intentType", "")
    if typeof(intent_type) == TYPE_STRING and intent_type != "":
        _ai_intent_types.append(intent_type)

# ACC:T16.1
func test_ai_cycle_emits_core_ai_contract_events() -> void:
    var main = await _load_main()
    var bus = get_node_or_null("/root/EventBus")
    assert_object(bus).is_not_null()

    bus.connect("DomainEventEmitted", Callable(self, "_on_domain_event"))

    var hud = main.get_node_or_null("HUD")
    assert_object(hud).is_not_null()

    # Advance 3 times: Resolution -> Player -> AiSimulation -> Resolution
    hud.AdvanceTurnFromGd()
    for i in range(60):
        await get_tree().process_frame
    hud.AdvanceTurnFromGd()
    for i in range(60):
        await get_tree().process_frame
    hud.AdvanceTurnFromGd()

    var ok_started := false
    var ok_intent := false
    var ok_completed := false
    var ok_phase_changed := false
    var ok_join_intent_type := false

    for i in range(240):
        ok_started = ok_started or _event_types.has("core.ai.cycle.started")
        ok_intent = ok_intent or _event_types.has("core.ai.intent.issued")
        ok_completed = ok_completed or _event_types.has("core.ai.cycle.completed")
        ok_phase_changed = ok_phase_changed or _event_types.has("core.game_turn.phase_changed")
        ok_join_intent_type = ok_join_intent_type or _ai_intent_types.has("core.guild.member.join")
        if ok_started and ok_intent and ok_completed and ok_phase_changed and ok_join_intent_type:
            break
        await get_tree().process_frame

    assert_bool(ok_started).is_true()
    assert_bool(ok_intent).is_true()
    assert_bool(ok_completed).is_true()
    assert_bool(ok_phase_changed).is_true()
    assert_bool(ok_join_intent_type).is_true()
