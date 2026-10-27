"""The protocol model: obs-websocket's protocol.json, patched where it is wrong, plus the client's overlay.

Everything the generators need to know about the protocol, and about what the client makes of it, is answered here.
Only the Python standard library is used.
"""

from __future__ import annotations

import json
import re
import tomllib
from dataclasses import dataclass
from pathlib import Path

TOOLS = Path(__file__).resolve().parent
ROOT = TOOLS.parents[1]
PROTOCOL_FILE = TOOLS / "obs-websocket-protocol.json"
OVERLAY_FILE = TOOLS / "overlay.toml"
ENUMS_DIR = ROOT / "OBSClient" / "Enums"

CSHARP_KEYWORDS = {
    "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked", "class", "const", "continue",
    "decimal", "default", "delegate", "do", "double", "else", "enum", "event", "explicit", "extern", "false", "finally",
    "fixed", "float", "for", "foreach", "goto", "if", "implicit", "in", "int", "interface", "internal", "is", "lock",
    "long", "namespace", "new", "null", "object", "operator", "out", "override", "params", "private", "protected",
    "public", "readonly", "ref", "return", "sbyte", "sealed", "short", "sizeof", "stackalloc", "static", "string",
    "struct", "switch", "this", "throw", "true", "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort",
    "using", "virtual", "void", "volatile", "while",
}

VALUE_TYPES = {"int", "long", "uint", "float", "double", "bool", "Guid", "JsonElement"}

# What the protocol says is what the convention follows when the overlay has nothing to say about a field.
DEFAULT_DATA_TYPES = {
    "String": "string",
    "Number": "double",
    "Boolean": "bool",
    "Array<String>": "string[]",
    "Array<Object>": "JsonElement[]",
    "Object": "JsonElement",
    "Any": "JsonElement",
}
DEFAULT_REQUEST_TYPES = dict(DEFAULT_DATA_TYPES, **{"Object": "Dictionary<string, object>", "Any": "object"})


class ModelError(Exception):
    """The protocol and the overlay do not fit together; the message says what to add to the overlay."""


def pascal(name: str) -> str:
    return name[0].upper() + name[1:]


def last_word(name: str) -> str:
    """The last word of a camelCase name, in lower case: destinationScene -> scene."""
    return re.findall(r"[A-Z]?[a-z0-9]+", name)[-1].lower()


def identifier(name: str) -> str:
    """A name that can be used as a C# identifier (a parameter or a local)."""
    return "@" + name if name in CSHARP_KEYWORDS else name


@dataclass(frozen=True)
class Field:
    name: str
    vtype: str
    optional: bool
    description: str
    behavior: str
    restrictions: str

    @property
    def nested(self) -> bool:
        """A member of an object field (the protocol lists those as 'parent.member')."""
        return "." in self.name


@dataclass(frozen=True)
class ClrType:
    name: str
    nullable: bool
    is_value_type: bool

    @property
    def text(self) -> str:
        return self.name + ("?" if self.nullable else "")

    @property
    def is_collection(self) -> bool:
        return self.name.endswith("[]") or self.name.startswith("Dictionary<")


@dataclass(frozen=True)
class Param:
    """A parameter of the method that sends a request, and of the one that adds the request to a batch."""

    name: str  # the C# name
    type: ClrType
    fields: tuple[Field, ...]  # the protocol field it fills: one, or the (<x>Name, <x>Uuid) pair of a ref
    ref: bool = False
    has_default: bool = False  # the argument can be left out (null)
    always_sent: bool = False  # written even when null, because the protocol gives a null a meaning

    @property
    def field(self) -> Field:
        return self.fields[0]


@dataclass(frozen=True)
class Returns:
    """What the method that sends a request hands back."""

    kind: str  # "none", "field" (the one field of the response) or "dto" (the response type)
    type: str  # the C# type; empty for "none"
    dto: str  # the type that holds the response; empty for "none"
    field: Field | None


@dataclass
class Request:
    name: str
    description: str
    category: str
    since: str
    request_fields: list[Field]
    response_fields: list[Field]

    @property
    def plain_request_fields(self) -> list[Field]:
        return [f for f in self.request_fields if not f.nested]

    @property
    def plain_response_fields(self) -> list[Field]:
        return [f for f in self.response_fields if not f.nested]


@dataclass
class Event:
    name: str
    description: str
    category: str
    since: str
    subscription: str
    fields: list[Field]

    @property
    def plain_fields(self) -> list[Field]:
        return [f for f in self.fields if not f.nested]


def _field(raw: dict) -> Field:
    return Field(
        name=raw["valueName"],
        vtype=raw["valueType"],
        optional=bool(raw.get("valueOptional")),
        description=(raw.get("valueDescription") or "").strip(),
        behavior=(raw.get("valueOptionalBehavior") or "").strip(),
        restrictions=(raw.get("valueRestrictions") or "").strip(),
    )


def client_enum_names() -> set[str]:
    names = set()
    if ENUMS_DIR.is_dir():
        for path in ENUMS_DIR.glob("*.cs"):
            names.update(re.findall(r"public\s+enum\s+(\w+)", path.read_text(encoding="utf-8-sig")))
    return names


class Model:
    def __init__(self, upstream: dict, overlay: dict, version: str):
        self.version = version
        self.overlay = overlay
        self.ref_kinds: dict[str, str] = dict(overlay.get("refs", {}).get("kinds", {}))
        self.ref_types = set(self.ref_kinds.values())
        self.enums = upstream["enums"]
        self.client_enums = client_enum_names()
        self.requests = [
            Request(
                name=r["requestType"],
                description=r["description"].strip(),
                category=r["category"],
                since=r["initialVersion"],
                request_fields=[_field(f) for f in r["requestFields"]],
                response_fields=[_field(f) for f in r["responseFields"]],
            )
            for r in upstream["requests"]
        ]
        self.events = [
            Event(
                name=e["eventType"],
                description=e["description"].strip(),
                category=e["category"],
                since=e["initialVersion"],
                subscription=e["eventSubscription"],
                fields=[_field(f) for f in e["dataFields"]],
            )
            for e in upstream["events"]
        ]
        self._apply_patches()
        self._request_by_name = {r.name: r for r in self.requests}
        self._event_by_name = {e.name: e for e in self.events}
        self._check_request_overlay()

    # -- loading ----------------------------------------------------------------------------------------------------

    @staticmethod
    def load(version: str | None = None) -> "Model":
        upstream = json.loads(PROTOCOL_FILE.read_text(encoding="utf-8"))
        with OVERLAY_FILE.open("rb") as handle:
            overlay = tomllib.load(handle)
        return Model(upstream, overlay, version or Model.recorded_version())

    @staticmethod
    def recorded_version() -> str:
        """The obs-websocket version of the committed protocol.json (kept in overlay.toml's [protocol] table)."""
        with OVERLAY_FILE.open("rb") as handle:
            return tomllib.load(handle)["protocol"]["version"]

    def _apply_patches(self) -> None:
        patches = self.overlay.get("patches", {})
        by_name = {r.name: r for r in self.requests}
        for name, patch in patches.get("response", {}).items():
            if name not in by_name:
                raise ModelError(f"overlay.toml patches the response of '{name}', which the protocol does not define.")
            request = by_name[name]
            omit = set(patch.get("omit", []))
            unknown = omit - {f.name for f in request.response_fields}
            if unknown:
                raise ModelError(
                    f"overlay.toml omits {sorted(unknown)} from the response of '{name}', which the protocol does not "
                    "define (the patch may no longer be needed)."
                )
            request.response_fields = [f for f in request.response_fields if f.name not in omit]
            existing = {f.name for f in request.response_fields}
            for add in patch.get("add", []):
                if add["name"] in existing:
                    raise ModelError(
                        f"overlay.toml adds the response field '{add['name']}' to '{name}', which the protocol now "
                        "defines itself (the patch is no longer needed)."
                    )
                request.response_fields.append(
                    Field(add["name"], add["type"], bool(add.get("optional")), add.get("description", ""), "", "")
                )

    # -- lookups ----------------------------------------------------------------------------------------------------

    def request(self, name: str) -> Request:
        return self._request_by_name[name]

    def event(self, name: str) -> Event:
        return self._event_by_name[name]

    # -- names ------------------------------------------------------------------------------------------------------

    def response_dto(self, request_name: str) -> str:
        """The name of the type that holds the response data of a request."""
        configured = self.overlay.get("responses", {}).get(request_name)
        if configured:
            return configured
        if not self.request(request_name).plain_response_fields:
            return "EmptyResponse"
        return f"{request_name}Response"

    def event_dto(self, event_name: str) -> str:
        """The name of the EventArgs type of an event; an empty string when the event carries no data."""
        configured = self.overlay.get("events", {}).get(event_name)
        if configured is not None:
            return configured
        return f"{event_name}EventArgs" if self.event(event_name).plain_fields else ""

    def is_manual_dto(self, name: str) -> bool:
        return name in self.overlay.get("manual", {}).get("dtos", [])

    # -- types ------------------------------------------------------------------------------------------------------

    def _clr(self, name: str, nullable: bool) -> ClrType:
        return ClrType(name, nullable, name in VALUE_TYPES or name in self.client_enums or name in self.ref_types)

    def data_type(self, dto: str, field: Field) -> ClrType:
        """The C# type of a field of a response or an event."""
        types = self.overlay.get("types", {})
        key = f"{dto}.{field.name}"
        name = types.get("data_exceptions", {}).get(key) or types.get("data", {}).get(field.name)
        if not name:
            name = "Guid" if field.vtype == "String" and field.name.endswith("Uuid") else DEFAULT_DATA_TYPES[field.vtype]
        nullable = field.optional or key in self.overlay.get("nullable", {}).get("fields", [])
        return self._clr(name, nullable)

    def request_type(self, request_name: str, field: Field) -> ClrType:
        """The C# type of a field (a parameter) of a request."""
        types = self.overlay.get("types", {})
        key = f"{request_name}.{field.name}"
        name = types.get("request_exceptions", {}).get(key) or types.get("request", {}).get(field.name)
        if not name:
            name = "Guid" if field.vtype == "String" and field.name.endswith("Uuid") else DEFAULT_REQUEST_TYPES[field.vtype]
        return self._clr(name, field.optional)

    def converter(self, dto: str, field: Field) -> str | None:
        return self.overlay.get("converters", {}).get(f"{dto}.{field.name}")

    # -- requests ---------------------------------------------------------------------------------------------------

    def _check_request_overlay(self) -> None:
        """An overlay entry about a request or field that the protocol no longer defines is stale: fail so it gets removed."""
        fields = {r.name: {f.name for f in r.request_fields} for r in self.requests}
        for name in self.overlay.get("manual", {}).get("requests", []):
            if name not in fields:
                raise ModelError(f"overlay.toml lists '{name}' as a manual request, which the protocol does not define (remove it).")
        for key in self.overlay.get("request_nulls", {}).get("fields", []):
            request, _, field = key.partition(".")
            if field not in fields.get(request, ()):
                raise ModelError(f"overlay.toml [request_nulls] has '{key}', which the protocol does not define (remove it).")
        for key in self.overlay.get("refs", {}).get("optional", []):
            request, _, base = key.partition(".")
            known = fields.get(request, ())
            if base + "Name" not in known or base + "Uuid" not in known:
                raise ModelError(f"overlay.toml [refs] lists the optional ref '{key}', which the protocol does not define (remove it).")

    def is_manual_request(self, name: str) -> bool:
        """True for a request whose methods are written by hand (the generator leaves them alone)."""
        return name in self.overlay.get("manual", {}).get("requests", [])

    def generated_requests(self) -> list[Request]:
        return [r for r in self.requests if not self.is_manual_request(r.name)]

    @staticmethod
    def payload_type(request_name: str) -> str:
        return f"{request_name}RequestData"

    def request_params(self, request: Request) -> list[Param]:
        """The parameters of the methods of a request: required ones first, then the ones that can be left out.

        An <x>Name and an <x>Uuid field together identify an object and are one parameter <x>, of a ref type that holds
        either (SceneRef, InputRef or SourceRef). The canvasUuid field is a parameter of its own, and the last one.
        """
        fields = request.plain_request_fields
        by_name = {f.name: f for f in fields}
        optional_refs = set(self.overlay.get("refs", {}).get("optional", []))
        always_sent = set(self.overlay.get("request_nulls", {}).get("fields", []))
        positional: list[Param] = []
        defaulted: list[Param] = []
        canvas: list[Param] = []
        consumed: set[str] = set()

        for f in fields:
            if f.name in consumed:
                continue
            base = f.name[:-4] if f.name.endswith("Name") else ""
            uuid = by_name.get(base + "Uuid") if base else None
            if uuid is not None and f.vtype == "String" and uuid.vtype == "String":
                kind = self.ref_kinds.get(last_word(base))
                if kind is None:
                    raise ModelError(
                        f"{request.name}: '{f.name}' and '{uuid.name}' identify an object, but overlay.toml has no ref type for "
                        f"'{last_word(base)}'. Add it to [refs.kinds] (the type itself is written by hand in OBSClient/Classes)."
                    )
                if self.request_type(request.name, uuid).name != "Guid":
                    raise ModelError(f"{request.name}: '{uuid.name}' is not a Guid, so it cannot be half of a {kind}.")
                consumed |= {f.name, uuid.name}
                optional = f"{request.name}.{base}" in optional_refs
                param = Param(identifier(base), self._clr(kind, optional), (f, uuid), ref=True, has_default=optional)
                (defaulted if optional else positional).append(param)
                continue

            ctype = self.request_type(request.name, f)
            if f"{request.name}.{f.name}" in always_sent:
                # Accepts null without a default: the caller says explicitly that the value is to be removed.
                positional.append(Param(identifier(f.name), self._clr(ctype.name, True), (f,), always_sent=True))
            elif not ctype.nullable:
                positional.append(Param(identifier(f.name), ctype, (f,)))
            else:
                param = Param(identifier(f.name), ctype, (f,), has_default=True)
                (canvas if f.name == "canvasUuid" else defaulted).append(param)

        positional.sort(key=lambda p: not p.ref)
        defaulted.sort(key=lambda p: not p.ref)
        params = positional + defaulted + canvas
        names = [p.name for p in params]
        if len(set(names)) != len(names):
            raise ModelError(f"{request.name}: the parameters {sorted(n for n in names if names.count(n) > 1)} have the same name.")
        return params

    def returns(self, request: Request) -> Returns:
        """The response of a request: nothing, the value of its only field, or the type that holds all of its fields."""
        fields = request.plain_response_fields
        if not fields:
            return Returns("none", "", "", None)
        dto = self.response_dto(request.name)
        if len(fields) == 1:
            return Returns("field", self.data_type(dto, fields[0]).text, dto, fields[0])
        return Returns("dto", dto, dto, None)

    # -- ordinals ---------------------------------------------------------------------------------------------------

    def ordinals(self, enum: str) -> dict[str, int]:
        return dict(self.overlay.get("ordinals", {}).get(enum, {}))


# -- shared DTO views -----------------------------------------------------------------------------------------------


@dataclass
class DtoField:
    field: Field
    type: ClrType
    converter: str | None


@dataclass
class Dto:
    name: str
    base: str  # "IResponse" or "EventArgs"
    users: list[str]  # the requests or events that use this type
    descriptions: list[str]
    since: str
    fields: list[DtoField]


def _merge_dto(model: Model, name: str, base: str, users: list[tuple[str, str, str, list[Field]]]) -> Dto:
    """One type for every request or event that is mapped to it; their fields have to agree."""
    merged: dict[str, Field] = {}
    for user, _description, _since, fields in users:
        for f in fields:
            known = merged.get(f.name)
            if known is None:
                merged[f.name] = f
            elif known.vtype != f.vtype:
                raise ModelError(
                    f"{name}: '{f.name}' is {known.vtype} for one of its users and {f.vtype} for '{user}'; "
                    "give them types of their own in overlay.toml."
                )
    fields = [DtoField(f, model.data_type(name, f), model.converter(name, f)) for f in merged.values()]
    return Dto(
        name=name,
        base=base,
        users=[u[0] for u in users],
        descriptions=[u[1] for u in users],
        since=min((u[2] for u in users), key=lambda v: tuple(int(p) for p in v.split("."))),
        fields=fields,
    )


def response_dtos(model: Model) -> list[Dto]:
    groups: dict[str, list[tuple[str, str, str, list[Field]]]] = {}
    for request in model.requests:
        dto = model.response_dto(request.name)
        if dto == "EmptyResponse" or model.is_manual_dto(dto):
            continue
        groups.setdefault(dto, []).append((request.name, request.description, request.since, request.plain_response_fields))
    return [_merge_dto(model, name, "IResponse", users) for name, users in sorted(groups.items())]


def event_dtos(model: Model) -> list[Dto]:
    groups: dict[str, list[tuple[str, str, str, list[Field]]]] = {}
    for event in model.events:
        dto = model.event_dto(event.name)
        if not dto or model.is_manual_dto(dto):
            continue
        groups.setdefault(dto, []).append((event.name, event.description, event.since, event.plain_fields))
    return [_merge_dto(model, name, "EventArgs", users) for name, users in sorted(groups.items())]
