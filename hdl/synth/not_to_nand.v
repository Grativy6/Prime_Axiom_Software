// Yosys techmap used only after `abc -g NAND`.
// ABC is allowed to leave NOT cells; this replacement makes each one a
// tied-input NAND2 before the optimized-netlist validator runs.
module \$_NOT_ (A, Y);
    input A;
    output Y;
    \$_NAND_ _TECHMAP_REPLACE_ (.A(A), .B(A), .Y(Y));
endmodule
