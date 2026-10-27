"""Tests of the protocol model: the conventions that turn a request of protocol.json into the parameters of a method.

    python -m unittest discover -s tools/protocol -p "test_*.py" -v

CI runs them next to `generate.py --check`. Only the Python standard library is used.
"""

from __future__ import annotations

import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

import generate  # noqa: E402
import protocol_model as pm  # noqa: E402

OVERLAY = {"refs": {"kinds": {"scene": "SceneRef", "input": "InputRef", "source": "SourceRef"}}}


def field(name: str, vtype: str = "String", optional: bool = False) -> dict:
    return {"valueName": name, "valueType": vtype, "valueOptional": optional, "valueDescription": f"The {name}."}


def model(*requests: tuple[str, list[dict], list[dict]], overlay: dict | None = None) -> pm.Model:
    """A model of an invented protocol: each request is (name, request fields, response fields)."""
    upstream = {
        "enums": [],
        "events": [],
        "requests": [
            {
                "requestType": name,
                "description": f"Does {name}.",
                "category": "general",
                "initialVersion": "5.0.0",
                "requestFields": request_fields,
                "responseFields": response_fields,
            }
            for name, request_fields, response_fields in requests
        ],
    }
    return pm.Model(upstream, OVERLAY if overlay is None else overlay, "9.9.9")


def signature(m: pm.Model, name: str) -> list[tuple[str, str, bool, bool]]:
    return [(p.name, p.type.text, p.ref, p.has_default) for p in m.request_params(m.request(name))]


class Refs(unittest.TestCase):
    def test_a_name_and_a_uuid_are_one_parameter(self):
        m = model(("SetThing", [field("inputName", optional=True), field("inputUuid", optional=True), field("inputMuted", "Boolean")], []))

        self.assertEqual(signature(m, "SetThing"), [("input", "InputRef", True, False), ("inputMuted", "bool", False, False)])

    def test_the_order_in_which_the_protocol_lists_them_does_not_matter(self):
        name_first = model(("SetThing", [field("inputName", optional=True), field("inputUuid", optional=True), field("inputMuted", "Boolean")], []))
        uuid_first = model(("SetThing", [field("inputUuid", optional=True), field("inputName", optional=True), field("inputMuted", "Boolean")], []))

        self.assertEqual(signature(uuid_first, "SetThing"), signature(name_first, "SetThing"))
        self.assertEqual([p.name for p in uuid_first.request_params(uuid_first.request("SetThing"))], ["input", "inputMuted"])

    def test_a_ref_is_required_unless_the_overlay_says_otherwise(self):
        requests = ("CopyThing", [field("sceneName", optional=True), field("sceneUuid", optional=True),
                                  field("destinationSceneName", optional=True), field("destinationSceneUuid", optional=True)], [])

        required = model(requests)
        optional = model(requests, overlay=dict(OVERLAY, refs=dict(OVERLAY["refs"], optional=["CopyThing.destinationScene"])))

        self.assertEqual(signature(required, "CopyThing"), [("scene", "SceneRef", True, False), ("destinationScene", "SceneRef", True, False)])
        self.assertEqual(signature(optional, "CopyThing"), [("scene", "SceneRef", True, False), ("destinationScene", "SceneRef?", True, True)])

    def test_required_parameters_come_first_and_the_canvas_is_last(self):
        m = model(
            ("DoThing", [field("canvasUuid", optional=True), field("note", optional=True), field("sceneName", optional=True),
                         field("sceneUuid", optional=True), field("count", "Number")], []),
            overlay=dict(OVERLAY, types={"request": {"count": "int"}}),
        )

        self.assertEqual([p.name for p in m.request_params(m.request("DoThing"))], ["scene", "count", "note", "canvasUuid"])

    def test_an_object_without_a_ref_type_is_reported(self):
        m = model(("DoThing", [field("widgetName", optional=True), field("widgetUuid", optional=True)], []))

        with self.assertRaises(pm.ModelError) as caught:
            m.request_params(m.request("DoThing"))

        self.assertIn("[refs.kinds]", str(caught.exception))


class Types(unittest.TestCase):
    def test_a_protocol_type_without_a_convention_is_reported(self):
        m = model(("DoThing", [field("matrix", "Matrix")], []))

        with self.assertRaises(pm.ModelError) as caught:
            m.request_params(m.request("DoThing"))

        self.assertIn("overlay.toml", str(caught.exception))

    def test_a_uuid_is_a_guid_unless_the_overlay_says_otherwise(self):
        m = model(("DoThing", [field("thingUuid")], []))

        self.assertEqual(signature(m, "DoThing"), [("thingUuid", "Guid", False, False)])

    def test_a_field_named_like_a_keyword_is_escaped(self):
        m = model(("DoThing", [field("object")], []))

        self.assertEqual([p.name for p in m.request_params(m.request("DoThing"))], ["@object"])


class Nulls(unittest.TestCase):
    def test_a_field_whose_null_has_a_meaning_accepts_null_without_a_default(self):
        m = model(("DoThing", [field("value", optional=True)], []), overlay=dict(OVERLAY, request_nulls={"fields": ["DoThing.value"]}))

        (param,) = m.request_params(m.request("DoThing"))

        self.assertEqual((param.type.text, param.has_default, param.always_sent), ("string?", False, True))

    def test_an_optional_field_is_a_nullable_parameter_with_a_default(self):
        m = model(("DoThing", [field("value", optional=True)], []))

        self.assertEqual(signature(m, "DoThing"), [("value", "string?", False, True)])


class Overlay(unittest.TestCase):
    def test_an_entry_about_a_request_the_protocol_does_not_have_is_reported(self):
        with self.assertRaises(pm.ModelError):
            model(("DoThing", [], []), overlay=dict(OVERLAY, manual={"requests": ["Gone"]}))

    def test_an_entry_about_a_field_the_protocol_does_not_have_is_reported(self):
        with self.assertRaises(pm.ModelError):
            model(("DoThing", [field("value")], []), overlay=dict(OVERLAY, request_nulls={"fields": ["DoThing.gone"]}))

    def test_an_optional_ref_the_protocol_does_not_have_is_reported(self):
        with self.assertRaises(pm.ModelError):
            model(("DoThing", [field("value")], []), overlay=dict(OVERLAY, refs=dict(OVERLAY["refs"], optional=["DoThing.scene"])))


class Returns(unittest.TestCase):
    def test_a_response_without_fields_returns_nothing(self):
        m = model(("DoThing", [], []))

        returns = m.returns(m.request("DoThing"))

        self.assertEqual((returns.kind, returns.type), ("none", ""))

    def test_a_response_with_one_field_returns_its_value(self):
        m = model(("GetThing", [], [field("thingCount", "Number")]), overlay=dict(OVERLAY, types={"data": {"thingCount": "int"}}))

        returns = m.returns(m.request("GetThing"))

        self.assertEqual((returns.kind, returns.type, returns.dto), ("field", "int", "GetThingResponse"))

    def test_a_response_with_several_fields_returns_the_response_type(self):
        m = model(("GetThing", [], [field("first", "Boolean"), field("second", "Boolean")]))

        returns = m.returns(m.request("GetThing"))

        self.assertEqual((returns.kind, returns.type), ("dto", "GetThingResponse"))


class ObsoleteAliases(unittest.TestCase):
    """A property of a released type that another one replaced stays as an obsolete alias."""

    @staticmethod
    def aliased(entries: dict[str, str]) -> pm.Model:
        return model(("GetThing", [], [field("sceneName"), field("sceneUuid")]), overlay=dict(OVERLAY, obsolete_aliases=entries))

    def test_an_alias_is_part_of_its_type(self):
        m = self.aliased({"GetThingResponse.CurrentSceneName": "SceneName"})

        (dto,) = pm.response_dtos(m)

        self.assertEqual([(alias, target.field.name) for alias, target in dto.aliases], [("CurrentSceneName", "sceneName")])

    def test_an_alias_is_written_as_an_obsolete_computed_property(self):
        m = self.aliased({"GetThingResponse.CurrentSceneName": "SceneName"})

        text = generate.emit_dtos(m, "Test", pm.response_dtos(m), "response")

        self.assertIn('[Obsolete("Use SceneName instead. This property will be removed in future versions.")]', text)
        self.assertIn("public string CurrentSceneName => this.SceneName;", text)
        self.assertIn("[JsonIgnore]", text)

    def test_an_alias_of_a_property_the_type_does_not_have_is_reported(self):
        m = self.aliased({"GetThingResponse.CurrentSceneName": "NoSuchProperty"})

        with self.assertRaises(pm.ModelError):
            pm.response_dtos(m)

    def test_an_alias_that_the_type_has_a_property_for_itself_is_reported(self):
        m = self.aliased({"GetThingResponse.SceneUuid": "SceneName"})

        with self.assertRaises(pm.ModelError):
            pm.response_dtos(m)

    def test_an_alias_of_a_type_that_is_not_generated_is_reported(self):
        m = self.aliased({"GoneResponse.CurrentSceneName": "SceneName"})

        with self.assertRaises(pm.ModelError):
            pm.check_dto_overlay(m, pm.response_dtos(m))

    def test_an_init_property_of_a_type_that_is_not_generated_is_reported(self):
        m = model(("GetThing", [], [field("first"), field("second")]), overlay=dict(OVERLAY, init_properties={"fields": ["GoneResponse.first"]}))

        with self.assertRaises(pm.ModelError):
            pm.check_dto_overlay(m, pm.response_dtos(m))


class CurrentProtocol(unittest.TestCase):
    """What has to hold for every request of the protocol that is committed, and for every one that a new version brings."""

    @classmethod
    def setUpClass(cls):
        cls.model = pm.Model.load()

    def each_request(self):
        for request in self.model.generated_requests():
            yield request, self.model.request_params(request)

    def test_every_field_of_a_request_is_in_exactly_one_parameter(self):
        for request, params in self.each_request():
            owned = sorted(f.name for p in params for f in p.fields)
            self.assertEqual(owned, sorted(f.name for f in request.plain_request_fields), request.name)

    def test_no_required_parameter_follows_one_that_can_be_left_out(self):
        for request, params in self.each_request():
            optional_seen = False
            for p in params:
                self.assertFalse(optional_seen and not p.has_default, f"{request.name}: {p.name} is required but follows an optional parameter")
                optional_seen = optional_seen or p.has_default

    def test_the_canvas_is_the_last_parameter(self):
        for request, params in self.each_request():
            names = [p.name for p in params]
            if "canvasUuid" in names:
                self.assertEqual(names[-1], "canvasUuid", request.name)

    def test_a_field_whose_null_has_a_meaning_is_nullable_and_has_no_default(self):
        for request, params in self.each_request():
            for p in params:
                if p.always_sent:
                    self.assertTrue(p.type.nullable and not p.has_default, f"{request.name}.{p.name}")

    def test_a_ref_is_one_of_the_ref_types(self):
        for request, params in self.each_request():
            for p in params:
                if p.ref:
                    self.assertIn(p.type.name, self.model.ref_types, f"{request.name}.{p.name}")

    def test_every_parameter_has_an_editor_in_the_test_app(self):
        for request, params in self.each_request():
            for p in params:
                kind, reader, _ = generate.catalog_parameter(self.model, request, p)
                self.assertIn(f'("{p.name.lstrip("@")}")', reader, f"{request.name}.{p.name}")
                self.assertEqual(reader.startswith("args.Optional"), p.type.nullable, f"{request.name}.{p.name}")
                self.assertTrue(kind, f"{request.name}.{p.name}")


class Catalog(unittest.TestCase):
    """The catalog of the test app: what the generator writes for each request and event."""

    def test_a_string_becomes_a_c_sharp_literal(self):
        self.assertEqual(generate.literal('say "hi"\n\\'), '"say \\"hi\\"\\n\\\\"')
        self.assertEqual(generate.literal("bell\x07"), '"bell\\u0007"')

    def test_protocol_text_for_the_screen_is_one_line_without_markdown(self):
        self.assertEqual(generate.plain("Gets the `inputName`\n  of   an input."), "Gets the inputName of an input.")

    def test_a_type_without_an_editor_is_reported(self):
        m = model(("DoThing", [field("matrix", "Matrix")], []), overlay=dict(OVERLAY, types={"request": {"matrix": "Matrix4x4"}}))
        request = m.request("DoThing")
        (param,) = m.request_params(request)

        with self.assertRaises(pm.ModelError) as caught:
            generate.catalog_parameter(m, request, param)

        self.assertIn("PARAMETER_KINDS", str(caught.exception))

    def test_an_optional_parameter_is_read_as_optional_and_a_required_one_is_not(self):
        m = model(("DoThing", [field("count", "Number"), field("note", optional=True)], []), overlay=dict(OVERLAY, types={"request": {"count": "int"}}))
        request = m.request("DoThing")
        count, note = m.request_params(request)

        self.assertEqual(generate.catalog_parameter(m, request, count)[:2], ("Integer", 'args.Int("count")'))
        self.assertEqual(generate.catalog_parameter(m, request, note)[:2], ("Text", 'args.OptionalText("note")'))

    def test_a_ref_is_read_as_the_ref_it_is(self):
        m = model(("DoThing", [field("sceneName", optional=True), field("sceneUuid", optional=True)], []))
        request = m.request("DoThing")
        (scene,) = m.request_params(request)

        self.assertEqual(generate.catalog_parameter(m, request, scene)[:2], ("Scene", 'args.Scene("scene")'))

    def test_a_null_that_means_something_is_marked_so_the_editor_can_say_it(self):
        m = model(("DoThing", [field("value", optional=True)], []), overlay=dict(OVERLAY, request_nulls={"fields": ["DoThing.value"]}))
        request = m.request("DoThing")
        (param,) = m.request_params(request)

        self.assertIn("SendsNullWhenOmitted: true", generate.catalog_parameter(m, request, param)[2])

    def test_the_catalog_has_every_request_and_event_of_the_protocol(self):
        current = pm.Model.load()
        text = generate.emit_catalog(current)

        for request in current.generated_requests():
            self.assertIn(f'"{request.name}",', text, request.name)
            self.assertIn(f"client.{request.name}(", text, request.name)
            self.assertIn(f"batch.Add{request.name}Request(", text, request.name)
        for event in current.events:
            self.assertIn(f"client.{event.name} +=", text, event.name)

    def test_the_requests_that_are_written_by_hand_have_a_descriptor(self):
        generate.check_manual_catalog(pm.Model.load())


if __name__ == "__main__":
    unittest.main()
