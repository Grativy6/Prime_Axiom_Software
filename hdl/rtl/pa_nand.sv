`default_nettype none

// Prime Axiom Hardware Build 002
// The only primitive combinational cell in design RTL is pa_nand2.
// Derived gates below are deliberately structural and are shared by both
// conventional and experimental lineages.

module pa_nand2(
    input  wire a,
    input  wire b,
    output wire y
);
    assign y = ~(a & b);
endmodule

module pa_not(
    input  wire a,
    output wire y
);
    pa_nand2 g0(.a(a), .b(a), .y(y));
endmodule

module pa_and2(
    input  wire a,
    input  wire b,
    output wire y
);
    wire n;
    pa_nand2 g0(.a(a), .b(b), .y(n));
    pa_nand2 g1(.a(n), .b(n), .y(y));
endmodule

module pa_or2(
    input  wire a,
    input  wire b,
    output wire y
);
    wire na;
    wire nb;
    pa_nand2 g0(.a(a),  .b(a),  .y(na));
    pa_nand2 g1(.a(b),  .b(b),  .y(nb));
    pa_nand2 g2(.a(na), .b(nb), .y(y));
endmodule

module pa_xor2(
    input  wire a,
    input  wire b,
    output wire y
);
    wire n0;
    wire n1;
    wire n2;
    pa_nand2 g0(.a(a),  .b(b),  .y(n0));
    pa_nand2 g1(.a(a),  .b(n0), .y(n1));
    pa_nand2 g2(.a(b),  .b(n0), .y(n2));
    pa_nand2 g3(.a(n1), .b(n2), .y(y));
endmodule

module pa_xnor2(
    input  wire a,
    input  wire b,
    output wire y
);
    wire x;
    pa_xor2 gx(.a(a), .b(b), .y(x));
    pa_not   gn(.a(x), .y(y));
endmodule

module pa_mux2(
    input  wire d0,
    input  wire d1,
    input  wire sel,
    output wire y
);
    wire nsel;
    wire n0;
    wire n1;
    pa_nand2 g0(.a(sel),  .b(sel),  .y(nsel));
    pa_nand2 g1(.a(d0),   .b(nsel), .y(n0));
    pa_nand2 g2(.a(d1),   .b(sel),  .y(n1));
    pa_nand2 g3(.a(n0),   .b(n1),   .y(y));
endmodule

module pa_half_adder(
    input  wire a,
    input  wire b,
    output wire sum,
    output wire carry
);
    wire n0;
    wire n1;
    wire n2;
    pa_nand2 g0(.a(a),  .b(b),  .y(n0));
    pa_nand2 g1(.a(a),  .b(n0), .y(n1));
    pa_nand2 g2(.a(b),  .b(n0), .y(n2));
    pa_nand2 g3(.a(n1), .b(n2), .y(sum));
    pa_nand2 g4(.a(n0), .b(n0), .y(carry));
endmodule

// Nine-NAND full adder. n0 and n3 are the complemented carry terms, so the
// final NAND also realizes their OR without introducing another gate family.
module pa_full_adder(
    input  wire a,
    input  wire b,
    input  wire cin,
    output wire sum,
    output wire cout
);
    wire n0;
    wire n1;
    wire n2;
    wire axb;
    wire n3;
    wire n4;
    wire n5;
    pa_nand2 g0(.a(a),   .b(b),   .y(n0));
    pa_nand2 g1(.a(a),   .b(n0),  .y(n1));
    pa_nand2 g2(.a(b),   .b(n0),  .y(n2));
    pa_nand2 g3(.a(n1),  .b(n2),  .y(axb));
    pa_nand2 g4(.a(axb), .b(cin), .y(n3));
    pa_nand2 g5(.a(axb), .b(n3),  .y(n4));
    pa_nand2 g6(.a(cin), .b(n3),  .y(n5));
    pa_nand2 g7(.a(n4),  .b(n5),  .y(sum));
    pa_nand2 g8(.a(n0),  .b(n3),  .y(cout));
endmodule

module pa_reduce_or #(
    parameter integer W = 1
)(
    input  wire [W-1:0] a,
    output wire         y
);
    wire [W-1:0] chain;
    assign chain[0] = a[0];
    generate
        for (genvar i = 1; i < W; i = i + 1) begin : g_or
            pa_or2 u_or(.a(chain[i-1]), .b(a[i]), .y(chain[i]));
        end
    endgenerate
    assign y = chain[W-1];
endmodule

module pa_reduce_and #(
    parameter integer W = 1
)(
    input  wire [W-1:0] a,
    output wire         y
);
    wire [W-1:0] chain;
    assign chain[0] = a[0];
    generate
        for (genvar i = 1; i < W; i = i + 1) begin : g_and
            pa_and2 u_and(.a(chain[i-1]), .b(a[i]), .y(chain[i]));
        end
    endgenerate
    assign y = chain[W-1];
endmodule

module pa_mux_word #(
    parameter integer W = 1
)(
    input  wire [W-1:0] d0,
    input  wire [W-1:0] d1,
    input  wire         sel,
    output wire [W-1:0] y
);
    generate
        for (genvar i = 0; i < W; i = i + 1) begin : g_mux
            pa_mux2 u_mux(.d0(d0[i]), .d1(d1[i]), .sel(sel), .y(y[i]));
        end
    endgenerate
endmodule

// State is deliberately not reduced to NAND gates by this logical model.
// Each pa_dff instance is one separately charged edge-delimited state bit.
module pa_dff(
    input  wire clk,
    input  wire reset,
    input  wire enable,
    input  wire d,
    output reg  q
);
    always @(posedge clk) begin
        if (reset)
            q <= 1'b0;
        else if (enable)
            q <= d;
    end
endmodule

module pa_register #(
    parameter integer W = 1
)(
    input  wire         clk,
    input  wire         reset,
    input  wire         enable,
    input  wire [W-1:0] d,
    output wire [W-1:0] q
);
    generate
        for (genvar i = 0; i < W; i = i + 1) begin : g_dff
            pa_dff u_dff(.clk(clk), .reset(reset), .enable(enable), .d(d[i]), .q(q[i]));
        end
    endgenerate
endmodule

`default_nettype wire
