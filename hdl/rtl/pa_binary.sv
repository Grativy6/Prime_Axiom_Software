`default_nettype none

module pa_ripple_add #(
    parameter integer W = 4
)(
    input  wire [W-1:0] a,
    input  wire [W-1:0] b,
    input  wire         cin,
    output wire [W-1:0] sum,
    output wire         cout
);
    wire [W:0] carry;
    assign carry[0] = cin;
    generate
        for (genvar i = 0; i < W; i = i + 1) begin : g_add
            pa_full_adder u_fa(
                .a(a[i]), .b(b[i]), .cin(carry[i]),
                .sum(sum[i]), .cout(carry[i+1])
            );
        end
    endgenerate
    assign cout = carry[W];
endmodule

module pa_increment #(
    parameter integer W = 4
)(
    input  wire [W-1:0] a,
    output wire [W-1:0] y,
    output wire         overflow
);
    wire [W-1:0] zero = {W{1'b0}};
    pa_ripple_add #(.W(W)) u_add(
        .a(a), .b(zero), .cin(1'b1), .sum(y), .cout(overflow)
    );
endmodule

module pa_addsub #(
    parameter integer W = 4
)(
    input  wire [W-1:0] a,
    input  wire [W-1:0] b,
    input  wire         subtract,
    output wire [W-1:0] y,
    output wire         carry_out,
    output wire         borrow_out
);
    wire [W-1:0] bx;
    generate
        for (genvar i = 0; i < W; i = i + 1) begin : g_bxor
            pa_xor2 u_xor(.a(b[i]), .b(subtract), .y(bx[i]));
        end
    endgenerate
    pa_ripple_add #(.W(W)) u_add(
        .a(a), .b(bx), .cin(subtract), .sum(y), .cout(carry_out)
    );
    wire ncarry;
    pa_not u_not_carry(.a(carry_out), .y(ncarry));
    pa_and2 u_borrow(.a(subtract), .b(ncarry), .y(borrow_out));
endmodule

module pa_unsigned_compare #(
    parameter integer W = 4
)(
    input  wire [W-1:0] a,
    input  wire [W-1:0] b,
    output wire         equal,
    output wire         less_than
);
    wire [W-1:0] ignored_difference;
    wire carry_out;
    wire ignored_borrow;
    wire [W-1:0] equal_bits;
    pa_addsub #(.W(W)) u_sub(
        .a(a), .b(b), .subtract(1'b1), .y(ignored_difference),
        .carry_out(carry_out), .borrow_out(ignored_borrow)
    );
    generate
        for (genvar i = 0; i < W; i = i + 1) begin : g_eq
            pa_xnor2 u_xnor(.a(a[i]), .b(b[i]), .y(equal_bits[i]));
        end
    endgenerate
    pa_reduce_and #(.W(W)) u_eq(.a(equal_bits), .y(equal));
    pa_not u_lt(.a(carry_out), .y(less_than));
endmodule

module pa_word_minmax #(
    parameter integer W = 4
)(
    input  wire [W-1:0] a,
    input  wire [W-1:0] b,
    output wire [W-1:0] minimum,
    output wire [W-1:0] maximum
);
    wire ignored_equal;
    wire a_less_b;
    pa_unsigned_compare #(.W(W)) u_cmp(
        .a(a), .b(b), .equal(ignored_equal), .less_than(a_less_b)
    );
    pa_mux_word #(.W(W)) u_min(.d0(b), .d1(a), .sel(a_less_b), .y(minimum));
    pa_mux_word #(.W(W)) u_max(.d0(a), .d1(b), .sel(a_less_b), .y(maximum));
endmodule

module pa_shift_add_multiplier #(
    parameter integer W = 4
)(
    input  wire [W-1:0]   a,
    input  wire [W-1:0]   b,
    output wire [2*W-1:0] product
);
    wire [2*W-1:0] stage [0:W];
    wire [2*W-1:0] partial [0:W-1];
    wire [W-1:0] ignored_carry;
    assign stage[0] = {2*W{1'b0}};
    generate
        for (genvar row = 0; row < W; row = row + 1) begin : g_row
            for (genvar bit_index = 0; bit_index < 2*W; bit_index = bit_index + 1) begin : g_bit
                if ((bit_index >= row) && (bit_index < row + W)) begin : g_product_bit
                    pa_and2 u_and(
                        .a(a[bit_index-row]), .b(b[row]), .y(partial[row][bit_index])
                    );
                end else begin : g_zero_bit
                    assign partial[row][bit_index] = 1'b0;
                end
            end
            pa_ripple_add #(.W(2*W)) u_add(
                .a(stage[row]), .b(partial[row]), .cin(1'b0),
                .sum(stage[row+1]), .cout(ignored_carry[row])
            );
        end
    endgenerate
    assign product = stage[W];
endmodule

module pa_binary_counter #(
    parameter integer W = 4
)(
    input  wire         clk,
    input  wire         reset,
    input  wire         enable,
    output wire [W-1:0] count,
    output wire         overflow
);
    wire [W-1:0] next_count;
    pa_increment #(.W(W)) u_increment(.a(count), .y(next_count), .overflow(overflow));
    pa_register  #(.W(W)) u_register(
        .clk(clk), .reset(reset), .enable(enable), .d(next_count), .q(count)
    );
endmodule

// Opcode: 00 ADD, 01 SUB, 10 MUL, 11 COMPARE.
// COMPARE result bit 0 is equality and bit 1 is unsigned less-than.
module pa_bin_fu #(
    parameter integer W = 4
)(
    input  wire [W-1:0]   a,
    input  wire [W-1:0]   b,
    input  wire [1:0]     opcode,
    output wire [2*W-1:0] result,
    output wire [3:0]     status
);
    wire [W-1:0] add_y;
    wire [W-1:0] sub_y;
    wire add_carry;
    wire add_borrow_ignored;
    wire sub_carry_ignored;
    wire sub_borrow;
    wire equal;
    wire less_than;
    wire [2*W-1:0] product;
    wire product_high_nonzero;
    wire [2*W-1:0] add_ext;
    wire [2*W-1:0] sub_ext;
    wire [2*W-1:0] cmp_ext;
    wire [2*W-1:0] low_pair;
    wire [2*W-1:0] high_pair;

    pa_addsub #(.W(W)) u_add(
        .a(a), .b(b), .subtract(1'b0), .y(add_y),
        .carry_out(add_carry), .borrow_out(add_borrow_ignored)
    );
    pa_addsub #(.W(W)) u_sub(
        .a(a), .b(b), .subtract(1'b1), .y(sub_y),
        .carry_out(sub_carry_ignored), .borrow_out(sub_borrow)
    );
    pa_unsigned_compare #(.W(W)) u_cmp(
        .a(a), .b(b), .equal(equal), .less_than(less_than)
    );
    pa_shift_add_multiplier #(.W(W)) u_mul(.a(a), .b(b), .product(product));
    pa_reduce_or #(.W(W)) u_high_or(.a(product[2*W-1:W]), .y(product_high_nonzero));

    assign add_ext = {{W{1'b0}}, add_y};
    assign sub_ext = {{W{1'b0}}, sub_y};
    assign cmp_ext = {{(2*W-2){1'b0}}, less_than, equal};
    pa_mux_word #(.W(2*W)) u_low(.d0(add_ext), .d1(sub_ext), .sel(opcode[0]), .y(low_pair));
    pa_mux_word #(.W(2*W)) u_high(.d0(product), .d1(cmp_ext), .sel(opcode[0]), .y(high_pair));
    pa_mux_word #(.W(2*W)) u_result(.d0(low_pair), .d1(high_pair), .sel(opcode[1]), .y(result));

    // Status is deliberately stable across opcodes: carry, borrow, equal,
    // and product-high-nonzero. Consumers select the meaningful bit.
    assign status = {product_high_nonzero, equal, sub_borrow, add_carry};
endmodule

module pa_bin_fu_registered #(
    parameter integer W = 4
)(
    input  wire           clk,
    input  wire           reset,
    input  wire           load_operands,
    input  wire           commit_result,
    input  wire [W-1:0]   a_in,
    input  wire [W-1:0]   b_in,
    input  wire [1:0]     opcode_in,
    output wire [2*W-1:0] result_q,
    output wire [3:0]     status_q
);
    wire [W-1:0] a_q;
    wire [W-1:0] b_q;
    wire [1:0] opcode_q;
    wire [2*W-1:0] result_d;
    wire [3:0] status_d;
    pa_register #(.W(W)) u_a(
        .clk(clk), .reset(reset), .enable(load_operands), .d(a_in), .q(a_q)
    );
    pa_register #(.W(W)) u_b(
        .clk(clk), .reset(reset), .enable(load_operands), .d(b_in), .q(b_q)
    );
    pa_register #(.W(2)) u_op(
        .clk(clk), .reset(reset), .enable(load_operands), .d(opcode_in), .q(opcode_q)
    );
    pa_bin_fu #(.W(W)) u_fu(
        .a(a_q), .b(b_q), .opcode(opcode_q), .result(result_d), .status(status_d)
    );
    pa_register #(.W(2*W)) u_result(
        .clk(clk), .reset(reset), .enable(commit_result), .d(result_d), .q(result_q)
    );
    pa_register #(.W(4)) u_status(
        .clk(clk), .reset(reset), .enable(commit_result), .d(status_d), .q(status_q)
    );
endmodule

`default_nettype wire
