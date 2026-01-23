extends "res://addons/gdUnit4/src/GdUnitTestSuite.gd"

var _blocked_reason := ""
var _blocked_url := ""

func _client() -> Node:
    var sc = load("res://Game.Godot/Scripts/Security/SecurityHttpClient.cs")
    if sc == null or not sc.has_method("new"):
        push_warning("SKIP: CSharpScript.new() unavailable, skip HTTP block signal test")
        return null
    var c = sc.new()
    add_child(auto_free(c))
    return c

func _on_blocked(reason: String, url: String) -> void:
    _blocked_reason = reason
    _blocked_url = url

func test_emits_request_blocked_signal_on_denied() -> void:
    var c = _client()
    if c == null:
        return

    await get_tree().process_frame

    _blocked_reason = ""
    _blocked_url = ""

    var cb := Callable(self, "_on_blocked")
    if c.has_signal("RequestBlocked") and not c.is_connected("RequestBlocked", cb):
        c.connect("RequestBlocked", cb)
    if c.has_signal("request_blocked") and not c.is_connected("request_blocked", cb):
        c.connect("request_blocked", cb)

    var ok = c.Validate("GET", "http://example.com", "", 0)
    assert_bool(ok).is_false()

    await get_tree().process_frame

    assert_str(_blocked_reason).is_not_empty()
    assert_str(_blocked_url).starts_with("http://")
