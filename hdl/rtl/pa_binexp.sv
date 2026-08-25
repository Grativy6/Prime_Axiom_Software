`default_nettype none

// Binary-exponent realization of the bounded S4 valuation state.
// Lane widths and caps are explicit parameters because the legal state space
// is a product of finite chains, not a uniform W-bit magnitude word.

module pa_lane_compose #(
    parameter integer EW = 2,
    parameter integer CAP = 3
)(
    input  wire [EW-1:0] a,
    input  wire [EW-1:0] b,
    output wire [EW-1:0] y,
    output wire          saturated
);
    localparam [EW:0] CAP_EXT = CAP;
    wire [EW:0] a_ext = {1'b0, a};
    wire [EW:0] b_ext = {1'b0, b};
    wire [EW:0] sum_ext;
    wire ignored_carry;
    wire equal_cap;
    wire less_cap;
    pa_ripple_add #(.W(EW+1)) u_add(
        .a(a_ext), .b(b_ext), .cin(1'b0), .sum(sum_ext), .cout(ignored_carry)
    );
    pa_unsigned_compare #(.W(EW+1)) u_cmp(
        .a(CAP_EXT), .b(sum_ext), .equal(equal_cap), .less_than(less_cap)
    );
    assign saturated = less_cap;
    pa_mux_word #(.W(EW)) u_clamp(
        .d0(sum_ext[EW-1:0]), .d1(CAP_EXT[EW-1:0]), .sel(saturated), .y(y)
    );
endmodule

module pa_lane_cancel #(
    parameter integer EW = 2
)(
    input  wire [EW-1:0] a,
    input  wire [EW-1:0] b,
    output wire [EW-1:0] difference,
    output wire          underflow
);
    wire ignored_carry;
    wire ignored_borrow;
    wire ignored_equal;
    pa_addsub #(.W(EW)) u_sub(
        .a(a), .b(b), .subtract(1'b1), .y(difference),
        .carry_out(ignored_carry), .borrow_out(ignored_borrow)
    );
    pa_unsigned_compare #(.W(EW)) u_cmp(
        .a(a), .b(b), .equal(ignored_equal), .less_than(underflow)
    );
endmodule

module pa_lane_le #(
    parameter integer EW = 2
)(
    input  wire [EW-1:0] a,
    input  wire [EW-1:0] b,
    output wire          less_or_equal
);
    wire equal;
    wire less;
    pa_unsigned_compare #(.W(EW)) u_cmp(.a(a), .b(b), .equal(equal), .less_than(less));
    pa_or2 u_or(.a(equal), .b(less), .y(less_or_equal));
endmodule

module pa_binexp_compose_s4 #(
    parameter integer E2W = 2, parameter integer T2 = 3,
    parameter integer E3W = 2, parameter integer T3 = 2,
    parameter integer E5W = 1, parameter integer T5 = 1,
    parameter integer E7W = 1, parameter integer T7 = 1
)(
    input  wire             zero_a,
    input  wire             zero_b,
    input  wire [E2W-1:0]   a2,
    input  wire [E3W-1:0]   a3,
    input  wire [E5W-1:0]   a5,
    input  wire [E7W-1:0]   a7,
    input  wire [E2W-1:0]   b2,
    input  wire [E3W-1:0]   b3,
    input  wire [E5W-1:0]   b5,
    input  wire [E7W-1:0]   b7,
    output wire             zero_y,
    output wire [E2W-1:0]   y2,
    output wire [E3W-1:0]   y3,
    output wire [E5W-1:0]   y5,
    output wire [E7W-1:0]   y7,
    output wire [3:0]       saturated
);
    wire [E2W-1:0] raw2;
    wire [E3W-1:0] raw3;
    wire [E5W-1:0] raw5;
    wire [E7W-1:0] raw7;
    wire [3:0] raw_sat;
    wire nonzero;
    wire [E2W-1:0] z2 = {E2W{1'b0}};
    wire [E3W-1:0] z3 = {E3W{1'b0}};
    wire [E5W-1:0] z5 = {E5W{1'b0}};
    wire [E7W-1:0] z7 = {E7W{1'b0}};

    pa_or2 u_zero(.a(zero_a), .b(zero_b), .y(zero_y));
    pa_not u_nonzero(.a(zero_y), .y(nonzero));
    (* pa_region = "lane2" *) pa_lane_compose #(.EW(E2W), .CAP(T2)) u_l2(
        .a(a2), .b(b2), .y(raw2), .saturated(raw_sat[0])
    );
    (* pa_region = "lane3" *) pa_lane_compose #(.EW(E3W), .CAP(T3)) u_l3(
        .a(a3), .b(b3), .y(raw3), .saturated(raw_sat[1])
    );
    (* pa_region = "lane5" *) pa_lane_compose #(.EW(E5W), .CAP(T5)) u_l5(
        .a(a5), .b(b5), .y(raw5), .saturated(raw_sat[2])
    );
    (* pa_region = "lane7" *) pa_lane_compose #(.EW(E7W), .CAP(T7)) u_l7(
        .a(a7), .b(b7), .y(raw7), .saturated(raw_sat[3])
    );
    pa_mux_word #(.W(E2W)) u_z2(.d0(raw2), .d1(z2), .sel(zero_y), .y(y2));
    pa_mux_word #(.W(E3W)) u_z3(.d0(raw3), .d1(z3), .sel(zero_y), .y(y3));
    pa_mux_word #(.W(E5W)) u_z5(.d0(raw5), .d1(z5), .sel(zero_y), .y(y5));
    pa_mux_word #(.W(E7W)) u_z7(.d0(raw7), .d1(z7), .sel(zero_y), .y(y7));
    generate
        for (genvar i = 0; i < 4; i = i + 1) begin : g_sat
            pa_and2 u_and(.a(raw_sat[i]), .b(nonzero), .y(saturated[i]));
        end
    endgenerate
endmodule

// Chainable resident-state adapter.  `bad_*[lane]` is persistent metadata
// saying that the stored lane is a clamped lower bound rather than an exact
// exponent.  New saturation and all prior bad tags are conserved.  A zero
// product is exact regardless of operand lane metadata because its explicit
// zero tag, not its exponent payload, denotes the result.
module pa_binexp_checked_compose_s4 #(
    parameter integer E2W = 2, parameter integer T2 = 3,
    parameter integer E3W = 2, parameter integer T3 = 2,
    parameter integer E5W = 1, parameter integer T5 = 1,
    parameter integer E7W = 1, parameter integer T7 = 1
)(
    input  wire             zero_a,
    input  wire             zero_b,
    input  wire [E2W-1:0]   a2,
    input  wire [E3W-1:0]   a3,
    input  wire [E5W-1:0]   a5,
    input  wire [E7W-1:0]   a7,
    input  wire [E2W-1:0]   b2,
    input  wire [E3W-1:0]   b3,
    input  wire [E5W-1:0]   b5,
    input  wire [E7W-1:0]   b7,
    input  wire [3:0]       bad_a,
    input  wire [3:0]       bad_b,
    output wire             zero_y,
    output wire [E2W-1:0]   y2,
    output wire [E3W-1:0]   y3,
    output wire [E5W-1:0]   y5,
    output wire [E7W-1:0]   y7,
    output wire [3:0]       bad_y
);
    wire [3:0] newly_saturated;
    wire [3:0] prior_bad;
    wire [3:0] combined_bad;
    wire nonzero_y;
    pa_binexp_compose_s4 #(
        .E2W(E2W),.T2(T2),.E3W(E3W),.T3(T3),
        .E5W(E5W),.T5(T5),.E7W(E7W),.T7(T7)
    ) u_compose(
        .zero_a(zero_a),.zero_b(zero_b),
        .a2(a2),.a3(a3),.a5(a5),.a7(a7),
        .b2(b2),.b3(b3),.b5(b5),.b7(b7),
        .zero_y(zero_y),.y2(y2),.y3(y3),.y5(y5),.y7(y7),
        .saturated(newly_saturated)
    );
    pa_not u_nonzero(.a(zero_y),.y(nonzero_y));
    generate
        for (genvar lane = 0; lane < 4; lane = lane + 1) begin : g_bad
            pa_or2 u_prior(.a(bad_a[lane]),.b(bad_b[lane]),.y(prior_bad[lane]));
            pa_or2 u_new(.a(prior_bad[lane]),.b(newly_saturated[lane]),.y(combined_bad[lane]));
            pa_and2 u_zero_exact(.a(combined_bad[lane]),.b(nonzero_y),.y(bad_y[lane]));
        end
    endgenerate
endmodule

module pa_binexp_cancel_s4 #(
    parameter integer E2W = 2,
    parameter integer E3W = 2,
    parameter integer E5W = 1,
    parameter integer E7W = 1
)(
    input  wire             zero_a,
    input  wire             zero_b,
    input  wire [E2W-1:0]   a2,
    input  wire [E3W-1:0]   a3,
    input  wire [E5W-1:0]   a5,
    input  wire [E7W-1:0]   a7,
    input  wire [E2W-1:0]   b2,
    input  wire [E3W-1:0]   b3,
    input  wire [E5W-1:0]   b5,
    input  wire [E7W-1:0]   b7,
    output wire             zero_y,
    output wire [E2W-1:0]   y2,
    output wire [E3W-1:0]   y3,
    output wire [E5W-1:0]   y5,
    output wire [E7W-1:0]   y7,
    output wire             exact
);
    wire [E2W-1:0] diff2;
    wire [E3W-1:0] diff3;
    wire [E5W-1:0] diff5;
    wire [E7W-1:0] diff7;
    wire [3:0] under;
    wire any_under;
    wire not_zero_a;
    wire active_under;
    wire invalid;
    wire [E2W-1:0] candidate2;
    wire [E3W-1:0] candidate3;
    wire [E5W-1:0] candidate5;
    wire [E7W-1:0] candidate7;
    wire [E2W-1:0] z2 = {E2W{1'b0}};
    wire [E3W-1:0] z3 = {E3W{1'b0}};
    wire [E5W-1:0] z5 = {E5W{1'b0}};
    wire [E7W-1:0] z7 = {E7W{1'b0}};

    (* pa_region = "lane2" *) pa_lane_cancel #(.EW(E2W)) u_l2(.a(a2), .b(b2), .difference(diff2), .underflow(under[0]));
    (* pa_region = "lane3" *) pa_lane_cancel #(.EW(E3W)) u_l3(.a(a3), .b(b3), .difference(diff3), .underflow(under[1]));
    (* pa_region = "lane5" *) pa_lane_cancel #(.EW(E5W)) u_l5(.a(a5), .b(b5), .difference(diff5), .underflow(under[2]));
    (* pa_region = "lane7" *) pa_lane_cancel #(.EW(E7W)) u_l7(.a(a7), .b(b7), .difference(diff7), .underflow(under[3]));
    pa_reduce_or #(.W(4)) u_any(.a(under), .y(any_under));
    pa_not u_nza(.a(zero_a), .y(not_zero_a));
    pa_and2 u_active(.a(not_zero_a), .b(any_under), .y(active_under));
    pa_or2 u_invalid(.a(zero_b), .b(active_under), .y(invalid));
    pa_not u_exact(.a(invalid), .y(exact));

    pa_mux_word #(.W(E2W)) u_zero2(.d0(diff2), .d1(z2), .sel(zero_a), .y(candidate2));
    pa_mux_word #(.W(E3W)) u_zero3(.d0(diff3), .d1(z3), .sel(zero_a), .y(candidate3));
    pa_mux_word #(.W(E5W)) u_zero5(.d0(diff5), .d1(z5), .sel(zero_a), .y(candidate5));
    pa_mux_word #(.W(E7W)) u_zero7(.d0(diff7), .d1(z7), .sel(zero_a), .y(candidate7));
    // Atomic rejection: every output field is restored from operand A.
    pa_mux_word #(.W(E2W)) u_atomic2(.d0(candidate2), .d1(a2), .sel(invalid), .y(y2));
    pa_mux_word #(.W(E3W)) u_atomic3(.d0(candidate3), .d1(a3), .sel(invalid), .y(y3));
    pa_mux_word #(.W(E5W)) u_atomic5(.d0(candidate5), .d1(a5), .sel(invalid), .y(y5));
    pa_mux_word #(.W(E7W)) u_atomic7(.d0(candidate7), .d1(a7), .sel(invalid), .y(y7));
    assign zero_y = zero_a;
endmodule

module pa_binexp_meet_s4 #(
    parameter integer E2W = 2,
    parameter integer E3W = 2,
    parameter integer E5W = 1,
    parameter integer E7W = 1
)(
    input wire zero_a, input wire zero_b,
    input wire [E2W-1:0] a2, input wire [E3W-1:0] a3,
    input wire [E5W-1:0] a5, input wire [E7W-1:0] a7,
    input wire [E2W-1:0] b2, input wire [E3W-1:0] b3,
    input wire [E5W-1:0] b5, input wire [E7W-1:0] b7,
    output wire zero_y,
    output wire [E2W-1:0] y2, output wire [E3W-1:0] y3,
    output wire [E5W-1:0] y5, output wire [E7W-1:0] y7
);
    wire [E2W-1:0] min2, max2_unused, za2;
    wire [E3W-1:0] min3, max3_unused, za3;
    wire [E5W-1:0] min5, max5_unused, za5;
    wire [E7W-1:0] min7, max7_unused, za7;
    (* pa_region = "lane2" *) pa_word_minmax #(.W(E2W)) u_l2(.a(a2), .b(b2), .minimum(min2), .maximum(max2_unused));
    (* pa_region = "lane3" *) pa_word_minmax #(.W(E3W)) u_l3(.a(a3), .b(b3), .minimum(min3), .maximum(max3_unused));
    (* pa_region = "lane5" *) pa_word_minmax #(.W(E5W)) u_l5(.a(a5), .b(b5), .minimum(min5), .maximum(max5_unused));
    (* pa_region = "lane7" *) pa_word_minmax #(.W(E7W)) u_l7(.a(a7), .b(b7), .minimum(min7), .maximum(max7_unused));
    pa_mux_word #(.W(E2W)) u_za2(.d0(min2), .d1(b2), .sel(zero_a), .y(za2));
    pa_mux_word #(.W(E3W)) u_za3(.d0(min3), .d1(b3), .sel(zero_a), .y(za3));
    pa_mux_word #(.W(E5W)) u_za5(.d0(min5), .d1(b5), .sel(zero_a), .y(za5));
    pa_mux_word #(.W(E7W)) u_za7(.d0(min7), .d1(b7), .sel(zero_a), .y(za7));
    pa_mux_word #(.W(E2W)) u_zb2(.d0(za2), .d1(a2), .sel(zero_b), .y(y2));
    pa_mux_word #(.W(E3W)) u_zb3(.d0(za3), .d1(a3), .sel(zero_b), .y(y3));
    pa_mux_word #(.W(E5W)) u_zb5(.d0(za5), .d1(a5), .sel(zero_b), .y(y5));
    pa_mux_word #(.W(E7W)) u_zb7(.d0(za7), .d1(a7), .sel(zero_b), .y(y7));
    pa_and2 u_zero(.a(zero_a), .b(zero_b), .y(zero_y));
endmodule

module pa_binexp_join_s4 #(
    parameter integer E2W = 2,
    parameter integer E3W = 2,
    parameter integer E5W = 1,
    parameter integer E7W = 1
)(
    input wire zero_a, input wire zero_b,
    input wire [E2W-1:0] a2, input wire [E3W-1:0] a3,
    input wire [E5W-1:0] a5, input wire [E7W-1:0] a7,
    input wire [E2W-1:0] b2, input wire [E3W-1:0] b3,
    input wire [E5W-1:0] b5, input wire [E7W-1:0] b7,
    output wire zero_y,
    output wire [E2W-1:0] y2, output wire [E3W-1:0] y3,
    output wire [E5W-1:0] y5, output wire [E7W-1:0] y7
);
    wire [E2W-1:0] min2_unused, max2;
    wire [E3W-1:0] min3_unused, max3;
    wire [E5W-1:0] min5_unused, max5;
    wire [E7W-1:0] min7_unused, max7;
    wire [E2W-1:0] z2 = {E2W{1'b0}};
    wire [E3W-1:0] z3 = {E3W{1'b0}};
    wire [E5W-1:0] z5 = {E5W{1'b0}};
    wire [E7W-1:0] z7 = {E7W{1'b0}};
    (* pa_region = "lane2" *) pa_word_minmax #(.W(E2W)) u_l2(.a(a2), .b(b2), .minimum(min2_unused), .maximum(max2));
    (* pa_region = "lane3" *) pa_word_minmax #(.W(E3W)) u_l3(.a(a3), .b(b3), .minimum(min3_unused), .maximum(max3));
    (* pa_region = "lane5" *) pa_word_minmax #(.W(E5W)) u_l5(.a(a5), .b(b5), .minimum(min5_unused), .maximum(max5));
    (* pa_region = "lane7" *) pa_word_minmax #(.W(E7W)) u_l7(.a(a7), .b(b7), .minimum(min7_unused), .maximum(max7));
    pa_or2 u_zero(.a(zero_a), .b(zero_b), .y(zero_y));
    pa_mux_word #(.W(E2W)) u_z2(.d0(max2), .d1(z2), .sel(zero_y), .y(y2));
    pa_mux_word #(.W(E3W)) u_z3(.d0(max3), .d1(z3), .sel(zero_y), .y(y3));
    pa_mux_word #(.W(E5W)) u_z5(.d0(max5), .d1(z5), .sel(zero_y), .y(y5));
    pa_mux_word #(.W(E7W)) u_z7(.d0(max7), .d1(z7), .sel(zero_y), .y(y7));
endmodule

module pa_binexp_divides_s4 #(
    parameter integer E2W = 2,
    parameter integer E3W = 2,
    parameter integer E5W = 1,
    parameter integer E7W = 1
)(
    input wire zero_a, input wire zero_b,
    input wire [E2W-1:0] a2, input wire [E3W-1:0] a3,
    input wire [E5W-1:0] a5, input wire [E7W-1:0] a7,
    input wire [E2W-1:0] b2, input wire [E3W-1:0] b3,
    input wire [E5W-1:0] b5, input wire [E7W-1:0] b7,
    output wire divides
);
    wire [3:0] lane_le;
    wire lanes_all;
    wire rhs_zero_case;
    (* pa_region = "lane2" *) pa_lane_le #(.EW(E2W)) u_l2(.a(a2), .b(b2), .less_or_equal(lane_le[0]));
    (* pa_region = "lane3" *) pa_lane_le #(.EW(E3W)) u_l3(.a(a3), .b(b3), .less_or_equal(lane_le[1]));
    (* pa_region = "lane5" *) pa_lane_le #(.EW(E5W)) u_l5(.a(a5), .b(b5), .less_or_equal(lane_le[2]));
    (* pa_region = "lane7" *) pa_lane_le #(.EW(E7W)) u_l7(.a(a7), .b(b7), .less_or_equal(lane_le[3]));
    pa_reduce_and #(.W(4)) u_all(.a(lane_le), .y(lanes_all));
    // For a|b: nonzero divides zero; zero divides only zero.
    pa_mux2 u_rhs_zero(.d0(lanes_all), .d1(1'b1), .sel(zero_b), .y(rhs_zero_case));
    pa_mux2 u_lhs_zero(.d0(rhs_zero_case), .d1(zero_b), .sel(zero_a), .y(divides));
endmodule

module pa_binexp_valuation_s4 #(
    parameter integer E2W = 2,
    parameter integer E3W = 2,
    parameter integer E5W = 1,
    parameter integer E7W = 1,
    parameter integer KW  = 3
)(
    input wire zero,
    input wire [E2W-1:0] e2, input wire [E3W-1:0] e3,
    input wire [E5W-1:0] e5, input wire [E7W-1:0] e7,
    input wire [1:0] prime_select,
    output wire [KW-1:0] exponent,
    output wire valid,
    output wire infinite
);
    wire [KW-1:0] e2x = {{(KW-E2W){1'b0}}, e2};
    wire [KW-1:0] e3x = {{(KW-E3W){1'b0}}, e3};
    wire [KW-1:0] e5x = {{(KW-E5W){1'b0}}, e5};
    wire [KW-1:0] e7x = {{(KW-E7W){1'b0}}, e7};
    wire [KW-1:0] low;
    wire [KW-1:0] high;
    wire [KW-1:0] selected;
    wire [KW-1:0] zero_exponent = {KW{1'b0}};
    pa_mux_word #(.W(KW)) u_low(.d0(e2x), .d1(e3x), .sel(prime_select[0]), .y(low));
    pa_mux_word #(.W(KW)) u_high(.d0(e5x), .d1(e7x), .sel(prime_select[0]), .y(high));
    pa_mux_word #(.W(KW)) u_pick(.d0(low), .d1(high), .sel(prime_select[1]), .y(selected));
    pa_mux_word #(.W(KW)) u_zero_sentinel(.d0(selected),.d1(zero_exponent),.sel(zero),.y(exponent));
    // The explicit infinity bit disambiguates zero from the exponent-0 value.
    // VALUATION is defined for zero and reports +infinity exactly.
    assign valid = 1'b1;
    assign infinite = zero;
endmodule

module pa_binexp_power_s4 #(
    parameter integer E2W = 2, parameter integer T2 = 3,
    parameter integer E3W = 2, parameter integer T3 = 2,
    parameter integer E5W = 1, parameter integer T5 = 1,
    parameter integer E7W = 1, parameter integer T7 = 1,
    parameter integer KW  = 3
)(
    input wire [1:0] prime_select,
    input wire [KW-1:0] exponent,
    output wire [E2W-1:0] y2, output wire [E3W-1:0] y3,
    output wire [E5W-1:0] y5, output wire [E7W-1:0] y7,
    output wire valid
);
    localparam [KW-1:0] C2 = T2;
    localparam [KW-1:0] C3 = T3;
    localparam [KW-1:0] C5 = T5;
    localparam [KW-1:0] C7 = T7;
    wire [3:0] cap_equal;
    wire [3:0] cap_less;
    wire [3:0] within_cap;
    wire within_low, within_high;
    wire sel0_n, sel1_n;
    wire [3:0] select;
    wire [E2W-1:0] k2 = exponent[E2W-1:0];
    wire [E3W-1:0] k3 = exponent[E3W-1:0];
    wire [E5W-1:0] k5 = exponent[E5W-1:0];
    wire [E7W-1:0] k7 = exponent[E7W-1:0];
    for (genvar i = 0; i < 4; i = i + 1) begin : g_within_or
        pa_or2 u_or(.a(cap_equal[i]), .b(cap_less[i]), .y(within_cap[i]));
    end
    pa_unsigned_compare #(.W(KW)) c2(.a(exponent), .b(C2), .equal(cap_equal[0]), .less_than(cap_less[0]));
    pa_unsigned_compare #(.W(KW)) c3(.a(exponent), .b(C3), .equal(cap_equal[1]), .less_than(cap_less[1]));
    pa_unsigned_compare #(.W(KW)) c5(.a(exponent), .b(C5), .equal(cap_equal[2]), .less_than(cap_less[2]));
    pa_unsigned_compare #(.W(KW)) c7(.a(exponent), .b(C7), .equal(cap_equal[3]), .less_than(cap_less[3]));
    pa_mux2 u_wlow(.d0(within_cap[0]), .d1(within_cap[1]), .sel(prime_select[0]), .y(within_low));
    pa_mux2 u_whigh(.d0(within_cap[2]), .d1(within_cap[3]), .sel(prime_select[0]), .y(within_high));
    pa_mux2 u_wpick(.d0(within_low), .d1(within_high), .sel(prime_select[1]), .y(valid));
    pa_not u_ns0(.a(prime_select[0]), .y(sel0_n));
    pa_not u_ns1(.a(prime_select[1]), .y(sel1_n));
    pa_and2 u_s0a(.a(sel1_n), .b(sel0_n), .y(select[0]));
    pa_and2 u_s1a(.a(sel1_n), .b(prime_select[0]), .y(select[1]));
    pa_and2 u_s2a(.a(prime_select[1]), .b(sel0_n), .y(select[2]));
    pa_and2 u_s3a(.a(prime_select[1]), .b(prime_select[0]), .y(select[3]));
    for (genvar j2 = 0; j2 < E2W; j2 = j2 + 1) begin : g_y2
        wire active;
        pa_and2 u_a(.a(select[0]), .b(valid), .y(active));
        pa_and2 u_k(.a(k2[j2]), .b(active), .y(y2[j2]));
    end
    for (genvar j3 = 0; j3 < E3W; j3 = j3 + 1) begin : g_y3
        wire active;
        pa_and2 u_a(.a(select[1]), .b(valid), .y(active));
        pa_and2 u_k(.a(k3[j3]), .b(active), .y(y3[j3]));
    end
    for (genvar j5 = 0; j5 < E5W; j5 = j5 + 1) begin : g_y5
        wire active;
        pa_and2 u_a(.a(select[2]), .b(valid), .y(active));
        pa_and2 u_k(.a(k5[j5]), .b(active), .y(y5[j5]));
    end
    for (genvar j7 = 0; j7 < E7W; j7 = j7 + 1) begin : g_y7
        wire active;
        pa_and2 u_a(.a(select[3]), .b(valid), .y(active));
        pa_and2 u_k(.a(k7[j7]), .b(active), .y(y7[j7]));
    end
endmodule

`default_nettype wire
