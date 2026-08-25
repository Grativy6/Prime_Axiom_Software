`default_nettype none

// Named width wrappers freeze elaboration identities in receipts. The generic
// cores remain the single implementation; these wrappers do not add logic.

`define PA_BINARY_WRAPPERS(S, WIDTH) \
module pa_bin_add_w``S(input wire [WIDTH-1:0] a, input wire [WIDTH-1:0] b, output wire [WIDTH-1:0] y, output wire carry); \
    wire ignored_borrow; \
    pa_addsub #(.W(WIDTH)) u(.a(a), .b(b), .subtract(1'b0), .y(y), .carry_out(carry), .borrow_out(ignored_borrow)); \
endmodule \
module pa_bin_sub_w``S(input wire [WIDTH-1:0] a, input wire [WIDTH-1:0] b, output wire [WIDTH-1:0] y, output wire borrow); \
    wire ignored_carry; \
    pa_addsub #(.W(WIDTH)) u(.a(a), .b(b), .subtract(1'b1), .y(y), .carry_out(ignored_carry), .borrow_out(borrow)); \
endmodule \
module pa_bin_compare_w``S(input wire [WIDTH-1:0] a, input wire [WIDTH-1:0] b, output wire equal, output wire less_than); \
    pa_unsigned_compare #(.W(WIDTH)) u(.a(a), .b(b), .equal(equal), .less_than(less_than)); \
endmodule \
module pa_bin_mul_w``S(input wire [WIDTH-1:0] a, input wire [WIDTH-1:0] b, output wire [2*WIDTH-1:0] product); \
    pa_shift_add_multiplier #(.W(WIDTH)) u(.a(a), .b(b), .product(product)); \
endmodule \
module pa_bin_fu_w``S(input wire [WIDTH-1:0] a, input wire [WIDTH-1:0] b, input wire [1:0] opcode, output wire [2*WIDTH-1:0] result, output wire [3:0] status); \
    pa_bin_fu #(.W(WIDTH)) u(.a(a), .b(b), .opcode(opcode), .result(result), .status(status)); \
endmodule \
module pa_bin_counter_w``S(input wire clk, input wire reset, input wire enable, output wire [WIDTH-1:0] count, output wire overflow); \
    pa_binary_counter #(.W(WIDTH)) u(.clk(clk), .reset(reset), .enable(enable), .count(count), .overflow(overflow)); \
endmodule \
module pa_bin_fu_registered_w``S(input wire clk, input wire reset, input wire load_operands, input wire commit_result, input wire [WIDTH-1:0] a_in, input wire [WIDTH-1:0] b_in, input wire [1:0] opcode_in, output wire [2*WIDTH-1:0] result_q, output wire [3:0] status_q); \
    pa_bin_fu_registered #(.W(WIDTH)) u(.clk(clk), .reset(reset), .load_operands(load_operands), .commit_result(commit_result), .a_in(a_in), .b_in(b_in), .opcode_in(opcode_in), .result_q(result_q), .status_q(status_q)); \
endmodule

`PA_BINARY_WRAPPERS(4, 4)
`PA_BINARY_WRAPPERS(6, 6)
`PA_BINARY_WRAPPERS(8, 8)
`undef PA_BINARY_WRAPPERS

// The raw operation wrappers are one-shot leaf measurement tops.  They do not
// define a resident-state re-entry policy.  Only `checked_compose` below is a
// chainable state adapter; its persistent bad tags prevent a clamped result
// from being reintroduced as an exact exponent state.
`define PA_BINEXP_WRAPPERS(S, E2, C2, E3, C3, E5, C5, E7, C7, K) \
module pa_binexp_compose_w``S(input wire zero_a, input wire zero_b, input wire [E2-1:0] a2, input wire [E3-1:0] a3, input wire [E5-1:0] a5, input wire [E7-1:0] a7, input wire [E2-1:0] b2, input wire [E3-1:0] b3, input wire [E5-1:0] b5, input wire [E7-1:0] b7, output wire zero_y, output wire [E2-1:0] y2, output wire [E3-1:0] y3, output wire [E5-1:0] y5, output wire [E7-1:0] y7, output wire [3:0] saturated); \
    pa_binexp_compose_s4 #(.E2W(E2),.T2(C2),.E3W(E3),.T3(C3),.E5W(E5),.T5(C5),.E7W(E7),.T7(C7)) u(.*); \
endmodule \
module pa_binexp_checked_compose_w``S(input wire zero_a, input wire zero_b, input wire [E2-1:0] a2, input wire [E3-1:0] a3, input wire [E5-1:0] a5, input wire [E7-1:0] a7, input wire [E2-1:0] b2, input wire [E3-1:0] b3, input wire [E5-1:0] b5, input wire [E7-1:0] b7, input wire [3:0] bad_a, input wire [3:0] bad_b, output wire zero_y, output wire [E2-1:0] y2, output wire [E3-1:0] y3, output wire [E5-1:0] y5, output wire [E7-1:0] y7, output wire [3:0] bad_y); \
    pa_binexp_checked_compose_s4 #(.E2W(E2),.T2(C2),.E3W(E3),.T3(C3),.E5W(E5),.T5(C5),.E7W(E7),.T7(C7)) u(.*); \
endmodule \
module pa_binexp_cancel_w``S(input wire zero_a, input wire zero_b, input wire [E2-1:0] a2, input wire [E3-1:0] a3, input wire [E5-1:0] a5, input wire [E7-1:0] a7, input wire [E2-1:0] b2, input wire [E3-1:0] b3, input wire [E5-1:0] b5, input wire [E7-1:0] b7, output wire zero_y, output wire [E2-1:0] y2, output wire [E3-1:0] y3, output wire [E5-1:0] y5, output wire [E7-1:0] y7, output wire exact); \
    pa_binexp_cancel_s4 #(.E2W(E2),.E3W(E3),.E5W(E5),.E7W(E7)) u(.*); \
endmodule \
module pa_binexp_meet_w``S(input wire zero_a, input wire zero_b, input wire [E2-1:0] a2, input wire [E3-1:0] a3, input wire [E5-1:0] a5, input wire [E7-1:0] a7, input wire [E2-1:0] b2, input wire [E3-1:0] b3, input wire [E5-1:0] b5, input wire [E7-1:0] b7, output wire zero_y, output wire [E2-1:0] y2, output wire [E3-1:0] y3, output wire [E5-1:0] y5, output wire [E7-1:0] y7); \
    pa_binexp_meet_s4 #(.E2W(E2),.E3W(E3),.E5W(E5),.E7W(E7)) u(.*); \
endmodule \
module pa_binexp_join_w``S(input wire zero_a, input wire zero_b, input wire [E2-1:0] a2, input wire [E3-1:0] a3, input wire [E5-1:0] a5, input wire [E7-1:0] a7, input wire [E2-1:0] b2, input wire [E3-1:0] b3, input wire [E5-1:0] b5, input wire [E7-1:0] b7, output wire zero_y, output wire [E2-1:0] y2, output wire [E3-1:0] y3, output wire [E5-1:0] y5, output wire [E7-1:0] y7); \
    pa_binexp_join_s4 #(.E2W(E2),.E3W(E3),.E5W(E5),.E7W(E7)) u(.*); \
endmodule \
module pa_binexp_divides_w``S(input wire zero_a, input wire zero_b, input wire [E2-1:0] a2, input wire [E3-1:0] a3, input wire [E5-1:0] a5, input wire [E7-1:0] a7, input wire [E2-1:0] b2, input wire [E3-1:0] b3, input wire [E5-1:0] b5, input wire [E7-1:0] b7, output wire divides); \
    pa_binexp_divides_s4 #(.E2W(E2),.E3W(E3),.E5W(E5),.E7W(E7)) u(.*); \
endmodule \
module pa_binexp_valuation_w``S(input wire zero, input wire [E2-1:0] e2, input wire [E3-1:0] e3, input wire [E5-1:0] e5, input wire [E7-1:0] e7, input wire [1:0] prime_select, output wire [K-1:0] exponent, output wire valid, output wire infinite); \
    pa_binexp_valuation_s4 #(.E2W(E2),.E3W(E3),.E5W(E5),.E7W(E7),.KW(K)) u(.*); \
endmodule \
module pa_binexp_power_w``S(input wire [1:0] prime_select, input wire [K-1:0] exponent, output wire [E2-1:0] y2, output wire [E3-1:0] y3, output wire [E5-1:0] y5, output wire [E7-1:0] y7, output wire valid); \
    pa_binexp_power_s4 #(.E2W(E2),.T2(C2),.E3W(E3),.T3(C3),.E5W(E5),.T5(C5),.E7W(E7),.T7(C7),.KW(K)) u(.*); \
endmodule

`PA_BINEXP_WRAPPERS(4, 2,3, 2,2, 1,1, 1,1, 2)
`PA_BINEXP_WRAPPERS(6, 3,5, 2,3, 2,2, 2,2, 3)
`PA_BINEXP_WRAPPERS(8, 3,7, 3,5, 2,3, 2,2, 3)
`undef PA_BINEXP_WRAPPERS

`define PA_THERM_WRAPPERS(S, E2, C2, E3, C3, E5, C5, E7, C7) \
module pa_therm_compose_w``S(input wire zero_a, input wire zero_b, input wire [C2-1:0] a2, input wire [C3-1:0] a3, input wire [C5-1:0] a5, input wire [C7-1:0] a7, input wire [C2-1:0] b2, input wire [C3-1:0] b3, input wire [C5-1:0] b5, input wire [C7-1:0] b7, output wire zero_y, output wire [C2-1:0] y2, output wire [C3-1:0] y3, output wire [C5-1:0] y5, output wire [C7-1:0] y7, output wire [3:0] saturated); \
    pa_therm_compose_s4 #(.T2(C2),.T3(C3),.T5(C5),.T7(C7)) u(.*); \
endmodule \
module pa_therm_meet_w``S(input wire zero_a, input wire zero_b, input wire [C2-1:0] a2, input wire [C3-1:0] a3, input wire [C5-1:0] a5, input wire [C7-1:0] a7, input wire [C2-1:0] b2, input wire [C3-1:0] b3, input wire [C5-1:0] b5, input wire [C7-1:0] b7, output wire zero_y, output wire [C2-1:0] y2, output wire [C3-1:0] y3, output wire [C5-1:0] y5, output wire [C7-1:0] y7); \
    pa_therm_meet_s4 #(.T2(C2),.T3(C3),.T5(C5),.T7(C7)) u(.*); \
endmodule \
module pa_therm_join_w``S(input wire zero_a, input wire zero_b, input wire [C2-1:0] a2, input wire [C3-1:0] a3, input wire [C5-1:0] a5, input wire [C7-1:0] a7, input wire [C2-1:0] b2, input wire [C3-1:0] b3, input wire [C5-1:0] b5, input wire [C7-1:0] b7, output wire zero_y, output wire [C2-1:0] y2, output wire [C3-1:0] y3, output wire [C5-1:0] y5, output wire [C7-1:0] y7); \
    pa_therm_join_s4 #(.T2(C2),.T3(C3),.T5(C5),.T7(C7)) u(.*); \
endmodule \
module pa_therm_divides_w``S(input wire zero_a, input wire zero_b, input wire [C2-1:0] a2, input wire [C3-1:0] a3, input wire [C5-1:0] a5, input wire [C7-1:0] a7, input wire [C2-1:0] b2, input wire [C3-1:0] b3, input wire [C5-1:0] b5, input wire [C7-1:0] b7, output wire divides); \
    pa_therm_divides_s4 #(.T2(C2),.T3(C3),.T5(C5),.T7(C7)) u(.*); \
endmodule \
module pa_therm_validate_w``S(input wire [C2-1:0] t2, input wire [C3-1:0] t3, input wire [C5-1:0] t5, input wire [C7-1:0] t7, output wire valid); \
    pa_therm_validate_s4 #(.T2(C2),.T3(C3),.T5(C5),.T7(C7)) u(.*); \
endmodule \
module pa_bin_to_therm_w``S(input wire [E2-1:0] e2, input wire [E3-1:0] e3, input wire [E5-1:0] e5, input wire [E7-1:0] e7, output wire [C2-1:0] t2, output wire [C3-1:0] t3, output wire [C5-1:0] t5, output wire [C7-1:0] t7, output wire valid); \
    pa_s4_binexp_to_therm #(.E2W(E2),.T2(C2),.E3W(E3),.T3(C3),.E5W(E5),.T5(C5),.E7W(E7),.T7(C7)) u(.*); \
endmodule \
module pa_therm_to_bin_w``S(input wire [C2-1:0] t2, input wire [C3-1:0] t3, input wire [C5-1:0] t5, input wire [C7-1:0] t7, output wire [E2-1:0] e2, output wire [E3-1:0] e3, output wire [E5-1:0] e5, output wire [E7-1:0] e7, output wire valid); \
    pa_s4_therm_to_binexp #(.E2W(E2),.T2(C2),.E3W(E3),.T3(C3),.E5W(E5),.T5(C5),.E7W(E7),.T7(C7)) u(.*); \
endmodule

`PA_THERM_WRAPPERS(4, 2,3, 2,2, 1,1, 1,1)
`PA_THERM_WRAPPERS(6, 3,5, 2,3, 2,2, 2,2)
`PA_THERM_WRAPPERS(8, 3,7, 3,5, 2,3, 2,2)
`undef PA_THERM_WRAPPERS

`define PA_SIDECAR_WRAPPERS(S, WIDTH, C2, C3, C5, C7) \
module pa_cold_encode_w``S(input wire [WIDTH-1:0] magnitude, output wire zero, output wire valid, output wire [C2-1:0] t2, output wire [C3-1:0] t3, output wire [C5-1:0] t5, output wire [C7-1:0] t7); \
    pa_cold_encode_s4 #(.W(WIDTH),.T2(C2),.T3(C3),.T5(C5),.T7(C7)) u(.*); \
endmodule \
module pa_vsc_query_w``S(input wire valid, input wire [C2-1:0] t2, input wire [C3-1:0] t3, input wire [C5-1:0] t5, input wire [C7-1:0] t7, input wire [1:0] prime_select, output wire predicate, output wire rejected); \
    pa_vsc_query_s4 #(.T2(C2),.T3(C3),.T5(C5),.T7(C7)) u(.*); \
endmodule \
module pa_bin_vsc_w``S(input wire [WIDTH-1:0] magnitude, input wire sidecar_valid, input wire [C2-1:0] t2, input wire [C3-1:0] t3, input wire [C5-1:0] t5, input wire [C7-1:0] t7, input wire [1:0] operation, input wire [1:0] prime_select, output wire [WIDTH-1:0] magnitude_y, output wire valid_y, output wire [C2-1:0] y2, output wire [C3-1:0] y3, output wire [C5-1:0] y5, output wire [C7-1:0] y7, output wire predicate, output wire rejected, output wire overflow); \
    pa_bin_vsc_s4 #(.W(WIDTH),.T2(C2),.T3(C3),.T5(C5),.T7(C7)) u(.*); \
endmodule

`PA_SIDECAR_WRAPPERS(4, 4, 3,2,1,1)
`PA_SIDECAR_WRAPPERS(6, 6, 5,3,2,2)
`PA_SIDECAR_WRAPPERS(8, 8, 7,5,3,2)
`undef PA_SIDECAR_WRAPPERS

`default_nettype wire
