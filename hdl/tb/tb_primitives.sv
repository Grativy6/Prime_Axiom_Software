`timescale 1ns/1ps
`default_nettype none

module tb_primitives;
    reg a, b, c;
    wire n, inv, aand, oor, xxor, xxnor, mux;
    wire hsum, hcarry, fsum, fcarry;
    integer av, bv, cv;

    pa_nand2      u_nand(.a(a), .b(b), .y(n));
    pa_not        u_not(.a(a), .y(inv));
    pa_and2       u_and(.a(a), .b(b), .y(aand));
    pa_or2        u_or(.a(a), .b(b), .y(oor));
    pa_xor2       u_xor(.a(a), .b(b), .y(xxor));
    pa_xnor2      u_xnor(.a(a), .b(b), .y(xxnor));
    pa_mux2       u_mux(.d0(a), .d1(b), .sel(c), .y(mux));
    pa_half_adder u_half(.a(a), .b(b), .sum(hsum), .carry(hcarry));
    pa_full_adder u_full(.a(a), .b(b), .cin(c), .sum(fsum), .cout(fcarry));

    initial begin
        for (av = 0; av < 2; av = av + 1) begin
            for (bv = 0; bv < 2; bv = bv + 1) begin
                for (cv = 0; cv < 2; cv = cv + 1) begin
                    a = av; b = bv; c = cv; #1;
                    if (n !== !(av && bv)) $fatal(1, "NAND mismatch");
                    if (inv !== !av) $fatal(1, "NOT mismatch");
                    if (aand !== (av & bv)) $fatal(1, "AND mismatch");
                    if (oor !== (av | bv)) $fatal(1, "OR mismatch");
                    if (xxor !== (av ^ bv)) $fatal(1, "XOR mismatch");
                    if (xxnor !== !(av ^ bv)) $fatal(1, "XNOR mismatch");
                    if (mux !== (cv ? bv : av)) $fatal(1, "MUX mismatch");
                    if ({hcarry, hsum} !== (av + bv)) $fatal(1, "half-adder mismatch");
                    if ({fcarry, fsum} !== (av + bv + cv)) $fatal(1, "full-adder mismatch");
                end
            end
        end
        $display("PASS tb_primitives truth_tables=8");
        $finish;
    end
endmodule

`default_nettype wire
