`default_nettype none

// Canonical thermometer bit k denotes exponent >= k+1.

module pa_therm_validate_lane #(
    parameter integer T = 3
)(
    input  wire [T-1:0] t,
    output wire         valid
);
    wire [T-1:0] violation;
    wire any_violation;
    assign violation[0] = 1'b0;
    generate
        for (genvar k = 1; k < T; k = k + 1) begin : g_order
            wire lower_not;
            pa_not  u_not(.a(t[k-1]), .y(lower_not));
            pa_and2 u_bad(.a(t[k]), .b(lower_not), .y(violation[k]));
        end
    endgenerate
    pa_reduce_or #(.W(T)) u_any(.a(violation), .y(any_violation));
    pa_not u_valid(.a(any_violation), .y(valid));
endmodule

module pa_therm_meet_lane #(
    parameter integer T = 3
)(
    input  wire [T-1:0] a,
    input  wire [T-1:0] b,
    output wire [T-1:0] y
);
    generate
        for (genvar k = 0; k < T; k = k + 1) begin : g_and
            pa_and2 u_and(.a(a[k]), .b(b[k]), .y(y[k]));
        end
    endgenerate
endmodule

module pa_therm_join_lane #(
    parameter integer T = 3
)(
    input  wire [T-1:0] a,
    input  wire [T-1:0] b,
    output wire [T-1:0] y
);
    generate
        for (genvar k = 0; k < T; k = k + 1) begin : g_or
            pa_or2 u_or(.a(a[k]), .b(b[k]), .y(y[k]));
        end
    endgenerate
endmodule

module pa_therm_divides_lane #(
    parameter integer T = 3
)(
    input  wire [T-1:0] a,
    input  wire [T-1:0] b,
    output wire         divides
);
    wire [T-1:0] implication;
    generate
        for (genvar k = 0; k < T; k = k + 1) begin : g_implies
            wire b_not;
            pa_not   u_not(.a(b[k]), .y(b_not));
            // NAND(a, !b) is exactly (!a | b).
            pa_nand2 u_imp(.a(a[k]), .b(b_not), .y(implication[k]));
        end
    endgenerate
    pa_reduce_and #(.W(T)) u_all(.a(implication), .y(divides));
endmodule

// Direct monotone threshold convolution. y[k] is the OR of every split
// (i, k+1-i) whose two threshold predicates establish ea+eb >= k+1.
module pa_therm_compose_lane #(
    parameter integer T = 3
)(
    input  wire [T-1:0] a,
    input  wire [T-1:0] b,
    output wire [T-1:0] y,
    output wire         saturated
);
    wire [T:0] term [0:T-1];
    wire [T:0] accum [0:T-1];
    generate
        for (genvar k = 0; k < T; k = k + 1) begin : g_threshold
            localparam integer K = k + 1;
            for (genvar i = 0; i <= T; i = i + 1) begin : g_split
                if (i > K) begin : g_unused
                    assign term[k][i] = 1'b0;
                end else if (i == 0) begin : g_left_identity
                    assign term[k][i] = b[K-1];
                end else if (i == K) begin : g_right_identity
                    assign term[k][i] = a[K-1];
                end else begin : g_pair
                    pa_and2 u_and(.a(a[i-1]), .b(b[K-i-1]), .y(term[k][i]));
                end
            end
            assign accum[k][0] = term[k][0];
            for (genvar j = 1; j <= T; j = j + 1) begin : g_fold
                if (j <= K) begin : g_used
                    pa_or2 u_or(.a(accum[k][j-1]), .b(term[k][j]), .y(accum[k][j]));
                end else begin : g_passthrough
                    assign accum[k][j] = accum[k][j-1];
                end
            end
            assign y[k] = accum[k][T];
        end
    endgenerate

    // One additional threshold (T+1) detects truncation beyond the lane cap.
    wire [T-1:0] overflow_term;
    wire [T-1:0] overflow_accum;
    generate
        for (genvar i = 1; i <= T; i = i + 1) begin : g_overflow_term
            pa_and2 u_and(.a(a[i-1]), .b(b[T-i]), .y(overflow_term[i-1]));
        end
    endgenerate
    assign overflow_accum[0] = overflow_term[0];
    generate
        for (genvar j = 1; j < T; j = j + 1) begin : g_overflow_fold
            pa_or2 u_or(.a(overflow_accum[j-1]), .b(overflow_term[j]), .y(overflow_accum[j]));
        end
    endgenerate
    assign saturated = overflow_accum[T-1];
endmodule

module pa_bin_to_therm_lane #(
    parameter integer EW = 2,
    parameter integer T  = 3
)(
    input  wire [EW-1:0] exponent,
    output wire [T-1:0]  thermometer,
    output wire          valid
);
    localparam [EW-1:0] CAP = T;
    wire cap_equal;
    wire below_cap;
    pa_unsigned_compare #(.W(EW)) u_cap(
        .a(exponent), .b(CAP), .equal(cap_equal), .less_than(below_cap)
    );
    pa_or2 u_valid(.a(cap_equal), .b(below_cap), .y(valid));
    generate
        for (genvar k = 0; k < T; k = k + 1) begin : g_threshold
            localparam [EW-1:0] K = k + 1;
            wire equal_k;
            wire below_k;
            wire below_not;
            pa_unsigned_compare #(.W(EW)) u_cmp(
                .a(exponent), .b(K), .equal(equal_k), .less_than(below_k)
            );
            pa_not u_ge(.a(below_k), .y(below_not));
            assign thermometer[k] = below_not;
        end
    endgenerate
endmodule

module pa_therm_to_bin_lane #(
    parameter integer T  = 3,
    parameter integer EW = 2
)(
    input  wire [T-1:0]  thermometer,
    output wire [EW-1:0] exponent,
    output wire          valid
);
    wire [EW-1:0] stage [0:T];
    wire [T-1:0] ignored_carry;
    assign stage[0] = {EW{1'b0}};
    generate
        for (genvar k = 0; k < T; k = k + 1) begin : g_count
            wire [EW-1:0] addend;
            assign addend = {{(EW-1){1'b0}}, thermometer[k]};
            pa_ripple_add #(.W(EW)) u_add(
                .a(stage[k]), .b(addend), .cin(1'b0),
                .sum(stage[k+1]), .cout(ignored_carry[k])
            );
        end
    endgenerate
    assign exponent = stage[T];
    pa_therm_validate_lane #(.T(T)) u_valid(.t(thermometer), .valid(valid));
endmodule

module pa_therm_validate_s4 #(
    parameter integer T2 = 3, parameter integer T3 = 2,
    parameter integer T5 = 1, parameter integer T7 = 1
)(
    input wire [T2-1:0] t2, input wire [T3-1:0] t3,
    input wire [T5-1:0] t5, input wire [T7-1:0] t7,
    output wire valid
);
    wire [3:0] lane_valid;
    (* pa_region = "lane2" *) pa_therm_validate_lane #(.T(T2)) u_l2(.t(t2), .valid(lane_valid[0]));
    (* pa_region = "lane3" *) pa_therm_validate_lane #(.T(T3)) u_l3(.t(t3), .valid(lane_valid[1]));
    (* pa_region = "lane5" *) pa_therm_validate_lane #(.T(T5)) u_l5(.t(t5), .valid(lane_valid[2]));
    (* pa_region = "lane7" *) pa_therm_validate_lane #(.T(T7)) u_l7(.t(t7), .valid(lane_valid[3]));
    pa_reduce_and #(.W(4)) u_all(.a(lane_valid), .y(valid));
endmodule

module pa_therm_meet_s4 #(
    parameter integer T2 = 3, parameter integer T3 = 2,
    parameter integer T5 = 1, parameter integer T7 = 1
)(
    input wire zero_a, input wire zero_b,
    input wire [T2-1:0] a2, input wire [T3-1:0] a3,
    input wire [T5-1:0] a5, input wire [T7-1:0] a7,
    input wire [T2-1:0] b2, input wire [T3-1:0] b3,
    input wire [T5-1:0] b5, input wire [T7-1:0] b7,
    output wire zero_y,
    output wire [T2-1:0] y2, output wire [T3-1:0] y3,
    output wire [T5-1:0] y5, output wire [T7-1:0] y7
);
    wire [T2-1:0] raw2, za2;
    wire [T3-1:0] raw3, za3;
    wire [T5-1:0] raw5, za5;
    wire [T7-1:0] raw7, za7;
    (* pa_region = "lane2" *) pa_therm_meet_lane #(.T(T2)) l2(.a(a2), .b(b2), .y(raw2));
    (* pa_region = "lane3" *) pa_therm_meet_lane #(.T(T3)) l3(.a(a3), .b(b3), .y(raw3));
    (* pa_region = "lane5" *) pa_therm_meet_lane #(.T(T5)) l5(.a(a5), .b(b5), .y(raw5));
    (* pa_region = "lane7" *) pa_therm_meet_lane #(.T(T7)) l7(.a(a7), .b(b7), .y(raw7));
    pa_mux_word #(.W(T2)) za_l2(.d0(raw2), .d1(b2), .sel(zero_a), .y(za2));
    pa_mux_word #(.W(T3)) za_l3(.d0(raw3), .d1(b3), .sel(zero_a), .y(za3));
    pa_mux_word #(.W(T5)) za_l5(.d0(raw5), .d1(b5), .sel(zero_a), .y(za5));
    pa_mux_word #(.W(T7)) za_l7(.d0(raw7), .d1(b7), .sel(zero_a), .y(za7));
    pa_mux_word #(.W(T2)) zb_l2(.d0(za2), .d1(a2), .sel(zero_b), .y(y2));
    pa_mux_word #(.W(T3)) zb_l3(.d0(za3), .d1(a3), .sel(zero_b), .y(y3));
    pa_mux_word #(.W(T5)) zb_l5(.d0(za5), .d1(a5), .sel(zero_b), .y(y5));
    pa_mux_word #(.W(T7)) zb_l7(.d0(za7), .d1(a7), .sel(zero_b), .y(y7));
    pa_and2 u_zero(.a(zero_a), .b(zero_b), .y(zero_y));
endmodule

module pa_therm_join_s4 #(
    parameter integer T2 = 3, parameter integer T3 = 2,
    parameter integer T5 = 1, parameter integer T7 = 1
)(
    input wire zero_a, input wire zero_b,
    input wire [T2-1:0] a2, input wire [T3-1:0] a3,
    input wire [T5-1:0] a5, input wire [T7-1:0] a7,
    input wire [T2-1:0] b2, input wire [T3-1:0] b3,
    input wire [T5-1:0] b5, input wire [T7-1:0] b7,
    output wire zero_y,
    output wire [T2-1:0] y2, output wire [T3-1:0] y3,
    output wire [T5-1:0] y5, output wire [T7-1:0] y7
);
    wire [T2-1:0] raw2, z2 = {T2{1'b0}};
    wire [T3-1:0] raw3, z3 = {T3{1'b0}};
    wire [T5-1:0] raw5, z5 = {T5{1'b0}};
    wire [T7-1:0] raw7, z7 = {T7{1'b0}};
    (* pa_region = "lane2" *) pa_therm_join_lane #(.T(T2)) l2(.a(a2), .b(b2), .y(raw2));
    (* pa_region = "lane3" *) pa_therm_join_lane #(.T(T3)) l3(.a(a3), .b(b3), .y(raw3));
    (* pa_region = "lane5" *) pa_therm_join_lane #(.T(T5)) l5(.a(a5), .b(b5), .y(raw5));
    (* pa_region = "lane7" *) pa_therm_join_lane #(.T(T7)) l7(.a(a7), .b(b7), .y(raw7));
    pa_or2 u_zero(.a(zero_a), .b(zero_b), .y(zero_y));
    pa_mux_word #(.W(T2)) z_l2(.d0(raw2), .d1(z2), .sel(zero_y), .y(y2));
    pa_mux_word #(.W(T3)) z_l3(.d0(raw3), .d1(z3), .sel(zero_y), .y(y3));
    pa_mux_word #(.W(T5)) z_l5(.d0(raw5), .d1(z5), .sel(zero_y), .y(y5));
    pa_mux_word #(.W(T7)) z_l7(.d0(raw7), .d1(z7), .sel(zero_y), .y(y7));
endmodule

module pa_therm_compose_s4 #(
    parameter integer T2 = 3, parameter integer T3 = 2,
    parameter integer T5 = 1, parameter integer T7 = 1
)(
    input wire zero_a, input wire zero_b,
    input wire [T2-1:0] a2, input wire [T3-1:0] a3,
    input wire [T5-1:0] a5, input wire [T7-1:0] a7,
    input wire [T2-1:0] b2, input wire [T3-1:0] b3,
    input wire [T5-1:0] b5, input wire [T7-1:0] b7,
    output wire zero_y,
    output wire [T2-1:0] y2, output wire [T3-1:0] y3,
    output wire [T5-1:0] y5, output wire [T7-1:0] y7,
    output wire [3:0] saturated
);
    wire [T2-1:0] raw2, z2 = {T2{1'b0}};
    wire [T3-1:0] raw3, z3 = {T3{1'b0}};
    wire [T5-1:0] raw5, z5 = {T5{1'b0}};
    wire [T7-1:0] raw7, z7 = {T7{1'b0}};
    wire [3:0] raw_sat;
    wire nonzero;
    (* pa_region = "lane2" *) pa_therm_compose_lane #(.T(T2)) l2(.a(a2), .b(b2), .y(raw2), .saturated(raw_sat[0]));
    (* pa_region = "lane3" *) pa_therm_compose_lane #(.T(T3)) l3(.a(a3), .b(b3), .y(raw3), .saturated(raw_sat[1]));
    (* pa_region = "lane5" *) pa_therm_compose_lane #(.T(T5)) l5(.a(a5), .b(b5), .y(raw5), .saturated(raw_sat[2]));
    (* pa_region = "lane7" *) pa_therm_compose_lane #(.T(T7)) l7(.a(a7), .b(b7), .y(raw7), .saturated(raw_sat[3]));
    pa_or2 u_zero(.a(zero_a), .b(zero_b), .y(zero_y));
    pa_not u_nonzero(.a(zero_y), .y(nonzero));
    pa_mux_word #(.W(T2)) z_l2(.d0(raw2), .d1(z2), .sel(zero_y), .y(y2));
    pa_mux_word #(.W(T3)) z_l3(.d0(raw3), .d1(z3), .sel(zero_y), .y(y3));
    pa_mux_word #(.W(T5)) z_l5(.d0(raw5), .d1(z5), .sel(zero_y), .y(y5));
    pa_mux_word #(.W(T7)) z_l7(.d0(raw7), .d1(z7), .sel(zero_y), .y(y7));
    generate
        for (genvar i = 0; i < 4; i = i + 1) begin : g_sat
            pa_and2 u_and(.a(raw_sat[i]), .b(nonzero), .y(saturated[i]));
        end
    endgenerate
endmodule

module pa_therm_divides_s4 #(
    parameter integer T2 = 3, parameter integer T3 = 2,
    parameter integer T5 = 1, parameter integer T7 = 1
)(
    input wire zero_a, input wire zero_b,
    input wire [T2-1:0] a2, input wire [T3-1:0] a3,
    input wire [T5-1:0] a5, input wire [T7-1:0] a7,
    input wire [T2-1:0] b2, input wire [T3-1:0] b3,
    input wire [T5-1:0] b5, input wire [T7-1:0] b7,
    output wire divides
);
    wire [3:0] lane_ok;
    wire lanes_all;
    wire rhs_zero_case;
    (* pa_region = "lane2" *) pa_therm_divides_lane #(.T(T2)) l2(.a(a2), .b(b2), .divides(lane_ok[0]));
    (* pa_region = "lane3" *) pa_therm_divides_lane #(.T(T3)) l3(.a(a3), .b(b3), .divides(lane_ok[1]));
    (* pa_region = "lane5" *) pa_therm_divides_lane #(.T(T5)) l5(.a(a5), .b(b5), .divides(lane_ok[2]));
    (* pa_region = "lane7" *) pa_therm_divides_lane #(.T(T7)) l7(.a(a7), .b(b7), .divides(lane_ok[3]));
    pa_reduce_and #(.W(4)) u_all(.a(lane_ok), .y(lanes_all));
    pa_mux2 u_rhs_zero(.d0(lanes_all), .d1(1'b1), .sel(zero_b), .y(rhs_zero_case));
    pa_mux2 u_lhs_zero(.d0(rhs_zero_case), .d1(zero_b), .sel(zero_a), .y(divides));
endmodule

module pa_s4_binexp_to_therm #(
    parameter integer E2W = 2, parameter integer T2 = 3,
    parameter integer E3W = 2, parameter integer T3 = 2,
    parameter integer E5W = 1, parameter integer T5 = 1,
    parameter integer E7W = 1, parameter integer T7 = 1
)(
    input wire [E2W-1:0] e2, input wire [E3W-1:0] e3,
    input wire [E5W-1:0] e5, input wire [E7W-1:0] e7,
    output wire [T2-1:0] t2, output wire [T3-1:0] t3,
    output wire [T5-1:0] t5, output wire [T7-1:0] t7,
    output wire valid
);
    wire [3:0] lane_valid;
    (* pa_region = "lane2" *) pa_bin_to_therm_lane #(.EW(E2W), .T(T2)) l2(.exponent(e2), .thermometer(t2), .valid(lane_valid[0]));
    (* pa_region = "lane3" *) pa_bin_to_therm_lane #(.EW(E3W), .T(T3)) l3(.exponent(e3), .thermometer(t3), .valid(lane_valid[1]));
    (* pa_region = "lane5" *) pa_bin_to_therm_lane #(.EW(E5W), .T(T5)) l5(.exponent(e5), .thermometer(t5), .valid(lane_valid[2]));
    (* pa_region = "lane7" *) pa_bin_to_therm_lane #(.EW(E7W), .T(T7)) l7(.exponent(e7), .thermometer(t7), .valid(lane_valid[3]));
    pa_reduce_and #(.W(4)) u_all(.a(lane_valid), .y(valid));
endmodule

module pa_s4_therm_to_binexp #(
    parameter integer E2W = 2, parameter integer T2 = 3,
    parameter integer E3W = 2, parameter integer T3 = 2,
    parameter integer E5W = 1, parameter integer T5 = 1,
    parameter integer E7W = 1, parameter integer T7 = 1
)(
    input wire [T2-1:0] t2, input wire [T3-1:0] t3,
    input wire [T5-1:0] t5, input wire [T7-1:0] t7,
    output wire [E2W-1:0] e2, output wire [E3W-1:0] e3,
    output wire [E5W-1:0] e5, output wire [E7W-1:0] e7,
    output wire valid
);
    wire [3:0] lane_valid;
    (* pa_region = "lane2" *) pa_therm_to_bin_lane #(.T(T2), .EW(E2W)) l2(.thermometer(t2), .exponent(e2), .valid(lane_valid[0]));
    (* pa_region = "lane3" *) pa_therm_to_bin_lane #(.T(T3), .EW(E3W)) l3(.thermometer(t3), .exponent(e3), .valid(lane_valid[1]));
    (* pa_region = "lane5" *) pa_therm_to_bin_lane #(.T(T5), .EW(E5W)) l5(.thermometer(t5), .exponent(e5), .valid(lane_valid[2]));
    (* pa_region = "lane7" *) pa_therm_to_bin_lane #(.T(T7), .EW(E7W)) l7(.thermometer(t7), .exponent(e7), .valid(lane_valid[3]));
    pa_reduce_and #(.W(4)) u_all(.a(lane_valid), .y(valid));
endmodule

`default_nettype wire
