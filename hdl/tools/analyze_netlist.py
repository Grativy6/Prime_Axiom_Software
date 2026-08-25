#!/usr/bin/env python3
"""Validate and measure a Yosys JSON netlist on the Build 002 NAND floor."""

from __future__ import annotations

import argparse
import hashlib
import json
import re
import sys
from collections import Counter, defaultdict, deque
from dataclasses import dataclass
from pathlib import Path
from typing import Any


PROTOCOL = "PAH-BUILD002-CONF0001"
CONSTANTS = {"0", "1"}
NAND_TYPES = {"$_NAND_"}
DFF_PATTERN = re.compile(r"^\$_(?:DFF|DFFE|SDFF|SDFFE|ADFF|ADFFE)")


class NetlistError(RuntimeError):
    pass


class Aliases:
    """Union-find for direct hierarchical aliases and constant output ports."""

    def __init__(self) -> None:
        self.parent: dict[str, str] = {}

    def add(self, value: str) -> None:
        self.parent.setdefault(value, value)

    def find(self, value: str) -> str:
        self.add(value)
        parent = self.parent[value]
        if parent != value:
            self.parent[value] = self.find(parent)
        return self.parent[value]

    def union(self, left: str, right: str) -> None:
        left_root = self.find(left)
        right_root = self.find(right)
        if left_root == right_root:
            return
        left_constant = left_root.startswith("const:")
        right_constant = right_root.startswith("const:")
        if left_constant and right_constant and left_root != right_root:
            raise NetlistError(f"conflicting constant aliases: {left_root} and {right_root}")
        if left_constant:
            root, child = left_root, right_root
        elif right_constant:
            root, child = right_root, left_root
        else:
            root, child = sorted((left_root, right_root))
        self.parent[child] = root


@dataclass(frozen=True)
class Gate:
    name: str
    inputs: tuple[str, str]
    output: str
    region: str


@dataclass(frozen=True)
class StateBit:
    name: str
    inputs: tuple[str, ...]
    d: str
    q: str
    region: str


def normalized_type(value: str) -> str:
    return value.lstrip("\\")


def bit_key(path: str, bit: int | str, binding: dict[int, str]) -> str:
    if isinstance(bit, str):
        if bit not in CONSTANTS:
            raise NetlistError(f"unsupported X/Z constant {bit!r} at {path}")
        return f"const:{bit}"
    return binding.get(bit, f"{path}#n{bit}")


def first_connection(cell: dict[str, Any], *names: str) -> list[int | str]:
    connections = cell.get("connections", {})
    for name in names:
        if name in connections:
            return connections[name]
    raise NetlistError(f"missing cell port; expected one of {names}")


class Elaborator:
    def __init__(self, modules: dict[str, Any], top: str, mode: str) -> None:
        self.modules = modules
        self.top = top
        self.mode = mode
        self.nets: set[str] = set()
        self.gates: list[Gate] = []
        self.state: list[StateBit] = []
        self.histogram: Counter[str] = Counter()
        self.drivers: dict[str, str] = {}
        self.driver_regions: dict[str, str] = {}
        self.sinks: list[tuple[str, str, str]] = []
        self.top_inputs: list[str] = []
        self.top_outputs: list[str] = []
        self.stack: list[str] = []
        self.aliases = Aliases()

    def add_driver(self, net: str, driver: str, region: str) -> None:
        if net.startswith("const:"):
            raise NetlistError(f"cell {driver} drives constant net {net}")
        self.aliases.add(net)
        if net in self.drivers:
            raise NetlistError(
                f"duplicate drivers for {net}: {self.drivers[net]} and {driver}"
            )
        self.drivers[net] = driver
        self.driver_regions[net] = region

    def add_sink(self, net: str, sink: str, region: str) -> None:
        self.aliases.add(net)
        self.sinks.append((net, sink, region))

    def register_module_nets(
        self, module: dict[str, Any], path: str, binding: dict[int, str]
    ) -> None:
        bit_values: set[int | str] = set()
        for port in module.get("ports", {}).values():
            bit_values.update(port.get("bits", []))
        for net in module.get("netnames", {}).values():
            bit_values.update(net.get("bits", []))
        for bit in bit_values:
            key = bit_key(path, bit, binding)
            self.aliases.add(key)
            if not key.startswith("const:"):
                self.nets.add(key)

    def leaf_nand(
        self,
        cell: dict[str, Any],
        cell_path: str,
        path: str,
        binding: dict[int, str],
        region: str,
        declared: bool,
    ) -> None:
        a = bit_key(path, first_connection(cell, "a", "A")[0], binding)
        b = bit_key(path, first_connection(cell, "b", "B")[0], binding)
        y = bit_key(path, first_connection(cell, "y", "Y")[0], binding)
        self.gates.append(Gate(cell_path, (a, b), y, region))
        self.histogram["pa_nand2" if declared else "$_NAND_"] += 1
        self.add_sink(a, f"{cell_path}.A", region)
        self.add_sink(b, f"{cell_path}.B", region)
        self.add_driver(y, cell_path, region)

    def leaf_dff(
        self,
        cell: dict[str, Any],
        cell_path: str,
        path: str,
        binding: dict[int, str],
        region: str,
        declared: bool,
    ) -> None:
        d_bits = first_connection(cell, "d", "D")
        q_bits = first_connection(cell, "q", "Q")
        if len(d_bits) != len(q_bits):
            raise NetlistError(f"D/Q width mismatch at {cell_path}")
        directions = cell.get("port_directions", {})
        input_ports = [name for name, direction in directions.items() if direction == "input"]
        for index, (d_bit, q_bit) in enumerate(zip(d_bits, q_bits, strict=True)):
            d = bit_key(path, d_bit, binding)
            q = bit_key(path, q_bit, binding)
            inputs: list[str] = []
            for port_name in input_ports:
                for raw_bit in cell.get("connections", {}).get(port_name, []):
                    net = bit_key(path, raw_bit, binding)
                    inputs.append(net)
                    self.add_sink(net, f"{cell_path}.{port_name}", region)
            state_name = f"{cell_path}[{index}]"
            self.state.append(StateBit(state_name, tuple(inputs), d, q, region))
            self.add_driver(q, state_name, region)
        self.histogram["pa_dff" if declared else normalized_type(cell["type"])] += len(d_bits)

    def walk(
        self,
        module_name: str,
        path: str,
        binding: dict[int, str],
        inherited_region: str = "",
    ) -> None:
        if module_name in self.stack:
            raise NetlistError(f"recursive module hierarchy: {' -> '.join(self.stack + [module_name])}")
        if module_name not in self.modules:
            raise NetlistError(f"missing module definition {module_name!r} at {path}")
        self.stack.append(module_name)
        module = self.modules[module_name]
        self.register_module_nets(module, path, binding)

        if path == self.top:
            for port_name, port in module.get("ports", {}).items():
                for index, raw_bit in enumerate(port.get("bits", [])):
                    net = bit_key(path, raw_bit, binding)
                    if port.get("direction") == "input":
                        self.top_inputs.append(net)
                        self.driver_regions.setdefault(net, "")
                    elif port.get("direction") == "output":
                        self.top_outputs.append(net)
                        self.add_sink(net, f"PORT.{port_name}[{index}]", "")
                    else:
                        raise NetlistError(f"unsupported inout port {port_name!r}")

        for cell_name in sorted(module.get("cells", {})):
            cell = module["cells"][cell_name]
            cell_type = normalized_type(cell["type"])
            cell_path = f"{path}/{cell_name}"
            attrs = cell.get("attributes", {})
            region = str(attrs.get("pa_region", inherited_region))

            if self.mode == "declared" and cell_type == "pa_nand2":
                self.leaf_nand(cell, cell_path, path, binding, region, True)
                continue
            if self.mode == "declared" and cell_type == "pa_dff":
                self.leaf_dff(cell, cell_path, path, binding, region, True)
                continue
            if self.mode == "optimized" and cell_type in NAND_TYPES:
                self.leaf_nand(cell, cell_path, path, binding, region, False)
                continue
            if self.mode == "optimized" and DFF_PATTERN.match(cell_type):
                self.leaf_dff(cell, cell_path, path, binding, region, False)
                continue

            if cell["type"] in self.modules:
                child_name = cell["type"]
            elif cell_type in self.modules:
                child_name = cell_type
            else:
                raise NetlistError(
                    f"forbidden or unresolved {self.mode} cell {cell_type!r} at {cell_path}"
                )
            child = self.modules[child_name]
            child_binding: dict[int, str] = {}
            parent_connections = cell.get("connections", {})
            for port_name, child_port in child.get("ports", {}).items():
                if port_name not in parent_connections:
                    raise NetlistError(f"unconnected hierarchical port {cell_path}.{port_name}")
                parent_bits = parent_connections[port_name]
                child_bits = child_port.get("bits", [])
                if len(parent_bits) != len(child_bits):
                    raise NetlistError(f"hierarchical width mismatch at {cell_path}.{port_name}")
                for child_bit, parent_bit in zip(child_bits, parent_bits, strict=True):
                    parent_net = bit_key(path, parent_bit, binding)
                    self.aliases.add(parent_net)
                    if isinstance(child_bit, str):
                        if child_bit not in CONSTANTS:
                            raise NetlistError(f"unsupported constant child port bit at {cell_path}.{port_name}")
                        self.aliases.union(parent_net, f"const:{child_bit}")
                    elif child_bit in child_binding:
                        self.aliases.union(child_binding[child_bit], parent_net)
                    else:
                        child_binding[child_bit] = parent_net
            self.walk(child_name, cell_path, child_binding, region)

        self.stack.pop()

    def result(self) -> dict[str, Any]:
        canonical = self.aliases.find
        gates = [
            Gate(gate.name, (canonical(gate.inputs[0]), canonical(gate.inputs[1])), canonical(gate.output), gate.region)
            for gate in self.gates
        ]
        state = [
            StateBit(bit.name, tuple(canonical(net) for net in bit.inputs), canonical(bit.d), canonical(bit.q), bit.region)
            for bit in self.state
        ]
        canonical_drivers: dict[str, str] = {}
        canonical_driver_regions: dict[str, str] = {}
        for net, driver in self.drivers.items():
            resolved = canonical(net)
            if resolved.startswith("const:"):
                raise NetlistError(f"cell {driver} drives aliased constant net {resolved}")
            if resolved in canonical_drivers and canonical_drivers[resolved] != driver:
                raise NetlistError(
                    f"duplicate drivers for {resolved}: {canonical_drivers[resolved]} and {driver}"
                )
            canonical_drivers[resolved] = driver
            canonical_driver_regions[resolved] = self.driver_regions.get(net, "")
        sinks = [(canonical(net), sink, region) for net, sink, region in self.sinks]
        top_inputs = [canonical(net) for net in self.top_inputs]
        top_outputs = [canonical(net) for net in self.top_outputs]
        nets = {
            canonical(net) for net in self.nets if not canonical(net).startswith("const:")
        }

        gate_by_driver = {gate.output: gate for gate in gates}
        indegree: dict[str, int] = {gate.name: 0 for gate in gates}
        successors: dict[str, list[str]] = defaultdict(list)
        gate_by_name = {gate.name: gate for gate in gates}
        for gate in gates:
            dependencies: set[str] = set()
            for net in gate.inputs:
                upstream = gate_by_driver.get(net)
                if upstream is not None:
                    dependencies.add(upstream.name)
            indegree[gate.name] = len(dependencies)
            for dependency in dependencies:
                successors[dependency].append(gate.name)

        queue = deque(sorted(name for name, degree in indegree.items() if degree == 0))
        depth: dict[str, int] = {}
        while queue:
            name = queue.popleft()
            gate = gate_by_name[name]
            upstream_depths = [
                depth[gate_by_driver[net].name]
                for net in gate.inputs
                if net in gate_by_driver
            ]
            depth[name] = 1 + (max(upstream_depths) if upstream_depths else 0)
            for successor in sorted(successors.get(name, [])):
                indegree[successor] -= 1
                if indegree[successor] == 0:
                    queue.append(successor)
        looped = len(depth) != len(gates)
        if looped:
            remaining = sorted(name for name in indegree if name not in depth)
            raise NetlistError(f"combinational cycle detected through {remaining[:8]}")

        endpoints = list(top_outputs) + [bit.d for bit in state]
        endpoint_depth = [
            depth[gate_by_driver[net].name]
            for net in endpoints
            if net in gate_by_driver
        ]
        fanout: Counter[str] = Counter(net for net, _, _ in sinks)
        cross_region = 0
        for net, _, sink_region in sinks:
            source_region = canonical_driver_regions.get(net, "")
            if source_region and sink_region and source_region != sink_region:
                cross_region += 1

        undriven = sorted(
            net
            for net in nets
            if net not in canonical_drivers and net not in top_inputs
            and fanout.get(net, 0) > 0
        )
        if undriven:
            raise NetlistError(f"undriven used nets: {undriven[:8]}")

        return {
            "nand2_static": len(gates),
            "dff_static": len(state),
            "state_bits": len(state),
            "input_bits": len(top_inputs),
            "output_bits": len(top_outputs),
            "port_bits": len(top_inputs) + len(top_outputs),
            "wire_bits": len(nets),
            "connections_static": len(sinks),
            "max_fanout": max(fanout.values(), default=0),
            "cross_lane_connections": cross_region,
            "unit_nand_critical_depth": max(endpoint_depth, default=0),
            "combinational_loop_status": "ACYCLIC",
            "cell_type_histogram": dict(sorted(self.histogram.items())),
        }


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--input", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--top", required=True)
    parser.add_argument("--mode", choices=("declared", "optimized"), required=True)
    args = parser.parse_args()

    raw = args.input.read_bytes()
    document = json.loads(raw)
    modules = document.get("modules", {})
    if args.top not in modules:
        raise NetlistError(f"top module {args.top!r} absent from JSON")
    elaborator = Elaborator(modules, args.top, args.mode)
    elaborator.walk(args.top, args.top, {})
    metrics = elaborator.result()
    receipt = {
        "schema": "prime-axiom-nand-netlist-metrics-v1",
        "protocol": PROTOCOL,
        "top": args.top,
        "evidence_class": (
            "STRUCTURAL_DECLARED" if args.mode == "declared" else "STRUCTURAL_OPTIMIZED"
        ),
        "netlist_sha256": hashlib.sha256(raw).hexdigest(),
        **metrics,
        "validation_status": "PASS",
    }
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(
        json.dumps(receipt, indent=2, sort_keys=True) + "\n", encoding="utf-8", newline="\n"
    )
    print(json.dumps(receipt, sort_keys=True))
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (NetlistError, OSError, ValueError, KeyError, TypeError) as error:
        print(f"NETLIST_VALIDATION_FAILED: {error}", file=sys.stderr)
        raise SystemExit(2)
