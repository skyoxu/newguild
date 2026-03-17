extends "res://addons/gdUnit4/src/GdUnitTestSuite.gd"

func _evaluate_unified(execution: Dictionary, evidence: Dictionary) -> Dictionary:
	var required_categories := ["unit", "godot", "acceptance"]
	for category in required_categories:
		if not execution.has(category):
			return {"status": "fail", "reason": "execution_category_missing", "conflict_source": category}
		if not evidence.has(category):
			return {"status": "fail", "reason": "evidence_category_missing", "conflict_source": category}
		var exec_item: Dictionary = execution[category]
		var ev_item: Dictionary = evidence[category]
		if not bool(exec_item.get("executed", false)):
			return {"status": "fail", "reason": "execution_not_executed", "conflict_source": category}
		if not bool(exec_item.get("passed", false)):
			return {"status": "fail", "reason": "execution_failed", "conflict_source": category}
		if not bool(ev_item.get("passed", false)):
			return {"status": "fail", "reason": "evidence_failed", "conflict_source": category}

	var run_id = String(execution["unit"].get("run_id", ""))
	var date_value = String(evidence["unit"].get("date", ""))
	if run_id.is_empty() or date_value.is_empty():
		return {"status": "fail", "reason": "round_metadata_missing", "conflict_source": "unit"}

	for category in required_categories:
		var exec_run_id = String(execution[category].get("run_id", ""))
		var ev_run_id = String(evidence[category].get("run_id", ""))
		var ev_date = String(evidence[category].get("date", ""))
		if exec_run_id != run_id or ev_run_id != run_id:
			return {"status": "fail", "reason": "evidence_run_id_mismatch", "conflict_source": category}
		if ev_date != date_value:
			return {"status": "fail", "reason": "evidence_date_mismatch", "conflict_source": category}

	return {"status": "pass", "reason": "", "conflict_source": ""}


# ACC:T55.4
func test_should_fail_when_any_category_is_missing_or_not_executed() -> void:
	var execution_missing_godot := {
		"unit": {"run_id": "run-55-gd-a", "executed": true, "passed": true},
		"acceptance": {"run_id": "run-55-gd-a", "executed": true, "passed": true},
	}
	var evidence := {
		"unit": {"run_id": "run-55-gd-a", "date": "2026-03-16", "passed": true},
		"godot": {"run_id": "run-55-gd-a", "date": "2026-03-16", "passed": true},
		"acceptance": {"run_id": "run-55-gd-a", "date": "2026-03-16", "passed": true},
	}
	var missing_result = _evaluate_unified(execution_missing_godot, evidence)
	assert_str(missing_result["status"]).is_equal("fail")
	assert_str(missing_result["reason"]).is_equal("execution_category_missing")

	var execution_not_executed := {
		"unit": {"run_id": "run-55-gd-a", "executed": true, "passed": true},
		"godot": {"run_id": "run-55-gd-a", "executed": false, "passed": false},
		"acceptance": {"run_id": "run-55-gd-a", "executed": true, "passed": true},
	}
	var not_executed_result = _evaluate_unified(execution_not_executed, evidence)
	assert_str(not_executed_result["status"]).is_equal("fail")
	assert_str(not_executed_result["reason"]).is_equal("execution_not_executed")

	var evidence_missing_godot := {
		"unit": {"run_id": "run-55-gd-a", "date": "2026-03-16", "passed": true},
		"acceptance": {"run_id": "run-55-gd-a", "date": "2026-03-16", "passed": true},
	}
	var evidence_missing_result = _evaluate_unified(
		{
			"unit": {"run_id": "run-55-gd-a", "executed": true, "passed": true},
			"godot": {"run_id": "run-55-gd-a", "executed": true, "passed": true},
			"acceptance": {"run_id": "run-55-gd-a", "executed": true, "passed": true},
		},
		evidence_missing_godot)
	assert_str(evidence_missing_result["status"]).is_equal("fail")
	assert_str(evidence_missing_result["reason"]).is_equal("evidence_category_missing")


# ACC:T55.8
func test_should_fail_when_run_id_or_date_is_not_same_round() -> void:
	var execution := {
		"unit": {"run_id": "run-55-gd-b", "executed": true, "passed": true},
		"godot": {"run_id": "run-55-gd-b", "executed": true, "passed": true},
		"acceptance": {"run_id": "run-55-gd-b", "executed": true, "passed": true},
	}
	var mixed_evidence := {
		"unit": {"run_id": "run-55-gd-b", "date": "2026-03-16", "passed": true},
		"godot": {"run_id": "run-55-old", "date": "2026-03-16", "passed": true},
		"acceptance": {"run_id": "run-55-gd-b", "date": "2026-03-16", "passed": true},
	}
	var run_mismatch_result = _evaluate_unified(execution, mixed_evidence)
	assert_str(run_mismatch_result["status"]).is_equal("fail")
	assert_str(run_mismatch_result["reason"]).is_equal("evidence_run_id_mismatch")

	var date_mismatch_evidence := {
		"unit": {"run_id": "run-55-gd-b", "date": "2026-03-16", "passed": true},
		"godot": {"run_id": "run-55-gd-b", "date": "2026-03-15", "passed": true},
		"acceptance": {"run_id": "run-55-gd-b", "date": "2026-03-16", "passed": true},
	}
	var date_mismatch_result = _evaluate_unified(execution, date_mismatch_evidence)
	assert_str(date_mismatch_result["status"]).is_equal("fail")
	assert_str(date_mismatch_result["reason"]).is_equal("evidence_date_mismatch")

	var unlabeled_evidence := {
		"unit": {"run_id": "run-55-gd-b", "date": "", "passed": true},
		"godot": {"run_id": "run-55-gd-b", "date": "2026-03-16", "passed": true},
		"acceptance": {"run_id": "run-55-gd-b", "date": "2026-03-16", "passed": true},
	}
	var unlabeled_result = _evaluate_unified(execution, unlabeled_evidence)
	assert_str(unlabeled_result["status"]).is_equal("fail")
	assert_str(unlabeled_result["reason"]).is_equal("round_metadata_missing")

	var valid_evidence := {
		"unit": {"run_id": "run-55-gd-b", "date": "2026-03-16", "passed": true},
		"godot": {"run_id": "run-55-gd-b", "date": "2026-03-16", "passed": true},
		"acceptance": {"run_id": "run-55-gd-b", "date": "2026-03-16", "passed": true},
	}
	var valid_result = _evaluate_unified(execution, valid_evidence)
	assert_str(valid_result["status"]).is_equal("pass")
	assert_bool(valid_result.has("status")).is_true()
	assert_bool(valid_result.has("statuses")).is_false()
