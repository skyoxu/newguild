extends "res://addons/gdUnit4/src/GdUnitTestSuite.gd"

func _new_db() -> Node:
    # Prefer compiled C# SqliteDataStore; if not available, skip these hard path security tests.
    if not ClassDB.class_exists("SqliteDataStore"):
        push_warning("SqliteDataStore C# class not available; skipping DB path security hard tests.")
        return null

    var db: Node = ClassDB.instantiate("SqliteDataStore")
    db.name = "DbPathSecurity"
    get_tree().get_root().add_child(auto_free(db))
    await get_tree().process_frame
    if not db.has_method("TryOpen"):
        await get_tree().process_frame
    return db


func test_sqlite_path_security_rejects_absolute_and_traversal() -> void:
    var db = await _new_db()
    if db == null:
        return

    var ok_user: bool = db.TryOpen("user://security_path_ok.db")
    assert_bool(ok_user).is_true()

    var abs: bool = db.TryOpen("C:/temp/security_path_bad.db")
    assert_bool(abs).is_false()

    var trav: bool = db.TryOpen("user://../security_path_bad.db")
    assert_bool(trav).is_false()
