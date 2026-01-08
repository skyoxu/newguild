extends "res://addons/gdUnit4/src/GdUnitTestSuite.gd"

func test_configfile_utf8_roundtrip() -> void:
    # In some CI/userdir override setups, ConfigFile.save("user://...") can fail with
    # "Cannot call method 'seek' on a null value." Use explicit FileAccess IO to keep
    # the UTF-8 roundtrip test stable across environments.
    await get_tree().process_frame

    var dir_user = "user://config-tests"
    var dir_abs = ProjectSettings.globalize_path(dir_user).replace("\\", "/")
    DirAccess.make_dir_recursive_absolute(dir_abs)
    var file_path = dir_abs.path_join("settings_%d.cfg" % int(Time.get_unix_time_from_system()))

    var cfg := ConfigFile.new()
    var note := "Hello, world! äöü ✓"
    cfg.set_value("app", "volume", 0.66)
    cfg.set_value("app", "lang", "zh")
    cfg.set_value("app", "note", note)
    var text := cfg.encode_to_text()
    var f := FileAccess.open(file_path, FileAccess.WRITE)
    assert_object(f).is_not_null()
    f.store_string(text)
    f.close()

    var cfg2 := ConfigFile.new()
    var err2 = cfg2.load(file_path)
    assert_int(err2).is_equal(0)
    assert_float(float(cfg2.get_value("app", "volume", 0.0))).is_equal(0.66)
    assert_str(str(cfg2.get_value("app", "lang", ""))).is_equal("zh")
    assert_str(str(cfg2.get_value("app", "note", ""))).is_equal(note)
