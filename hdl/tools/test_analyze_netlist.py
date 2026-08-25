#!/usr/bin/env python3
"""Regression tests for Build 002 structural netlist validation."""

from __future__ import annotations

import unittest

from hdl.tools.analyze_netlist import Elaborator, NetlistError


def elaborate(modules: dict, top: str = "top", mode: str = "declared") -> dict:
    checker = Elaborator(modules, top, mode)
    checker.walk(top, top, {})
    return checker.result()


class AnalyzerRegressionTests(unittest.TestCase):
    def test_hierarchical_direct_and_constant_outputs_are_aliases(self) -> None:
        modules = {
            "top": {
                "ports": {
                    "a": {"direction": "input", "bits": [2]},
                    "y_alias": {"direction": "output", "bits": [3]},
                    "y_constant": {"direction": "output", "bits": [4]},
                },
                "cells": {
                    "u_alias": {
                        "type": "alias_child",
                        "connections": {
                            "a": [2],
                            "y_alias": [3],
                            "y_constant": [4],
                        },
                    }
                },
            },
            "alias_child": {
                "ports": {
                    "a": {"direction": "input", "bits": [2]},
                    "y_alias": {"direction": "output", "bits": [2]},
                    "y_constant": {"direction": "output", "bits": ["0"]},
                }
            },
        }

        metrics = elaborate(modules)

        self.assertEqual(metrics["nand2_static"], 0)
        self.assertEqual(metrics["input_bits"], 1)
        self.assertEqual(metrics["output_bits"], 2)
        self.assertEqual(metrics["combinational_loop_status"], "ACYCLIC")

    def test_duplicate_driver_is_rejected(self) -> None:
        modules = {
            "top": {
                "ports": {
                    "a": {"direction": "input", "bits": [2]},
                    "b": {"direction": "input", "bits": [3]},
                    "y": {"direction": "output", "bits": [4]},
                },
                "cells": {
                    "n0": {
                        "type": "$_NAND_",
                        "connections": {"A": [2], "B": [3], "Y": [4]},
                    },
                    "n1": {
                        "type": "$_NAND_",
                        "connections": {"A": [3], "B": [2], "Y": [4]},
                    },
                },
            }
        }

        with self.assertRaisesRegex(NetlistError, "duplicate drivers"):
            elaborate(modules, mode="optimized")

    def test_forbidden_cell_is_rejected(self) -> None:
        modules = {
            "top": {
                "ports": {
                    "a": {"direction": "input", "bits": [2]},
                    "b": {"direction": "input", "bits": [3]},
                    "y": {"direction": "output", "bits": [4]},
                },
                "cells": {
                    "xor0": {
                        "type": "$_XOR_",
                        "connections": {"A": [2], "B": [3], "Y": [4]},
                    }
                },
            }
        }

        with self.assertRaisesRegex(NetlistError, "forbidden or unresolved"):
            elaborate(modules, mode="optimized")

    def test_combinational_cycle_is_rejected(self) -> None:
        modules = {
            "top": {
                "ports": {
                    "a": {"direction": "input", "bits": [2]},
                    "y": {"direction": "output", "bits": [3]},
                },
                "netnames": {"feedback": {"bits": [4]}},
                "cells": {
                    "n0": {
                        "type": "$_NAND_",
                        "connections": {"A": [4], "B": [2], "Y": [3]},
                    },
                    "n1": {
                        "type": "$_NAND_",
                        "connections": {"A": [3], "B": [2], "Y": [4]},
                    },
                },
            }
        }

        with self.assertRaisesRegex(NetlistError, "combinational cycle"):
            elaborate(modules, mode="optimized")


if __name__ == "__main__":
    unittest.main()
